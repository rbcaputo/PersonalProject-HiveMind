using HiveMind.Core.Entities;
using HiveMind.Core.Enums;
using Microsoft.Extensions.Logging;

namespace HiveMind.Core.AI
{
  /// <summary>
  /// Enhanced AI system for more sophisticated bee behavior patterns.
  /// Implements emergent behavior through complex rule interactions.
  /// </summary>
  public static class BeeAI
  {
    /// <summary>
    /// Determines the optimal activity for a worker bee using advanced AI rules.
    /// </summary>
    /// <param name="worker">The worker bee.</param>
    /// <param name="environment">Current environmental conditions.</param>
    /// <param name="hive">The bee's hive.</param>
    /// <param name="logger">Logger for AI decisions.</param>
    /// <returns>The optimal activity for the worker.</returns>
    public static WorkerActivity DetermineOptimalWorkerActivity(
      Worker worker,
      Entities.Environment environment,
      Beehive hive,
      ILogger logger
    )
    {
      ArgumentNullException.ThrowIfNull(worker);
      ArgumentNullException.ThrowIfNull(environment);
      ArgumentNullException.ThrowIfNull(hive);

      if (!worker.IsAlive || worker.LifecycleStage != LifecycleStage.Adult)
        return WorkerActivity.Resting;

      WorkerDecisionContext context = new()
      {
        Worker = worker,
        Environment = environment,
        Hive = hive,
        HiveStats = hive.GetStats()
      };

      // Apply decision tree with weighted factors
      Dictionary<WorkerActivity, double> activityScores = CalculateActivityScores(context);
      var optimalActivity = SelectOptimalActivity(activityScores, context);

      LogAIDecision(logger, worker, optimalActivity, context, activityScores);

      return optimalActivity;
    }

    /// <summary>
    /// Determines optimal queen behavior using advanced reproductive strategies.
    /// </summary>
    /// <param name="queen">The queen bee.</param>
    /// <param name="environment">Current environmental conditions.</param>
    /// <param name="hive">The queen's hive.</param>
    /// <param name="logger">Logger for AI decisions.</param>
    /// <returns>Optimal egg laying rate modifier.</returns>
    public static double DetermineQueenEggLayingStrategy(
      Queen queen,
      Entities.Environment environment,
      Beehive hive,
      ILogger logger
    )
    {
      ArgumentNullException.ThrowIfNull(queen);
      ArgumentNullException.ThrowIfNull(environment);
      ArgumentNullException.ThrowIfNull(hive);

      if (!queen.IsAlive || !queen.IsMated)
        return 0.0;

      var baseRate = environment.GetEffectiveEggLayingRate();
      BeehiveStats hiveStats = hive.GetStats();

      // Adaptive laying based on colony conditions
      object[] adaptiveModifiers = new[]
      {
        CalculatePopulationPressureModifier(hiveStats),
        CalculateResourceAvailabilityModifier(hiveStats),
        CalculateSeasonalStrategyModifier(environment),
        CalculateAgeBasedModifier(queen),
        CalculateEnvironmentalStressModifier(environment)
      };

      var finalRate = adaptiveModifiers.Aggregate(baseRate, (current, modifier) => current * modifier);

      logger.LogDebug(
        "Queen AI: Base rate {BaseRate:F2}, Final rate {FinalRate:F2}, Modifiers: [{Modifiers}]",
        baseRate,
        finalRate,
        string.Join(", ", adaptiveModifiers.Select(m => m.ToString("F2")))
      );

      return Math.Max(0.0, Math.Min(2.0, finalRate));
    }

