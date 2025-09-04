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

      // Simple nest location - in a full implementation, this would reference the colony
      var nestPosition = new Position(0, 0);
      var distanceToNest = ant.Position.DistanceTo(nestPosition);

      if (distanceToNest > 2.0)
        ant.MoveTo(nestPosition);
      else
      {
        // Arrived at nest - drop food
        ant.DropFood();
        _returningToNest = false;
        _targetFoodSource = null;
        ant.SetState(ActivityState.Idle);
      }
    }

    private void CollectFoodFromSource(Ant ant)
    {
      if (_targetFoodSource == null)
        return;

      var distanceToFood = ant.Position.DistanceTo(_targetFoodSource.Position);
      if (distanceToFood > 1.0)
      {
        ant.SetState(ActivityState.Moving);
        ant.MoveTo(_targetFoodSource.Position);
      }
      else
      {
        // At food source - collect food
        ant.SetState(ActivityState.Foraging);
        var harvestedAmount = _targetFoodSource.Harvest(5.0);
        ant.CollectFood(harvestedAmount);
        ant.ConsumeEnergy(0.3);

        if (_targetFoodSource.IsExhausted || ant.CarriedFood >= 10)
          _targetFoodSource = null;
      }
    }

    private void SearchForFood(Ant ant, ISimulationContext context)
    {
      // Look for nearby food sources
      var foodSources = context.Environment.GetFoodSources()
        .Where(fs => !fs.IsExhausted && fs.Position.DistanceTo(ant.Position) <= _forageRadius)
        .OrderBy(fs => fs.Position.DistanceTo(ant.Position));

      _targetFoodSource = foodSources.FirstOrDefault();
      if (_targetFoodSource != null)
      {
        ant.SetState(ActivityState.Moving);
        ant.MoveTo(_targetFoodSource.Position);
      }
      else
        // No food sources found - explore randomly
        ExploreRandomly(ant, context);
    }

    private void ExploreRandomly(Ant ant, ISimulationContext context)
    {
      if (_exploreTarget == null || ant.Position.DistanceTo(_exploreTarget.Value) < 2.0)
      {
        // Generate new exploration target
        var angle = context.Random.NextDouble() * 2 * Math.PI;
        var distance = context.Random.NextDouble() * _forageRadius;

        var x = Math.Cos(angle) * distance;
        var y = Math.Sin(angle) * distance;

        _exploreTarget = new Position(x, y);
      }

      ant.SetState(ActivityState.Moving);
      ant.MoveTo(_exploreTarget.Value);
      ant.ConsumeEnergy(0.2);
    }
  }
}
