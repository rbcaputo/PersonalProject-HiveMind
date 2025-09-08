using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for the queen ant - focused on reproduction and colony management
  /// </summary>
  public class QueenBehavior : BaseBehavior
  {
    private long _lastReproduction = 0;
    private readonly int _reproductionInterval = 100;
    private readonly double _maxMovementDistance = 2.0; // Queens move very little

    public override void Update(Ant ant, ISimulationContext context)
    {
      SafeUpdate(ant, context, (a, ctx) =>
      {
        // Queens primarily stay near the nest center and reproduce
        if (a.Energy < a.MaxEnergy * 0.5)
        {
          // Rest to restore energy
          SafeSetState(a, ActivityState.Resting);
          SafeRestoreEnergy(a, 2.0);
        }
        else if (ctx.CurrentTick - _lastReproduction >= _reproductionInterval)
        {
          // Reproduction cycle
          SafeSetState(a, ActivityState.Caring);
          _lastReproduction = ctx.CurrentTick;
        }
        else
          // Idle behavior - minimal movement
          PerformIdleBehavior(a, ctx);
      });
    }

    private void PerformIdleBehavior(Ant ant, ISimulationContext context)
    {
      try
      {
        if (!ValidateInputs(ant, context))
          return;

        // 5% chance to move slightly
        if (context.Random.NextDouble() < 0.05)
        {
          Position currentPos = ant.Position;
          double deltaX = (context.Random.NextDouble() - 0.5) * _maxMovementDistance;
          double deltaY = (context.Random.NextDouble() - 0.5) * _maxMovementDistance;

          Position newPosition = new(currentPos.X + deltaX, currentPos.Y + deltaY);
          // Only move if the new position is valid
          if (newPosition.IsValid && context.Environment.IsValidPosition(newPosition))
            SafeMoveTo(ant, newPosition, context);
        }
        else
          SafeSetState(ant, ActivityState.Idle);
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(PerformIdleBehavior));
        SafeSetState(ant, ActivityState.Idle);
      }
    }
  }
}
