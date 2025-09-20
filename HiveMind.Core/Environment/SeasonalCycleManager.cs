using HiveMind.Core.Enums;
using Microsoft.Extensions.Logging;

namespace HiveMind.Core.Environment
{
  /// <summary>
  /// Manages seasonal transitions and their effects on the environment and bee behavior.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="SeasonalCycleManager"/> class.
  /// </remarks>
  /// <param name="logger">Logger instance.</param>
  /// <param name="initialSeason">Initial season.</param>
  /// <param name="startDate">Season start date.</param>
  public sealed class SeasonalCycleManager(
    ILogger<SeasonalCycleManager> logger,
    Season initialSeason = Season.Spring,
    DateTime? startDate = null
  )
  {
    private readonly ILogger<SeasonalCycleManager> _logger =
      logger ?? throw new ArgumentNullException(nameof(logger));
    private Season _currentSeason = initialSeason;
    private DateTime _seasonStartDate = startDate ?? DateTime.UtcNow;
    private readonly Dictionary<Season, SeasonalCharacteristics> _seasonalData = InitializeSeasonalData();

    /// <summary>
    /// Gets the current season.
    /// </summary>
    public Season CurrentSeason => _currentSeason;

    /// <summary>
    /// Gets the date when the current season started.
    /// </summary>
    public DateTime SeasonStartDate => _seasonStartDate;

    /// <summary>
    /// Gets the characteristics of the current season.
    /// </summary>
    public SeasonalCharacteristics CurrentSeasonalCharacteristics =>
      _seasonalData[_currentSeason];

    /// <summary>
    /// Gets the progress through the current season (0.0 to 1.0).
    /// </summary>
    /// <param name="currentTime">Current simulation time.</param>
    /// <returns>Season progress from 0.0 (start) to 1.0 (end).</returns>
    public double GetSeasonProgress(DateTime currentTime)
    {
      TimeSpan elapsed = currentTime - _seasonStartDate;
      double seasonLength = CurrentSeasonalCharacteristics.DurationDays;
      return Math.Min(1.0, elapsed.TotalDays / seasonLength);
    }

    /// <summary>
    /// Updates the seasonal cycle and checks for season transitions.
    /// </summary>
    /// <param name="currentTime">Current simulation time.</param>
    /// <returns>True if season changed; otherwise, false.</returns>
    public bool UpdateSeason(DateTime currentTime)
    {
      TimeSpan elapsed = currentTime - _seasonStartDate;
      SeasonalCharacteristics currentCharacteristics = CurrentSeasonalCharacteristics;

      if (elapsed.TotalDays >= currentCharacteristics.DurationDays)
      {
        Season newSeason = GetNextSeason(_currentSeason);
        TransitionToSeason(newSeason, currentTime);
        return true;
      }

      return false;
    }

    /// <summary>
    /// Gets environmental modifiers based on current season and progress.
    /// </summary>
    /// <param name="currentTime">Current simulation time.</param>
    /// <returns>Environmental modifiers for the current season.</returns>
    public EnvironmentalModifiers GetEnvironmentalModifiers(DateTime currentTime)
    {
      double progress = GetSeasonProgress(currentTime);
      SeasonalCharacteristics characteristics = CurrentSeasonalCharacteristics;

      return new()
      {
        TemperatureModifier = CalculateTemperatureModifier(progress, characteristics),
        ForagingEfficiencyModifier = CalculateForagingModifier(progress, characteristics),
        EggLayingRateModifier = CalculateEggLayingModifier(progress, characteristics),
        EnergyConsumptionModifier = CalculateEnergyModifier(progress, characteristics),
        HoneyProductionModifier = CalculateHoneyProductionModifier(progress, characteristics),
        BroodDevelopmentModifier = CalculateBroodDevelopmentModifier(progress, characteristics)
      };
    }

    /// <summary>
    /// Transitions to a new season.
    /// </summary>
    /// <param name="newSeason">The new season to transition to.</param>
    /// <param name="transitionTime">The time of transition.</param>
    private void TransitionToSeason(Season newSeason, DateTime transitionTime)
    {
      Season previousSeason = _currentSeason;
      _currentSeason = newSeason;
      _seasonStartDate = transitionTime;

      _logger.LogInformation(
        "Season changed from {OldSeason} to {NewSeason} at {Time}",
        previousSeason,
        newSeason,
        transitionTime
      );
    }

    /// <summary>
    /// Gets the next season in the cycle.
    /// </summary>
    /// <param name="currentSeason">Current season.</param>
    /// <returns>Next season.</returns>
    private static Season GetNextSeason(Season currentSeason) => currentSeason switch
    {
      Season.Spring => Season.Summer,
      Season.Summer => Season.Autumn,
      Season.Autumn => Season.Winter,
      Season.Winter => Season.Spring,
      _ => Season.Spring
    };

    /// <summary>
    /// Calculates temperature modifier based on season progress.
    /// </summary>
    /// <param name="progress">Season progress (0.0 to 1.0).</param>
    /// <param name="characteristics">Season characteristics.</param>
    /// <returns>Temperature modifier.</returns>
    private double CalculateTemperatureModifier(double progress, SeasonalCharacteristics characteristics) =>
      // Temperature changes throughout the season
      _currentSeason switch
      {
        Season.Spring => characteristics.BaseTemperatureRange.Min +
                         (characteristics.BaseTemperatureRange.Max - characteristics.BaseTemperatureRange.Min) *
                         progress,
        Season.Summer => characteristics.BaseTemperatureRange.Max -
                         (characteristics.BaseTemperatureRange.Max - characteristics.BaseTemperatureRange.Min) *
                         Math.Abs(0.5 - progress) * 0.3, // Peak in middle
        Season.Autumn => characteristics.BaseTemperatureRange.Max -
                         (characteristics.BaseTemperatureRange.Max - characteristics.BaseTemperatureRange.Min) *
                         progress,
        Season.Winter => characteristics.BaseTemperatureRange.Min +
                         (characteristics.BaseTemperatureRange.Max - characteristics.BaseTemperatureRange.Min) *
                         Math.Abs(0.5 - progress) * 0.2, // Coldest in middle
        _ => 0
      };

    /// <summary>
    /// Calculates foraging efficiency modifier.
    /// </summary>
    /// <param name="progress">Season progress.</param>
    /// <param name="characteristics">Season characteristics.</param>
    /// <returns>Foraging efficiency modifier.</returns>
    private double CalculateForagingModifier(double progress, SeasonalCharacteristics characteristics)
    {
      double baseModifier = characteristics.ForagingEfficiency;

      // Adjust based on season progress
      return _currentSeason switch
      {
        Season.Spring => baseModifier * (0.6 + 0.4 * progress),                     // Improves through season
        Season.Summer => baseModifier * (0.9 + 0.1 * Math.Sin(progress * Math.PI)), // Peak in middle
        Season.Autumn => baseModifier * (1.0 - 0.5 * progress),                     // Declines through season
        Season.Winter => baseModifier * 0.5,                                        // Consistently low
        _ => baseModifier
      };
    }

    /// <summary>
    /// Calculates egg laying rate modifier.
    /// </summary>
    /// <param name="progress">Season progress.</param>
    /// <param name="characteristics">Season characteristics.</param>
    /// <returns>Egg laying rate modifier.</returns>
    private double CalculateEggLayingModifier(double progress, SeasonalCharacteristics characteristics) =>
      _currentSeason switch
      {
        Season.Spring => 0.5 + 1.5 * progress, // Rapid increase
        Season.Summer => 1.8 - 0.8 * progress, // Peak early, then decline
        Season.Autumn => 1.0 - 0.9 * progress, // Steady decline
        Season.Winter => 0.1,                  // Minimal egg laying
        _ => 1.0
      };

    /// <summary>
    /// Calculates energy consumption modifier.
    /// </summary>
    /// <param name="progress">Season progress.</param>
    /// <param name="characteristics">Season characteristics.</param>
    /// <returns>Energy consumption modifier.</returns>
    private double CalculateEnergyModifier(double progress, SeasonalCharacteristics characteristics) =>
      _currentSeason switch
      {
        Season.Spring => 1.2,                  // High energy for growth
        Season.Summer => 1.0,                  // Normal energy consumption
        Season.Autumn => 0.9 + 0.3 * progress, // Increasing to prepare for winter
        Season.Winter => 1.5,                  // High energy to maintain body heat
        _ => 1.0
      };

    /// <summary>
    /// Calculates honey production modifier.
    /// </summary>
    /// <param name="progress">Season progress.</param>
    /// <param name="characteristics">Season characteristics.</param>
    /// <returns>Honey production modifier.</returns>
    private double CalculateHoneyProductionModifier(double progress, SeasonalCharacteristics characteristics) =>
      _currentSeason switch
      {
        Season.Spring => 0.8 + 0.6 * progress, // Building up
        Season.Summer => 1.4 - 0.2 * progress, // Peak production early
        Season.Autumn => 0.8 - 0.6 * progress, // Declining
        Season.Winter => 0.1,                  // Minimal production
        _ => 1.0
      };

    /// <summary>
    /// Calculates brood development modifier.
    /// </summary>
    /// <param name="progress">Season progress.</param>
    /// <param name="characteristics">Season characteristics.</param>
    /// <returns>Brood development modifier.</returns>
    private double CalculateBroodDevelopmentModifier(double progress, SeasonalCharacteristics characteristics) =>
      _currentSeason switch
      {
        Season.Spring => 1.2, // Faster development
        Season.Summer => 1.0, // Normal development
        Season.Autumn => 0.9, // Slightly slower
        Season.Winter => 0.7, // Much slower development
        _ => 1.0
      };

    /// <summary>
    /// Initializes seasonal characteristics data.
    /// </summary>
    /// <returns>Dictionary of seasonal data.</returns>
    private static Dictionary<Season, SeasonalCharacteristics> InitializeSeasonalData() => new()
    {
      [Season.Spring] = new()
      {
        DurationDays = 90,
        BaseTemperatureRange = (8.0, 22.0),
        ForagingEfficiency = 0.8,
        Description = "Growth and expansion season with increasing activity"
      },
      [Season.Summer] = new()
      {
        DurationDays = 95,
        BaseTemperatureRange = (18.0, 35.0),
        ForagingEfficiency = 1.0,
        Description = "Peak activity season with maximum honey production"
      },
      [Season.Autumn] = new()
      {
        DurationDays = 85,
        BaseTemperatureRange = (5.0, 20.0),
        ForagingEfficiency = 0.6,
        Description = "Preparation season with declining activity and winter prep"
      },
      [Season.Winter] = new()
      {
        DurationDays = 95,
        BaseTemperatureRange = (-5.0, 8.0),
        ForagingEfficiency = 0.2,
        Description = "Survival season with minimal activity and cluster formation"
      }
    };
  }

