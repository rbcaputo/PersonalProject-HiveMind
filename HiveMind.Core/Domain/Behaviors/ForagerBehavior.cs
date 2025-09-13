using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;
using HiveMind.Core.Domain.Services;
using HiveMind.Core.Domain.ValueObjects;

namespace HiveMind.Core.Domain.Behaviors
{
  // ---------------------------------------------------------------
  //  Forager behavior using pheromone trail communication
  //  Implements trail-laying, following, and recruitment behaviors
  // ---------------------------------------------------------------

  public class ForagerBehavior : TaskBasedBehavior
  {
    private IFoodSource? _targetFoodSource;
    private readonly double _forageRadius = 35.0;
    private readonly double _trailStrength = 3.0;
    private ForagerTaskType _currentTaskType = ForagerTaskType.SearchForFood;
    private DateTime _lastPheromoneDeposit = DateTime.MinValue;
    private Vector2 _lastMovementDirection = Vector2.Zero;
    private double _foodQualityRating = 1.0;

    //  Pheromone behavior parameters
    private readonly double _trailFollowingProbability = 0.8;  //  80% chance to follow trails
    private readonly double _explorationRandomness = 0.3;      //  Exploration noise factor
    private readonly double _trailLayingInterval = 2.0;        //  Seconds between pheromone deposits

    protected override int TaskUpdateInterval =>
      60;  //  More frequent updates for pheromone responsiveness
    protected override double GetRestThreshold() =>
      0.15;
    protected override double GetRestAmount() =>
      1.0;
    protected override double TaskCompletionDistance =>
      1.5;

    protected override void AssignNewTask(Ant ant, ISimulationContext context)
    {
      var pheromoneMap = GetPheromoneMap(context);
      if (pheromoneMap == null)
      {
        //  Fallback to basic behavior if no pheromone system
        AssignBasicTask(ant, context);

        return;
      }

      //  Enhanced decision making with pheromone awareness
      if (ant.CarriedFood > 0)
        AssignReturnToNestWithTrail(ant, context, pheromoneMap);
      else if (ShouldFollowFoodTrail(ant, context, pheromoneMap))
        AssignFollowFoodTrail(ant, context, pheromoneMap);
      else if (_targetFoodSource != null && !_targetFoodSource.IsExhausted)
        AssignMoveToKnownFoodSource(ant, context, pheromoneMap);
      else if (ShouldRespondToRecruitmentSignal(ant, context, pheromoneMap))
        AssignRespondToRecruitment(ant, context, pheromoneMap);
      else
        AssignExploreWithTrailAwareness(ant, context, pheromoneMap);
    }

    protected override void OnTaskCompleted(Ant ant, ISimulationContext context, BehaviorTask task)
    {
      var pheromoneMap = GetPheromoneMap(context);
      if (pheromoneMap != null)
        //  Lay pheromone trails during movement and task completion
        LayPheromoneTrails(ant, context, pheromoneMap);

      base.OnTaskCompleted(ant, context, task);
    }

    protected override void ExecuteStationaryTask(Ant ant, ISimulationContext context)
    {
      var pheromoneMap = GetPheromoneMap(context);

      switch (_currentTaskType)
      {
        case ForagerTaskType.CollectFood:
          ExecuteCollectFoodTask(ant, context, pheromoneMap);
          break;
        case ForagerTaskType.DropFood:
          ExecuteDropFoodTask(ant, context, pheromoneMap);
          break;
        case ForagerTaskType.EvaluateFood:
          ExecuteEvaluateFoodTask(ant, context, pheromoneMap);
          break;
        default:
          base.ExecuteStationaryTask(ant, context);
          break;
      }
    }

    // -----------------------------------------
    //  Pheromone-aware task assignment methods
    // -----------------------------------------

