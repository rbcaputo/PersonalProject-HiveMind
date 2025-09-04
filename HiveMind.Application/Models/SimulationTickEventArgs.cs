namespace HiveMind.Application.Models
{
  /// <summary>
  /// Event args for simulation tick updates
  /// </summary>
  public class SimulationTickEventArgs(long tickNumber, double deltaTime, SimulationStatistics statistics) : EventArgs
  {
    public long TickNumber { get; } = tickNumber;
    public double DeltaTime { get; } = deltaTime;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public SimulationStatistics Statistics { get; } = statistics;
  }
}
