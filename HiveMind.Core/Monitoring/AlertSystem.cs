using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace HiveMind.Core.Monitoring
{
  /// <summary>
  /// Represents a monitoring alert raised when significant conditions are detected.
  /// </summary>
  public sealed class MonitoringAlert
  {
    /// <summary>
    /// Gets or sets the unique identifier for this alert.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the alert was raised.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the severity level of the alert.
    /// </summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the type of alert.
    /// </summary>
    public AlertType Type { get; set; }

    /// <summary>
    /// Gets or sets the human-readable alert message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the hive this alert relates to (if applicable).
    /// </summary>
    public Guid? HiveId { get; set; }

    /// <summary>
    /// Gets or sets the monitoring data that triggered this alert.
    /// </summary>
    public HiveMonitoringData? Data { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this alert has been acknowledged.
    /// </summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>
    /// Gets or sets the time when the alert was acknowledged.
    /// </summary>
    public DateTime? AcknowledgedTime { get; set; }
  }

  /// <summary>
  /// Severity levels for monitoring alerts.
  /// </summary>
  public enum AlertSeverity
  {
    /// <summary>
    /// Informational alert - no action required.
    /// </summary>
    Info = 1,

    /// <summary>
    /// Warning alert - attention recommended.
    /// </summary>
    Warning = 2,

    /// <summary>
    /// Critical alert - immediate attention required.
    /// </summary>
    Critical = 3,

    /// <summary>
    /// Emergency alert - colony survival at risk.
    /// </summary>
    Emergency = 4
  }

  /// <summary>
  /// Types of monitoring alerts that can be raised.
  /// </summary>
  public enum AlertType
  {
    /// <summary>
    /// Queen has been lost from the hive.
    /// </summary>
    QueenLoss = 1,

    /// <summary>
    /// Population has collapsed below viable levels.
    /// </summary>
    PopulationCollapse = 2,

    /// <summary>
    /// Population is declining but still viable.
    /// </summary>
    LowPopulation = 3,

    /// <summary>
    /// No available cells for egg laying.
    /// </summary>
    NoAvailableCells = 4,

    /// <summary>
    /// Foraging conditions are very poor.
    /// </summary>
    PoorForagingConditions = 5,

    /// <summary>
    /// Honey stores are critically low.
    /// </summary>
    LowHoneyStores = 6,

    /// <summary>
    /// Colony is in critical overall condition.
    /// </summary>
    ColonyCritical = 7,

    /// <summary>
    /// Environmental event is impacting the hive.
    /// </summary>
    EnvironmentalStress = 8,

    /// <summary>
    /// Overcrowding in the hive.
    /// </summary>
    Overcrowding = 9,

    /// <summary>
    /// Swarming conditions detected.
    /// </summary>
    SwarmingConditions = 10,

    /// <summary>
    /// Simulation performance issue.
    /// </summary>
    PerformanceIssue = 11
  }

  /// <summary>
  /// Event arguments for monitoring alerts.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="MonitoringAlertArgs"/> class.
  /// </remarks>
  /// <param name="alert">The alert that was raised.</param>
  public sealed class MonitoringAlertArgs(MonitoringAlert alert) : EventArgs
  {
    /// <summary>
    /// Gets the monitoring alert that was raised.
    /// </summary>
    public MonitoringAlert Alert { get; } =
      alert ?? throw new ArgumentNullException(nameof(alert));
  }

  /// <summary>
  /// Service for managing and tracking monitoring alerts over time.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="AlertManager"/> class.
  /// </remarks>
  /// <param name="logger">Logger instance.</param>
  public sealed class AlertManager(ILogger<AlertManager> logger)
  {
    private readonly ILogger<AlertManager> _logger =
      logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<Guid, MonitoringAlert> _activeAlerts = new();
    private readonly ConcurrentQueue<MonitoringAlert> _alertHistory = new();
    private readonly Lock _alertLock = new();

    /// <summary>
    /// Gets the currently active alerts.
    /// </summary>
    public IEnumerable<MonitoringAlert> ActiveAlerts => _activeAlerts.Values.OrderByDescending(a => a.Timestamp);

    /// <summary>
    /// Gets the recent alert history.
    /// </summary>
    public IEnumerable<MonitoringAlert> RecentAlerts => _alertHistory.TakeLast(200);

    /// <summary>
    /// Event raised when a new alert is activated.
    /// </summary>
    public event EventHandler<MonitoringAlertArgs>? AlertActivated;

    /// <summary>
    /// Event raised when an alert is resolved.
    /// </summary>
    public event EventHandler<MonitoringAlertArgs>? AlertResolved;

    /// <summary>
    /// Raises a new monitoring alert.
    /// </summary>
    /// <param name="alert">The alert to raise.</param>
    public void RaiseAlert(MonitoringAlert alert)
    {
      ArgumentNullException.ThrowIfNull(alert);

      lock (_alertLock)
      {
        // Check if similar alert already exists for this hive
        MonitoringAlert? existingAlert = _activeAlerts.Values.FirstOrDefault(a =>
          a.HiveId == alert.HiveId &&
          a.Type == alert.Type
        );

        if (existingAlert != null)
        {
          // Update existing alert timestamp instead of creating duplicate
          existingAlert.Timestamp = alert.Timestamp;
          existingAlert.Message = alert.Message;
          return;
        }

        _activeAlerts.TryAdd(alert.Id, alert);
        _alertHistory.Enqueue(alert);

        // Keep history size manageable
        while (_alertHistory.Count > 1000) _alertHistory.TryDequeue(out _);
      }

      _logger.LogWarning(
        "Alert raised [{Severity}] {Type}: {Message}",
        alert.Severity,
        alert.Type,
        alert.Message
      );

      AlertActivated?.Invoke(this, new(alert));
    }

    /// <summary>
    /// Resolves an active alert.
    /// </summary>
    /// <param name="alertId">ID of the alert to resolve.</param>
    public void ResolveAlert(Guid alertId)
    {
      if (_activeAlerts.TryRemove(alertId, out var alert))
      {
        _logger.LogInformation(
          "Alert resolved: {Type} for hive {HiveId}",
          alert.Type,
          alert.HiveId
        );

        AlertResolved?.Invoke(this, new(alert));
      }
    }

    /// <summary>
    /// Acknowledges an alert without resolving it.
    /// </summary>
    /// <param name="alertId">ID of the alert to acknowledge.</param>
    public void AcknowledgeAlert(Guid alertId)
    {
      if (_activeAlerts.TryGetValue(alertId, out var alert))
      {
        alert.IsAcknowledged = true;
        alert.AcknowledgedTime = DateTime.UtcNow;

        _logger.LogInformation(
          "Alert acknowledged: {Type} for hive {HiveId}",
          alert.Type,
          alert.HiveId
        );
      }
    }

    /// <summary>
    /// Gets alerts for a specific hive.
    /// </summary>
    /// <param name="hiveId">ID of the hive.</param>
    /// <returns>Active alerts for the specified hive.</returns>
    public IEnumerable<MonitoringAlert> GetHiveAlerts(Guid hiveId) =>
      _activeAlerts.Values.Where(a => a.HiveId == hiveId)
                          .OrderByDescending(a => a.Timestamp);

    /// <summary>
    /// Gets alerts by severity level.
    /// </summary>
    /// <param name="severity">Minimum severity level.</param>
    /// <returns>Active alerts at or above the specified severity.</returns>
    public IEnumerable<MonitoringAlert> GetAlertsBySeverity(AlertSeverity severity) =>
      _activeAlerts.Values.Where(a => a.Severity >= severity)
                          .OrderByDescending(a => a.Severity)
                          .ThenByDescending(a => a.Timestamp);

    /// <summary>
    /// Clears all alerts for a specific hive (when hive is removed).
    /// </summary>
    /// <param name="hiveId">ID of the hive to clear alerts for.</param>
    public void ClearHiveAlerts(Guid hiveId)
    {
      List<MonitoringAlert> hiveAlerts = [.. _activeAlerts.Values.Where(a => a.HiveId == hiveId)];

      foreach (MonitoringAlert alert in hiveAlerts)
        _activeAlerts.TryRemove(alert.Id, out _);

      _logger.LogInformation(
        "Cleared {Count} alerts for removed hive {HiveId}",
        hiveAlerts.Count,
        hiveId
      );
    }

    /// <summary>
    /// Gets alert statistics for analysis.
    /// </summary>
    /// <returns>Alert statistics summary.</returns>
    public AlertStatistics GetAlertStatistics()
    {
      List<MonitoringAlert> activeAlerts = [.. _activeAlerts.Values];
      List<MonitoringAlert> allAlerts = [.. _alertHistory];

      return new()
      {
        ActiveAlertCount = activeAlerts.Count,
        CriticalAlertCount = activeAlerts.Count(a => a.Severity >= AlertSeverity.Critical),
        TotalAlertsToday = allAlerts.Count(a => a.Timestamp.Date == DateTime.Today),
        MostCommonAlertType = allAlerts.GroupBy(a => a.Type)
                                       .OrderByDescending(g => g.Count())
                                       .FirstOrDefault()?.Key ?? AlertType.ColonyCritical,
        AverageResolutionTime = CalculateAverageResolutionTime(allAlerts),
        LastAlertTime = activeAlerts.LastOrDefault()?.Timestamp
      };
    }

    /// <summary>
    /// Calculates average resolution time for resolved alerts.
    /// </summary>
    /// <param name="alerts">Historical alerts to analyze.</param>
    /// <returns>Average resolution time.</returns>
    private TimeSpan CalculateAverageResolutionTime(List<MonitoringAlert> alerts)
    {
      List<MonitoringAlert> resolvedAlerts = [.. alerts.Where(a => a.AcknowledgedTime.HasValue)];

      if (resolvedAlerts.Count == 0)
        return TimeSpan.Zero;

      double totalResolutionTime = resolvedAlerts
                                   .Sum(a => (a.AcknowledgedTime!.Value - a.Timestamp).TotalSeconds);

      return TimeSpan.FromSeconds(totalResolutionTime / resolvedAlerts.Count);
    }
  }

  /// <summary>
  /// Statistics about monitoring alerts.
  /// </summary>
  public sealed class AlertStatistics
  {
    /// <summary>
    /// Gets or sets the number of currently active alerts.
    /// </summary>
    public int ActiveAlertCount { get; set; }

    /// <summary>
    /// Gets or sets the number of critical or emergency alerts.
    /// </summary>
    public int CriticalAlertCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of alerts raised today.
    /// </summary>
    public int TotalAlertsToday { get; set; }

    /// <summary>
    /// Gets or sets the most commonly occurring alert type.
    /// </summary>
    public AlertType MostCommonAlertType { get; set; }

    /// <summary>
    /// Gets or sets the average time to resolve alerts.
    /// </summary>
    public TimeSpan AverageResolutionTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the most recent alert.
    /// </summary>
    public DateTime? LastAlertTime { get; set; }
  }
}
