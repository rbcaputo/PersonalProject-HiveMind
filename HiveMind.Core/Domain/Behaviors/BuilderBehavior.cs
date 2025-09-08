using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for builder ants - focused on nest construction and maintenance
  /// </summary>
  public class BuilderBehavior : BaseBehavior
  {
    private Position? _buildTarget;
    private long _lastBuildTask = 0;
    private readonly int _lastBuildTaskInterval = 180;
    private readonly double _buildRadius = 15.0;

    public override void Update(Ant ant, ISimulationContext context)
    {
      SafeUpdate(ant, context, (a, ctx) =>
      {
        // Check if ant needs rest
        if (a.Energy < a.MaxEnergy * 0.3)
        {
          SafeSetState(a, ActivityState.Resting);
          SafeRestoreEnergy(a, 1.1);

          return;
        }

        // Assign new build task periodically
        if (ctx.CurrentTick - _lastBuildTask >= _lastBuildTaskInterval || a.CurrentState == ActivityState.Idle)
        {
          AssignBuildTask(a, ctx);
          _lastBuildTask = ctx.CurrentTick;
        }

        ExecuteBuildTask(a, ctx);
      });
    }

    private void AssignBuildTask(Ant ant, ISimulationContext context)
    {
      try
      {
        Position? nestPosition = GetSafeNestPosition(ant);
        if (nestPosition == null)
        {
          SafeSetState(ant, ActivityState.Idle);

          return;
        }

        _buildTarget = GenerateSafePosition(nestPosition.Value, _buildRadius, context);
        if (_buildTarget != null)
          SafeSetState(ant, ActivityState.Building);
        else
          SafeSetState(ant, ActivityState.Idle);
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(AssignBuildTask));
        ResetBuildState(ant);
      }
    }

    private void ExecuteBuildTask(Ant ant, ISimulationContext context)
    {
      try
      {
        if (!ValidateInputs(ant, context) || !_buildTarget.HasValue)
          return;

        double distanceToTarget = SafeCalculateDistance(ant.Position, _buildTarget.Value);
        if (distanceToTarget > 1.0 && distanceToTarget != double.MaxValue)
        {
          if (!SafeMoveTo(ant, _buildTarget.Value, context))
          {
            // Movement failed - reset build task
            _buildTarget = null;

            return;
          }
        }
        else
        {
          // At build location - perform building activities
          SafeSetState(ant, ActivityState.Building);

          if (SafeConsumeEnergy(ant, 0.6))
          {
            _buildTarget = null; // Task completed
            SafeSetState(ant, ActivityState.Idle);
          }
        }
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(ExecuteBuildTask));
        ResetBuildState(ant);
      }
    }

    private void ResetBuildState(Ant ant)
    {
      try
      {
        _buildTarget = null;
        SafeSetState(ant, ActivityState.Idle);
      }
      catch
      {
        _buildTarget = null;
      }
    }
  }
}