    /// <summary>
    /// Determines drone mating behavior using sophisticated mate selection strategies.
    /// </summary>
    /// <param name="drone">The drone bee.</param>
    /// <param name="environment">Current environmental conditions.</param>
    /// <param name="hive">The drone's hive.</param>
    /// <returns>Mating flight probability (0.0 to 1.0).</returns>
    public static double DetermineDroneMatingStrategy(
      Drone drone,
      Entities.Environment environment,
      Beehive hive
    )
    {
      ArgumentNullException.ThrowIfNull(drone);
      ArgumentNullException.ThrowIfNull(environment);
      ArgumentNullException.ThrowIfNull(hive);

      if (!drone.IsAlive || drone.HasAttemptedMating || drone.Age.TotalDays < 24)
        return 0.0;

      double baseProbability = 0.05; // 5% base chance

      // Environmental factors
      var weatherModifier = CalculateWeatherMatingModifier(environment);
      var timeOfDayModifier = CalculateTimeOfDayMatingModifier();
      var seasonalModifier = CalculateSeasonalMatingModifier(environment);

      // Colony factors
      var competitionModifier = CalculateDroneCompetitionModifier(hive);
      var colonyHealthModifier = CalculateColonyHealthMatingModifier(hive);

      // Individual factors
      var ageModifier = CalculateDroneAgeModifier(drone);
      double energyModifier = Math.Max(0.1, drone.Energy);

      var finalProbability = baseProbability * weatherModifier * timeOfDayModifier *
                             seasonalModifier * competitionModifier * colonyHealthModifier *
                             ageModifier * energyModifier;

      return Math.Max(0.0, Math.Min(1.0, finalProbability));
    }

    /// <summary>
    /// Calculates weighted scores for all possible worker activities.
    /// </summary>
    private static Dictionary<WorkerActivity, double> CalculateActivityScores(WorkerDecisionContext context)
    {
      Dictionary<WorkerActivity, double> scores = [];

      // Age-based activity preferences
      Dictionary<WorkerActivity, double> ageFactors = CalculateAgeBasedActivityFactors(context.Worker);

      // Environmental influence
      Dictionary<WorkerActivity, double> environmentFactors = CalculateEnvironmentalActivityFactors(context);

      // Colony needs assessment
      Dictionary<WorkerActivity, double> colonyNeedsFactors = CalculateColonyNeedsActivityFactors(context);

      // Individual bee state
      Dictionary<WorkerActivity, double> individualFactors = CalculateIndividualActivityFactors(context.Worker);

      foreach (WorkerActivity activity in Enum.GetValues<WorkerActivity>())
      {
        double score = ageFactors.GetValueOrDefault(activity, 0.0) *
                       environmentFactors.GetValueOrDefault(activity, 1.0) *
                       colonyNeedsFactors.GetValueOrDefault(activity, 1.0) *
                       individualFactors.GetValueOrDefault(activity, 1.0);

        scores[activity] = Math.Max(0.0, score);
      }

      return scores;
    }

    /// <summary>
    /// Calculates age-based activity preference factors.
    /// </summary>
    private static Dictionary<WorkerActivity, double> CalculateAgeBasedActivityFactors(Worker worker)
    {
      double ageInDays = worker.Age.TotalDays;

      return new()
      {
        [WorkerActivity.HouseCleaning] = Math.Max(0, 2.0 - (ageInDays - 21) * 0.1),
        [WorkerActivity.NurseDuty] = Math.Max(0, 1.5 - Math.Abs(ageInDays - 27) * 0.1),
        [WorkerActivity.WaxProduction] = Math.Max(0, 1.3 - Math.Abs(ageInDays - 31) * 0.08),
        [WorkerActivity.FoodStorage] = Math.Max(0, 1.2 - Math.Abs(ageInDays - 35) * 0.07),
        [WorkerActivity.GuardDuty] = Math.Max(0, 1.1 - Math.Abs(ageInDays - 37) * 0.06),
        [WorkerActivity.Foraging] = ageInDays >= 39 ? 1.0 + (ageInDays - 39) * 0.02 : 0.1,
        [WorkerActivity.Resting] = worker.Energy < 0.3 ? 2.0 : 0.5
      };
    }

    /// <summary>
    /// Calculates environmental factors affecting activity preferences.
    /// </summary>
    private static Dictionary<WorkerActivity, double> CalculateEnvironmentalActivityFactors(WorkerDecisionContext context)
    {
      var foragingEfficiency = context.Environment.GetEffectiveForagingEfficiency();
      var isSuitableForActivity = context.Environment.IsSuitableForBeeActivity;

      return new()
      {
        [WorkerActivity.Foraging] = isSuitableForActivity ? foragingEfficiency : 0.1,
        [WorkerActivity.GuardDuty] = context.Environment.WeatherState.SeverityIndex > 0.5 ? 1.5 : 1.0,
        [WorkerActivity.HouseCleaning] = !isSuitableForActivity ? 1.3 : 1.0,
        [WorkerActivity.NurseDuty] = 1.0, // Not directly affected by weather
        [WorkerActivity.WaxProduction] = context.Environment.EffectiveTemperature.Celsius > 35 ? 0.7 : 1.0,
        [WorkerActivity.FoodStorage] = 1.0,
        [WorkerActivity.Resting] = context.Environment.WeatherState.SeverityIndex > 0.7 ? 1.5 : 1.0
      };
    }

