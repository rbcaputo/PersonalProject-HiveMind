using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for builder ants - focused on nest construction and maintenance
  /// </summary>
  public class BuilderBehavior : TaskBasedBehavior
  {
    private readonly double _buildRadius = 15.0;

    protected override int TaskUpdateInterval => 180;
    protected override double GetRestThreshold() => 0.3;
    protected override double GetRestAmount() => 1.1;

    protected override void AssignNewTask(Ant ant, ISimulationContext context)
    {
      Position? nestPosition = GetSafeNestPosition(ant);
      if (nestPosition == null)
      {
        ClearCurrentTask();

        return;
      }

      Position? buildPosition = GenerateSafePosition(nestPosition.Value, _buildRadius, context);
      BehaviorTask buildTask = CreateTask(ActivityState.Building, 0.6, buildPosition);

      SetCurrentTask(buildTask);
    }
  }
}
