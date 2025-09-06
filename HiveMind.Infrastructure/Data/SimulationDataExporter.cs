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
    private readonly ILogger<SimulationDataExporter>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SimulationDataExporter(ILogger<SimulationDataExporter>? logger = null)
    {
      _logger = logger;
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

        // Assuming statistics are SimulationStatistics objects
        csv.AppendLine("Tick,Population,ActiveColonies,TotalFood,AvgEnergy,Deaths,Births");

        foreach (object stat in statsArray)
        {
          string json = JsonSerializer.Serialize(stat, _jsonOptions);
          Dictionary<string, object>? statDict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

          if (statDict != null)
          {
            string line = $"{statDict.GetValueOrDefault("currentTick", 0)}," +
              $"{statDict.GetValueOrDefault("totalPopulation", 0)}," +
              $"{statDict.GetValueOrDefault("activeColonies", 0)}," +
              $"{statDict.GetValueOrDefault("totalFoodStored", 0)}," +
              $"{statDict.GetValueOrDefault("avgEnergyLevel", 0)}," +
              $"{statDict.GetValueOrDefault("deathCount", 0)}," +
              $"{statDict.GetValueOrDefault("birthCount", 0)}";

            csv.AppendLine(line);
          }
        }

        await File.WriteAllTextAsync(filePath, csv.ToString());
        _logger?.LogInformation("Exported statistics to CSV: {FilePath}", filePath);
        return filePath;
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Failed to export statistics to CSV: {FilePath}", filePath);
        throw;
      }
    }

    public async Task<string> ExportColonyDataToJsonAsync(object colonyData, string filePath)
    {
      try
      {
        string json = JsonSerializer.Serialize(colonyData, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);

        _logger?.LogInformation("Exported colony data to JSON: {FilePath}", filePath);
        return filePath;
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Failed to export colony data to JSON: {FilePath}", filePath);
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
          "csv" => await ExportPopulationToCsvAsync(filePath),
          _ => throw new ArgumentException($"Unsupported format: {format}"),
        };
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Failed to export population data: {FilePath}", filePath);
        throw;
      }
    }

    private static async Task<string> ExportPopulationToCsvAsync(string filePath)
    {
      StringBuilder csv = new();
      csv.AppendLine("Role,Count,AvgEnergy,AvgAge");

      // This would need to be implemented based on the actual structure of populationData
      // For now, just create a placeholder
      csv.AppendLine("Queen,1,85.5,150");
      csv.AppendLine("Worker,25,42.3,45");
      csv.AppendLine("Forager,15,38.7,35");
      csv.AppendLine("Soldier,8,55.2,65");

      await File.WriteAllTextAsync(filePath, csv.ToString());
      return filePath;
    }
  }
}
