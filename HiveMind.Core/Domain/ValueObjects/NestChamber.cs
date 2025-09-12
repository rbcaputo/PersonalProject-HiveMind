using HiveMind.Core.Domain.Common;
using System.Runtime.CompilerServices;

namespace HiveMind.Core.Domain.ValueObjects
{
  // ------------------------------------------------------
  //  Represents a chamber within the ant nest
  //  Immutable value object ensuring structural integrity
  // ------------------------------------------------------

  public class NestChamber(
    ChamberType type,
    Position centerPosition,
    double volume,
    int maxCapacity,
    double temperature = 22.0,
    double humidity = 0.75,
    int depthLayer = 1
  )
  {
    private HashSet<Guid> _connectedChambers = [];

    // -----------------
    //  Core properties
    // -----------------

    public Guid Id { get; } = Guid.NewGuid();
    public ChamberType Type { get; } = type;
    public Position CenterPosition { get; } = centerPosition;
    public double Volume { get; } = ValidatePositive(volume, nameof(volume));
    public int MaxCapacity { get; } = ValidatePoitive(maxCapacity, nameof(maxCapacity));
    public int DepthLayer { get; } = ValidatePositive(depthLayer, nameof(depthLayer));
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime LastMaintenanceAt { get; private set; } = DateTime.UtcNow;

    //  Environmental conditions
    public double Temperature { get; private set; } = ValidateRange(temperature, -5, 40, nameof(temperature));
    public double Humidity { get; private set; } = ValidateRange(humidity, 0.0, 1.0, nameof(humidity));
    public double StructuralIntegrity { get; private set; } = 1.0;
    public double AirQuality { get; private set; } = 1.0;

    //  Occupancy and connections
    public int CurrentOccupants { get; private set; }
    public IReadOnlySet<Guid> ConnectedChambers =>
      _connectedChambers.ToHashSet();

    //  Chamber status
    public bool NeedsMaintenance =>
      StructuralIntegrity < 0.8 || AirQuality < 0.7;
    public bool IsOvercrowded =>
      CurrentOccupants > MaxCapacity * 0.9;
    public double OccupancyRatio =>
      MaxCapacity > 0
        ? (double)CurrentOccupants / MaxCapacity
        : 0;
    public bool IsStructurallySound =>
      StructuralIntegrity > 0.5;

    // --------------------------
    //  Type-specific properties
    // --------------------------

    public double FoodStorageCapacity =>
      Type == ChamberType.FoodStorage
        ? Volume * 0.8
        : 0;
    public int BroodCapacity => Type switch
    {
      ChamberType.Nursery =>
        MaxCapacity * 2,  //  Nurseries can hold more brood
      ChamberType.QueensChamber =>
        50,  //  Queens produce eggs
      _ =>
        0
    };

    public double VentilationRating =>
      CalculateVentilationRating();
    public double OptimalTemperature =>
      GetOptimalTemperature();
    public double OptimalHumidity =>
      GetOptimalHumidity();

    // ----------------------------
    //  Chamber management methods
    // ----------------------------

    public NestChamber WithOccupants(int newOccupantsCount)
    {
      if (newOccupantsCount < 0)
        throw new ArgumentException("Occupant count cannot be negative");
      if (newOccupantsCount > MaxCapacity * 1.2)
        throw new ArgumentException("Chamber severely overcrowded");

      return new(Type, CenterPosition, Volume, MaxCapacity, Temperature, Humidity, DepthLayer)
      {
        CurrentOccupants = newOccupantsCount,
        StructuralIntegrity = this.StructuralIntegrity,
        AirQuality = this.AirQuality,
        LastMaintenanceAt = this.LastMaintenanceAt,
        _connectedChambers = [.. this.ConnectedChambers]
      };
    }

    public NestChamber WithConnection(Guid connectedChamberId)
    {
      HashSet<Guid> newConnections = [.. _connectedChambers, connectedChamberId];

      return new(Type, CenterPosition, Volume, MaxCapacity, Temperature, Humidity, DepthLayer)
      {
        CurrentOccupants = this.CurrentOccupants,
        StructuralIntegrity = this.StructuralIntegrity,
        AirQuality = this.AirQuality,
        LastMaintenanceAt = this.LastMaintenanceAt,
        _connectedChambers = newConnections
      };
    }

    public NestChamber WithEnvironmentalConditions(double temperature, double humidity) =>
      new(
        Type,
        CenterPosition,
        Volume,
        MaxCapacity,
        ValidateRange(temperature, -5, 40, nameof(temperature)),
        ValidateRange(humidity, 0.0, 1.0, nameof(humidity)),
        DepthLayer
      )
      {
        CurrentOccupants = this.CurrentOccupants,
        StructuralIntegrity = this.StructuralIntegrity,
        AirQuality = this.AirQuality,
        LastMaintenanceAt = this.LastMaintenanceAt,
        _connectedChambers = [.. this._connectedChambers]
      };

    public NestChamber WithMaintenance(double structuralImprovement = 0.2, double airQualityImprovement = 0.2) =>
      new(
        Type,
        CenterPosition,
        Volume,
        MaxCapacity,
        Temperature,
        Humidity,
        DepthLayer
      )
      {
        CurrentOccupants = this.CurrentOccupants,
        StructuralIntegrity = Math.Min(1.0, this.StructuralIntegrity + structuralImprovement),
        AirQuality = Math.Min(1.0, this.AirQuality + airQualityImprovement),
        LastMaintenanceAt = DateTime.UtcNow,
        _connectedChambers = [.. this._connectedChambers]
      };

    public NestChamber WithWear(double structuralWear = 0.001, double airQualityWear = 0.0005) =>
      new(
        Type,
        CenterPosition,
        Volume,
        MaxCapacity,
        Temperature,
        Humidity,
        DepthLayer
      )
      {
        CurrentOccupants = this.CurrentOccupants,
        StructuralIntegrity = Math.Max(0.1, this.StructuralIntegrity - structuralWear),
        AirQuality = Math.Max(0.1, this.AirQuality - airQualityWear),
        LastMaintenanceAt = this.LastMaintenanceAt,
        _connectedChambers = [.. this._connectedChambers]
      };

    // -----------------------------
    //  Private calculation methods
    // -----------------------------

    private double CalculateVentilationRating()
    {
      //  More connections = better ventilation
      double connectionBonus = Math.Min(1.0, _connectedChambers.Count * 0.15);
      //  Type-specific ventilation characteristics
      double typeMultiplier = Type switch
      {
        ChamberType.Entrance =>
          2.5,  //  Excellent ventilation
        ChamberType.MainTunnel =>
          2.0,  //  Very good ventilation
        ChamberType.WasteDumps =>
          1.8,  //  Need good air circulation
        ChamberType.WorkshopArea =>
          1.5,  //  Moderate ventilation needs
        ChamberType.FoodStorage =>
          0.9,  //  Lower ventilation to preserve food
        ChamberType.QueensChamber =>
          1.2,  //  Moderate, controlled environment
        ChamberType.Nursery =>
          1.3,  //  Good for brood health
        _ =>
          1.0
      };

      //  Depth affects ventilation - deepper chambers need more connections
      double depthPenalty = Math.Max(0.5, 1.0 - (DepthLayer * 0.1));

      return (0.5 + connectionBonus) * typeMultiplier * depthPenalty;
    }

    private double GetOptimalTemperature() =>
      Type switch
      {
        ChamberType.QueensChamber =>
          25.0,  //  Warm for egg development
        ChamberType.Nursery =>
          24.0,  //  Warm for larvae/pupae
        ChamberType.FoodStorage =>
          18.0,  //  Cool to preserve food
        ChamberType.WinterChamber =>
          15.0,  //  Cool for hibernation
        ChamberType.WasteDumps =>
          20.0,  //  Moderate to control decomposition
        _ =>
          22.0  //  Standard nest temperature
      };


    private double GetOptimalHumidity() =>
      Type switch
      {
        ChamberType.FoodStorage =>
          0.6,   //  Lower humidity to prevent spoilage
        ChamberType.Nursery =>
          0.8,   //  High humidity for brood development
        ChamberType.QueensChamber =>
          0.75,  //  Optimal for queen 
        ChamberType.WasteDumps =>
          0.7,   //  Moderate for waste processing
        _ =>
          0.75   //  Standard nest humidity
      };

    // --------------------------------------
    //  Utility methods for chamber analysis
    // --------------------------------------

    public double GetEnvironmentalStress()
    {
      double temperatureStress = Math.Abs(Temperature - OptimalTemperature) / OptimalTemperature;
      double humidityStress = Math.Abs(Humidity - OptimalHumidity) / OptimalHumidity;
      double occupancyStress = Math.Max(0, (OccupancyRatio - 0.8) / 0.2);  //  Stress above 80% capacity

      return (temperatureStress + humidityStress + occupancyStress) / 3.0;
    }

    public bool CanAccommodateAdditionalOccupants(int count) =>
      CurrentOccupants + count <= MaxCapacity;

    public double GetMaintenancePriority()
    {
      double structuralUrgency = (1.0 - StructuralIntegrity) * 2.0;
      double airQualityUrgency = (1.0 - AirQuality) * 1.5;
      double timeUrgency = Math.Min(1.0, (DateTime.UtcNow - LastMaintenanceAt).TotalDays / 30.0);
      double occupancyUrgency = Math.Max(0, (OccupancyRatio - 0.8) / 0.2);

      return Math.Max(0, structuralUrgency + airQualityUrgency + timeUrgency + occupancyUrgency);
    }

    // --------------------
    //  Validation helpers
    // --------------------

    private static double ValidatePositive(double value, string paramName) =>
      value > 0
        ? value
        : throw new ArgumentException($"{paramName} must be positive", paramName);

    private static int ValidatePositive(int value, string paramName) =>
      value > 0
        ? value
        : throw new ArgumentException($"{paramName} must be positive", paramName);

    private static double ValidateRange(double value, double min, double max, string paramName) =>
      value >= min && value <= max
        ? value
        : throw new ArgumentException($"{paramName} must be between {min} and {max}", paramName);
  }

