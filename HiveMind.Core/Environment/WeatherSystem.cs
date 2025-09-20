using HiveMind.Core.Enums;
using HiveMind.Core.ValueObject;
using Microsoft.Extensions.Logging;

namespace HiveMind.Core.Environment
{
  /// <summary>
  /// Advanced weather system that generates realistic weather patterns and transitions.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="WeatherSystem"/> class.
  /// </remarks>
  /// <param name="logger">The logger instance.</param>
  /// <param name="initialWeather">The initial weather state.</param>
  public sealed class WeatherSystem(ILogger<WeatherSystem> logger, WeatherState? initialWeather = null)
  {
    private readonly ILogger<WeatherSystem> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Random _random = new();
    private WeatherState _currentState = initialWeather ?? WeatherState.CreateDefault();
    private DateTime _lastWeatherChange = DateTime.UtcNow;
    private readonly List<WeatherPattern> _seasonalPatterns = InitializeSeasonalPatterns();

    /// <summary>
    /// Gets the current weather state.
    /// </summary>
    public WeatherState CurrentState => _currentState;

    /// <summary>
    /// Gets the time of the last weather change.
    /// </summary>
    public DateTime LastWeatherChange => _lastWeatherChange;

    /// <summary>
    /// Updates the weather system based on the current time and season.
    /// </summary>
    /// <param name="currentTime">The current simulation time.</param>
    /// <param name="season">The current season.</param>
    /// <returns>True if weather conditions changed; otherwise, false.</returns>
    public bool UpdateWeather(DateTime currentTime, Season season)
    {
      TimeSpan timeSinceLastChange = currentTime - _lastWeatherChange;
      WeatherPattern pattern = _seasonalPatterns.First(p => p.Season == season);

      // Check if weather should change based on pattern stability
      double changeChance = CalculateWeatherChangeChance(timeSinceLastChange, pattern);

      if (_random.NextDouble() < changeChance)
      {
        WeatherState newWeatherState = GenerateNewWeatherState(season, pattern);

        if (!newWeatherState.Equals(_currentState))
        {
          WeatherState previousWeather = _currentState;
          _currentState = newWeatherState;
          _lastWeatherChange = currentTime;

          _logger.LogInformation(
            "Weather changed from {OldWeather} to {NewWeather} (Season: {Season})",
             previousWeather.WeatherType,
             _currentState.WeatherType, season
           );

          return true;
        }
      }

      // Gradual temperature and humidity adjustments
      ApplyGradualChanges(season);

      return false;
    }

    /// <summary>
    /// Calculates the probability of weather change based on time and seasonal patterns.
    /// </summary>
    /// <param name="timeSinceLastChange">Time since the last weather change.</param>
    /// <param name="pattern">The current seasonal pattern.</param>
    /// <returns>Probability of weather change (0.0 to 1.0).</returns>
    private double CalculateWeatherChangeChance(TimeSpan timeSinceLastChange, WeatherPattern pattern)
    {
      double hoursStable = timeSinceLastChange.TotalHours;
      double baseChance = 0.05; // 5% base chance per hour

      // Weather becomes more likely to change the longer it's been stable
      double stabilityFactor = Math.Min(hoursStable / pattern.AverageStabilityHours, 2.0);

      // Current weather affects change probability
      double weatherStabilityFactor = _currentState.WeatherType switch
      {
        WeatherType.Clear => 0.8,     // Clear weather is relatively stable
        WeatherType.Storm => 2.0,     // Storms change quickly
        WeatherType.HeavyRain => 1.5, // Heavy rain doesn't last long
        WeatherType.LightRain => 0.9, // Light rain is moderately stable
        WeatherType.Snow => 0.6,      // Snow can persist
        _ => 1.0
      };

      return baseChance * stabilityFactor * weatherStabilityFactor * pattern.VolatilityFactor;
    }

