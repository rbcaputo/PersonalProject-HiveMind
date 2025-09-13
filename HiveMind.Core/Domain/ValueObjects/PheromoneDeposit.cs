using HiveMind.Core.Domain.Common;

namespace HiveMind.Core.Domain.ValueObjects
{
  // ---------------------------------------------------
  //  Represents a pheromone deposit in the environment
  //  Immutable value object with decay modeling
  // ---------------------------------------------------

  public sealed record PheromoneDeposit
  {
    public PheromoneDeposit(
      PheromoneType type,
      double intensity,
      Guid depositorId,
      DateTime despositedAt,
      Position location
    )
    {
      Type = type;
      InitialIntensity = ValidatePositive(intensity, nameof(intensity));
      CurrentIntensity = InitialIntensity;
      DepositorId = depositorId;
      DepositedAt = despositedAt;
      Location = location;

      var characteristics = PheromoneProperties.GetCharacteristics(type);
      DecayRate = characteristics.DecayRate;
      DiffusionRange = characteristics.DiffusionRange;
      DetectionThreshold = characteristics.DetectionThreshold;
    }

    // -----------------
    //  Core properties
    // -----------------

    public PheromoneType Type { get; }
    public double InitialIntensity { get; }
    public double CurrentIntensity { get; init; }
    public Guid DepositorId { get; }
    public DateTime DepositedAt { get; }
    public Position Location { get; }

    //  Decay and diffusion properties
    public double DecayRate { get; }
    public double DiffusionRange { get; }
    public double DetectionThreshold { get; }

    //  Status properties
    public bool IsActive =>
      CurrentIntensity > DetectionThreshold;
    public double Age =>
      (DateTime.UtcNow - DepositedAt).TotalSeconds;
    public double IntensityRatio =>
      InitialIntensity > 0
        ? CurrentIntensity / InitialIntensity
        : 0;

    //  Applies natural decay over time with environmental modifiers
    public PheromoneDeposit WithDecay(double deltaTime, double temperature = 25.0, double humidity = 0.7, double windSpeed = 0.1)
    {
      if (!IsActive)
        return this;

      //  Environmental factors affect decay rate
      double temperatureModifier = CalculateTemperatureModifier(temperature);
      double humidityModifier = CalculateHumidityModifier(humidity);
      double windModifier = CalculateWindModifier(windSpeed);

      double effectiveDecayRate = DecayRate * temperatureModifier * humidityModifier * windModifier;
      double decayAmount = CurrentIntensity * effectiveDecayRate * deltaTime;

      double newIntensity = Math.Max(0, CurrentIntensity - decayAmount);

      return this with
      {
        CurrentIntensity = newIntensity
      };
    }

    // Reinforces the pheromone with additional deposits from the same or different ants
    public PheromoneDeposit WithReinforcement(double additionalIntensity, Guid? reinforcingAntId = null)
    {
      if (!IsActive || additionalIntensity <= 0)
        return this;

      //  Different ants can reinforce trails, but with diminishing returns
      double reinforcementEfficiency = reinforcingAntId == DepositorId ? 1.0 : 0.7;
      double effectiveReinforcement = additionalIntensity * reinforcementEfficiency;

      //  Pheromones have saturation limits
      double maxIntensity = InitialIntensity * GetMaxReinforcement();
      double newIntensity = Math.Min(maxIntensity, CurrentIntensity + effectiveReinforcement);

      return this with
      {
        CurrentIntensity = newIntensity
      };
    }

    //  Calculates the effective intensity at a given position considering diffusion
    public double GetIntensityAtPosition(Position position)
    {
      if (!IsActive)
        return 0.0;

      double distance = Location.DistanceTo(position);
      if (distance > DiffusionRange)
        return 0.0;

      //  Exponential falloff with distance
      double falloffFactor = Math.Exp(-2.0 * distance / DiffusionRange);
      return CurrentIntensity * falloffFactor;
    }

