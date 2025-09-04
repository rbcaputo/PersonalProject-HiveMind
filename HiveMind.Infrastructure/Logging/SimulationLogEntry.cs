using Microsoft.Extensions.Logging;

namespace HiveMind.Infrastructure.Logging
{
  /// <summary>
  /// Represents a simulation log entry
  /// </summary>
  public class SimulationLogEntry
  {
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }

    public override string ToString()
    {
      var result = $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] {CategoryName}: {Message}";

      if (Exception != null)
        result += $"\nException: {Exception.Message}";

      return result;
    }
  }
}
