using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;
using HiveMind.Core.Domain.Services;
using HiveMind.Core.ValueObjects;

namespace HiveMind.Core.Domain.Behaviors
{
  // ====================================================================
  //  Behavior for FORAGER ants - focused on finding and collecting food
  // ====================================================================

  public class ForagerBehavior : TaskBasedBehavior
  {
    private IFoodSource? _targetFoodSource;
    private readonly double _forageRadius = 30.0;
    private ForagerTaskType _currentTaskType = ForagerTaskType.SearchForFood;
    private bool _isFollowingTrail = false;
    private bool _isLayingTrail = false;

    protected override int TaskUpdateInterval => 50;  //  Foragers are very active
    protected override double GetRestThreshold() => 0.15;
    protected override double GetRestAmount() => 1.0;
    protected override double TaskCompletionDistance => 1.0;

    protected override void AssignNewTask(Ant ant, ISimulationContext context)
    {
      var pheromoneMap = GetPheromoneMap(context);
      if (pheromoneMap == null)
      {
        //  Fallback to basic behavior if no pheromone system
        AssignBasicTask(ant, context);

        return;
      }

      //  Decision making with pheromone awereness
      if (ant.CarriedFood > 0)
        AssignReturnToNestWithTrail(ant, pheromoneMap);
      else if (ShouldFollowFoodTrail(ant, pheromoneMap))
        AssignFollowFoodTrail(ant, pheromoneMap);
      else if (_targetFoodSource != null && !_targetFoodSource.IsExhausted)
        AssignMoveToKnownFoodSource(ant, pheromoneMap);
      else
        AssignExploreWithTrialAwereness(ant, context, pheromoneMap);
    }

    private void AssignReturnToNestWithTrail(Ant ant, PheromoneMap pheromoneMap)
    {
      var nestPosition = GetSafeNestPosition(ant);
      if (nestPosition == null)
      {
        ClearCurrentTask();

        return;
      }

      _currentTaskType = ForagerTaskType.ReturnToNest;
      _isLayingTrail = true;  //  Lay food trail while returning

      //  Use home trail to navigate if available
      var homeGradient = pheromoneMap.GetPheromoneGradient(ant.Position, PheromoneType.HomeTrail, ant.Id);

      Position targetPosition;
      if (homeGradient.Magnitude > 0.1)
      {
        //  Follow home trail gradient
        var direction = homeGradient.Normalized;
        targetPosition = new(
          ant.Position.X + direction.X * 10.0,
          ant.Position.Y + direction.Y * 10.0
        );
      }
      else
        targetPosition = nestPosition.Value;

      BehaviorTask task = CreateTask(ActivityState.Moving, 0.1, targetPosition);
      SetCurrentTask(task);
    }

    private bool ShouldFollowFoodTrail(Ant ant, PheromoneMap pheromoneMap)
    {
      var foodTrailIntensity = pheromoneMap.GetPheromoneIntensity(
        ant.Position,
        PheromoneType.FoodTrail,
        ant.Id  //  Exclude own trails
      );

      return foodTrailIntensity > 0.2;  //  Threshold for trail following
    }

    private void AssignFollowFoodTrail(Ant ant, PheromoneMap pheromoneMap)
    {
      _currentTaskType = ForagerTaskType.FollowTrail;
      _isFollowingTrail = true;

      Vector2 gradient = pheromoneMap.GetPheromoneGradient(ant.Position, PheromoneType.FoodTrail, ant.Id);
      if (gradient.Magnitude > 0.01)
      {
        Vector2 direction = gradient.Normalized;
        Position targetPosition = new(
          ant.Position.X + direction.X * 5.0,
          ant.Position.Y + direction.Y * 5.0
        );

        BehaviorTask task = CreateTask(ActivityState.Moving, 0.2, targetPosition);
        SetCurrentTask(task);
      }
      else
        //  Trail lost - switch to exploration
        AssignExploreTask(ant, null);
    }

    protected override void OnTaskCompleted(Ant ant, ISimulationContext context, BehaviorTask task)
    {
      var pheromoneMap = GetPheromoneMap(context);

      //  Lay pheromone trails during movement
      if (_isLayingTrail && pheromoneMap != null)
        LayPheromoneTrail(ant, pheromoneMap);

      base.OnTaskCompleted(ant, context, task);
    }

    private void LayPheromoneTrail(Ant ant, PheromoneMap pheromoneMap)
    {
      if (ant.CarriedFood > 0)
      {
        //  Lay food trail when carrying food back to nest
        var intensity = Math.Min(5.0, ant.CarriedFood);  //  Trail strength based on food amount
        pheromoneMap.DepositPheromone(ant.Position, PheromoneType.FoodTrail, intensity, ant.Id);
      }
      else
        //  Lay home trail when searching (weaker)
        pheromoneMap.DepositPheromone(ant.Position, PheromoneType.HomeTrail, 1.0, ant.Id);
    }

    private PheromoneMap? GetPheromoneMap(ISimulationContext context)
    {
      //  This would be injected through the context in a full implementation
      //  For now, return null to maintain compilation

      return context.Environment as IPheromoneEnvironment;
    }

    private void AssignBasicTask(Ant ant, ISimulationContext context)
    {
      //  Fallback to original behavior logic
      if (ant.CarriedFood > 0)
        AssignReturnToNestTask(ant);
      else
        AssignExploreTask(ant, context);
    }

    // ==========================================
    //  Types of tasks that foragers can perform
    // ==========================================

    private enum ForagerTaskType
    {
      SearchForFood,
      MoveToFood,
      CollectFood,
      ReturnToNest,
      DropFood,
      Explore
    }
  }
}
