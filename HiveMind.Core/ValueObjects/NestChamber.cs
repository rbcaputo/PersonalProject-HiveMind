using HiveMind.Core.Domain.Common;

namespace HiveMind.Core.ValueObjects
{
  // ==========================================
  //  Represents a chamber within the ant nest
  // ==========================================

  public class NestChamber(
    ChamberType type,
    Position centerPosition,
    double volume,
    int maxCapacity,
    double temperature = 20.0,
    double humidity = 0.7
  ) : IEquatable<NestChamber>
  {
    public Guid Id { get; } = Guid.NewGuid();
    public ChamberType Type { get; } = type;
    public Position CenterPosition { get; } = centerPosition;
    public double Volume { get; private set; } = ValidatePositive(volume, nameof(volume));
    public int MaxCapacity { get; } = ValidatePositive(maxCapacity, nameof(maxCapacity));
    public int CurrentOccupants { get; private set; }
    public double Temperature { get; private set; } = ValidateRange(temperature, -10, 50, nameof(temperature));
    public double Humidity { get; private set; } = ValidateRange(humidity, 0.0, 1.0, nameof(humidity));
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime LastMaintenanceAt { get; private set; } = DateTime.UtcNow;

    //  Connections to other chambers
    public HashSet<Guid> ConnectedChambers { get; } = [];

    //  Chamber condition
    public double StructuralIntegrity { get; private set; } = 1.0;
    public double AirQuality { get; private set; } = 1.0;
    public bool NeedsMaintenance => StructuralIntegrity < 0.8 || AirQuality < 0.7;

    //  Type-specific properties
    public double FoodStorageCapacity => Type == ChamberType.FoodStorage ? Volume * 0.8 : 0;
    public int BroodCapacity => Type == ChamberType.Nursery ? MaxCapacity : 0;
    public double VentilationRating => CalculateVentilationRating();

    //  Chamber management
    public bool CanAccomodate(int additionalOccupants) =>
      CurrentOccupants + additionalOccupants <= MaxCapacity;

    public bool TryAddOccupants(int count)
    {
      if (CanAccomodate(count))
      {
        CurrentOccupants += count;

        return true;
      }

      return false;
    }

    public void RemoveOccupants(int count) =>
      CurrentOccupants = Math.Max(0, CurrentOccupants - count);

    public void UpdateEnvironmentalConditions(double temperature, double humidity)
    {
      Temperature = ValidateRange(temperature, -10, 50, nameof(temperature));
      Humidity = ValidateRange(humidity, 0.0, 1.0, nameof(humidity));
    }

    public void PerformMaintenance(double qualityImprovement = 0.2)
    {
      StructuralIntegrity = Math.Min(1.0, StructuralIntegrity + qualityImprovement);
      AirQuality = Math.Min(1.0, AirQuality + qualityImprovement);
      LastMaintenanceAt = DateTime.UtcNow;
    }

    public void ApplyWear(double wearAmount = 0.001)
    {
      StructuralIntegrity = Math.Max(0.1, StructuralIntegrity - wearAmount);
      AirQuality = Math.Max(0.1, AirQuality - wearAmount * 0.5);
    }

    private double CalculateVentilationRating()
    {
      //  More connections = better ventilation
      double connectionBonus = Math.Min(1.0, ConnectedChambers.Count * 0.2);

      //  Type-specific ventilation needs
      double typeMultiplier = Type switch
      {
        ChamberType.Entrance => 2.0,
        ChamberType.MainTunnel => 1.5,
        ChamberType.FoodStorage => 0.8,
        ChamberType.WasteDumps => 2.0,
        _ => 1.0
      };

      return (0.5 + connectionBonus) * typeMultiplier;
    }

    //  Validation helpers
    private static double ValidatePositive(double value, string paramName) =>
      value > 0 ? value : throw new ArgumentException($"{paramName} must be positive", paramName);

    private static int ValidatePositive(int value, string paramName) =>
      value > 0 ? value : throw new ArgumentException($"{paramName} must be positive", paramName);

    private static double ValidateRange(double value, double min, double max, string paramName) =>
      value >= min && value <= max
        ? value
        : throw new ArgumentException($"{paramName} must be between {min} and {max}", paramName);

    public bool Equals(NestChamber? other) =>
      other is not null && Id == other.Id;

    public override bool Equals(object? obj) =>
      Equals(obj as NestChamber);

    public override int GetHashCode() =>
      Id.GetHashCode();
  }

  // ======================================
  //  Types of chambers found in ant nests
  // ======================================

  public enum ChamberType
  {
    Entrance,          //  Entry/exit points - multiple per nest
    MainTunnel,        //  Primary thoroughfares for traffic
    QueensChamber,     //  Royal quarters - optimal temperature/humidity
    Nursery,           //  Brood care chambers - age segregated
    FoodStorage,       //  Granaries for storing food
    WasteDumps,        //  Refuse and sanitation chambers
    FungusGarden,      //  For species that cultivate fungus
    WinterChamber,     //  Deeps chambers for cold weather
    EmergencyChamber,  //  Backup spaces for disasters
    WorkshopArea       //  Tool storage and construction staging
  }

  // =============================================
  //  Represents a tunnel connecting two chambers
  // =============================================

  public class Tunnel : IEquatable<Tunnel>
  {
    public Tunnel(Guid fromChamber, Guid toChamber, double length, double width = 1.0)
    {
      Id = Guid.NewGuid();
      FromChamber = fromChamber;
      ToChamber = toChamber;
      Length = ValidatePositive(length, nameof(length));
      Width = ValidatePositive(width, nameof(width));
      TrafficCapacity = CalculateTrafficCapacity();
    }

    public Guid Id { get; }
    public Guid FromChamber { get; }
    public Guid ToChamber { get; }
    public double Length { get; }
    public double Width { get; }
    public int TrafficCapacity { get; }
    public int CurrentTraffic { get; private set; }
    public double StructuralIntegrity { get; private set; } = 1.0;

    public bool IsCongested => CurrentTraffic > TrafficCapacity * 0.8;
    public double MovementSpeedModifier => IsCongested ? 0.5 : 1.0;

    public bool TryAddTraffic(int antCount)
    {
      if (CurrentTraffic + antCount <= TrafficCapacity)
      {
        CurrentTraffic += antCount;

        return true;
      }

      return false;
    }

    public void RemoveTraffic(int antCount) =>
      CurrentTraffic = Math.Max(0, CurrentTraffic - antCount);

    public void ApplyWear(double wearAmount = 0.0005) =>
      StructuralIntegrity = Math.Max(0.1, StructuralIntegrity - wearAmount);

    public void PerformMaintenance(double improvement = 0.1) =>
      StructuralIntegrity = Math.Min(1.0, StructuralIntegrity + improvement);

    private int CalculateTrafficCapacity()
    {
      // Wider tunnels can handle more traffic
      double baseCapacity = Width * 10;

      // Shorter tunnels have higher effective capacity
      double lengthPenalty = Math.Max(0.5, 1.0 - (Length / 100.0));

      return (int)(baseCapacity * lengthPenalty);
    }

    private static double ValidatePositive(double value, string paramName) =>
      value > 0 ? value : throw new ArgumentException($"{paramName} must be positive", paramName);

    public bool Equals(Tunnel? other) =>
      other is not null && Id == other.Id;

    public override bool Equals(object? obj) =>
      Equals(obj as Tunnel);

    public override int GetHashCode() =>
      Id.GetHashCode();
  }
}