    /// <summary>
    /// Calculates colony needs factors affecting activity priorities.
    /// </summary>
    private static Dictionary<WorkerActivity, double> CalculateColonyNeedsActivityFactors(WorkerDecisionContext context)
    {
      var stats = context.HiveStats;
      var populationDensity = (double)stats.TotalPopulation / (stats.BroodChamberCount * 100.0);

      return new()
      {
        [WorkerActivity.NurseDuty] = stats.AvailableBroodCells < 50 ? 0.5 : 1.5,
        [WorkerActivity.WaxProduction] = stats.AvailableBroodCells < 100 ? 2.0 : 0.8,
        [WorkerActivity.FoodStorage] = stats.CurrentHoneyStored < 20 ? 1.8 : 1.0,
        [WorkerActivity.GuardDuty] = populationDensity > 0.8 ? 1.5 : 1.0,
        [WorkerActivity.HouseCleaning] = populationDensity > 0.9 ? 1.8 : 1.0,
        [WorkerActivity.Foraging] = stats.CurrentHoneyStored < 50 ? 1.5 : 1.0,
        [WorkerActivity.Resting] = 1.0
      };
    }

    /// <summary>
    /// Calculates individual bee state factors.
    /// </summary>
    private static Dictionary<WorkerActivity, double> CalculateIndividualActivityFactors(Worker worker)
    {
      double energyLevel = worker.Energy;

      return new()
      {
        [WorkerActivity.Foraging] = Math.Max(0.1, energyLevel * 1.5),
        [WorkerActivity.GuardDuty] = Math.Max(0.3, energyLevel * 1.2),
        [WorkerActivity.WaxProduction] = Math.Max(0.2, energyLevel),
        [WorkerActivity.HouseCleaning] = Math.Max(0.5, energyLevel * 0.8),
        [WorkerActivity.NurseDuty] = Math.Max(0.4, energyLevel * 0.9),
        [WorkerActivity.FoodStorage] = Math.Max(0.6, energyLevel),
        [WorkerActivity.Resting] = energyLevel < 0.4 ? 3.0 : 0.3
      };
    }