  // --------------------------------------
  //  Types of chambers found in ant nests
  // --------------------------------------

  public enum ChamberType
  {
    [ChamberInfo("Primary entrance/exit", 1, 2)]
    Entrance,          //  Entry/exit points - tipically 1-3 per nest
    [ChamberInfo("High traffic corridor", 2, 3)]
    MainTunnel,        //  Primary thoroughfares connecting major chambers
    [ChamberInfo("Royal quartes", 1, 1)]
    QueensChamber,     //  Single chamber for queen - optimal conditions
    [ChamberInfo("Brood rearing area", 3, 6)]
    Nursery,           //  Multiple nurseries for different brood stages
    [ChamberInfo("Food granary", 2, 5)]
    FoodStorage,       //  Food storage chambers - controlled climate
    [ChamberInfo("Waste disposal", 1, 3)]
    WasteDumps,        //  Sanitation chambers - isolated from main nest
    [ChamberInfo("Fungus cultivation", 1, 4)]
    FungusGarden,      //  For fungus growing species (optional)
    [ChamberInfo("Deep hibernation chamber", 1, 2)]
    WinterChamber,     //  Deep chambers for cold weather survival
    [ChamberInfo("Emergency shelter", 2, 4)]
    EmergencyChamber,  //  Backup spaces for disasters/expansion
    [ChamberInfo("Construction staging", 1, 3)]
    WorkshopArea       //  Tool storage and construction material staging
  }

