namespace HiveMind.Core.Enums
{
  /// <summary>
  /// Defines different weather conditions that affect bee behavior and colony activities.
  /// </summary>
  public enum WeatherType
  {
    /// <summary>
    /// Clear, sunny weather - optimal for foraging activities.
    /// </summary>
    Clear = 1,

    /// <summary>
    /// Partially cloudy conditions - good for foraging with some limitations.
    /// </summary>
    PartlyCloudy = 2,

    /// <summary>
    /// Overcast conditions - reduced foraging efficiency.
    /// </summary>
    Overcast = 3,

    /// <summary>
    /// Light rain - significantly reduces foraging activities.
    /// </summary>
    LightRain = 4,

    /// <summary>
    /// Heavy rain - stops most outdoor activities.
    /// </summary>
    HeavyRain = 5,

    /// <summary>
    /// Storm conditions - dangerous for bees, all outdoor activities cease.
    /// </summary>
    Storm = 6,

    /// <summary>
    /// Snow conditions - no foraging, colony in survival mode.
    /// </summary>
    Snow = 7,

    /// <summary>
    /// Windy conditions - affects flight patterns and foraging efficiency.
    /// </summary>
    Windy = 8
  }
}