    //  Used for trail following behavior
    public Vector2 GetGradientDirection(Position fromPosition)
    {
      if (!IsActive)
        return Vector2.Zero;

      double distance = Location.DistanceTo(fromPosition);
      if (distance < 0.1 || distance > DiffusionRange)
        return Vector2.Zero;

      //  Direction vector from position toward pheromone source
      double deltaX = Location.X - fromPosition.X;
      double deltaY = Location.Y - fromPosition.Y;

      //  Normalize and weight by intensity gradient
      double magnitude = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
      double intensity = GetIntensityAtPosition(fromPosition);
      double weight = intensity / CurrentIntensity;  //  Strength of gradient

      return new Vector2(deltaX / magnitude * weight, deltaY / magnitude * weight);
    }

    // -----------------------------
    //  Private calculation methods
    // -----------------------------

    private double GetMaxReinforcement() =>
      Type switch
      {
        PheromoneType.FoodTrail =>
          5.0,   //  Food trails can be heavily reinforced
        PheromoneType.HomeTrail =>
          3.0,   //  Home trails moderately reinforced
        PheromoneType.Alarm =>
          2.0,   //  Alarm pheromones don't accumulate much
        PheromoneType.Territorial =>
          10.0,  //  Territory markers very persistent
        PheromoneType.Construction =>
          4.0,   //  Construction coordination moderate
        PheromoneType.Recruitment =>
          6.0,   //  Recruitment trails strong reinforcement
        _ =>
          2.0
      };

    private static double CalculateTemperatureModifier(double temperature)
    {
      //  Higher temperatures increase evaporation rate
      const double referenceTemperature = 25.0;  //  °C
      const double temperatureSensitivity = 0.08;  //  8% change per degree

      return Math.Max(0.1, 1.0 + (temperature - referenceTemperature) * temperatureSensitivity);
    }

    private static double CalculateHumidityModifier(double humidity)
    {
      //  Higher humidity decreases evaporation rate
      const double referenceHumidity = 0.7;  //  70%
      const double humiditySensitivity = 0.5;  //  50% effect range

      return Math.Max(0.2, 1.0 - (humidity - referenceHumidity) * humiditySensitivity);
    }

    private static double CalculateWindModifier(double windSpeed) =>
      //  Wind disperses pheromones faster
      Math.Max(0.3, 1.0 + windSpeed * 2.0);  //  Wind speed in m/s

    private static double ValidatePositive(double value, string paramName) =>
      value > 0
        ? value
        : throw new ArgumentException($"{paramName} must be positive", paramName);

    public override string ToString() =>
      $"{Type} pheromone at {Location} - Intensity: {CurrentIntensity:F2}/{InitialIntensity:F2} (Age: {Age:F1}s)";
  }

  // ----------------------------------------------------
  //  Types of pheromones used by ants for communication
  // ----------------------------------------------------

  public enum PheromoneType
  {
    [PheromoneInfo("Trail to food sources", 600, 8.0, 0.05)]
    FoodTrail,     //  Recruitment to food sources - moderate persistence
    [PheromoneInfo("Navigation home", 1200, 5.0, 0.03)]
    HomeTrail,     //  Navigation back to nest - long persistence
    [PheromoneInfo("Danger alarm", 60, 12.0, 0.1)]
    Alarm,         //  Danger signals - short burst, fast decay, wide range
    [PheromoneInfo("Remove dead ants", 300, 6.0, 0.08)]
    Necrophoric,   //  Dead ant removal signals - medium persistence
    [PheromoneInfo("Mating signals", 3600, 15.0, 0.02)]
    Sexual,        //  Mating pheromones - seasonal, long lasting
    [PheromoneInfo("Territory boundary", 7200, 4.0, 0.01)]
    Territorial,   //  Colony boundary markers - very long persistence
    [PheromoneInfo("Construction coordination", 900, 7.0, 0.06)]
    Construction,  //  Building coordination - medium persistence, good range
    [PheromoneInfo("Colony recognition", 1800, 3.0, 0.04)]
    Recognition,   //  Colony member identification - persistent, short range
    [PheromoneInfo("Task recruitment", 450, 10.0, 0.07)]
    Recruitment,   //  General recruitment for tasks - medium duration, wide range
    [PheromoneInfo("Activity suppression", 180, 6.0, 0.09)]
    Inhibition     //  Suppression signals - short persistence
  }

