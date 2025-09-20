using HiveMind.Core.Enums;
using HiveMind.Core.ValueObject;

namespace HiveMind.Core.Entities
{
  /// <summary>
  /// Represents a drone bee - the male bee whose primary purpose is reproduction.
  /// Drones have a different lifecycle and behavior pattern compared to workers.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="Drone"/> class.
  /// </remarks>
  /// <param name="birthTime">The time when the drone was born.</param>
  /// <param name="position">The initial position of the drone.</param>
  public sealed class Drone(DateTime birthTime, Position3D position) : Bee(birthTime, position)
  {
    private bool _hasAttemptedMating = false;
    private DateTime? _ejectionDate = null;

    /// <summary>
    /// Gets the bee type (always Drone).
    /// </summary>
    public override BeeType BeeType => BeeType.Drone;

    /// <summary>
    /// Gets the maximum lifespan for a drone bee.
    /// Drones typically live 8 weeks during mating season, but may be ejected earlier.
    /// </summary>
    public override TimeSpan MaxLifespan => TimeSpan.FromDays(56); // 8 weeks

    /// <summary>
    /// Gets a value indicating whether the drone has attempted mating.
    /// </summary>
    public bool HasAttemptedMating => _hasAttemptedMating;

    /// <summary>
    /// Gets the date when the drone was ejected from the colony (if applicable).
    /// Drones are often ejected before winter to conserve resources.
    /// </summary>
    public DateTime? EjectionDate => _ejectionDate;

    /// <summary>
    /// Gets a value indicating whether the drone has been ejected from the colony.
    /// </summary>
    public bool IsEjected => _ejectionDate.HasValue;

    /// <summary>
    /// Gets the larval period duration for drone bees (6.5 days).
    /// </summary>
    /// <returns>6.5 days for drone bees.</returns>
    protected override double GetLarvalPeriodDays() => 9.5; // 3 days egg + 6.5 days larva

    /// <summary>
    /// Gets the pupal period duration for drone bees (14.5 days).
    /// </summary>
    /// <returns>14.5 days for drone bees.</returns>
    protected override double GetPupalPeriodDays() => 24.0; // 3 days egg + 6.5 days larva + 14.5 days pupa

    /// <summary>
    /// Records that the drone has attempted mating.
    /// In nature, successful mating results in the drone's death.
    /// </summary>
    /// <param name="successful">Whether the mating attempt was successful.</param>
    public void AttemptMating(bool successful)
    {
      _hasAttemptedMating = true;

      if (successful)
        // Successful mating kills the drone in nature
        Die();
    }

    /// <summary>
    /// Ejects the drone from the colony.
    /// This typically happens before winter to conserve colony resources.
    /// </summary>
    public void Eject()
    {
      _ejectionDate = DateTime.UtcNow;
      Die(); // Ejected drones typically die
    }

    /// <summary>
    /// Determines if the drone should be ejected based on season and colony conditions.
    /// </summary>
    /// <param name="season">The current season.</param>
    /// <param name="colonyResourceLevel">The colony's resource level (0.0 to 1.0).</param>
    /// <returns>True if the drone should be ejected.</returns>
    public bool ShouldBeEjected(Season season, double colonyResourceLevel) =>
      // Drones are ejected in late autumn/winter or when resources are low
      season == Season.Winter ||
                (season == Season.Autumn && colonyResourceLevel < 0.3) ||
                colonyResourceLevel < 0.1;

    /// <summary>
    /// Performs the drone's activity during a simulation tick.
    /// Drones have limited activities compared to workers.
    /// </summary>
    /// <param name="environment">The current environment state.</param>
    public override void PerformActivity(Environment environment)
    {
      ArgumentNullException.ThrowIfNull(environment);

      if (!IsAlive || LifecycleStage != LifecycleStage.Adult)
        return;

      // Update lifecycle stage first
      UpdateLifecycleStage();
      if (!IsAlive) return;

      // Drones primarily rest and occasionally attempt to leave for mating flights
      if (CanAttemptMatingFlight(environment))
        AttemptMatingFlight(environment);
      else
        Rest();
    }

    /// <summary>
    /// Determines if conditions are suitable for a mating flight.
    /// </summary>
    /// <param name="environment">The current environment state.</param>
    /// <returns>True if mating flight conditions are suitable.</returns>
    private bool CanAttemptMatingFlight(Environment environment) =>
      // Mating flights occur during good weather and appropriate temperature
      environment.Temperature.IsOptimalForForaging() &&
      environment.Weather == WeatherType.Clear &&
      Age.TotalDays >= 24 && // Drones become sexually mature around 24 days
      !_hasAttemptedMating &&
      !IsEjected;

    /// <summary>
    /// Simulates a mating flight attempt.
    /// </summary>
    /// <param name="environment">The current environment state.</param>
    private void AttemptMatingFlight(Environment environment)
    {
      ConsumeEnergy(0.08); // Mating flights are energy-intensive

      // Simulate random mating success (very low probability in simulation)
      Random random = new();
      bool matingSuccess = random.NextDouble() < 0.01; // 1% chance of successful mating

      AttemptMating(matingSuccess);
    }

    /// <summary>
    /// Performs resting behavior, slowly restoring energy.
    /// </summary>
    private void Rest() => RestoreEnergy(0.005); // Slow energy restoration while resting
  }
}
