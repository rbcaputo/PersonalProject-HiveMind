using HiveMind.Core.Enums;
using HiveMind.Core.ValueObject;

namespace HiveMind.Core.Entities
{
  /// <summary>
  /// Represents the queen bee - the fertile female responsible for laying eggs and leading the colony.
  /// There is typically only one queen per colony, and her presence is vital for colony survival.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="Queen"/> class.
  /// </remarks>
  /// <param name="birthTime">The time when the queen was born.</param>
  /// <param name="position">The initial position of the queen.</param>
  public sealed class Queen(DateTime birthTime, Position3D position) : Bee(birthTime, position)
  {
    private int _totalEggsLaid = 0;
    private DateTime _lastEggLaid = birthTime;
    private bool _isMated = false;
    private DateTime? _matingDate = null;
    private double _eggLayingRate = 0;

    /// <summary>
    /// Gets the bee type (always Queen).
    /// </summary>
    public override BeeType BeeType => BeeType.Queen;

    /// <summary>
    /// Gets the maximum lifespan for a queen bee.
    /// Queens can live 2-5 years, with peak productivity in the first 2 years.
    /// </summary>
    public override TimeSpan MaxLifespan => TimeSpan.FromDays(1095); // 3 years average

    /// <summary>
    /// Gets the total number of eggs the queen has laid.
    /// </summary>
    public int TotalEggsLaid => _totalEggsLaid;

    /// <summary>
    /// Gets the time when the queen last laid an egg.
    /// </summary>
    public DateTime LastEggLaid => _lastEggLaid;

    /// <summary>
    /// Gets a value indicating whether the queen has been mated.
    /// </summary>
    public bool IsMated => _isMated;

    /// <summary>
    /// Gets the date when the queen was mated (if applicable).
    /// </summary>
    public DateTime? MatingDate => _matingDate;

    /// <summary>
    /// Gets the current egg-laying rate (eggs per day).
    /// This varies based on season, age, and colony conditions.
    /// </summary>
    public double EggLayingRate => _eggLayingRate;

    /// <summary>
    /// Gets a value indicating whether the queen is actively laying eggs.
    /// </summary>
    public bool IsLayingEggs => _isMated && IsAlive && LifecycleStage == LifecycleStage.Adult;

    /// <summary>
    /// Maximum eggs a queen can lay per day during peak season.
    /// </summary>
    public const int MaxEggsPerDay = 2000;

    /// <summary>
    /// Gets the larval period duration for queen bees (5.5 days).
    /// </summary>
    /// <returns>5.5 days for queen bees.</returns>
    protected override double GetLarvalPeriodDays() => 8.5; // 3 days egg + 5.5 days larva

    /// <summary>
    /// Gets the pupal period duration for queen bees (7.5 days).
    /// </summary>
    /// <returns>7.5 days for queen bees.</returns>
    protected override double GetPupalPeriodDays() => 16.0; // 3 days egg + 5.5 days larva + 7.5 days pupa

    /// <summary>
    /// Records the queen's mating event.
    /// Queens typically mate with multiple drones during nuptial flights.
    /// </summary>
    public void Mate()
    {
      if (_isMated) return; // Already mated

      _isMated = true;
      _matingDate = DateTime.UtcNow;

      // Mated queens begin laying eggs
      CalculateEggLayingRate(Season.Spring); // Default to spring initially
    }

    /// <summary>
    /// Attempts to lay eggs based on current conditions.
    /// </summary>
    /// <param name="availableCells">Number of available cells for egg laying.</param>
    /// <param name="season">Current season affecting laying rate.</param>
    /// <returns>Number of eggs actually laid.</returns>
    public int LayEggs(int availableCells, Season season)
    {
      if (!IsLayingEggs || availableCells <= 0) return 0;

      CalculateEggLayingRate(season);

      TimeSpan timeSinceLastEgg = DateTime.UtcNow - _lastEggLaid;
      int expectedEggs = (int)(timeSinceLastEgg.TotalDays * _eggLayingRate);

      int eggsToLay = Math.Min(expectedEggs, availableCells);
      eggsToLay = Math.Min(eggsToLay, GetMaxEggsPerInterval());

      if (eggsToLay > 0)
      {
        _totalEggsLaid += eggsToLay;
        _lastEggLaid = DateTime.UtcNow;
        ConsumeEnergy(eggsToLay * 0.001); // Small energy cost per egg
      }

      return eggsToLay;
    }

