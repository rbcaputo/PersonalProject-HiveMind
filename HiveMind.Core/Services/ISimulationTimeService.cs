namespace HiveMind.Core.Services
{
  /// <summary>
  /// Service for managing simulation time and providing time-related operations.
  /// </summary>
  public interface ISimulationTimeService
  {
    /// <summary>
    /// Gets the current simulation time.
    /// </summary>
    DateTime CurrentTime { get; }

    /// <summary>
    /// Gets the time when the simulation started.
    /// </summary>
    DateTime StartTime { get; }

    /// <summary>
    /// Gets the total elapsed simulation time.
    /// </summary>
    TimeSpan ElapsedTime { get; }

    /// <summary>
    /// Gets the current simulation tick number.
    /// </summary>
    long CurrentTick { get; }

    /// <summary>
    /// Advances the simulation time by one tick.
    /// </summary>
    void AdvanceTick();

    /// <summary>
    /// Sets the simulation time to a specific value.
    /// </summary>
    /// <param name="time">The time to set.</param>
    void SetTime(DateTime time);

    /// <summary>
    /// Resets the simulation time to the start time.
    /// </summary>
    void Reset();
  }
}
