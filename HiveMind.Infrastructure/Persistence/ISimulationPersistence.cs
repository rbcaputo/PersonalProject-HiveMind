namespace HiveMind.Infrastructure.Persistence
{
  /// <summary>
  /// Interface for simulation persistence operations
  /// </summary>
  public interface ISimulationPersistence
  {
    /// <summary>
    /// Saves a simulation snapshot
    /// </summary>
    Task<string> SaveSnapshotAsync(SimulationSnapshot snapshot);

    /// <summary>
    /// Loads a simulation snapshot
    /// </summary>
    Task<SimulationSnapshot?> LoadSnapshotAsync(string snapshotId);

    /// <summary>
    /// Gets all available snapshots
    /// </summary>
    Task<IEnumerable<SimulationSnapshot>> GetSnapshotsAsync();

    /// <summary>
    /// Deletes a snapshot
    /// </summary>
    Task<bool> DeleteSnapshotAsync(string snapshotId);

    /// <summary>
    /// Exports simulation data
    /// </summary>
    Task<string> ExportSimulationDataAsync(string snapshotId, string format = "json");
  }
}
