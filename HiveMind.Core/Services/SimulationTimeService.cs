namespace HiveMind.Core.Services
{
  /// <summary>
  /// Implementation of simulation time service for managing simulation time progression.
  /// </summary>
  public sealed class SimulationTimeService : ISimulationTimeService
  {
    private DateTime _currentTime;
    private readonly DateTime _startTime;
    private long _currentTick;

    /// <summary>
    /// Gets the current simulation time.
    /// </summary>
    public DateTime CurrentTime => _currentTime;

    /// <summary>
    /// Gets the time when the simulation started.
    /// </summary>
    public DateTime StartTime => _startTime;

    /// <summary>
    /// Gets the total elapsed simulation time.
    /// </summary>
    public TimeSpan ElapsedTime => _currentTime - _startTime;

    /// <summary>
    /// Gets the current simulation tick number.
    /// </summary>
    public long CurrentTick => _currentTick;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationTimeService"/> class.
    /// </summary>
    /// <param name="startTime">The starting time for the simulation.</param>
    public SimulationTimeService(DateTime? startTime = null)
    {
      _startTime = startTime ?? DateTime.UtcNow;
      _currentTime = _startTime;
      _currentTick = 0;
    }

    /// <summary>
    /// Advances the simulation time by one tick.
    /// Each tick represents 1 minute of simulation time
    /// </summary>
    public void AdvanceTick()
    {
      _currentTick++;
      _currentTime = _currentTime.AddMinutes(1);
    }

    /// <summary>
    /// Sets the simulation time to a specific value.
    /// </summary>
    /// <param name="time">The time to set.</param>
    public void SetTime(DateTime time) => _currentTime = time;

    /// <summary>
    /// Resets the simulation time to the start time.
    /// </summary>
    public void Reset()
    {
      _currentTime = _startTime;
      _currentTick = 0;
    }
  }
}
