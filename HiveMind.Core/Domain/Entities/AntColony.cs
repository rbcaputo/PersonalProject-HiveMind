using HiveMind.Core.Domain.Behaviors;
using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Entities
{
  /// <summary>
  /// Represents an ant colony with its members and collective behaviors
  /// </summary>
  public class AntColony : BaseEntity, IColony
  {
    private readonly Dictionary<Guid, Ant> _members;
    private readonly List<Position> _nestLocations;

    public InsectType ColonyType => InsectType.Ant;
    public Position CenterPosition { get; private set; }
    public IReadOnlyCollection<IInsect> Members => [.. _members.Values.Cast<IInsect>()];
    public int Population => _members.Count(kv => kv.Value.IsAlive);
    public double TotalFoodStored { get; private set; }
    public bool IsActive => Population > 0 && HasQueen;
    public bool HasQueen => _members.Values.Any(ant => ant.Role == AntRole.Queen && ant.IsAlive);

    // Colony-specific properties
    public int MaxPopulation { get; private set; }
    public double ExpansionRadius { get; private set; }
    public IReadOnlyCollection<Position> NestLocations => _nestLocations.AsReadOnly();

    public AntColony(Position centerPosition, int maxPopulation = 500)
    {
      _members = [];
      _nestLocations = [centerPosition];
      CenterPosition = centerPosition;
      MaxPopulation = maxPopulation;
      ExpansionRadius = 50.0;
      TotalFoodStored = 100.0; // Starting food

      InitializeColony();
    }

    public void AddMember(IInsect insect)
    {
      if (insect is Ant ant && _members.Count < MaxPopulation)
      {
        _members[ant.Id] = ant;
        UpdateTimestamp();
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
      if (!IsActive)
        return;

      // Update all colony members
      var membesrToUpdate = _members.Values.Where(ant => ant.IsAlive).ToList();
      foreach (var ant in membesrToUpdate)
      {
        ant.Update(context);

        // Collect food from returning foragers
        if (ant.Role == AntRole.Forager && ant.CarriedFood > 0 && ant.Position.DistanceTo(CenterPosition) < 2.0)
          TotalFoodStored += ant.DropFood();
      }

      // Remove dead ants
      var deadAnts = _members.Where(kv => !kv.Value.IsAlive).ToList();
      foreach (var deadAnt in deadAnts)
        _members.Remove(deadAnt.Key);

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

    public IEnumerable<Ant> GetAntsByRole(AntRole role) =>
      _members.Values.Where(ant => ant.Role == role && ant.IsAlive);

    public Position GetNearestNest(Position position) =>
      _nestLocations.OrderBy(nest => nest.DistanceTo(position)).First();

    private void InitializeColony()
    {
      // Create queen
      var queen = new Ant(AntRole.Queen, CenterPosition, AntBehaviorFactory.CreateBehavior(AntRole.Queen));
      AddMember(queen);

      // Create initial workers
      for (int i = 0; i < 10; i++)
      {
        var position = GetRandomPositionNearCenter(5.0);
        var worker = new Ant(AntRole.Worker, position, AntBehaviorFactory.CreateBehavior(AntRole.Worker));
        AddMember(worker);
      }

      // Create initial foragers
      for (int i = 0; i < 5; i++)
      {
        var position = GetRandomPositionNearCenter(3.0);
        var forager = new Ant(AntRole.Forager, position, AntBehaviorFactory.CreateBehavior(AntRole.Forager));
        AddMember(forager);
      }
    }

    private void ManagePopulation(ISimulationContext context)
    {
      if (!HasQueen || Population >= MaxPopulation)
        return;

      // Simple reproduction logic - queen spawns new ants periodically
      if (context.CurrentTick % 100 == 0 && TotalFoodStored > 50)
        SpawnNewAnt();
    }

    private void ManageFood()
    {
      // Colony consumes food over time
      var consumptionRate = Population * 0.05;
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
        var newNestPosition = GetRandomPositionNearCenter(ExpansionRadius);
        _nestLocations.Add(newNestPosition);
      }
    }

    private void SpawnNewAnt()
    {
      var role = DetermineNewAntRole();
      var position = GetRandomPositionNearCenter(2.0);

      var newAnt = new Ant(role, position, AntBehaviorFactory.CreateBehavior(role));
      AddMember(newAnt);

      // Consume food for reproduction
      ConsumeFood(10.0);
    }

    private AntRole DetermineNewAntRole()
    {
      var foragerCount = GetAntsByRole(AntRole.Forager).Count();
      var soldierCount = GetAntsByRole(AntRole.Soldier).Count();

      // Simple role distribution logic
      if (foragerCount < Population * 0.3)
        return AntRole.Forager;
      if (soldierCount < Population * 0.1)
        return AntRole.Soldier;

      return AntRole.Worker;
    }

    private void DispatchForagers()
    {
      var idleForagers = GetAntsByRole(AntRole.Forager)
        .Where(ant => ant.CurrentState == ActivityState.Idle)
        .Take(3);

      // Convert some workers to foragers temporarily
      // This would require more sophisticated role switching in a full implementation
    }

    private Position GetRandomPositionNearCenter(double radius)
    {
      var random = new Random();
      var angle = random.NextDouble() * 2 * Math.PI;
      var distance = random.NextDouble() * radius;

      var x = CenterPosition.X + Math.Cos(angle) * distance;
      var y = CenterPosition.Y + Math.Sin(angle) * distance;

      return new(x, y);
    }
  }
}
