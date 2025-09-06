using Microsoft.Extensions.Logging;

namespace HiveMind.Infrastructure.Logging
{
  /// <summary>
  /// Specialized logger for simulation events
  /// </summary>
  public class SimulationLogger : ILogger
  {
    private readonly string _categoryName;
    private readonly List<SimulationLogEntry> _logEntries;
    private readonly object _lock = new();

    public SimulationLogger(string categoryName) => (_categoryName, _logEntries) = (categoryName, []);

    public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
      NoOpDisposable.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
      if (!IsEnabled(logLevel)) return;

      string message = formatter(state, exception);
      SimulationLogEntry logEntry = new()
      {
        Timestamp = DateTime.UtcNow,
        Level = logLevel,
        CategoryName = _categoryName,
        Message = message,
        Exception = exception
      };

      lock (_lock)
      {
        _logEntries.Add(logEntry);

        if (_logEntries.Count > 1000) _logEntries.RemoveRange(0, _logEntries.Count - 1000); // Keep only last 1000 entries to prevent memory issues
      }

      // Also output to console for development
      Console.WriteLine($"[{logEntry.Timestamp:HH:mm:ss}] [{logLevel}] {_categoryName}: {message}");
      if (exception != null) Console.WriteLine($"Exception: {exception}");
    }

    public IReadOnlyList<SimulationLogEntry> GetRecentEntries(int count = 100)
    {
      lock (_lock) return [.. _logEntries.TakeLast(count)];
    }

    public void ClearLogs()
    {
      lock (_lock) _logEntries.Clear();
    }

    private class NoOpDisposable : IDisposable
    {
      public static readonly NoOpDisposable Instance = new();
      public void Dispose() { }
    }
  }
}
