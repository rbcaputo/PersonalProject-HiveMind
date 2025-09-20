using HiveMind.Core.Events;

namespace HiveMind.Core.Simulation
{
  /// <summary>
  /// Base class for simulation event arguments.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="SimulationEventArgs"/> class.
  /// </remarks>
  /// <param name="state">The current simulation state.</param>
  public abstract class SimulationEventArgs(SimulationState state) : EventArgs
  {
    /// <summary>
    /// Gets the simulation state at the time of the event.
    /// </summary>
    public SimulationState State { get; } = state ?? throw new ArgumentNullException(nameof(state));

    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
  }

  /// <summary>
  /// Event arguments for simulation started event.
  /// </summary>
  public sealed class SimulationStartedEventArgs(SimulationState state)
    : SimulationEventArgs(state)
  { }

  /// <summary>
  /// Event arguments for simulation stopped event.
  /// </summary>
  public sealed class SimulationStoppedEventArgs(SimulationState state)
    : SimulationEventArgs(state)
  { }

  /// <summary>
  /// Event arguments for simulation paused event.
  /// </summary>
  public sealed class SimulationPausedEventArgs(SimulationState state)
    : SimulationEventArgs(state)
  { }

  /// <summary>
  /// Event arguments for simulation resumed event.
  /// </summary>
  public sealed class SimulationResumedEventArgs(SimulationState state)
    : SimulationEventArgs(state)
  { }

  /// <summary>
  /// Event arguments for simulation completed event.
  /// </summary>
  public sealed class SimulationCompletedEventArgs(SimulationState state)
    : SimulationEventArgs(state)
  { }

  /// <summary>
  /// Event arguments for simulation saved event.
  /// </summary>
  public sealed class SimulationSavedEventArgs(SimulationState state)
    : SimulationEventArgs(state)
  { }

  /// <summary>
  /// Event arguments for bee-related events.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="BeeEventArgs"/> class.
  /// </remarks>
  /// <param name="beeEvent">The bee event.</param>
  /// <param name="state">The simulation state.</param>
  public sealed class BeeEventArgs(IBeeEvent beeEvent, SimulationState state)
    : SimulationEventArgs(state)
  {
    /// <summary>
    /// Gets the bee event that occurred.
    /// </summary>
    public IBeeEvent BeeEvent { get; } = beeEvent ?? throw new ArgumentNullException(nameof(beeEvent));
  }

  /// <summary>
  /// Event arguments for hive-related events.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="HiveEventArgs"/> class.
  /// </remarks>
  /// <param name="hiveEvent">The hive event.</param>
  /// <param name="state">The simulation state.</param>
  public sealed class HiveEventArgs(IHiveEvent hiveEvent, SimulationState state)
    : SimulationEventArgs(state)
  {
    /// <summary>
    /// Gets the hive event that occurred.
    /// </summary>
    public IHiveEvent HiveEvent { get; } = hiveEvent ?? throw new ArgumentNullException(nameof(hiveEvent));
  }

  // Interface definitions for events
  public interface IBeeEvent
  {
    Guid EventId { get; }
    DateTime Timestamp { get; }
    BeeEventType EventType { get; }
  }

  public interface IHiveEvent
  {
    Guid EventId { get; }
    DateTime Timestamp { get; }
    HiveEventType EventType { get; }
  }
}
