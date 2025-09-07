using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for forager ants - focused on finding and collecting food
  /// </summary>
  public class ForagerBehavior : IAntBehavior
  {
    private IFoodSource? _targetFoodSource;
    private Position? _exploreTarget;
    private bool _returningToNest = false;
    private readonly double _forageRadius = 30.0;

    public void Update(Ant ant, ISimulationContext context)
    {
      // Check if ant needs rest
      if (ant.Energy < ant.MaxEnergy * 0.15)
      {
        ant.SetState(ActivityState.Resting);
        ant.RestoreEnergy(1.0);

        return;
      }

      if (ant.CarriedFood > 0)
        ReturnToNest(ant);
      else if (_targetFoodSource != null && !_targetFoodSource.IsExhausted)
        CollectFoodFromSource(ant);
      else
        SearchForFood(ant, context);
    }

    private void ReturnToNest(Ant ant)
    {
      _returningToNest = true;
      ant.SetState(ActivityState.Moving);

      Position nestPosition = ant.Colony.CenterPosition;
      double distanceToNest = ant.Position.DistanceTo(nestPosition);

      if (distanceToNest > 2.0)
        ant.MoveTo(nestPosition);
      else
      {
        // Arrived at nest - drop food and add to colony
        double droppedFood = ant.DropFood();
        if (droppedFood > 0)
          ant.Colony.AddFood(droppedFood);

        // Always reset when reaching nest
        _returningToNest = false;
        _targetFoodSource = null;
        ant.SetState(ActivityState.Idle);
      }
    }

    private void CollectFoodFromSource(Ant ant)
    {
      if (_targetFoodSource == null)
        return;

      double distanceToFood = ant.Position.DistanceTo(_targetFoodSource.Position);
      if (distanceToFood > 1.0)
      {
        ant.SetState(ActivityState.Moving);
        ant.MoveTo(_targetFoodSource.Position);
      }
      else
      {
        // At food source - collect food
        ant.SetState(ActivityState.Foraging);

        double harvestedAmount = _targetFoodSource.Harvest(5.0);

        ant.CollectFood(harvestedAmount);
        ant.ConsumeEnergy(0.3);

        if (_targetFoodSource.IsExhausted || ant.CarriedFood >= 10)
          _targetFoodSource = null;
      }
    }

    private void SearchForFood(Ant ant, ISimulationContext context)
    {
      Position antPosition = ant.Position;
      List<(IFoodSource source, double distance)> foodSourcesWithDistance = [];

      // Single pass to calculate distances and filter
      foreach (var fs in context.Environment.GetFoodSources())
        if (!fs.IsExhausted)
        {
          double distance = fs.Position.DistanceTo(antPosition);
          if (distance <= _forageRadius)
            foodSourcesWithDistance.Add((fs, distance));
        }

      // Find closest food source from pre-calculated distances
      _targetFoodSource = foodSourcesWithDistance.OrderBy(item => item.distance).FirstOrDefault().source;
      if (_targetFoodSource != null)
      {
        ant.SetState(ActivityState.Moving);
        ant.MoveTo(_targetFoodSource.Position);
      }
      else
        ExploreRandomly(ant, context); // No food sources found - explore randomly
    }

    private void ExploreRandomly(Ant ant, ISimulationContext context)
    {
      if (_exploreTarget == null || ant.Position.DistanceTo(_exploreTarget.Value) < 2.0)
      {
        // Generate new exploration target
        Position nestPosition = ant.Colony.CenterPosition;
        double angle = context.Random.NextDouble() * 2 * Math.PI;
        double distance = context.Random.NextDouble() * _forageRadius;

        double x = nestPosition.X + Math.Cos(angle) * distance;
        double y = nestPosition.Y + Math.Sin(angle) * distance;

        _exploreTarget = new Position(x, y);
      }

      ant.SetState(ActivityState.Moving);
      ant.MoveTo(_exploreTarget.Value);
      ant.ConsumeEnergy(0.2);
    }
  }
}
