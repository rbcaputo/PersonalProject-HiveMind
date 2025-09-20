namespace HiveMind.Core.Enums
{
  /// <summary>
  /// Defines the lifecycle stages of a bee from egg to death.
  /// Based on real bee biology and development cycles.
  /// </summary>
  public enum LifecycleStage
  {
    /// <summary>
    /// Egg stage - lasts approximately 3 days.
    /// </summary>
    Egg = 1,

    /// <summary>
    /// Larva stage - lasts approximately 6 days for workers, 6.5 days for drones, 5.5 days for queens.
    /// </summary>
    Larva = 2,

    /// <summary>
    /// Pupa stage - lasts approximately 12 days for workers, 14.5 days for drones, 7.5 days for queens.
    /// </summary>
    Pupa = 3,

    /// <summary>
    /// Adult stage - fully developed bee capable of performing caste-specific duties.
    /// </summary>
    Adult = 4,

    /// <summary>
    /// Death stage - bee has died and should be removed from active simulation.
    /// </summary>
    Dead = 5
  }
}
