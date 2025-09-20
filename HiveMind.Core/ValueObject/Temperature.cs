namespace HiveMind.Core.ValueObject
{
  /// <summary>
  /// Represents a temperature value with validation and conversion capabilities.
  /// </summary>
  public sealed class Temperature : Common.ValueObject
  {
    /// <summary>
    /// Gets the temperature value in Celsius.
    /// </summary>
    public double Celsius { get; }

    /// <summary>
    /// Gets the temperature value in Fahrenheit.
    /// </summary>
    public double Fahrenheit => (Celsius * 9.0 / 5.0) + 32.0;

    /// <summary>
    /// Gets the temperature value in Kelvin.
    /// </summary>
    public double Kelvin => Celsius + 273.15;

    /// <summary>
    /// Minimum valid temperature in Celsius (absolute zero).
    /// </summary>
    public const double MinTemperatureCelsius = -273.15;

    /// <summary>
    /// Maximum reasonable temperature for bee simulation in Celsius.
    /// </summary>
    public const double MaxTemperatureCelsius = 60.0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Temperature"/> class.
    /// </summary>
    /// <param name="celsius">The temperature in Celsius.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when temperature is outside valid range.</exception>
    public Temperature(double celsius)
    {
      if (celsius < MinTemperatureCelsius || celsius > MaxTemperatureCelsius)
        throw new ArgumentOutOfRangeException(
          nameof(celsius),
          celsius,
          $"Temperature must be between {MinTemperatureCelsius}°C and {MaxTemperatureCelsius}°C."
        );

      Celsius = celsius;
    }

    /// <summary>
    /// Creates a Temperature instance from a Fahrenheit value.
    /// </summary>
    /// <param name="fahrenheit">The temperature in Fahrenheit.</param>
    /// <returns>A new Temperature instance.</returns>
    public static Temperature FromFahrenheit(double fahrenheit)
    {
      double celsius = (fahrenheit - 32.0) * 5.0 / 9.0;
      return new(celsius);
    }

    /// <summary>
    /// Creates a Temperature instance from a Kelvin value.
    /// </summary>
    /// <param name="kelvin">The temperature in Kelvin.</param>
    /// <returns>A new Temperature instance.</returns>
    public static Temperature FromKelvin(double kelvin)
    {
      double celsius = kelvin - 273.15;
      return new(celsius);
    }

    /// <summary>
    /// Determines if the temperature is suitable for bee activity.
    /// Bees are most active between 13°C and 38°C.
    /// </summary>
    /// <returns>True if temperature supports bee activity.</returns>
    public bool IsSuitableForBeeActivity() =>
      Celsius >= 13.0 && Celsius <= 38.0;

    /// <summary>
    /// Determines if the temperature is optimal for bee foraging.
    /// Optimal foraging occurs between 18°C and 35°C.
    /// </summary>
    /// <returns>True if temperature is optimal for foraging.</returns>
    public bool IsOptimalForForaging() =>
      Celsius >= 18.0 && Celsius <= 35.0;

    /// <summary>
    /// Gets the equality components for value object comparison.
    /// </summary>
    /// <returns>The components that define equality.</returns>
    protected override IEnumerable<object> GetEqualityComponents()
    {
      yield return Math.Round(Celsius, 2);
    }

    /// <summary>
    /// Returns a string representation of the temperature.
    /// </summary>
    /// <returns>A string showing temperature in Celsius.</returns>
    public override string ToString() => $"{Celsius:F1}°C";
  }
}
