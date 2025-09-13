using HiveMind.Core.Domain.Aggregates;
using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;
using HiveMind.Core.Domain.Services;
using HiveMind.Core.Domain.ValueObjects;

namespace HiveMind.Core.Domain.Entities
{
  // ----------------------------------------------------------------------------
  //  Represents ant colony with nest architecture and construction intelligence
  // ----------------------------------------------------------------------------

  public class AntColony : BaseEntity, IColony, IDisposable
  {
    private readonly Dictionary<Guid, Ant> _members = [];
    private readonly ColonyConstructionPlanner _constructionPlanner;
    private readonly Random _random;
    private bool _isInitialized = false;
    private bool _disposed = false;

    public AntColony(Position centerPosition, int maxPopulation = 500, int? randomSeed = null)
    {
      CenterPosition = centerPosition;
      MaxPopulation = maxPopulation;
      _random = randomSeed.HasValue
        ? new(randomSeed.Value)
        : new();
      _constructionPlanner = new(_random);

      //  Initialize with realistic nest architecture
      NestArchitecture = new(centerPosition);

      TotalFoodStored = 150.0;  //  Starting food reserves

      UpdateTimestamp();
    }

    // ----------------------------------
    //  IColony interface implementation
    // ----------------------------------

    public Guid Id { get; } = Guid.NewGuid();
    public InsectType ColonyType =>
      InsectType.Ant;
    public Position CenterPosition { get; }
    public IReadOnlyCollection<IInsect> Members =>
      _members.Values
        .Cast<IInsect>()
        .ToList()
        .AsReadOnly();
    public int Population =>
      _members.Values.Count(ant => ant.IsAlive);
    public double TotalFoodStored { get; private set; }
    public bool IsActive =>
      Population > 0 && HasQueen && !_disposed;

    //  Enhanced colony properties
    public NestArchitecture NestArchitecture { get; }
    public int MaxPopulation { get; }
    public bool HasQueen =>
      _members.Values
        .OfType<QueenAnt>()
        .Any(q => q.IsAlive);
    public bool IsInitialized =>
      _isInitialized;

    //  Colony statistics
    public double AverageAntAge =>
      _members.Values
        .Where(a => a.IsAlive)
        .Average(a => a.AgeDays);
    public double ColonyHealthScore =>
      _members.Values
        .Where(a => a.IsAlive)
        .Average(a => a.Health / a.MaxHealth);
    public double ColonyEnergyScore =>
      _members.Values
        .Where(a => a.IsAlive)
        .Average(a => a.Energy / a.MaxEnergy);

    //  Initializes the colony with starting population and nest structure
    public void Initialize(ISimulationContext context)
    {
      if (_isInitialized)
        throw new InvalidOperationException("Colony has already been initialized");

      ArgumentNullException.ThrowIfNull(context);

      InitializeFoundingPopulation(context);
      _isInitialized = true;

      UpdateTimestamp();
    }

    // ----------------------------------------------------------------
    //  Main colony update with biological and architectural processes
    // ----------------------------------------------------------------

    public void Update(ISimulationContext context)
    {
      ThrowIfDisposed();

      if (!_isInitialized)
        throw new InvalidOperationException("Colony must be initialized before updating");
      if (!IsActive)
        return;

      //  Update all colony members
      UpdateColonyMembers(context);
      //  Remove dead ants and track lifecycle events
      ProcessLifecycleEvents();
      //  Update nest architecture
      NestArchitecture.UpdateArchitecture(context);
      //  Colony-level decision making and management
      ProcessColonyManagement(context);
      //  Construction planning and execution
      ProcessConstruction(context);

      UpdateTimestamp();
    }

    //  Adds food to colony stores with storage constraints
    public void AddFood(double amount)
    {
      if (amount <= 0 || _disposed)
        return;

      double maxStorageCapacity = CalculateMaxFoodStorage();
      double availableStorage = Math.Max(0, maxStorageCapacity - TotalFoodStored);
      double actualAmount = Math.Min(amount, availableStorage);
      TotalFoodStored += actualAmount;

      UpdateTimestamp();
    }

    //  Consumes food from colony stores
    public bool ConsumeFood(double amount)
    {
      if (amount <= 0 || _disposed)
        return false;

      if (TotalFoodStored >= amount)
      {
        TotalFoodStored -= amount;
        UpdateTimestamp();
        return true;
      }

      return false;
    }

    //  Adds new member to colony with nest assignment
    public void AddMember(IInsect insect)
    {
      if (insect is Ant ant && _members.Count < MaxPopulation && !_disposed)
      {
        _members[ant.Id] = ant;
        AssignAntToOptimalChamber(ant);

        UpdateTimestamp();
      }
    }