  // --------------------------------------------------------
  //  Metadata attribute providing pheromone characteristics
  // --------------------------------------------------------

  [AttributeUsage(AttributeTargets.Field)]
  public class PheromoneInfoAttribute(
    string description,
    int durationSeconds,
    double diffusionRange,
    double decayRate
  ) : Attribute
  {
    public string Description { get; } = description;
    public int DurationSeconds { get; } = durationSeconds;
    public double DiffusionRange { get; } = diffusionRange;
    public double DecayRate { get; } = decayRate;
  }

  // -----------------------------------------
  //  Properties of different pheromone types
  // -----------------------------------------

  public static class PheromoneProperties
  {
    private static readonly Dictionary<PheromoneType, PheromoneCharacteristics> Properties =
      InitializeProperties();

    public static PheromoneCharacteristics GetCharacteristics(PheromoneType type) =>
      Properties.TryGetValue(type, out var characteristics)
        ? characteristics
        : GetDefaultCharacteristics();

    private static Dictionary<PheromoneType, PheromoneCharacteristics> InitializeProperties()
    {
      Dictionary<PheromoneType, PheromoneCharacteristics> properties = [];

      foreach (PheromoneType type in Enum.GetValues<PheromoneType>())
      {
        var field = typeof(PheromoneType).GetField(type.ToString());
        if (field?
              .GetCustomAttributes(typeof(PheromoneInfoAttribute), false)
              .FirstOrDefault() is PheromoneInfoAttribute attribute)
          properties[type] = new(
            DecayRate: attribute.DecayRate,
            DiffusionRange: attribute.DiffusionRange,
            DetectionThreshold: attribute.DecayRate * 2,      //  Threshold related to decay rate
            MaxIntensity: 20.0 / (attribute.DecayRate * 10),  //  Inversely related to decay
            Description: attribute.Description
          );
      }

      return properties;
    }

    private static PheromoneCharacteristics GetDefaultCharacteristics() =>
      new(
        DecayRate: 0.1,
        DiffusionRange: 5.0,
        DetectionThreshold: 0.05,
        MaxIntensity: 10.0,
        Description: "Unknown pheromone"
      );
  }

  public record PheromoneCharacteristics(
    double DecayRate,
    double DiffusionRange,
    double DetectionThreshold,
    double MaxIntensity,
    string Description
  );

  // ------------------------------------------------------
  //  Simple 2D vector for pheromone gradient calculations
  // ------------------------------------------------------

  public readonly record struct Vector2(double X, double Y)
  {
    public double Magnitude =>
      Math.Sqrt(X * X + Y * Y);

    public Vector2 Normalized
    {
      get
      {
        var mag = Magnitude;
        return mag > 0.001
          ? new Vector2(X / mag, Y / mag)
          : Zero;
      }
    }

    public static Vector2 Zero =>
      new(0, 0);

    //  Operators overload
    public static Vector2 operator +(Vector2 a, Vector2 b) =>
      new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) =>
      new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 v, double scalar) =>
      new(v.X * scalar, v.Y * scalar);
    public static Vector2 operator /(Vector2 v, double scalar) =>
      scalar != 0
        ? new(v.X / scalar, v.Y / scalar)
        : Zero;

    //  Calculates angle between two vectors in radians
    public double AngleTo(Vector2 other)
    {
      double dot = X * other.X + Y * other.Y;
      double det = X * other.Y - Y * other.X;

      return Math.Atan2(det, dot);
    }

    //  Rotates vector by specified angle in radians
    public Vector2 Rotate(double angleRadians)
    {
      double cos = Math.Cos(angleRadians);
      double sin = Math.Sin(angleRadians);

      return new Vector2(X * cos - Y * sin, X * sin + Y * cos);
    }

    public override string ToString() =>
      $"({X:F2}, {Y:F2})";
  }
}
