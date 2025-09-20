using HiveMind.Core.Enums;

namespace HiveMind.Application.Configuration
{
  /// <summary>
  /// Configuration settings for the HiveMind simulation.
  /// </summary>
  public sealed class SimulationConfiguration
  {
    /// <summary>
    /// Gets or sets the interval between simulation ticks in milliseconds.
    /// </summary>
    public int TickIntervalMilliseconds { get; set; } = 100;

    /// <summary>
    /// Gets or sets the interval between automatic saves in minutes.
    /// </summary>
    public int AutoSaveIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of bees allowed in the simulation.
    /// </summary>
    public int MaxBeeCount { get; set; } = 500;

    /// <summary>
    /// Gets or sets a value indicating whether detailed logging is enabled.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of concurrent processing threads.
    /// </summary>
    public int MaxConcurrentThreads { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the simulation speed multiplier.
    /// </summary>
    public double SimulationSpeedMultiplier { get; set; } = 1.0;
  }

  /// <summary>
  /// Configuration settings for the simulation environment.
  /// </summary>
  public sealed class EnvironmentConfiguration
  {
    /// <summary>
    /// Gets or sets the initial temperature in Celsius.
    /// </summary>
    public double InitialTemperatureCelsius { get; set; } = 25.0;

    /// <summary>
    /// Gets or sets the initial humidity percentage.
    /// </summary>
    public double InitialHumidityPercent { get; set; } = 60.0;

    /// <summary>
    /// Gets or sets the frequency of weather changes in hours.
    /// </summary>
    public double WeatherChangeFrequencyHours { get; set; } = 6.0;

    /// <summary>
    /// Gets or sets the initial season.
    /// </summary>
    public Season InitialSeason { get; set; } = Season.Spring;
  }
}
