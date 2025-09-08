using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for forager ants - focused on finding and collecting food
  /// </summary>
  public class ForagerBehavior : BaseBehavior
  {
    private ForagerState _currentState = ForagerState.Searching;
    private IFoodSource? _targetFoodSource;
    private Position? _exploreTarget;
    private readonly double _forageRadius = 30.0;

    public override void Update(Ant ant, ISimulationContext context)
    {
      SafeUpdate(ant, context, (a, ctx) =>
      {
        // Check if ant needs rest
        if (a.Energy < a.MaxEnergy * 0.15)
        {
          SafeSetState(a, ActivityState.Resting);
          SafeRestoreEnergy(a, 1.0);

          return;
        }

        // Execute current state
        ExecuteCurrentState(a, ctx);
      });
    }

    private void ExecuteCurrentState(Ant ant, ISimulationContext context)
    {
      switch (_currentState)
      {
        case ForagerState.Searching:
          ExecuteSearchingState(ant, context);
          break;
        case ForagerState.MovingToFood:
          ExecuteMovingToFoodState(ant, context);
          break;
        case ForagerState.Collecting:
          ExecuteCollectingState(ant, context);
          break;
        case ForagerState.ReturningToNest:
          ExecuteReturningState(ant, context);
          break;
        case ForagerState.Exploring:
          ExecuteExploringState(ant, context);
          break;
      }
    }

    private void ExecuteSearchingState(Ant ant, ISimulationContext context)
    {
      // Determine next action based on ant's current condition
      if (ant.CarriedFood > 0)
      {
        TransitionToState(ant, ForagerState.ReturningToNest);

        return;
      }

      if (_targetFoodSource != null && !_targetFoodSource.IsExhausted)
      {
        TransitionToState(ant, ForagerState.MovingToFood);

        return;
      }

      // Look for food sources
      var nearbyFoodSource = FindNearbyFoodSource(ant, context);
      if (nearbyFoodSource != null)
      {
        _targetFoodSource = nearbyFoodSource;
        TransitionToState(ant, ForagerState.MovingToFood);
      }
      else
        TransitionToState(ant, ForagerState.Exploring);
    }

    private void ExecuteMovingToFoodState(Ant ant, ISimulationContext context)
    {
      if (_targetFoodSource == null || _targetFoodSource.IsExhausted)
      {
        _targetFoodSource = null;
        TransitionToState(ant, ForagerState.Searching);

        return;
      }

      if (MoveTowardsTarget(ant, _targetFoodSource.Position, context, 1.0))
        TransitionToState(ant, ForagerState.Collecting);
    }

    private void ExecuteCollectingState(Ant ant, ISimulationContext context)
    {
      if (_targetFoodSource == null || !_targetFoodSource.IsExhausted)
      {
        _targetFoodSource = null;
        TransitionToState(ant, ForagerState.Searching);

        return;
      }

      // Collect food
      SafeSetState(ant, ActivityState.Foraging);
      double harvestedAmount = SafeHarvestFood(_targetFoodSource, 5.0);
      if (harvestedAmount > 0)
        SafeCollectFood(ant, harvestedAmount);

      SafeConsumeEnergy(ant, 0.3);

      // Check if should continue collecting or return
      if (_targetFoodSource.IsExhausted || ant.CarriedFood >= 10)
      {
        _targetFoodSource = null;
        TransitionToState(ant, ForagerState.ReturningToNest);
      }
      else
        TransitionToState(ant, ForagerState.Searching);
    }

    private void ExecuteReturningState(Ant ant, ISimulationContext context)
    {
      Position? nestPosition = GetSafeNestPosition(ant);
      if (nestPosition == null)
      {
        TransitionToState(ant, ForagerState.Searching);

        return;
      }

      if (MoveTowardsTarget(ant, nestPosition.Value, context, 2.0))
      {
        // Arrived at nest - drop food
        double droppedFood = SafeDropFood(ant);
        if (droppedFood > 0 && ant.Colony != null)
          SafeAddFoodToColony(ant.Colony, droppedFood);

        TransitionToState(ant, ForagerState.Searching);
      }
    }

    private void ExecuteExploringState(Ant ant, ISimulationContext context)
    {
      if (_exploreTarget == null)
        _exploreTarget = GenerateExploreTarget(ant, context);
      if (_exploreTarget != null)
      {
        if (MoveTowardsTarget(ant, _exploreTarget.Value, context, 2.0))
        {
          // Reached exploration point
          _exploreTarget = null;
          TransitionToState(ant, ForagerState.Searching);
        }
      }
      else
        // Couldn't generate exploration target
        TransitionToState(ant, ForagerState.Searching);

      SafeConsumeEnergy(ant, 0.2);
    }

    /// <summary>
    /// Common method for moving towards a target
    /// </summary>
    private static bool MoveTowardsTarget(Ant ant, Position target, ISimulationContext context, double completionDistance)
    {
      double distance = SafeCalculateDistance(ant.Position, target);
      if (distance > completionDistance && distance != double.MaxValue)
      {
        SafeSetState(ant, ActivityState.Moving);

        return SafeMoveTo(ant, target, context);
      }

      return true; // Already at target
    }

    private void TransitionToState(Ant ant, ForagerState newState)
    {
      _currentState = newState;

      // Set appropriate activity state based on forager state
      ActivityState activityState = newState switch
      {
        ForagerState.MovingToFood or ForagerState.ReturningToNest or ForagerState.Exploring => ActivityState.Moving,
        ForagerState.Collecting => ActivityState.Foraging,
        _ => ActivityState.Idle
      };

      SafeSetState(ant, activityState);
    }

    private IFoodSource? FindNearbyFoodSource(Ant ant, ISimulationContext context)
    {
      IReadOnlyCollection<IFoodSource> foodSources = SafeGetFoodSources(context.Environment);
      List<(IFoodSource source, double distance)> nearbyFoodSources = [];
      foreach (IFoodSource fs in foodSources)
        if (!fs.IsExhausted)
        {
          double distance = SafeCalculateDistance(fs.Position, ant.Position);
          if (distance <= _forageRadius)
            nearbyFoodSources.Add((fs, distance));
        }

      return nearbyFoodSources.OrderBy(item => item.distance).FirstOrDefault().source;
    }

    private Position? GenerateExploreTarget(Ant ant, ISimulationContext context)
    {
      Position nestPosition = GetSafeNestPosition(ant) ?? ant.Position;

      return GenerateSafePosition(nestPosition, _forageRadius, context);
    }

    /// <summary>
    /// States for the forager behavior state machine
    /// </summary>
    public enum ForagerState
    {
      Searching,
      MovingToFood,
      Collecting,
      ReturningToNest,
      Exploring
    }
  }
}
