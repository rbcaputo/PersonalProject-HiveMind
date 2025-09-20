using HiveMind.Core.Monitoring;

namespace HiveMind.Application.Configuration
{
  /// <summary>
  /// Configuration settings for the monitoring and output system.
  /// </summary>
  public sealed class MonitoringConfiguration
  {
    /// <summary>
    /// Gets or sets the interval between monitoring processing cycles in minutes.
    /// </summary>
    public int ProcessingIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the interval between report generation in minutes.
    /// </summary>
    public int ReportGenerationIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the interval between CSV exports in minutes.
    /// </summary>
    public int CsvExportIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the number of days to retain event data.
    /// </summary>
    public int DataRetentionDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether to save reports to files.
    /// </summary>
    public bool SaveReportsToFile { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to save CSV data to files.
    /// </summary>
    public bool SaveCsvToFile { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include events in reports.
    /// </summary>
    public bool IncludeEventsInReports { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include alerts in reports.
    /// </summary>
    public bool IncludeAlertsInReports { get; set; } = true;

    /// <summary>
    /// Gets or sets the output directory for generated files.
    /// </summary>
    public string OutputDirectory { get; set; } = "Outputs";

    /// <summary>
    /// Gets or sets the maximum number of hive snapshots to keep in memory.
    /// </summary>
    public int MaxHiveSnapshots { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum number of events to keep in memory.
    /// </summary>
    public int MaxEventHistory { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the alert severity level for notifications.
    /// </summary>
    public AlertSeverity AlertNotificationLevel { get; set; } = AlertSeverity.Warning;

    /// <summary>
    /// Gets or sets a value indicating whether to enable detailed performance monitoring.
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the dashboard refresh interval in seconds.
    /// </summary>
    public int DashboardRefreshIntervalSeconds { get; set; } = 30;
  }
}
