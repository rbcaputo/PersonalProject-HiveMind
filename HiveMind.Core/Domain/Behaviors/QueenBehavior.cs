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
        if (ShouldRest(a))
          HandleRest(a);
        else if (ShouldReproduce(ctx))
          HandleReproduction(a, ctx);
        else
          HandleIdleBehavior(a, ctx);
      });
    }

    private static bool ShouldRest(Ant ant) =>
      ant.Energy < ant.MaxEnergy * 0.5;

    private static void HandleRest(Ant ant)
    {
      SafeSetState(ant, ActivityState.Resting);
      SafeRestoreEnergy(ant, 2.0);
    }

    private bool ShouldReproduce(ISimulationContext context) =>
      context.CurrentTick - _lastReproduction >= _reproductionInterval;

    private void HandleReproduction(Ant ant, ISimulationContext context)
    {
      SafeSetState(ant, ActivityState.Caring);
      _lastReproduction = context.CurrentTick;
    }

    private void HandleIdleBehavior(Ant ant, ISimulationContext context)
    {
      // 5% chance to move slightly
      if (context.Random.NextDouble() < 0.05)
      {
        Position currentPos = ant.Position;
        double deltaX = (context.Random.NextDouble() - 0.5) * _maxMovementDistance;
        double deltaY = (context.Random.NextDouble() - 0.5) * _maxMovementDistance;

        Position newPosition = new(currentPos.X + deltaX, currentPos.Y + deltaY);
        if (newPosition.IsValid && context.Environment.IsValidPosition(newPosition))
          SafeMoveTo(ant, newPosition, context);
      }
      else
        SafeSetState(ant, ActivityState.Idle);
    }
  }
}
