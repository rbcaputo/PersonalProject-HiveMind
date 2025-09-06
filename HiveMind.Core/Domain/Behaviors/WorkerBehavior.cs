using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for worker ants - focused on nest maintenance and general tasks
  /// </summary>
  public class WorkerBehavior : IAntBehavior
  {
    private Position? _workTarget;
    private long _lastTaskSwitch = 0;
    private readonly int _taskSwitchInterval = 200;

    public void Update(Ant ant, ISimulationContext context)
    {
      // Check if ant needs rest
      if (ant.Energy < ant.MaxEnergy * 0.2)
      {
        ant.SetState(ActivityState.Resting);
        ant.RestoreEnergy(1.5);
        return;
      }

      // Switch tasks periodically
      if (context.CurrentTick - _lastTaskSwitch >= _taskSwitchInterval || ant.CurrentState == ActivityState.Idle)
      {
        AssignNewTask(ant, context);
        _lastTaskSwitch = context.CurrentTick;
      }

      ExecuteCurrentTask(ant);
    }

    private void AssignNewTask(Ant ant, ISimulationContext context)
    {
      double random = context.Random.NextDouble();

      if (random < 0.6) // 60% chance to build/maintain
      {
        ant.SetState(ActivityState.Building);
        _workTarget = GetRandomPositionNearNest(ant, context, 10.0);
      }
      else if (random < 0.8) // 20% chance to care for young
      {
        ant.SetState(ActivityState.Caring);
        _workTarget = GetRandomPositionNearNest(ant, context, 5.0);
      }
      else // 20% chance to patrol
      {
        ant.SetState(ActivityState.Moving);
        _workTarget = GetRandomPositionNearNest(ant, context, 15.0);
      }
    }

    private void ExecuteCurrentTask(Ant ant)
    {
      if (_workTarget.HasValue)
      {
        double distanceToTarget = ant.Position.DistanceTo(_workTarget.Value);

        if (distanceToTarget > 1.0)
          ant.MoveTo(_workTarget.Value);
        else
        {
          // Arrived at target, perform task
          PerformTaskAtLocation(ant);
          _workTarget = null;
        }
      }
    }

    private static void PerformTaskAtLocation(Ant ant)
    {
      switch (ant.CurrentState)
      {
        case ActivityState.Building:
          ant.ConsumeEnergy(0.5);
          break;
        case ActivityState.Caring:
          ant.ConsumeEnergy(0.3);
          break;
        default:
          ant.ConsumeEnergy(0.2);
          break;
      }

      // After completing task, brief idle period
      ant.SetState(ActivityState.Idle);
    }

    private static Position GetRandomPositionNearNest(Ant ant, ISimulationContext context, double radius)
    {
      Position nestPosition = ant.Colony.CenterPosition;
      double angle = context.Random.NextDouble() * 2 * Math.PI;
      double distance = context.Random.NextDouble() * radius;

      double x = nestPosition.X + Math.Cos(angle) * distance;
      double y = nestPosition.Y + Math.Sin(angle) * distance;

      return new(x, y);
    }
  }
}
