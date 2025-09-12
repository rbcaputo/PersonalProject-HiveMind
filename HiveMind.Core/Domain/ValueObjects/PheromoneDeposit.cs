namespace HiveMind.Core.Domain.ValueObjects
{
  // ===================================================
  //  Represents a pheromone deposit in the environment
  // ===================================================

  public class PheromoneDeposit : IEquatable<PheromoneDeposit>
  {
    public PheromoneDeposit(
      PheromoneType type,
      double intensity,
      Guid depositorId,
      DateTime despositedAt,
      double decayRate
    )
    {
      Type = type;
      InitialIntensity = ValidatePositive(intensity, nameof(intensity));
      CurrentIntensity = InitialIntensity;
      DepositorId = depositorId;
      DepositedAt = despositedAt;
      DecayRate = ValidatePositive(decayRate, nameof(decayRate));

    }

    public PheromoneType Type { get; }
    public double InitialIntensity { get; }
    public double CurrentIntensity { get; private set; }
    public Guid DepositorId { get; }
    public DateTime DepositedAt { get; }
    public double DecayRate { get; }
    public bool IsActive => CurrentIntensity > 0.01; // Threshold for detection

    //  Applies decay over time based on pheromone type and environmental conditions
    public void ApplyDecay(double deltaTime, double temperatureModifier = 1.0, double humidityModifier = 1.0)
    {
      if (!IsActive)
        return;

      //  Environmental factors affect decay rate
      double effectiveDecayRate = DecayRate * temperatureModifier * humidityModifier;
      double decayAmount = CurrentIntensity * effectiveDecayRate * deltaTime;

      CurrentIntensity = Math.Max(0, CurrentIntensity - decayAmount);
    }

    //  Reinforces the pheromone with additional deposits
    public void Reinforce(double additionalIntensity)
    {
      //  Pheromones have saturation limits - can't reinforce indefinitely
      double maxIntensity = InitialIntensity * GetMaxReinforcement();
      CurrentIntensity = Math.Min(maxIntensity, CurrentIntensity + additionalIntensity);
    }

    private double GetMaxReinforcement() =>
      Type switch
      {
        PheromoneType.FoodTrail => 5.0,     //  Food trails can be heavily reinforced
        PheromoneType.HomeTrail => 3.0,     //  Home trails moderately reinforced
        PheromoneType.Alarm => 2.0,         //  Alarm pheromones don't accumulate much
        PheromoneType.Territorial => 10.0,  //  Territory markers very persistent
        _ => 2.0
      };

    private static double ValidatePositive(double value, string paramName) =>
      value > 0 ? value : throw new ArgumentException($"{paramName} must be positive", paramName);

    public bool Equals(PheromoneDeposit? other) =>
      other is not null &&
      Type == other.Type &&
      DepositorId == other.DepositorId &&
      DepositedAt == other.DepositedAt;

    public override bool Equals(object? obj) =>
      Equals(obj as PheromoneDeposit);

    public override int GetHashCode() =>
      DepositorId.GetHashCode();
  }

  // Types of pheromones used by ants for communication
  public enum PheromoneType
  {
    FoodTrail,     //  Recruitment to food sources - moderate persistence
    HomeTrail,     //  Navigation back to nest - long persistence
    Alarm,         //  Danger signals - short burst, fast decay
    Necrophoric,   //  Dead ant removal signals - medium persistance
    Sexual,        //  Mating pheromones - seasonal, specific to reproductives
    Territorial,   //  Colony bounday markers - very long persistence
    Construction,  //  Building coordination - medium persistence
    Recognition,   //  Colony member identification - persistent
    Recruitment,   //  General recruitment for tasks - short to medium persistence
    Inhibition     //  Suppression signals - short persistence
  }

  //  Properties of different pheromone types
  public static class PheromoneProperties
  {
    private static readonly Dictionary<PheromoneType, PheromoneCharacteristics> Properties =
    [
      [PheromoneType.FoodTrail] = new(
        decayRate: 0.1,       //  10% decay per time out
        diffusionRange: 3.0,  //  Spreads 3 units from source
        detectionThreshold: 0.05,
        maxIntensity: 10.0
      ),
      [PheromoneType.HomeTrail] = new (
        decayRate: 0.05,  //  5% decay - more persistent
        diffusionRange: 2.0,
        detectionThreshold: 0.03,
        maxIntensity: 8.0
      ),
      [PheromoneType.Alarm] = new(
        decayRate: 0.3,       //  30% decay - dissipates quickly
        diffusionRange: 5.0,  //  Spreads widely for alert
        detectionThershold: 0.1,
        maxIntensity: 20.0
      ),
      [PheromoneType.Territorial] = new(
        decayRate: 0.01,  //  1% decay - very pesistent
        diffusionRange: 1.5,
        detectionThreshold: 0.02,
        maxIntensite: 15.0
      )
    ];

    public static PheromoneCharacteristics GetCharacteristics(PheromoneType type) =>
      Properties.TryGetValue(type, out var characteristics) ? characteristics : GetDefaultCharacteristics();

    private static PheromoneCharacteristics GeyDefaultCharacteristics() =>
      new(decayRate: 0.1, diffusionRange: 2.0, detectionThreshold: 0.05, maxIntensity: 5.0);
  }

  public record PheromoneCharacteristics(
    double DecayRate,
    double DiffusionRange,
    double DetectionThreshold,
    double MaxIntensity
  );
}
