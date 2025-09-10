using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Interfaces;
using HiveMind.Core.ValueObjects;
using System.Security.AccessControl;

namespace HiveMind.Core.Domain.Aggregates
{
  // =====================================================================
  //  Represents the complete nest architecture with chambers and tunnels
  //  This is an aggregate root managing the entire nest structure
  // =====================================================================

  public class NestArchitecture : BaseEntity
  {
    private readonly Dictionary<Guid, NestChamber> _chambers = [];
    private readonly Dictionary<Guid, Tunnel> _tunnels = [];
    private readonly Queue<ConstructionProject> _constructionQueue = [];

    public NestArchitecture(Position centerPosition, double initialChamberVolume = 50.0)
    {
      CenterPosition = centerPosition;

      //  Create initial chamber - every nest starts with one chamber
      NestChamber initialChamber = new(
        ChamberType.QueensChamber,
        centerPosition,
        initialChamberVolume,
        maxCapacity: 20,
        temperature: 22.0,  //  Optimal temperature for queen
        humidity: 0.75      //  Optimal humidity
      );

      _chambers[initialChamber.Id] = initialChamber;
      MainChamberIds.Add(initialChamber.Id);

      UpdateTimestamp();
    }

    public Position CenterPosition { get; }
    public IReadOnlyDictionary<Guid, NestChamber> Chambers => _chambers.AsReadOnly();
    public IReadOnlyDictionary<Guid, Tunnel> Tunnels => _tunnels.AsReadOnly();
    public IReadOnlyCollection<ConstructionProject> ConstructionQueue => _constructionQueue.AsReadOnly();

    //  Key chamber tracking
    public HashSet<Guid> MainChamberIds { get; } = [];
    public HashSet<Guid> EntranceIds { get; } = [];
    public HashSet<Guid> NurseryIds { get; } = [];
    public HashSet<Guid> FoodStorageIds { get; } = [];

    //  Architecture metrics
    public double TotalVolume => _chambers.Values.Sum(c => c.Volume);
    public int TotalCapacity => _chambers.Values.Sum(c => c.MaxCapacity);
    public int MaxDepthLayers { get; private set; } = 1;
    public double AverageStructuralIntegrity => _chambers.Values.Average(c => c.StructuralIntegrity);

    //  Construction management
    public bool IsUnderConstruction => _constructionQueue.Count > 0;
    public double ConstructionProgress { get; private set; } = 1.0;  //  1.0 = fully constructed

    //  Adds a new chamber to the nest
    public void AddChamber(NestChamber chamber, Guid? connectedToChamber = null)
    {
      ArgumentNullException.ThrowIfNull(chamber);

      if (_chambers.ContainsKey(chamber.Id))
        throw new InvalidOperationException($"Chamber {chamber.Id} already exists");

      _chambers[chamber.Id] = chamber;

      //  Track special chamber types
      TrackSpecialChamber(chamber);

      //  Connect to existing chamber if specified
      if (connectedToChamber.HasValue && _chambers.ContainsKey(connectedToChamber.Value))
        ConnectChambers(chamber.Id, connectedToChamber.Value);

      UpdateTimestamp();
    }

    //  Creates a tunnel connecting two chambers
    public void ConnectChambers(Guid chamber1Id, Guid chamber2Id, double? customWidth = null)
    {
      if (!_chambers.TryGetValue(chamber1Id, out var chamber1) ||
          !_chambers.TryGetValue(chamber2Id, out var chamber2))
        throw new ArgumentException("Both chambers must exist");

      if (chamber1.ConnectedChambers.Contains(chamber2Id))
        return; //  Already connected

      double distance = chamber1.CenterPosition.DistanceTo(chamber2.CenterPosition);
      double width = customWidth ?? CalculateOptimalTunnelWidth(chamber1, chamber2);

      Tunnel tunnel = new(chamber1Id, chamber2Id, distance, width);
      _tunnels[tunnel.Id] = tunnel;

      //  Update chamber connections (bidirectional)
      chamber1.ConnectedChambers.Add(chamber2Id);
      chamber2.ConnectedChambers.Add(chamber1Id);

      UpdateTimestamp();
    }

    //  Plans construction of a new chamber based on colony needs
    public void PlanConstruction(ConstructionProject project)
    {
      ArgumentNullException.ThrowIfNull(project);

      //  Validate construction is feasible
      ValidateConstructionProject(project);

      _constructionQueue.Enqueue(project);

      UpdateTimestamp();
    }

    //  Determines the next construction priority based on colony needs
    public ConstructionPriority GetNextConstructionPriority(ColonyNeeds needs)
    {
      ArgumentException.ThrowIfNull(needs);

      //  Priority algorithm based on real ant colony behavior
      if (needs.PopulationGrowthRate > 0.1 && GetChamberCount(ChamberType.Nursery) < needs.EstimatedPopulation / 50)
        return new(ChamberType.Nursery, Priority.High, "Population growth requires more nuseries");
      if (needs.FoodStockLevel < 0.3 && GetChamberCount(ChamberType.FoodStorage) < 2)
        return new(ChamberType.FoodStorage, Priority.High, "Low food reserves require storage expansion");
      if (needs.DefenseThreatLevel > 0.7 && GetChamberCount(ChamberType.Entrance) < 3)
        return new(ChamberType.Entrance, Priority.Medium, "Defense requires multiple escape routes");
      if (needs.WasteAccumulation > 0.8)
        return new(ChamberType.WasteDumps, Priority.Medium, "Sanitation requires waste management");

      return new(ChamberType.EmergencyChamber, Priority.Low, "General expansion");
    }