  /// <summary>
  /// Characteristics defining a season's behavior and environmental conditions.
  /// </summary>
  public sealed class SeasonalCharacteristics
  {
    /// <summary>
    /// Gets or sets the duration of this season in days.
    /// </summary>
    public double DurationDays { get; set; }

    /// <summary>
    /// Gets or sets the temperature range for this season (min, max) in Celsius.
    /// </summary>
    public (double Min, double Max) BaseTemperatureRange { get; set; }

    /// <summary>
    /// Gets or sets the base foraging efficiency for this season (0.0 to 1.0).
    /// </summary>
    public double ForagingEfficiency { get; set; }

    /// <summary>
    /// Gets or sets a description of this season.
    /// </summary>
    public string Description { get; set; } = string.Empty;
  }

  /// <summary>
  /// Environmental modifiers that affect bee behavior during different seasons.
  /// </summary>
  public sealed class EnvironmentalModifiers
  {
    /// <summary>
    /// Gets or sets the temperature adjustment factor.
    /// </summary>
    public double TemperatureModifier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the foraging efficiency modifier (0.0 to 2.0).
    /// </summary>
    public double ForagingEfficiencyModifier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the egg laying rate modifier (0.0 to 2.0).
    /// </summary>
    public double EggLayingRateModifier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the energy consumption modifier (0.5 to 2.0).
    /// </summary>
    public double EnergyConsumptionModifier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the honey production modifier (0.0 to 2.0).
    /// </summary>
    public double HoneyProductionModifier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the brood development speed modifier (0.5 to 1.5).
    /// </summary>
    public double BroodDevelopmentModifier { get; set; } = 1.0;
  }
}
