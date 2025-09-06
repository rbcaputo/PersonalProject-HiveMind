using Microsoft.Extensions.Logging;

namespace HiveMind.Infrastructure.Logging
{
  /// <summary>
  /// Logger provider for simulation logging
  /// </summary>
  public class SimulationLoggerProvider : ILoggerProvider
  {
    private readonly Dictionary<string, SimulationLogger> _loggers = [];
    private readonly object _lock = new();
    private bool _disposed = false;

    public ILogger CreateLogger(string categoryName)
    {
      lock (_lock)
      {
        if (!_loggers.ContainsKey(categoryName))
          _loggers[categoryName] = new SimulationLogger(categoryName);

        return _loggers[categoryName];
      }
    }

    public SimulationLogger GetSimulationLogger(string categoryName)
    {
      lock (_lock)
        return _loggers.TryGetValue(categoryName, out var logger) ? logger : new SimulationLogger(categoryName);
    }

    public IEnumerable<SimulationLogEntry> GetAllLogEntries()
    {
      lock (_lock)
        return _loggers.Values.SelectMany(logger => logger.GetRecentEntries()).OrderBy(entry => entry.Timestamp);
    }

    public void Dispose()
    {
      if (!_disposed)
      {
        lock (_lock) _loggers.Clear();

        _disposed = true;

        GC.SuppressFinalize(this);
      }
    }
  }
}
