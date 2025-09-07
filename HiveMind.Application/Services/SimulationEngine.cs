using HiveMind.Application.Interfaces;
using HiveMind.Application.Models;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace HiveMind.Application.Services
{
  /// <summary>
  /// Main simulation engine implementation
  /// </summary>
  public class SimulationEngine : ISimulationEngine, IDisposable
  {
    private readonly List<IColony> _colonies;
    private readonly Timer _simulationTimer;
    private readonly Stopwatch _performanceTimer;
    private readonly IEnvironmentFactory _environmentFactory;
    private readonly ILogger<SimulationEngine> _logger;

    private SimulationState _state = SimulationState.Uninitialized;
    private SimulationConfiguration? _configuration;
    private IEnvironment? _environment;
    private ISimulationContext? _context;
    private long _currentTick = 0;
    private DateTime _simulationStartTime;
    private bool _disposed = false;

    private int _totalBirthCount = 0;
    private int _totalDeathCount = 0;
    private int _lastTickPopulation = 0;
    private readonly Dictionary<Guid, bool> _antLifeTracker = [];

    public SimulationState State => _state;
    public long CurrentTick => _currentTick;
    public IReadOnlyCollection<IColony> Colonies => _colonies.AsReadOnly();
    public IEnvironment Environment => _environment ?? throw new InvalidOperationException("Simulation not initialized");

    public event EventHandler<SimulationStateChangedEventArgs>? StateChanged;
    public event EventHandler<SimulationTickEventArgs>? Tick;

    public SimulationEngine(IEnvironmentFactory environmentFactory, ILogger<SimulationEngine> logger)
    {
      _colonies = [];
      _performanceTimer = new();
      _simulationTimer = new(SimulationTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
      _environmentFactory = environmentFactory ?? throw new ArgumentNullException(nameof(environmentFactory));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task InitializeAsync(SimulationConfiguration config)
    {
      if (_state != SimulationState.Uninitialized && _state != SimulationState.Stopped)
        throw new InvalidOperationException($"Cannot initialize simulation in state: {_state}");

      _configuration = config ?? throw new ArgumentNullException(nameof(config));

      // Initialize environment
      _environment = _environmentFactory.CreateEnvironment(
        _configuration.EnvironmentWidth,
        _configuration.EnvironmentHeight,
        _configuration.InitialFoodSources,
        _configuration.RandomSeed
      );

      // Initialize simulation context
      _context = new SimulationContext(_environment, _configuration.DeltaTime, _configuration.RandomSeed);

      // Create initial colony
      AntColony colony = new(_configuration.ColonyPosition, _configuration.MaxColonyPopulation);
      _colonies.Add(colony);
      colony.Initialize(_context);

      ValidateColoniesInitialized();

      // Reset counters
      _currentTick = 0;

      // Reset statistics tracking
      _totalBirthCount = 0;
      _totalDeathCount = 0;
      _lastTickPopulation = 0;
      _antLifeTracker.Clear();

      // Initialize tracking with starting population
      IEnumerable<Ant> allAnts = _colonies.SelectMany(c => c.Members).OfType<Ant>();
      foreach (Ant ant in allAnts)
      {
        if (ant.IsAlive)
        {
          _antLifeTracker[ant.Id] = true;
          _totalBirthCount++; // Count initial population as births
        }
      }
      _lastTickPopulation = _antLifeTracker.Count;

      SetState(SimulationState.Initialized);
      _logger.LogInformation("Simulation initialized with {ColonyCount} colonies and {InitialPopulation} initial ants", _colonies.Count, _lastTickPopulation);

      return Task.CompletedTask;
    }

    /// <summary>
    /// Validate all colonies are initialized before starting
    /// </summary>
    private void ValidateColoniesInitialized()
    {
      List<AntColony> uninitializedColonies = [.. _colonies.OfType<AntColony>().Where(c => !c.IsInitialized)];
      if (uninitializedColonies.Count != 0)
        throw new InvalidOperationException($"Found {uninitializedColonies.Count} uninitialized colonies. " +
        "All colonies must be initialized before starting simulation.");
    }

    public Task StartAsync()
    {
      if (_state != SimulationState.Initialized && _state != SimulationState.Paused)
        throw new InvalidOperationException($"Cannot start simulation in state: {_state}");

      if (_configuration == null)
        throw new InvalidOperationException("Simulation not properly initialized");

      int intervalMs = (int)(1000.0 / _configuration.TargetTPS);
      _simulationTimer.Change(0, intervalMs);
      _simulationStartTime = DateTime.UtcNow;
      _performanceTimer.Start();

      SetState(SimulationState.Running);
      _logger.LogInformation("Simulation started with target {TPS} TPS", _configuration.TargetTPS);

      return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
      if (_state != SimulationState.Running)
        throw new InvalidOperationException($"Cannot pause simulation in state: {_state}");

      _simulationTimer.Change(Timeout.Infinite, Timeout.Infinite);
      _performanceTimer.Stop();

      SetState(SimulationState.Paused);
      _logger.LogInformation("Simulation paused at tick {Tick}", _currentTick);

      return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
      if (_state == SimulationState.Uninitialized || _state == SimulationState.Stopped)
        return;

      // Stop the timer
      _simulationTimer.Change(Timeout.Infinite, Timeout.Infinite);

      // Wait a brief moment for any ongoing timer callbacks to complete
      await Task.Delay(50);

      _performanceTimer.Stop();

      SetState(SimulationState.Stopped);
      _logger.LogInformation("Simulation stopped at tick {Tick}", _currentTick);
    }

    public Task StepAsync()
    {
      if (_state != SimulationState.Initialized && _state != SimulationState.Paused)
        throw new InvalidOperationException($"Cannot step simulation in state: {_state}");

      ExecuteSimulationStep();

      return Task.CompletedTask;
    }

    private void SimulationTimerCallback(object? state)
    {
      if (_disposed || _state != SimulationState.Running)
        return;

      try
      {
        ExecuteSimulationStep();
      }
      catch (ObjectDisposedException)
      {
        return; // Timer was disposed during execution - this is expected during shutdown
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error during simulation step");
        SetState(SimulationState.Error);
      }
    }

    private void ExecuteSimulationStep()
    {
      if (_context == null || _configuration == null)
        throw new InvalidOperationException("Simulation not properly initialized");

      Stopwatch stepWatch = Stopwatch.StartNew();

      // Update simulation context
      ((SimulationContext)_context).UpdateTick(_currentTick);

      // Update all colonies
      List<IColony> activeColonies = [.. _colonies.Where(c => c.IsActive)];
      foreach (IColony colony in activeColonies)
        colony.Update(_context);

      // Remove inactive colonies
      _colonies.RemoveAll(c => !c.IsActive);

      // Update environment
      Environment.Update(_context);

      // Track births and deaths
      UpdateBirthDeathStatistics();

      _currentTick++;
      stepWatch.Stop();

      // Generate statistics
      SimulationStatistics statistics = GenerateStatistics();

      // Fire tick event
      Tick?.Invoke(this, new SimulationTickEventArgs(_currentTick, _configuration.DeltaTime, statistics));

      // Check termination conditions
      if (_configuration.MaxTicks > 0 && _currentTick >= _configuration.MaxTicks)
        Task.Run(StopAsync); // Use Task.Run to avoid blocking the timer thread
      else if (_colonies.Count == 0)
      {
        _logger.LogInformation("All colonies extinct - stopping simulation");
        Task.Run(StopAsync);
      }
    }

    private void UpdateBirthDeathStatistics()
    {
      HashSet<Guid> currentAliveCounts = [];
      HashSet<Guid> currentAntIds = [];

      // Single iteration through all ants
      foreach (IColony colony in _colonies)
        foreach (IInsect member in colony.Members)
          if (member is Ant ant)
          {
            currentAntIds.Add(ant.Id);

            if (ant.IsAlive)
            {
              currentAliveCounts.Add(ant.Id);

              // If this ant wasn't tracked before, it's a birth
              if (!_antLifeTracker.ContainsKey(ant.Id))
              {
                _antLifeTracker[ant.Id] = true;
                _totalBirthCount++;
              }
            }
            else
            {
              // If this ant was alive last tick but is dead now, it's a death
              if (_antLifeTracker.TryGetValue(ant.Id, out bool wasAlive) && wasAlive)
              {
                _antLifeTracker[ant.Id] = false;
                _totalDeathCount++;
              }
            }
          }

      // Clean up tracking for ants that no longer exist (were removed from colonies)
      List<Guid> antsToRemove = [];
      foreach (Guid trackedAntId in _antLifeTracker.Keys)
        if (!currentAntIds.Contains(trackedAntId))
        {
          if (_antLifeTracker[trackedAntId])
            _totalDeathCount++; // Was alive, now gone = death

          antsToRemove.Add(trackedAntId);
        }

      // Remove in separate loop to avoid modification during iteration
      foreach (Guid antId in antsToRemove)
        _antLifeTracker.Remove(antId);

      _lastTickPopulation = currentAliveCounts.Count;
    }

    private SimulationStatistics GenerateStatistics()
    {
      // Single-pass statistics collection
      int totalPopulation = 0;
      double totalEnergy = 0.0;
      Dictionary<string, int> populationByRole = [];

      // Single iteration through all ants
      foreach (IColony colony in _colonies)
        foreach (IInsect member in colony.Members)
        {
          if (member is Ant ant && ant.IsAlive)
          {
            totalPopulation++;
            totalEnergy += ant.Energy;

            // Update role counts
            string roleKey = ant.Role.ToString();
            populationByRole[roleKey] = populationByRole.GetValueOrDefault(roleKey, 0) + 1;
          }
        }

      return new SimulationStatistics
      {
        CurrentTick = _currentTick,
        TotalPopulation = totalPopulation,
        ActiveColonies = _colonies.Count(c => c.IsActive),
        TotalFoodStored = _colonies.Sum(c => c.TotalFoodStored),
        AvgEnergyLevel = totalPopulation > 0 ? totalEnergy / totalPopulation : 0,
        BirthCount = _totalBirthCount,
        DeathCount = _totalDeathCount,
        PopulationByRole = populationByRole,
        SimulationTimeElapsed = _performanceTimer.Elapsed.TotalSeconds
      };
    }

    private void SetState(SimulationState newState)
    {
      if (_state != newState)
      {
        SimulationState previousState = _state;
        _state = newState;
        StateChanged?.Invoke(this, new SimulationStateChangedEventArgs(previousState, newState));
      }
    }

    public void Dispose()
    {
      if (!_disposed)
      {
        // Stop the simulation first to prevent race conditions
        if (_state == SimulationState.Running || _state == SimulationState.Paused)
        {
          try
          {
            StopAsync().Wait(TimeSpan.FromSeconds(5)); // Wait max 5 seconds for clean shutdown
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Error during simulation shutdown in Dispose");
          }
        }

        // Dispose timer safely
        try
        {
          _simulationTimer.Dispose();
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Error disposing simulation timer");
        }

        _antLifeTracker.Clear();
        _performanceTimer?.Stop();
        _disposed = true;

        GC.SuppressFinalize(this);
      }
    }
  }
}
