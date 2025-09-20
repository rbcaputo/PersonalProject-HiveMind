using HiveMind.Core.Environment;
using HiveMind.Core.Events;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace HiveMind.Core.Monitoring
{
  /// <summary>
  /// Comprehensive event logging system for tracking all simulation events.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="EventLogger"/> class.
  /// </remarks>
  /// <param name="logger">Logger instance.</param>
  public sealed class EventLogger(ILogger<EventLogger> logger)
  {
    private readonly ILogger<EventLogger> _logger =
      logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentQueue<SimulationEventRecord> _eventLog = new();
    private readonly ConcurrentDictionary<string, EventStatistics> _eventStats = new();
    private readonly Lock _statsLock = new();

    /// <summary>
    /// Gets the recent event log entries.
    /// </summary>
    public IEnumerable<SimulationEventRecord> RecentEvents => _eventLog.TakeLast(1000);

    /// <summary>
    /// Gets event statistics by type.
    /// </summary>
    public IReadOnlyDictionary<string, EventStatistics> EventStatistics => _eventStats.AsReadOnly();

    /// <summary>
    /// Logs a bee-related event.
    /// </summary>
    /// <param name="beeEvent">The bee event to log.</param>
    /// <param name="additionalContext">Additional context information.</param>
    public void LogBeeEvent(BeeEvent beeEvent, object? additionalContext = null)
    {
      ArgumentNullException.ThrowIfNull(beeEvent);

      SimulationEventRecord record = new()
      {
        Id = Guid.NewGuid(),
        Timestamp = beeEvent.Timestamp,
        EventType = EventTypeCategory.Bee,
        EventName = beeEvent.EventType.ToString(),
        EntityId = beeEvent.Bee.Id,
        EntityType = beeEvent.Bee.BeeType.ToString(),
        Description = GenerateBeeEventDescription(beeEvent),
        AdditionalData = SerializeAdditionalData(beeEvent, additionalContext)
      };

      LogEvent(record);
    }

    /// <summary>
    /// Logs a hive-related event.
    /// </summary>
    /// <param name="hiveEvent">The hive event to log.</param>
    /// <param name="additionalContext">Additional context information.</param>
    public void LogHiveEvent(HiveEvent hiveEvent, object? additionalContext = null)
    {
      ArgumentNullException.ThrowIfNull(hiveEvent);

      SimulationEventRecord record = new()
      {
        Id = Guid.NewGuid(),
        Timestamp = hiveEvent.Timestamp,
        EventType = EventTypeCategory.Hive,
        EventName = hiveEvent.EventType.ToString(),
        EntityId = hiveEvent.Hive.Id,
        EntityType = "Beehive",
        Description = GenerateHiveEventDescription(hiveEvent),
        AdditionalData = SerializeAdditionalData(hiveEvent, additionalContext)
      };

      LogEvent(record);
    }

    /// <summary>
    /// Logs an environmental event.
    /// </summary>
    /// <param name="environmentalEvent">The environmental event to log.</param>
    /// <param name="impactDescription">Description of the event's impact.</param>
    public void LogEnvironmentalEvent(EnvironmentalEvent environmentalEvent, string? impactDescription = null)
    {
      ArgumentNullException.ThrowIfNull(environmentalEvent);

      SimulationEventRecord record = new()
      {
        Id = Guid.NewGuid(),
        Timestamp = environmentalEvent.StartTime,
        EventType = EventTypeCategory.Environmental,
        EventName = environmentalEvent.EventType.ToString(),
        EntityId = environmentalEvent.Id,
        EntityType = "Environment",
        Description = impactDescription ?? $"{environmentalEvent.EventType} event started",
        AdditionalData = SerializeAdditionalData(environmentalEvent, null)
      };

      LogEvent(record);
    }

    /// <summary>
    /// Logs a simulation system event.
    /// </summary>
    /// <param name="eventName">Name of the system event.</param>
    /// <param name="description">Event description.</param>
    /// <param name="additionalData">Additional event data.</param>
    public void LogSystemEvent(string eventName, string description, object? additionalData = null)
    {
      ArgumentException.ThrowIfNullOrEmpty(eventName);
      ArgumentException.ThrowIfNullOrEmpty(description);

      SimulationEventRecord record = new()
      {
        Id = Guid.NewGuid(),
        Timestamp = DateTime.UtcNow,
        EventType = EventTypeCategory.System,
        EventName = eventName,
        EntityId = null,
        EntityType = "System",
        Description = description,
        AdditionalData = SerializeAdditionalData(additionalData, null)
      };

      LogEvent(record);
    }

    /// <summary>
    /// Gets events filtered by criteria.
    /// </summary>
    /// <param name="filter">Event filter criteria.</param>
    /// <returns>Filtered events.</returns>
    public IEnumerable<SimulationEventRecord> GetEvents(EventFilter filter)
    {
      ArgumentNullException.ThrowIfNull(filter);

      IEnumerable<SimulationEventRecord> events = _eventLog.AsEnumerable();

      if (filter.StartTime.HasValue)
        events = events.Where(e => e.Timestamp >= filter.StartTime);

      if (filter.EndTime.HasValue)
        events = events.Where(e => e.Timestamp <= filter.EndTime);

      if (filter.EventTypes?.Count > 0 == true)
        events = events.Where(e => filter.EventTypes.Contains(e.EventType));

      if (filter.EntityIds?.Count > 0 == true)
        events = events.Where(e => e.EntityId.HasValue && filter.EntityIds.Contains(e.EntityId.Value));

      if (!string.IsNullOrEmpty(filter.EventName))
        events = events.Where(e => e.EventName.Contains(filter.EventName, StringComparison.OrdinalIgnoreCase));

      return events.OrderByDescending(e => e.Timestamp).Take(filter.MaxResults ?? 1000);
    }

    /// <summary>
    /// Gets event statistics for a specific time period.
    /// </summary>
    /// <param name="startTime">Start of the time period.</param>
    /// <param name="endTime">End of the time period.</param>
    /// <returns>Event statistics for the period.</returns>
    public EventPeriodStatistics GetEventStatistics(DateTime startTime, DateTime endTime)
    {
      List<SimulationEventRecord> periodEvents = [.. _eventLog.Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)];

      return new()
      {
        StartTime = startTime,
        EndTime = endTime,
        TotalEvents = periodEvents.Count,
        BeeEvents = periodEvents.Count(e => e.EventType == EventTypeCategory.Bee),
        HiveEvents = periodEvents.Count(e => e.EventType == EventTypeCategory.Hive),
        EnvironmentalEvents = periodEvents.Count(e => e.EventType == EventTypeCategory.Environmental),
        SystemEvents = periodEvents.Count(e => e.EventType == EventTypeCategory.System),
        EventsPerHour = periodEvents.Count / Math.Max(1, (endTime - startTime).TotalHours),
        MostActiveHour = GetMostActiveHour(periodEvents),
        TopEventTypes = GetTopEventTypes(periodEvents, 5)
      };
    }

    /// <summary>
    /// Exports events to JSON format.
    /// </summary>
    /// <param name="filter">Filter for events to export.</param>
    /// <returns>JSON string containing the events.</returns>
    public string ExportEventsToJson(EventFilter filter)
    {
      IEnumerable<SimulationEventRecord> events = GetEvents(filter);
      JsonSerializerOptions options = new()
      {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
      };

      return JsonSerializer.Serialize(events, options);
    }

    /// <summary>
    /// Clears old events beyond the retention period.
    /// </summary>
    /// <param name="retentionPeriod">How long to keep events.</param>
    /// <returns>Number of events cleared.</returns>
    public int ClearOldEvents(TimeSpan retentionPeriod)
    {
      DateTime cutoffTime = DateTime.UtcNow - retentionPeriod;
      int eventsCleared = 0;

      // Note: ConcurrentQueue doesn't support direct removal, 
      // so we'd need to rebuild the queue in a full implementation
      _logger.LogInformation(
        "Event retention cleanup would remove events older than {CutoffTime}",
        cutoffTime
      );

      return eventsCleared;
    }

    /// <summary>
    /// Core event logging method.
    /// </summary>
    /// <param name="record">Event record to log.</param>
    private void LogEvent(SimulationEventRecord record)
    {
      _eventLog.Enqueue(record);

      // Update statistics
      lock (_statsLock)
      {
        string key = $"{record.EventType}_{record.EventName}";
        _eventStats.AddOrUpdate(
          key,
          new EventStatistics { EventType = record.EventName, Count = 1, LastOccurrence = record.Timestamp },
          (k, existing) => new()
          {
            EventType = existing.EventType,
            Count = existing.Count + 1,
            LastOccurrence = record.Timestamp
          }
        );
      }

      // Keep log size manageable
      while (_eventLog.Count > 10000) _eventLog.TryDequeue(out _);

      // Log to standard logging system
      _logger.LogDebug(
        "Event logged: {EventType}.{EventName} for {EntityType} {EntityId}",
        record.EventType,
        record.EventName,
        record.EntityType,
        record.EntityId
      );
    }

    /// <summary>
    /// Generates description for bee events.
    /// </summary>
    /// <param name="beeEvent">The bee event.</param>
    /// <returns>Human-readable description.</returns>
    private static string GenerateBeeEventDescription(BeeEvent beeEvent) => beeEvent switch
    {
      BeeBirthEvent birth =>
        $"{birth.Bee.BeeType} bee emerged in chamber at {birth.BirthChamber.Position}",
      BeeDeathEvent death =>
        $"{death.Bee.BeeType} bee died at age {death.AgeAtDeath.TotalDays:F1} days (Cause: {death.Cause})",
      WorkerActivityChangeEvent activity =>
        $"Worker changed activity from {activity.PreviousActivity} to {activity.NewActivity}",
      QueenEggLayingEvent laying =>
        $"Queen laid {laying.EggsLaid} eggs in chamber",
      _ => $"{beeEvent.EventType} event occurred for {beeEvent.Bee.BeeType} bee"
    };

    /// <summary>
    /// Generates description for hive events.
    /// </summary>
    /// <param name="hiveEvent">The hive event.</param>
    /// <returns>Human-readable description.</returns>
    private static string GenerateHiveEventDescription(HiveEvent hiveEvent) => hiveEvent switch
    {
      HiveEstablishedEvent established =>
        $"Hive established with {established.InitialPopulation} bees",
      HiveCollapseEvent collapse =>
        $"Hive collapsed after {collapse.HiveAge.TotalDays:F0} days (Reason: {collapse.Reason})",
      HoneyProductionEvent production =>
        $"{production.HoneyAmount:F1} honey units produced by {production.ForagingWorkers} foragers",
      HoneyReadyForHarvestEvent harvest =>
        $"Honey super ready for harvest: {harvest.HarvestableAmount:F1} units available",
      _ => $"{hiveEvent.EventType} event occurred for hive {hiveEvent.Hive.Id:N}"
    };

    /// <summary>
    /// Serializes additional event data to JSON.
    /// </summary>
    /// <param name="primaryData">Primary event data.</param>
    /// <param name="additionalContext">Additional context.</param>
    /// <returns>Serialized JSON string.</returns>
    private static string SerializeAdditionalData(object? primaryData, object? additionalContext)
    {
      try
      {
        object data = new { Primary = primaryData, Context = additionalContext };
        return JsonSerializer.Serialize(data);
      }
      catch
      {
        return "{}";
      }
    }

    /// <summary>
    /// Gets the hour with most events in the given period.
    /// </summary>
    /// <param name="events">Events to analyze.</param>
    /// <returns>Hour of day with most activity.</returns>
    private static int GetMostActiveHour(List<SimulationEventRecord> events)
    {
      if (events.Count == 0) return 0;

      return events.GroupBy(e => e.Timestamp.Hour)
                   .OrderByDescending(g => g.Count())
                   .First().Key;
    }

    /// <summary>
    /// Gets the top event types by frequency.
    /// </summary>
    /// <param name="events">Events to analyze.</param>
    /// <param name="count">Number of top types to return.</param>
    /// <returns>Top event types with counts.</returns>
    private static Dictionary<string, int> GetTopEventTypes(List<SimulationEventRecord> events, int count) =>
      events.GroupBy(e => e.EventName)
            .OrderByDescending(g => g.Count())
            .Take(count)
            .ToDictionary(g => g.Key, g => g.Count());
  }

  /// <summary>
  /// Record representing a single simulation event.
  /// </summary>
  public sealed class SimulationEventRecord
  {
    /// <summary>
    /// Gets or sets the unique identifier for this event record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the category of event.
    /// </summary>
    public EventTypeCategory EventType { get; set; }

    /// <summary>
    /// Gets or sets the specific name of the event.
    /// </summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the entity involved in the event.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Gets or sets the type of entity involved.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a human-readable description of the event.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional event data as JSON.
    /// </summary>
    public string AdditionalData { get; set; } = "{}";
  }

  /// <summary>
  /// Categories of simulation events.
  /// </summary>
  public enum EventTypeCategory
  {
    /// <summary>
    /// Events related to individual bees.
    /// </summary>
    Bee = 1,

    /// <summary>
    /// Events related to beehives.
    /// </summary>
    Hive = 2,

    /// <summary>
    /// Environmental events affecting the simulation.
    /// </summary>
    Environmental = 3,

    /// <summary>
    /// System events related to simulation management.
    /// </summary>
    System = 4
  }

  /// <summary>
  /// Filter criteria for querying events.
  /// </summary>
  public sealed class EventFilter
  {
    /// <summary>
    /// Gets or sets the start time for event filtering.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time for event filtering.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the event types to include.
    /// </summary>
    public List<EventTypeCategory>? EventTypes { get; set; }

    /// <summary>
    /// Gets or sets the entity IDs to filter by.
    /// </summary>
    public List<Guid>? EntityIds { get; set; }

    /// <summary>
    /// Gets or sets the event name to search for (partial match).
    /// </summary>
    public string? EventName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of results to return.
    /// </summary>
    public int? MaxResults { get; set; } = 1000;
  }

  /// <summary>
  /// Statistics about a specific event type.
  /// </summary>
  public sealed class EventStatistics
  {
    /// <summary>
    /// Gets or sets the event type name.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of times this event has occurred.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last occurrence.
    /// </summary>
    public DateTime LastOccurrence { get; set; }
  }

  /// <summary>
  /// Statistics for events within a specific time period.
  /// </summary>
  public sealed class EventPeriodStatistics
  {
    /// <summary>
    /// Gets or sets the start time of the analysis period.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of the analysis period.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the total number of events in the period.
    /// </summary>
    public int TotalEvents { get; set; }

    /// <summary>
    /// Gets or sets the number of bee events.
    /// </summary>
    public int BeeEvents { get; set; }

    /// <summary>
    /// Gets or sets the number of hive events.
    /// </summary>
    public int HiveEvents { get; set; }

    /// <summary>
    /// Gets or sets the number of environmental events.
    /// </summary>
    public int EnvironmentalEvents { get; set; }

    /// <summary>
    /// Gets or sets the number of system events.
    /// </summary>
    public int SystemEvents { get; set; }

    /// <summary>
    /// Gets or sets the average events per hour.
    /// </summary>
    public double EventsPerHour { get; set; }

    /// <summary>
    /// Gets or sets the hour of day with most activity.
    /// </summary>
    public int MostActiveHour { get; set; }

    /// <summary>
    /// Gets or sets the most frequent event types.
    /// </summary>
    public Dictionary<string, int> TopEventTypes { get; set; } = [];
  }
}
