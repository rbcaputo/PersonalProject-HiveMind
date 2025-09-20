using HiveMind.Core.Enums;
using HiveMind.Core.ValueObject;

namespace HiveMind.Core.Environment
{
  /// <summary>
  /// Represents a complete weather state with all atmospheric conditions.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="WeatherState"/> class.
  /// </remarks>
  /// <param name="temperature">The current temperature.</param>
  /// <param name="humidity">The current humidity.</param>
  /// <param name="weatherType">The weather type.</param>
  /// <param name="windSpeed">The wind speed in km/h.</param>
  /// <param name="visibility">The visibility in kilometers.</param>
  /// <param name="atmosphericPressure">The atmospheric pressure in hPa.</param>
  public sealed class WeatherState(
    Temperature temperature,
    Humidity humidity,
    WeatherType weatherType,
    double windSpeed,
    double visibility,
    double atmosphericPressure
  ) : IEquatable<WeatherState>
  {
    /// <summary>
    /// Gets the current temperature.
    /// </summary>
    public Temperature Temperature { get; } =
      temperature ?? throw new ArgumentNullException(nameof(temperature));

    /// <summary>
    /// Gets the current humidity.
    /// </summary>
    public Humidity Humidity { get; } =
      humidity ?? throw new ArgumentNullException(nameof(humidity));

    /// <summary>
    /// Gets the current weather type.
    /// </summary>
    public WeatherType WeatherType { get; } = weatherType;

    /// <summary>
    /// Gets the current wind speed in km/h.
    /// </summary>
    public double WindSpeed { get; } = Math.Max(0, windSpeed);

    /// <summary>
    /// Gets the current visibility in kilometers.
    /// </summary>
    public double Visibility { get; } = Math.Max(0, visibility);

    /// <summary>
    /// Gets the current atmospheric pressure in hPa.
    /// </summary>
    public double AtmosphericPressure { get; } = atmosphericPressure;

    /// <summary>
    /// Gets the weather severity index (0.0 = calm, 1.0 = extreme).
    /// </summary>
    public double SeverityIndex => CalculateSeverityIndex();

    /// <summary>
    /// Gets a value indicating whether conditions are favorable for bee foraging.
    /// </summary>
    public bool IsFavorableForForaging => Temperature.IsOptimalForForaging() &&
                                          WeatherType != WeatherType.HeavyRain &&
                                          WeatherType != WeatherType.Storm &&
                                          WeatherType != WeatherType.Snow &&
                                          WindSpeed < 25.0 &&
                                          Visibility > 2.0;

    /// <summary>
    /// Gets a value indicating whether conditions allow any bee activity.
    /// </summary>
    public bool IsSuitableForBeeActivity => Temperature.IsSuitableForBeeActivity() &&
                                            WeatherType != WeatherType.Storm &&
                                            WeatherType != WeatherType.Snow &&
                                            WindSpeed < 40.0;

    /// <summary>
    /// Creates a default weather state suitable for bee activity.
    /// </summary>
    /// <returns>A default weather state.</returns>
    public static WeatherState CreateDefault()
    {
      return new(
        new Temperature(22.0),
        new Humidity(55.0),
        WeatherType.Clear,
        8.0,
        15.0,
        1013.25
      );
    }

    /// <summary>
    /// Calculates the impact of current weather on bee foraging efficiency.
    /// </summary>
    /// <returns>Foraging efficiency modifier (0.0 to 1.0).</returns>
    public double GetForagingEfficiencyModifier()
    {
      if (!IsSuitableForBeeActivity) return 0.0;

      double tempModifier = Temperature.IsOptimalForForaging() ? 1.0 : 0.5;

      double weatherModifier = WeatherType switch
      {
        WeatherType.Clear => 1.0,
        WeatherType.PartlyCloudy => 0.85,
        WeatherType.Overcast => 0.6,
        WeatherType.LightRain => 0.3,
        WeatherType.Windy => 0.7,
        _ => 0.1
      };

      double windModifier = WindSpeed switch
      {
        < 10 => 1.0,
        < 20 => 0.8,
        < 30 => 0.5,
        _ => 0.2
      };

      double visibilityModifier = Visibility switch
      {
        >= 10 => 1.0,
        >= 5 => 0.8,
        >= 2 => 0.6,
        _ => 0.3
      };

      return tempModifier * weatherModifier * windModifier * visibilityModifier;
    }

    /// <summary>
    /// Calculates the overall weather severity.
    /// </summary>
    /// <returns>Severity index from 0.0 to 1.0.</returns>
    private double CalculateSeverityIndex()
    {
      double tempSeverity = Temperature.Celsius switch
      {
        < 0 => 0.8,
        < 5 => 0.6,
        < 10 => 0.4,
        > 35 => 0.6,
        > 40 => 0.8,
        _ => 0.0
      };

      double weatherSeverity = WeatherType switch
      {
        WeatherType.Storm => 1.0,
        WeatherType.HeavyRain => 0.8,
        WeatherType.Snow => 0.7,
        WeatherType.LightRain => 0.3,
        WeatherType.Windy => 0.4,
        WeatherType.Overcast => 0.2,
        _ => 0.0
      };

      double windSeverity = WindSpeed switch
      {
        > 50 => 1.0,
        > 35 => 0.8,
        > 25 => 0.6,
        > 15 => 0.3,
        _ => 0.0
      };

      return Math.Max(tempSeverity, Math.Max(weatherSeverity, windSeverity));
    }

    /// <summary>
    /// Determines equality with another weather state.
    /// </summary>
    /// <param name="other">Other weather state to compare.</param>
    /// <returns>True if weather states are equal.</returns>
    public bool Equals(WeatherState? other)
    {
      if (other is null) return false;
      if (ReferenceEquals(this, other)) return true;

      return Temperature.Equals(other.Temperature) &&
             Humidity.Equals(other.Humidity) &&
             WeatherType == other.WeatherType &&
             Math.Abs(WindSpeed - other.WindSpeed) < 0.1 &&
             Math.Abs(Visibility - other.Visibility) < 0.1 &&
             Math.Abs(AtmosphericPressure - other.AtmosphericPressure) < 0.1;
    }

    /// <summary>
    /// Determines equality with another object.
    /// </summary>
    /// <param name="obj">Object to compare.</param>
    /// <returns>True if objects are equal.</returns>
    public override bool Equals(object? obj) => obj is WeatherState other && Equals(other);

    /// <summary>
    /// Gets the hash code for this weather state.
    /// </summary>
    /// <returns>Hash code.</returns>
    public override int GetHashCode() =>
      HashCode.Combine(Temperature, Humidity, WeatherType, WindSpeed, Visibility, AtmosphericPressure);

    /// <summary>
    /// Returns a string representation of the weather state.
    /// </summary>
    /// <returns>String representation.</returns>
    public override string ToString() =>
      $"{WeatherType}: {Temperature}, {Humidity}, Wind: {WindSpeed:F1} km/h, Visibility: {Visibility:F1} km";
  }
}
