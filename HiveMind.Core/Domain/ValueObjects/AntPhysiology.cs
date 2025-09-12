using HiveMind.Core.Domain.Enums;

namespace HiveMind.Core.Domain.ValueObjects
{
  // --------------------------------------------------------
  //  Represents the physiological characteristics of an ant
  // --------------------------------------------------------

  public class AntPhysiology(
    double metabolismRate,
    double maxCarryCapacity,
    double movementSpeed,
    int longevityDays,
    double scentSensitivity = 0.5,
    double pheromoneProduction = 1.0,
    double mandibleStrength = 1.0,
    double thermalTolerance = 0.5
  )
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

    // -----------------------------------------
    //  Factory methods for common physiologies
    // -----------------------------------------

    public static AntPhysiology CreateQueenPhysiology() =>
      new(
        metabolismRate: 0.3,       //  Very slow metabolism
        maxCarryCapacity: 0.0,     //  Queens don't carry items
        movementSpeed: 0.2,        //  Very slow movement
        longevityDays: 7300,       //  ~20 years
        scentSensitivity: 0.3,     //  Don't need to follow trails
        pheromoneProduction: 5.0,  //  Strong pheromone production
        mandibleStrength: 1.0,     //  Standard jaw strength
        thermalTolerance: 0.8      //  High tolerance for nest regulation
      );

    public static AntPhysiology CreateWorkerPhysiology() =>
      new(
        metabolismRate: 1.0,       //  Standard metabolism
        maxCarryCapacity: 10.0,    //  Can carry 10x body weight
        movementSpeed: 1.0,        //  Standard speed
        longevityDays: 120,        //  ~4 months
        scentSensitivity: 0.8,     //  Good trail following
        pheromoneProduction: 1.0,  //  Standard pheromone production
        mandibleStrength: 1.5,     //  Good for construction
        thermalTolerance: 0.5      //  Standard tolerance
      );

    public static AntPhysiology CreateSoldierPhysiology() =>
      new(
        metabolismRate: 1.5,       //  Higher energy needs for combat
        maxCarryCapacity: 3.0,     //  Limited by large head
        movementSpeed: 0.7,        //  Slowed due to size
        longevityDays: 90,         //  ~3 months
        scentSensitivity: 0.6,     //  Moderate trail following
        pheromoneProduction: 0.8,  //  Less pheromone production
        mandibleStrength: 5.0,     //  Very strong jaws
        thermalTolerance: 0.6      //  Good tolerance
      );

    public static AntPhysiology CreateForagerPhysiology() =>
      new(
        metabolismRate: 1.2,       //  Higher metabolism for activity
        maxCarryCapacity: 8.0,     //  Good carrying capacity
        movementSpeed: 1.5,        //  Faster for efficient foraging
        longevityDays: 90,         //  ~90 months
        scentSensitivity: 1.0,     //  Excellent trail following
        pheromoneProduction: 1.5,  //  Strong trail laying
        mandibleStrength: 1.0,     //  Standard jaw strength
        thermalTolerance: 0.4      //  Lower tolerance
      );

    // --------------------
    //  Validation helpers
    // --------------------

    private static double ValidatePositive(double value, string paramName) =>
      value > 0
        ? value
        : throw new ArgumentException($"{paramName} must be positive", paramName);

    private static double ValidateNonNegative(double value, string paramName) =>
      value >= 0
        ? value
        : throw new ArgumentException($"{paramName} must be non-negative", paramName);

    private static int ValidatePositive(int value, string paramName) =>
      value > 0
        ? value
        : throw new ArgumentException($"{paramName} must be positive", paramName);

    private static double ValidateRange(double value, double min, double max, string paramName) =>
      value >= min && value <= max
        ? value
        : throw new ArgumentException($"{paramName} must be between {min} and {max}", paramName);

    //  Calculates effective movement speed based on current load
    //  Slow down significantly when carrying heavy loads
    public double GetEffectiveMovementSpeed(double currentLoad)
    {
      if (MaxCarryCapacity <= 0)
        return MovementSpeed;

      double loadRation = currentLoad / MaxCarryCapacity;
      double speedReduction = Math.Min(0.8, loadRation * 0.5);  //  Max 80% speed reduction

      return MovementSpeed * (1.0 - speedReduction);
    }

    //  Calculates metabolic cost modifier based on activity
    //  Different activities consume different amounts of energy
    public double GetMetabolicCostMultiplier(ActivityState activity) =>
      activity switch
      {
        ActivityState.Resting =>
          0.3,
        ActivityState.Idle =>
          0.7,
        ActivityState.Moving =>
          1.2,
        ActivityState.Foraging =>
          1.5,
        ActivityState.Building =>
          1.8,
        ActivityState.Fighting =>
          2.5,
        ActivityState.Caring =>
          1.0,
        _ =>
          1.0
      };
  }

  //  Development stages of ant lifecycle
  //  Timing varies by species and temperature
  public enum DevelopmentStage
  {
    Egg,         //  7-14 days - helpless, needs constant temperature regulation
    Larva,       //  14-21 days - growing rapidly, fed by workers
    Pupa,        //  7-14 days - metamorphosis, no feeding needed
    YoungAdult,  //  0-30 days - learning tasks, high energy needs
    Adult,       //  30%-80% of lifespan - peak productivity period
    Elder,       //  80%+ of lifespan - reduced activity, experienced
    Dead
  }

  //  Nutritional status affecting ant performance
  public enum NutritionalStatus
  {
    Starving,  //  < 10% energy - critical condition, health declining
    Hungry,    //  10%-30% energy - low performance, seeks food priority
    Adequate,  //  30%-70% energy - normal functioning
    WellFed,   //  70%-95% energy - peak performance, can share food
    Overfed    //  > 95% energy - slightly reduced mobility
  }

  // Worker ant specializations that develop pver time
  public enum WorkerSpecialization
  {
    None,            //  Newly adult workers
    Construction,    //  Building and maintenance specialists
    Nursing,         //  Brood care and queen tending
    Foraging,        //  Food collection specialists
    Maintenance,     //  Nest cleaning and repair
    Defense,         //  Guard duties and patrol
    Excavation,      //  Tunnel digging specialists
    FoodProcessing,  //  Food storage and preparation
    Ventilation      //  Air circulation management
  }
}
