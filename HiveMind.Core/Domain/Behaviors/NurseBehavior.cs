using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for nurse ants - focused on caring for eggs
  /// </summary>
  public class NurseBehavior : BaseBehavior
  {
    private Position? _careTarget;
    private long _lastCareTask = 0;
    private readonly int _careTaskInterval = 120;
    private readonly double _careRadius = 5.0; // Stay close to nest

    public override void Update(Ant ant, ISimulationContext context)
    {
      SafeUpdate(ant, context, (a, ctx) =>
      {
        // Check if ant needs rest
        if (a.Energy < a.MaxEnergy * 0.2)
        {
          SafeSetState(a, ActivityState.Resting);
          SafeRestoreEnergy(ant, 1.3);

          return;
        }

        // Assign new care task periodically
        if (ctx.CurrentTick - _lastCareTask >= _careTaskInterval || a.CurrentState == ActivityState.Idle)
        {
          AssignCareTask(a, ctx);
          _lastCareTask = ctx.CurrentTick;
        }

        ExecuteCareTask(a, ctx);
      });
    }

    private void AssignCareTask(Ant ant, ISimulationContext context)
    {
      try
      {
        Position? nestPosition = GetSafeNestPosition(ant);
        if (nestPosition == null)
        {
          SafeSetState(ant, ActivityState.Idle);

          return;
        }

        _careTarget = GenerateSafePosition(nestPosition.Value, _careRadius, context);
        if (_careTarget != null)
          SafeSetState(ant, ActivityState.Caring);
        else
          SafeSetState(ant, ActivityState.Idle);
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(AssignCareTask));
        ResetCareState(ant);
      }
    }

    private void ExecuteCareTask(Ant ant, ISimulationContext context)
    {
      try
      {
        if (!ValidateInputs(ant, context) || !_careTarget.HasValue)
          return;

        double distanceToTarget = SafeCalculateDistance(ant.Position, _careTarget.Value);
        if (distanceToTarget > 0.5 && distanceToTarget != double.MaxValue)
        {
          if (!SafeMoveTo(ant, _careTarget.Value, context))
          {
            // Movement failed - reset care task
            _careTarget = null;

            return;
          }
        }
        else
        {
          // At care location - perform caring activities
          SafeSetState(ant, ActivityState.Caring);

          if (SafeConsumeEnergy(ant, 0.4))
          {
            _careTarget = null; // Task completed
            SafeSetState(ant, ActivityState.Idle);
          }
        }
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(ExecuteCareTask));
        ResetCareState(ant);
      }
    }

    private void ResetCareState(Ant ant)
    {
      try
      {
        _careTarget = null;
        SafeSetState(ant, ActivityState.Idle);
      }
      catch
      {
        _careTarget = null;
      }
    }
  }
}

