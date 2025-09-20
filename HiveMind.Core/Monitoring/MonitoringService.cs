using HiveMind.Core.Entities;
using HiveMind.Core.Simulation;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace HiveMind.Core.Monitoring
{
  /// <summary>
  /// Central monitoring service that tracks and aggregates data from all simulation components.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="MonitoringService"/> class.
  /// </remarks>
  /// <param name="logger">Logger instance.</param>
  /// <param name="snapshotInterval">Interval between monitoring snapshots.</param>
  public sealed class MonitoringService(
    ILogger<MonitoringService> logger,
    TimeSpan? snapshotInterval = null
  )
  {
    private readonly ILogger<MonitoringService> _logger =
      logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<Guid, HiveMonitoringData> _hiveData = new();
    private readonly ConcurrentQueue<MonitoringSnapshot> _snapshots = new();
    private readonly Lock _statsLock = new();

    private SimulationMetrics _currentMetrics = new();
    private DateTime _lastSnapshotTime = DateTime.UtcNow;
    private readonly TimeSpan _snapshotInterval = snapshotInterval ?? TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets the current simulation metrics.
    /// </summary>
    public SimulationMetrics CurrentMetrics
    {
      get
      {
        lock (_statsLock)
          return _currentMetrics;
      }
    }

    /// <summary>
    /// Gets the recent monitoring snapshots.
    /// </summary>
    public IEnumerable<MonitoringSnapshot> RecentSnapshots =>
      _snapshots.TakeLast(100); // Keep last 100 snapshots

    /// <summary>
    /// Event raised when significant changes occur in monitored data.
    /// </summary>
    public event EventHandler<MonitoringAlertArgs>? MonitoringAlert;

    /// <summary>
    /// Updates monitoring data for a specific beehive.
    /// </summary>
    /// <param name="beehive">The beehive to monitor.</param>
    /// <param name="environment">Current environmental conditions.</param>
    public void UpdateHiveData(Beehive beehive, Entities.Environment environment)
    {
      ArgumentNullException.ThrowIfNull(beehive);
      ArgumentNullException.ThrowIfNull(environment);

      BeehiveStats hiveStats = beehive.GetStats();
      var environmentStats = environment.GetEnvironmentalStats();

      HiveMonitoringData monitoringData = new()
      {
        HiveId = beehive.Id,
        Timestamp = DateTime.UtcNow,
        Population = new()
        {
          TotalBees = hiveStats.TotalPopulation,
          Workers = hiveStats.WorkerPopulation,
          Drones = hiveStats.DronePopulation,
          HasQueen = hiveStats.HasQueen,
          HealthStatus = hiveStats.HealthStatus
        },
        Production = new()
        {
          CurrentHoneyStored = hiveStats.CurrentHoneyStored,
          TotalHoneyProduced = hiveStats.TotalHoneyProduced,
          AvailableBroodCells = hiveStats.AvailableBroodCells,
          BroodChamberCount = hiveStats.BroodChamberCount,
          HoneySuperCount = hiveStats.HoneySuperCount
        },
        Environment = new()
        {
          Temperature = environmentStats.Temperature,
          Humidity = environmentStats.Humidity,
          WeatherType = environmentStats.WeatherType,
          Season = environmentStats.CurrentSeason,
          ForagingEfficiency = environmentStats.ForagingEfficiency,
          ActiveEventCount = environmentStats.ActiveEventCount
        },
        Location = beehive.Location
      };

      _hiveData.AddOrUpdate(beehive.Id, monitoringData, (key, existing) => monitoringData);

      // Check for alerts
      CheckForAlerts(monitoringData, beehive);
    }

    /// <summary>
    /// Updates overall simulation metrics.
    /// </summary>
    /// <param name="simulationState">Current simulation state.</param>
    public void UpdateSimulationMetrics(SimulationState simulationState)
    {
      ArgumentNullException.ThrowIfNull(simulationState);

      lock (_statsLock)
      {
        SimulationStats stats = simulationState.GetStats();

        _currentMetrics = new()
        {
          Timestamp = DateTime.UtcNow,
          TotalTicks = stats.TotalTicks,
          Status = stats.Status,
          TotalHives = stats.TotalHives,
          ViableHives = stats.ViableHives,
          TotalLivingBees = stats.TotalLivingBees,
          TotalWorkers = stats.TotalWorkers,
          TotalDrones = stats.TotalDrones,
          TotalQueens = stats.TotalQueens,
          TotalHoneyProduced = stats.TotalHoneyProduced,
          TotalBeesProduced = stats.TotalBeesProduced,
          CurrentEnvironment = stats.CurrentEnvironment
        };
      }

      // Create snapshot if interval has passed
      if (DateTime.UtcNow - _lastSnapshotTime >= _snapshotInterval)
      {
        CreateMonitoringSnapshot();
        _lastSnapshotTime = DateTime.UtcNow;
      }
    }

    /// <summary>
    /// Gets detailed monitoring data for a specific hive.
    /// </summary>
    /// <param name="hiveId">ID of the hive to get data for.</param>
    /// <returns>Monitoring data for the hive, or null if not found.</returns>
    public HiveMonitoringData? GetHiveData(Guid hiveId) =>
      _hiveData.TryGetValue(hiveId, out HiveMonitoringData? data) ? data : null;

    /// <summary>
    /// Gets monitoring data for all tracked hives.
    /// </summary>
    /// <returns>Collection of all hive monitoring data.</returns>
    public IEnumerable<HiveMonitoringData> GetAllHiveData() =>
      [.. _hiveData.Values.OrderBy(h => h.Timestamp)];

    /// <summary>
    /// Gets aggregated statistics across all monitored hives.
    /// </summary>
    /// <returns>Aggregated hive statistics.</returns>
    public AggregatedHiveStats GetAggregatedStats()
    {
      List<HiveMonitoringData> allHiveData = [.. _hiveData.Values];

      if (allHiveData.Count == 0) return new();

      return new()
      {
        TotalHives = allHiveData.Count,
        HealthyHives = allHiveData.Count(h => h.Population.HealthStatus == ColonyHealth.Good),
        AveragePopulation = allHiveData.Average(h => h.Population.TotalBees),
        TotalHoneyStored = allHiveData.Sum(h => h.Production.CurrentHoneyStored),
        AverageForagingEfficiency = allHiveData.Average(h => h.Environment.ForagingEfficiency),
        QueenedHives = allHiveData.Count(h => h.Population.HasQueen),
        LastUpdated = allHiveData.Max(h => h.Timestamp)
      };
    }

    /// <summary>
    /// Creates a monitoring snapshot of current conditions.
    /// </summary>
    private void CreateMonitoringSnapshot()
    {
      MonitoringSnapshot snapshot = new()
      {
        Timestamp = DateTime.UtcNow,
        SimulationMetrics = CurrentMetrics,
        AggregatedHiveStats = GetAggregatedStats(),
        HiveCount = _hiveData.Count,
        ActiveAlerts = [.. GetActiveAlerts()]
      };

      _snapshots.Enqueue(snapshot);

      // Keep only recent snapshots
      while (_snapshots.Count > 1000)
        _snapshots.TryDequeue(out _);

      _logger.LogDebug(
        "Created monitoring snapshot: {HiveCount} hives, {TotalBees} bees, {Alerts} alerts",
        snapshot.HiveCount,
        snapshot.SimulationMetrics.TotalLivingBees,
        snapshot.ActiveAlerts.Count
      );
    }

    /// <summary>
    /// Checks for monitoring alerts based on hive data.
    /// </summary>
    /// <param name="data">Hive monitoring data to check.</param>
    /// <param name="beehive">The beehive being monitored.</param>
    private void CheckForAlerts(HiveMonitoringData data, Beehive beehive)
    {
      // Population alerts
      if (!data.Population.HasQueen)
        RaiseAlert(
          AlertSeverity.Critical,
          AlertType.QueenLoss,
          $"Hive {beehive.Id:N} has lost its queen",
          data
        );

      if (data.Population.TotalBees < Beehive.MinViableWorkerCount)
        RaiseAlert(
          AlertSeverity.Critical,
          AlertType.PopulationCollapse,
          $"Hive {beehive.Id:N} population critically low: {data.Population.TotalBees} bees",
          data
        );
      else if (data.Population.TotalBees < Beehive.MinViableWorkerCount * 2)
        RaiseAlert(
          AlertSeverity.Warning,
          AlertType.LowPopulation,
          $"Hive {beehive.Id:N} population declining: {data.Population.TotalBees} bees",
          data
        );

      // Production alerts
      if (data.Production.AvailableBroodCells == 0 && data.Population.HasQueen)
        RaiseAlert(
          AlertSeverity.Warning,
          AlertType.NoAvailableCells,
          $"Hive {beehive.Id:N} has no available brood cells for egg laying",
          data
        );

      // Environmental alerts
      if (data.Environment.ForagingEfficiency < 0.1)
        RaiseAlert(
          AlertSeverity.Warning,
          AlertType.PoorForagingConditions,
          $"Hive {beehive.Id:N} experiencing poor foraging conditions: {data.Environment.ForagingEfficiency:P0} efficiency",
          data
        );

      // Health alerts
      if (data.Population.HealthStatus == ColonyHealth.Critical)
        RaiseAlert(
          AlertSeverity.Critical,
          AlertType.ColonyCritical,
          $"Hive {beehive.Id:N} in critical condition",
          data
        );
    }

    /// <summary>
    /// Raises a monitoring alert.
    /// </summary>
    /// <param name="severity">Severity of the alert.</param>
    /// <param name="type">Type of alert.</param>
    /// <param name="message">Alert message.</param>
    /// <param name="data">Related monitoring data.</param>
    private void RaiseAlert(AlertSeverity severity, AlertType type, string message, HiveMonitoringData data)
    {
      MonitoringAlert alert = new()
      {
        Id = Guid.NewGuid(),
        Timestamp = DateTime.UtcNow,
        Severity = severity,
        Type = type,
        Message = message,
        HiveId = data.HiveId,
        Data = data
      };

      _logger.LogWarning(
        "Monitoring Alert [{Severity}] {Type}: {Message}",
        severity,
        type,
        message
      );

      MonitoringAlert?.Invoke(this, new(alert));
    }

    /// <summary>
    /// Gets currently active alerts.
    /// </summary>
    /// <returns>Collection of active monitoring alerts.</returns>
    private IEnumerable<MonitoringAlert> GetActiveAlerts()
    {
      // In a full implementation, this would track alerts over time
      // For now, return empty collection as alerts are event-driven
      return [];
    }

    /// <summary>
    /// Clears monitoring data for a specific hive (when hive is removed).
    /// </summary>
    /// <param name="hiveId">ID of the hive to remove from monitoring.</param>
    public void RemoveHive(Guid hiveId)
    {
      _hiveData.TryRemove(hiveId, out _);
      _logger.LogInformation("Removed hive {HiveId} from monitoring", hiveId);
    }

    /// <summary>
    /// Gets monitoring statistics for performance analysis.
    /// </summary>
    /// <returns>Performance statistics for the monitoring system.</returns>
    public MonitoringPerformanceStats GetPerformanceStats() => new()
    {
      TrackedHives = _hiveData.Count,
      SnapshotCount = _snapshots.Count,
      MemoryUsageEstimate = EstimateMemoryUsage(),
      LastUpdateTime = _lastSnapshotTime
    };

    /// <summary>
    /// Estimates memory usage of the monitoring system.
    /// </summary>
    /// <returns>Estimated memory usage in bytes.</returns>
    private long EstimateMemoryUsage()
    {
      // Rough estimate based on object sizes
      int hiveDataSize = _hiveData.Count * 1024; // ~1KB per hive data
      int snapshotSize = _snapshots.Count * 2048; // ~2KB per snapshot
      return hiveDataSize + snapshotSize;
    }
  }
}