  // ----------------------------------------------------------------------
  //  Metadata attribute for chamber types providing construction guidance
  // ----------------------------------------------------------------------

  [AttributeUsage(AttributeTargets.Field)]
  public class ChamberInfoAttribute(string description, int minPerColony, int maxPerColony) : Attribute
  {
    public string Description { get; } = description;
    public int MinPerColony { get; } = minPerColony;
    public int MaxPerColony { get; } = maxPerColony;
  }

  // ---------------------------------------------
  //  Represents a tunnel connecting two chambers
  // ---------------------------------------------

  public sealed record Tunnel
  {
    public Tunnel(Guid fromChamber, Guid toChamber, double length, double width = 1.5, TunnelType type = TunnelType.Standard)
    {
      Id = Guid.NewGuid();
      FromChamber = fromChamber;
      ToChamber = toChamber;
      Length = ValidatePositive(length, nameof(length));
      Width = ValidatePositive(width, nameof(width));
      Type = type;
      TrafficCapacity = CalculateTrafficCapacity();
      ConstructionCost = CalculateConstructionCost();
      CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; }
    public Guid FromChamber { get; }
    public Guid ToChamber { get; }
    public double Length { get; }
    public double Width { get; }
    public TunnelType Type { get; }
    public int TrafficCapacity { get; }
    public double ConstructionCost { get; }
    public DateTime CreatedAt { get; }
    public int CurrentTraffic { get; init; }
    public double StructuralIntegrity { get; init; } = 1.0;
    public double MaintenanceLevel { get; init; } = 1.0;

    //  Traffic management
    public bool IsCongested =>
      CurrentTraffic > TrafficCapacity * 0.85;
    public bool IsBlocked =>
      StructuralIntegrity < 0.3;
    public double MovementSpeedModifier =>
      CalculateSpeedModifier();
    public double TrafficEfficiency =>
      IsCongested
        ? 0.5
        : (IsBlocked ? 0.1 : 1.0);

    //  Tunnel condition assessment
    public bool NeedsMaintenance =>
      StructuralIntegrity < 0.7 || MaintenanceLevel < 0.6;
    public bool IsEmergencyRepairNeeded =>
      StructuralIntegrity < 0.4;
    public double GetMaintenancePriority =>
      CalculateMaintenancePriority();

    //  Construction and maintenance
    public Tunnel WithTraffic(int newTrafficCount) =>
      this with
      {
        CurrentTraffic = Math.Max(0, Math.Min(TrafficCapacity * 2, newTrafficCount))  //  Allow 2x overcapacity in emergencies
      };