    /// <summary>
    /// Generates a new weather state based on seasonal patterns.
    /// </summary>
    /// <param name="season">The current season.</param>
    /// <param name="pattern">The seasonal weather pattern.</param>
    /// <returns>A new weather state.</returns>
    private WeatherState GenerateNewWeatherState(Season season, WeatherPattern pattern)
    {
      WeatherType newWeatherType = SelectWeatherTypeFromPattern(pattern);
      double baseTemperature = GenerateSeasonalTemperature(season);
      double baseHumidity = GenerateWeatherHumidity(newWeatherType);
      double windSpeed = GenerateWindSpeed(newWeatherType);

      // Apply weather-specific temperature modifiers
      double temperatureModifier = newWeatherType switch
      {
        WeatherType.Clear => _random.NextDouble() * 4 - 2,          // ±2°C
        WeatherType.PartlyCloudy => _random.NextDouble() * 3 - 1.5, // ±1.5°C
        WeatherType.Overcast => _random.NextDouble() * 2 - 3,       // -3 to -1°C
        WeatherType.LightRain => _random.NextDouble() * 2 - 4,      // -4 to -2°C
        WeatherType.HeavyRain => _random.NextDouble() * 3 - 6,      // -6 to -3°C
        WeatherType.Storm => _random.NextDouble() * 4 - 8,          // -8 to -4°C
        WeatherType.Snow => _random.NextDouble() * 2 - 12,          // -12 to -10°C
        WeatherType.Windy => _random.NextDouble() * 2 - 1,          // ±1°C
        _ => 0
      };

      double finalTemperature = Math.Max(-30, Math.Min(45, baseTemperature + temperatureModifier));
      double finalHumidity = Math.Max(10, Math.Min(100, baseHumidity + (_random.NextDouble() * 20 - 10)));

      return new(
        new Temperature(finalTemperature),
        new Humidity(finalHumidity),
        newWeatherType,
        windSpeed,
        CalculateVisibility(newWeatherType),
        CalculatePressure(newWeatherType)
      );
    }

    /// <summary>
    /// Selects a weather type from the seasonal pattern probabilities.
    /// </summary>
    /// <param name="pattern">The seasonal pattern.</param>
    /// <returns>Selected weather type.</returns>
    private WeatherType SelectWeatherTypeFromPattern(WeatherPattern pattern)
    {
      double random = _random.NextDouble();
      double cumulative = 0.0;

      foreach (var (weatherType, probability) in pattern.WeatherProbabilities)
      {
        cumulative += probability;
        if (random <= cumulative)
          return weatherType;
      }

      return WeatherType.Clear; // Fallback
    }

    /// <summary>
    /// Generates seasonal base temperature.
    /// </summary>
    /// <param name="season">The current season.</param>
    /// <returns>Base temperature for the season.</returns>
    private double GenerateSeasonalTemperature(Season season)
    {
      var (min, max) = season switch
      {
        Season.Winter => (-5.0, 8.0),
        Season.Spring => (8.0, 22.0),
        Season.Summer => (18.0, 35.0),
        Season.Autumn => (5.0, 20.0),
        _ => (15.0, 25.0)
      };

      return min + _random.NextDouble() * (max - min);
    }

    /// <summary>
    /// Generates humidity based on weather type.
    /// </summary>
    /// <param name="weatherType">The weather type.</param>
    /// <returns>Humidity percentage.</returns>
    private double GenerateWeatherHumidity(WeatherType weatherType) => weatherType switch
    {
      WeatherType.Clear => 40 + _random.NextDouble() * 25,        // 40-65%
      WeatherType.PartlyCloudy => 50 + _random.NextDouble() * 30, // 50-80%
      WeatherType.Overcast => 65 + _random.NextDouble() * 25,     // 65-90%
      WeatherType.LightRain => 80 + _random.NextDouble() * 15,    // 80-95%
      WeatherType.HeavyRain => 85 + _random.NextDouble() * 15,    // 85-100%
      WeatherType.Storm => 85 + _random.NextDouble() * 15,        // 85-100%
      WeatherType.Snow => 75 + _random.NextDouble() * 20,         // 75-95%
      WeatherType.Windy => 35 + _random.NextDouble() * 40,        // 35-75%
      _ => 55
    };

