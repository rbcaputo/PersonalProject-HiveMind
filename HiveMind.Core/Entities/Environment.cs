using HiveMind.Core.Common;
using HiveMind.Core.Enums;
using HiveMind.Core.ValueObject;

namespace HiveMind.Core.Entities
{
  /// <summary>
  /// Represents the environmental conditions affecting the bee colony.
  /// Includes weather, temperature, humidity, and seasonal changes.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="Environment"/> class.
  /// </remarks>
  /// <param name="initialTemperature">The initial temperature.</param>
  /// <param name="initialHumidity">The initial humidity.</param>
  /// <param name="initialWeather">The initial weather type.</param>
  /// <param name="initialSeason">The initial season.</param>
  public sealed class Environment(
    Temperature initialTemperature,
    Humidity initialHumidity,
    WeatherType initialWeather,
    Season initialSeason
  ) : Entity
  {
    private Temperature _temperature = initialTemperature ?? throw new ArgumentNullException(nameof(initialTemperature));
    private Humidity _humidity = initialHumidity ?? throw new ArgumentNullException(nameof(initialHumidity));
    private WeatherType _weather = initialWeather;
    private Season _season = initialSeason;
    private double _windSpeed = 5.0;
    private DateTime _lastWeatherChange = DateTime.UtcNow;
    private readonly Random _random = new();

    /// <summary>
    /// Gets the current temperature.
    /// </summary>
    public Temperature Temperature => _temperature;

    /// <summary>
    /// Gets the current humidity level.
    /// </summary>
    public Humidity Humidity => _humidity;

    /// <summary>
    /// Gets the current weather type.
    /// </summary>
    public WeatherType Weather => _weather;

    /// <summary>
    /// Gets the current season.
    /// </summary>
    public Season Season => _season;

    /// <summary>
    /// Gets the current wind speed in km/h.
    /// </summary>
    public double WindSpeed => _windSpeed;

    /// <summary>
    /// Gets the time when weather conditions last changed.
    /// </summary>
    public DateTime LastWeatherChange => _lastWeatherChange;

    /// <summary>
    /// Gets a value indicating whether conditions are favorable for bee foraging.
    /// </summary>
    public bool IsFavorableForForaging => _temperature.IsOptimalForForaging() &&
                                          _weather != WeatherType.HeavyRain &&
                                          _weather != WeatherType.Storm &&
                                          _weather != WeatherType.Snow &&
                                          _windSpeed < 25.0; // Less than 25 km/h wind

    /// <summary>
    /// Gets a value indicating whether conditions are suitable for any bee activity.
    /// </summary>
    public bool IsSuitableForBeeActivity => _temperature.IsSuitableForBeeActivity() &&
                                            _weather != WeatherType.Storm &&
                                            _weather != WeatherType.Snow;

    /// <summary>
    /// Updates environmental conditions based on time passage and random factors.
    /// </summary>
    /// <param name="simulationTime">Current simulation time.</param>
    public void UpdateConditions(DateTime simulationTime)
    {
      UpdateSeason(simulationTime);
      UpdateWeather(simulationTime);
      UpdateTemperature();
      UpdateHumidity();
      UpdateWindSpeed();
    }

    /// <summary>
    /// Updates the current season based on simulation time.
    /// </summary>
    /// <param name="simulationTime">Current simulation time.</param>
    private void UpdateSeason(DateTime simulationTime) =>
      // Simple season calculation based on month
      _season = simulationTime.Month switch
      {
        12 or 1 or 2 => Season.Winter,
        3 or 4 or 5 => Season.Spring,
        6 or 7 or 8 => Season.Summer,
        9 or 10 or 11 => Season.Autumn,
        _ => Season.Spring
      };

    /// <summary>
    /// Updates weather conditions with random changes and seasonal influences.
    /// </summary>
    /// <param name="simulationTime">Current simulation time.</param>
    private void UpdateWeather(DateTime simulationTime)
    {
      TimeSpan timeSinceLastChange = simulationTime - _lastWeatherChange;

      // Weather changes every 2-8 hours on average
      if (timeSinceLastChange.TotalHours < 2) return;

      double changeChance = Math.Min(timeSinceLastChange.TotalHours / 6.0, 1.0);
      if (_random.NextDouble() > changeChance) return;

      WeatherType previousWeather = _weather;
      _weather = GenerateNewWeather(_season, previousWeather);
      _lastWeatherChange = simulationTime;
    }

    /// <summary>
    /// Generates new weather based on season and previous weather.
    /// </summary>
    /// <param name="season">Current season.</param>
    /// <param name="previousWeather">Previous weather condition.</param>
    /// <returns>New weather type.</returns>
    private WeatherType GenerateNewWeather(Season season, WeatherType previousWeather)
    {
      // Weather probabilities based on season
      Dictionary<WeatherType, double> weatherWeights = season switch
      {
        Season.Winter => new()
        {
          { WeatherType.Clear, 0.2 },
          { WeatherType.PartlyCloudy, 0.3 },
          { WeatherType.Overcast, 0.3 },
          { WeatherType.LightRain, 0.1 },
          { WeatherType.HeavyRain, 0.05 },
          { WeatherType.Snow, 0.05 }
         },
        Season.Spring => new()
        {
          { WeatherType.Clear, 0.4 },
          { WeatherType.PartlyCloudy, 0.3 },
          { WeatherType.Overcast, 0.15 },
          { WeatherType.LightRain, 0.1 },
          { WeatherType.HeavyRain, 0.04 },
          { WeatherType.Storm, 0.01 }
        },
        Season.Summer => new()
        {
          { WeatherType.Clear, 0.6 },
          { WeatherType.PartlyCloudy, 0.25 },
          { WeatherType.Overcast, 0.1 },
          { WeatherType.LightRain, 0.03 },
          { WeatherType.HeavyRain, 0.01 },
          { WeatherType.Storm, 0.01 }
        },
        Season.Autumn => new()
        {
          { WeatherType.Clear, 0.3 },
          { WeatherType.PartlyCloudy, 0.3 },
          { WeatherType.Overcast, 0.25 },
          { WeatherType.LightRain, 0.1 },
          { WeatherType.HeavyRain, 0.04 },
          { WeatherType.Storm, 0.01 }
        },
        _ => throw new ArgumentException($"Unknown season: {season}")
      };

      return SelectWeatherByWeight(weatherWeights);
    }

    /// <summary>
    /// Selects weather type based on weighted probabilities.
    /// </summary>
    /// <param name="weights">Weather type weights.</param>
    /// <returns>Selected weather type.</returns>
    private WeatherType SelectWeatherByWeight(Dictionary<WeatherType, double> weights)
    {
      double random = _random.NextDouble();
      double cumulative = 0.0;

      foreach (var (weatherType, weight) in weights)
      {
        cumulative += weight;
        if (random <= cumulative)
          return weatherType;
      }

      return WeatherType.Clear; // Fallback
    }

    /// <summary>
    /// Updates temperature based on season and weather.
    /// </summary>
    private void UpdateTemperature()
    {
      double baseTemp = _season switch
      {
        Season.Winter => -2.0 + _random.NextDouble() * 12.0, // -2 to 10°C
        Season.Spring => 10.0 + _random.NextDouble() * 15.0, // 10 to 25°C
        Season.Summer => 20.0 + _random.NextDouble() * 15.0, // 20 to 35°C
        Season.Autumn => 5.0 + _random.NextDouble() * 20.0,  // 5 to 25°C
        _ => 15.0
      };

      // Weather modifies temperature
      double weatherModifier = _weather switch
      {
        WeatherType.Clear => 2.0,
        WeatherType.PartlyCloudy => 0.0,
        WeatherType.Overcast => -2.0,
        WeatherType.LightRain => -3.0,
        WeatherType.HeavyRain => -5.0,
        WeatherType.Storm => -7.0,
        WeatherType.Snow => -10.0,
        WeatherType.Windy => -1.0,
        _ => 0.0
      };

      double newTemp = baseTemp + weatherModifier + (_random.NextDouble() - 0.5) * 4.0;
      _temperature = new(Math.Max(-20.0, Math.Min(45.0, newTemp)));
    }

    /// <summary>
    /// Updates humidity based on weather conditions.
    /// </summary>
    private void UpdateHumidity()
    {
      double baseHumidity = _weather switch
      {
        WeatherType.Clear => 45.0 + _random.NextDouble() * 20.0,        // 45-65%
        WeatherType.PartlyCloudy => 55.0 + _random.NextDouble() * 25.0, // 55-80%
        WeatherType.Overcast => 70.0 + _random.NextDouble() * 20.0,     // 70-90%
        WeatherType.LightRain => 85.0 + _random.NextDouble() * 10.0,    // 85-95%
        WeatherType.HeavyRain => 90.0 + _random.NextDouble() * 10.0,    // 90-100%
        WeatherType.Storm => 90.0 + _random.NextDouble() * 10.0,        // 90-100%
        WeatherType.Snow => 80.0 + _random.NextDouble() * 15.0,         // 80-95%
        WeatherType.Windy => 40.0 + _random.NextDouble() * 30.0,        // 40-70%
        _ => 60.0
      };

      _humidity = new(Math.Max(20.0, Math.Min(100.0, baseHumidity)));
    }

    /// <summary>
    /// Updates wind speed based on weather conditions.
    /// </summary>
    private void UpdateWindSpeed()
    {
      _windSpeed = _weather switch
      {
        WeatherType.Clear => 2.0 + _random.NextDouble() * 8.0,         // 2-10 km/h
        WeatherType.PartlyCloudy => 5.0 + _random.NextDouble() * 10.0, // 5-15 km/h
        WeatherType.Overcast => 8.0 + _random.NextDouble() * 12.0,     // 8-20 km/h
        WeatherType.LightRain => 10.0 + _random.NextDouble() * 15.0,   // 10-25 km/h
        WeatherType.HeavyRain => 15.0 + _random.NextDouble() * 20.0,   // 15-35 km/h
        WeatherType.Storm => 40.0 + _random.NextDouble() * 40.0,       // 40-80 km/h
        WeatherType.Snow => 5.0 + _random.NextDouble() * 15.0,         // 5-20 km/h
        WeatherType.Windy => 25.0 + _random.NextDouble() * 25.0,       // 25-50 km/h
        _ => 10.0
      };
    }

    /// <summary>
    /// Creates a default environment suitable for bee colony simulation.
    /// </summary>
    /// <returns>A new environment with default conditions.</returns>
    public static Environment CreateDefault() => new(
      initialTemperature: new(22.0), // 22°C
      initialHumidity: new(55.0),    // 55% humidity
      WeatherType.Clear,
      Season.Spring
    );
  }
}
