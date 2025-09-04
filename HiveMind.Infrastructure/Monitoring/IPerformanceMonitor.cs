namespace HiveMind.Infrastructure.Monitoring
{
  /// <summary>
  /// Interface for monitoring simulation performance
  /// </summary>
  public interface IPerformanceMonitor
  {
    /// <summary>
    /// Records the time taken for a simulation tick
    /// </summary>
    void RecordTickTime(double milliseconds);

    /// <summary>
    /// Records memory usage
    /// </summary>
    void RecordMemoryUsage(long bytes);

    /// <summary>
    /// Records population count
    /// </summary>
    void RecordPopulation(int count);

    /// <summary>
    /// Gets average tick time over the last N ticks
    /// </summary>
    double GetAverageTickTime(int sampleSize = 100);

    /// <summary>
    /// Gets current memory usage
    /// </summary>
    long GetCurrentMemoryUsage();

    /// <summary>
    /// Gets performance statistics
    /// </summary>
    PerformanceStatistics GetStatistics();

    /// <summary>
    /// Resets all performance counters
    /// </summary>
    void Reset();
  }
}
