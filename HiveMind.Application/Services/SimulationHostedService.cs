using HiveMind.Application.Configuration;
using HiveMind.Core.Entities;
using HiveMind.Core.Enums;
using HiveMind.Core.Factories;
using HiveMind.Core.Services;
using HiveMind.Core.Simulation;
using HiveMind.Core.ValueObject;
using HiveMind.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HiveMind.Application.Services
{
  /// <summary>
  /// Enhanced simulation hosted service with integrated persistence support.
  /// </summary>
  public sealed class SimulationHostedService : BackgroundService
  {
    private readonly ILogger<SimulationHostedService> _logger;
    private readonly SimulationConfiguration _config;
    private readonly ISimulationTimeService _timeService;
    private readonly PersistenceService _persistenceService;
    private SimulationEngine? _simulationEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationHostedService"/> class.
    /// </summary>
    public SimulationHostedService(
      ILogger<SimulationHostedService> logger,
      IOptions<SimulationConfiguration> config,
      ISimulationTimeService timeService,
      PersistenceService persistenceService
    )
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
      _timeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
      _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));

      // Subscribe to persistence events
      _persistenceService.AutoSaveCompleted += OnAutoSaveCompleted;
      _persistenceService.BackupCreated += OnBackupCreated;
    }

    /// <summary>
    /// Executes the enhanced simulation service with persistence support.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _logger.LogInformation("Enhanced HiveMind simulation service starting with persistence support...");

      try
      {
        // Try to load existing simulation state
        SimulationState? existingState = await _persistenceService.LoadSimulationAsync(stoppingToken);

        if (existingState != null)
        {
          _logger.LogInformation(
            "Loaded existing simulation state with {HiveCount} hives",
            existingState.Beehives.Count
          );
          await InitializeSimulationFromState(existingState);
        }
        else
        {
          _logger.LogInformation("No existing simulation found, creating new simulation");
          await InitializeNewSimulation();
        }

        // Start simulation
        TimeSpan tickInterval = TimeSpan.FromMilliseconds(_config.TickIntervalMilliseconds);
        await _simulationEngine!.StartAsync(tickInterval);
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Enhanced simulation service was cancelled");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Fatal error in enhanced simulation service");
        throw;
      }
      finally
      {
        // Save final state before shutdown
        if (_simulationEngine != null && _persistenceService.HasUnsavedChanges)
        {
          try
          {
            await _persistenceService.SaveSimulationAsync(_simulationEngine.State, stoppingToken);
            _logger.LogInformation("Final simulation state saved before shutdown");
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Failed to save final simulation state");
          }
        }

        _simulationEngine?.Dispose();
      }
    }

    /// <summary>
    /// Initializes simulation from loaded state.
    /// </summary>
    private async Task InitializeSimulationFromState(SimulationState existingState)
    {
      // Restore environment
      Core.Entities.Environment environment = existingState.Environment;

      // Create simulation engine with restored state
      TimeSpan autoSaveInterval = TimeSpan.FromMinutes(_config.AutoSaveIntervalMinutes);
      _simulationEngine = new(_timeService, _logger, environment, autoSaveInterval);

      // Subscribe to simulation events for persistence
      _simulationEngine.SimulationEvent += OnSimulationEvent;

      // Restore beehives to the engine
      foreach (Beehive beehive in existingState.Beehives)
        _simulationEngine.AddBeehive(beehive);

      _logger.LogInformation("Simulation restored from saved state");
      await Task.CompletedTask;
    }

    /// <summary>
    /// Initializes a new simulation.
    /// </summary>
    private async Task InitializeNewSimulation()
    {
      // Create initial environment
      Core.Entities.Environment environment = BeehiveFactory.CreateSeasonalEnvironment(Season.Spring);

      // Create simulation engine
      TimeSpan autoSaveInterval = TimeSpan.FromMinutes(_config.AutoSaveIntervalMinutes);
      _simulationEngine = new(_timeService, _logger, environment, autoSaveInterval);

      // Subscribe to simulation events
      _simulationEngine.SimulationEvent += OnSimulationEvent;

      // Create initial beehives
      await CreateInitialHives();

      // Save initial state
      await _persistenceService.SaveSimulationAsync(_simulationEngine.State);

      _logger.LogInformation("New simulation initialized and saved");
    }

    /// <summary>
    /// Creates initial beehives for a new simulation.
    /// </summary>
    private async Task CreateInitialHives()
    {
      // Create a production hive at origin
      Position3D hive1Location = new(0, 0, 0);
      Beehive hive1 = BeehiveFactory.CreateProductionHive(hive1Location);
      _simulationEngine!.AddBeehive(hive1);

      // Add a smaller nucleus colony nearby
      Position3D hive2Location = new(100, 0, 0);
      Beehive hive2 = BeehiveFactory.CreateNucleusColony(hive2Location);
      _simulationEngine.AddBeehive(hive2);

      _logger.LogInformation(
        "Created initial hives: Production hive ({Bees1} bees) and Nucleus colony ({Bees2} bees)",
        hive1.TotalPopulation,
        hive2.TotalPopulation
      );

      await Task.CompletedTask;
    }

    /// <summary>
    /// Handles simulation events and manages persistence.
    /// </summary>
    private void OnSimulationEvent(object? sender, SimulationEventArgs e)
    {
      // Mark as changed for auto-save
      _persistenceService.MarkAsChanged();

      switch (e)
      {
        case SimulationStartedEventArgs:
          _logger.LogInformation(
            "Simulation started with {Hives} hives and {Bees} total bees",
            e.State.Beehives.Count,
            e.State.TotalLivingBees
          );
          break;

        case SimulationStoppedEventArgs:
          _logger.LogInformation("Simulation stopped after {Ticks} ticks", e.State.TotalTicks);
          break;

        case SimulationCompletedEventArgs:
          _logger.LogWarning(
            "Simulation completed - all hives collapsed after {Ticks} ticks",
            e.State.TotalTicks
          );
          break;

        case SimulationSavedEventArgs:
          _logger.LogDebug("Simulation state auto-saved at tick {Tick}", e.State.TotalTicks);
          break;
      }
    }

    /// <summary>
    /// Handles auto-save completion events.
    /// </summary>
    private void OnAutoSaveCompleted(object? sender, AutoSaveCompletedEventArgs e)
    {
      if (e.Success)
        _logger.LogDebug("Auto-save completed successfully at {Time}", e.CompletedAt);
      else
        _logger.LogWarning("Auto-save failed at {Time}: {Error}", e.CompletedAt, e.ErrorMessage);
    }

    /// <summary>
    /// Handles backup creation events.
    /// </summary>
    private void OnBackupCreated(object? sender, BackupCreatedEventArgs e) =>
      _logger.LogInformation("{BackupType} backup created at {Time}", e.BackupType, e.CreatedAt);

    /// <summary>
    /// Stops the simulation service gracefully with final save.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
      _logger.LogInformation("Enhanced HiveMind simulation service stopping...");

      if (_simulationEngine != null)
      {
        await _simulationEngine.StopAsync();

        // Perform final save
        if (_persistenceService.HasUnsavedChanges)
        {
          try
          {
            await _persistenceService.SaveSimulationAsync(_simulationEngine.State, cancellationToken);
            _logger.LogInformation("Final state saved before shutdown");
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Failed to save final state");
          }
        }
      }

      await base.StopAsync(cancellationToken);
      _logger.LogInformation("HiveMind simulation service stopped");
    }
  }
}
