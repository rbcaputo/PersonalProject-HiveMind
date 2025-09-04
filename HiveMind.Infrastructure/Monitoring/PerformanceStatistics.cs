namespace HiveMind.Infrastructure.Monitoring
{
  /// <summary>
  /// Performance statistics for the simulation
  /// </summary>
  public class PerformanceStatistics
  {
    public double AverageTickTime { get; set; }
    public double MinTickTime { get; set; }
    public double MaxTickTime { get; set; }
    public long CurrentMemoryUsage { get; set; }
    public long PeakMemoryUsage { get; set; }
    public int CurrentPopulation { get; set; }
    public int PeakPopulation { get; set; }
    public DateTime LastUpdated { get; set; }
    public TimeSpan TotalRuntime { get; set; }
    public long TotalTicks { get; set; }

    public override string ToString()
    {
      return $"AvgTick: {AverageTickTime:F2}ms, Memory: {CurrentMemoryUsage / 1024 / 1024}MB, " +
        $"Population: {CurrentPopulation}, Runtime: {TotalRuntime:hh\\:mm\\:ss}";
    }
  }
}
