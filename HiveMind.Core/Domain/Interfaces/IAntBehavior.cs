using HiveMind.Core.Domain.Entities;

namespace HiveMind.Core.Domain.Interfaces
{
  /// <summary>
  /// Defines behavior patterns for ants
  /// </summary>
  public interface IAntBehavior
  {
    /// <summary>
    /// Updates the ant's behavior for the current simulation tick
    /// </summary>
    void Update(Ant ant, ISimulationContext context);
  }
}
