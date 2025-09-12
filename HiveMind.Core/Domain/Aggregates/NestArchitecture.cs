using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Interfaces;
using HiveMind.Core.Domain.ValueObjects;

namespace HiveMind.Core.Domain.Aggregates
{
  // --------------------------------------------------------------
  //  Aggregate root managing the complete nest architecture
  //  Enforces business rules and maintains structural consistency
  // --------------------------------------------------------------

  public class NestArchitecture : BaseEntity
  {
    private readonly Dictionary<Guid, NestChamber> _chambers = [];
    private readonly Dictionary<Guid, Tunnel> _tunnels = [];
    private readonly Queue<ConstructionProject> _constructionQueue = [];
    private readonly Dictionary<ChamberType, List<Guid>> _chambersByType = [];

    public NestArchitecture(Position centerPosition, double initialChamberVolume = 60.0)
    {
      CenterPosition = centerPosition;

      //  Every nest starts with a queen's chamber - this is the foundation
      var foundationChamber = new NestChamber(
        ChamberType.QueensChamber,
        centerPosition,
        initialChamberVolume,
        maxCapacity: 25,    //  Queen + attendants
        temperature: 25.0,  //  Optimal for egg development
        humidity: 0.75,     //  Optimal for queen
        depthLayer: 2       //  Slightly underground for protection
      );

      AddChamberInternal(foundationChamber);

      UpdateTimestamp();
    }

    // -----------------
    //  Core properties
    // -----------------

    public Position CenterPosition { get; }
    public IReadOnlyDictionary<Guid, NestChamber> Chambers =>
      _chambers.AsReadOnly();
    public IReadOnlyDictionary<Guid, Tunnel> Tunnels =>
      _tunnels.AsReadOnly();
    public IReadOnlyCollection<ConstructionProject> ConstructionQueue =>
      _constructionQueue.AsReadOnly();

    //  Key chamber tracking
    public HashSet<Guid> MainChamberIds { get; } = [];
    public HashSet<Guid> EntranceIds { get; } = [];
    public HashSet<Guid> NurseryIds { get; } = [];
    public HashSet<Guid> FoodStorageIds { get; } = [];

    //  Architecture metrics
    public double TotalVolume =>
      _chambers.Values.Sum(c => c.Volume);
    public int TotalCapacity =>
      _chambers.Values.Sum(c => c.MaxCapacity);
    public int MaxDepthLayers =>
      _chambers.Values.Count > 0
        ? _chambers.Values.Max(c => c.DepthLayer)
        : 0;
    public double AverageStructuralIntegrity =>
      _chambers.Values.Count > 0
        ? _chambers.Values.Average(c => c.StructuralIntegrity)
        : 1.0;
    public double AverageAirQuality =>
      _chambers.Values.Count > 0
        ? _chambers.Values.Average(c => c.AirQuality)
        : 1.0;

    //  Specialized chamber access
    public NestChamber? QueensChamber =>
      GetChambersByType(ChamberType.QueensChamber)
        .FirstOrDefault();
    public IReadOnlyList<NestChamber> Nurseries =>
      GetChambersByType(ChamberType.Nursery);
    public IReadOnlyList<NestChamber> FoodStorages =>
      GetChambersByType(ChamberType.FoodStorage);
    public IReadOnlyList<NestChamber> Entrances =>
      GetChambersByType<NestChamber>(ChamberType.Entrance);

    //  Construction status
    public bool IsUnderConstruction =>
      _constructionQueue.Count > 0;
    public int PlannedExpansions =>
      _constructionQueue.Count;

    //  Nest health assessment
    public bool RequiresMaintenanceAttention =>
      GetMaintenancePriorityScore() > 2.0;
    public bool HasStructuralIssues =>
      _chambers.Values.Any(c => !c.IsStructurallySound) || _tunnels.Values.Any(t => t.IsEmergencyRepairNeeded);

    //  Adds a new chamber to the nest with validation and business rules
    public void AddChamber(NestChamber chamber, Guid? connectToChamber = null, TunnelType tunnelType = TunnelType.Standard)
    {
      ArgumentNullException.ThrowIfNull(chamber);

      ValidateNewChamber(chamber);
      AddChamberInternal(chamber);

      //  Connect to existing chamber if specified
      if (connectToChamber.HasValue && _chambers.ContainsKey(connectToChamber.Value))
        ConnectChambers(chamber.Id, connectToChamber.Value, tunnelType);
      else if (_chambers.Count > 1)  // Auto-connect to nearest suitable chamber
      {
        var nearestChamber = FindNearestSuitableConnectionPoint(chamber);
        if (nearestChamber != null)
          ConnectChambers(chamber.Id, nearestChamber.Id, tunnelType);
      }

      UpdateTimestamp();
    }

