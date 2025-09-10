using HiveMind.Core.Domain.Behaviors;
using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Entities
{
  /// <summary>
  /// Represents an ant colony with its members and collective behaviors
  /// </summary>
  public class AntColony(Position centerPosition, int maxPopulation = 500) : BaseEntity, IColony
  {
    private readonly Dictionary<Guid, Ant> _members = [];
    private readonly List<Position> _nestLocations = [centerPosition];
    private readonly Dictionary<AntCaste, List<Ant>> _roleCache = [];
    private long _lastCacheUpdate = -1;
    private bool _isInitialized = false;

    public InsectType ColonyType => InsectType.Ant;
    public Position CenterPosition { get; private set; } = centerPosition;
    public IReadOnlyCollection<IInsect> Members => [.. _members.Values.Cast<IInsect>()];
    public int Population => _members.Count(kv => kv.Value.IsAlive);
    public double TotalFoodStored { get; private set; } = 100.0; // Starting food
    public bool IsActive => Population > 0 && HasQueen;
    public bool HasQueen => _members.Values.Any(ant => ant.Role == AntCaste.Queen && ant.IsAlive);

    // Colony-specific properties
    public int MaxPopulation { get; private set; } = maxPopulation;
    public double ExpansionRadius { get; private set; } = 50.0;
    public IReadOnlyCollection<Position> NestLocations => _nestLocations.AsReadOnly();

    public void Initialize(ISimulationContext context)
    {
      if (_isInitialized)
        throw new InvalidOperationException("Colony has already been initialized");
      
      ArgumentNullException.ThrowIfNull(context);

      InitializeColony(context);
      _isInitialized = true;
    }

    /// <summary>
    /// Checks if colony is properly initialized before operations
    /// </summary>
    public bool IsInitialized => _isInitialized;

    public void AddMember(IInsect insect)
    {
      if (insect is Ant ant && _members.Count < MaxPopulation)
      {
        _members[ant.Id] = ant;
        UpdateTimestamp();
      }
    }

    public IEnumerable<Ant> GetAntsByRole(AntCaste role)
    {
      // Update cache only when needed
      if (_lastCacheUpdate < LastUpdatedAt.Ticks)
      {
        RefreshRoleCache();
        _lastCacheUpdate = LastUpdatedAt.Ticks;
      }

      return _roleCache.TryGetValue(role, out var ants)
        ? ants
        : Enumerable.Empty<Ant>();
    }

    public void RefreshRoleCache()
    {
      _roleCache.Clear();

      foreach (var ant in _members.Values.Where(a => a.IsAlive))
      {
        if (!_roleCache.ContainsKey(ant.Role))
          _roleCache[ant.Role] = [];

        _roleCache[ant.Role].Add(ant);
      }
    }

    public void RemoveMember(Guid insectId)
    {
      if (!_members.ContainsKey(insectId))
        return;

      _members.Remove(insectId);
      UpdateTimestamp();
    }

    public void Update(ISimulationContext context)
    {
      if (!_isInitialized)
        throw new InvalidOperationException("Colony must be initialized before updating");
      if (!IsActive)
        return;

      // Single-pass update with combined operations
      List<Guid> deadAnts = [];

      foreach (Ant ant in _members.Values)
      {
        if (ant.IsAlive)
        {
          ant.Update(context);

          // Handle food collection inline
          if (ant.Role == AntCaste.Forager && ant.CarriedFood > 0)
          {
            double distanceToNest = ant.Position.DistanceTo(CenterPosition);
            if (distanceToNest < 2.0)
            {
              double droppedFood = ant.DropFood();
              TotalFoodStored += droppedFood;
            }
          }
        }
        else
          deadAnts.Add(ant.Id); // Collect dead ants for removal
      }

      // Remove dead ants in single operation
      foreach (Guid deadAntId in deadAnts)
        _members.Remove(deadAntId);

      // Colony-level behaviors
      ManagePopulation(context);
      ManageFood();
      ExpandTerritory(context);

      UpdateTimestamp();
    }

    public void AddFood(double amount) =>
      TotalFoodStored += amount;

    public bool ConsumeFood(double amount)
    {
      if (TotalFoodStored >= amount)
      {
        TotalFoodStored -= amount;
        
        return true;
      }

      return false;
    }

    public Position GetNearestNest(Position position) =>
      _nestLocations.OrderBy(nest => nest.DistanceTo(position)).First();

    private void InitializeColony(ISimulationContext context)
    {
      // Create queen
      Ant queen = new(AntCaste.Queen, CenterPosition, AntBehaviorFactory.CreateBehavior(AntCaste.Queen), this);
      AddMember(queen);

      // Create initial workers
      for (int i = 0; i < 10; i++)
      {
        Position position = GetRandomPositionNearCenter(5.0, context);
        Ant worker = new(AntCaste.Worker, position, AntBehaviorFactory.CreateBehavior(AntCaste.Worker), this);
        AddMember(worker);
      }

      // Create initial foragers
      for (int i = 0; i < 5; i++)
      {
        Position position = GetRandomPositionNearCenter(3.0, context);
        Ant forager = new(AntCaste.Forager, position, AntBehaviorFactory.CreateBehavior(AntCaste.Forager), this);
        AddMember(forager);
      }
    }

    private void ManagePopulation(ISimulationContext context)
    {
      if (!HasQueen || Population >= MaxPopulation)
        return;

      // Simple reproduction logic - queen spawns new ants periodically
      if (context.CurrentTick % 100 == 0 && TotalFoodStored > 50)
        SpawnNewAnt(context);
    }

    private void ManageFood()
    {
      // Colony consumes food over time
      double consumptionRate = Population * 0.05;
      TotalFoodStored = Math.Max(0, TotalFoodStored - consumptionRate);

      // If food is low, send more foragers
      if (TotalFoodStored < Population * 2)
        DispatchForagers();
    }

    private void ExpandTerritory(ISimulationContext context)
    {
      // Simple territory expansion when population grows
      if (Population > _nestLocations.Count * 50 && context.CurrentTick % 500 == 0)
      {
        Position newNestPosition = GetRandomPositionNearCenter(ExpansionRadius);
        _nestLocations.Add(newNestPosition);
      }
    }

    private void SpawnNewAnt(ISimulationContext context)
    {
      AntCaste role = DetermineNewAntRole();
      Position position = GetRandomPositionNearCenter(2.0, context);

      Ant newAnt = new(role, position, AntBehaviorFactory.CreateBehavior(role), this);
      AddMember(newAnt);

      // Consume food for reproduction
      ConsumeFood(10.0);
    }

    private AntCaste DetermineNewAntRole()
    {
      int foragerCount = GetAntsByRole(AntCaste.Forager).Count();
      int soldierCount = GetAntsByRole(AntCaste.Soldier).Count();

      // Simple role distribution logic
      if (foragerCount < Population * 0.3)
        return AntCaste.Forager;
      if (soldierCount < Population * 0.1)
        return AntCaste.Soldier;

      return AntCaste.Worker;
    }

    private void DispatchForagers()
    {
      IEnumerable<Ant> idleForagers = GetAntsByRole(AntCaste.Forager)
        .Where(ant => ant.CurrentState == ActivityState.Idle)
        .Take(3);

      // Convert some workers to foragers temporarily
      // This would require more sophisticated role switching in a full implementation
    }

    private Position GetRandomPositionNearCenter(double radius, ISimulationContext? context = null)
    {
      Random random = context?.Random ?? new Random();
      double angle = random.NextDouble() * 2 * Math.PI;
      double distance = random.NextDouble() * radius;

      double x = CenterPosition.X + Math.Cos(angle) * distance;
      double y = CenterPosition.Y + Math.Sin(angle) * distance;

      return new(x, y);
    }
  }
}
