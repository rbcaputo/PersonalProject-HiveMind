namespace HiveMind.Core.ValueObject
{
  /// <summary>
  /// Represents a humidity percentage value with validation.
  /// </summary>
  public sealed class Humidity : Common.ValueObject
  {
    /// <summary>
    /// Gets the humidity percentage (0-100).
    /// </summary>
    public double Percentage { get; }

    /// <summary>
    /// Minimum valid humidity percentage.
    /// </summary>
    public const double MinHumidity = 0.0;

    /// <summary>
    /// Maximum valid humidity percentage.
    /// </summary>
    public const double MaxHumidity = 100.0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Humidity"/> class.
    /// </summary>
    /// <param name="percentage">The humidity percentage (0-100).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when humidity is outside valid range.</exception>
    public Humidity(double percentage)
    {
      if (percentage < MinHumidity || percentage > MaxHumidity)
        throw new ArgumentOutOfRangeException(
          nameof(percentage),
          percentage,
          $"Humidity must be between {MinHumidity}% and {MaxHumidity}%."
        );

      Percentage = percentage;
    }

    /// <summary>
    /// Determines if the humidity level is suitable for bee activity.
    /// Bees prefer humidity between 50% and 60% inside the hive.
    /// </summary>
    /// <returns>True if humidity supports bee activity.</returns>
    public bool IsSuitableForBees() =>
      Percentage >= 40.0 && Percentage <= 70.0;

    /// <summary>
    /// Determines if the humidity level is optimal for honey production.
    /// Optimal honey production occurs at 50-60% humidity.
    /// </summary>
    /// <returns>True if humidity is optimal for honey production.</returns>
    public bool IsOptimalForHoneyProduction() =>
      Percentage >= 50.0 && Percentage <= 60.0;

    /// <summary>
    /// Gets the equality components for value object comparison.
    /// </summary>
    /// <returns>The components that define equality.</returns>
    protected override IEnumerable<object> GetEqualityComponents()
    {
      yield return Math.Round(Percentage, 1);
    }

    /// <summary>
    /// Returns a string representation of the humidity.
    /// </summary>
    /// <returns>A string showing humidity percentage.</returns>
    public override string ToString() => $"{Percentage:F1}%";
  }
}
