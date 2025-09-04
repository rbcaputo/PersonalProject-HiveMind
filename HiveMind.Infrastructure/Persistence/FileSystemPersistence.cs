using Microsoft.Extensions.Logging;
using System.Text.Json;
using static System.Environment;

namespace HiveMind.Infrastructure.Persistence
{
  /// <summary>
  /// File system implementation of simulation persistence
  /// </summary>
  public class FileSystemPersistence : ISimulationPersistence
  {
    private readonly string _basePath;
    private readonly ILogger<FileSystemPersistence>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemPersistence(string? basePath = null, ILogger<FileSystemPersistence>? logger = null)
    {
      _basePath = basePath ?? Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData), "HiveMind", "Snapshots");
      _logger = logger;
      _jsonOptions = new JsonSerializerOptions
      {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
      };

      EnsureDirectoryExists();
    }

    public async Task<string> SaveSnapshotAsync(SimulationSnapshot snapshot)
    {
      try
      {
        var fileName = $"{snapshot.Id:D}.json";
        var filePath = Path.Combine(_basePath, fileName);

        var json = JsonSerializer.Serialize(snapshot, _jsonOptions);

        await File.WriteAllTextAsync(filePath, json);

        _logger?.LogInformation("Saved simulation snapshot {SnapshotId} to {FilePath}", snapshot.Id, filePath);
        return snapshot.Id.ToString();
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Failed to save simulation snapshot {SnapshotId}", snapshot.Id);
        throw;
      }
    }

    public async Task<SimulationSnapshot?> LoadSnapshotAsync(string snapshotId)
    {
      try
      {
        var fileName = $"{snapshotId}.json";
        var filePath = Path.Combine(_basePath, fileName);

        if (!File.Exists(filePath))
        {
          _logger?.LogWarning("Snapshot file not found: {FilePath}", filePath);
          return null;
        }

        var json = await File.ReadAllTextAsync(filePath);
        var snapshot = JsonSerializer.Deserialize<SimulationSnapshot>(json, _jsonOptions);

        _logger?.LogInformation("Loaded simulation snapshot {SnapshotId} from {FilePath}", snapshotId, filePath);
        return snapshot;
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Failed to load simulation snapshot {SnapshotId}", snapshotId);
        throw;
      }
    }

    public async Task<IEnumerable<SimulationSnapshot>> GetSnapshotsAsync()
    {
      try
      {
        var snapshots = new List<SimulationSnapshot>();
        var jsonFiles = Directory.GetFiles(_basePath, "*.json");

        foreach (var filePath in jsonFiles)
        {
          try
          {
            var json = await File.ReadAllTextAsync(filePath);
            var snapshot = JsonSerializer.Deserialize<SimulationSnapshot>(json, _jsonOptions);
            if (snapshot != null)
              snapshots.Add(snapshot);
          }
          catch (Exception ex)
          {
            _logger?.LogWarning(ex, "Failed to deserialize snapshot file: {FilePath}", filePath);
          }
        }

        return snapshots.OrderByDescending(s => s.CreatedAt);
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Failed to get snapshots from directory: {BasePath}", _basePath);
        throw;
      }
    }

    public async Task<bool> DeleteSnapshotAsync(string snapshotId)
    {
      try
      {
        var fileName = $"{snapshotId}.json";
        var filePath = Path.Combine(_basePath, fileName);

        if (!File.Exists(filePath))
        {
          _logger?.LogWarning("Cannot delete snapshot - file not found: {FilePath}", filePath);
          return false;
        }

        await Task.Run(() => File.Delete(filePath));
        _logger?.LogInformation("Deleted simulation snapshot {SnapshotId}", snapshotId);
        return true;
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Failed to delete simulation snapshot {SnapshotId}", snapshotId);
        return false;
      }
    }

    public async Task<string> ExportSimulationDataAsync(string snapshotId, string format = "json")
    {
      try
      {
        var snapshot = await LoadSnapshotAsync(snapshotId) ?? throw new FileNotFoundException($"Snapshot {snapshotId} not found");
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var exportFileName = $"hivemind_export_{timestamp}.{format.ToLower()}";
        var exportPath = Path.Combine(_basePath, "Exports");

        Directory.CreateDirectory(exportPath);
        var exportFilePath = Path.Combine(exportPath, exportFileName);

        switch (format.ToLower())
        {
          case "json":
            var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            await File.WriteAllTextAsync(exportFilePath, json);
            break;
          case "csv":
            await ExportToCsvAsync(snapshot, exportFilePath);
            break;
          default:
            throw new ArgumentException($"Unsupported export format: {format}");
        }

        _logger?.LogInformation("Exported simulation data to {ExportPath}", exportFilePath);
        return exportFilePath;
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Failed to export simulation data for snapshot {SnapshotId}", snapshotId);
        throw;
      }
    }

    private static async Task ExportToCsvAsync(SimulationSnapshot snapshot, string filePath)
    {
      var lines = new List<string>
      {
        "Property,Value",
        $"SnapshotId,{snapshot.Id}",
        $"CreatedAt,{snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss}",
        $"TickNumber,{snapshot.TickNumber}",
        $"Description,\"{snapshot.Description}\"",
        "",
        "Configuration Data:",
        snapshot.ConfigurationJson,
        "",
        "Statistics Data:",
        snapshot.StatisticsJson
      };

      await File.WriteAllLinesAsync(filePath, lines);
    }

    private void EnsureDirectoryExists()
    {
      if (!Directory.Exists(_basePath))
      {
        Directory.CreateDirectory(_basePath);
        _logger?.LogInformation("Created snapshots directory: {BasePath}", _basePath);
      }
    }
  }
}
