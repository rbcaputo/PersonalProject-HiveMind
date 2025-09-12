using HiveMind.Core.Domain.Aggregates;
using HiveMind.Core.Domain.Common;

namespace HiveMind.Core.Domain.ValueObjects
{
  // --------------------------------------------------------------------------------
  //  Represents a planned construction project with realistic resource requirements
  // --------------------------------------------------------------------------------

  public sealed record ConstructionProject
  {
    public ConstructionProject(
      ChamberType targetType,
      Position proposedLocation,
      double estimatedWorkDays,
      int requiredWorkers,
      Priority priority = Priority.Medium
    )
    {
      Id = Guid.NewGuid();
      TargetType = targetType;
      ProposedLocation = proposedLocation;
      EstimatedWorkDays = ValidatePositive(estimatedWorkDays, nameof(estimatedWorkDays));
      RequiredWorkers = ValidatePositive(requiredWorkers, nameof(requiredWorkers));
      Priority = priority;
      PlannedAt = DateTime.UtcNow;

      //  Calculate derived properties
      EstimatedVolume = CalculateEstimatedVolume();
      EstimatedCapacity = CalculateEstimatedCapacity();
      DepthLayer = CalculateOptimalDepth();
      MaterialRequirements = CalculateMaterialRequirements();
    }

    // -------------------------
    //  Core project properties
    // -------------------------

    public Guid Id { get; }
    public ChamberType TargetType { get; }
    public Position ProposedLocation { get; }
    public double EstimatedWorkDays { get; }
    public int RequiredWorkers { get; }
    public Priority Priority { get; init; }
    public DateTime PlannedAt { get; }

    //  Calculated properties

    public double EstimatedVolume { get; }
    public int EstimatedCapacity { get; }
    public int DepthLayer { get; }
    public IReadOnlyList<ResourceRequirement> MaterialRequirements { get; }

    //  Project status

    public ConstructionStatus Status { get; init; } = ConstructionStatus.Planned;
    public double CompletionPercentage { get; init; } = 0.0;
    public int WorkersAssigned { get; init; } = 0;
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }

    //  Cost analysis
    public double TotalEnergyCost =>
      EstimatedWorkDays * RequiredWorkers * 15.0;  //  Energy cost per worker-day
    public double TotalMaterialCost =>
      MaterialRequirements.Sum(r => r.Quantity * r.UnitCost);
    public double TotalProjectCost =>
      TotalEnergyCost + TotalMaterialCost;

    //  Project management
    public bool IsReadyToStart =>
      Status == ConstructionStatus.Planned && WorkersAssigned >= RequiredWorkers;
    public bool IsInProgress =>
      Status == ConstructionStatus.InProgress;
    public bool IsCompleted =>
      Status == ConstructionStatus.Completed;
    public bool IsOverdue =>
      StartedAt.HasValue &&
      DateTime.UtcNow > StartedAt.Value.AddDays(EstimatedWorkDays * 1.2);

    public ConstructionProject WithStatus(ConstructionStatus newStatus, double? completion = null) =>
      this with
      {
        Status = newStatus,
        CompletionPercentage = completion ?? CompletionPercentage,
        StartedAt = newStatus == ConstructionStatus.InProgress && !StartedAt.HasValue
          ? DateTime.UtcNow
          : StartedAt,
        CompletedAt = newStatus == ConstructionStatus.Completed
          ? DateTime.UtcNow
          : CompletedAt
      };

    public ConstructionProject WithWorkers(int assignedWorkers) =>
      this with
      {
        WorkersAssigned = Math.Max(0, assignedWorkers)
      };

    public ConstructionProject WithPriority(Priority newPriority) =>
      this with
      {
        Priority = newPriority
      };

    // -----------------------------
    //  Private calculation methods
    // -----------------------------

    private double CalculateEstimatedVolume() =>
      TargetType switch
      {
        ChamberType.QueensChamber =>
          80.0,  //  Large chamber for queen and attendants
        ChamberType.Nursery =>
          60.0,  //  Good size for brood care
        ChamberType.FoodStorage =>
          100.0, //  Large for food stockpiling
        ChamberType.MainTunnel =>
          40.0,  //  Wide corridor space
        ChamberType.Entrance =>
          30.0,  //  Smaller entry chamber
        ChamberType.WasteDumps =>
          45.0,  //  Medium size for waste management
        ChamberType.WorkshopArea =>
          70.0,  //  Space for construction materials
        ChamberType.WinterChamber =>
          90.0,  //  Large for colony hibernation
        ChamberType.EmergencyChamber =>
          50.0,  //  Standard backup space
        ChamberType.FungusGarden =>
          85.0,  //  Large for fungus cultivation
        _ =>
          50.0   //  Default chamber size
      };

    private int CalculateEstimatedCapacity() =>
      TargetType switch
      {
        ChamberType.QueensChamber =>
          25,  //  Queen + royal attendants
        ChamberType.Nursery =>
          40,  //  High capacity for brood
        ChamberType.FoodStorage =>
          20,  //  Lower capacity, more space for food
        ChamberType.MainTunnel =>
          60,  //  High throughput corridor
        ChamberType.Entrance =>
          30,  //  Guard capacity
        ChamberType.WasteDumps =>
          15,  //  Few workers needed
        ChamberType.WorkshopArea =>
          35,  //  Construction teams
        ChamberType.WinterChamber =>
          80,  //  Many ants during hibernation
        ChamberType.EmergencyChamber =>
          45,  //  Emergency capacity
        ChamberType.FungusGarden =>
          25,  //  Specialized fungus tenders
        _ =>
          30   //  Default capacity
      };

