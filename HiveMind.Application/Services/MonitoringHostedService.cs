using HiveMind.Application.Configuration;
using HiveMind.Core.Monitoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HiveMind.Application.Services
{
  /// <summary>
  /// Hosted service that manages monitoring and output generation for the simulation.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="MonitoringHostedService"/> class.
  /// </remarks>
  /// <param name="logger">Logger instance.</param>
  /// <param name="config">Monitoring configuration.</param>
  /// <param name="monitoringService">Monitoring service.</param>
  /// <param name="eventLogger">Event logger service.</param>
  /// <param name="alertManager">Alert manager service.</param>
  /// <param name="outputGenerator">Output generator service.</param>
  public sealed class MonitoringHostedService(
    ILogger<MonitoringHostedService> logger,
    IOptions<MonitoringConfiguration> config,
    MonitoringService monitoringService,
    EventLogger eventLogger,
    AlertManager alertManager,
    OutputGenerator outputGenerator
  ) : BackgroundService
  {
    private readonly ILogger<MonitoringHostedService> _logger =
      logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly MonitoringConfiguration _config =
      config?.Value ?? throw new ArgumentNullException(nameof(config));
    private readonly MonitoringService _monitoringService =
      monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
    private readonly EventLogger _eventLogger =
      eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
    private readonly AlertManager _alertManager =
      alertManager ?? throw new ArgumentNullException(nameof(alertManager));
    private readonly OutputGenerator _outputGenerator =
      outputGenerator ?? throw new ArgumentNullException(nameof(outputGenerator));

    private DateTime _lastReportGeneration = DateTime.UtcNow;
    private DateTime _lastCsvExport = DateTime.UtcNow;

    /// <summary>
    /// Executes the monitoring service background tasks.
    /// </summary>
    /// <param name="stoppingToken">Token to stop the service.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _logger.LogInformation("Monitoring service starting...");

      try
      {
        while (!stoppingToken.IsCancellationRequested)
        {
          await ProcessMonitoringTasks();

          // Wait for next processing cycle
          await Task.Delay(TimeSpan.FromMinutes(_config.ProcessingIntervalMinutes), stoppingToken);
        }
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Monitoring service was cancelled");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Fatal error in monitoring service");
        throw;
      }
      finally
      {
        _logger.LogInformation("Monitoring service stopped");
      }
    }

    /// <summary>
    /// Processes all monitoring-related tasks.
    /// </summary>
    private async Task ProcessMonitoringTasks()
    {
      try
      {
        // Generate periodic reports
        if (ShouldGenerateReport())
        {
          await GeneratePeriodicReport();
          _lastReportGeneration = DateTime.UtcNow;
        }

        // Export CSV data
        if (ShouldExportCsv())
        {
          await ExportCsvData();
          _lastCsvExport = DateTime.UtcNow;
        }

        // Clean up old data
        if (ShouldCleanupData())
        {
          await CleanupOldData();
        }

        // Log monitoring statistics
        LogMonitoringStatistics();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing monitoring tasks");
      }
    }

    /// <summary>
    /// Determines if a periodic report should be generated.
    /// </summary>
    /// <returns>True if report should be generated.</returns>
    private bool ShouldGenerateReport()
    {
      TimeSpan timeSinceLastReport = DateTime.UtcNow - _lastReportGeneration;
      return timeSinceLastReport >= TimeSpan.FromMinutes(_config.ReportGenerationIntervalMinutes);
    }

    /// <summary>
    /// Determines if CSV export should be performed.
    /// </summary>
    /// <returns>True if CSV should be exported.</returns>
    private bool ShouldExportCsv()
    {
      TimeSpan timeSinceLastExport = DateTime.UtcNow - _lastCsvExport;
      return timeSinceLastExport >= TimeSpan.FromMinutes(_config.CsvExportIntervalMinutes);
    }

    /// <summary>
    /// Determines if data cleanup should be performed.
    /// </summary>
    /// <returns>True if cleanup should be performed.</returns>
    private bool ShouldCleanupData() =>
      // Cleanup once per day
      DateTime.UtcNow.Hour == 2 && DateTime.UtcNow.Minute < _config.ProcessingIntervalMinutes;

    /// <summary>
    /// Generates a periodic monitoring report.
    /// </summary>
    private async Task GeneratePeriodicReport()
    {
      try
      {
        _logger.LogInformation("Generating periodic monitoring report...");

        var report = _outputGenerator.GenerateMonitoringReport(
          includeEventLog: _config.IncludeEventsInReports,
          includeAlerts: _config.IncludeAlertsInReports
        );

        if (_config.SaveReportsToFile)
          await SaveReportToFile(report, "monitoring_report");

        _logger.LogDebug("Periodic monitoring report generated successfully");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to generate periodic monitoring report");
      }
    }

    /// <summary>
    /// Exports hive statistics to CSV format.
    /// </summary>
    private async Task ExportCsvData()
    {
      try
      {
        _logger.LogInformation("Exporting CSV data...");

        var csvData = _outputGenerator.GenerateHiveStatisticsCsv();

        if (_config.SaveCsvToFile)
          await SaveCsvToFile(csvData, "hive_statistics");

        _logger.LogDebug("CSV data export completed successfully");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to export CSV data");
      }
    }

    /// <summary>
    /// Performs cleanup of old monitoring data.
    /// </summary>
    private async Task CleanupOldData()
    {
      try
      {
        _logger.LogInformation("Performing data cleanup...");

        TimeSpan retentionPeriod = TimeSpan.FromDays(_config.DataRetentionDays);
        int eventsCleared = _eventLogger.ClearOldEvents(retentionPeriod);

        _logger.LogInformation("Data cleanup completed. Cleared {EventsCleared} old events", eventsCleared);

        await Task.CompletedTask;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to perform data cleanup");
      }
    }

    /// <summary>
    /// Logs current monitoring statistics.
    /// </summary>
    private void LogMonitoringStatistics()
    {
      try
      {
        MonitoringPerformanceStats performanceStats = _monitoringService.GetPerformanceStats();
        AlertStatistics alertStats = _alertManager.GetAlertStatistics();
        AggregatedHiveStats aggregatedStats = _monitoringService.GetAggregatedStats();

        _logger.LogInformation(
          "Monitoring Stats - Hives: {TrackedHives}, Snapshots: {SnapshotCount}, " +
          "Active Alerts: {ActiveAlerts}, Memory Usage: {MemoryUsage} bytes, " +
          "Healthy Hives: {HealthyHives}/{TotalHives}",
          performanceStats.TrackedHives,
          performanceStats.SnapshotCount,
          alertStats.ActiveAlertCount,
          performanceStats.MemoryUsageEstimate,
          aggregatedStats.HealthyHives,
          aggregatedStats.TotalHives
        );
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Failed to log monitoring statistics");
      }
    }

    /// <summary>
    /// Saves a report to file.
    /// </summary>
    /// <param name="content">Report content.</param>
    /// <param name="filePrefix">File name prefix.</param>
    private async Task SaveReportToFile(string content, string filePrefix)
    {
      try
      {
        string fileName = $"{filePrefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        string filePath = Path.Combine(_config.OutputDirectory, fileName);

        Directory.CreateDirectory(_config.OutputDirectory);
        await File.WriteAllTextAsync(filePath, content);

        _logger.LogDebug("Report saved to {FilePath}", filePath);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to save report to file");
      }
    }

    /// <summary>
    /// Saves CSV data to file.
    /// </summary>
    /// <param name="csvContent">CSV content.</param>
    /// <param name="filePrefix">File name prefix.</param>
    private async Task SaveCsvToFile(string csvContent, string filePrefix)
    {
      try
      {
        string fileName = $"{filePrefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        string filePath = Path.Combine(_config.OutputDirectory, fileName);

        Directory.CreateDirectory(_config.OutputDirectory);
        await File.WriteAllTextAsync(filePath, csvContent);

        _logger.LogDebug("CSV data saved to {FilePath}", filePath);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to save CSV data to file");
      }
    }
  }
}
