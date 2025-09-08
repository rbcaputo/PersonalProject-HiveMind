using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for worker ants - focused on nest maintenance and general tasks
  /// </summary>
  public class WorkerBehavior : BaseBehavior
  {
    private Position? _workTarget;
    private long _lastTaskSwitch = 0;
    private readonly int _taskSwitchInterval = 200;
    private readonly double _workRadius = 15.0;

    public override void Update(Ant ant, ISimulationContext context)
    {
      SafeUpdate(ant, context, (a, ctx) =>
      {
        // Check if ant needs rest
        if (a.Energy < a.MaxEnergy * 0.2)
        {
          SafeSetState(a, ActivityState.Resting);
          SafeRestoreEnergy(a, 1.5);

          return;
        }

        // Switch tasks periodically or when idle
        if (ctx.CurrentTick - _lastTaskSwitch >= _taskSwitchInterval || a.CurrentState == ActivityState.Idle)
        {
          AssignNewTask(a, ctx);
          _lastTaskSwitch = ctx.CurrentTick;
        }

        ExecuteCurrentTask(a, ctx);
      });
    }

    private void AssignNewTask(Ant ant, ISimulationContext context)
    {
      try
      {
        if (!ValidateInputs(ant, context))
          return;

        double random = context.Random.NextDouble();
        Position? nestPosition = GetSafeNestPosition(ant);
        if (nestPosition == null)
        {
          // No nest available - default to idle
          SafeSetState(ant, ActivityState.Idle);

          return;
        }

        if (random < 6.0) // 60% chance to build/maintain
        {
          SafeSetState(ant, ActivityState.Building);
          _workTarget = GenerateSafePosition(nestPosition.Value, _workRadius * 0.67, context); // Stay closer for building
        }
        else if (random < 0.8) // 20% chance to care for young
        {
          SafeSetState(ant, ActivityState.Caring);
          _workTarget = GenerateSafePosition(nestPosition.Value, _workRadius * 0.33, context); // Stay very close for caring
        }
        else // 20% chance to patrol
        {
          SafeSetState(ant, ActivityState.Moving);
          _workTarget = GenerateSafePosition(nestPosition.Value, _workRadius, context); // Full range for patrol
        }

        // Fallback if position generation failed
        if (_workTarget == null)
          _workTarget = nestPosition;
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(AssignNewTask));
        ResetTaskState(ant);
      }
    }

    private void ExecuteCurrentTask(Ant ant, ISimulationContext context)
    {
      try
      {
        if (!ValidateInputs(ant, context) || !_workTarget.HasValue)
          return;

        double distanceToTarget = SafeCalculateDistance(ant.Position, _workTarget.Value);
        if (distanceToTarget > 1.0 && distanceToTarget != double.MaxValue)
        {
          // Move towards target
          if (!SafeMoveTo(ant, _workTarget.Value, context))
          {
            // Movement failed - reassign task
            _workTarget = null;
            return;
          }
        }
        else
        {
          // Arrived at target, perform task
          PerformTaskAtLocation(ant);
          _workTarget = null;
        }
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(ExecuteCurrentTask));
        ResetTaskState(ant);
      }
    }

    private static void PerformTaskAtLocation(Ant ant)
    {
      try
      {
        if (!IsAntOperational(ant))
          return;

        bool energyConsumed = false;

        switch (ant.CurrentState)
        {
          case ActivityState.Building:
            energyConsumed = SafeConsumeEnergy(ant, 0.5);
            break;
          case ActivityState.Caring:
            energyConsumed = SafeConsumeEnergy(ant, 0.3);
            break;
          case ActivityState.Moving:
            energyConsumed = SafeConsumeEnergy(ant, 0.2);
            break;
          default:
            energyConsumed = SafeConsumeEnergy(ant, 0.1);
            break;
        }
        ;

        // Set to idle after completing task (if energy consumption succeeded)
        if (energyConsumed)
          SafeSetState(ant, ActivityState.Idle);
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(PerformTaskAtLocation));
      }
    }

    /// <summary>
    /// Resets task state to safe defaults
    /// </summary>
    private void ResetTaskState(Ant ant)
    {
      try
      {
        _workTarget = null;
        SafeSetState(ant, ActivityState.Idle);
      }
      catch
      {
        _workTarget = null; // Ultimate fallback - clear work target at minimum
      }
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