    //  Removes member from colony
    public void RemoveMember(Guid insectId)
    {
      if (_members.Remove(insectId))
        UpdateTimestamp();
    }

    //  Gets detailed colony needs analysis for construction planning
    public ColonyNeeds AssessColonyNeeds()
    {
      var livingAnts = _members.Values
        .Where(a => a.IsAlive)
        .ToList();

      return new()
      {
        PopulationGrowthRate = CalculatePopulationGrowthRate(),
        FoodStockLevel = TotalFoodStored / Math.Max(1, CalculateMaxFoodStorage()),
        DefenseThreatLevel = CalculateDefenseThreatLevel(),
        WasteAccumulation = CalculateWasteAccumulation(),
        EstimatedPopulation = Population,
        SpaceUtilization = CalculateSpaceUtilization(),
        EnvironmentalStress = CalculateEnvironmentalStress(),
        ResourceAvailability = CalculateResourceAvailability()
      };
    }

    //  Gets construction recommendations based on current colony state
    public ConstructionRecommendation GetConstructionRecommendations()
    {
      var needs = AssessColonyNeeds();
      var availableWorkers = _members.Values
        .OfType<WorkerAnt>()
        .Where(w => w.IsProductive);

      return _constructionPlanner.AnalyzeConstructionNeeds(NestArchitecture, needs, availableWorkers);
    }

    // --------------------------------
    //  Private implementation methods
    // --------------------------------

    private void InitializeFoundingPopulation(ISimulationContext context)
    {
      //  Create founding queen
      var queen = new QueenAnt(CenterPosition, this, context.CurrentTick);
      AddMember(queen);

      //  Create initial worker population
      for (int i = 0; i < 12; i++)
      {
        var position = GeneratePositionNearCenter(5.0);
        var worker = new WorkerAnt(position, this, context.CurrentTick);
        AddMember(worker);
      }

      //  Create initial foragers
      for (int i = 0; i < 6; i++)
      {
        var position = GeneratePositionNearCenter(3.0);
        var forager = new ForagerAnt(position, this, context.CurrentTick);
        AddMember(forager);
      }

      //  Create a soldier for defense
      var soldierPosition = GeneratePositionNearCenter(4.0);
      var soldier = new SoldierAnt(soldierPosition, this, context.CurrentTick);
      AddMember(soldier);
    }

    private void UpdateColonyMembers(ISimulationContext context)
    {
      //  Update in parallel for performance with large colonies
      var livingAnts = _members.Values
        .Where(a => a.IsAlive)
        .ToList();

      foreach (var ant in livingAnts)
      {
        try
        {
          ant.Update(context);
        }
        catch (Exception ex)
        {
          //  Log ant update error but continue with others
          //  In production, would use proper logging
          Console.WriteLine($"Error updating ant {ant.Id}: {ex.Message}");
        }
      }
    }

    private void ProcessLifecycleEvents()
    {
      var deadAnts = _members.Values
        .Where(a => !a.IsAlive)
        .ToList();

      foreach (var deadAnt in deadAnts)
      {
        _members.Remove(deadAnt.Id);
        //  Could trigger waste accumulation or disease spread here
      }
    }

    private void ProcessColonyManagement(ISimulationContext context)
    {
      //  Natural food consumption
      double consumptionRate = Population * 0.08;  //  Base consumption per ant
      ConsumeFood(consumptionRate);
      //  Queen reproduction
      ProcessQueenReproduction(context);
      //  Worker task allocation
      ProcessWorkerTaskAllocation();
      //  Chamber occupancy management
      UpdateChamberOccupancy();
    }

    private void ProcessQueenReproduction(ISimulationContext context)
    {
      var queen = _members.Values
        .OfType<QueenAnt>()
        .FirstOrDefault(q => q.IsAlive);
      if (queen == null || Population >= MaxPopulation)
        return;

      //  Queens lay eggs based on colony needs and their condition
      if (queen.CanLayEggs && TotalFoodStored > Population * 2)
      {
        int desiredEggs = CalculateDesiredEggProduction();
        int eggsLaid = queen.LayEggs(desiredEggs);
        if (eggsLaid > 0)
        {
          ConsumeFood(eggsLaid * 1.5);  //  Food cost for egg production

          //  Create new ants (simplified - in reality would track egg development)
          for (int i = 0; i < eggsLaid && _members.Count < MaxPopulation; i++)
          {
            var newAntRole = DetermineNewAntRole();
            var position = GeneratePositionNearCenter(2.0);
            var newAnt = EnhancedAntBehaviorFactory.CreateEnhancedAnt(newAntRole, position, this, context.CurrentTick);
            AddMember(newAnt);
          }
        }
      }
    }

