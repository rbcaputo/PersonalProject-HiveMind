using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for soldier ants - focused on defense and territory patrol
  /// </summary>
  public class SoldierBehavior : IAntBehavior
  {
    private Position? _patrolTarget;
    private readonly double _patrolRadius = 20.0;
    private long _lastPatrolUpdate = 0;
    private readonly int _patrolUpdateInterval = 150;

    public void Update(Ant ant, ISimulationContext context)
    {
      // Check if ant needs rest
      if (ant.Energy < ant.MaxEnergy * 0.25)
      {
        ant.SetState(ActivityState.Resting);
        ant.RestoreEnergy(1.2);

        return;
      }

      // Check for threats (simplified - in full implementation would detect enemies)
      if (DetectThreats())
      {
        EngageThreats(ant);

        return;
      }

      // Regular patrol behavior
      Patrol(ant, context);
    }

    private static bool DetectThreats()
    {
      // Simplified threat detection - would be expanded for multi-colony scenarios
      // For now, just return false as no threats exist
      return false;
    }

    private static void EngageThreats(Ant ant)
    {
      ant.SetState(ActivityState.Fighting);
      ant.ConsumeEnergy(1.0);
    }

    private void Patrol(Ant ant, ISimulationContext context)
    {
      if (context.CurrentTick - _lastPatrolUpdate >= _patrolUpdateInterval || _patrolTarget == null)
      {
        AssignPatrolTarget(ant, context);
        _lastPatrolUpdate = context.CurrentTick;
      }

      if (_patrolTarget != null)
      {
        double distanceToTarget = ant.Position.DistanceTo(_patrolTarget.Value);
        if (distanceToTarget > 1.5)
        {
          ant.SetState(ActivityState.Moving);
          ant.MoveTo(_patrolTarget.Value);
          ant.ConsumeEnergy(0.15);
        }
        else
        {
          // Reached patrol point - brief guard duty
          ant.SetState(ActivityState.Idle);
          _patrolTarget = null;
        }
      }
    }

    private void AssignPatrolTarget(Ant ant, ISimulationContext context)
    {
      // Create patrol route around colony perimeter
      Position nestPosition = ant.Colony.CenterPosition;
      double angle = context.Random.NextDouble() * 2 * Math.PI;
      double distance = _patrolRadius * (0.8 + context.Random.NextDouble() * 0.4); // 80% - 120% of patrol radius

      double x = nestPosition.X + Math.Cos(angle) * distance;
      double y = nestPosition.Y + Math.Sin(angle) * distance;

      _patrolTarget = new Position(x, y);
    }
  }
}
