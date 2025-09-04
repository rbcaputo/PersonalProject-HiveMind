using HiveMind.Application.Interfaces;
using HiveMind.Application.Models;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Interfaces;
using HiveMind.Infrastructure.Environment;
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
    private readonly ILogger<SimulationEngine>? _logger;

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

    public SimulationEngine(ILogger<SimulationEngine>? logger = null)
    {
      _colonies = [];
      _performanceTimer = new();
      _logger = logger;
      _simulationTimer = new(SimulationTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public Task InitializeAsync(SimulationConfiguration config)
    {
      if (_state != SimulationState.Uninitialized && _state != SimulationState.Stopped)
        throw new InvalidOperationException($"Cannot initialize simulation in state: {_state}");

      _configuration = config ?? throw new ArgumentNullException(nameof(config));

      // Initialize environment
      _environment = new SimulationEnvironment(
        _configuration.EnvironmentWidth,
        _configuration.EnvironmentHeight,
        _configuration.InitialFoodSources,
        _configuration.RandomSeed
      );

      // Initialize simulation context
      _context = new SimulationContext(_environment, _configuration.DeltaTime, _configuration.RandomSeed);

      // Create initial colony
      var colony = new AntColony(_configuration.ColonyPosition, _configuration.MaxColonyPopulation);
      _colonies.Add(colony);

      // Reset counters
      _currentTick = 0;

      // Reset statistics tracking
      _totalBirthCount = 0;
      _totalDeathCount = 0;
      _lastTickPopulation = 0;
      _antLifeTracker.Clear();

      // Initialize tracking with starting population
      var allAnts = _colonies.SelectMany(c => c.Members).OfType<Ant>();
      foreach (var ant in allAnts)
      {
        if (ant.IsAlive)
        {
          _antLifeTracker[ant.Id] = true;
          _totalBirthCount++; // Count initial population as births
        }
      }
      _lastTickPopulation = _antLifeTracker.Count;

      SetState(SimulationState.Initialized);
      _logger?.LogInformation("Simulation initialized with {ColonyCount} colonies and {InitialPopulation} initial ants", _colonies.Count, _lastTickPopulation);

      return Task.CompletedTask;
    }

    public Task StartAsync()
    {
      if (_state != SimulationState.Initialized && _state != SimulationState.Paused)
        throw new InvalidOperationException($"Cannot start simulation in state: {_state}");

      if (_configuration == null)
        throw new InvalidOperationException("Simulation not properly initialized");

      var intervalMs = (int)(1000.0 / _configuration.TargetTPS);
      _simulationTimer.Change(0, intervalMs);
      _simulationStartTime = DateTime.UtcNow;
      _performanceTimer.Start();

      SetState(SimulationState.Running);
      _logger?.LogInformation("Simulation started with target {TPS} TPS", _configuration.TargetTPS);

      return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
      if (_state != SimulationState.Running)
        throw new InvalidOperationException($"Cannot pause simulation in state: {_state}");

      _simulationTimer.Change(Timeout.Infinite, Timeout.Infinite);
      _performanceTimer.Stop();

      SetState(SimulationState.Paused);
      _logger?.LogInformation("Simulation paused at tick {Tick}", _currentTick);

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
      _logger?.LogInformation("Simulation stopped at tick {Tick}", _currentTick);
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
        _logger?.LogError(ex, "Error during simulation step");
        SetState(SimulationState.Error);
      }
    }

    private void ExecuteSimulationStep()
    {
      if (_context == null || _configuration == null)
        throw new InvalidOperationException("Simulation not properly initialized");

      var stepWatch = Stopwatch.StartNew();

      // Update simulation context
      ((SimulationContext)_context).UpdateTick(_currentTick);

      // Update all colonies
      var activeColonies = _colonies.Where(c => c.IsActive).ToList();
      foreach (var colony in activeColonies) colony.Update(_context);

      // Remove inactive colonies
      _colonies.RemoveAll(c => !c.IsActive);

      // Update environment
      if (_environment is SimulationEnvironment simEnv) simEnv.Update(_context);

      // Track births and deaths
      UpdateBirthDeathStatistics();

      _currentTick++;
      stepWatch.Stop();

      // Generate statistics
      var statistics = GenerateStatistics();

      // Fire tick event
      Tick?.Invoke(this, new SimulationTickEventArgs(_currentTick, _configuration.DeltaTime, statistics));

      // Check termination conditions
      if (_configuration.MaxTicks > 0 && _currentTick >= _configuration.MaxTicks) Task.Run(StopAsync); // Use Task.Run to avoid blocking the timer thread
      else if (_colonies.Count == 0)
      {
        _logger?.LogInformation("All colonies extinct - stopping simulation");
        Task.Run(StopAsync);
      }
    }

    private void UpdateBirthDeathStatistics()
    {
      var allAnts = _colonies.SelectMany(c => c.Members).OfType<Ant>().ToList();
      var currentAliveAnts = new HashSet<Guid>();

      // Check current status of all ants
      foreach (var ant in allAnts)
      {
        if (ant.IsAlive)
        {
          currentAliveAnts.Add(ant.Id);

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
          if (_antLifeTracker.TryGetValue(ant.Id, out var wasAlive) && wasAlive)
          {
            _antLifeTracker[ant.Id] = false;
            _totalDeathCount++;
          }
        }
      }

      // Clean up tracking for ants that no longer exist (were removed from colonies)
      var antsToRemove = _antLifeTracker.Keys.Except([.. allAnts.Select(a => a.Id)]);
      foreach (var antId in antsToRemove)
      {
        if (_antLifeTracker[antId]) _totalDeathCount++; // Was alive, now gone = death

        _antLifeTracker.Remove(antId);
      }

      _lastTickPopulation = currentAliveAnts.Count;
    }

    private SimulationStatistics GenerateStatistics()
    {
      var allAnts = _colonies.SelectMany(c => c.Members).OfType<Ant>().ToList();
      var aliveAnts = allAnts.Where(a => a.IsAlive).ToList();

      return new SimulationStatistics
      {
        CurrentTick = _currentTick,
        TotalPopulation = aliveAnts.Count,
        ActiveColonies = _colonies.Count(c => c.IsActive),
        TotalFoodStored = _colonies.Sum(c => c.TotalFoodStored),
        AvgEnergyLevel = aliveAnts.Count != 0 ? aliveAnts.Average(a => a.Energy) : 0,
        BirthCount = _totalBirthCount,
        DeathCount = _totalDeathCount,
        PopulationByRole = aliveAnts.GroupBy(a => a.Role.ToString())
          .ToDictionary(g => g.Key, g => g.Count()),
        SimulationTimeElapsed = _performanceTimer.Elapsed.TotalSeconds
      };
    }

    private void SetState(SimulationState newState)
    {
      if (_state != newState)
      {
        var previousState = _state;
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
            _logger?.LogWarning(ex, "Error during simulation shutdown in Dispose");
          }
        }

        // Dispose timer safely
        try
        {
          _simulationTimer.Dispose();
        }
        catch (Exception ex)
        {
          _logger?.LogWarning(ex, "Error disposing simulation timer");
        }

        _antLifeTracker.Clear();
        _performanceTimer?.Stop();
        _disposed = true;

        GC.SuppressFinalize(this);
      }
    }
  }
}