    /// <summary>
    /// Selects the optimal activity based on calculated scores and context.
    /// </summary>
    private static WorkerActivity SelectOptimalActivity(
      Dictionary<WorkerActivity, double> scores,
      WorkerDecisionContext context
    )
    {
      // Add randomization to prevent deterministic behavior
      Random random = new();
      Dictionary<WorkerActivity, double> randomizedScores = scores.ToDictionary(
        kvp => kvp.Key,
        kvp => kvp.Value * (0.8 + random.NextDouble() * 0.4)
      ); // ±20% randomization

      // Select activity with highest score
      return randomizedScores.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    /// <summary>
    /// Logs AI decision for debugging and analysis.
    /// </summary>
    private static void LogAIDecision(
      ILogger logger,
      Worker worker,
      WorkerActivity selectedActivity,
      WorkerDecisionContext context,
      Dictionary<WorkerActivity, double> scores
    )
    {
      if (!logger.IsEnabled(LogLevel.Trace)) return;

      IEnumerable<string> topScores = scores
                                      .OrderByDescending(kvp => kvp.Value).Take(3)
                                      .Select(kvp => $"{kvp.Key}:{kvp.Value:F2}");

      logger.LogTrace(
        "Worker AI Decision - Bee: {BeeId}, Age: {Age:F1}d, Energy: {Energy:F2}, " +
        "Selected: {Activity}, Top Scores: [{Scores}]",
        worker.Id,
        worker.Age.TotalDays,
        worker.Energy,
        selectedActivity,
        string.Join(", ", topScores)
      );
    }

    // Queen AI helper methods
    private static double CalculatePopulationPressureModifier(BeehiveStats stats)
    {
      double density = stats.TotalPopulation / (stats.BroodChamberCount * 100.0);
      return density switch
      {
        < 0.3 => 1.5, // Low density - increase laying
        < 0.7 => 1.0, // Normal density
        < 0.9 => 0.7, // High density - reduce laying
        _ => 0.3      // Overcrowded - minimal laying
      };
    }

    private static double CalculateResourceAvailabilityModifier(BeehiveStats stats)
    {
      double honeyPerBee = stats.TotalPopulation > 0 ? stats.CurrentHoneyStored / stats.TotalPopulation : 0;
      return honeyPerBee switch
      {
        > 0.1 => 1.2,  // Abundant resources
        > 0.05 => 1.0, // Adequate resources
        > 0.02 => 0.7, // Limited resources
        _ => 0.3       // Scarce resources
      };
    }

    private static double CalculateSeasonalStrategyModifier(Entities.Environment environment) =>
      environment.CurrentSeason switch
      {
        Season.Spring => 1.4, // Expansion season
        Season.Summer => 1.1, // Peak season
        Season.Autumn => 0.6, // Preparation season
        Season.Winter => 0.2, // Survival season
        _ => 1.0
      };

    private static double CalculateAgeBasedModifier(Queen queen)
    {
      double ageInDays = queen.Age.TotalDays;
      return ageInDays switch
      {
        < 365 => 1.2,  // Young queen - high productivity
        < 730 => 1.0,  // Prime queen
        < 1095 => 0.8, // Aging queen
        _ => 0.5       // Old queen - declining productivity
      };
    }

    private static double CalculateEnvironmentalStressModifier(Entities.Environment environment)
    {
      var stressLevel = environment.WeatherState.SeverityIndex;
      return stressLevel switch
      {
        < 0.2 => 1.0, // No stress
        < 0.5 => 0.8, // Mild stress
        < 0.8 => 0.6, // Moderate stress
        _ => 0.3      // High stress
      };
    }

    // Drone AI helper methods
    private static double CalculateWeatherMatingModifier(Entities.Environment environment)
    {
      if (!environment.IsFavorableForForaging) return 0.1;

      return environment.WeatherState.WeatherType switch
      {
        WeatherType.Clear => 1.5,
        WeatherType.PartlyCloudy => 1.2,
        WeatherType.Overcast => 0.8,
        WeatherType.Windy => 0.5,
        _ => 0.2
      };
    }

    private static double CalculateTimeOfDayMatingModifier()
    {
      long hour = DateTime.Now.Hour;
      return hour switch
      {
        >= 10 and <= 16 => 1.5, // Prime mating hours
        >= 8 and < 10 => 1.2,   // Early morning
        > 16 and <= 18 => 1.2,  // Early evening
        _ => 0.3                // Poor mating hours
      };
    }

    private static double CalculateSeasonalMatingModifier(Entities.Environment environment) =>
      environment.CurrentSeason switch
      {
        Season.Spring => 1.8,
        Season.Summer => 1.5,
        Season.Autumn => 0.7,
        Season.Winter => 0.1,
        _ => 1.0
      };

    private static double CalculateDroneCompetitionModifier(Beehive hive)
    {
      int droneCount = hive.DronePopulation;
      int workerCount = hive.WorkerPopulation;

      if (workerCount == 0) return 0.5;

      double droneRatio = (double)droneCount / workerCount;
      return droneRatio switch
      {
        < 0.05 => 1.5, // Few drones - high success probability
        < 0.1 => 1.2,  // Normal drone population
        < 0.15 => 1.0, // High drone population
        _ => 0.7       // Too many drones - high competition
      };
    }

    private static double CalculateColonyHealthMatingModifier(Beehive hive) =>
      hive.HealthStatus switch
      {
        ColonyHealth.Good => 1.3,
        ColonyHealth.Fair => 1.0,
        ColonyHealth.Poor => 0.7,
        ColonyHealth.Critical => 0.3,
        ColonyHealth.Overcrowded => 0.8,
        _ => 1.0
      };

    private static double CalculateDroneAgeModifier(Drone drone)
    {
      double ageInDays = drone.Age.TotalDays;
      return ageInDays switch
      {
        < 30 => 0.7, // Too young
        < 45 => 1.3, // Prime age
        < 60 => 1.0, // Good age
        _ => 0.6     // Getting old
      };
    }
  }

  /// <summary>
  /// Context information for worker bee AI decisions.
  /// </summary>
  internal sealed class WorkerDecisionContext
  {
    public Worker Worker { get; set; } = null!;
    public Entities.Environment Environment { get; set; } = null!;
    public Beehive Hive { get; set; } = null!;
    public BeehiveStats HiveStats { get; set; } = null!;
  }
}