    private void ProcessWorkerTaskAllocation()
    {
      //  Simplified task allocation - would be enhanced with actual work queue system
      var workers = _members.Values
        .OfType<WorkerAnt>()
        .Where(w => w.IsProductive)
        .ToList();
      foreach (var worker in workers)
      {
        //  Workers gain experience and develop specializations based on colony needs
        var colonyNeeds = AssessColonyNeeds();
        if (colonyNeeds.FoodStockLevel < 0.3 && worker.GetTaskEfficiency(WorkerSpecialization.FoodProcessing) > 1.0)
          worker.CompleteTask(WorkerSpecialization.FoodProcessing, 1.0);
        else if (NestArchitecture.RequiresMaintenanceAttention && worker.GetTaskEfficiency(WorkerSpecialization.Maintenance) > 1.0)
          worker.CompleteTask(WorkerSpecialization.Maintenance, 1.0);
        else if (colonyNeeds.PopulationGrowthRate > 0.1 && worker.GetTaskEfficiency(WorkerSpecialization.Nursing) > 1.0)
          worker.CompleteTask(WorkerSpecialization.Nursing, 1.0);
      }
    }

    private void ProcessConstruction(ISimulationContext context)
    {
      //  Get construction recommendations
      var recommendations = GetConstructionRecommendations();
      //  Plan high-priority construction projects
      foreach (var recommendation in recommendations.RecommendedProjects
        .Where(r => r.Project.Priority >= Priority.High)
        .Take(2))  //  Limit concurrent projects
      {
        try
        {
          NestArchitecture.PlanConstruction(recommendation.Project);
        }
        catch (Exception ex)
        {
          //  Log construction planning error
          Console.WriteLine($"Failed to plan construction: {ex.Message}");
        }
      }
    }

    private void UpdateChamberOccupancy()
    {
      //  Reset all chamber occupancy
      foreach (var chamber in NestArchitecture.Chambers.Values)
        NestArchitecture.UpdateChamberOccupancy(chamber.Id, 0);

      //  Assign ants to chambers based on current activity and preferences
      foreach (var ant in _members.Values.Where(a => a.IsAlive))
      {
        var optimalChamber = FindOptimalChamberForAnt(ant);
        if (optimalChamber != null)
        {
          var currentOccupancy = optimalChamber.CurrentOccupants;
          NestArchitecture.UpdateChamberOccupancy(optimalChamber.Id, currentOccupancy + 1);
        }
      }
    }

    private void AssignAntToOptimalChamber(Ant ant)
    {
      var optimalChamber = FindOptimalChamberForAnt(ant);
      if (optimalChamber != null && optimalChamber.CanAccommodateAdditionalOccupants(1))
        NestArchitecture.UpdateChamberOccupancy(optimalChamber.Id, optimalChamber.CurrentOccupants + 1);
    }

    private NestChamber? FindOptimalChamberForAnt(Ant ant)
    {
      //  Assign ants to appropriate chambers based on their caste and current state
      var preferredChamberTypes = ant.Caste switch
      {
        AntCaste.Queen =>
          [ChamberType.QueensChamber],
        AntCaste.Worker =>
          [ChamberType.WorkshopArea, ChamberType.MainTunnel, ChamberType.EmergencyChamber],
        AntCaste.Forager =>
          [ChamberType.FoodStorage, ChamberType.Entrance, ChamberType.MainTunnel],
        AntCaste.Soldier =>
          [ChamberType.Entrance, ChamberType.MainTunnel, ChamberType.EmergencyChamber],
        AntCaste.Nurse =>
          [ChamberType.Nursery, ChamberType.QueensChamber],
        AntCaste.Builder =>
          [ChamberType.WorkshopArea, ChamberType.MainTunnel],
        _ =>
          new[] { ChamberType.EmergencyChamber }
      };

      foreach (var chamberType in preferredChamberTypes)
      {
        var chamber = NestArchitecture.Chambers.Values
          .Where(c => c.Type == chamberType && c.CanAccommodateAdditionalOccupants(1))
          .OrderBy(c => c.OccupancyRatio)
          .FirstOrDefault();

        if (chamber != null)
          return chamber;
      }

      return null;
    }

    // ----------------------------
    //  Calculation helper methods
    // ----------------------------

    private double CalculatePopulationGrowthRate()
    {
      //  Simplified growth rate calculation
      //  Would track historical population data in full implementation
      var queen = _members.Values
        .OfType<QueenAnt>()
        .FirstOrDefault(q => q.IsAlive);

      return queen?.EggsLaidToday > 0
        ? 0.15
        : 0.05;
    }

