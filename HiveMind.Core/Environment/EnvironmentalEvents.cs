namespace HiveMind.Core.Environment
{
  /// <summary>
  /// Base class for environmental events that affect bee colonies.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="EnvironmentalEvent"/> class.
  /// </remarks>
  /// <param name="startTime">When the event starts.</param>
  /// <param name="duration">How long the event lasts.</param>
  public abstract class EnvironmentalEvent(DateTime startTime, TimeSpan duration)
  {
    /// <summary>
    /// Gets the unique identifier for this event.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the time when this event started.
    /// </summary>
    public DateTime StartTime { get; } = startTime;

    /// <summary>
    /// Gets the duration of this event.
    /// </summary>
    public TimeSpan Duration { get; } = duration;

    /// <summary>
    /// Gets the time when this event will end.
    /// </summary>
    public DateTime EndTime => StartTime + Duration;

    /// <summary>
    /// Gets the type of environmental event.
    /// </summary>
    public abstract EnvironmentalEventType EventType { get; }

    /// <summary>
    /// Determines if this event should end at the specified time.
    /// </summary>
    /// <param name="currentTime">Current time to check against.</param>
    /// <returns>True if the event should end.</returns>
    public virtual bool ShouldEnd(DateTime currentTime) => currentTime >= EndTime;

    /// <summary>
    /// Updates the event state based on current conditions.
    /// </summary>
    /// <param name="currentTime">Current simulation time.</param>
    /// <param name="currentWeather">Current weather conditions.</param>
    public virtual void Update(DateTime currentTime, WeatherState currentWeather)
    {
      // Base implementation - derived classes can override for specific behavior
    }

    /// <summary>
    /// Gets the environmental impact of this event.
    /// </summary>
    /// <param name="currentWeather">Current weather conditions.</param>
    /// <returns>The environmental impact.</returns>
    public abstract EnvironmentalImpact GetImpact(WeatherState currentWeather);
  }

  /// <summary>
  /// Represents a drought event that reduces foraging effectiveness.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="DroughtEvent"/> class.
  /// </remarks>
  /// <param name="startTime">Start time of the drought.</param>
  /// <param name="duration">Duration of the drought.</param>
  /// <param name="severity">Severity of the drought (0.0 to 1.0).</param>
  public sealed class DroughtEvent(
    DateTime startTime,
    TimeSpan duration,
    double severity = 0.8
  ) : EnvironmentalEvent(startTime, duration)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override EnvironmentalEventType EventType => EnvironmentalEventType.Drought;

    /// <summary>
    /// Gets the severity of the drought (0.0 = mild, 1.0 = severe).
    /// </summary>
    public double Severity { get; } = Math.Max(0.0, Math.Min(1.0, severity));

    /// <summary>
    /// Gets the environmental impact of the drought.
    /// </summary>
    /// <param name="currentWeather">Current weather conditions.</param>
    /// <returns>Drought impact on the environment.</returns>
    public override EnvironmentalImpact GetImpact(WeatherState currentWeather)
    {
      double impactMultiplier = 0.2 + (Severity * 0.8); // Range: 0.2 to 1.0

      return new()
      {
        ForagingEfficiencyModifier = 0.1 + (0.4 * (1.0 - Severity)), // 0.1 to 0.5
        HoneyProductionModifier = 0.2 + (0.3 * (1.0 - Severity)),    // 0.2 to 0.5
        WaterAvailabilityModifier = 0.1 + (0.2 * (1.0 - Severity)),  // 0.1 to 0.3
        TemperatureModifier = 1.0 + (Severity * 3.0),                // +1 to +4°C
        EggLayingRateModifier = 0.3 + (0.4 * (1.0 - Severity)),      // 0.3 to 0.7
        Description =
          $"Drought conditions (Severity: {Severity:P0}) reducing nectar and water availability"
      };
    }
  }

  /// <summary>
  /// Represents a cold snap that affects bee activity.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="ColdSnapEvent"/> class.
  /// </remarks>
  /// <param name="startTime">Start time of the cold snap.</param>
  /// <param name="duration">Duration of the cold snap.</param>
  /// <param name="intensity">Intensity of the cold snap (0.0 to 1.0).</param>
  public sealed class ColdSnapEvent(
    DateTime startTime,
    TimeSpan duration,
    double intensity = 0.7
  ) : EnvironmentalEvent(startTime, duration)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override EnvironmentalEventType EventType => EnvironmentalEventType.ColdSnap;

    /// <summary>
    /// Gets the intensity of the cold snap (0.0 = mild, 1.0 = severe).
    /// </summary>
    public double Intensity { get; } = Math.Max(0.0, Math.Min(1.0, intensity));

    /// <summary>
    /// Gets the environmental impact of the cold snap.
    /// </summary>
    /// <param name="currentWeather">Current weather conditions.</param>
    /// <returns>Cold snap impact on the environment.</returns>
    public override EnvironmentalImpact GetImpact(WeatherState currentWeather)
    {
      return new()
      {
        ForagingEfficiencyModifier = Math.Max(0.05, 0.4 * (1.0 - Intensity)), // 0.05 to 0.4
        EnergyConsumptionModifier = 1.2 + (Intensity * 0.8),                  // 1.2 to 2.0
        BroodDevelopmentModifier = 0.4 + (0.4 * (1.0 - Intensity)),           // 0.4 to 0.8
        TemperatureModifier = -(3.0 + (Intensity * 12.0)),                    // -3 to -15°C
        EggLayingRateModifier = 0.1 + (0.5 * (1.0 - Intensity)),              // 0.1 to 0.6
        Description =
          $"Cold snap (Intensity: {Intensity:P0}) preventing bee activity and increasing energy needs"
      };
    }
  }

  /// <summary>
  /// Represents a heat wave that stresses the colony.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="HeatWaveEvent"/> class.
  /// </remarks>
  /// <param name="startTime">Start time of the heat wave.</param>
  /// <param name="duration">Duration of the heat wave.</param>
  /// <param name="intensity">Intensity of the heat wave (0.0 to 1.0).</param>
  public sealed class HeatWaveEvent(
    DateTime startTime,
    TimeSpan duration,
    double intensity = 0.6
  ) : EnvironmentalEvent(startTime, duration)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override EnvironmentalEventType EventType => EnvironmentalEventType.HeatWave;

    /// <summary>
    /// Gets the intensity of the heat wave (0.0 = mild, 1.0 = extreme).
    /// </summary>
    public double Intensity { get; } = Math.Max(0.0, Math.Min(1.0, intensity));

    /// <summary>
    /// Gets the environmental impact of the heat wave.
    /// </summary>
    /// <param name="currentWeather">Current weather conditions.</param>
    /// <returns>Heat wave impact on the environment.</returns>
    public override EnvironmentalImpact GetImpact(WeatherState currentWeather)
    {
      return new()
      {
        ForagingEfficiencyModifier = 0.3 + (0.4 * (1.0 - Intensity)),        // 0.3 to 0.7
        EnergyConsumptionModifier = 1.1 + (Intensity * 0.6),                 // 1.1 to 1.7
        WaterAvailabilityModifier = 0.5 + (0.4 * (1.0 - Intensity)),         // 0.5 to 0.9
        TemperatureModifier = 3.0 + (Intensity * 10.0),                      // +3 to +13°C
        BroodDevelopmentModifier = 0.6 + (0.3 * (1.0 - Intensity)),          // 0.6 to 0.9
        EggLayingRateModifier = 0.7 + (0.2 * (1.0 - Intensity)),             // 0.7 to 0.9
        Description = $"Heat wave (Intensity: {Intensity:P0}) causing stress and reduced activity"
      };
    }
  }

  /// <summary>
  /// Represents a nectar flow event that boosts honey production.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="NectarFlowEvent"/> class.
  /// </remarks>
  /// <param name="startTime">Start time of the nectar flow.</param>
  /// <param name="duration">Duration of the nectar flow.</param>
  /// <param name="abundance">Abundance of the nectar flow (0.0 to 1.0).</param>
  public sealed class NectarFlowEvent(
    DateTime startTime,
    TimeSpan duration,
    double abundance = 0.7
  ) : EnvironmentalEvent(startTime, duration)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override EnvironmentalEventType EventType => EnvironmentalEventType.NectarFlow;

    /// <summary>
    /// Gets the abundance level of the nectar flow (0.0 = light, 1.0 = major flow).
    /// </summary>
    public double Abundance { get; } = Math.Max(0.0, Math.Min(1.0, abundance));

    /// <summary>
    /// Gets the environmental impact of the nectar flow.
    /// </summary>
    /// <param name="currentWeather">Current weather conditions.</param>
    /// <returns>Nectar flow impact on the environment.</returns>
    public override EnvironmentalImpact GetImpact(WeatherState currentWeather)
    {
      double flowMultiplier = 0.5 + (Abundance * 1.5); // Range: 0.5 to 2.0

      return new()
      {
        ForagingEfficiencyModifier = 1.2 + (Abundance * 1.0), // 1.2 to 2.2
        HoneyProductionModifier = 1.5 + (Abundance * 1.2),    // 1.5 to 2.7
        WaterAvailabilityModifier = 1.1 + (Abundance * 0.3),  // 1.1 to 1.4
        EggLayingRateModifier = 1.1 + (Abundance * 0.6),      // 1.1 to 1.7
        BroodDevelopmentModifier = 1.0 + (Abundance * 0.2),   // 1.0 to 1.2
        Description =
          $"Nectar flow (Abundance: {Abundance:P0}) providing excellent foraging opportunities"
      };
    }
  }

  /// <summary>
  /// Represents a storm system that severely impacts bee activities.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="StormEvent"/> class.
  /// </remarks>
  /// <param name="startTime">Start time of the storm.</param>
  /// <param name="duration">Duration of the storm.</param>
  /// <param name="severity">Severity of the storm (0.0 to 1.0).</param>
  public sealed class StormEvent(
    DateTime startTime,
    TimeSpan duration,
    double severity = 0.8
  ) : EnvironmentalEvent(startTime, duration)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override EnvironmentalEventType EventType => EnvironmentalEventType.Storm;

    /// <summary>
    /// Gets the severity of the storm (0.0 = light storm, 1.0 = severe storm).
    /// </summary>
    public double Severity { get; } = Math.Max(0.0, Math.Min(1.0, severity));

    /// <summary>
    /// Gets the environmental impact of the storm.
    /// </summary>
    /// <param name="currentWeather">Current weather conditions.</param>
    /// <returns>Storm impact on the environment.</returns>
    public override EnvironmentalImpact GetImpact(WeatherState currentWeather)
    {
      return new()
      {
        ForagingEfficiencyModifier = Math.Max(0.0, 0.2 * (1.0 - Severity)), // 0.0 to 0.2
        HoneyProductionModifier = Math.Max(0.0, 0.1 * (1.0 - Severity)),    // 0.0 to 0.1
        EnergyConsumptionModifier = 1.1 + (Severity * 0.4),                 // 1.1 to 1.5
        WaterAvailabilityModifier = 0.8 + (0.4 * Severity),                 // 0.8 to 1.2 (flooding vs drought)
        BroodDevelopmentModifier = 0.7 + (0.2 * (1.0 - Severity)),          // 0.7 to 0.9
        TemperatureModifier = -(1.0 + (Severity * 4.0)),                    // -1 to -5°C
        Description = $"Storm system (Severity: {Severity:P0}) preventing most bee activities"
      };
    }
  }

  /// <summary>
  /// Represents the environmental impact of an event.
  /// </summary>
  public sealed class EnvironmentalImpact
  {
    /// <summary>
    /// Gets or sets the foraging efficiency modifier (0.0 to 3.0).
    /// </summary>
    public double ForagingEfficiencyModifier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the honey production modifier (0.0 to 3.0).
    /// </summary>
    public double HoneyProductionModifier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the water availability modifier (0.0 to 2.0).
    /// </summary>
    public double WaterAvailabilityModifier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the energy consumption modifier (0.5 to 2.0).
    /// </summary>
    public double EnergyConsumptionModifier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the egg laying rate modifier (0.0 to 2.0).
    /// </summary>
    public double EggLayingRateModifier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the brood development modifier (0.3 to 1.5).
    /// </summary>
    public double BroodDevelopmentModifier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the temperature modifier (adjustment in degrees Celsius).
    /// </summary>
    public double TemperatureModifier { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets a description of the environmental impact.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets a neutral environmental impact (no changes).
    /// </summary>
    public static EnvironmentalImpact None => new();

    /// <summary>
    /// Combines this impact with another impact, taking the most significant effects.
    /// </summary>
    /// <param name="other">Other impact to combine with.</param>
    public void Combine(EnvironmentalImpact other)
    {
      ArgumentNullException.ThrowIfNull(other);

      // Take the most restrictive modifiers for negative effects
      ForagingEfficiencyModifier = Math.Min(ForagingEfficiencyModifier, other.ForagingEfficiencyModifier);
      WaterAvailabilityModifier = Math.Min(WaterAvailabilityModifier, other.WaterAvailabilityModifier);

      // Take the most beneficial modifiers for positive effects
      HoneyProductionModifier = Math.Max(HoneyProductionModifier, other.HoneyProductionModifier);
      EggLayingRateModifier = Math.Max(EggLayingRateModifier, other.EggLayingRateModifier);

      // Combine energy and development effects multiplicatively
      EnergyConsumptionModifier *= other.EnergyConsumptionModifier;
      BroodDevelopmentModifier *= other.BroodDevelopmentModifier;

      // Add temperature effects
      TemperatureModifier += other.TemperatureModifier;

      // Combine descriptions
      if (!string.IsNullOrEmpty(other.Description))
        Description = string.IsNullOrEmpty(Description)
                      ? other.Description
                      : $"{Description}; {other.Description}";
    }
  }

  /// <summary>
  /// Types of environmental events.
  /// </summary>
  public enum EnvironmentalEventType
  {
    /// <summary>
    /// Drought conditions reducing water and nectar availability.
    /// </summary>
    Drought = 1,

    /// <summary>
    /// Sudden temperature drop affecting bee activity.
    /// </summary>
    ColdSnap = 2,

    /// <summary>
    /// Extended period of high temperatures.
    /// </summary>
    HeatWave = 3,

    /// <summary>
    /// Period of abundant nectar availability.
    /// </summary>
    NectarFlow = 4,

    /// <summary>
    /// Storm system affecting weather patterns.
    /// </summary>
    Storm = 5,

    /// <summary>
    /// Disease outbreak affecting plants or bees.
    /// </summary>
    Disease = 6
  }

  /// <summary>
  /// Event arguments for environmental event started.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="EnvironmentalEventStartedArgs"/> class.
  /// </remarks>
  /// <param name="environmentalEvent">The event that started.</param>
  /// <param name="startTime">When it started.</param>
  public sealed class EnvironmentalEventStartedArgs(
    EnvironmentalEvent environmentalEvent,
    DateTime startTime
  ) : EventArgs
  {
    /// <summary>
    /// Gets the environmental event that started.
    /// </summary>
    public EnvironmentalEvent Event { get; } =
      environmentalEvent ?? throw new ArgumentNullException(nameof(environmentalEvent));

    /// <summary>
    /// Gets the time when the event started.
    /// </summary>
    public DateTime StartTime { get; } = startTime;
  }

  /// <summary>
  /// Event arguments for environmental event ended.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="EnvironmentalEventEndedArgs"/> class.
  /// </remarks>
  /// <param name="environmentalEvent">The event that ended.</param>
  /// <param name="endTime">When it ended.</param>
  public sealed class EnvironmentalEventEndedArgs(
    EnvironmentalEvent environmentalEvent,
    DateTime endTime
  ) : EventArgs
  {
    /// <summary>
    /// Gets the environmental event that ended.
    /// </summary>
    public EnvironmentalEvent Event { get; } =
      environmentalEvent ?? throw new ArgumentNullException(nameof(environmentalEvent));

    /// <summary>
    /// Gets the time when the event ended.
    /// </summary>
    public DateTime EndTime { get; } = endTime;
  }
}
