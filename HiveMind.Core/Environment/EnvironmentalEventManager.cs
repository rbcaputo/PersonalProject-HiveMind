using HiveMind.Core.Enums;
using Microsoft.Extensions.Logging;

namespace HiveMind.Core.Environment
{
  /// <summary>
  /// Manages environmental events that can affect the bee colony simulation.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="EnvironmentalEventManager"/> class.
  /// </remarks>
  /// <param name="logger">Logger instance.</param>
  public sealed class EnvironmentalEventManager(ILogger<EnvironmentalEventManager> logger)
  {
    private readonly ILogger<EnvironmentalEventManager> _logger =
      logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Random _random = new();
    private readonly List<EnvironmentalEvent> _activeEvents = [];
    private DateTime _lastEventCheck = DateTime.UtcNow;

    /// <summary>
    /// Gets the currently active environmental events.
    /// </summary>
    public IReadOnlyList<EnvironmentalEvent> ActiveEvents => _activeEvents.AsReadOnly();

    /// <summary>
    /// Event raised when a new environmental event starts.
    /// </summary>
    public event EventHandler<EnvironmentalEventStartedArgs>? EventStarted;

    /// <summary>
    /// Event raised when an environmental event ends.
    /// </summary>
    public event EventHandler<EnvironmentalEventEndedArgs>? EventEnded;

    /// <summary>
    /// Updates environmental events and checks for new events to trigger.
    /// </summary>
    /// <param name="currentTime">Current simulation time.</param>
    /// <param name="currentWeather">Current weather state.</param>
    /// <param name="season">Current season.</param>
    /// <returns>True if any events changed; otherwise, false.</returns>
    public bool UpdateEvents(DateTime currentTime, WeatherState currentWeather, Season season)
    {
      bool eventsChanged = false;

      // Check for ending events
      List<EnvironmentalEvent> endingEvents = [.. _activeEvents.Where(e => e.ShouldEnd(currentTime))];
      foreach (EnvironmentalEvent endingEvent in endingEvents)
      {
        EndEvent(endingEvent, currentTime);
        eventsChanged = true;
      }

      // Check for new events (limit checks to prevent excessive computation)
      TimeSpan timeSinceLastCheck = currentTime - _lastEventCheck;
      if (timeSinceLastCheck.TotalMinutes >= 30) // Check every 30 minutes
      {
        EnvironmentalEvent? newEvent = CheckForNewEvent(currentTime, currentWeather, season);
        if (newEvent != null)
        {
          StartEvent(newEvent, currentTime);
          eventsChanged = true;
        }
        _lastEventCheck = currentTime;
      }

      // Update active events
      foreach (EnvironmentalEvent activeEvent in _activeEvents)
        activeEvent.Update(currentTime, currentWeather);

      return eventsChanged;
    }

    /// <summary>
    /// Gets the combined impact of all active environmental events.
    /// </summary>
    /// <param name="currentWeather">Current weather state.</param>
    /// <returns>Combined environmental impact.</returns>
    public EnvironmentalImpact GetCombinedImpact(WeatherState currentWeather)
    {
      if (_activeEvents.Count == 0)
        return EnvironmentalImpact.None;

      EnvironmentalImpact combinedImpact = new();

      foreach (var activeEvent in _activeEvents)
      {
        EnvironmentalImpact impact = activeEvent.GetImpact(currentWeather);
        combinedImpact.Combine(impact);
      }

      return combinedImpact;
    }

    /// <summary>
    /// Checks for new environmental events to trigger.
    /// </summary>
    /// <param name="currentTime">Current time.</param>
    /// <param name="currentWeather">Current weather.</param>
    /// <param name="season">Current season.</param>
    /// <returns>New event to start, or null if none.</returns>
    private EnvironmentalEvent? CheckForNewEvent(
      DateTime currentTime,
      WeatherState currentWeather,
      Season season
    )
    {
      // Drought conditions
      if (ShouldTriggerDrought(currentWeather, season))
        return new DroughtEvent(currentTime, CalculateDroughtDuration());

      // Cold snap
      if (ShouldTriggerColdSnap(currentWeather, season))
        return new ColdSnapEvent(currentTime, CalculateColdSnapDuration());

      // Heat wave
      if (ShouldTriggerHeatWave(currentWeather, season))
        return new HeatWaveEvent(currentTime, CalculateHeatWaveDuration());

      // Nectar flow (positive event)
      if (ShouldTriggerNectarFlow(currentWeather, season))
        return new NectarFlowEvent(currentTime, CalculateNectarFlowDuration());

      return null;
    }

