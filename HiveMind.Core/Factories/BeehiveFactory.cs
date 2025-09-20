using HiveMind.Core.Entities;
using HiveMind.Core.Enums;
using HiveMind.Core.ValueObject;

namespace HiveMind.Core.Factories
{
  /// <summary>
  /// Factory for creating and initializing beehives with proper starting conditions.
  /// </summary>
  public static class BeehiveFactory
  {
    /// <summary>
    /// Creates a new beehive with a founding population.
    /// </summary>
    /// <param name="location">Location where the hive should be established.</param>
    /// <param name="startingPopulation">Initial population configuration.</param>
    /// <returns>A newly created and populated beehive.</returns>
    public static Beehive CreateNewHive(Position3D location, StartingPopulation? startingPopulation = null)
    {
      ArgumentNullException.ThrowIfNull(location);

      startingPopulation ??= StartingPopulation.Default;

      Beehive hive = new(location);

      // Add founding queen
      Queen queen = new(DateTime.UtcNow.AddDays(-30), location); // 30-day-old queen
      queen.Mate(); // Start with mated queen
      hive.IntroduceQueen(queen);

      // Add initial worker population
      for (int i = 0; i < startingPopulation.WorkerCount; i++)
      {
        TimeSpan workerAge = TimeSpan.FromDays(Random.Shared.NextDouble() * 40 + 10); // 10-50 days old
        Worker worker = new(DateTime.UtcNow - workerAge, location.Move(
          Random.Shared.NextDouble() * 20 - 10, // Random positions around hive
          Random.Shared.NextDouble() * 20 - 10,
          Random.Shared.NextDouble() * 10 - 5)
        );

        hive.AddBee(worker);
      }

      // Add initial drones (fewer than workers)
      for (int i = 0; i < startingPopulation.DroneCount; i++)
      {
        TimeSpan droneAge = TimeSpan.FromDays(Random.Shared.NextDouble() * 35 + 15); // 15-50 days old
        Drone drone = new(DateTime.UtcNow - droneAge, location.Move(
          Random.Shared.NextDouble() * 15 - 7.5,
          Random.Shared.NextDouble() * 15 - 7.5,
          Random.Shared.NextDouble() * 8 - 4)
        );

        hive.AddBee(drone);
      }

      // Add initial honey super if requested
      if (startingPopulation.IncludeHoneySuper)
      {
        Position3D honeySuperPosition = location.Move(0, 80, 0); // Above brood chambers
        HoneySuper honeySuper = new(honeySuperPosition);
        hive.AddHoneySuper(honeySuper);
      }

      return hive;
    }

    /// <summary>
    /// Creates a small nucleus colony (nuc) with minimal population.
    /// </summary>
    /// <param name="location">Location for the nucleus colony.</param>
    /// <returns>A small but viable beehive.</returns>
    public static Beehive CreateNucleusColony(Position3D location)
    {
      StartingPopulation smallPopulation = new()
      {
        WorkerCount = 3000, // Small but viable worker population
        DroneCount = 100,   // Few drones
        IncludeHoneySuper = false
      };

      return CreateNewHive(location, smallPopulation);
    }

    /// <summary>
    /// Creates a strong production hive with large population.
    /// </summary>
    /// <param name="location">Location for the production hive.</param>
    /// <returns>A large, productive beehive.</returns>
    public static Beehive CreateProductionHive(Position3D location)
    {
      StartingPopulation largePopulation = new()
      {
        WorkerCount = 40000, // Large worker population
        DroneCount = 2000,   // More drones for breeding
        IncludeHoneySuper = true
      };

      Beehive hive = CreateNewHive(location, largePopulation);

      // Add multiple honey supers for production
      for (int i = 1; i < 3; i++) // Additional supers
      {
        Position3D honeySuperPosition = location.Move(0, 80 + (i * 20), 0);
        HoneySuper honeySuper = new(honeySuperPosition);
        hive.AddHoneySuper(honeySuper);
      }

      return hive;
    }

    /// <summary>
    /// Creates an environment suitable for the specified season.
    /// </summary>
    /// <param name="season">The target season.</param>
    /// <returns>An environment configured for the season.</returns>
    public static Entities.Environment CreateSeasonalEnvironment(Season season) => season switch
    {
      Season.Spring => new(
        initialTemperature: new(18.0),
        initialHumidity: new(65.0),
        WeatherType.PartlyCloudy,
        Season.Spring
      ),
      Season.Summer => new(
        initialTemperature: new(28.0),
        initialHumidity: new(55.0),
        WeatherType.Clear,
        Season.Summer
      ),
      Season.Autumn => new(
        initialTemperature: new(15.0),
        initialHumidity: new(70.0),
        WeatherType.Overcast,
        Season.Autumn
      ),
      Season.Winter => new(
        initialTemperature: new(2.0),
        initialHumidity: new(80.0),
        WeatherType.Snow,
        Season.Winter
      ),
      _ => Entities.Environment.CreateDefault()
    };
  }

  /// <summary>
  /// Configuration for starting population of a new hive.
  /// </summary>
  public sealed class StartingPopulation
  {
    /// <summary>
    /// Gets or sets the initial worker bee count.
    /// </summary>
    public int WorkerCount { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the initial drone bee count.
    /// </summary>
    public int DroneCount { get; set; } = 500;

    /// <summary>
    /// Gets or sets a value indicating whether to include a honey super.
    /// </summary>
    public bool IncludeHoneySuper { get; set; } = true;

    /// <summary>
    /// Gets the default starting population configuration.
    /// </summary>
    public static StartingPopulation Default => new()
    {
      WorkerCount = 10000,
      DroneCount = 500,
      IncludeHoneySuper = true
    };

    /// <summary>
    /// Gets a small population configuration for testing or weak colonies.
    /// </summary>
    public static StartingPopulation Small => new()
    {
      WorkerCount = 3000,
      DroneCount = 150,
      IncludeHoneySuper = false
    };

    /// <summary>
    /// Gets a large population configuration for strong production colonies.
    /// </summary>
    public static StartingPopulation Large => new()
    {
      WorkerCount = 25000,
      DroneCount = 1500,
      IncludeHoneySuper = true
    };
  }
}
