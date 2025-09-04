namespace HiveMind.Application.Models
{
  /// <summary>
  /// Represents the current state of the simulation
  /// </summary>
  public enum SimulationState
  {
    Uninitialized,
    Initialized,
    Running,
    Paused,
    Stopped,
    Error
  }
}