    //  Updates all chambers and tunnels (maintanance, wear, etc.)
    public void UpdateArchitecture(ISimulationContext context)
    {
      //  Apply natural wear to all structures
      foreach (NestChamber chamber in _chambers.Values)
        chamber.ApplyWear();

      foreach (Tunnel tunnel in _tunnels.Values)
        tunnel.ApplyWear();

      // Process construction if any is queued
      ProcessConstruction(context);

      UpdateTimestamp();
    }

    //  Finds the best path between two chambers
    public List<Guid> FindPath(Guid fromChamber, Guid toChamber)
    {
      if (!_chambers.ContainsKey(fromChamber) || !_chambers.ContainsKey(toChamber))
        return [];

      //  Simple BFS pathfinding - could be enhanced with A* for performance
      HashSet<Guid> visited = [];
      Queue<(Guid chamberId, List<Guid> path)> queue = [];

      queue.Enqueue((fromChamber, new List<Guid> { toChamber }));
      visited.Add(fromChamber);

      while (queue.Count > 0)
      {
        var (currentId, path) = queue.Dequeue();

        if (currentId == toChamber)
          return path;

        var currentChamber = _chambers[currentId];
        foreach (var connectedId in currentChamber.ConnectedChambers)
        {
          if (!visited.Contains(connectedId))
          {
            visited.Add(connectedId);
            var newPath = new List<Guid>(path) { connectedId };
            queue.Enqueue((connectedId, newPath));
          }
        }
      }

      return [];  //  No path found
    }

    // ========================
    //  Private helper methods
    // ========================

    private void TrackSpecialChamber(NestChamber chamber)
    {
      switch (chamber.Type)
      {
        case ChamberType.Entrance:
          EntranceIds.Add(chamber.Id);
          break;
        case ChamberType.Nursery:
          NurseryIds.Add(chamber.Id);
          break;
        case ChamberType.FoodStorage:
          FoodStorageIds.Add(chamber.Id);
          break;
      }
    }

    private int GetChamberCount(ChamberType type) =>
      _chambers.Values.Count(c => c.Type == type);

    private double CalculateOptimalTunnelWidth(NestChamber chamber1, NestChamber chamber2)
    {
      //  Wider tunnels for high-traffic connections
      double baseWidth = 1.0;

      if (chamber1.Type == ChamberType.MainTunnel || chamber2.Type == ChamberType.MainTunnel)
        baseWidth = 2.0;

      if (chamber1.Type == ChamberType.Entrance || chamber2.Type == ChamberType.Entrance)
        baseWidth = 1.5;

      return baseWidth;
    }

    private void ValidateConstructionProject(ConstructionProject project)
    {
      //  Basic validation - could be enhanced
      if (project.ProposedLocation.X < 0 || project.ProposedLocation.Y < 0)
        throw new ArgumentException("Construction location must be valid");

      if (project.EstimatedWorkDays <= 0)
        throw new ArgumentException("Work estimate must be positive");
    }

    private void ProcessConstruction(ISimulationContext context)
    {
      //  Simplified construction processing - would be enhanced with worker allocation
      if (_constructionQueue.Count > 0 && ConstructionProgress >= 1.0)
      {
        var project = _constructionQueue.Dequeue();
        ConstructionProgress = 0.0;

        //  Create new chamber when construction completes
        //  This is simplified - real implementation would track progress over time
      }
    }
  }

  // ===========================================
  //  Represents a planned construction project
  // ===========================================

  public class ConstructionProject(
    ChamberType targetType,
    Position proposedLocation,
    double estimatedWorkDays,
    int requiredWorkers
  )
  {
    public ChamberType TargetType { get; } = targetType;
    public Position ProposedLocation { get; } = proposedLocation;
    public double EstimatedWorkDays { get; } = estimatedWorkDays;
    public int RequiredWorkers { get; } = requiredWorkers;
    public DateTime PlannedAt { get; } = DateTime.UtcNow;
    public List<ResourceRequirement> Materials { get; } = new();
    public Priority Priority { get; set; } = Priority.Medium;
    public string? Notes { get; set; }
  }

  public class ResourceRequirement(string resourceType, double quantity, string unit = "units")
  {
    public string ResourceType { get; } = resourceType;
    public double Quantity { get; } = quantity;
    public string Unit { get; } = unit;
  }

  // ===========================================================
  //  Represents colony needs that drive construction decisions
  // ===========================================================

  public class ColonyWeeds
  {
    public double PopulationGrowthRate { get; set; }
    public double FoodStockLevel { get; set; }
    public double DefenseThreatLevel { get; set; }
    public double WasteAccumulation { get; set; }
    public int EstimatedPopulation { get; set; }
    public double SpaceUtilization { get; set; }
  }

  public class ConstructionPriority(ChamberType chamberType, Priority priority, string reason)
  {
    public ChamberType ChamberType { get; } = chamberType;
    public Priority Priority { get; } = priority;
    public string Reason { get; } = reason;
  }

  public enum Priority
  {
    Low,
    Medium,
    High,
    Critical
  }
}