    private void AssignReturnToNestWithTrail(Ant ant, ISimulationContext context, PheromoneMap pheromoneMap)
    {
      var nestPosition = GetSafeNestPosition(ant);
      if (nestPosition == null)
      {
        ClearCurrentTask();

        return;
      }

      _currentTaskType = ForagerTaskType.ReturnToNest;

      //  Use home trail to navigate efficiently
      var homeTrail = pheromoneMap.FindStrongestTrail(
        ant.Position,
        PheromoneType.HomeTrail,
        searchRadius: 12.0,
        excludeDepositor: ant.Id
      );

      Position targetPosition;
      if (homeTrail != null && homeTrail.Direction.Magnitude > 0.1)
      {
        //  Follow home trail with some forward momentum
        var trailDirection = homeTrail.Direction.Normalized;
        targetPosition = new Position(
          ant.Position.X + trailDirection.X * 8.0,
          ant.Position.Y + trailDirection.Y * 8.0
        );
      }
      else
        //  Direct navigation to nest
        targetPosition = nestPosition.Value;

      var task = CreateTask(ActivityState.Moving, 0.1, targetPosition);
      SetCurrentTask(task);
    }

    private bool ShouldFollowFoodTrail(Ant ant, ISimulationContext context, PheromoneMap pheromoneMap)
    {
      //  Check for existing food trails
      var foodTrail = pheromoneMap.FindStrongestTrail(
        ant.Position,
        PheromoneType.FoodTrail,
        searchRadius: 10.0,
        excludeDepositor: ant.Id
      );

      //  Follow trail based on intensity and random factor
      if (foodTrail != null)
      {
        double followProbability = _trailFollowingProbability * (foodTrail.Intensity / 5.0);  //  Normalize intensity

        return context.Random.NextDouble() < followProbability;
      }

      return false;
    }

    private void AssignFollowFoodTrail(Ant ant, ISimulationContext context, PheromoneMap pheromoneMap)
    {
      _currentTaskType = ForagerTaskType.FollowTrail;

      var foodTrail = pheromoneMap.FindStrongestTrail(
        ant.Position,
        PheromoneType.FoodTrail,
        searchRadius: 10.0,
        excludeDepositor: ant.Id
      );

      if (foodTrail != null && foodTrail.Direction.Magnitude > 0.1)
      {
        //  Follow trail with exploration noise for path optimization
        var baseDirection = foodTrail.Direction.Normalized;
        var explorationNoise = GenerateExplorationNoise(context.Random);
        var finalDirection = (baseDirection + explorationNoise * _explorationRandomness).Normalized;

        var targetPosition = new Position(
          ant.Position.X + finalDirection.X * 7.0,
          ant.Position.Y + finalDirection.Y * 7.0
        );

        var task = CreateTask(ActivityState.Moving, 0.2, targetPosition);
        SetCurrentTask(task);

        //  Remember movement direction for trail laying
        _lastMovementDirection = finalDirection;
      }
      else
        //  Trail lost - switch to exploration
        AssignExploreTask(ant, context);
    }

    private bool ShouldRespondToRecruitmentSignal(Ant ant, ISimulationContext context, PheromoneMap pheromoneMap)
    {
      var recruitmentIntensity = pheromoneMap.GetPheromoneIntensity(
        ant.Position,
        PheromoneType.Recruitment,
        excludeDepositor: ant.Id
      );

      return recruitmentIntensity > 0.5;  //  Respond to moderate recruitment signals
    }

    private void AssignRespondToRecruitment(Ant ant, ISimulationContext context, PheromoneMap pheromoneMap)
    {
      _currentTaskType = ForagerTaskType.RespondToRecruitment;

      var recruitmentGradient = pheromoneMap.GetPheromoneGradient(
        ant.Position,
        PheromoneType.Recruitment,
        excludeDepositor: ant.Id
      );

      if (recruitmentGradient.Magnitude > 0.1)
      {
        var targetPosition = new Position(
          ant.Position.X + recruitmentGradient.X * 10.0,
          ant.Position.Y + recruitmentGradient.Y * 10.0
        );

        var task = CreateTask(ActivityState.Moving, 0.3, targetPosition);
        SetCurrentTask(task);
      }
      else
        AssignExploreTask(ant, context);
    }

