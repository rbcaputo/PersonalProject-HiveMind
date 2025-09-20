using HiveMind.Core.Entities;
using HiveMind.Core.Enums;
using HiveMind.Core.Simulation;
using HiveMind.Core.ValueObject;

namespace HiveMind.Core.Monitoring
{
  /// <summary>
  /// Complete monitoring data for a single beehive at a specific point in time.
  /// </summary>
  public sealed class HiveMonitoringData
  {
    /// <summary>
    /// Gets or sets the unique identifier of the hive.
    /// </summary>
    public Guid HiveId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this data was collected.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets population-related metrics.
    /// </summary>
    public PopulationMetrics Population { get; set; } = new();

    /// <summary>
    /// Gets or sets production-related metrics.
    /// </summary>
    public ProductionMetrics Production { get; set; } = new();

    /// <summary>
    /// Gets or sets environment-related metrics.
    /// </summary>
    public EnvironmentMetrics Environment { get; set; } = new();

    /// <summary>
    /// Gets or sets the physical location of the hive.
    /// </summary>
    public Position3D Location { get; set; } = Position3D.Origin;
  }

  /// <summary>
  /// Population metrics for a beehive.
  /// </summary>
  public sealed class PopulationMetrics
  {
    /// <summary>
    /// Gets or sets the total number of living bees in the hive.
    /// </summary>
    public int TotalBees { get; set; }

    /// <summary>
    /// Gets or sets the number of worker bees.
    /// </summary>
    public int Workers { get; set; }

    /// <summary>
    /// Gets or sets the number of drone bees.
    /// </summary>
    public int Drones { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the hive has a living queen.
    /// </summary>
    public bool HasQueen { get; set; }

    /// <summary>
    /// Gets or sets the overall health status of the colony.
    /// </summary>
    public ColonyHealth HealthStatus { get; set; }

    /// <summary>
    /// Gets or sets the population growth rate (bees per day).
    /// </summary>
    public double GrowthRate { get; set; }

    /// <summary>
    /// Gets or sets the population density (bees per chamber).
    /// </summary>
    public double PopulationDensity { get; set; }
  }

  /// <summary>
  /// Production metrics for a beehive.
  /// </summary>
  public sealed class ProductionMetrics
  {
    /// <summary>
    /// Gets or sets the current amount of honey stored in the hive.
    /// </summary>
    public double CurrentHoneyStored { get; set; }

    /// <summary>
    /// Gets or sets the total honey ever produced by this hive.
    /// </summary>
    public double TotalHoneyProduced { get; set; }

    /// <summary>
    /// Gets or sets the honey production rate (units per day).
    /// </summary>
    public double HoneyProductionRate { get; set; }

    /// <summary>
    /// Gets or sets the number of available brood cells.
    /// </summary>
    public int AvailableBroodCells { get; set; }

    /// <summary>
    /// Gets or sets the total number of brood chambers.
    /// </summary>
    public int BroodChamberCount { get; set; }

    /// <summary>
    /// Gets or sets the number of honey supers.
    /// </summary>
    public int HoneySuperCount { get; set; }

    /// <summary>
    /// Gets or sets the percentage of honey storage capacity used.
    /// </summary>
    public double StorageUtilization { get; set; }

    /// <summary>
    /// Gets or sets the number of eggs laid in the last 24 hours.
    /// </summary>
    public int RecentEggsLaid { get; set; }
  }

  /// <summary>
  /// Environmental metrics affecting the hive.
  /// </summary>
  public sealed class EnvironmentMetrics
  {
    /// <summary>
    /// Gets or sets the current temperature in Celsius.
    /// </summary>
    public double Temperature { get; set; }

    /// <summary>
    /// Gets or sets the current humidity percentage.
    /// </summary>
    public double Humidity { get; set; }

    /// <summary>
    /// Gets or sets the current weather type.
    /// </summary>
    public WeatherType WeatherType { get; set; }

    /// <summary>
    /// Gets or sets the current season.
    /// </summary>
    public Season Season { get; set; }

    /// <summary>
    /// Gets or sets the current foraging efficiency (0.0 to 3.0+).
    /// </summary>
    public double ForagingEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the number of active environmental events.
    /// </summary>
    public int ActiveEventCount { get; set; }

    /// <summary>
    /// Gets or sets the wind speed in km/h.
    /// </summary>
    public double WindSpeed { get; set; }

    /// <summary>
    /// Gets or sets the atmospheric pressure in hPa.
    /// </summary>
    public double AtmosphericPressure { get; set; }
  }

