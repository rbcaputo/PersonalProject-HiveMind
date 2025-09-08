using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for soldier ants - focused on defense and territory patrol
  /// </summary>
  public class SoldierBehavior : BaseBehavior
  {
    private Position? _patrolTarget;
    private readonly double _patrolRadius = 20.0;
    private long _lastPatrolUpdate = 0;
    private readonly int _patrolUpdateInterval = 150;

    public override void Update(Ant ant, ISimulationContext context)
    {
      SafeUpdate(ant, context, (a, ctx) =>
      {
        // Check if ant needs rest
        if (a.Energy < a.MaxEnergy * 0.25)
        {
          SafeSetState(a, ActivityState.Resting);
          SafeRestoreEnergy(a, 1.2);

          return;
        }

        // Check for threats (simplified - in full implementation would detect enemies)
        if (DetectThreats(a, ctx))
        {
          EngageThreats(a);

          return;
        }

        // Regular patrol behavior
        Patrol(a, ctx);
      });
    }

    private bool DetectThreats(Ant ant, ISimulationContext context)
    {
      try
      {
        // Simplified threat detection - would be expanded for multi-colony scenarios
        // For now, just return false as no threats exist
        // Future: Could check for enemy ants, predators, etc.

        return false;
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(DetectThreats));

        return false; // Assume no threats on error
      }
    }

    private static void EngageThreats(Ant ant)
    {
      try
      {
        if (!IsAntOperational(ant))
          return;

        SafeSetState(ant, ActivityState.Fighting);
        SafeConsumeEnergy(ant, 1.0);
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(EngageThreats));
      }
    }

    private void Patrol(Ant ant, ISimulationContext context)
    {
      try
      {
        if (!ValidateInputs(ant, context))
          return;

        // Update patrol target periodically
        if (context.CurrentTick - _lastPatrolUpdate >= _patrolUpdateInterval || _patrolTarget == null)
        {
          AssignPatrolTarget(ant, context);
          _lastPatrolUpdate = context.CurrentTick;
        }

        if (_patrolTarget != null)
        {
          double distanceTotarget = SafeCalculateDistance(ant.Position, _patrolTarget.Value);
          if (distanceTotarget > 1.5 && distanceTotarget != double.MaxValue)
          {
            SafeSetState(ant, ActivityState.Moving);

            if (SafeMoveTo(ant, _patrolTarget.Value, context))
              SafeConsumeEnergy(ant, 0.15);
            else
              // Movement failed - get new patrol target
              _patrolTarget = null;
          }
          else
          {
            // Reached patrol point - brief guard duty
            SafeSetState(ant, ActivityState.Moving);
            _patrolTarget = null;
          }
        }
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(Patrol));
        ResetPatrolState(ant);
      }
    }

    private void AssignPatrolTarget(Ant ant, ISimulationContext context)
    {
      try
      {
        Position? nestPosition = GetSafeNestPosition(ant);
        if (nestPosition == null)
        {
          _patrolTarget = null;

          return;
        }

        // Create patrol route around colony perimeter with safe distance variation
        double baseDistance = _patrolRadius;
        double distanceVariation = baseDistance * 0.4; // 40% variation
        double minDistance = baseDistance * 0.8;
        double maxDistance = baseDistance * 1.2;

        double angle = context.Random.NextDouble() * 2 * Math.PI;
        double distance = minDistance + (context.Random.NextDouble() * distanceVariation);

        double x = nestPosition.Value.X + Math.Cos(angle) * distance;
        double y = nestPosition.Value.Y + Math.Sin(angle) * distance;

        Position candidateTarget = new(x, y);

        // Validate patrol target is within environment
        if (candidateTarget.IsValid && context.Environment.IsValidPosition(candidateTarget))
          _patrolTarget = candidateTarget;
        else
          // Fallback to closer position
          _patrolTarget = GenerateSafePosition(nestPosition.Value, _patrolRadius * 0.5, context);
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(AssignPatrolTarget));
        _patrolTarget = null;
      }
    }

    private void ResetPatrolState(Ant ant)
    {
      try
      {
        _patrolTarget = null;
        SafeSetState(ant, ActivityState.Idle);
      }
      catch
      {
        _patrolTarget = null; // At minimum, clear patrol target
      }
    }
  }
}