    private void AssignExploreWithTrailAwareness(Ant ant, ISimulationContext context, PheromoneMap pheromoneMap)
    {
      _currentTaskType = ForagerTaskType.Explore;

      //  Avoid areas with high territorial pheromones from other colonies
      var territorialIntensity = pheromoneMap.GetPheromoneIntensity(
        ant.Position,
        PheromoneType.Territorial
      );
      Position? exploreTarget;

      if (territorialIntensity > 2.0)
      {
        //  Move away from territorial markers
        var territorialGradient = pheromoneMap.GetPheromoneGradient(ant.Position, PheromoneType.Territorial);
        var avoidanceDirection = territorialGradient.Magnitude > 0
          ? (territorialGradient * -1).Normalized
          : GenerateRandomDirection(context.Random);

        exploreTarget = new(
          ant.Position.X + avoidanceDirection.X * 15.0,
          ant.Position.Y + avoidanceDirection.Y * 15.0
        );
      }
      else
        exploreTarget = GenerateExploreTarget(ant, context);

      if (exploreTarget != null)
      {
        var task = CreateTask(ActivityState.Moving, 0.25, exploreTarget);
        SetCurrentTask(task);
      }
      else
        ClearCurrentTask();
    }

    private void AssignMoveToKnownFoodSource(Ant ant, ISimulationContext context, PheromoneMap pheromoneMap)
    {
      if (_targetFoodSource == null)
      {
        ClearCurrentTask();

        return;
      }

      _currentTaskType = ForagerTaskType.MoveToFood;
      var task = CreateTask(ActivityState.Moving, 0.2, _targetFoodSource.Position);
      SetCurrentTask(task);
    }

    // ------------------------
    //  Task execution methods
    // ------------------------

    private void ExecuteCollectFoodTask(Ant ant, ISimulationContext context, PheromoneMap? pheromoneMap)
    {
      if (_targetFoodSource == null || _targetFoodSource.IsExhausted)
      {
        _targetFoodSource = null;
        CompleteCurrentTask(ant, context);

        return;
      }

      //  Evaluate food quality for pheromone trail strength
      _foodQualityRating = EvaluateFoodQuality(_targetFoodSource);

      //  Collect food
      double harvestedAmount = SafeHarvestFood(_targetFoodSource, 8.0);
      if (harvestedAmount > 0)
      {
        SafeCollectFood(ant, harvestedAmount);

        //  Lay recruitment pheromone for high-quality food sources
        if (pheromoneMap != null && _foodQualityRating > 0.7)
        {
          double recruitmentStrength = _foodQualityRating * 4.0;
          pheromoneMap.DepositPheromone(
            ant.Position,
            PheromoneType.Recruitment,
            recruitmentStrength,
            ant.Id
          );
        }
      }

      //  Check if should continue collecting or return
      if (_targetFoodSource.IsExhausted || ant.CarriedFood >= ant.MaxCarryingCapacity * 0.8)
      {
        _targetFoodSource = null;
        CompleteCurrentTask(ant, context);
      }
    }

    private void ExecuteDropFoodTask(Ant ant, ISimulationContext context, PheromoneMap? pheromoneMap)
    {
      //  Drop food at nest
      double droppedFood = SafeDropFood(ant);
      if (droppedFood > 0 && ant.Colony != null)
      {
        SafeAddFoodToColony(ant.Colony, droppedFood);

        //  Lay home trail marker
        pheromoneMap?.DepositPheromone(
          ant.Position,
          PheromoneType.HomeTrail,
          2.0,
          ant.Id
        );
      }

      CompleteCurrentTask(ant, context);
    }

    private void ExecuteEvaluateFoodTask(Ant ant, ISimulationContext context, PheromoneMap? pheromoneMap)
    {
      //  Simplified food evaluation - in reality would involve chemical analysis
      if (_targetFoodSource != null)
      {
        _foodQualityRating = EvaluateFoodQuality(_targetFoodSource);

        //  Share information via pheromones
        if (pheromoneMap != null)
          if (_foodQualityRating > 0.8)
            //  Excellent food - strong recruitment signal
            pheromoneMap.DepositPheromone(
              ant.Position,
              PheromoneType.Recruitment,
              _foodQualityRating * 5.0,
              ant.Id
            );
          else if (_foodQualityRating < 0.3)
            //  Poor food - inhibition signal
            pheromoneMap.DepositPheromone(
              ant.Position,
              PheromoneType.Inhibition,
              2.0,
              ant.Id
            );
      }

      CompleteCurrentTask(ant, context);
    }

