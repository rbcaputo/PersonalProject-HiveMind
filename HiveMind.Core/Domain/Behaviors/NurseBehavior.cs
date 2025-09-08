using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for nurse ants - focused on caring for eggs
  /// </summary>
  public class NurseBehavior : TaskBasedBehavior
  {
    private readonly double _careRadius = 5.0;

    protected override int TaskUpdateInterval => 120;
    protected override double GetRestThreshold() => 0.2;
    protected override double GetRestAmount() => 1.3;
    protected override double TaskCompletionDistance => 0.5;

    protected override void AssignNewTask(Ant ant, ISimulationContext context)
    {
      Position? nestPosition = GetSafeNestPosition(ant);
      if (nestPosition == null)
      {
        ClearCurrentTask();

        return;
      }

      Position? carePosition = GenerateSafePosition(nestPosition.Value, _careRadius, context);
      BehaviorTask careTask = CreateTask(ActivityState.Caring, 0.4, carePosition);

      SetCurrentTask(careTask);
    }
  }
}

