using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace HiveMind.Core.Performance
{
  /// <summary>
  /// Performance profiler for monitoring and optimizing simulation performance.
  /// Tracks execution times, memory usage, and system resource utilization.
  /// </summary>
  public sealed class PerformanceProfiler
  {
    private readonly ILogger<PerformanceProfiler> _logger;
    private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics;
    private readonly ConcurrentQueue<PerformanceSnapshot> _snapshots;
    private readonly Timer _snapshotTimer;
    private readonly Lock _metricsLock = new();

    /// <summary>
    /// Gets the current performance metrics.
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetric> CurrentMetrics => _metrics.AsReadOnly();

    /// <summary>
    /// Gets recent performance snapshots.
    /// </summary>
    public IEnumerable<PerformanceSnapshot> RecentSnapshots => _snapshots.TakeLast(100);

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceProfiler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public PerformanceProfiler(ILogger<PerformanceProfiler> logger)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _metrics = new();
      _snapshots = new();

      // Take snapshots every 30 seconds
      _snapshotTimer = new(TakeSnapshot, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Measures the execution time of an operation.
    /// </summary>
    /// <param name="operationName">Name of the operation being measured.</param>
    /// <param name="operation">The operation to measure.</param>
    /// <returns>The result of the operation.</returns>
    public T MeasureOperation<T>(string operationName, Func<T> operation)
    {
      ArgumentException.ThrowIfNullOrEmpty(operationName);
      ArgumentNullException.ThrowIfNull(operation);

      Stopwatch stopwatch = Stopwatch.StartNew();
      long initialMemory = GC.GetTotalMemory(false);

      try
      {
        T result = operation();
        stopwatch.Stop();

        long finalMemory = GC.GetTotalMemory(false);
        long memoryDelta = finalMemory - initialMemory;

        RecordMetric(operationName, stopwatch.Elapsed, memoryDelta, true);
        return result;
      }
      catch (Exception ex)
      {
        stopwatch.Stop();
        RecordMetric(operationName, stopwatch.Elapsed, 0, false);
        _logger.LogError(ex, "Performance measurement failed for operation {OperationName}", operationName);
        throw;
      }
    }

    /// <summary>
    /// Measures the execution time of an async operation.
    /// </summary>
    /// <param name="operationName">Name of the operation being measured.</param>
    /// <param name="operation">The async operation to measure.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<T> MeasureOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
      ArgumentException.ThrowIfNullOrEmpty(operationName);
      ArgumentNullException.ThrowIfNull(operation);

      Stopwatch stopwatch = Stopwatch.StartNew();
      long initialMemory = GC.GetTotalMemory(false);

      try
      {
        T result = await operation();
        stopwatch.Stop();

        long finalMemory = GC.GetTotalMemory(false);
        long memoryDelta = finalMemory - initialMemory;

        RecordMetric(operationName, stopwatch.Elapsed, memoryDelta, true);
        return result;
      }
      catch (Exception ex)
      {
        stopwatch.Stop();
        RecordMetric(operationName, stopwatch.Elapsed, 0, false);
        _logger.LogError(ex, "Performance measurement failed for operation {OperationName}", operationName);
        throw;
      }
    }

    /// <summary>
    /// Creates a performance measurement scope that automatically measures duration.
    /// </summary>
    /// <param name="operationName">Name of the operation being measured.</param>
    /// <returns>A disposable performance scope.</returns>
    public IPerformanceScope CreateScope(string operationName) => new PerformanceScope(operationName, this);

    /// <summary>
    /// Records a performance metric.
    /// </summary>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="duration">Duration of the operation.</param>
    /// <param name="memoryDelta">Memory change during operation.</param>
    /// <param name="success">Whether the operation succeeded.</param>
    internal void RecordMetric(string operationName, TimeSpan duration, long memoryDelta, bool success)
    {
      _metrics.AddOrUpdate(
        operationName,
        new PerformanceMetric(operationName, duration, memoryDelta, success),
        (key, existing) => existing.Update(duration, memoryDelta, success)
      );

      // Log slow operations
      if (duration.TotalMilliseconds > 1000)
      {
        _logger.LogWarning(
          "Slow operation detected: {OperationName} took {Duration}ms",
          operationName,
          duration.TotalMilliseconds
        );
      }
    }

    /// <summary>
    /// Gets performance statistics for analysis.
    /// </summary>
    /// <returns>Performance statistics summary.</returns>
    public PerformanceStatistics GetStatistics()
    {
      lock (_metricsLock)
      {
        List<PerformanceMetric> metrics = [.. _metrics.Values];
        List<PerformanceSnapshot> recentSnapshots = [.. _snapshots.TakeLast(10)];

        return new()
        {
          TotalOperations = metrics.Sum(m => m.TotalCalls),
          AverageExecutionTime = TimeSpan.FromMilliseconds(
            metrics
              .Where(m => m.TotalCalls > 0)
              .Average(m => m.AverageExecutionTime.TotalMilliseconds)
          ),
          TotalMemoryAllocated = metrics.Sum(m => m.TotalMemoryAllocated),
          SuccessRate = metrics.Where(m => m.TotalCalls > 0).Average(m => m.SuccessRate),
          SlowestOperations = [.. metrics.OrderByDescending(m => m.AverageExecutionTime).Take(5)],
          MemoryIntensiveOperations = [.. metrics.OrderByDescending(m => m.AverageMemoryPerCall).Take(5)],
          RecentCpuUsage = recentSnapshots.LastOrDefault()?.CpuUsagePercent ?? 0,
          RecentMemoryUsage = recentSnapshots.LastOrDefault()?.MemoryUsageMB ?? 0
        };
      }
    }

    /// <summary>
    /// Takes a system performance snapshot.
    /// </summary>
    private void TakeSnapshot(object? state)
    {
      try
      {
        Process process = Process.GetCurrentProcess();
        PerformanceSnapshot snapshot = new()
        {
          Timestamp = DateTime.UtcNow,
          CpuUsagePercent = GetCpuUsage(),
          MemoryUsageMB = process.WorkingSet64 / (1024.0 * 1024.0),
          ThreadCount = process.Threads.Count,
          HandleCount = process.HandleCount,
          GcGeneration0Collections = GC.CollectionCount(0),
          GcGeneration1Collections = GC.CollectionCount(1),
          GcGeneration2Collections = GC.CollectionCount(2)
        };

        _snapshots.Enqueue(snapshot);

        // Keep only recent snapshots
        while (_snapshots.Count > 1000)
          _snapshots.TryDequeue(out _);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to take performance snapshot");
      }
    }

    /// <summary>
    /// Gets current CPU usage percentage.
    /// </summary>
    /// <returns>CPU usage as a percentage.</returns>
    private static double GetCpuUsage()
    {
      try
      {
        using Process process = Process.GetCurrentProcess();
        DateTime startTime = DateTime.UtcNow;
        TimeSpan startCpuUsage = process.TotalProcessorTime;

        Thread.Sleep(100); // Short delay for measurement

        DateTime endTime = DateTime.UtcNow;
        TimeSpan endCpuUsage = process.TotalProcessorTime;

        double cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        double totalMsPassed = (endTime - startTime).TotalMilliseconds;
        var cpuUsageTotal = cpuUsedMs / (Entities.Environment.ProcessorCount * totalMsPassed);

        return Math.Min(100.0, cpuUsageTotal * 100.0);
      }
      catch
      {
        return 0; // Return 0 if unable to measure
      }
    }

    /// <summary>
    /// Disposes the profiler and releases resources.
    /// </summary>
    public void Dispose() => _snapshotTimer?.Dispose();
  }

  /// <summary>
  /// Represents a performance metric for a specific operation.
  /// </summary>
  public sealed class PerformanceMetric
  {
    private readonly Lock _updateLock = new();
    private long _totalCalls;
    private long _successfulCalls;
    private TimeSpan _totalExecutionTime;
    private long _totalMemoryAllocated;
    private TimeSpan _minExecutionTime = TimeSpan.MaxValue;
    private TimeSpan _maxExecutionTime = TimeSpan.MinValue;

    /// <summary>
    /// Gets the operation name.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets the total number of calls to this operation.
    /// </summary>
    public long TotalCalls => _totalCalls;

    /// <summary>
    /// Gets the number of successful calls.
    /// </summary>
    public long SuccessfulCalls => _successfulCalls;

    /// <summary>
    /// Gets the success rate as a percentage.
    /// </summary>
    public double SuccessRate => _totalCalls == 0 ? 0 : (double)_successfulCalls / _totalCalls * 100.0;

    /// <summary>
    /// Gets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime => _totalCalls == 0 ? TimeSpan.Zero
                                                             : TimeSpan.FromTicks(_totalExecutionTime.Ticks / _totalCalls);

    /// <summary>
    /// Gets the minimum execution time.
    /// </summary>
    public TimeSpan MinExecutionTime => _minExecutionTime == TimeSpan.MaxValue ? TimeSpan.Zero : _minExecutionTime;

    /// <summary>
    /// Gets the maximum execution time.
    /// </summary>
    public TimeSpan MaxExecutionTime => _maxExecutionTime == TimeSpan.MinValue ? TimeSpan.Zero : _maxExecutionTime;

    /// <summary>
    /// Gets the total memory allocated by this operation.
    /// </summary>
    public long TotalMemoryAllocated => _totalMemoryAllocated;

    /// <summary>
    /// Gets the average memory allocated per call.
    /// </summary>
    public double AverageMemoryPerCall => _totalCalls == 0 ? 0 : (double)_totalMemoryAllocated / _totalCalls;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceMetric"/> class.
    /// </summary>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="executionTime">Initial execution time.</param>
    /// <param name="memoryDelta">Initial memory change.</param>
    /// <param name="success">Whether the initial call was successful.</param>
    public PerformanceMetric(string operationName, TimeSpan executionTime, long memoryDelta, bool success)
    {
      OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
      Update(executionTime, memoryDelta, success);
    }

    /// <summary>
    /// Updates the metric with a new measurement.
    /// </summary>
    /// <param name="executionTime">Execution time for this call.</param>
    /// <param name="memoryDelta">Memory change for this call.</param>
    /// <param name="success">Whether this call was successful.</param>
    /// <returns>The updated metric.</returns>
    public PerformanceMetric Update(TimeSpan executionTime, long memoryDelta, bool success)
    {
      lock (_updateLock)
      {
        _totalCalls++;
        if (success) _successfulCalls++;

        _totalExecutionTime = _totalExecutionTime.Add(executionTime);
        _totalMemoryAllocated += Math.Max(0, memoryDelta); // Only count allocations

        if (executionTime < _minExecutionTime)
          _minExecutionTime = executionTime;
        if (executionTime > _maxExecutionTime)
          _maxExecutionTime = executionTime;
      }

      return this;
    }
  }

  /// <summary>
  /// Represents a point-in-time performance snapshot of the system.
  /// </summary>
  public sealed class PerformanceSnapshot
  {
    /// <summary>
    /// Gets or sets the timestamp of this snapshot.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the CPU usage percentage.
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Gets or sets the memory usage in megabytes.
    /// </summary>
    public double MemoryUsageMB { get; set; }

    /// <summary>
    /// Gets or sets the number of active threads.
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Gets or sets the number of system handles.
    /// </summary>
    public int HandleCount { get; set; }

    /// <summary>
    /// Gets or sets the number of Generation 0 GC collections.
    /// </summary>
    public int GcGeneration0Collections { get; set; }

    /// <summary>
    /// Gets or sets the number of Generation 1 GC collections.
    /// </summary>
    public int GcGeneration1Collections { get; set; }

    /// <summary>
    /// Gets or sets the number of Generation 2 GC collections.
    /// </summary>
    public int GcGeneration2Collections { get; set; }
  }

  /// <summary>
  /// Performance statistics summary.
  /// </summary>
  public sealed class PerformanceStatistics
  {
    /// <summary>
    /// Gets or sets the total number of operations performed.
    /// </summary>
    public long TotalOperations { get; set; }

    /// <summary>
    /// Gets or sets the average execution time across all operations.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the total memory allocated across all operations.
    /// </summary>
    public long TotalMemoryAllocated { get; set; }

    /// <summary>
    /// Gets or sets the overall success rate percentage.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the slowest operations.
    /// </summary>
    public List<PerformanceMetric> SlowestOperations { get; set; } = [];

    /// <summary>
    /// Gets or sets the most memory-intensive operations.
    /// </summary>
    public List<PerformanceMetric> MemoryIntensiveOperations { get; set; } = [];

    /// <summary>
    /// Gets or sets the recent CPU usage percentage.
    /// </summary>
    public double RecentCpuUsage { get; set; }

    /// <summary>
    /// Gets or sets the recent memory usage in megabytes.
    /// </summary>
    public double RecentMemoryUsage { get; set; }
  }

  /// <summary>
  /// Interface for performance measurement scopes.
  /// </summary>
  public interface IPerformanceScope : IDisposable
  {
    /// <summary>
    /// Gets the operation name being measured.
    /// </summary>
    string OperationName { get; }

    /// <summary>
    /// Gets the elapsed time since the scope was created.
    /// </summary>
    TimeSpan Elapsed { get; }
  }

  /// <summary>
  /// Performance measurement scope that automatically measures operation duration.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="PerformanceScope"/> class.
  /// </remarks>
  /// <param name="operationName">Name of the operation being measured.</param>
  /// <param name="profiler">The performance profiler.</param>
  internal sealed class PerformanceScope(string operationName, PerformanceProfiler profiler) : IPerformanceScope
  {
    private readonly PerformanceProfiler _profiler =
      profiler ?? throw new ArgumentNullException(nameof(profiler));
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly long _initialMemory = GC.GetTotalMemory(false);
    private bool _disposed;

    /// <summary>
    /// Gets the operation name being measured.
    /// </summary>
    public string OperationName { get; } = operationName ?? throw new ArgumentNullException(nameof(operationName));

    /// <summary>
    /// Gets the elapsed time since the scope was created.
    /// </summary>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    /// Disposes the scope and records the performance measurement.
    /// </summary>
    public void Dispose()
    {
      if (_disposed) return;

      _stopwatch.Stop();
      long finalMemory = GC.GetTotalMemory(false);
      long memoryDelta = finalMemory - _initialMemory;

      _profiler.RecordMetric(OperationName, _stopwatch.Elapsed, memoryDelta, true);
      _disposed = true;
    }
  }
}