    // Pheromone trail laying logic
    private void LayPheromoneTrails(Ant ant, ISimulationContext context, PheromoneMap pheromoneMap)
    {
      var timeSinceLastDeposit = (DateTime.UtcNow - _lastPheromoneDeposit).TotalSeconds;
      if (timeSinceLastDeposit < _trailLayingInterval)
        return;

      if (ant.CarriedFood > 0)
      {
        //  Lay food trail when carrying food back to nest
        double intensity = CalculateFoodTrailIntensity(ant.CarriedFood, _foodQualityRating);
        pheromoneMap.DepositPheromone(ant.Position, PheromoneType.FoodTrail, intensity, ant.Id);
      }
      else if (_currentTaskType == ForagerTaskType.Explore)
        //  Lay weak exploration trail
        pheromoneMap.DepositPheromone(ant.Position, PheromoneType.HomeTrail, 0.8, ant.Id);

      _lastPheromoneDeposit = DateTime.UtcNow;
    }

    // -----------------
    //  Utility methods
    // -----------------

    private double CalculateFoodTrailIntensity(double carriedFood, double foodQuality)
    {
      double baseIntensity = Math.Min(5.0, carriedFood * 0.5);  //  Base intensity from food amount
      double qualityMultiplier = 0.5 + (foodQuality * 1.5);     //  Quality affects trail strength

      return baseIntensity * qualityMultiplier;
    }

    private double EvaluateFoodQuality(IFoodSource foodSource)
    {
      //  Simplified food quality evaluation
      double availabilityScore = Math.Min(1.0, foodSource.AvailableFood / 50.0);
      double accessibilityScore = 1.0;  //  Would consider distance, obstacles, etc.

      return (availabilityScore + accessibilityScore) / 2.0;
    }

    private Vector2 GenerateExplorationNoise(Random random)
    {
      double angle = random.NextDouble() * 2 * Math.PI;
      double magnitude = random.NextDouble();

      return new Vector2(Math.Cos(angle) * magnitude, Math.Sin(angle) * magnitude);
    }

    private Vector2 GenerateRandomDirection(Random random)
    {
      double angle = random.NextDouble() * 2 * Math.PI;

      return new Vector2(Math.Cos(angle), Math.Sin(angle));
    }

    private Position? GenerateExploreTarget(Ant ant, ISimulationContext context)
    {
      Position nestPosition = GetSafeNestPosition(ant) ?? ant.Position;

      return GenerateSafePosition(nestPosition, _forageRadius, context);
    }

    private PheromoneMap? GetPheromoneMap(ISimulationContext context)
    {
      //  This would be enhanced to get the pheromone map from the environment
      return context.Environment as IPheromoneEnvironment;
    }

    private void AssignBasicTask(Ant ant, ISimulationContext context)
    {
      //  Fallback to original behavior logic when no pheromone system is available
      if (ant.CarriedFood > 0)
      {
        _currentTaskType = ForagerTaskType.ReturnToNest;
        var nestPosition = GetSafeNestPosition(ant);
        if (nestPosition.HasValue)
        {
          var task = CreateTask(ActivityState.Moving, 0.1, nestPosition.Value);
          SetCurrentTask(task);
        }
      }
      else
        AssignExploreTask(ant, context);
    }

    private void AssignExploreTask(Ant ant, ISimulationContext context)
    {
      _currentTaskType = ForagerTaskType.Explore;
      var exploreTarget = GenerateExploreTarget(ant, context);
      if (exploreTarget != null)
      {
        var task = CreateTask(ActivityState.Moving, 0.25, exploreTarget);
        SetCurrentTask(task);
      }
    }

    // --------------------------------------------------------
    //  Forager task types including pheromone-based behaviors
    // --------------------------------------------------------

    private enum ForagerTaskType
    {
      SearchForFood,
      MoveToFood,
      CollectFood,
      ReturnToNest,
      DropFood,
      Explore,
      FollowTrail,
      RespondToRecruitment,
      EvaluateFood
    }
  }

  // -----------------------------------------------------------------
  //  Interface for environments that support pheromone communication
  // -----------------------------------------------------------------

  public interface IPheromoneEnvironment : IEnvironment
  {
    PheromoneMap PheromoneMap { get; }
  }
}