    /// <summary>
    /// Generates wind speed based on weather type.
    /// </summary>
    /// <param name="weatherType">The weather type.</param>
    /// <returns>Wind speed in km/h.</returns>
    private double GenerateWindSpeed(WeatherType weatherType) => weatherType switch
    {
      WeatherType.Clear => 2 + _random.NextDouble() * 8,         // 2-10 km/h
      WeatherType.PartlyCloudy => 5 + _random.NextDouble() * 10, // 5-15 km/h
      WeatherType.Overcast => 8 + _random.NextDouble() * 12,     // 8-20 km/h
      WeatherType.LightRain => 10 + _random.NextDouble() * 15,   // 10-25 km/h
      WeatherType.HeavyRain => 15 + _random.NextDouble() * 20,   // 15-35 km/h
      WeatherType.Storm => 40 + _random.NextDouble() * 40,       // 40-80 km/h
      WeatherType.Snow => 5 + _random.NextDouble() * 15,         // 5-20 km/h
      WeatherType.Windy => 25 + _random.NextDouble() * 25,       // 25-50 km/h
      _ => 10
    };

    /// <summary>
    /// Calculates visibility based on weather conditions.
    /// </summary>
    /// <param name="weatherType">The weather type.</param>
    /// <returns>Visibility in kilometers.</returns>
    private double CalculateVisibility(WeatherType weatherType) => weatherType switch
    {
      WeatherType.Clear => 15 + _random.NextDouble() * 10,        // 15-25 km
      WeatherType.PartlyCloudy => 10 + _random.NextDouble() * 10, // 10-20 km
      WeatherType.Overcast => 8 + _random.NextDouble() * 7,       // 8-15 km
      WeatherType.LightRain => 3 + _random.NextDouble() * 5,      // 3-8 km
      WeatherType.HeavyRain => 1 + _random.NextDouble() * 3,      // 1-4 km
      WeatherType.Storm => 0.5 + _random.NextDouble() * 2,        // 0.5-2.5 km
      WeatherType.Snow => 1 + _random.NextDouble() * 4,           // 1-5 km
      WeatherType.Windy => 8 + _random.NextDouble() * 12,         // 8-20 km
      _ => 10
    };

    /// <summary>
    /// Calculates atmospheric pressure based on weather type.
    /// </summary>
    /// <param name="weatherType">The weather type.</param>
    /// <returns>Atmospheric pressure in hPa.</returns>
    private double CalculatePressure(WeatherType weatherType)
    {
      double basePressure = 1013.25; // Standard atmospheric pressure

      double modifier = weatherType switch
      {
        WeatherType.Clear => _random.NextDouble() * 20 + 5,      // +5 to +25 hPa
        WeatherType.PartlyCloudy => _random.NextDouble() * 15,   // 0 to +15 hPa
        WeatherType.Overcast => _random.NextDouble() * 10 - 5,   // -5 to +5 hPa
        WeatherType.LightRain => _random.NextDouble() * 15 - 10, // -10 to +5 hPa
        WeatherType.HeavyRain => _random.NextDouble() * 20 - 15, // -15 to +5 hPa
        WeatherType.Storm => _random.NextDouble() * 25 - 25,     // -25 to 0 hPa
        WeatherType.Snow => _random.NextDouble() * 15 - 10,      // -10 to +5 hPa
        WeatherType.Windy => _random.NextDouble() * 20 - 10,     // -10 to +10 hPa
        _ => 0
      };

      return basePressure + modifier;
    }

