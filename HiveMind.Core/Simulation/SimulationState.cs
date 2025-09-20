using HiveMind.Core.Entities;
using HiveMind.Core.Enums;

namespace HiveMind.Core.Simulation
{
  /// <summary>
  /// Represents the current state of the simulation including all entities and statistics.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="SimulationState"/> class.
  /// </remarks>
  /// <param name="environment">The initial environment state.</param>
  public sealed class SimulationState(Entities.Environment environment)
  {
    private readonly List<Beehive> _beehives = [];
    private Entities.Environment _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    private SimulationStatus _status = SimulationStatus.Stopped;
    private DateTime _lastSaveTime = DateTime.UtcNow;
    private long _totalTicks = 0;

    /// <summary>
    /// Gets all beehives in the simulation.
    /// </summary>
    public IReadOnlyList<Beehive> Beehives => _beehives.AsReadOnly();

    /// <summary>
    /// Gets the current environment state.
    /// </summary>
    public Entities.Environment Environment => _environment;

    /// <summary>
    /// Gets the current simulation status.
    /// </summary>
    public SimulationStatus Status => _status;

    /// <summary>
    /// Gets the time when the state was last saved.
    /// </summary>
    public DateTime LastSaveTime => _lastSaveTime;

    /// <summary>
    /// Gets the total number of simulation ticks processed.
    /// </summary>
    public long TotalTicks => _totalTicks;

    /// <summary>
    /// Gets the total number of living bees across all hives.
    /// </summary>
    public int TotalLivingBees => _beehives.Sum(h => h.TotalPopulation);

    /// <summary>
    /// Gets the number of viable hives.
    /// </summary>
    public int ViableHives => _beehives.Count(h => h.IsViable);

    /// <summary>
    /// Gets a value indicating whether any hives are still viable.
    /// </summary>
    public bool HasViableHives => ViableHives > 0;

    /// <summary>
    /// Adds a beehive to the simulation.
    /// </summary>
    /// <param name="beehive">The beehive to add.</param>
    public void AddBeehive(Beehive beehive)
    {
      ArgumentNullException.ThrowIfNull(beehive);

      if (!_beehives.Contains(beehive)) _beehives.Add(beehive);
    }

    /// <summary>
    /// Removes a beehive from the simulation.
    /// </summary>
    /// <param name="beehive">The beehive to remove.</param>
    /// <returns>True if the beehive was removed; otherwise, false.</returns>
    public bool RemoveBeehive(Beehive beehive)
    {
      ArgumentNullException.ThrowIfNull(beehive);
      return _beehives.Remove(beehive);
    }

    /// <summary>
    /// Updates the environment state.
    /// </summary>
    /// <param name="newEnvironment">The new environment state.</param>
    public void UpdateEnvironment(Entities.Environment newEnvironment) =>
      _environment = newEnvironment ?? throw new ArgumentNullException(nameof(newEnvironment));

    /// <summary>
    /// Sets the simulation status.
    /// </summary>
    /// <param name="status">The new status.</param>
    public void SetStatus(SimulationStatus status) => _status = status;

    /// <summary>
    /// Increments the tick counter.
    /// </summary>
    public void IncrementTicks() => _totalTicks++;

    /// <summary>
    /// Updates the last save time to the current time.
    /// </summary>
    public void UpdateLastSaveTime() => _lastSaveTime = DateTime.UtcNow;

    /// <summary>
    /// Gets comprehensive statistics about the simulation state.
    /// </summary>
    /// <returns>Simulation statistics.</returns>
    public SimulationStats GetStats()
    {
      List<BeehiveStats> hiveStats = [.. _beehives.Select(h => h.GetStats())];

      return new()
      {
        TotalTicks = _totalTicks,
        Status = _status,
        TotalHives = _beehives.Count,
        ViableHives = ViableHives,
        TotalLivingBees = TotalLivingBees,
        TotalWorkers = _beehives.Sum(h => h.WorkerPopulation),
        TotalDrones = _beehives.Sum(h => h.DronePopulation),
        TotalQueens = _beehives.Count(h => h.HasQueen),
        TotalHoneyProduced = _beehives.Sum(h => h.TotalHoneyProduced),
        TotalBeesProduced = _beehives.Sum(h => h.TotalBeesProduced),
        LastSaveTime = _lastSaveTime,
        CurrentEnvironment = new()
        {
          Temperature = _environment.Temperature.Celsius,
          Humidity = _environment.Humidity.Percentage,
          Weather = _environment.Weather,
          Season = _environment.Season,
          WindSpeed = _environment.WindSpeed,
          IsFavorableForForaging = _environment.IsFavorableForForaging
        },
        HiveStatistics = hiveStats
      };
    }
  }

  /// <summary>
  /// Represents the status of the simulation.
  /// </summary>
  public enum SimulationStatus
  {
    /// <summary>
    /// Simulation is stopped.
    /// </summary>
    Stopped = 0,

    /// <summary>
    /// Simulation is running normally.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Simulation is paused.
    /// </summary>
    Paused = 2,

    /// <summary>
    /// Simulation has completed (all hives collapsed).
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Simulation encountered an error.
    /// </summary>
    Error = 4
  }

  /// <summary>
  /// Statistics for the current simulation state.
  /// </summary>
  public sealed class SimulationStats
  {
    public long TotalTicks { get; set; }
    public SimulationStatus Status { get; set; }
    public int TotalHives { get; set; }
    public int ViableHives { get; set; }
    public int TotalLivingBees { get; set; }
    public int TotalWorkers { get; set; }
    public int TotalDrones { get; set; }
    public int TotalQueens { get; set; }
    public double TotalHoneyProduced { get; set; }
    public int TotalBeesProduced { get; set; }
    public DateTime LastSaveTime { get; set; }
    public EnvironmentSnapshot CurrentEnvironment { get; set; } = new();
    public List<BeehiveStats> HiveStatistics { get; set; } = [];
  }

  /// <summary>
  /// Snapshot of environment conditions.
  /// </summary>
  public sealed class EnvironmentSnapshot
  {
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public WeatherType Weather { get; set; }
    public Season Season { get; set; }
    public double WindSpeed { get; set; }
    public bool IsFavorableForForaging { get; set; }
  }
}