    //  Creates a tunnel connecting two chambers with pathfinding validation
    public void ConnectChambers(Guid chamber1Id, Guid chamber2Id, TunnelType tunnelType = TunnelType.Standard, double? customWidth = null)
    {
      if (!_chambers.TryGetValue(chamber1Id, out var chamber1) ||
          !_chambers.TryGetValue(chamber2Id, out var chamber2))
        throw new ArgumentException("Both chambers must exist in the nest");

      //  Check if already connected
      if (IsDirectlyConnected(chamber1Id, chamber2Id))
        return;  //  Already connected

      ValidateConnection(chamber1, chamber2, tunnelType);

      double distance = chamber1.CenterPosition.DistanceTo(chamber2.CenterPosition);
      double width = customWidth
        ?? CalculateOptimalTunnelWidth(chamber1, chamber2, tunnelType);

      Tunnel tunnel = new(chamber1Id, chamber2Id, distance, width, tunnelType);
      _tunnels[tunnel.Id] = tunnel;

      //  Update chamber connections (bidirectional)
      _chambers[chamber1Id] = chamber1.WithConnection(chamber2Id);
      _chambers[chamber2Id] = chamber2.WithConnection(chamber1Id);

      UpdateTimestamp();
    }

    //  Plans construction based on colony needs and available resources
    public void PlanConstruction(ConstructionProject project)
    {
      ArgumentNullException.ThrowIfNull(project);

      ValidateConstructionProject(project);

      //  Insert based on priority
      var projectsArray = _constructionQueue.ToArray();
      _constructionQueue.Clear();

      bool inserted = false;
      foreach (var existingProject in projectsArray)
      {
        if (!inserted && project.Priority > existingProject.Priority)
        {
          _constructionQueue.Enqueue(project);
          inserted = true;
        }

        _constructionQueue.Enqueue(existingProject);
      }

      if (!inserted)
        _constructionQueue.Enqueue(project);

      UpdateTimestamp();
    }

    //  Determines optimal construction priority based on colony needs
    //  Uses collective intelligence algorithms
    public ConstructionPriority GetNextConstructionPriority(ColonyNeeds needs)
    {
      ArgumentNullException.ThrowIfNull(needs);

      List<(ChamberType type, Priority priority, double score, string reason)> priorities = [];

      //  Critical needs assessment
      if (needs.PopulationGrowthRate > 0.15 && GetChamberCount(ChamberType.Nursery) < needs.EstimatedPopulation / 40)
        priorities.Add((ChamberType.Nursery, Priority.Critical, 10.0,
          $"High population growth ({needs.PopulationGrowthRate:P0}) requires nursery expansion"));
      if (needs.FoodStockLevel < 0.2 && GetChamberCount(ChamberType.FoodStorage) < 2)
        priorities.Add((ChamberType.FoodStorage, Priority.Critical, 9.0,
          $"Critical food shortage ({needs.FoodStockLevel:P0}) requires storage expansion"));

      //  High priority needs
      if (needs.DefenseThreatLevel > 0.7 && GetChamberCount(ChamberType.Entrance) < 3)
        priorities.Add((ChamberType.Entrance, Priority.High, 8.0,
          $"High threat level ({needs.DefenseThreatLevel:P0}) requires multiple escape routes"));
      if (needs.WasteAccumulation > 0.8 && GetChamberCount(ChamberType.WasteDumps) == 0)
        priorities.Add((ChamberType.WasteDumps, Priority.High, 7.5,
          $"High waste accumulation ({needs.WasteAccumulation:P0}) requires sanitation facilities"));
      if (needs.SpaceUtilization > 0.9)
        priorities.Add((ChamberType.EmergencyChamber, Priority.High, 7.0,
          $"Overcrowding ({needs.SpaceUtilization:P0}) requires emergency expansion"));

      //  Medium priority needs
      if (needs.EstimatedPopulation > 100 && GetChamberCount(ChamberType.WorkshopArea) == 0)
        priorities.Add((ChamberType.WorkshopArea, Priority.Medium, 5.0,
          $"Large colony requires construction staging areas"));
      if (HasPoorVentilation())
        priorities.Add((ChamberType.MainTunnel, Priority.Medium, 4.5,
          "Poor air circulation requires ventilation improvement"));

      //  Low priority - optimization and comfort
      if (GetChamberCount(ChamberType.WinterChamber) == 0 && needs.EstimatedPopulation > 50)
        priorities.Add((ChamberType.WinterChamber, Priority.Low, 2.0,
          "Seasonal preparation requires hibernation chambers"));

      //  Select highest priority need
      if (priorities.Count > 1)
      {
        var top = priorities
          .OrderByDescending(p => p.priority.Score)
          .First();

        return new(top.type, top.priority, top.reason);
      }

      return new(ChamberType.EmergencyChamber, Priority.Low, "General expansion");
    }

