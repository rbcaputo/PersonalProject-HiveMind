namespace HiveMind.Infrastructure.Configuration
{
  /// <summary>
  /// Configuration settings for the persistence layer.
  /// </summary>
  public sealed class PersistenceConfiguration
  {
    /// <summary>
    /// Gets or sets the directory where save files are stored.
    /// </summary>
    public string SaveDirectory { get; set; } = "SaveData";

    /// <summary>
    /// Gets or sets the path to the SQLite database file.
    /// </summary>
    public string DatabaseFilePath { get; set; } = "SaveData/hivemind.db";

    /// <summary>
    /// Gets or sets the persistence provider type (Json or Sqlite).
    /// </summary>
    public PersistenceProvider Provider { get; set; } = PersistenceProvider.Json;

    /// <summary>
    /// Gets or sets the maximum number of backup saves to retain.
    /// </summary>
    public int MaxBackups { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether to compress save files.
    /// </summary>
    public bool CompressSaveFiles { get; set; } = true;

    /// <summary>
    /// Gets or sets the auto-save interval in minutes.
    /// </summary>
    public int AutoSaveIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether auto-save is enabled.
    /// </summary>
    public bool EnableAutoSave { get; set; } = true;

    /// <summary>
    /// Gets or sets the backup creation interval in hours.
    /// </summary>
    public int BackupIntervalHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets a value indicating whether to create automatic backups.
    /// </summary>
    public bool EnableAutomaticBackups { get; set; } = true;
  }

  /// <summary>
  /// Available persistence provider types.
  /// </summary>
  public enum PersistenceProvider
  {
    /// <summary>
    /// JSON file-based persistence.
    /// </summary>
    Json = 0,

    /// <summary>
    /// SQLite database-based persistence.
    /// </summary>
    Sqlite = 1
  }
}