    private double CalculateDefenseThreatLevel()
    {
      //  Simplified threat assessment
      //  Would integrate with environmental threats in full implementation
      var soldiers = _members.Values
        .OfType<SoldierAnt>()
        .Where(s => s.IsAlive)
        .ToList();
      double soldierRatio = soldiers.Count / (double)Math.Max(1, Population);

      return soldierRatio < 0.1
        ? 0.7
        : 0.3;  //  High threat if insufficient defenders
    }

    private double CalculateWasteAccumulation()
    {
      //  Waste accumulates over time without proper waste management
      bool hasWasteFacilities = NestArchitecture.Chambers.Values.Any(c => c.Type == ChamberType.WasteDumps);
      double baseWaste = Population * 0.01;  //  Base waste per ant

      return hasWasteFacilities
        ? Math.Min(0.5, baseWaste)
        : Math.Min(1.0, baseWaste * 2);
    }

    private double CalculateSpaceUtilization()
    {
      int totalOccupancy = NestArchitecture.Chambers.Values.Sum(c => c.CurrentOccupants);
      int totalCapacity = Math.Max(1, NestArchitecture.TotalCapacity);

      return (double)totalOccupancy / totalCapacity;
    }

    private double CalculateEnvironmentalStress()
    {
      double avgStructuralIntegrity = NestArchitecture.AverageStructuralIntegrity;
      double avgAirQuality = NestArchitecture.AverageAirQuality;

      return 1.0 - ((avgStructuralIntegrity + avgAirQuality) / 2.0);
    }

    private double CalculateResourceAvailability()
    {
      var workers = _members.Values
        .OfType<WorkerAnt>()
        .Where(w => w.IsProductive)
        .Count();
      double workerRatio = workers / (double)Math.Max(1, Population);
      double foodRatio = TotalFoodStored / Math.Max(1, Population * 5);  //  5 units per ant is comfortable

      return Math.Min(1.0, (workerRatio + foodRatio) / 2.0);
    }

    private double CalculateMaxFoodStorage()
    {
      double baseStorage = 50.0;
      double storageBonus = NestArchitecture.FoodStorages.Sum(fs => fs.FoodStorageCapacity);

      return baseStorage + storageBonus;
    }

    private int CalculateDesiredEggProduction()
    {
      var needs = AssessColonyNeeds();
      int baseEggs = 10;

      //  Increase egg production based on colony needs
      if (needs.PopulationGrowthRate > 0.1)
        baseEggs += 15;
      if (needs.SpaceUtilization < 0.7)
        baseEggs += 10;
      if (needs.FoodStockLevel > 0.6)
        baseEggs += 8;

      return Math.Min(50, baseEggs);  //  Cap at 50 eggs per cycle
    }

    private AntCaste DetermineNewAntRole()
    {
      var needs = AssessColonyNeeds();
      var currentDistribution = GetCurrentCasteDistribution();

      //  Determine what type of ant is most needed
      if (currentDistribution.GetValueOrDefault(AntCaste.Forager, 0) < Population * 0.3 &&
          needs.FoodStockLevel < 0.4)
        return AntCaste.Forager;
      if (currentDistribution.GetValueOrDefault(AntCaste.Soldier, 0) < Population * 0.15 &&
          needs.DefenseThreatLevel > 0.5)
        return AntCaste.Soldier;
      if (currentDistribution.GetValueOrDefault(AntCaste.Nurse, 0) < Population * 0.1 &&
          needs.PopulationGrowthRate > 0.1)
        return AntCaste.Nurse;
      if (currentDistribution.GetValueOrDefault(AntCaste.Builder, 0) < Population * 0.1 &&
          NestArchitecture.IsUnderConstruction)
        return AntCaste.Builder;

      return AntCaste.Worker;  //  Default to worker
    }

    private Dictionary<AntCaste, int> GetCurrentRoleDistribution() =>
      _members.Values
        .Where(a => a.IsAlive)
        .GroupBy(a => a.Caste)
        .ToDictionary(g => g.Key, g => g.Count());

    private Position GeneratePositionNearCenter(double radius)
    {
      double angle = _random.NextDouble() * 2 * Math.PI;
      double distance = _random.NextDouble() * radius;

      double x = CenterPosition.X + Math.Cos(angle) * distance;
      double y = CenterPosition.Y + Math.Sin(angle) * distance;

      return new(x, y);
    }

    private void ThrowIfDisposed() =>
      ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
      if (!_disposed)
      {
        _members.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
      }
    }

    public override string ToString() =>
      $"Enhanced Colony [{Id:N}] - Population: {Population}, Chambers: {NestArchitecture.Chambers.Count}, Food: {TotalFoodStored:F1}";
  }
}
