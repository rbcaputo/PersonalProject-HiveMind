namespace HiveMind.Infrastructure.Configuration
{
  /// <summary>
  /// Application settings for the simulation
  /// </summary>
  public class SimulationSettings
  {
    public const string SectionName = "Simulation";

    /// <summary>
    /// Default environment width
    /// </summary>
    public double DefaultEnvironmentWidth { get; set; } = 200.0;

    /// <summary>
    /// Default environment height
    /// </summary>
    public double DefaultEnvironmentHeight { get; set; } = 200.0;

    /// <summary>
    /// Default target ticks per second
    /// </summary>
    public int DefaultTargetTPS { get; set; } = 30;

    /// <summary>
    /// Default maximum colony population
    /// </summary>
    public int DefaultMaxColonyPopulation { get; set; } = 500;

    /// <summary>
    /// Path for saving simulation snapshots
    /// </summary>
    public string SnapshotsPath { get; set; } = string.Empty;

    /// <summary>
    /// Enable performance logging
    /// </summary>
    public bool EnablePerformanceLogging { get; set; } = true;

    /// <summary>
    /// Maximum number of log entries to keep in memory
    /// </summary>
    public int MaxLogEntries { get; set; } = 1000;

    /// <summary>
    /// Auto-save interval in ticks (0 = disabled)
    /// </summary>
    public long AutoSaveInterval { get; set; } = 0;

    /// <summary>
    /// Maximum number of auto-save snapshots to keep
    /// </summary>
    public int MaxAutoSaveSnapshots { get; set; } = 10;
  }
}
