namespace HiveMind.Core.ValueObjects
{
  // ========================================================
  //  Represents the physiological characteristics of an ant
  // ========================================================

  public class AntPhysiology(
    double metabolismRate,
    double maxCarryCapacity,
    double movementSpeed,
    int longevityDays,
    double scentSensitivity = 0.5,
    double pheromoneProduction = 1.0,
    double mandibleStrength = 1.0,
    double thermalTolerance = 0.5
  ) : IEquatable<AntPhysiology>
  {

    //  Calories burned per simulation tick (based on real ant metabolism)
    public double MetabolismRate { get; } = ValidatePositive(metabolismRate, nameof(metabolismRate));

    //  Maximum carrying capacity as multiple of body weight
    //  Workers 10-50x, Soldiers 5-20x, Queens 0x
    public double MaxCarryCapacity { get; } = ValidateNonNegative(maxCarryCapacity, nameof(maxCarryCapacity));

    //  Movement speed in simulation units per tick
    //  3-7 cm/second depending on caste
    public double MovementSpeed { get; } = ValidatePositive(movementSpeed, nameof(movementSpeed));

    //  Expected lifespan in simulation days
    //  Queens 10-30 years, Workers 1-6 months, Soldiers 2-4 months
    public int LongevityDays { get; } = ValidatePositive(longevityDays, nameof(longevityDays));

    //  Ability to detect and follow pheromone trails (0.0 to 2.0)
    public double ScentSensitivity { get; } = ValidateRange(scentSensitivity, 0.0, 2.0, nameof(scentSensitivity));

    //  Rate of pheromone production and deposition
    public double PheromoneProduction { get; } = ValidateNonNegative(pheromoneProduction, nameof(pheromoneProduction));

    //  Jaw strength for combat and construction (soldiers have highest)
    public double MandibleStrength { get; } = ValidateNonNegative(mandibleStrength, nameof(mandibleStrength));

    //  Tolerance to temperature variations (0.0 to 1.0)
    public double ThermalTolerance { get; } = ValidateRange(thermalTolerance, 0.0, 1.0, nameof(thermalTolerance));

    // ====================
    //  Validation helpers
    // ====================

    private static double ValidatePositive(double value, string paramName) =>
      value > 0 ? value : throw new ArgumentException($"{paramName} must be positive", paramName);

    private static double ValidateNonNegative(double value, string paramName) =>
      value >= 0 ? value : throw new ArgumentException($"{paramName} must be non-negative", paramName);

    private static int ValidatePositive(int value, string paramName) =>
      value > 0 ? value : throw new ArgumentException($"{paramName} must be positive", paramName);

    private static double ValidateRange(double value, double min, double max, string paramName) =>
      value >= min && value <= max
        ? value
        : throw new ArgumentException($"{paramName} must be between {min} and {max}", paramName);

    public bool Equals(AntPhysiology? other) =>
      other is not null &&
      Math.Abs(MetabolismRate - other.MetabolismRate) < 0.001 &&
      Math.Abs(MaxCarryCapacity - other.MaxCarryCapacity) < 0.001 &&
      Math.Abs(MovementSpeed - other.MovementSpeed) < 0.001 &&
      LongevityDays == other.LongevityDays;

    public override bool Equals(object? obj) =>
      Equals(obj as AntPhysiology);

    public override int GetHashCode() =>
      HashCode.Combine(MetabolismRate, MaxCarryCapacity, MovementSpeed, LongevityDays);

    //  Development stages of ant lifecycle
    //  Timing varies by species and temperature
    public enum DevelopmentStage
    {
      Egg,         //  7-14 days - helpless, needs constant care
      Larva,       //  14-21 days - growing, fed by workers
      Pupa,        //  7-14 days - metamorphosis, no feeding needed
      YoungAdult,  //  0-30 days - learning tasks, high energy needs
      Adult,       //  30-180 days - peak productivity period
      Elder,       //  180+ days - reduced activity, wisdom/experience
      Dead
    }

    //  Nutritional status affecting ant performance
    public enum NutritionalStatus
    {
      Starving,  //  Critical - health declining
      Hungry,    //  Low energy, reduced performance
      Adequate,  //  Normal functioning
      WellFed,   //  Peak performance
      Overfed    //  Reduced mobility
    }
  }
}
