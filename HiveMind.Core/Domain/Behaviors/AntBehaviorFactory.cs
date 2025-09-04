using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Factory for creating ant behaviors based on role
  /// </summary>
  public class AntBehaviorFactory
  {
    public static IAntBehavior CreateBehavior(AntRole role) =>
      role switch
      {
        AntRole.Queen => new QueenBehavior(),
        AntRole.Worker => new WorkerBehavior(),
        AntRole.Forager => new ForagerBehavior(),
        AntRole.Soldier => new SoldierBehavior(),
        AntRole.Nurse => new NurseBehavior(),
        AntRole.Builder => new BuilderBehavior(),
        _ => throw new ArgumentException($"Unknown ant role: {role}")
      };
  }
}
