using HiveMind.Application.Configuration;
using HiveMind.Core.Monitoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HiveMind.Application.Services
{
  /// <summary>
  /// Service that provides console output display of monitoring data.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="ConsoleDisplayService"/> class.
  /// </remarks>
  /// <param name="logger">Logger instance.</param>
  /// <param name="config">Monitoring configuration.</param>
  /// <param name="monitoringService">Monitoring service.</param>
  /// <param name="outputGenerator">Output generator.</param>
  public sealed class ConsoleDisplayService(
    ILogger<ConsoleDisplayService> logger,
    IOptions<MonitoringConfiguration> config,
    MonitoringService monitoringService,
    OutputGenerator outputGenerator
  ) : BackgroundService
  {
    private readonly ILogger<ConsoleDisplayService> _logger =
      logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly MonitoringConfiguration _config =
      config?.Value ?? throw new ArgumentNullException(nameof(config));
    private readonly MonitoringService _monitoringService =
      monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
    private readonly OutputGenerator _outputGenerator =
      outputGenerator ?? throw new ArgumentNullException(nameof(outputGenerator));

    /// <summary>
    /// Executes the console display service.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _logger.LogInformation("Console display service starting...");

      try
      {
        while (!stoppingToken.IsCancellationRequested)
        {
          DisplayDashboard();

          await Task.Delay(
            TimeSpan.FromSeconds(_config.DashboardRefreshIntervalSeconds),
            stoppingToken
          );
        }
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Console display service was cancelled");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in console display service");
      }
    }

    /// <summary>
    /// Displays the monitoring dashboard in the console.
    /// </summary>
    private void DisplayDashboard()
    {
      try
      {
        Console.Clear();

        var dashboard = _outputGenerator.GenerateDashboardSummary();

        DisplayHeader();
        DisplaySimulationStats(dashboard);
        DisplayHiveStats(dashboard);
        DisplayEnvironmentalInfo(dashboard);
        DisplayAlertSummary(dashboard);
        DisplayPerformanceInfo(dashboard);

        Console.WriteLine($"\nLast Updated: {dashboard.GeneratedAt:HH:mm:ss}");
        Console.WriteLine("Press Ctrl+C to stop simulation...\n");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error displaying dashboard: {ex.Message}");
      }
    }

    /// <summary>
    /// Displays the dashboard header.
    /// </summary>
    private static void DisplayHeader()
    {
      Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
      Console.WriteLine("║                    HiveMind Simulation                       ║");
      Console.WriteLine("║                   Real-time Dashboard                        ║");
      Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
      Console.WriteLine();
    }

    /// <summary>
    /// Displays simulation statistics.
    /// </summary>
    /// <param name="dashboard">Dashboard data.</param>
    private static void DisplaySimulationStats(DashboardSummary dashboard)
    {
      Console.WriteLine("┌─ Simulation Overview ─────────────────────────────────────────┐");
      Console.WriteLine($"│ Uptime: {dashboard.SimulationUptime:dd\\:hh\\:mm\\:ss}                                    │");
      Console.WriteLine($"│ Performance: {dashboard.PerformanceMetrics.TicksPerSecond:F1} ticks/sec                           │");
      Console.WriteLine($"│ Memory Usage: {dashboard.PerformanceMetrics.MemoryUsage / 1024:F0} KB                              │");
      Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
      Console.WriteLine();
    }

    /// <summary>
    /// Displays hive statistics.
    /// </summary>
    /// <param name="dashboard">Dashboard data.</param>
    private static void DisplayHiveStats(DashboardSummary dashboard)
    {
      Console.WriteLine("┌─ Colony Statistics ───────────────────────────────────────────┐");
      Console.WriteLine($"│ Total Hives: {dashboard.TotalHives,3} │ Healthy: {dashboard.HealthyHives,3} │ Health Rate: {GetHealthPercentage(dashboard):F0}%   │");
      Console.WriteLine($"│ Total Bees:  {dashboard.TotalBees,8}                              │");
      Console.WriteLine($"│ Honey Stored: {dashboard.TotalHoneyStored,6:F1} units                          │");
      Console.WriteLine($"│ Avg Foraging: {dashboard.AverageForagingEfficiency,5:P0} efficiency                    │");
      Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
      Console.WriteLine();
    }

    /// <summary>
    /// Displays environmental information.
    /// </summary>
    /// <param name="dashboard">Dashboard data.</param>
    private static void DisplayEnvironmentalInfo(DashboardSummary dashboard)
    {
      Console.WriteLine("┌─ Environmental Conditions ────────────────────────────────────┐");
      Console.WriteLine($"│ Season: {dashboard.CurrentSeason,-10} │ Weather: {dashboard.CurrentWeather,-15} │");
      Console.WriteLine($"│ Foraging Conditions: {GetForagingConditionText(dashboard.AverageForagingEfficiency),-25} │");
      Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
      Console.WriteLine();
    }

    /// <summary>
    /// Displays alert summary.
    /// </summary>
    /// <param name="dashboard">Dashboard data.</param>
    private static void DisplayAlertSummary(DashboardSummary dashboard)
    {
      string alertColor = dashboard.CriticalAlerts > 0 ? "CRITICAL" :
                          dashboard.ActiveAlerts > 0 ? "WARNING" : "NORMAL";

      Console.WriteLine("┌─ Alert Status ─────────────────────────────────────────────────┐");
      Console.WriteLine($"│ Status: {alertColor,-10} │ Active: {dashboard.ActiveAlerts,3} │ Critical: {dashboard.CriticalAlerts,3}     │");
      Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
      Console.WriteLine();
    }

    /// <summary>
    /// Displays performance information.
    /// </summary>
    /// <param name="dashboard">Dashboard data.</param>
    private static void DisplayPerformanceInfo(DashboardSummary dashboard)
    {
      Console.WriteLine("┌─ Performance Metrics ──────────────────────────────────────────┐");
      Console.WriteLine($"│ Simulation Speed: {dashboard.PerformanceMetrics.TicksPerSecond,6:F1} ticks/sec                  │");
      Console.WriteLine($"│ Update Rate: {dashboard.PerformanceMetrics.LastUpdateFrequency,10:F1} Hz                        │");
      Console.WriteLine($"│ Memory Usage: {dashboard.PerformanceMetrics.MemoryUsage / (1024 * 1024),9:F1} MB                        │");
      Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
    }

    /// <summary>
    /// Calculates health percentage.
    /// </summary>
    /// <param name="dashboard">Dashboard data.</param>
    /// <returns>Health percentage.</returns>
    private static double GetHealthPercentage(DashboardSummary dashboard) =>
      dashboard.TotalHives > 0 ? (double)dashboard.HealthyHives / dashboard.TotalHives * 100 : 0;

    /// <summary>
    /// Gets foraging condition text description.
    /// </summary>
    /// <param name="efficiency">Foraging efficiency.</param>
    /// <returns>Condition description.</returns>
    private static string GetForagingConditionText(double efficiency) => efficiency switch
    {
      > 1.5 => "Excellent",
      > 1.0 => "Good",
      > 0.5 => "Fair",
      > 0.2 => "Poor",
      _ => "Critical"
    };
  }
}
