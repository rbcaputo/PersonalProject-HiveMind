using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Enums;

namespace HiveMind.Core.Domain.Interfaces
{
  /// <summary>
  /// Base interface for all insects in the simulation
  /// </summary>
  public interface IInsect
  {
    Guid Id { get; }
    InsectType Type { get; }
    Position Position { get; }
    ActivityState CurrentState { get; }
    double Health { get; }
    double Energy { get; }
    int AgeDays { get; }
    bool IsAlive { get; }

    /// <summary>
    /// Update's the insect's state for the current simulation tick
    /// </summary>
    void Update(ISimulationContext context);

    /// <summary>
    /// Moves the insect to a new position
    /// </summary>
    void MoveTo(Position newPosition);

    /// <summary>
    /// Consumes energy for activities
    /// </summary>
    void ConsumeEnergy(double amount);

    /// <summary>
    /// Restores energy (e.g., from food)
    /// </summary>
    void RestoreEnergy(double amount);
  }
}
