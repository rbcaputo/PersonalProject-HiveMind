using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace HiveMind.Infrastructure.Monitoring
{
  /// <summary>
  /// Implementation of performance monitoring for the simulation
  /// </summary>
  public class SimulationPerformanceMonitor(ILogger<SimulationPerformanceMonitor>? logger = null) : IPerformanceMonitor
  {
    private readonly ILogger<SimulationPerformanceMonitor>? _logger = logger;
    private readonly Queue<double> _tickTimes = new();
    private readonly object _lock = new();
    private readonly Stopwatch _runtimeStopwatch = Stopwatch.StartNew();

    private long _totalTicks = 0;
    private double _minTickTime = double.MaxValue;
    private double _maxTickTime = 0;
    private long _peakMemoryUsage = 0;
    private int _currentPopulation = 0;
    private int _peakPopulation = 0;

    public void RecordTickTime(double milliseconds)
    {
      lock (_lock)
      {
        _tickTimes.Enqueue(milliseconds);
        _totalTicks++;

        if (_tickTimes.Count > 1000)
          _tickTimes.Dequeue();

        if (milliseconds < _minTickTime)
          _minTickTime = milliseconds;
        if (milliseconds > _maxTickTime)
          _maxTickTime = milliseconds;

        // Log performance warnings
        if (milliseconds > 100) // Tick took longer than 100ms
          _logger?.LogWarning("Slow tick detected: {TickTime:F2}ms", milliseconds);
      }
    }

    public void RecordMemoryUsage(long bytes)
    {
      lock (_lock)
      {
        if (bytes > _peakMemoryUsage)
          _peakMemoryUsage = bytes;
      }
    }

    public void RecordPopulation(int count)
    {
      lock (_lock)
      {
        _currentPopulation = count;
        if (count > _peakPopulation)
          _peakPopulation = count;
      }
    }

    public double GetAverageTickTime(int sampleSize = 100)
    {
      lock (_lock)
      {
        if (_tickTimes.Count == 0)
          return 0;

        var samplesToTake = Math.Min(sampleSize, _tickTimes.Count);
        return _tickTimes.TakeLast(samplesToTake).Average();
      }
    }

    public long GetCurrentMemoryUsage() =>
      GC.GetTotalMemory(false);

    public PerformanceStatistics GetStatistics()
    {
      lock (_lock)
      {
        return new PerformanceStatistics
        {
          AverageTickTime = GetAverageTickTime(),
          MinTickTime = _minTickTime == double.MaxValue ? 0 : _minTickTime,
          MaxTickTime = _maxTickTime,
          CurrentMemoryUsage = GetCurrentMemoryUsage(),
          PeakMemoryUsage = _peakMemoryUsage,
          CurrentPopulation = _currentPopulation,
          PeakPopulation = _peakPopulation,
          LastUpdated = DateTime.UtcNow,
          TotalRuntime = _runtimeStopwatch.Elapsed,
          TotalTicks = _totalTicks
        };
      }
    }

    public void Reset()
    {
      lock ( _lock)
      {
        _tickTimes.Clear();
        _totalTicks = 0;
        _minTickTime = double.MaxValue;
        _maxTickTime = 0;
        _peakMemoryUsage = 0;
        _peakPopulation = 0;
        _runtimeStopwatch.Restart();

        _logger?.LogInformation("Performance monitor reset");
      }
    }
  }
}
