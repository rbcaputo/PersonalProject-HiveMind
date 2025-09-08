using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for worker ants - focused on nest maintenance and general tasks
  /// </summary>
  public class WorkerBehavior : TaskBasedBehavior
  {
    private readonly double _workRadius = 15.0;

    protected override int TaskUpdateInterval => 200;
    protected override double GetRestThreshold() => 0.2;
    protected override double GetRestAmount() => 1.5;

    protected override void AssignNewTask(Ant ant, ISimulationContext context)
    {
      Position? nestPosition = GetSafeNestPosition(ant);
      if (nestPosition == null)
      {
        ClearCurrentTask();

        return;
      }

      double random = context.Random.NextDouble();
      BehaviorTask newTask;

      if (random < 0.6) // 60% chance to build/maintain
      {
        Position? buildPosition = GenerateSafePosition(nestPosition.Value, _workRadius * 0.67, context);
        newTask = CreateTask(ActivityState.Building, 0.5, buildPosition);
      }
      else if (random < 0.8) // 20% chance to care for young
      {
        Position? carePosition = GenerateSafePosition(nestPosition.Value, _workRadius * 0.33, context);
        newTask = CreateTask(ActivityState.Caring, 0.3, carePosition);
      }
      else // 20% chance to patrol
      {
        Position? patrolPosition = GenerateSafePosition(nestPosition.Value, _workRadius, context);
        newTask = CreateTask(ActivityState.Moving, 0.2, patrolPosition);
      }

      SetCurrentTask(newTask);
    }
  }
}
