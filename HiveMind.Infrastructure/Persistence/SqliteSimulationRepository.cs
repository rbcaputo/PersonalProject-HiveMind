using HiveMind.Core.Entities;
using HiveMind.Core.Repositories;
using HiveMind.Core.Simulation;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data.Common;
using System.Text.Json;

namespace HiveMind.Infrastructure.Persistence
{
  /// <summary>
  /// SQLite-based implementation of the simulation repository.
  /// Provides database-backed persistence with relational data structure.
  /// </summary>
  public sealed class SqliteSimulationRepository : ISimulationRepository, IDisposable
  {
    private readonly ILogger<SqliteSimulationRepository> _logger;
    private readonly PersistenceConfiguration _config;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteSimulationRepository"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="config">Persistence configuration.</param>
    public SqliteSimulationRepository(
      ILogger<SqliteSimulationRepository> logger,
      IOptions<PersistenceConfiguration> config
    )
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _config = config?.Value ?? throw new ArgumentNullException(nameof(config));

      string databasePath = Path.GetFullPath(_config.DatabaseFilePath);
      Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

      _connectionString = $"Data Source={databasePath};Cache=Shared;";

      _serializerOptions = new()
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
      };

      InitializeDatabaseAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Saves the complete simulation state to persistent storage.
    /// </summary>
    /// <param name="state">The simulation state to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveSimulationStateAsync(SimulationState state, CancellationToken cancellationToken = default)
    {
      ArgumentNullException.ThrowIfNull(state);

      using SqliteConnection connection = new(_connectionString);
      await connection.OpenAsync(cancellationToken);

      using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
      try
      {
        string saveId = GenerateSaveId();
        string stateJson = JsonSerializer.Serialize(state, _serializerOptions);

        // Insert simulation state
        const string insertStateSql = """
          INSERT INTO SimulationStates (SaveId, CreatedAt, StateData, HiveCount, TotalBees, SimulationTicks)
          VALUES (@SaveId, @CreatedAt, @StateData, @HiveCount, @TotalBees, @SimulationTicks)
        """;

        using SqliteCommand stateCommand = new(insertStateSql, connection, transaction);
        stateCommand.Parameters.AddWithValue("@SaveId", saveId);
        stateCommand.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
        stateCommand.Parameters.AddWithValue("@StateData", stateJson);
        stateCommand.Parameters.AddWithValue("@HiveCount", state.Beehives.Count);
        stateCommand.Parameters.AddWithValue("@TotalBees", state.TotalLivingBees);
        stateCommand.Parameters.AddWithValue("@SimulationTicks", state.TotalTicks);

        await stateCommand.ExecuteNonQueryAsync(cancellationToken);

        // Save individual beehives
        foreach (Beehive beehive in state.Beehives)
          await SaveBeehiveInternalAsync(connection, transaction, beehive, cancellationToken);

        // Update latest reference
        const string updateLatestSql = """
          INSERT OR REPLACE INTO LatestSave (Id, SaveId) VALUES (1, @SaveId)
        """;

        using SqliteCommand latestCommand = new(updateLatestSql, connection, transaction);
        latestCommand.Parameters.AddWithValue("@SaveId", saveId);
        await latestCommand.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Simulation state saved to database ({HiveCount} hives, {BeeCount} bees)",
            state.Beehives.Count, state.TotalLivingBees);

        // Cleanup old saves if needed
        if (_config.MaxBackups > 0)
          await CleanupBackupsAsync(_config.MaxBackups, cancellationToken);
      }
      catch
      {
        await transaction.RollbackAsync(cancellationToken);
        throw;
      }
    }

    /// <summary>
    /// Loads the most recent simulation state from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded simulation state, or null if no saved state exists.</returns>
    public async Task<SimulationState?> LoadSimulationStateAsync(CancellationToken cancellationToken = default)
    {
      using SqliteConnection connection = new(_connectionString);
      await connection.OpenAsync(cancellationToken);

      const string selectLatestSql = """
        SELECT ss.StateData 
        FROM SimulationStates ss
        INNER JOIN LatestSave ls ON ss.SaveId = ls.SaveId
      """;

      using SqliteCommand command = new(selectLatestSql, connection);
      object? result = await command.ExecuteScalarAsync(cancellationToken);

      if (result == null)
      {
        _logger.LogInformation("No saved simulation state found");
        return null;
      }

      try
      {
        string stateJson = (string)result;
        SimulationState? state = JsonSerializer.Deserialize<SimulationState>(stateJson, _serializerOptions);

        _logger.LogInformation("Loaded simulation state from database");
        return state;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to deserialize simulation state");
        throw new PersistenceException("Failed to deserialize simulation state", ex);
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

      using SqliteConnection connection = new(_connectionString);
      await connection.OpenAsync(cancellationToken);

      using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
      try
      {
        await SaveBeehiveInternalAsync(connection, transaction, beehive, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
      }
      catch
      {
        await transaction.RollbackAsync(cancellationToken);
        throw;
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
      using SqliteConnection connection = new(_connectionString);
      await connection.OpenAsync(cancellationToken);

      const string selectSql = """
        SELECT HiveData FROM Beehives WHERE HiveId = @HiveId ORDER BY SavedAt DESC LIMIT 1
      """;

      using SqliteCommand command = new(selectSql, connection);
      command.Parameters.AddWithValue("@HiveId", hiveId.ToString());

      object? result = await command.ExecuteScalarAsync(cancellationToken);
      if (result == null) return null;

      try
      {
        string hiveJson = (string)result;
        return JsonSerializer.Deserialize<Beehive>(hiveJson, _serializerOptions);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to deserialize beehive {HiveId}", hiveId);
        throw new PersistenceException($"Failed to deserialize beehive {hiveId}", ex);
      }
    }

    /// <summary>
    /// Loads all beehives from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all saved beehives.</returns>
    public async Task<IEnumerable<Beehive>> LoadAllBeehivesAsync(CancellationToken cancellationToken = default)
    {
      using SqliteConnection connection = new(_connectionString);
      await connection.OpenAsync(cancellationToken);

      const string selectSql = """
        SELECT DISTINCT HiveData FROM Beehives 
        WHERE SavedAt = (SELECT MAX(SavedAt) FROM Beehives b2 WHERE b2.HiveId = Beehives.HiveId)
      """;

      using SqliteCommand command = new(selectSql, connection);
      using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

      List<Beehive> beehives = [];
      while (await reader.ReadAsync(cancellationToken))
      {
        try
        {
          string hiveJson = reader.GetString(0);
          Beehive? beehive = JsonSerializer.Deserialize<Beehive>(hiveJson, _serializerOptions);
          if (beehive != null)
            beehives.Add(beehive);
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Failed to deserialize beehive from database");
        }
      }

      _logger.LogInformation("Loaded {HiveCount} beehives from database", beehives.Count);
      return beehives;
    }

    /// <summary>
    /// Saves the environment state to persistent storage.
    /// </summary>
    /// <param name="environment">The environment to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveEnvironmentAsync(Core.Entities.Environment environment, CancellationToken cancellationToken = default)
    {
      ArgumentNullException.ThrowIfNull(environment);

      using SqliteConnection connection = new(_connectionString);
      await connection.OpenAsync(cancellationToken);

      const string insertSql = """
        INSERT OR REPLACE INTO Environment (Id, SavedAt, EnvironmentData)
        VALUES (1, @SavedAt, @EnvironmentData)
      """;

      using SqliteCommand command = new(insertSql, connection);
      command.Parameters.AddWithValue("@SavedAt", DateTime.UtcNow);
      command.Parameters.AddWithValue("@EnvironmentData", JsonSerializer.Serialize(environment, _serializerOptions));

      await command.ExecuteNonQueryAsync(cancellationToken);
      _logger.LogDebug("Environment state saved to database");
    }

    /// <summary>
    /// Loads the environment state from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded environment, or null if not found.</returns>
    public async Task<Core.Entities.Environment?> LoadEnvironmentAsync(CancellationToken cancellationToken = default)
    {
      using SqliteConnection connection = new(_connectionString);
      await connection.OpenAsync(cancellationToken);

      const string selectSql = """
        SELECT EnvironmentData FROM Environment WHERE Id = 1
      """;

      using SqliteCommand command = new(selectSql, connection);
      object? result = await command.ExecuteScalarAsync(cancellationToken);
      if (result == null) return null;

      try
      {
        string environmentJson = (string)result;
        return JsonSerializer.Deserialize<Core.Entities.Environment>(environmentJson, _serializerOptions);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to deserialize environment state");
        throw new PersistenceException("Failed to deserialize environment state", ex);
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

      using SqliteConnection connection = new(_connectionString);
      await connection.OpenAsync(cancellationToken);

      const string deleteSql = """
        DELETE FROM SimulationStates WHERE SaveId = @SaveId
      """;

      using SqliteCommand command = new(deleteSql, connection);
      command.Parameters.AddWithValue("@SaveId", saveId);

      int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
      if (rowsAffected > 0)
        _logger.LogInformation("Deleted save {SaveId} from database", saveId);
    }

    /// <summary>
    /// Gets information about all available saves.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of save information.</returns>
    public async Task<IEnumerable<SaveInfo>> GetAvailableSavesAsync(CancellationToken cancellationToken = default)
    {
      using SqliteConnection connection = new(_connectionString);
      await connection.OpenAsync(cancellationToken);

      const string selectSql = """
        SELECT SaveId, CreatedAt, LENGTH(StateData) as FileSize, HiveCount, TotalBees, SimulationTicks
        FROM SimulationStates 
        ORDER BY CreatedAt DESC
      """;

      using SqliteCommand command = new(selectSql, connection);
      using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

      List<SaveInfo> saves = [];
      while (await reader.ReadAsync(cancellationToken))
      {
        saves.Add(new()
        {
          SaveId = reader.GetString("SaveId"),
          CreatedAt = reader.GetDateTime("CreatedAt"),
          FileSizeBytes = reader.GetInt64("FileSize"),
          HiveCount = reader.GetInt32("HiveCount"),
          TotalBees = reader.GetInt32("TotalBees"),
          SimulationTicks = reader.GetInt64("SimulationTicks"),
          Description = $"Database save from {reader.GetDateTime("CreatedAt"):yyyy-MM-dd HH:mm}"
        });
      }

      return saves;
    }

    /// <summary>
    /// Creates a backup of the current save data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CreateBackupAsync(CancellationToken cancellationToken = default)
    {
      // For SQLite, backup involves copying the database file
      try
      {
        string backupDirectory = Path.Combine(Path.GetDirectoryName(_config.DatabaseFilePath)!, "backups");
        Directory.CreateDirectory(backupDirectory);

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string backupPath = Path.Combine(backupDirectory, $"hivemind_backup_{timestamp}.db");

        File.Copy(_config.DatabaseFilePath, backupPath);

        _logger.LogInformation("Database backup created at {BackupPath}", backupPath);
        await Task.CompletedTask;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to create database backup");
        throw new PersistenceException("Failed to create database backup", ex);
      }
    }

    /// <summary>
    /// Performs cleanup of old backup files based on retention policy.
    /// </summary>
    /// <param name="maxBackups">Maximum number of backups to keep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CleanupBackupsAsync(int maxBackups, CancellationToken cancellationToken = default)
    {
      using SqliteConnection connection = new(_connectionString);
      await connection.OpenAsync(cancellationToken);

      const string deleteSql = """
        DELETE FROM SimulationStates 
        WHERE SaveId NOT IN (
          SELECT SaveId FROM SimulationStates ORDER BY CreatedAt DESC LIMIT @MaxBackups
        )
      """;

      using SqliteCommand command = new(deleteSql, connection);
      command.Parameters.AddWithValue("@MaxBackups", maxBackups);

      int deletedRows = await command.ExecuteNonQueryAsync(cancellationToken);
      if (deletedRows > 0)
        _logger.LogInformation("Cleaned up {DeletedRows} old simulation saves", deletedRows);
    }

    /// <summary>
    /// Initializes the database schema.
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
      using SqliteConnection connection = new(_connectionString);
      await connection.OpenAsync();

      const string createTablesSql = """
        CREATE TABLE IF NOT EXISTS SimulationStates (
          SaveId TEXT PRIMARY KEY,
          CreatedAt DATETIME NOT NULL,
          StateData TEXT NOT NULL,
          HiveCount INTEGER NOT NULL,
          TotalBees INTEGER NOT NULL,
          SimulationTicks INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Beehives (
          HiveId TEXT NOT NULL,
          SavedAt DATETIME NOT NULL,
          HiveData TEXT NOT NULL,
          PRIMARY KEY (HiveId, SavedAt)
        );

        CREATE TABLE IF NOT EXISTS Environment (
          Id INTEGER PRIMARY KEY,
          SavedAt DATETIME NOT NULL,
          EnvironmentData TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS LatestSave (
          Id INTEGER PRIMARY KEY,
          SaveId TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_simulationstates_createdat ON SimulationStates(CreatedAt);
        CREATE INDEX IF NOT EXISTS idx_beehives_savedat ON Beehives(SavedAt);
      """;

      using SqliteCommand command = new(createTablesSql, connection);
      await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Internal method to save beehive data within a transaction.
    /// </summary>
    private async Task SaveBeehiveInternalAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      Beehive beehive,
      CancellationToken cancellationToken
    )
    {
      const string insertSql = """
        INSERT INTO Beehives (HiveId, SavedAt, HiveData)
        VALUES (@HiveId, @SavedAt, @HiveData)
      """;

      using SqliteCommand command = new(insertSql, connection, transaction);
      command.Parameters.AddWithValue("@HiveId", beehive.Id.ToString());
      command.Parameters.AddWithValue("@SavedAt", DateTime.UtcNow);
      command.Parameters.AddWithValue("@HiveData", JsonSerializer.Serialize(beehive, _serializerOptions));

      await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Generates a unique save identifier.
    /// </summary>
    private static string GenerateSaveId() => DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");

    /// <summary>
    /// Disposes the repository resources.
    /// </summary>
    public void Dispose()
    {
      // SQLite connections are disposed automatically
      // No explicit cleanup needed for this implementation
    }
  }
}