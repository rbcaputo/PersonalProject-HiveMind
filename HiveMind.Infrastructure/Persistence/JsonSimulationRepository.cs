using HiveMind.Core.Entities;
using HiveMind.Core.Repositories;
using HiveMind.Core.Simulation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HiveMind.Infrastructure.Persistence
{
  /// <summary>
  /// JSON-based implementation of the simulation repository.
  /// Provides file-based persistence using JSON serialization.
  /// </summary>
  public sealed class JsonSimulationRepository : ISimulationRepository
  {
    private readonly ILogger<JsonSimulationRepository> _logger;
    private readonly PersistenceConfiguration _config;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _saveDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSimulationRepository"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="config">Persistence configuration.</param>
    public JsonSimulationRepository(
      ILogger<JsonSimulationRepository> logger,
      IOptions<PersistenceConfiguration> config
    )
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _config = config?.Value ?? throw new ArgumentNullException(nameof(config));

      _saveDirectory = Path.GetFullPath(_config.SaveDirectory);
      Directory.CreateDirectory(_saveDirectory);

      _serializerOptions = new()
      {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new SimulationStateJsonConverter() },
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
      };
    }

    /// <summary>
    /// Saves the complete simulation state to persistent storage.
    /// </summary>
    /// <param name="state">The simulation state to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveSimulationStateAsync(SimulationState state, CancellationToken cancellationToken = default)
    {
      ArgumentNullException.ThrowIfNull(state);

      try
      {
        SimulationSaveData saveData = new()
        {
          SaveId = GenerateSaveId(),
          CreatedAt = DateTime.UtcNow,
          SimulationState = state,
          Metadata = new()
          {
            Version = "1.0",
            HiveCount = state.Beehives.Count,
            TotalBees = state.TotalLivingBees,
            SimulationTicks = state.TotalTicks
          }
        };

        string json = JsonSerializer.Serialize(saveData, _serializerOptions);
        string filePath = GetSaveFilePath(saveData.SaveId);

        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation(
          "Simulation state saved to {FilePath} ({HiveCount} hives, {BeeCount} bees)",
          filePath,
          saveData.Metadata.HiveCount,
          saveData.Metadata.TotalBees
        );

        // Update latest save reference
        await UpdateLatestSaveReferenceAsync(saveData.SaveId, cancellationToken);

        // Cleanup old backups if needed
        if (_config.MaxBackups > 0)
          await CleanupBackupsAsync(_config.MaxBackups, cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to save simulation state");
        throw new PersistenceException("Failed to save simulation state", ex);
      }
    }

    /// <summary>
    /// Loads the most recent simulation state from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded simulation state, or null if no saved state exists.</returns>
    public async Task<SimulationState?> LoadSimulationStateAsync(CancellationToken cancellationToken = default)
    {
      try
      {
        string? latestSaveId = await GetLatestSaveIdAsync(cancellationToken);
        if (string.IsNullOrEmpty(latestSaveId))
        {
          _logger.LogInformation("No saved simulation state found");
          return null;
        }

        string filePath = GetSaveFilePath(latestSaveId);
        if (!File.Exists(filePath))
        {
          _logger.LogWarning("Latest save file not found: {FilePath}", filePath);
          return null;
        }

        string json = await File.ReadAllTextAsync(filePath, cancellationToken);
        SimulationSaveData? saveData = JsonSerializer.Deserialize<SimulationSaveData>(json, _serializerOptions);

        if (saveData?.SimulationState == null)
        {
          _logger.LogError("Invalid save data in file: {FilePath}", filePath);
          return null;
        }

        _logger.LogInformation(
          "Loaded simulation state from {FilePath} ({HiveCount} hives, {BeeCount} bees)",
          filePath,
          saveData.Metadata.HiveCount,
          saveData.Metadata.TotalBees
        );

        return saveData.SimulationState;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to load simulation state");
        throw new PersistenceException("Failed to load simulation state", ex);
      }
    }

    /// <summary>
    /// Saves a beehive to persistent storage.
    /// </summary>
    /// <param name="beehive">The beehive to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveBeehiveAsync(Beehive beehive, CancellationToken cancellationToken = default)
    {
      ArgumentNullException.ThrowIfNull(beehive);

      try
      {
        BeehiveSaveData hiveData = new()
        {
          HiveId = beehive.Id,
          SavedAt = DateTime.UtcNow,
          Beehive = beehive
        };

        string json = JsonSerializer.Serialize(hiveData, _serializerOptions);
        string filePath = GetHiveFilePath(beehive.Id);

        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogDebug("Beehive {HiveId} saved to {FilePath}", beehive.Id, filePath);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to save beehive {HiveId}", beehive.Id);
        throw new PersistenceException($"Failed to save beehive {beehive.Id}", ex);
      }
    }

    /// <summary>
    /// Loads a beehive by its unique identifier.
    /// </summary>
    /// <param name="hiveId">The unique identifier of the hive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded beehive, or null if not found.</returns>
    public async Task<Beehive?> LoadBeehiveAsync(Guid hiveId, CancellationToken cancellationToken = default)
    {
      try
      {
        string filePath = GetHiveFilePath(hiveId);
        if (!File.Exists(filePath))
          return null;

        string json = await File.ReadAllTextAsync(filePath, cancellationToken);
        BeehiveSaveData? hiveData = JsonSerializer.Deserialize<BeehiveSaveData>(json, _serializerOptions);

        return hiveData?.Beehive;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to load beehive {HiveId}", hiveId);
        throw new PersistenceException($"Failed to load beehive {hiveId}", ex);
      }
    }

    /// <summary>
    /// Loads all beehives from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all saved beehives.</returns>
    public async Task<IEnumerable<Beehive>> LoadAllBeehivesAsync(CancellationToken cancellationToken = default)
    {
      try
      {
        string hiveDirectory = GetHivesDirectory();
        if (!Directory.Exists(hiveDirectory))
          return [];

        string[] hiveFiles = Directory.GetFiles(hiveDirectory, "*.json");
        List<Beehive> beehives = [];

        foreach (string filePath in hiveFiles)
        {
          try
          {
            string json = await File.ReadAllTextAsync(filePath, cancellationToken);
            BeehiveSaveData? hiveData = JsonSerializer.Deserialize<BeehiveSaveData>(json, _serializerOptions);

            if (hiveData?.Beehive != null)
              beehives.Add(hiveData.Beehive);
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Failed to load beehive from {FilePath}", filePath);
          }
        }

        _logger.LogInformation("Loaded {HiveCount} beehives from storage", beehives.Count);
        return beehives;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to load beehives");
        throw new PersistenceException("Failed to load beehives", ex);
      }
    }

    /// <summary>
    /// Saves the environment state to persistent storage.
    /// </summary>
    /// <param name="environment">The environment to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveEnvironmentAsync(Core.Entities.Environment environment, CancellationToken cancellationToken = default)
    {
      ArgumentNullException.ThrowIfNull(environment);

      try
      {
        EnvironmentSaveData environmentData = new()
        {
          SavedAt = DateTime.UtcNow,
          Environment = environment
        };

        string json = JsonSerializer.Serialize(environmentData, _serializerOptions);
        string filePath = GetEnvironmentFilePath();

        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogDebug("Environment state saved to {FilePath}", filePath);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to save environment state");
        throw new PersistenceException("Failed to save environment state", ex);
      }
    }

    /// <summary>
    /// Loads the environment state from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded environment, or null if not found.</returns>
    public async Task<Core.Entities.Environment?> LoadEnvironmentAsync(CancellationToken cancellationToken = default)
    {
      try
      {
        string filePath = GetEnvironmentFilePath();
        if (!File.Exists(filePath))
          return null;

        string json = await File.ReadAllTextAsync(filePath, cancellationToken);
        EnvironmentSaveData? environmentData = JsonSerializer.Deserialize<EnvironmentSaveData>(json, _serializerOptions);

        return environmentData?.Environment;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to load environment state");
        throw new PersistenceException("Failed to load environment state", ex);
      }
    }

    /// <summary>
    /// Deletes a specific save file.
    /// </summary>
    /// <param name="saveId">The identifier of the save to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteSaveAsync(string saveId, CancellationToken cancellationToken = default)
    {
      ArgumentException.ThrowIfNullOrEmpty(saveId);

      try
      {
        string filePath = GetSaveFilePath(saveId);
        if (File.Exists(filePath))
        {
          File.Delete(filePath);
          _logger.LogInformation("Deleted save file: {FilePath}", filePath);
        }

        await Task.CompletedTask;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to delete save {SaveId}", saveId);
        throw new PersistenceException($"Failed to delete save {saveId}", ex);
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
        string[] saveFiles = Directory.GetFiles(_saveDirectory, "save_*.json");
        List<SaveInfo> saveInfos = [];

        foreach (string filePath in saveFiles)
        {
          try
          {
            FileInfo fileInfo = new(filePath);
            string saveId = Path.GetFileNameWithoutExtension(filePath).Replace("save_", "");

            // Try to read metadata from file
            string json = await File.ReadAllTextAsync(filePath, cancellationToken);
            SimulationSaveData? saveData = JsonSerializer.Deserialize<SimulationSaveData>(json, _serializerOptions);

            saveInfos.Add(new()
            {
              SaveId = saveId,
              CreatedAt = saveData?.CreatedAt ?? fileInfo.CreationTime,
              FileSizeBytes = fileInfo.Length,
              HiveCount = saveData?.Metadata.HiveCount ?? 0,
              TotalBees = saveData?.Metadata.TotalBees ?? 0,
              SimulationTicks = saveData?.Metadata.SimulationTicks ?? 0,
              Description = $"Simulation save from {saveData?.CreatedAt:yyyy-MM-dd HH:mm}"
            });
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Failed to read save info from {FilePath}", filePath);
          }
        }

        return saveInfos.OrderByDescending(s => s.CreatedAt);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get available saves");
        throw new PersistenceException("Failed to get available saves", ex);
      }
    }

    /// <summary>
    /// Creates a backup of the current save data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CreateBackupAsync(CancellationToken cancellationToken = default)
    {
      try
      {
        string backupDirectory = GetBackupDirectory();
        Directory.CreateDirectory(backupDirectory);

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string backupPath = Path.Combine(backupDirectory, $"backup_{timestamp}.zip");

        // Create a zip file with all save data
        // Note: This would require System.IO.Compression.ZipFile for full implementation
        _logger.LogInformation("Backup would be created at {BackupPath}", backupPath);

        await Task.CompletedTask;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to create backup");
        throw new PersistenceException("Failed to create backup", ex);
      }
    }

    /// <summary>
    /// Performs cleanup of old backup files based on retention policy.
    /// </summary>
    /// <param name="maxBackups">Maximum number of backups to keep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CleanupBackupsAsync(int maxBackups, CancellationToken cancellationToken = default)
    {
      try
      {
        IEnumerable<SaveInfo> saves = await GetAvailableSavesAsync(cancellationToken);
        List<SaveInfo> orderedSaves = [.. saves.OrderByDescending(s => s.CreatedAt)];

        if (orderedSaves.Count > maxBackups)
        {
          IEnumerable<SaveInfo> savesToDelete = orderedSaves.Skip(maxBackups);
          foreach (SaveInfo save in savesToDelete)
            await DeleteSaveAsync(save.SaveId, cancellationToken);

          _logger.LogInformation("Cleaned up {Count} old save files", orderedSaves.Count - maxBackups);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to cleanup backups");
        throw new PersistenceException("Failed to cleanup backups", ex);
      }
    }

    // Private helper methods
    private string GenerateSaveId() => DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");

    private string GetSaveFilePath(string saveId) => Path.Combine(_saveDirectory, $"save_{saveId}.json");

    private string GetHiveFilePath(Guid hiveId)
    {
      string hivesDirectory = GetHivesDirectory();
      Directory.CreateDirectory(hivesDirectory);
      return Path.Combine(hivesDirectory, $"hive_{hiveId:N}.json");
    }

    private string GetEnvironmentFilePath() => Path.Combine(_saveDirectory, "environment.json");

    private string GetHivesDirectory() => Path.Combine(_saveDirectory, "hives");

    private string GetBackupDirectory() => Path.Combine(_saveDirectory, "backups");

    private async Task<string?> GetLatestSaveIdAsync(CancellationToken cancellationToken)
    {
      string latestFile = Path.Combine(_saveDirectory, "latest.txt");
      if (File.Exists(latestFile))
        return (await File.ReadAllTextAsync(latestFile, cancellationToken)).Trim();

      // Fallback: find the most recent save file
      IEnumerable<SaveInfo> saves = await GetAvailableSavesAsync(cancellationToken);
      return saves.OrderByDescending(s => s.CreatedAt).FirstOrDefault()?.SaveId;
    }

    private async Task UpdateLatestSaveReferenceAsync(string saveId, CancellationToken cancellationToken)
    {
      string latestFile = Path.Combine(_saveDirectory, "latest.txt");
      await File.WriteAllTextAsync(latestFile, saveId, cancellationToken);
    }
  }

  /// <summary>
  /// Exception thrown when persistence operations fail.
  /// </summary>
  public sealed class PersistenceException : Exception
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="PersistenceException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public PersistenceException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistenceException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PersistenceException(string message, Exception innerException) : base(message, innerException) { }
  }

  // Save data models
  internal sealed class SimulationSaveData
  {
    public string SaveId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public SimulationState SimulationState { get; set; } = null!;
    public SaveMetadata Metadata { get; set; } = new();
  }

  internal sealed class SaveMetadata
  {
    public string Version { get; set; } = string.Empty;
    public int HiveCount { get; set; }
    public int TotalBees { get; set; }
    public long SimulationTicks { get; set; }
  }

  internal sealed class BeehiveSaveData
  {
    public Guid HiveId { get; set; }
    public DateTime SavedAt { get; set; }
    public Beehive Beehive { get; set; } = null!;
  }

  internal sealed class EnvironmentSaveData
  {
    public DateTime SavedAt { get; set; }
    public Core.Entities.Environment Environment { get; set; } = null!;
  }
}
