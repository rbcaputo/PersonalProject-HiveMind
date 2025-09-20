using HiveMind.Core.Entities;
using HiveMind.Core.Simulation;

namespace HiveMind.Core.Repositories
{
  /// <summary>
  /// Repository interface for persisting and loading simulation state data.
  /// </summary>
  public interface ISimulationRepository
  {
    /// <summary>
    /// Saves the complete simulation state to persistent storage.
    /// </summary>
    /// <param name="state">The simulation state to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the save operation.</returns>
    Task SaveSimulationStateAsync(SimulationState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the most recent simulation state from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded simulation state, or null if no saved state exists.</returns>
    Task<SimulationState?> LoadSimulationStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a beehive to persistent storage.
    /// </summary>
    /// <param name="beehive">The beehive to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the save operation.</returns>
    Task SaveBeehiveAsync(Beehive beehive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a beehive by its unique identifier.
    /// </summary>
    /// <param name="hiveId">The unique identifier of the hive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded beehive, or null if not found.</returns>
    Task<Beehive?> LoadBeehiveAsync(Guid hiveId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all beehives from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all saved beehives.</returns>
    Task<IEnumerable<Beehive>> LoadAllBeehivesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the environment state to persistent storage.
    /// </summary>
    /// <param name="environment">The environment to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the save operation.</returns>
    Task SaveEnvironmentAsync(Entities.Environment environment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the environment state from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded environment, or null if not found.</returns>
    Task<Entities.Environment?> LoadEnvironmentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific save file.
    /// </summary>
    /// <param name="saveId">The identifier of the save to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the delete operation.</returns>
    Task DeleteSaveAsync(string saveId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about all available saves.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of save information.</returns>
    Task<IEnumerable<SaveInfo>> GetAvailableSavesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a backup of the current save data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the backup operation.</returns>
    Task CreateBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs cleanup of old backup files based on retention policy.
    /// </summary>
    /// <param name="maxBackups">Maximum number of backups to keep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the cleanup operation.</returns>
    Task CleanupBackupsAsync(int maxBackups, CancellationToken cancellationToken = default);
  }

  /// <summary>
  /// Information about a saved simulation state.
  /// </summary>
  public sealed class SaveInfo
  {
    /// <summary>
    /// Gets or sets the unique identifier for the save.
    /// </summary>
    public string SaveId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the save was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the size of the save file in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of hives in the save.
    /// </summary>
    public int HiveCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of bees in the save.
    /// </summary>
    public int TotalBees { get; set; }

    /// <summary>
    /// Gets or sets the simulation tick number at save time.
    /// </summary>
    public long SimulationTicks { get; set; }

    /// <summary>
    /// Gets or sets a description of the save.
    /// </summary>
    public string Description { get; set; } = string.Empty;
  }
}