    /// <summary>
    /// Determines if drought conditions should trigger.
    /// </summary>
    private bool ShouldTriggerDrought(WeatherState weather, Season season)
    {
      if (_activeEvents.Any(e => e is DroughtEvent)) return false;

      double baseProbability = season switch
      {
        Season.Summer => 0.002, // 0.2% chance per check
        Season.Spring => 0.001,
        Season.Autumn => 0.001,
        Season.Winter => 0.0005,
        _ => 0.001
      };

      double weatherFactor = weather.WeatherType == WeatherType.Clear &&
                             weather.Humidity.Percentage < 40 ? 2.0 : 1.0;

      return _random.NextDouble() < baseProbability * weatherFactor;
    }

    /// <summary>
    /// Determines if cold snap should trigger.
    /// </summary>
    private bool ShouldTriggerColdSnap(WeatherState weather, Season season)
    {
      if (_activeEvents.Any(e => e is ColdSnapEvent)) return false;

      double baseProbability = season switch
      {
        Season.Winter => 0.003,
        Season.Spring => 0.002,
        Season.Autumn => 0.002,
        Season.Summer => 0.0005,
        _ => 0.001
      };

      double temperatureFactor = weather.Temperature.Celsius < 10 ? 1.5 : 1.0;

      return _random.NextDouble() < baseProbability * temperatureFactor;
    }

    /// <summary>
    /// Determines if heat wave should trigger.
    /// </summary>
    private bool ShouldTriggerHeatWave(WeatherState weather, Season season)
    {
      if (_activeEvents.Any(e => e is HeatWaveEvent)) return false;

      double baseProbability = season switch
      {
        Season.Summer => 0.003,
        Season.Spring => 0.001,
        Season.Autumn => 0.001,
        Season.Winter => 0.0002,
        _ => 0.001
      };

      double temperatureFactor = weather.Temperature.Celsius > 30 ? 1.5 : 1.0;

      return _random.NextDouble() < baseProbability * temperatureFactor;
    }

    /// <summary>
    /// Determines if nectar flow should trigger.
    /// </summary>
    private bool ShouldTriggerNectarFlow(WeatherState weather, Season season)
    {
      if (_activeEvents.Any(e => e is NectarFlowEvent)) return false;

      double baseProbability = season switch
      {
        Season.Spring => 0.004,
        Season.Summer => 0.003,
        Season.Autumn => 0.001,
        Season.Winter => 0.0001,
        _ => 0.002
      };

      double conditionFactor = weather.IsFavorableForForaging ? 1.8 : 1.0;

      return _random.NextDouble() < baseProbability * conditionFactor;
    }

    /// <summary>
    /// Calculates drought duration.
    /// </summary>
    private TimeSpan CalculateDroughtDuration() =>
      TimeSpan.FromDays(7 + _random.NextDouble() * 21); // 7-28 days

    /// <summary>
    /// Calculates cold snap duration.
    /// </summary>
    private TimeSpan CalculateColdSnapDuration() =>
      TimeSpan.FromDays(2 + _random.NextDouble() * 5); // 2-7 days

    /// <summary>
    /// Calculates heat wave duration.
    /// </summary>
    private TimeSpan CalculateHeatWaveDuration() =>
      TimeSpan.FromDays(3 + _random.NextDouble() * 7); // 3-10 days

    /// <summary>
    /// Calculates nectar flow duration.
    /// </summary>
    private TimeSpan CalculateNectarFlowDuration() =>
      TimeSpan.FromDays(5 + _random.NextDouble() * 15); // 5-20 days

    /// <summary>
    /// Starts a new environmental event.
    /// </summary>
    /// <param name="environmentalEvent">Event to start.</param>
    /// <param name="startTime">Start time.</param>
    private void StartEvent(EnvironmentalEvent environmentalEvent, DateTime startTime)
    {
      _activeEvents.Add(environmentalEvent);

      _logger.LogInformation(
        "Environmental event started: {EventType} (Duration: {Duration})",
        environmentalEvent.GetType().Name,
        environmentalEvent.Duration
      );

      EventStarted?.Invoke(this, new(environmentalEvent, startTime));
    }

    /// <summary>
    /// Ends an active environmental event.
    /// </summary>
    /// <param name="environmentalEvent">Event to end.</param>
    /// <param name="endTime">End time.</param>
    private void EndEvent(EnvironmentalEvent environmentalEvent, DateTime endTime)
    {
      _activeEvents.Remove(environmentalEvent);

      _logger.LogInformation(
        "Environmental event ended: {EventType} (Lasted: {Duration})",
        environmentalEvent.GetType().Name,
        endTime - environmentalEvent.StartTime
      );

      EventEnded?.Invoke(this, new(environmentalEvent, endTime));
    }
  }
}
