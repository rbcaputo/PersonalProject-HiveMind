namespace HiveMind.Infrastructure.Data
{
  /// <summary>
  /// Interface for exporting simulation data in various formats
  /// </summary>
  public interface IDataExporter
  {
    /// <summary>
    /// Exports simulation statistics to CSV format
    /// </summary>
    Task<string> ExportStatisticsToCsvAsync(IEnumerable<object> statistics, string filePath);

    /// <summary>
    /// Exports colony data to JSON format
    /// </summary>
    Task<string> ExportColonyDataToJsonAsync(object colonyData, string filePath);

    /// <summary>
    /// Exports ant population data to various formats
    /// </summary>
    Task<string> ExportPopulationDataAsync(object populationData, string filePath, string format);
  }
}
