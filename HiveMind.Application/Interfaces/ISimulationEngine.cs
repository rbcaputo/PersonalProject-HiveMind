using HiveMind.Application.Models;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Application.Interfaces
{
  /// <summary>
  /// Main interface for the simulation engine
  /// </summary>
  public interface ISimulationEngine
  {
    /// <summary>
    /// Current simulation state
    /// </summary>
    SimulationState State { get; }

    /// <summary>
    /// Current simulation tick
    /// </summary>
    long CurrentTick { get; }

    /// <summary>
    /// Colonies in the simulation
    /// </summary>
    IReadOnlyCollection<IColony> Colonies { get; }

    /// <summary>
    /// Simulation environment
    /// </summary>
    IEnvironment Environment { get; }

    /// <summary>
    /// Initializes a new simulation
    /// </summary>
    Task InitializeAsync(SimulationConfiguration config);

    /// <summary>
    /// Starts the simulation
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Pauses the simulation
    /// </summary>
    Task PauseAsync();

    /// <summary>
    /// Stops the simulation
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Steps the simulation forward by one tick
    /// </summary>
    Task StepAsync();

    /// <summary>
    /// Event fired when simulation state changes
    /// </summary>
    event EventHandler<SimulationStateChangedEventArgs> StateChanged;

    /// <summary>
    /// Event fired on each simulation tick
    /// </summary>
    event EventHandler<SimulationTickEventArgs> Tick;
  }
}