    private int CalculateOptimalDepth() =>
      TargetType switch
      {
        ChamberType.Entrance =>
          1,  //  Surface level
        ChamberType.MainTunnel =>
          2,  //  Shallow for easy access
        ChamberType.QueensChamber =>
          4,  //  Deep for protection
        ChamberType.Nursery =>
          3,  //  Moderate depth for safety
        ChamberType.FoodStorage =>
          3,  //  Cool storage depth
        ChamberType.WasteDumps =>
          5,  //  Deep to isolate waste
        ChamberType.WinterChamber =>
          6,  //  Deepest for temperature stability
        ChamberType.WorkshopArea =>
          2,  //  Accessible depth
        ChamberType.EmergencyChamber =>
          4,  //  Safe depth
        ChamberType.FungusGarden =>
          3,  //  Optimal depth for cultivation
        _ =>
          3   //  Default depth
      };

    private List<ResourceRequirement> CalculateMaterialRequirements()
    {
      List<ResourceRequirement> requirements = [];

      //  Basic excavation materials (all chambers need these)
      requirements.Add(new ResourceRequirement("Soil Excavation", EstimatedVolume, "cubic_units", 0.1));
      requirements.Add(new ResourceRequirement("Structural Support", EstimatedVolume * 0.1, "units", 0.5));

      //  Chamber-specific materials
      switch (TargetType)
      {
        case ChamberType.QueensChamber:
          requirements.Add(new ResourceRequirement("Royal Lining", EstimatedVolume * 0.2, "units", 1.0));
          requirements.Add(new ResourceRequirement("Temperature Control", 5, "units", 2.0));
          break;

        case ChamberType.Nursery:
          requirements.Add(new ResourceRequirement("Soft Lining", EstimatedVolume * 0.3, "units", 0.8));
          requirements.Add(new ResourceRequirement("Humidity Control", 3, "units", 1.5));
          break;

        case ChamberType.FoodStorage:
          requirements.Add(new ResourceRequirement("Moisture Barrier", EstimatedVolume * 0.5, "units", 0.7));
          requirements.Add(new ResourceRequirement("Storage Containers", 10, "units", 1.2));
          break;

        case ChamberType.WasteDumps:
          requirements.Add(new ResourceRequirement("Waste Isolation", EstimatedVolume * 0.4, "units", 0.6));
          requirements.Add(new ResourceRequirement("Drainage System", 2, "units", 2.5));
          break;

        case ChamberType.MainTunnel:
          requirements.Add(new ResourceRequirement("Traffic Reinforcement", EstimatedVolume * 0.6, "units", 0.9));
          requirements.Add(new ResourceRequirement("Ventilation Channels", 4, "units", 1.8));
          break;

        case ChamberType.Entrance:
          requirements.Add(new ResourceRequirement("Security Features", 3, "units", 2.2));
          requirements.Add(new ResourceRequirement("Weather Protection", 2, "units", 1.5));
          break;

        case ChamberType.WorkshopArea:
          requirements.Add(new ResourceRequirement("Tool Storage", 5, "units", 1.0));
          requirements.Add(new ResourceRequirement("Work Surfaces", 3, "units", 1.3));
          break;

        case ChamberType.WinterChamber:
          requirements.Add(new ResourceRequirement("Insulation", EstimatedVolume * 0.8, "units", 1.1));
          requirements.Add(new ResourceRequirement("Deep Foundation", 2, "units", 3.0));
          break;
      }

      return [.. requirements.AsReadOnly()];
    }

    private static double ValidatePositive(double value, string paramName) =>
      value > 0
        ? value
        : throw new ArgumentException($"{paramName} must be positive", paramName);

    private static int ValidatePositive(int value, string paramName) =>
      value > 0
        ? value
        : throw new ArgumentException($"{paramName} must be positive", paramName);

    public override string ToString() =>
      $"{TargetType} Construction [{Priority}] - {CompletionPercentage:P0} complete, {WorkersAssigned}/{RequiredWorkers} workers";
  }

  public class ResourceRequirement(string resourceType, double quantity, string unit, double unitCost)
  {
    public string ResourceType { get; } = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
    public double Quantity { get; } = quantity >= 0
      ? quantity
      : throw new ArgumentException("Quantity cannot be negative");
    public string Unit { get; } = unit ?? throw new ArgumentNullException(nameof(unit));
    public double UnitCost { get; } = unitCost >= 0
      ? unitCost
      : throw new ArgumentException("Unit cost cannot be negative");
    public double TotalCost =>
      Quantity * UnitCost;

    public override string ToString() =>
      $"{Quantity:F1} {Unit} of {ResourceType} (${TotalCost:F2})";
  }

  public enum ConstructionStatus
  {
    Planned,     //  Project is planned but not started
    InProgress,  //  Construction is ongoing
    Paused,      //  Construction temporarily halted
    Completed,   //  Construction finished successfully
    Cancelled,   //  Project cancelled
    Failed       //  Construction failed due to problems
  }
}