    public Tunnel WithWear(double wearAmount = 0.001) =>
      this with
      {
        StructuralIntegrity = Math.Max(0.1, StructuralIntegrity - wearAmount),
        MaintenanceLevel = Math.Max(0.1, MaintenanceLevel - wearAmount * 0.5)
      };

    public Tunnel WithMaintenance(double improvement = 0.2) =>
      this with
      {
        StructuralIntegrity = Math.Min(1.0, StructuralIntegrity + improvement),
        MaintenanceLevel = Math.Min(1.0, MaintenanceLevel + improvement)
      };

    // -----------------------------
    //  Private calculation methods
    // -----------------------------

    private int CalculateTrafficCapaciity()
    {
      double baseCapacity = Width * 8.0;  //  8 ants per width unit
      //  Tunnel type affects capacity
      double typleMultiplier = Type switch
      {
        TunnelType.MainArtery =>
          1.5,  //  High capacity corridors
        TunnelType.Emergency =>
          2.0,  //  Wide emergency exits
        TunnelType.Service =>
          0.8,  //  Smaller service tunnels
        TunnelType.Ventilation =>
          0.5,  //  Primarily for air, limited ant traffic
        _ =>
          1.0
      };

      //  Shorter tunnels have higher effactive capacity
      double lengthEfficiency = Math.Max(0.5, 1.0 - (Length / 200.0));

      return Math.Max(1, (int)(baseCapacity * typleMultiplier * lengthEfficiency));
    }

    private double CalculateConstructionCost()
    {
      double baseCost = Length * Width * 0.5;  //  Base cost per volume
      //  Tunnel type affects construction complexity
      double typeMultiplier = Type switch
      {
        TunnelType.Emergency =>
          1.8,  //  Reinforced construction
        TunnelType.MainArtery =>
          1.5,  //  Higher quality construction
        TunnelType.Ventilation =>
          1.2,  //  Specialized air flow design
        TunnelType.Service =>
          0.8,  //  Simpler construction
        _ =>
          1.0
      };

      return baseCost * typeMultiplier;
    }

    private double CalculateSpeedModifier()
    {
      if (IsBlocked)
        return 0.1;

      double congestionPenalty = IsCongested
        ? 0.6
        : 1.0;
      double conditionBonus = (StructuralIntegrity + MaintenanceLevel) / 2.0;
      double typeBonus = Type switch
      {
        TunnelType.MainArtery =>
          1.2,  //  Smooth, wide corridors
        TunnelType.Emergency =>
          1.1,  //  Well-maintained for quick evacuation
        TunnelType.Service =>
          0.9,  //  Narrower, slower
        TunnelType.Ventilation =>
          0.7,  //  Not optimized for movement
        _ =>
          1.0
      };

      return congestionPenalty * conditionBonus * typeBonus;
    }

    private double CalculateMaintenancePriority()
    {
      double structuralUrgency = (1.0 - StructuralIntegrity) * 3.0;
      double maintenanceUrgency = (1.0 - MaintenanceLevel) * 2.0;
      double usageUrgency = Math.Min(2.0, (double)CurrentTraffic / TrafficCapacity);
      double typeImportance = Type switch
      {
        TunnelType.MainArtery =>
          2.0,  //  Critical infrastructure
        TunnelType.Emergency =>
          2.5,  //  Life safety critical
        TunnelType.Ventialtion =>
          1.5,  //  Important for air quality
        _ =>
          1.0
      };

      return (structuralUrgency + maintenanceUrgency + usageUrgency) * typeImportance;
    }

    private static double ValidatePositive(double value, string paramName) =>
      value > 0
        ? value
        : throw new ArgumentException($"{paramName} must be positive", paramName);

    public bool Connects(Guid chamberId) =>
      FromChamber == chamberId || ToChamber == chamberId;

    public Guid GetOtherEnd(Guid chamberId) =>
      FromChamber == chamberId
        ? ToChamber
        : ToChamber == chamberId
        ? FromChamber
        : throw new ArgumentException("Chamber not connected to this tunnel");

    public override string ToString() =>
      $"{Type} Tunnel [{Id:N}] - {Length:F1}x{Width:F1}, Capacity: {CurrentTraffic}/{TrafficCapacity}";
  }

  // -------------------------------------------
  //  Types of tunnels in the nest architecture
  // -------------------------------------------

  public enum TunnelType
  {
    Standard,    //  Regular connection tunnels
    MainArtery,  //  High-capacity main corridors
    Emergency,   //  Emergency evacuation routes
    Service,     //  Small tunnels for maintenance access
    Ventilation  //  Air circulation tunnels
  }
}
