namespace HiveMind.Application.Models
{
  /// <summary>
  /// Event args for simulation state changes
  /// </summary>
  public class SimulationStateChangedEventArgs(SimulationState previousState, SimulationState newState) : EventArgs
  {
    public SimulationState PreviousState { get; } = previousState;
    public SimulationState NewState { get; } = newState;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
  }
}
