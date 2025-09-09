using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for forager ants - focused on finding and collecting food
  /// </summary>
  public class ForagerBehavior : TaskBasedBehavior
  {
    private IFoodSource? _targetFoodSource;
    private readonly double _forageRadius = 30.0;
    private ForagerTaskType _currentTaskType = ForagerTaskType.SearchForFood;

    protected override int TaskUpdateInterval => 50; // Foragers are very active
    protected override double GetRestThreshold() => 0.15;
    protected override double GetRestAmount() => 1.0;
    protected override double TaskCompletionDistance => 1.0;

    protected override void AssignNewTask(Ant ant, ISimulationContext context)
    {
      // Determine what the forager should do next based on current state
      if (ant.CarriedFood > 0)
        // Carrying food - return to nest
        AssignReturnToNestTask(ant);
      else if (_targetFoodSource != null && !_targetFoodSource.IsExhausted)
        // Has valid food source - go collect from it
        AssignCollectFoodTask();
      else
      {
        // Need to find food - search or explore
        if (TryFindNearbyFoodSource(ant, context))
          AssignMoveToFoodTask(ant);
        else
          AssignExploreTask(ant, context);
      }
    }

    protected override void ExecuteStationaryTask(Ant ant, ISimulationContext context)
    {
      if (CurrentTask == null)
        return;

      switch (_currentTaskType)
      {
        case ForagerTaskType.CollectFood:
          ExecuteCollectFoodTask(ant, context);
          break;
        case ForagerTaskType.DropFood:
          ExecuteDropFoodTask(ant, context);
          break;
        default:
          base.ExecuteStationaryTask(ant, context);
          break;
      }
    }

    protected override void OnTaskCompleted(Ant ant, ISimulationContext context, BehaviorTask task)
    {
      switch (_currentTaskType)
      {
        case ForagerTaskType.MoveToFood:
          // Arrived at food source - start collecting
          AssignCollectFoodTask();
          break;
        case ForagerTaskType.ReturnToNest:
          // Arrived at nest - drop food
          AssignDropFoodTask();
          break;
        case ForagerTaskType.Explore:
          // Finished exploring - search for food again
          ClearCurrentTask(); // Will trigger new task assignment
          break;
        case ForagerTaskType.CollectFood:
        case ForagerTaskType.DropFood:
          // These tasks complete themselves
          ClearCurrentTask();
          break;
        default:
          base.OnTaskCompleted(ant, context, task);
          break;
      }
    }

    private void AssignReturnToNestTask(Ant ant)
    {
      Position? nestPosition = GetSafeNestPosition(ant);
      if (nestPosition == null)
      {
        ClearCurrentTask();

        return;
      }

      _currentTaskType = ForagerTaskType.ReturnToNest;
      BehaviorTask task = CreateTask(ActivityState.Moving, 0.1, nestPosition); // Low energy cost for returning
      SetCurrentTask(task);
    }

    private void AssignCollectFoodTask()
    {
      if (_targetFoodSource == null || _targetFoodSource.IsExhausted)
      {
        ClearCurrentTask();

        return;
      }

      _currentTaskType = ForagerTaskType.CollectFood;
      BehaviorTask task = CreateTask(ActivityState.Foraging, 0.3); // Stationary task - no target position
      SetCurrentTask(task);
    }

    private void AssignMoveToFoodTask()
    {
      if (_targetFoodSource == null)
      {
        ClearCurrentTask();

        return;
      }

      _currentTaskType = ForagerTaskType.MoveToFood;
      BehaviorTask task = CreateTask(ActivityState.Moving, 0.2, _targetFoodSource.Position);
      SetCurrentTask(task);
    }

    private void AssignExploreTask(Ant ant, ISimulationContext context)
    {
      Position? exploreTarget = GenerateExploreTarget(ant, context);
      if (exploreTarget == null)
      {
        ClearCurrentTask();

        return;
      }

      _currentTaskType = ForagerTaskType.Explore;
      BehaviorTask task = CreateTask(ActivityState.Moving, 0.2, exploreTarget);
      SetCurrentTask(task);
    }

    private void AssignDropFoodTask()
    {
      _currentTaskType = ForagerTaskType.DropFood;
      BehaviorTask task = CreateTask(ActivityState.Idle, 0.1); // Stationary task
      SetCurrentTask(task);
    }

    private void ExecuteCollectFoodTask(Ant ant, ISimulationContext context)
    {
      if (_targetFoodSource == null || _targetFoodSource.IsExhausted)
      {
        _targetFoodSource = null;
        CompleteCurrentTask(ant, context);

        return;
      }

      // Collect food
      double harvestedAmount = SafeHarvestFood(_targetFoodSource, 5.0);
      if (harvestedAmount > 0)
        SafeCollectFood(ant, harvestedAmount);

      // Check if should continue collecting or return
      if (_targetFoodSource.IsExhausted || ant.CarriedFood >= 10)
      {
        _targetFoodSource = null;
        CompleteCurrentTask(ant, context);
      }
      // If still collecting, the task continues
    }

    private void ExecuteDropFoodTask(Ant ant, ISimulationContext context)
    {
      // Drop food at nest
      double droppedFood = SafeDropFood(ant);
      if (droppedFood > 0 && ant.Colony != null)
        SafeAddFoodToColony(ant.Colony, droppedFood);

      CompleteCurrentTask(ant, context);
    }

    private bool TryFindNearbyFoodSource(Ant ant, ISimulationContext context)
    {
      IReadOnlyCollection<IFoodSource> foodSources = SafeGetFoodSources(context.Environment);
      List<(IFoodSource source, double distance)> nearbyFoodSources = [];

      foreach (IFoodSource foodSource in foodSources)
        if (!foodSource.IsExhausted)
        {
          double distance = SafeCalculateDistance(foodSource.Position, ant.Position);
          if (distance <= _forageRadius)
            nearbyFoodSources.Add((foodSource, distance));
        }

      if (nearbyFoodSources.Count > 0)
      {
        // Select closest food source
        _targetFoodSource = nearbyFoodSources.OrderBy(fs => fs.distance).First().source;

        return true;
      }

      _targetFoodSource = null;

      return false;
    }

    private Position? GenerateExploreTarget(Ant ant, ISimulationContext context)
    {
      Position nestPosition = GetSafeNestPosition(ant) ?? ant.Position;

      return GenerateSafePosition(nestPosition, _forageRadius, context);
    }

    /// <summary>
    /// Types of tasks that foragers can perform
    /// </summary>
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
