using HiveMind.Core.Enums;
using HiveMind.Core.ValueObject;

namespace HiveMind.Core.Entities
{
  /// <summary>
  /// Represents a worker bee - the sterile female responsible for most colony activities.
  /// Worker bees perform different tasks based on their age and colony needs.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="Worker"/> class.
  /// </remarks>
  /// <param name="birthTime">The time when the worker was born.</param>
  /// <param name="position">The initial position of the worker.</param>
  public sealed class Worker(DateTime birthTime, Position3D position) : Bee(birthTime, position)
  {
    private WorkerActivity _currentActivity = WorkerActivity.Resting;
    private DateTime _lastActivityChange = birthTime;

    /// <summary>
    /// Gets the bee type (always Worker).
    /// </summary>
    public override BeeType BeeType => BeeType.Worker;

    /// <summary>
    /// Gets the maximum lifespan for a worker bee.
    /// Summer workers live ~6 weeks, winter workers live ~4-6 months.
    /// </summary>
    public override TimeSpan MaxLifespan => Season switch
    {
      Season.Summer => TimeSpan.FromDays(42),  // 6 weeks
      Season.Spring => TimeSpan.FromDays(56),  // 8 weeks
      Season.Autumn => TimeSpan.FromDays(120), // ~4 months (winter preparation)
      Season.Winter => TimeSpan.FromDays(180), // ~6 months
      _ => TimeSpan.FromDays(42)
    };

    /// <summary>
    /// Gets the current activity the worker is performing.
    /// </summary>
    public WorkerActivity CurrentActivity => _currentActivity;

    /// <summary>
    /// Gets the time when the worker last changed activities.
    /// </summary>
    public DateTime LastActivityChange => _lastActivityChange;

    /// <summary>
    /// Gets the current season (affects lifespan and behavior).
    /// In a real implementation, this would come from the environment.
    /// </summary>
    private Season Season => Season.Summer; // Placeholder - will be updated when Environment is available

    /// <summary>
    /// Gets the larval period duration for worker bees (6 days).
    /// </summary>
    /// <returns>6 days for worker bees.</returns>
    protected override double GetLarvalPeriodDays() => 9.0; // 3 days egg + 6 days larva

    /// <summary>
    /// Gets the pupal period duration for worker bees (12 days).
    /// </summary>
    /// <returns>12 days for worker bees.</returns>
    protected override double GetPupalPeriodDays() => 21.0; // 3 days egg + 6 days larva + 12 days pupa

    /// <summary>
    /// Changes the worker's current activity.
    /// </summary>
    /// <param name="newActivity">The new activity to perform.</param>
    public void ChangeActivity(WorkerActivity newActivity)
    {
      if (_currentActivity != newActivity)
      {
        _currentActivity = newActivity;
        _lastActivityChange = DateTime.UtcNow;
      }
    }

    /// <summary>
    /// Determines the appropriate activity based on the worker's age and environmental conditions.
    /// Worker bees follow an age-based progression of duties.
    /// </summary>
    /// <param name="environment">The current environment state.</param>
    /// <returns>The recommended activity for this worker.</returns>
    public WorkerActivity DetermineOptimalActivity(Environment environment)
    {
      if (!IsAlive || LifecycleStage != LifecycleStage.Adult)
        return WorkerActivity.Resting;

      double ageInDays = Age.TotalDays;

      // Age-based activity progression (realistic bee behavior)
      return ageInDays switch
      {
        < 25 when ageInDays >= 21 => WorkerActivity.HouseCleaning, // First 4 days as adult
        < 29 => WorkerActivity.NurseDuty,                          // Days 4-8: nurse bees
        < 33 => WorkerActivity.WaxProduction,                      // Days 8-12: builders
        < 37 => WorkerActivity.FoodStorage,                        // Days 12-16: food processors
        < 39 => WorkerActivity.GuardDuty,                          // Days 16-18: guards
        _ => DetermineForagingOrRest(environment)                  // Days 18+: foragers (weather dependent)
      };
    }

    /// <summary>
    /// Determines whether to forage or rest based on environmental conditions.
    /// </summary>
    /// <param name="environment">The current environment state.</param>
    /// <returns>Foraging if conditions are suitable, otherwise Resting.</returns>
    private WorkerActivity DetermineForagingOrRest(Environment environment)
    {
      ArgumentNullException.ThrowIfNull(environment);

      // Check if environmental conditions are suitable for foraging
      bool canForage = environment.Temperature.IsSuitableForBeeActivity() &&
                       environment.Weather != WeatherType.HeavyRain &&
                       environment.Weather != WeatherType.Storm &&
                       environment.Weather != WeatherType.Snow;

      return canForage ? WorkerActivity.Foraging : WorkerActivity.Resting;
    }

    /// <summary>
    /// Performs the worker's current activity during a simulation tick.
    /// </summary>
    /// <param name="environment">The current environment state.</param>
    public override void PerformActivity(Environment environment)
    {
      ArgumentNullException.ThrowIfNull(environment);

      if (!IsAlive || LifecycleStage != LifecycleStage.Adult)
      {
        ChangeActivity(WorkerActivity.Resting);
        return;
      }

      // Update lifecycle stage first
      UpdateLifecycleStage();
      if (!IsAlive) return;

      // Determine if activity should change
      WorkerActivity optimalActivity = DetermineOptimalActivity(environment);
      if (_currentActivity != optimalActivity)
        ChangeActivity(optimalActivity);

      // Perform activity-specific actions
      PerformCurrentActivity(environment);
    }

    /// <summary>
    /// Executes the actions associated with the current activity.
    /// </summary>
    /// <param name="environment">The current environment state.</param>
    private void PerformCurrentActivity(Environment environment)
    {
      double energyConsumption = _currentActivity switch
      {
        WorkerActivity.HouseCleaning => 0.02,
        WorkerActivity.NurseDuty => 0.03,
        WorkerActivity.WaxProduction => 0.04,
        WorkerActivity.FoodStorage => 0.02,
        WorkerActivity.GuardDuty => 0.01,
        WorkerActivity.Foraging => 0.05,
        WorkerActivity.Resting => -0.01, // Resting restores energy
        _ => 0.01
      };

      if (energyConsumption > 0)
        ConsumeEnergy(energyConsumption);
      else
        RestoreEnergy(Math.Abs(energyConsumption));
    }
  }
}