    //  Updates all chambers and tunnels, processing wear, maintenance, and construction
    public void UpdateArchitecture(ISimulationContext context)
    {
      //  Apply natural wear to all structures
      ApplyStructuralWear();
      //  Update environmental conditions based on ventilation and occupancy
      UpdateEnvironmentalConditions();
      //  Process any ongoing construction
      ProcessConstruction(context);
      //  Optimize traffic flow
      OptimizeTrafficFlow();

      UpdateTimestamp();
    }

    //  Finds optimal path between chambers using A* pathfinding
    //  Considers tunnel capacity, condition, and traffic
    public List<Guid> FindOptimalPath(Guid fromChamber, Guid toChamber, PathFindingOptions? options = null)
    {
      if (!_chambers.ContainsKey(fromChamber) || !_chambers.ContainsKey(toChamber))
        return [];
      if (fromChamber == toChamber)
        return [fromChamber];

      options ??= new PathFindingOptions();

      //  A* pathfinding implementation
      PriorityQueue<Guid, double> openSet = new();
      Dictionary<Guid, Guid> cameFrom = [];
      Dictionary<Guid, double> gScore = [];
      Dictionary<Guid, double> fScore = [];

      gScore[fromChamber] = 0;
      fScore[fromChamber] = HeuristicDistance(fromChamber, toChamber);
      openSet.Enqueue(fromChamber, fScore[fromChamber]);

      while (openSet.Count > 0)
      {
        var current = openSet.Dequeue();
        if (current == toChamber)
          return ReconstructPath(cameFrom, current);

        foreach (var neighborId in _chambers[current].ConnectedChambers)
        {
          var tunnel = GetTunnelBetween(current, neighborId);
          if (tunnel == null)
            continue;

          double tentativeGScore = gScore[current] + CalculateMovementCost(tunnel, options);
          if (!gScore.ContainsKey(neighborId) || tentativeGScore < gScore[neighborId])
          {
            cameFrom[neighborId] = current;
            gScore[neighborId] = tentativeGScore;
            fScore[neighborId] = gScore[neighborId] + HeuristicDistance(neighborId, toChamber);

            openSet.Enqueue(neighborId, fScore[neighborId]);
          }
        }
      }

      return [];  //  No path found
    }

    //  Gets chambers requiring maintenance, ordered by priority
    public IEnumerable<NestChamber> GetChamberMaintenancePriorities() =>
      _chambers.Values
        .Where(c => c.NeedsMaintenance)
        .OrderByDescending(c => c.GetMaintenancePriority());

    //  Gets tunnels requiring maintenance, ordered by priority
    public IEnumerable<Tunnel> GetTunnelMaintenancePriorities() =>
      _tunnels.Values
        .Where(t => t.NeedsMaintenance)
        .OrderByDescending(t => t.GetMaintenancePriority);

    //  Performs maintenance on a chamber
    public bool PerformChamberMaintenance(Guid chamberId, double qualityImprovement = 0.3)
    {
      if (!_chambers.TryGetValue(chamberId, out var chamber))
        return false;

      _chambers[chamberId] = chamber.WithMaintenance(qualityImprovement, qualityImprovement * 0.8);

      UpdateTimestamp();

      return true;
    }

    //  Performs maintenance on a tunnel
    public bool PerformTunnelMaintenance(Guid tunnelId, double improvement = 0.3)
    {
      if (!_tunnels.TryGetValue(tunnelId, out var tunnel))
        return false;

      _tunnels[tunnelId] = tunnel.WithMaintenance(improvement);

      UpdateTimestamp();

      return true;
    }

