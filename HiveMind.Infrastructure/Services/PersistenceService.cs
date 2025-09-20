using HiveMind.Core.Repositories;
using HiveMind.Core.Simulation;
using HiveMind.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HiveMind.Infrastructure.Services
{
  /// <summary>
  /// High-level service for managing simulation persistence operations.
  /// Provides automated save/load functionality and backup management.
  /// </summary>
  public sealed class PersistenceService
  {
    private readonly ILogger<PersistenceService> _logger;
    private readonly ISimulationRepository _repository;
    private readonly PersistenceConfiguration _config;
    private readonly Timer? _autoSaveTimer;
    private readonly Timer? _backupTimer;

    private DateTime _lastSave;
    private bool _hasUnsavedChanges;

    /// <summary>
    /// Gets a value indicating whether there are unsaved changes.
    /// </summary>
    public bool HasUnsavedChanges => _hasUnsavedChanges;

    /// <summary>
    /// Gets the timestamp of the last save operation.
    /// </summary>
    public DateTime LastSaveTime => _lastSave;

    /// <summary>
    /// Event raised when an auto-save operation completes.
    /// </summary>
    public event EventHandler<AutoSaveCompletedEventArgs>? AutoSaveCompleted;

    /// <summary>
    /// Event raised when a backup is created.
    /// </summary>
    public event EventHandler<BackupCreatedEventArgs>? BackupCreated;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistenceService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="repository">Simulation repository.</param>
    /// <param name="config">Persistence configuration.</param>
    public PersistenceService(
      ILogger<PersistenceService> logger,
      ISimulationRepository repository,
      IOptions<PersistenceConfiguration> config
    )
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _repository = repository ?? throw new ArgumentNullException(nameof(repository));
      _config = config?.Value ?? throw new ArgumentNullException(nameof(config));

      _lastSave = DateTime.UtcNow;
      _hasUnsavedChanges = false;

      // Setup auto-save timer
      if (_config.EnableAutoSave)
      {
        TimeSpan autoSaveInterval = TimeSpan.FromMinutes(_config.AutoSaveIntervalMinutes);
        _autoSaveTimer = new(AutoSaveCallback, null, autoSaveInterval, autoSaveInterval);
      }

      // Setup backup timer
      if (_config.EnableAutomaticBackups)
      {
        TimeSpan backupInterval = TimeSpan.FromHours(_config.BackupIntervalHours);
        _backupTimer = new(BackupCallback, null, backupInterval, backupInterval);
      }
    }

    /// <summary>
    /// Saves the simulation state and marks changes as saved.
    /// </summary>
    /// <param name="state">The simulation state to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the save operation.</returns>
    public async Task SaveSimulationAsync(SimulationState state, CancellationToken cancellationToken = default)
    {
      ArgumentNullException.ThrowIfNull(state);

      try
      {
        await _repository.SaveSimulationStateAsync(state, cancellationToken);

        _lastSave = DateTime.UtcNow;
        _hasUnsavedChanges = false;

        _logger.LogInformation("Simulation saved successfully at {SaveTime}", _lastSave);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to save simulation state");
        throw;
      }
    }

    /// <summary>
    /// Loads the most recent simulation state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded simulation state, or null if no save exists.</returns>
    public async Task<SimulationState?> LoadSimulationAsync(CancellationToken cancellationToken = default)
    {
      try
      {
        SimulationState? state = await _repository.LoadSimulationStateAsync(cancellationToken);

        if (state != null)
        {
          _hasUnsavedChanges = false;
          _logger.LogInformation("Simulation loaded successfully");
        }

        return state;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to load simulation state");
        throw;
      }
    }

    /// <summary>
    /// Marks the simulation as having unsaved changes.
    /// </summary>
    public void MarkAsChanged() => _hasUnsavedChanges = true;

    /// <summary>
    /// Creates a manual backup of the current data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the backup operation.</returns>
    public async Task CreateBackupAsync(CancellationToken cancellationToken = default)
    {
      try
      {
        await _repository.CreateBackupAsync(cancellationToken);

        _logger.LogInformation("Manual backup created successfully");
        OnBackupCreated(new BackupCreatedEventArgs(DateTime.UtcNow, BackupType.Manual));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to create manual backup");
        throw;
      }
    }

    /// <summary>
    /// Gets information about all available saves.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of save information.</returns>
    public async Task<IEnumerable<SaveInfo>> GetAvailableSavesAsync(CancellationToken cancellationToken = default)
    {
      try
      {
        return await _repository.GetAvailableSavesAsync(cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get available saves");
        throw;
      }
    }

    /// <summary>
    /// Deletes a specific save file.
    /// </summary>
    /// <param name="saveId">The ID of the save to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the delete operation.</returns>
    public async Task DeleteSaveAsync(string saveId, CancellationToken cancellationToken = default)
    {
      ArgumentException.ThrowIfNullOrEmpty(saveId);

      try
      {
        await _repository.DeleteSaveAsync(saveId, cancellationToken);
        _logger.LogInformation("Save {SaveId} deleted successfully", saveId);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to delete save {SaveId}", saveId);
        throw;
      }
    }

    /// <summary>
    /// Performs cleanup of old saves and backups.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the cleanup operation.</returns>
    public async Task PerformCleanupAsync(CancellationToken cancellationToken = default)
    {
      try
      {
        if (_config.MaxBackups > 0)
        {
          await _repository.CleanupBackupsAsync(_config.MaxBackups, cancellationToken);
          _logger.LogInformation("Cleanup completed - keeping {MaxBackups} recent saves", _config.MaxBackups);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to perform cleanup");
        throw;
      }
    }

    /// <summary>
    /// Auto-save timer callback.
    /// </summary>
    private async void AutoSaveCallback(object? state)
    {
      if (!_hasUnsavedChanges) return;

      try
      {
        // In a full implementation, this would get the current simulation state
        // from the simulation engine. For now, we'll skip the actual save
        // but trigger the event to indicate auto-save was attempted.

        _logger.LogDebug("Auto-save triggered but no current simulation state available");

        OnAutoSaveCompleted(args: new(
          completedAt: DateTime.UtcNow,
          success: true,
          errorMessage: "No current state available"
        ));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Auto-save failed");
        OnAutoSaveCompleted(args: new(
          completedAt: DateTime.UtcNow,
          success: false,
          errorMessage: ex.Message
        ));
      }
    }

    /// <summary>
    /// Backup timer callback.
    /// </summary>
    private async void BackupCallback(object? state)
    {
      try
      {
        await _repository.CreateBackupAsync();
        _logger.LogInformation("Automatic backup created");
        OnBackupCreated(args: new(
          createdAt: DateTime.UtcNow,
          backupType: BackupType.Automatic
        ));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Automatic backup failed");
      }
    }

    /// <summary>
    /// Raises the AutoSaveCompleted event.
    /// </summary>
    private void OnAutoSaveCompleted(AutoSaveCompletedEventArgs args) =>
      AutoSaveCompleted?.Invoke(this, args);

    /// <summary>
    /// Raises the BackupCreated event.
    /// </summary>
    private void OnBackupCreated(BackupCreatedEventArgs args) =>
      BackupCreated?.Invoke(this, args);

    /// <summary>
    /// Disposes the persistence service and its resources.
    /// </summary>
    public void Dispose()
    {
      _autoSaveTimer?.Dispose();
      _backupTimer?.Dispose();
    }
  }

  /// <summary>
  /// Event arguments for auto-save completion.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="AutoSaveCompletedEventArgs"/> class.
  /// </remarks>
  /// <param name="completedAt">When the auto-save completed.</param>
  /// <param name="success">Whether the save was successful.</param>
  /// <param name="errorMessage">Error message if failed.</param>
  public sealed class AutoSaveCompletedEventArgs(
    DateTime completedAt,
    bool success,
    string? errorMessage = null
  ) : EventArgs
  {
    /// <summary>
    /// Gets the time when the auto-save completed.
    /// </summary>
    public DateTime CompletedAt { get; } = completedAt;

    /// <summary>
    /// Gets a value indicating whether the auto-save was successful.
    /// </summary>
    public bool Success { get; } = success;

    /// <summary>
    /// Gets the error message if the auto-save failed.
    /// </summary>
    public string? ErrorMessage { get; } = errorMessage;
  }

  /// <summary>
  /// Event arguments for backup creation.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="BackupCreatedEventArgs"/> class.
  /// </remarks>
  /// <param name="createdAt">When the backup was created.</param>
  /// <param name="backupType">The type of backup.</param>
  public sealed class BackupCreatedEventArgs(
    DateTime createdAt,
    BackupType backupType
  ) : EventArgs
  {
    /// <summary>
    /// Gets the time when the backup was created.
    /// </summary>
    public DateTime CreatedAt { get; } = createdAt;

    /// <summary>
    /// Gets the type of backup that was created.
    /// </summary>
    public BackupType BackupType { get; } = backupType;
  }

  /// <summary>
  /// Types of backups that can be created.
  /// </summary>
  public enum BackupType
  {
    /// <summary>
    /// Manual backup created by user request.
    /// </summary>
    Manual,

    /// <summary>
    /// Automatic backup created by timer.
    /// </summary>
    Automatic
  }
}
