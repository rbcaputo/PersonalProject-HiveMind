using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for builder ants - focused on nest construction and maintenance
  /// </summary>
  public class BuilderBehavior : IAntBehavior
  {
    private Position? _buildTarget;
    private long _lastBuildTask = 0;
    private readonly int _lastBuildTaskInterval = 180;

    public void Update(Ant ant, ISimulationContext context)
    {
      // Check if ant needs rest
      if (ant.Energy < ant.MaxEnergy * 0.3)
      {
        ant.SetState(ActivityState.Resting);
        ant.RestoreEnergy(1.1);

        return;
      }

      if (context.CurrentTick - _lastBuildTask >= _lastBuildTaskInterval || ant.CurrentState == ActivityState.Idle)
      {
        AssignBuildTask(ant, context);
        _lastBuildTask = context.CurrentTick;
      }

      ExecuteBuildTask(ant);
    }

    private void AssignBuildTask(Ant ant, ISimulationContext context)
    {
      // Assign construction target around the colony
      Position nestPosition = ant.Colony.CenterPosition;
      double angle = context.Random.NextDouble() * 2 * Math.PI;
      double distance = context.Random.NextDouble() * 15.0; // Moderate range for construction

      double x = nestPosition.X + Math.Cos(angle) * distance;
      double y = nestPosition.Y + Math.Sin(angle) * distance;

      _buildTarget = new Position(x, y);
      ant.SetState(ActivityState.Building);
    }

    private void ExecuteBuildTask(Ant ant)
    {
      if (_buildTarget.HasValue)
      {
        double distanceToTarget = ant.Position.DistanceTo(_buildTarget.Value);
        if (distanceToTarget > 1.0)
          ant.MoveTo(_buildTarget.Value);
        else
        {
          // Perform building activities
          ant.SetState(ActivityState.Building);
          ant.ConsumeEnergy(0.6);
          _buildTarget = null;
        }
      }
    }
  }
}