    //  Updates ocuppancy for a chamber
    public bool UpdateChamberOccupancy(Guid chamberId, int newOcuupantsCount)
    {
      if (!_chambers.TryGetValue(chamberId, out var chamber))
        return false;

      try
      {
        _chambers[chamberId] = chamber.WithOccupants(newOcuupantsCount);

        UpdateTimestamp();

        return true;
      }
      catch (ArgumentException)
      {
        return false;  //  Invalid occupant count
      }
    }

    //  Gets comprehensive nest statistics for analysis
    public NestStatistics GetStatistics() =>
      new()
      {
        TotalChambers = _chambers.Count,
        TotalTunnels = _tunnels.Count,
        TotalVolume = TotalVolume,
        TotalCapacity = TotalCapacity,
        AverageStructuralIntegrity = AverageStructuralIntegrity,
        AverageAirQuality = AverageAirQuality,
        MaxDepth = MaxDepthLayers,
        ChambersByType = _chambersByType.ToDictionary(
          kvp => kvp.Key,
          kvp => kvp.Value.Count
        ),
        MaintenanceBacklog = GetMaintenancePriorities().Count() + GetTunnelMaintenancePriorities().Count(),
        ConstructionBacklog = _constructionQueue.Count,
        VentilationRating = CalculateOverallVentilationRating(),
        TrafficEfficiency = CalculateOverallTrafficEfficiency()
      };

    // --------------------------------
    //  Private implementation methods
    // --------------------------------

    private void AddChamberInternal(NestChamber chamber)
    {
      _chambers[chamber.Id] = chamber;

      if (_chambersByType.ContainsKey(chamber.Type))
        _chambersByType[chamber.Type] = [];

      _chambersByType[chamber.Type].Add(chamber.Id);
    }

    private void ValidateNewChamber(NestChamber chamber)
    {
      //  Only one queens chamber per nest
      if (chamber.Type == ChamberType.QueensChamber && _chambersByType.ContainsKey(ChamberType.QueensChamber))
        throw new InvalidOperationException("Nest can only have one Queen's Chamber");

      //  Check maximum chambers per type
      var attribute = GetChamberTypeInfo(chamber.Type);
      if (attribute != null && GetChamberCount(chamber.Type) >= attribute.MaxPerColony)
        throw new InvalidOperationException($"Maximum number of {chamber.Type} chambers ({attribute.MaxPerColony}) already reached");

      //  Validate chamber placement
      if (!IsValidChamberPlacement(chamber))
        throw new InvalidOperationException("Chamber placement violates nest architecture rules");
    }

    private void ValidateConnection(NestChamber chamber1, NestChamber chamber2, TunnelType tunnelType)
    {
      //  Waste dumps should not connect directly to food storage
      if ((chamber1.Type == ChamberType.WasteDumps && chamber2.Type == ChamberType.FoodStorage) ||
          (chamber1.Type == ChamberType.FoodStorage && chamber2.Type == ChamberType.WasteDumps))
        throw new InvalidOperationException("Waste dumps cannot connect directly to food storage chambers");

      //  Maximum connections per chamber based no type
      var maxConnections = GetMaxConnectionsForChambeType(chamber1.Type);
      if (chamber1.ConnectedChambers.Count >= maxConnections)
        throw new InvalidOperationException($"{chamber1.Type} chambers can have maximum {maxConnections} connections");
    }

    private void ValidateConstructionProject(ConstructionProject project)
    {
      if (project.EstimatedWorkDays <= 0)
        throw new ArgumentException("Work estimate must be positive");
      if (project.RequiredWorkers <= 0)
        throw new ArgumentException("Required workers must be positive");

      //  Validate we don't exceed chamber type limits
      var attribute = GetChamberTypeInfo(project.TargetType);
      if (attribute != null && GetChamberCount(project.TargetType) >= attribute.MaxPerColony)
        throw new InvalidOperationException($"Cannot plan more {project.TargetType} chambers - maximum ({attribute.MaxPerColony}) reached");
    }