  /// <summary>
  /// Overall simulation metrics.
  /// </summary>
  public sealed class SimulationMetrics
  {
    /// <summary>
    /// Gets or sets the timestamp when these metrics were collected.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the total simulation ticks processed.
    /// </summary>
    public long TotalTicks { get; set; }

    /// <summary>
    /// Gets or sets the current simulation status.
    /// </summary>
    public SimulationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the total number of hives in the simulation.
    /// </summary>
    public int TotalHives { get; set; }

    /// <summary>
    /// Gets or sets the number of viable hives.
    /// </summary>
    public int ViableHives { get; set; }

    /// <summary>
    /// Gets or sets the total number of living bees across all hives.
    /// </summary>
    public int TotalLivingBees { get; set; }

    /// <summary>
    /// Gets or sets the total number of worker bees.
    /// </summary>
    public int TotalWorkers { get; set; }

    /// <summary>
    /// Gets or sets the total number of drone bees.
    /// </summary>
    public int TotalDrones { get; set; }

    /// <summary>
    /// Gets or sets the total number of queens.
    /// </summary>
    public int TotalQueens { get; set; }

    /// <summary>
    /// Gets or sets the total honey produced across all hives.
    /// </summary>
    public double TotalHoneyProduced { get; set; }

    /// <summary>
    /// Gets or sets the total bees produced across all hives.
    /// </summary>
    public int TotalBeesProduced { get; set; }

    /// <summary>
    /// Gets or sets the current environmental snapshot.
    /// </summary>
    public EnvironmentSnapshot CurrentEnvironment { get; set; } = new();

    /// <summary>
    /// Gets or sets the simulation uptime.
    /// </summary>
    public TimeSpan SimulationUptime { get; set; }

    /// <summary>
    /// Gets or sets the average ticks per second processing rate.
    /// </summary>
    public double TicksPerSecond { get; set; }
  }

  /// <summary>
  /// Aggregated statistics across all monitored hives.
  /// </summary>
  public sealed class AggregatedHiveStats
  {
    /// <summary>
    /// Gets or sets the total number of hives being monitored.
    /// </summary>
    public int TotalHives { get; set; }

    /// <summary>
    /// Gets or sets the number of healthy hives.
    /// </summary>
    public int HealthyHives { get; set; }

    /// <summary>
    /// Gets or sets the average population across all hives.
    /// </summary>
    public double AveragePopulation { get; set; }

    /// <summary>
    /// Gets or sets the total honey stored across all hives.
    /// </summary>
    public double TotalHoneyStored { get; set; }

    /// <summary>
    /// Gets or sets the average foraging efficiency across all hives.
    /// </summary>
    public double AverageForagingEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the number of hives with living queens.
    /// </summary>
    public int QueenedHives { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last update.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Gets or sets the strongest hive population.
    /// </summary>
    public int StrongestHivePopulation { get; set; }

    /// <summary>
    /// Gets or sets the weakest hive population.
    /// </summary>
    public int WeakestHivePopulation { get; set; }

    /// <summary>
    /// Gets or sets the total production rate across all hives.
    /// </summary>
    public double TotalProductionRate { get; set; }
  }

  /// <summary>
  /// A monitoring snapshot capturing the state at a specific time.
  /// </summary>
  public sealed class MonitoringSnapshot
  {
    /// <summary>
    /// Gets or sets the timestamp of this snapshot.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the simulation metrics at snapshot time.
    /// </summary>
    public SimulationMetrics SimulationMetrics { get; set; } = new();

    /// <summary>
    /// Gets or sets the aggregated hive statistics.
    /// </summary>
    public AggregatedHiveStats AggregatedHiveStats { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of hives being monitored.
    /// </summary>
    public int HiveCount { get; set; }

    /// <summary>
    /// Gets or sets the active monitoring alerts.
    /// </summary>
    public List<MonitoringAlert> ActiveAlerts { get; set; } = [];
  }

  /// <summary>
  /// Performance statistics for the monitoring system itself.
  /// </summary>
  public sealed class MonitoringPerformanceStats
  {
    /// <summary>
    /// Gets or sets the number of hives being tracked.
    /// </summary>
    public int TrackedHives { get; set; }

    /// <summary>
    /// Gets or sets the number of snapshots stored.
    /// </summary>
    public int SnapshotCount { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory usage in bytes.
    /// </summary>
    public long MemoryUsageEstimate { get; set; }

    /// <summary>
    /// Gets or sets the last update time.
    /// </summary>
    public DateTime LastUpdateTime { get; set; }

    /// <summary>
    /// Gets or sets the monitoring update frequency (updates per second).
    /// </summary>
    public double UpdateFrequency { get; set; }
  }
}
