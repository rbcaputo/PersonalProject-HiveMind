using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace HiveMind.Infrastructure.Data
{
  /// <summary>
  /// Implementation of simulation data exporter
  /// </summary>
  public class SimulationDataExporter : IDataExporter
  {
    private readonly ILogger<SimulationDataExporter> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SimulationDataExporter(ILogger<SimulationDataExporter> logger)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _jsonOptions = new JsonSerializerOptions
      {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
      };
    }

    public async Task<string> ExportStatisticsToCsvAsync(IEnumerable<object> statistics, string filePath)
    {
      try
      {
        StringBuilder csv = new();
        object[] statsArray = [.. statistics];

        if (statsArray.Length == 0)
        {
          csv.AppendLine("No data to export");
          await File.WriteAllTextAsync(filePath, csv.ToString());

          return filePath;
        }

        // Write CSV header
        csv.AppendLine("Tick,Population,ActiveColonies,TotalFood,AvgEnergy,Deaths,Births");

        foreach (object stat in statsArray)
        {
          try
          {
            // Handle different types of statistics objects
            string csvLine = ConvertStatisticToCsvLine(stat);
            if (!string.IsNullOrEmpty(csvLine))
              csv.AppendLine(csvLine);
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Failed to process statistic entry of type {Type}", stat.GetType().Name);
            continue; // Continue processing other entries
          }
        }

        await File.WriteAllTextAsync(filePath, csv.ToString());
        _logger.LogInformation("Exported {Count} statistics entries to CSV: {FilePath}", statsArray.Length, filePath);

        return filePath;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to export statistics to CSV: {FilePath}", filePath);
        throw;
      }
    }

    public async Task<string> ExportColonyDataToJsonAsync(object colonyData, string filePath)
    {
      try
      {
        string json;

        if (colonyData is IColony colony)
        {
          // Convert to exportable format
          object exportData = ConvertColonyToExportFormat(colony);
          json = JsonSerializer.Serialize(exportData, _jsonOptions);
        }
        else if (colonyData is IEnumerable<IColony> colonies)
        {
          // Handle multiple colonies
          List<object> exportData = [.. colonies.Select(ConvertColonyToExportFormat)];
          json = JsonSerializer.Serialize(exportData, _jsonOptions);
        }
        else // Fallback to generic serialization
          json = JsonSerializer.Serialize(colonyData, _jsonOptions);

        await File.WriteAllTextAsync(filePath, json);
        _logger.LogInformation("Exported colony data to JSON: {FilePath}", filePath);

        return filePath;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to export colony data to JSON: {FilePath}", filePath);
        throw;
      }
    }

    public async Task<string> ExportPopulationDataAsync(object populationData, string filePath, string format)
    {
      try
      {
        return format.ToLower() switch
        {
          "json" => await ExportColonyDataToJsonAsync(populationData, filePath),
          "csv" => await ExportPopulationToCsvAsync(populationData, filePath),
          _ => throw new ArgumentException($"Unsupported format: {format}"),
        };
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to export population data: {FilePath}", filePath);
        throw;
      }
    }

    /// <summary>
    /// Exports population data to CSV
    /// </summary>
    private async Task<string> ExportPopulationToCsvAsync(object populationData, string filePath)
    {
      StringBuilder csv = new();
      csv.AppendLine("Role,Count,AvgAge,AvgHealth,AvgEnergy,TotalFood");

      try
      {
        List<PopulationRoleStatistic> populationStats = ProcessPopulationData(populationData);
        if (populationStats.Count == 0)
          csv.AppendLine("No population data available");
        else
          foreach (var stat in populationStats.OrderBy(s => s.Role))
            csv.AppendLine($"{stat.Role},{stat.Count},{stat.AvgAge:F0},{stat.AvgHealth:F1},{stat.AvgEnergy:F1},{stat.TotalFood:F1}");

        await File.WriteAllTextAsync(filePath, csv.ToString());
        _logger.LogInformation("Exported population data to CSV: {FilePath} ({Count} roles)", filePath, populationStats.Count);

        return filePath;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to process population data for CSV export");

        // Fallback with error message
        csv.Clear();
        csv.AppendLine("Role,Count,AvgEnergy,AvgAge,AvgHealth,TotalFood");
        csv.AppendLine($"Error,0,0,0,0,0  # Failed to process data: {ex.Message}");

        await File.WriteAllTextAsync(filePath, csv.ToString());

        return filePath;
      }
    }

    /// <summary>
    /// Exports population data to JSON format
    /// </summary>
    private async Task<string> ExportPopulationToJsonAsync(object populationData, string filePath)
    {
      try
      {
        List<PopulationRoleStatistic> populationStats = ProcessPopulationData(populationData);
        object exportData = new
        {
          ExportTime = DateTime.UtcNow,
          TotalPopulation = populationStats.Sum(s => s.Count),
          PopulationByRole = populationStats.ToDictionary(s => s.Role, s => new
          {
            s.Count,
            s.AvgAge,
            s.AvgHealth,
            s.AvgEnergy,
            s.TotalFood
          }),
        };

        string json = JsonSerializer.Serialize(exportData, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        _logger.LogInformation("Exported population data to JSON: {FilePath}", filePath);

        return filePath;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to export population data to JSON");
        throw;
      }
    }

    /// <summary>
    /// Processes various types of population data into standardized statistics
    /// </summary>
    private static List<PopulationRoleStatistic> ProcessPopulationData(object populationData)
    {
      return populationData switch
      {
        // Handle single colony
        IColony colony => ProcessColonyPopulation(colony),

        // Handle collection of colonies
        IEnumerable<IColony> colonies => ProcessMultipleColoniesPopulation(colonies),

        // Handle collection of ants directly
        IEnumerable<Ant> ants => ProcessAntsPopulation(ants),

        // Handle collection of insects (more generic)
        IEnumerable<IInsect> insects => ProcessInsectsPopulation(insects),

        // Handle dictionary format (e.g., from SimulationStatistics)
        IDictionary<string, int> roleCountDict => ProcessRoleDictionary(roleCountDict),

        // Try JSON deserialization as fallback
        string jsonString => ProcessJsonPopulationData(jsonString),

        _ => throw new ArgumentException($"Unsupported population data type: {populationData.GetType().Name}")
      };
    }

    private static List<PopulationRoleStatistic> ProcessColonyPopulation(IColony colony)
    {
      List<Ant> ants = [.. colony.Members.OfType<Ant>().Where(ant => ant.IsAlive)];

      return ProcessAntsPopulation(ants);
    }

    private static List<PopulationRoleStatistic> ProcessMultipleColoniesPopulation(IEnumerable<IColony> colonies)
    {
      List<Ant> allAnts = [.. colonies.SelectMany(c => c.Members).OfType<Ant>().Where(ant => ant.IsAlive)];

      return ProcessAntsPopulation(allAnts);
    }

    private static List<PopulationRoleStatistic> ProcessAntsPopulation(IEnumerable<Ant> ants) =>
      [.. ants.GroupBy(ant => ant.Role).Select(roleGroup => new PopulationRoleStatistic
      {
        Role = roleGroup.Key.ToString(),
        Count = roleGroup.Count(),
        AvgAge = roleGroup.Average(ant => ant.Age),
        AvgHealth = roleGroup.Average(ant => ant.Health),
        AvgEnergy = roleGroup.Average(ant => ant.Energy),
        TotalFood = roleGroup.Sum(ant => ant.CarriedFood)
      })];

    private static List<PopulationRoleStatistic> ProcessInsectsPopulation(IEnumerable<IInsect> insects)
    {
      IEnumerable<Ant> ants = insects.OfType<Ant>().Where(Ant => Ant.IsAlive);

      return ProcessAntsPopulation(ants);
    }

    private static List<PopulationRoleStatistic> ProcessRoleDictionary(IDictionary<string, int> roleCountDict) =>
      // Limited information available - only counts
      [.. roleCountDict.Select(kvp => new PopulationRoleStatistic
      {
        Role = kvp.Key,
        Count = kvp.Value,
        AvgAge = 0, // Not available
        AvgHealth = 0, // Not available
        AvgEnergy = 0, // Not available
        TotalFood = 0 // Not available
      })];

    private static List<PopulationRoleStatistic> ProcessJsonPopulationData(string jsonString)
    {
      try
      {
        // Try to deserialize as dictionary first
        Dictionary<string, int>? dict = JsonSerializer.Deserialize<Dictionary<string, int>>(jsonString);
        if (dict != null)
          return ProcessRoleDictionary(dict);

        // Could add more JSON format handling here
        return [];
      }
      catch
      {
        return [];
      }
    }

    private static string ConvertStatisticToCsvLine(object statistic)
    {
      // Handle different statistic object types
      if (statistic is IDictionary<string, object> dict)
        return $"{GetValue(dict, "currentTick", "tick", "tickNumber")}," +
          $"{GetValue(dict, "totalPopulation", "population")}," +
          $"{GetValue(dict, "activeColonies", "colonies")}," +
          $"{GetValue(dict, "totalFoodStored", "totalFood", "food")}," +
          $"{GetValue(dict, "avgEnergyLevel", "avgEnergy", "energy")}," +
          $"{GetValue(dict, "deathCount", "deaths")}," +
          $"{GetValue(dict, "birthCount", "births")}," +
          $"{GetValue(dict, "simulationTimeElapsed", "timeElapsed", "elapsed")}";

      // Try JSON serialization and conversion
      try
      {
        string json = JsonSerializer.Serialize(statistic);
        Dictionary<string, object>? dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        if (dictionary != null)
          return ConvertStatisticToCsvLine(dictionary);
      }
      catch
      {
        return statistic.ToString() ?? ""; // Fallback - return object's string representation
      }

      return "";
    }

    private static string GetValue(IDictionary<string, object> dict, params string[] keys)
    {
      foreach (string key in keys)
        if (dict.TryGetValue(key, out object? value))
          return value?.ToString() ?? "0";

      return "0";
    }

    private object ConvertColonyToExportFormat(IColony colony) =>
      new
      {
        colony.Id,
        colony.ColonyType,
        colony.CenterPosition,
        colony.Population,
        colony.TotalFoodStored,
        colony.IsActive,
        ExportTime = DateTime.UtcNow,
        Members = colony.Members.OfType<Ant>().Select(ant => new
        {
          ant.Id,
          ant.Role,
          ant.Position,
          ant.CurrentState,
          ant.Health,
          ant.Energy,
          ant.Age,
          ant.IsAlive,
          ant.CarriedFood
        }).ToList()
      };

    /// <summary>
    /// Statistics for a population role
    /// </summary>
    private class PopulationRoleStatistic
    {
      public string Role { get; set; } = "";
      public int Count { get; set; }
      public double AvgAge { get; set; }
      public double AvgHealth { get; set; }
      public double AvgEnergy { get; set; }
      public double TotalFood { get; set; }
    }
  }
}