    private bool IsValidChamberPlacement(NestChamber chamber)
    {
      //  Check minimum distances between certain chamber types
      Dictionary<(ChamberType, ChamberType), double> minimumDistances = new()
      {
        { (ChamberType.WasteDumps, ChamberType.FoodStorage), 15.0 },
        { (ChamberType.WasteDumps, ChamberType.QueensChamber), 20.0 },
        { (ChamberType.WasteDumps, ChamberType.Nursery), 18.0 }
      };

      foreach (var existingChamber in _chambers.Values)
      {
        var key = (chamber.Type, existingChamber.Type);
        var reverseKey = (existingChamber.Type, chamber.Type);

        if (minimumDistances.TryGetValue(key, out var minDistance) ||
            minimumDistances.TryGetValue(reverseKey, out minDistance))
        {
          var distance = chamber.CenterPosition.DistanceTo(existingChamber.CenterPosition);
          if (distance < minDistance)
            return false;
        }
      }

      return true;
    }

    private NestChamber? FindNearestSuitableConnectionPoint(NestChamber newChamber) =>
      _chambers.Values
        .Where(c => c.Id != newChamber.Id && CanConnect(newChamber, c))
        .OrderBy(c => c.CenterPosition.DistanceTo(newChamber.CenterPosition))
        .FirstOrDefault();

    private bool CanConnect(NestChamber chamber1, NestChamber chamber2)
    {
      //  Check if connection would violate rules
      try
      {
        ValidateConnection(chamber1, chamber2, TunnelType.Standard);

        return true;
      }
      catch
      {
        return false;
      }
    }

    private double CalculateOptimalTunnelWidth(NestChamber chamber1, NestChamber chamber2, TunnelType tunnelType)
    {
      double baseWidth = tunnelType switch
      {
        TunnelType.MainArtery =>
          3.0,
        TunnelType.Emergency =>
          2.5,
        TunnelType.Standard =>
          1.5,
        TunnelType.Service =>
          1.0,
        TunnelType.Ventilation =>
          1.2,
        _ =>
          1.5
      };

      //  Adjust based on chamber types and expected traffic
      var trafficMultiplier = CalculateExpectedTrafficMultiplier(chamber1.Type, chamber2.Type);

      return baseWidth * trafficMultiplier;
    }

    private double CalculateExpectedTrafficMultiplier(ChamberType type1, ChamberType type2)
    {
      var highTrafficTypes = new[] { ChamberType.Entrance, ChamberType.MainTunnel, ChamberType.FoodStorage };

      bool type1HighTraffic = highTrafficTypes.Contains(type1);
      bool type2HighTraffic = highTrafficTypes.Contains(type2);
      if (type1HighTraffic && type2HighTraffic)
        return 1.5;
      if (type1HighTraffic || type2HighTraffic)
        return 1.2;

      return 1.0;
    }

    private void ApplyStructuralWear()
    {
      //  Apply wear to chambers
      Dictionary<Guid, NestChamber> chamberUpdates = [];

      foreach (var (id, chamber) in _chambers)
      {
        double wearRate = CalculateChamberWearRate(chamber);
        chamberUpdates[id] = chamber.WithWear(wearRate, wearRate * 0.7);
      }

      foreach (var (id, updatedChamber) in chamberUpdates)
        _chambers[id] = updatedChamber;

      //  Apply wear to tunnels
      Dictionary<Guid, Tunnel> tunnelUpdates = [];

      foreach (var (id, tunnel) in _tunnels)
      {
        double wearRate = CalculateTunnelWearRate(tunnel);
        tunnelUpdates[id] = tunnel.WithWear(wearRate);
      }

      foreach (var (id, updatedTunnel) in tunnelUpdates)
        _tunnels[id] = updatedTunnel;
    }

    private double CalculateChamberWearRate(NestChamber chamber)
    {
      double baseWear = 0.0005;  //  Base daily wear rate
      double occupancyMultiplier = 1.0 + (chamber.OccupancyRatio * 0.5);  //  More occupants = more wear
      double environmentalMultiplier = 1.0 + chamber.GetEnvironmentalStress() * 0.3;

      return baseWear * occupancyMultiplier * environmentalMultiplier;
    }

    private double CalculateTunnelWearRate(Tunnel tunnel)
    {
      double baseWear = 0.0003;  //  Base daily wear rate for tunnels
      double trafficMultiplier = 1.0 + ((double)tunnel.CurrentTraffic / tunnel.TrafficCapacity);
      double lengthMultiplier = 1.0 + (tunnel.Length / 100.0) * 0.2;  //  Longer tunnels wear slightly faster

      return baseWear * trafficMultiplier * lengthMultiplier;
    }

