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

      SetState(SimulationState.Initialized);
      _logger?.LogInformation("Simulation initialized with {ColonyCount} colonies", _colonies.Count);

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

    public Task StopAsync()
    {
      if (_state == SimulationState.Uninitialized || _state == SimulationState.Stopped)
        return Task.CompletedTask;

      _simulationTimer.Change(Timeout.Infinite, Timeout.Infinite);
      _performanceTimer.Stop();

      SetState(SimulationState.Stopped);
      _logger?.LogInformation("Simulation stopped at tick {Tick}", _currentTick);

      return Task.CompletedTask;
    }

    public async Task StepAsync()
    {
      if (_state != SimulationState.Initialized && _state != SimulationState.Paused)
        throw new InvalidOperationException($"Cannot step simulation in state: {_state}");

      await ExecuteSimulationStep();
    }

    private void SimulationTimerCallback(object? state)
    {
      if (_state == SimulationState.Running)
      {
        try
        {
          ExecuteSimulationStep().Wait();
        }
        catch (Exception ex)
        {
          _logger?.LogError(ex, "Error during simulation step");
          SetState(SimulationState.Error);
        }
      }
    }

    private async Task ExecuteSimulationStep()
    {
      if (_context == null || _configuration == null)
        throw new InvalidOperationException("Simulation not properly initialized");

      var stepWatch = Stopwatch.StartNew();

      // Update simulation context
      ((SimulationContext)_context).UpdateTick(_currentTick);

      // Update all colonies
      var activeColonies = _colonies.Where(c => c.IsActive).ToList();
      foreach (var colony in activeColonies)
        colony.Update(_context);

      // Remove inactive colonies
      _colonies.RemoveAll(c => !c.IsActive);

      // Update environment
      if (_environment is SimulationEnvironment simEnv)
        simEnv.Update(_context);

      _currentTick++;
      stepWatch.Stop();

      // Generate statistics
      var statistics = GenerateStatistics();

      // Fire tick event
      Tick?.Invoke(this, new SimulationTickEventArgs(_currentTick, _configuration.DeltaTime, statistics));

      // Check termination conditions
      if (_configuration.MaxTicks > 0 && _currentTick >= _configuration.MaxTicks)
        await StopAsync();
      else if (_colonies.Count == 0)
      {
        _logger?.LogInformation("All colonies extinct - stopping simulation");
        await StopAsync();
      }
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
        DeathCount = allAnts.Count(a => !a.IsAlive),
        BirthCount = aliveAnts.Count, // Simplified - would track births separately in full implementation
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
        _simulationTimer?.Dispose();
        _performanceTimer?.Stop();
        _disposed = true;

        GC.SuppressFinalize(this);
      }
    }
  }
}
