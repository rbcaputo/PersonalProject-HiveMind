using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Factory for creating ant behaviors based on role
  /// </summary>
  public static class AntBehaviorFactory
  {
    /// <summary>
    /// Configuration for behavior parameters
    /// </summary>
    public class BehaviorConfiguration
    {
      public double RestThreshold { get; set; } = 0.2;
      public double RestAmount { get; set; } = 1.0;
      public double UpdateInterval { get; set; } = 100;
      public double MovementRange { get; set; } = 15.0;
      public double TaskCompletionDistance { get; set; } = 1.0;
    }

    public static IAntBehavior CreateBehavior(AntCaste role)
    {
      if (!Enum.IsDefined(typeof(AntCaste), role))
        throw new ArgumentException($"Unknown ant role: {role}", nameof(role));

      return role switch
      {
        AntCaste.Queen => new QueenBehavior(),
        AntCaste.Worker => new WorkerBehavior(),
        AntCaste.Forager => new ForagerBehavior(),
        AntCaste.Soldier => new SoldierBehavior(),
        AntCaste.Nurse => new NurseBehavior(),
        AntCaste.Builder => new BuilderBehavior(),
        _ => throw new ArgumentException($"Unknown ant role: {role}")
      };
    }


    /// <summary>
    /// Creates behavior with custom configuration
    /// </summary>
    public static T CreateBehavior<T>(AntCaste role, Action<T>? configure = null) where T : class, IAntBehavior
    {
      var behavior = CreateBehavior(role) as T
        ?? throw new InvalidOperationException($"Behavior for role {role} is not of type {typeof(T).Name}");

      configure?.Invoke(behavior);

      return behavior;
    }

    /// <summary>
    /// Gets behavior configuration for a role
    /// </summary>
    public static BehaviorConfiguration GetDefaultConfiguration(AntCaste role) =>
      role switch
      {
        AntCaste.Queen => new() { RestThreshold = 0.5, RestAmount = 2.0, UpdateInterval = 100 },
        AntCaste.Worker => new() { RestThreshold = 0.2, RestAmount = 1.5, UpdateInterval = 200 },
        AntCaste.Forager => new() { RestThreshold = 0.15, RestAmount = 1.0, UpdateInterval = 50 },
        AntCaste.Soldier => new() { RestThreshold = 0.25, RestAmount = 1.2, UpdateInterval = 150 },
        AntCaste.Nurse => new() { RestThreshold = 0.2, RestAmount = 1.3, UpdateInterval = 120 },
        AntCaste.Builder => new() { RestThreshold = 0.3, RestAmount = 1.1, UpdateInterval = 180 },
        _ => new()
      };
  }
}
