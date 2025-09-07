using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for nurse ants - focused on caring for eggs
  /// </summary>
  public class NurseBehavior : IAntBehavior
  {
    private Position? _careTarget;
    private long _lastCareTask = 0;
    private readonly int _careTaskInterval = 120;

    public void Update(Ant ant, ISimulationContext context)
    {
      // Check if ant needs rest
      if (ant.Energy < ant.MaxEnergy * 0.2)
      {
        ant.SetState(ActivityState.Resting);
        ant.RestoreEnergy(1.3);

        return;
      }

      if (context.CurrentTick - _lastCareTask >= _careTaskInterval || ant.CurrentState == ActivityState.Idle)
      {
        AssignCareTask(ant, context);
        _lastCareTask = context.CurrentTick;
      }

      ExecuteCareTask(ant);
    }

    private void AssignCareTask(Ant ant, ISimulationContext context)
    {
      // Assign location near nest center for caring activities
      Position nestPosition = ant.Colony.CenterPosition;
      double angle = context.Random.NextDouble() * 2 * Math.PI;
      double distance = context.Random.NextDouble() * 5.0; // Stay close to nest

      double x = nestPosition.X + Math.Cos(angle) * distance;
      double y = nestPosition.Y + Math.Sin(angle) * distance;

      _careTarget = new(x, y);
      ant.SetState(ActivityState.Caring);
    }

    private void ExecuteCareTask(Ant ant)
    {
      if (_careTarget.HasValue)
      {
        double distanceToTarget = ant.Position.DistanceTo(_careTarget.Value);
        if (distanceToTarget > 0.5)
          ant.MoveTo(_careTarget.Value);
        else
        {
          // Perform caring activities
          ant.SetState(ActivityState.Caring);
          ant.ConsumeEnergy(0.4);
          _careTarget = null;
        }
      }
    }
  }
}