    /// <summary>
    /// Applies gradual changes to temperature and humidity.
    /// </summary>
    /// <param name="season">The current season.</param>
    private void ApplyGradualChanges(Season season)
    {
      // Small random fluctuations to make weather feel more natural
      double tempChange = (_random.NextDouble() - 0.5) * 0.2; // ±0.1°C per update
      double humidityChange = (_random.NextDouble() - 0.5) * 0.4; // ±0.2% per update

      try
      {
        double newTemp = Math.Max(-30, Math.Min(45, _currentState.Temperature.Celsius + tempChange));
        double newHumidity = Math.Max(10, Math.Min(100, _currentState.Humidity.Percentage + humidityChange));

        _currentState = new(
          temperature: new(newTemp),
          humidity: new(newHumidity),
          _currentState.WeatherType,
          _currentState.WindSpeed,
          _currentState.Visibility,
          _currentState.AtmosphericPressure
        );
      }
      catch (ArgumentOutOfRangeException)
      {
        // Ignore invalid gradual changes
      }
    }

    /// <summary>
    /// Initializes seasonal weather patterns.
    /// </summary>
    /// <returns>List of seasonal weather patterns.</returns>
    private static List<WeatherPattern> InitializeSeasonalPatterns()
    {
      return
      [
        new()
        {
          Season = Season.Winter,
          AverageStabilityHours = 8,
          VolatilityFactor = 0.7,
          WeatherProbabilities = new()
          {
            { WeatherType.Clear, 0.20 },
            { WeatherType.PartlyCloudy, 0.25 },
            { WeatherType.Overcast, 0.30 },
            { WeatherType.LightRain, 0.10 },
            { WeatherType.HeavyRain, 0.05 },
            { WeatherType.Snow, 0.08 },
            { WeatherType.Windy, 0.02 }
          }
        },
        new()
        {
          Season = Season.Spring,
          AverageStabilityHours = 6,
          VolatilityFactor = 1.2,
          WeatherProbabilities = new()
          {
            { WeatherType.Clear, 0.35 },
            { WeatherType.PartlyCloudy, 0.30 },
            { WeatherType.Overcast, 0.15 },
            { WeatherType.LightRain, 0.12 },
            { WeatherType.HeavyRain, 0.05 },
            { WeatherType.Storm, 0.02 },
            { WeatherType.Windy, 0.01 }
          }
        },
        new()
        {
          Season = Season.Summer,
          AverageStabilityHours = 12,
          VolatilityFactor = 0.8,
          WeatherProbabilities = new()
          {
            { WeatherType.Clear, 0.55 },
            { WeatherType.PartlyCloudy, 0.25 },
            { WeatherType.Overcast, 0.10 },
            { WeatherType.LightRain, 0.05 },
            { WeatherType.HeavyRain, 0.02 },
            { WeatherType.Storm, 0.02 },
            { WeatherType.Windy, 0.01 }
          }
        },
        new()
        {
          Season = Season.Autumn,
          AverageStabilityHours = 8,
          VolatilityFactor = 1.0,
          WeatherProbabilities = new()
          {
            { WeatherType.Clear, 0.25 },
            { WeatherType.PartlyCloudy, 0.30 },
            { WeatherType.Overcast, 0.25 },
            { WeatherType.LightRain, 0.12 },
            { WeatherType.HeavyRain, 0.05 },
            { WeatherType.Storm, 0.02 },
            { WeatherType.Windy, 0.01 }
          }
        }
      ];
    }
  }

  /// <summary>
  /// Represents seasonal weather patterns and probabilities.
  /// </summary>
  public sealed class WeatherPattern
  {
    /// <summary>
    /// Gets or sets the season this pattern applies to.
    /// </summary>
    public Season Season { get; set; }

    /// <summary>
    /// Gets or sets the average number of hours weather conditions remain stable.
    /// </summary>
    public double AverageStabilityHours { get; set; }

    /// <summary>
    /// Gets or sets the volatility factor affecting weather change frequency.
    /// </summary>
    public double VolatilityFactor { get; set; }

    /// <summary>
    /// Gets or sets the probability distribution of weather types for this season.
    /// </summary>
    public Dictionary<WeatherType, double> WeatherProbabilities { get; set; } = [];
  }
}