    /// <summary>
    /// Calculates the queen's egg-laying rate based on various factors.
    /// </summary>
    /// <param name="season">The current season.</param>
    private void CalculateEggLayingRate(Season season)
    {
      if (!_isMated)
      {
        _eggLayingRate = 0;
        return;
      }

      double baseRate = MaxEggsPerDay * 0.8; // 80% of maximum as base

      // Season modifier
      double seasonModifier = season switch
      {
        Season.Spring => 1.0,
        Season.Summer => 0.8,
        Season.Autumn => 0.3,
        Season.Winter => 0.1,
        _ => 0.5
      };

      // Age modifier (queens are most productive in their first year)
      double ageInDays = Age.TotalDays;
      double ageModifier = ageInDays switch
      {
        < 365 => 1.0,
        < 730 => 0.8,
        < 1095 => 0.6,
        _ => 0.3
      };

      // Energy modifier
      double energyModifier = Energy;

      _eggLayingRate = baseRate * seasonModifier * ageModifier * energyModifier;
    }

    /// <summary>
    /// Gets the maximum number of eggs the queen can lay in a single interval.
    /// </summary>
    /// <returns>Maximum eggs per interval.</returns>
    private int GetMaxEggsPerInterval() =>
      // Limit eggs per simulation tick to prevent unrealistic spikes
      (int)(_eggLayingRate / 24.0); // Assuming hourly ticks approximation

    /// <summary>
    /// Performs the queen's activity during a simulation tick.
    /// Queens primarily focus on laying eggs and maintaining the colony.
    /// </summary>
    /// <param name="environment">The current environment state.</param>
    public override void PerformActivity(Environment environment)
    {
      ArgumentNullException.ThrowIfNull(environment);

      if (!IsAlive)
        return;

      // Update lifecycle stage first
      UpdateLifecycleStage();
      if (!IsAlive) return;

      if (LifecycleStage != LifecycleStage.Adult)
        return;

      // Queens need to mate before they can be productive
      if (!_isMated && Age.TotalDays >= 16) // Queens can mate after ~16 days
        AttemptMating(environment);

      // Perform royal duties
      PerformRoyalDuties(environment);
    }

    /// <summary>
    /// Attempts mating based on environmental conditions.
    /// </summary>
    /// <param name="environment">Current environment state.</param>
    private void AttemptMating(Environment environment)
    {
      // Queens attempt mating during favorable weather
      if (environment.Temperature.IsOptimalForForaging() &&
          environment.Weather == WeatherType.Clear)
      {
        Random random = new();
        if (random.NextDouble() < 0.1) // 10% chance per tick during good weather
          Mate();
      }
    }

    /// <summary>
    /// Performs the queen's regular duties including laying eggs and pheromone production.
    /// </summary>
    /// <param name="environment">Current environment state.</param>
    private void PerformRoyalDuties(Environment environment)
    {
      if (!IsLayingEggs)
      {
        Rest();
        return;
      }

      // In a full implementation, this would interact with the hive to determine available cells
      // For now, we simulate the egg-laying process
      var simulatedAvailableCells = 100; // Placeholder
      Season currentSeason = DetermineSeasonFromEnvironment(environment);

      LayEggs(simulatedAvailableCells, currentSeason);

      // Queens consume energy maintaining the colony through pheromone production
      ConsumeEnergy(0.02);
    }

    /// <summary>
    /// Determines the current season from environmental conditions.
    /// This is a placeholder - in full implementation, this would come from the environment.
    /// </summary>
    /// <param name="environment">The environment state.</param>
    /// <returns>The determined season.</returns>
    private Season DetermineSeasonFromEnvironment(Environment environment) =>
      // Placeholder logic based on temperature
      environment.Temperature.Celsius switch
      {
        < 10 => Season.Winter,
        < 18 => Season.Spring,
        < 30 => Season.Summer,
        _ => Season.Autumn
      };

    /// <summary>
    /// Performs resting behavior for the queen.
    /// </summary>
    private void Rest() => RestoreEnergy(0.01); // Queens restore energy while resting
  }
}
