using HiveMind.Core.Entities;
using HiveMind.Core.Simulation;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace HiveMind.Core.Monitoring
{
  /// <summary>
  /// Generates structured output data from monitoring information.
  /// Provides data in various formats for external consumption.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="OutputGenerator"/> class.
  /// </remarks>
  public sealed class OutputGenerator(
    ILogger<OutputGenerator> logger,
    MonitoringService monitoringService,
    EventLogger eventLogger,
    AlertManager alertManager
  )
  {
    private readonly ILogger<OutputGenerator> _logger =
      logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly MonitoringService _monitoringService =
      monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
    private readonly EventLogger _eventLogger =
      eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
    private readonly AlertManager _alertManager =
      alertManager ?? throw new ArgumentNullException(nameof(alertManager));

    /// <summary>
    /// Generates a comprehensive monitoring report in JSON format.
    /// </summary>
    public string GenerateMonitoringReport(bool includeEventLog = true, bool includeAlerts = true)
    {
      try
      {
        MonitoringReport report = new()
        {
          GeneratedAt = DateTime.UtcNow,
          SimulationMetrics = _monitoringService.CurrentMetrics,
          AggregatedStats = _monitoringService.GetAggregatedStats(),
          HiveData = [.. _monitoringService.GetAllHiveData()],
          PerformanceStats = _monitoringService.GetPerformanceStats()
        };

        if (includeEventLog)
        {
          report.RecentEvents = [.. _eventLogger.RecentEvents.Take(100)];
          report.EventStatistics = [.. _eventLogger.EventStatistics.Values];
        }

        if (includeAlerts)
        {
          report.ActiveAlerts = [.. _alertManager.ActiveAlerts];
          report.AlertStatistics = _alertManager.GetAlertStatistics();
        }

        JsonSerializerOptions options = new()
        {
          WriteIndented = true,
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(report, options);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to generate monitoring report");
        return GenerateErrorReport("Failed to generate monitoring report", ex);
      }
    }

    /// <summary>
    /// Generates a summary dashboard view of key metrics.
    /// </summary>
    public DashboardSummary GenerateDashboardSummary()
    {
      try
      {
        AggregatedHiveStats aggregatedStats = _monitoringService.GetAggregatedStats();
        SimulationMetrics simulationMetrics = _monitoringService.CurrentMetrics;
        AlertStatistics alertStats = _alertManager.GetAlertStatistics();

        return new()
        {
          GeneratedAt = DateTime.UtcNow,
          TotalHives = aggregatedStats.TotalHives,
          HealthyHives = aggregatedStats.HealthyHives,
          TotalBees = simulationMetrics.TotalLivingBees,
          TotalHoneyStored = aggregatedStats.TotalHoneyStored,
          AverageForagingEfficiency = aggregatedStats.AverageForagingEfficiency,
          ActiveAlerts = alertStats.ActiveAlertCount,
          CriticalAlerts = alertStats.CriticalAlertCount,
          SimulationUptime = simulationMetrics.SimulationUptime,
          CurrentWeather = simulationMetrics.CurrentEnvironment.Weather.ToString(),
          CurrentSeason = GetSeasonFromEnvironment(simulationMetrics.CurrentEnvironment),
          PerformanceMetrics = new()
          {
            TicksPerSecond = simulationMetrics.TicksPerSecond,
            MemoryUsage = _monitoringService.GetPerformanceStats().MemoryUsageEstimate,
            LastUpdateFrequency = _monitoringService.GetPerformanceStats().UpdateFrequency
          }
        };
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to generate dashboard summary");
        return new()
        {
          GeneratedAt = DateTime.UtcNow,
          ErrorMessage = ex.Message
        };
      }
    }

    /// <summary>
    /// Generates detailed statistics for a specific hive.
    /// </summary>
    public HiveDetailedReport? GenerateHiveReport(Guid hiveId)
    {
      try
      {
        HiveMonitoringData? hiveData = _monitoringService.GetHiveData(hiveId);
        if (hiveData == null)
        {
          _logger.LogWarning("No monitoring data found for hive {HiveId}", hiveId);
          return null;
        }

        List<MonitoringAlert> hiveAlerts = [.. _alertManager.GetHiveAlerts(hiveId)];
        List<SimulationEventRecord> hiveEvents = [.. _eventLogger.GetEvents(
          new EventFilter
          {
          EntityIds = [hiveId],
          MaxResults = 50
          })];

        return new()
        {
          GeneratedAt = DateTime.UtcNow,
          HiveId = hiveId,
          MonitoringData = hiveData,
          ActiveAlerts = hiveAlerts,
          RecentEvents = hiveEvents,
          HealthAnalysis = AnalyzeHiveHealth(hiveData),
          ProductionAnalysis = AnalyzeHiveProduction(hiveData),
          Recommendations = GenerateHiveRecommendations(hiveData, hiveAlerts)
        };
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to generate hive report for {HiveId}", hiveId);
        return null;
      }
    }

    /// <summary>
    /// Generates a CSV export of current hive statistics.
    /// </summary>
    public string GenerateHiveStatisticsCsv()
    {
      try
      {
        IEnumerable<HiveMonitoringData> hiveData = _monitoringService.GetAllHiveData();
        StringBuilder csv = new();

        // Header
        csv.AppendLine(
          "HiveId,Timestamp,TotalBees,Workers,Drones,HasQueen,HealthStatus," +
          "HoneyStored,TotalHoneyProduced,AvailableCells,Temperature,Humidity," +
          "WeatherType,Season,ForagingEfficiency,LocationX,LocationY,LocationZ"
        );

        // Data rows
        foreach (HiveMonitoringData hive in hiveData)
        {
          csv.AppendLine(
            $"{hive.HiveId:N}," +
            $"{hive.Timestamp:yyyy-MM-dd HH:mm:ss}," +
            $"{hive.Population.TotalBees}," +
            $"{hive.Population.Workers}," +
            $"{hive.Population.Drones}," +
            $"{hive.Population.HasQueen}," +
            $"{hive.Population.HealthStatus}," +
            $"{hive.Production.CurrentHoneyStored:F2}," +
            $"{hive.Production.TotalHoneyProduced:F2}," +
            $"{hive.Production.AvailableBroodCells}," +
            $"{hive.Environment.Temperature:F1}," +
            $"{hive.Environment.Humidity:F1}," +
            $"{hive.Environment.WeatherType}," +
            $"{hive.Environment.Season}," +
            $"{hive.Environment.ForagingEfficiency:F3}," +
            $"{hive.Location.X:F2}," +
            $"{hive.Location.Y:F2}," +
            $"{hive.Location.Z:F2}"
          );
        }

        return csv.ToString();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to generate CSV export");
        return $"Error generating CSV: {ex.Message}";
      }
    }

    /// <summary>
    /// Generates an environmental conditions report.
    /// </summary>
    public EnvironmentalReport GenerateEnvironmentalReport()
    {
      try
      {
        SimulationMetrics simulationMetrics = _monitoringService.CurrentMetrics;
        List<SimulationEventRecord> recentEvents = [.. _eventLogger.GetEvents(
          new EventFilter
          {
            EventTypes = [EventTypeCategory.Environmental],
            MaxResults = 20
          })];

        var environmentalEvents = recentEvents
            .Where(e => e.EventType == EventTypeCategory.Environmental)
            .ToList();

        return new()
        {
          GeneratedAt = DateTime.UtcNow,
          CurrentConditions = simulationMetrics.CurrentEnvironment,
          RecentEnvironmentalEvents = environmentalEvents,
          ImpactAnalysis = AnalyzeEnvironmentalImpact(environmentalEvents),
          WeatherForecast = GenerateWeatherForecast(),
          SeasonalTrends = AnalyzeSeasonalTrends()
        };
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to generate environmental report");
        return new()
        {
          GeneratedAt = DateTime.UtcNow,
          ErrorMessage = ex.Message
        };
      }
    }

    // Private helper methods
    private static string GenerateErrorReport(string errorMessage, Exception exception)
    {
      object errorReport = new
      {
        GeneratedAt = DateTime.UtcNow,
        Status = "Error",
        ErrorMessage = errorMessage,
        ExceptionType = exception.GetType().Name,
        ExceptionMessage = exception.Message
      };
      return JsonSerializer.Serialize(errorReport, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GetSeasonFromEnvironment(EnvironmentSnapshot environment) =>
      DateTime.Now.Month switch
      {
        12 or 1 or 2 => "Winter",
        3 or 4 or 5 => "Spring",
        6 or 7 or 8 => "Summer",
        9 or 10 or 11 => "Autumn",
        _ => "Unknown"
      };

    private static HealthAnalysis AnalyzeHiveHealth(HiveMonitoringData hiveData)
    {
      double healthScore = CalculateHealthScore(hiveData);

      return new()
      {
        OverallScore = healthScore,
        HealthStatus = hiveData.Population.HealthStatus,
        PopulationHealth = AnalyzePopulationHealth(hiveData.Population),
        ProductionHealth = AnalyzeProductionHealth(hiveData.Production),
        EnvironmentalStress = AnalyzeEnvironmentalStress(hiveData.Environment),
        KeyConcerns = IdentifyHealthConcerns(hiveData),
        HealthTrend = DetermineHealthTrend(hiveData)
      };
    }

    private static double CalculateHealthScore(HiveMonitoringData hiveData)
    {
      double populationScore = hiveData.Population.HasQueen ? 0.3 : 0.0;
      populationScore += Math.Min(0.3, hiveData.Population.TotalBees / 10000.0 * 0.3);

      double productionScore = Math.Min(0.2, hiveData.Production.CurrentHoneyStored / 100.0 * 0.2);
      double environmentScore = Math.Min(0.2, hiveData.Environment.ForagingEfficiency * 0.2);

      return populationScore + productionScore + environmentScore;
    }

    private static string AnalyzePopulationHealth(PopulationMetrics population)
    {
      if (!population.HasQueen) return "Critical: No queen present";
      if (population.TotalBees < 1000) return "Poor: Very low population";
      if (population.TotalBees < 5000) return "Fair: Below optimal population";
      return "Good: Healthy population size";
    }

    private static string AnalyzeProductionHealth(ProductionMetrics production)
    {
      if (production.CurrentHoneyStored < 10) return "Critical: Very low honey stores";
      if (production.AvailableBroodCells == 0) return "Warning: No available brood cells";
      if (production.CurrentHoneyStored > 50) return "Excellent: Good honey reserves";
      return "Good: Adequate production levels";
    }

    private static string AnalyzeEnvironmentalStress(EnvironmentMetrics environment)
    {
      if (environment.ForagingEfficiency < 0.1) return "High stress: Very poor foraging conditions";
      if (environment.ActiveEventCount > 2) return "Moderate stress: Multiple environmental events";
      if (environment.ForagingEfficiency > 1.5) return "Favorable: Excellent environmental conditions";
      return "Normal: Standard environmental conditions";
    }

    private static List<string> IdentifyHealthConcerns(HiveMonitoringData hiveData)
    {
      List<string> concerns = [];

      if (!hiveData.Population.HasQueen)
        concerns.Add("Queen loss - immediate replacement needed");

      if (hiveData.Population.TotalBees < 2000)
        concerns.Add("Low population - colony viability at risk");

      if (hiveData.Production.CurrentHoneyStored < 5)
        concerns.Add("Low honey stores - starvation risk");

      if (hiveData.Environment.ForagingEfficiency < 0.2)
        concerns.Add("Poor foraging conditions - limited food sources");

      if (hiveData.Production.AvailableBroodCells == 0)
        concerns.Add("No brood space - expansion needed");

      return concerns;
    }

    private static string DetermineHealthTrend(HiveMonitoringData hiveData)
    {
      if (hiveData.Environment.ForagingEfficiency > 1.2) return "Improving";
      if (hiveData.Environment.ForagingEfficiency < 0.5) return "Declining";
      return "Stable";
    }

    private static ProductionAnalysis AnalyzeHiveProduction(HiveMonitoringData hiveData)
    {
      return new()
      {
        CurrentEfficiency = CalculateProductionEfficiency(hiveData),
        HoneyProductionRate = hiveData.Production.HoneyProductionRate,
        StorageUtilization = hiveData.Production.StorageUtilization,
        OptimizationSuggestions = GenerateProductionOptimizations(hiveData)
      };
    }

    private static double CalculateProductionEfficiency(HiveMonitoringData hiveData)
    {
      double baseEfficiency = hiveData.Environment.ForagingEfficiency;
      double populationFactor = Math.Min(1.0, hiveData.Population.Workers / 8000.0);
      double storageFactor = hiveData.Production.AvailableBroodCells > 0 ? 1.0 : 0.7;
      return baseEfficiency * populationFactor * storageFactor;
    }

    private static List<string> GenerateProductionOptimizations(HiveMonitoringData hiveData)
    {
      List<string> suggestions = [];

      if (hiveData.Production.StorageUtilization > 90)
        suggestions.Add("Add honey supers - storage nearly full");

      if (hiveData.Population.Workers < 5000)
        suggestions.Add("Build worker population for better foraging capacity");

      if (hiveData.Environment.ForagingEfficiency < 0.5)
        suggestions.Add("Consider relocating hive to better foraging area");

      if (hiveData.Production.AvailableBroodCells < 100)
        suggestions.Add("Add brood chambers for colony expansion");

      return suggestions;
    }

    private static List<string> GenerateHiveRecommendations(HiveMonitoringData hiveData, List<MonitoringAlert> alerts)
    {
      List<string> recommendations = [];

      foreach (MonitoringAlert alert in alerts.Where(a => a.Severity >= AlertSeverity.Critical))
        recommendations.Add($"URGENT: Address {alert.Type} - {alert.Message}");

      if (hiveData.Population.TotalBees > 40000)
        recommendations.Add("Consider splitting hive to prevent swarming");

      if (hiveData.Production.CurrentHoneyStored > 100)
        recommendations.Add("Honey harvest opportunity available");

      if (hiveData.Environment.ForagingEfficiency > 1.5)
        recommendations.Add("Excellent conditions for colony expansion");

      return recommendations;
    }

    private static string AnalyzeEnvironmentalImpact(List<SimulationEventRecord> environmentalEvents)
    {
      if (environmentalEvents.Count == 0)
        return "No significant environmental events recently";

      List<SimulationEventRecord> recentEvents = [.. environmentalEvents.Take(5)];
      List<string> eventTypes = [.. recentEvents.Select(e => e.EventName).Distinct()];

      return $"Recent environmental activity: {string.Join(", ", eventTypes)}. " +
             $"Total events in period: {environmentalEvents.Count}";
    }

    private static string GenerateWeatherForecast() =>
      "Weather patterns continue based on seasonal trends and current conditions.";

    private static string AnalyzeSeasonalTrends()
    {
      string currentSeason = GetCurrentSeason();
      return currentSeason switch
      {
        "Spring" => "Colony expansion period - expect increased activity and brood production",
        "Summer" => "Peak activity season - maximum foraging and honey production expected",
        "Autumn" => "Preparation phase - colonies reducing activity and storing resources",
        "Winter" => "Survival mode - minimal activity, focus on cluster maintenance",
        _ => "Seasonal patterns normal for current time period"
      };
    }

    private static string GetCurrentSeason() => DateTime.Now.Month switch
    {
      12 or 1 or 2 => "Winter",
      3 or 4 or 5 => "Spring",
      6 or 7 or 8 => "Summer",
      9 or 10 or 11 => "Autumn",
      _ => "Unknown"
    };
  }

  /// <summary>
  /// Dashboard summary for quick overview.
  /// </summary>
  public sealed class DashboardSummary
  {
    public DateTime GeneratedAt { get; set; }
    public int TotalHives { get; set; }
    public int HealthyHives { get; set; }
    public int TotalBees { get; set; }
    public double TotalHoneyStored { get; set; }
    public double AverageForagingEfficiency { get; set; }
    public int ActiveAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public TimeSpan SimulationUptime { get; set; }
    public string CurrentWeather { get; set; } = string.Empty;
    public string CurrentSeason { get; set; } = string.Empty;
    public PerformanceSummary PerformanceMetrics { get; set; } = new();
    public string? ErrorMessage { get; set; }
  }

  /// <summary>
  /// Performance summary for dashboard.
  /// </summary>
  public sealed class PerformanceSummary
  {
    public double TicksPerSecond { get; set; }
    public long MemoryUsage { get; set; }
    public double LastUpdateFrequency { get; set; }
  }

  /// <summary>
  /// Comprehensive monitoring report structure.
  /// </summary>
  public sealed class MonitoringReport
  {
    public DateTime GeneratedAt { get; set; }
    public SimulationMetrics SimulationMetrics { get; set; } = new();
    public AggregatedHiveStats AggregatedStats { get; set; } = new();
    public List<HiveMonitoringData> HiveData { get; set; } = [];
    public MonitoringPerformanceStats PerformanceStats { get; set; } = new();
    public List<SimulationEventRecord>? RecentEvents { get; set; }
    public List<EventStatistics>? EventStatistics { get; set; }
    public List<MonitoringAlert>? ActiveAlerts { get; set; }
    public AlertStatistics? AlertStatistics { get; set; }
  }

  /// <summary>
  /// Detailed report for a specific hive.
  /// </summary>
  public sealed class HiveDetailedReport
  {
    public DateTime GeneratedAt { get; set; }
    public Guid HiveId { get; set; }
    public HiveMonitoringData MonitoringData { get; set; } = new();
    public List<MonitoringAlert> ActiveAlerts { get; set; } = [];
    public List<SimulationEventRecord> RecentEvents { get; set; } = [];
    public HealthAnalysis HealthAnalysis { get; set; } = new();
    public ProductionAnalysis ProductionAnalysis { get; set; } = new();
    public List<string> Recommendations { get; set; } = [];
  }

  /// <summary>
  /// Health analysis for a hive.
  /// </summary>
  public sealed class HealthAnalysis
  {
    public double OverallScore { get; set; }
    public ColonyHealth HealthStatus { get; set; }
    public string PopulationHealth { get; set; } = string.Empty;
    public string ProductionHealth { get; set; } = string.Empty;
    public string EnvironmentalStress { get; set; } = string.Empty;
    public List<string> KeyConcerns { get; set; } = [];
    public string HealthTrend { get; set; } = string.Empty;
  }

  /// <summary>
  /// Production analysis for a hive.
  /// </summary>
  public sealed class ProductionAnalysis
  {
    public double CurrentEfficiency { get; set; }
    public double HoneyProductionRate { get; set; }
    public double StorageUtilization { get; set; }
    public List<string> OptimizationSuggestions { get; set; } = [];
  }

  /// <summary>
  /// Environmental conditions report.
  /// </summary>
  public sealed class EnvironmentalReport
  {
    public DateTime GeneratedAt { get; set; }
    public EnvironmentSnapshot CurrentConditions { get; set; } = new();
    public List<SimulationEventRecord> RecentEnvironmentalEvents { get; set; } = [];
    public string ImpactAnalysis { get; set; } = string.Empty;
    public string WeatherForecast { get; set; } = string.Empty;
    public string SeasonalTrends { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
  }
}

