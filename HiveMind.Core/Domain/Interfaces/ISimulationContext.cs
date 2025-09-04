namespace HiveMind.Core.Domain.Interfaces
{
  /// <summary>
  /// Provides context and services for simulation entities
  /// </summary>
  public interface ISimulationContext
  {
    /// <summary>
    /// Current simulation tick
    /// </summary>
    long CurrentTick { get; }

    /// <summary>
    /// Simulation time step in seconds
    /// </summary>
    double DeltaTime { get; }

    /// <summary>
    /// Environment information
    /// </summary>
    IEnvironment Environment { get; }

    /// <summary>
    /// Random number generator for simulation
    /// </summary>
    Random Random { get; }
  }
}
