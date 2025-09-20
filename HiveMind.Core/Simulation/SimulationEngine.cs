using HiveMind.Core.Entities;
using HiveMind.Core.Events;
using HiveMind.Core.Services;
using Microsoft.Extensions.Logging;

namespace HiveMind.Core.Simulation
{
  /// <summary>
  /// Core simulation engine that manages the simulation loop and orchestrates all simulation activities.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="SimulationEngine"/> class.
  /// </remarks>
  /// <param name="timeService">The simulation time service.</param>
  /// <param name="logger">The logger instance.</param>
  /// <param name="initialEnvironment">The initial environment state.</param>
  /// <param name="autoSaveInterval">The interval between automatic saves.</param>
  public sealed class SimulationEngine(
    ISimulationTimeService timeService,
    ILogger<SimulationEngine> logger,
    Entities.Environment initialEnvironment,
    TimeSpan? autoSaveInterval = null
  )
  {
    private readonly ISimulationTimeService _timeService =
      timeService ?? throw new ArgumentNullException(nameof(timeService));
    private readonly ILogger<SimulationEngine> _logger =
      logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SimulationState _state = new(initialEnvironment);
    private readonly List<IBeeEvent> _eventBuffer = [];
    private readonly List<IHiveEvent> _hiveEventBuffer = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private bool _isRunning;
    private Task? _simulationTask;
    private readonly Lock _stateLock = new();
    private DateTime _lastAutoSave = DateTime.UtcNow;
    private readonly TimeSpan _autoSaveInterval = autoSaveInterval ?? TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the current simulation state.
    /// </summary>
    public SimulationState State => _state;

    /// <summary>
    /// Gets a value indicating whether the simulation is currently running.
    /// </summary>
    public bool IsRunning
    {
      get
      {
        lock (_stateLock)
          return _isRunning;
      }
    }

    /// <summary>
    /// Event raised when simulation events occur.
    /// </summary>
    public event EventHandler<SimulationEventArgs>? SimulationEvent;

    /// <summary>
    /// Starts the simulation with the specified tick interval.
    /// </summary>
    /// <param name="tickInterval">The time between simulation ticks.</param>
    /// <returns>A task representing the simulation operation.</returns>
    public async Task StartAsync(TimeSpan tickInterval)
    {
      lock (_stateLock)
      {
        if (_isRunning)
        {
          _logger.LogWarning("Simulation is already running");
          return;
        }

        _isRunning = true;
        _state.SetStatus(SimulationStatus.Running);
      }

      _logger.LogInformation("Starting HiveMind simulation with {TickInterval}ms tick interval", tickInterval.TotalMilliseconds);

      _simulationTask = Task.Run(() => SimulationLoop(tickInterval, _cancellationTokenSource.Token));

      OnSimulationEvent(new SimulationStartedEventArgs(_state));

      try
      {
        await _simulationTask;
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Simulation was cancelled");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Simulation encountered an error");
        _state.SetStatus(SimulationStatus.Error);
      }
      finally
      {
        lock (_stateLock)
          _isRunning = false;
      }
    }

    /// <summary>
    /// Stops the simulation gracefully.
    /// </summary>
    /// <returns>A task representing the stop operation.</returns>
    public async Task StopAsync()
    {
      _logger.LogInformation("Stopping HiveMind simulation");

      lock (_stateLock)
        if (!_isRunning)
        {
          _logger.LogWarning("Simulation is not running");
          return;
        }

      _cancellationTokenSource.Cancel();

      if (_simulationTask != null)
      {
        try
        {
          await _simulationTask;
        }
        catch (OperationCanceledException)
        {
          // Expected when cancellation is requested
        }
      }

      _state.SetStatus(SimulationStatus.Stopped);
      OnSimulationEvent(new SimulationStoppedEventArgs(_state));

      _logger.LogInformation("HiveMind simulation stopped");
    }

    /// <summary>
    /// Pauses the simulation.
    /// </summary>
    public void Pause()
    {
      lock (_stateLock)
        if (_isRunning && _state.Status == SimulationStatus.Running)
        {
          _state.SetStatus(SimulationStatus.Paused);
          _logger.LogInformation("Simulation paused");
          OnSimulationEvent(new SimulationPausedEventArgs(_state));
        }
    }

    /// <summary>
    /// Resumes a paused simulation.
    /// </summary>
    public void Resume()
    {
      lock (_stateLock)
        if (_isRunning && _state.Status == SimulationStatus.Paused)
        {
          _state.SetStatus(SimulationStatus.Running);
          _logger.LogInformation("Simulation resumed");
          OnSimulationEvent(new SimulationResumedEventArgs(_state));
        }
    }

    /// <summary>
    /// Adds a beehive to the simulation.
    /// </summary>
    /// <param name="beehive">The beehive to add.</param>
    public void AddBeehive(Beehive beehive)
    {
      ArgumentNullException.ThrowIfNull(beehive);

      lock (_stateLock)
      {
        _state.AddBeehive(beehive);
        _logger.LogInformation(
          "Added beehive with {Population} bees at {Location}",
          beehive.TotalPopulation,
          beehive.Location
        );
      }
    }

