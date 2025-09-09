using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for soldier ants - focused on defense and territory patrol
  /// </summary>
  public class SoldierBehavior : TaskBasedBehavior
  {
    private readonly double _patrolRadius = 20.0;

    protected override int TaskUpdateInterval => 150;
    protected override double GetRestThreshold() => 0.25;
    protected override double GetRestAmount() => 1.2;
    protected override double TaskCompletionDistance => 1.5;

    protected override void AssignNewTask(Ant ant, ISimulationContext context)
    {
      // Check for threats first
      if (DetectThreats())
      {
        BehaviorTask fightTask = CreateTask(ActivityState.Fighting, 1.0);
        SetCurrentTask(fightTask);

        return;
      }

      // Regular patrol task
      Position? nestPosition = GetSafeNestPosition(ant);
      if (nestPosition == null)
      {
        ClearCurrentTask();

        return;
      }

      Position? patrolPosition = GeneratePatrolPosition(nestPosition.Value, context);
      BehaviorTask patrolTask = CreateTask(ActivityState.Moving, 0.15, patrolPosition);

      SetCurrentTask(patrolTask);
    }

    protected override void OnTaskCompleted(Ant ant, ISimulationContext context, BehaviorTask task)
    {
      if (task.ActivityState == ActivityState.Moving)
        // Brief guard duty at patrol point
        SafeSetState(ant, ActivityState.Idle);
    }

    private bool DetectThreats()
    {
      // Simplified threat detection - would be expanded for multi-colony scenarios
      return false;
    }

    private Position? GeneratePatrolPosition(Position nestCenter, ISimulationContext context)
    {
      double angle = context.Random.NextDouble() * 2 * Math.PI;
      double distance = _patrolRadius * (0.8 + context.Random.NextDouble() * 0.4);

      return GenerateSafePosition(nestCenter, distance, context);
    }
  }
}
