using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Factory for creating ant behaviors based on role
  /// </summary>
  public static class AntBehaviorFactory
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

    /// <summary>
    /// Creates behavior with custom configuration
    /// </summary>
    public static T CreateBehavior<T>(AntRole role, Action<T>? configure = null) where T : class, IAntBehavior
    {
      var behavior = CreateBehavior(role) as T
        ?? throw new InvalidOperationException($"Behavior for role {role} is not of type {typeof(T).Name}");

      configure?.Invoke(behavior);

      return behavior;
    }

    /// <summary>
    /// Gets behavior configuration for a role
    /// </summary>
    public static BehaviorConfigurarion GetDefaultConfiguration(AntRole role) =>
      role switch
      {
        AntRole.Queen => new() { RestThreshold = 0.5, RestAmount = 2.0, UpddateInterval = 100 },
        AntRole.Worker => new() { RestThreshold = 0.2, RestAmount = 1.5, UpddateInterval = 200 },
        AntRole.Forager => new() { RestThreshold = 0.15, RestAmount = 1.0, UpddateInterval = 50 },
        AntRole.Soldier => new() { RestThreshold = 0.25, RestAmount = 1.2, UpddateInterval = 150 },
        AntRole.Nurse => new() { RestThreshold = 0.2, RestAmount = 1.3, UpddateInterval = 120 },
        AntRole.Builder => new() { RestThreshold = 0.3, RestAmount = 1.1, UpddateInterval = 180 },
        _ => new()
      };

    /// <summary>
    /// Configuration for behavior parameters
    /// </summary>
    public class BehaviorConfigurarion
    {
      public double RestThreshold { get; set; } = 0.2;
      public double RestAmount { get; set; } = 1.0;
      public double UpddateInterval { get; set; } = 100;
      public double MovementRange { get; set; } = 15.0;
      public double TaskCompletionDistance { get; set; } = 1.0;
    }
  }
}