    /// <summary>
    /// Main simulation loop that processes ticks at the specified interval.
    /// </summary>
    /// <param name="tickInterval">The time between ticks.</param>
    /// <param name="cancellationToken">Token to cancel the simulation.</param>
    private async Task SimulationLoop(TimeSpan tickInterval, CancellationToken cancellationToken)
    {
      while (!cancellationToken.IsCancellationRequested)
      {
        try
        {
          // Check if simulation should continue
          if (!ShouldContinueSimulation())
          {
            _logger.LogInformation("Simulation completed - no viable hives remaining");
            _state.SetStatus(SimulationStatus.Completed);
            OnSimulationEvent(new SimulationCompletedEventArgs(_state));
            break;
          }

          // Only process if running (not paused)
          if (_state.Status == SimulationStatus.Running)
            await ProcessSimulationTick();

          // Wait for next tick
          await Task.Delay(tickInterval, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error during simulation tick {Tick}", _state.TotalTicks);
          _state.SetStatus(SimulationStatus.Error);
          throw;
        }
      }
    }

    /// <summary>
    /// Processes a single simulation tick.
    /// </summary>
    private async Task ProcessSimulationTick()
    {
      // Advance simulation time
      _timeService.AdvanceTick();
      _state.IncrementTicks();

      // Update environment
      _state.Environment.UpdateConditions(_timeService.CurrentTime);

      // Clear event buffers
      _eventBuffer.Clear();
      _hiveEventBuffer.Clear();

      // Process all beehives
      Task[] tasks = [.. _state.Beehives.Select(ProcessBeehiveTick)];
      await Task.WhenAll(tasks);

      // Process events
      ProcessEvents();

      // Check for autosave
      await CheckAutoSave();

      // Log periodic statistics
      if (_state.TotalTicks % 60 == 0) // Every hour of simulation time
        LogPeriodicStats();
    }

    /// <summary>
    /// Processes a single tick for a beehive.
    /// </summary>
    /// <param name="beehive">The beehive to process.</param>
    private Task ProcessBeehiveTick(Beehive beehive) => Task.Run(() =>
    {
      try
      {
        beehive.UpdateHiveActivities(_state.Environment);

        // Check for colony collapse
        if (!beehive.IsViable)
          lock (_hiveEventBuffer)
            _hiveEventBuffer.Add(new HiveCollapseEvent(
              beehive,
              DetermineCollapseReason(beehive), _timeService.CurrentTime
            ));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing beehive {HiveId}", beehive.Id);
      }
    });

    /// <summary>
    /// Processes accumulated events from the current tick.
    /// </summary>
    private void ProcessEvents()
    {
      foreach (var beeEvent in _eventBuffer)
        OnSimulationEvent(new BeeEventArgs(beeEvent, _state));

      foreach (var hiveEvent in _hiveEventBuffer)
        OnSimulationEvent(new HiveEventArgs(hiveEvent, _state));
    }

    /// <summary>
    /// Checks if an automatic save should be performed.
    /// </summary>
    private async Task CheckAutoSave()
    {
      if (DateTime.UtcNow - _lastAutoSave >= _autoSaveInterval)
      {
        await PerformAutoSave();
        _lastAutoSave = DateTime.UtcNow;
        _state.UpdateLastSaveTime();
      }
    }

    /// <summary>
    /// Performs an automatic save of the simulation state.
    /// </summary>
    private async Task PerformAutoSave()
    {
      try
      {
        // In Phase 6, this will save to persistence layer
        // For now, just log the save operation
        _logger.LogInformation("Auto-saving simulation state at tick {Tick}", _state.TotalTicks);

        OnSimulationEvent(new SimulationSavedEventArgs(_state));

        await Task.CompletedTask;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to auto-save simulation state");
      }
    }

    /// <summary>
    /// Determines if the simulation should continue running.
    /// </summary>
    /// <returns>True if simulation should continue; otherwise, false.</returns>
    private bool ShouldContinueSimulation() => _state.HasViableHives;

    /// <summary>
    /// Determines the reason for a hive collapse.
    /// </summary>
    /// <param name="beehive">The collapsed beehive.</param>
    /// <returns>The determined collapse reason.</returns>
    private CollapseReason DetermineCollapseReason(Beehive beehive)
    {
      if (!beehive.HasQueen)
        return CollapseReason.QueenLoss;

      if (beehive.WorkerPopulation < Beehive.MinViableWorkerCount)
        return CollapseReason.PopulationCollapse;

      // Add more sophisticated collapse detection in later phases
      return CollapseReason.MultipleCauses;
    }

    /// <summary>
    /// Logs periodic statistics about the simulation.
    /// </summary>
    private void LogPeriodicStats()
    {
      SimulationStats stats = _state.GetStats();

      _logger.LogInformation(
        "Simulation Stats - Tick: {Tick}, Hives: {Hives}/{ViableHives}, " +
        "Bees: {Bees} (W: {Workers}, D: {Drones}, Q: {Queens}), " +
        "Environment: {Temp}°C, {Weather}",
        stats.TotalTicks,
        stats.TotalHives,
        stats.ViableHives,
        stats.TotalLivingBees,
        stats.TotalWorkers,
        stats.TotalDrones,
        stats.TotalQueens,
        stats.CurrentEnvironment.Temperature,
        stats.CurrentEnvironment.Weather
      );
    }

    /// <summary>
    /// Raises the SimulationEvent.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    private void OnSimulationEvent(SimulationEventArgs args) =>
      SimulationEvent?.Invoke(this, args);

    /// <summary>
    /// Disposes the simulation engine and releases resources.
    /// </summary>
    public void Dispose()
    {
      _cancellationTokenSource.Cancel();
      _cancellationTokenSource.Dispose();
      _simulationTask?.Dispose();
    }
  }
}
