using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for the queen ant - focused on reproduction and colony management
  /// </summary>
  public class QueenBehavior : IAntBehavior
  {
    private long _lastReproduction = 0;
    private readonly int _reproductionInterval = 100;

    public void Update(Ant ant, ISimulationContext context)
    {
      // Queens primarily stay near the nest center and reproduce
      if (ant.Energy < ant.MaxEnergy * 0.5)
      {
        // Rest to restore energy
        ant.SetState(ActivityState.Resting);
        ant.RestoreEnergy(2.0);
      }
      else if (context.CurrentTick - _lastReproduction >= _reproductionInterval)
      {
        // Reproduction cycle
        ant.SetState(ActivityState.Caring);
        _lastReproduction = context.CurrentTick;
      }
      else
      {
        // Idle behavior - minimal movement
        if (context.Random.NextDouble() < 0.05) // 5% chance to move slightly
        {
          Position currentPos = ant.Position;
          double newX = currentPos.X + (context.Random.NextDouble() - 0.5) * 2;
          double newY = currentPos.Y + (context.Random.NextDouble() - 0.5) * 2;
          ant.MoveTo(new Position(newX, newY));
        }
        else
          ant.SetState(ActivityState.Idle);
      }
    }
  }
}