    private void UpdateEnvironmentalConditions()
    {
      //  Simplified environmental updates - would be enhanced with realistic heat transfer
      foreach (var (id, chamber) in _chambers.ToList())
      {
        double newTemp = CalculateUpdatedTemperature(chamber);
        double newHumidity = CalculateUpdatedHumidity(chamber);

        _chambers[id] = chamber.WithEnvironmentalConditions(newTemp, newHumidity);
      }
    }

    private double CalculateUpdatedTemperature(NestChamber chamber)
    {
      //  Simplified temperature regulation
      double targetTemp = chamber.OptimalTemperature;
      double currentTemp = chamber.Temperature;
      double adjustment = (targetTemp - currentTemp) * 0.1;  //  Gradual adjustment

      //  Ventilation and occupancy effects
      double ventilationCooling = chamber.VentilationRating * 0.5;
      double occupancyHeating = chamber.OccupancyRatio * 2.0;

      return currentTemp + adjustment - ventilationCooling + occupancyHeating;
    }

    private double CalculateUpdatedHumidity(NestChamber chamber)
    {
      //  Simplified humidity regulation  
      double targetHumidity = chamber.OptimalHumidity;
      double currentHumidity = chamber.Humidity;
      double adjustment = (targetHumidity - currentHumidity) * 0.1;

      //  Ventilation reduces humidity
      double ventilationEffect = chamber.VentilationRating * 0.02;

      return Math.Max(0.3, Math.Min(0.95, currentHumidity + adjustment - ventilationEffect));
    }

    private void ProcessConstruction(ISimulationContext context)
    {
      //  Simplified construction processing
      //  In full implementation, this would track progress over time
      if (_constructionQueue.Count > 0)
      {
        var project = _constructionQueue.Peek();

        //  Check if construction can be completed (simplified)
        if (CanCompleteConstruction(project, context))
          CompleteConstruction(_constructionQueue.Dequeue());
      }
    }

    private bool CanCompleteConstruction(ConstructionProject project, ISimulationContext context)
    {
      //  Simplified check - in reality would track worker allocation and time
      return context.CurrentTick % 100 == 0;  //  Complete one project every 100 ticks
    }

    private void CompleteConstruction(ConstructionProject project)
    {
      //  Create the new chamber
      NestChamber newChamber = new(
          project.TargetType,
          project.ProposedLocation,
          project.EstimatedVolume,
          project.EstimatedCapacity,
          project.DepthLayer
      );

      AddChamber(newChamber);
    }

    private void OptimizeTrafficFlow()
    {
      //  Update tunnel traffic based on usage patterns
      foreach (var (id, tunnel) in _tunnels.ToList())
      {
        //  Simplified traffic simulation - would be enhanced with actual ant movement
        int estimatedTraffic = EstimateTunnelTraffic(tunnel);
        _tunnels[id] = tunnel.WithTraffic(estimatedTraffic);
      }
    }

    private int EstimateTunnelTraffic(Tunnel tunnel)
    {
      //  Estimate traffic based on connected chamber types and occupancy
      var chamber1 = _chambers[tunnel.FromChamber];
      var chamber2 = _chambers[tunnel.ToChamber];

      int traffic = (chamber1.CurrentOccupants + chamber2.CurrentOccupants) / 4;

      //  Adjust for chamber types that generate more traffic
      if (chamber1.Type == ChamberType.FoodStorage || chamber2.Type == ChamberType.FoodStorage)
        traffic = (int)(traffic * 1.5);
      if (chamber1.Type == ChamberType.Entrance || chamber2.Type == ChamberType.Entrance)
        traffic = (int)(traffic * 1.3);

      return Math.Min(traffic, tunnel.TrafficCapacity);
    }

    // ----------------
    //  Helper methods
    // ----------------

    private IReadOnlyList<NestChamber> GetChambersByType(ChamberType type)
    {
      if (!_chambersByType.TryGetValue(type, out var chamberIds))
        return [];

      return chamberIds.Select(id => _chambers[id]).ToList().AsReadOnly();
    }

    private int GetChamberCount(ChamberType type) =>
      _chambersByType.TryGetValue(type, out var chamberIds)
        ? chamberIds.Count
        : 0;

