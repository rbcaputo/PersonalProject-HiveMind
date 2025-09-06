using HiveMind.Core.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace HiveMind.Application.Models
{
  /// <summary>
  /// Configuration settings for the simulation
  /// </summary>
  public class SimulationConfiguration : IValidatableObject
  {
    private double _environmentWidth = 200.0;
    private double _environmentHeight = 200.0;
    private double _deltaTime = 0.1;
    private int _targetTPS = 30;
    private int _maxColonyPopulation = 500;

    /// <summary>
    /// Width of the simulation environment
    /// </summary>
    public double EnvironmentWidth
    {
      get => _environmentWidth;
      set
      {
        if (double.IsNaN(value) || double.IsInfinity(value))
          throw new ArgumentException("Environment width must be a finite number");
        if (value <= 0)
          throw new ArgumentException("Environment width must be positive");
        if (value > 10000)
          throw new ArgumentException("Environment width cannot exceed 10,000 units");

        _environmentWidth = value;
      }
    }

    /// <summary>
    /// Height of the simulation environment
    /// </summary>
    public double EnvironmentHeight
    {
      get => _environmentHeight;
      set
      {
        if (double.IsNaN(value) || double.IsInfinity(value))
          throw new ArgumentException("Environment height must be a finite number");
        if (value <= 0)
          throw new ArgumentException("Environment height must be positive");
        if (value > 10000)
          throw new ArgumentException("Environment height cannot exceed 10,000 units");

        _environmentHeight = value;
      }
    }

    /// <summary>
    /// Time step for each simulation tick in seconds
    /// </summary>
    public double DeltaTime
    {
      get => _deltaTime;
      set
      {
        if (double.IsNaN(value) || double.IsInfinity(value))
          throw new ArgumentException("Delta time must be a finite number");
        if (value <= 0)
          throw new ArgumentException("Delta time must be positive");
        if (value > 1.0)
          throw new ArgumentException("Delta time should not exceed 1 second");

        _deltaTime = value;
      }
    }

    /// <summary>
    /// Target ticks per second
    /// </summary>
    public int TargetTPS
    {
      get => _targetTPS;
      set
      {
        if (value <= 0)
          throw new ArgumentException("Target TPS must be positive");
        if (value > 1000)
          throw new ArgumentException("Target TPS should not exceed 1000");

        _targetTPS = value;
      }
    }

    /// <summary>
    /// Initial colony position
    /// </summary>
    public Position ColonyPosition { get; set; } = new Position(100, 100);

    /// <summary>
    /// Maximum colony population
    /// </summary>
    public int MaxColonyPopulation
    {
      get => _maxColonyPopulation;
      set
      {
        if (value <= 0)
          throw new ArgumentException("Max colony population must be positive");
        if (value > 10000)
          throw new ArgumentException("Max colony population should not exceed 10,000");

        _maxColonyPopulation = value;
      }
    }

    /// <summary>
    /// Number of initial food sources
    /// </summary>
    public int InitialFoodSources { get; set; } = 10;

    /// <summary>
    /// Random seed for reproducible simulations
    /// </summary>
    public int? RandomSeed { get; set; }

    /// <summary>
    /// Maximum simulation ticks (0 = unlimited)
    /// </summary>
    public long MaxTicks { get; set; } = 0;

    /// <summary>
    /// Validates the entire configuration for logical consistency
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      List<ValidationResult> results = [];

      // Validate colony position is within environment bounds
      if (!ColonyPosition.IsWithinBounds(0, 0, EnvironmentWidth, EnvironmentHeight))
        results.Add(new($"Colony position {ColonyPosition} is outside environment bounds (0,0) to ({EnvironmentWidth},{EnvironmentHeight})",
          [nameof(ColonyPosition)]));

      // Validate reasonable TPS vs DeltaTime relationship
      double effectiveTimeStep = 1.0 / TargetTPS;
      if (DeltaTime > effectiveTimeStep * 2)
        results.Add(new($"DeltaTime ({DeltaTime}) is too large relative to TargetTPS ({TargetTPS}). " +
          $"Consider reducing DeltaTime or TargetTPS.", [nameof(DeltaTime), nameof(TargetTPS)]));

      // Validate food sources count
      if (InitialFoodSources < 0)
        results.Add(new("Initial food sources should not exceed 1000 for performance reasons", [nameof(InitialFoodSources)]));

      return results;
    }

    /// <summary>
    /// Creates a validated configuration with bounds checking
    /// </summary>
    public static SimulationConfiguration CreateValidated(
      double environmentWidth,
      double environmentHeight,
      Position colonyPositon,
      double deltaTime = 0.1,
      int targetTPS = 30
    )
    {
      SimulationConfiguration config = new()
      {
        EnvironmentWidth = environmentWidth,
        EnvironmentHeight = environmentHeight,
        DeltaTime = deltaTime,
        TargetTPS = targetTPS,
        ColonyPosition = colonyPositon.ClampToBounds(0, 0, environmentWidth, environmentHeight)
      };

      return config;
    }
  }
}