    private bool IsDirectlyConnected(Guid chamber1Id, Guid chamber2Id) =>
      _chambers[chamber1Id].ConnectedChambers.Contains(chamber2Id);

    private Tunnel? GetTunnelBetween(Guid chamber1Id, Guid chamber2Id) =>
      _tunnels.Values.FirstOrDefault(t =>
        (t.FromChamber == chamber1Id && t.ToChamber == chamber2Id) ||
        (t.FromChamber == chamber2Id && t.ToChamber == chamber1Id));

    private double HeuristicDistance(Guid fromChamber, Guid toChamber) =>
      _chambers[fromChamber].CenterPosition.DistanceTo(_chambers[toChamber].CenterPosition);

    private double CalculateMovementCost(Tunnel tunnel, PathfindingOptions options)
    {
      double baseCost = tunnel.Length;
      double conditionPenalty = (2.0 - tunnel.StructuralIntegrity) * options.ConditionWeight;
      double trafficPenalty = tunnel.IsCongested
        ? tunnel.Length * 0.5 * options.TrafficWeight
        : 0;

      return baseCost + conditionPenalty + trafficPenalty;
    }

    private List<Guid> ReconstructPath(Dictionary<Guid, Guid> cameFrom, Guid current)
    {
      List<Guid> path = [current];

      while (cameFrom.ContainsKey(current))
      {
        current = cameFrom[current];
        path.Insert(0, current);
      }

      return path;
    }

    private ChamberInfoAttribute? GetChamberTypeInfo(ChamberType type)
    {
      var field = typeof(ChamberType)
        .GetField(type.ToString());

      return field?
        .GetCustomAttributes(typeof(ChamberInfoAttribute), false)
        .FirstOrDefault() as ChamberInfoAttribute;
    }

    private int GetMaxConnectionsForChamberType(ChamberType type) =>
      type switch
      {
        ChamberType.MainTunnel =>
          6,  //  Hub chambers can have many connections
        ChamberType.Entrance =>
          4,  //  Multiple access routes
        ChamberType.FoodStorage =>
          4,  //  Accessible from multiple areas
        ChamberType.QueensChamber =>
          2,  //  Limited access for security
        ChamberType.WasteDumps =>
          2,  //  Isolated for sanitation
        _ =>
          3   //  Standard maximum
      };

    private bool HasPoorVentilation() =>
      _chambers.Values.Any(c => c.VentilationRating < 0.8);

    private double GetMaintenancePriorityScore()
    {
      if (_chambers.Count == 0)
        return 0.0;

      return _chambers.Values
        .Average(c => c.GetMaintenancePriority()) +
        _tunnels.Values
        .Average(t => t.GetMaintenancePriority);
    }

    private double CalculateOverallVentilationRating()
    {
      if (_chambers.Count == 0)
        return 1.0;

      return _chambers.Values.Average(c => c.VentilationRating);
    }

    private double CalculateOverallTrafficEfficiency()
    {
      if (_tunnels.Count == 0)
        return 1.0;

      return _tunnels.Values.Average(t => t.TrafficEfficiency);
    }
  }

  // ------------------------------
  //  Supporting classes and enums
  // ------------------------------

  public class PathfindingOptions
  {
    public double ConditionWeight { get; set; } = 1.0;
    public double TrafficWeight { get; set; } = 0.5;
    public bool AvoidMaintenance { get; set; } = false;
    public bool PreferMainArteries { get; set; } = true;
  }

  public enum Priority
  {
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
  }

  public class NestStatistics
  {
    public int TotalChambers { get; set; }
    public int TotalTunnels { get; set; }
    public double TotalVolume { get; set; }
    public int TotalCapacity { get; set; }
    public double AverageStructuralIntegrity { get; set; }
    public double AverageAirQuality { get; set; }
    public int MaxDepth { get; set; }
    public Dictionary<ChamberType, int> ChambersByType { get; set; } = [];
    public int MaintenanceBacklog { get; set; }
    public int ConstructionBacklog { get; set; }
    public double VentilationRating { get; set; }
    public double TrafficEfficiency { get; set; }
  }

  // -------------------------------------------
  //  Represents a planned construction project
  // -------------------------------------------

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

  // -----------------------------------------------------------
  //  Represents colony needs that drive construction decisions
  // -----------------------------------------------------------

  public class ColonyNeeds
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
}
