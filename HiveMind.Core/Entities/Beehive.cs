using HiveMind.Core.Common;
using HiveMind.Core.Enums;
using HiveMind.Core.ValueObject;

namespace HiveMind.Core.Entities
{
  /// <summary>
  /// Represents the complete beehive structure containing the colony and all its components.
  /// The beehive manages the population, chambers, honey supers, and overall colony health.
  /// </summary>
  public sealed class Beehive : Entity
  {
    private readonly List<Bee> _bees;
    private readonly List<Chamber> _broodChambers;
    private readonly List<HoneySuper> _honeySupers;
    private Queen? _queen;
    private Position3D _location;
    private DateTime _foundedDate;
    private double _totalHoneyProduced;
    private int _totalBeesProduced;

    /// <summary>
    /// Gets the location of the beehive in 3D space.
    /// </summary>
    public Position3D Location => _location;

    /// <summary>
    /// Gets the date when the hive was established.
    /// </summary>
    public DateTime FoundedDate => _foundedDate;

    /// <summary>
    /// Gets the current queen of the hive, if present.
    /// </summary>
    public Queen? Queen => _queen;

    /// <summary>
    /// Gets a value indicating whether the hive has a queen.
    /// </summary>
    public bool HasQueen => _queen != null && _queen.IsAlive;

    /// <summary>
    /// Gets all bees currently in the hive.
    /// </summary>
    public IReadOnlyList<Bee> Bees => _bees.AsReadOnly();

    /// <summary>
    /// Gets all living bees in the hive.
    /// </summary>
    public IEnumerable<Bee> LivingBees => _bees.Where(b => b.IsAlive);

    /// <summary>
    /// Gets all worker bees in the hive.
    /// </summary>
    public IEnumerable<Worker> Workers => _bees.OfType<Worker>();

    /// <summary>
    /// Gets all drone bees in the hive.
    /// </summary>
    public IEnumerable<Drone> Drones => _bees.OfType<Drone>();

    /// <summary>
    /// Gets the brood chambers of the hive.
    /// </summary>
    public IReadOnlyList<Chamber> BroodChambers => _broodChambers.AsReadOnly();

    /// <summary>
    /// Gets the honey supers attached to the hive.
    /// </summary>
    public IReadOnlyList<HoneySuper> HoneySupers => _honeySupers.AsReadOnly();

    /// <summary>
    /// Gets the total population count including all living bees.
    /// </summary>
    public int TotalPopulation => LivingBees.Count();

    /// <summary>
    /// Gets the worker bee population count.
    /// </summary>
    public int WorkerPopulation => Workers.Count(w => w.IsAlive);

    /// <summary>
    /// Gets the drone bee population count.
    /// </summary>
    public int DronePopulation => Drones.Count(d => d.IsAlive);

    /// <summary>
    /// Gets the total amount of honey ever produced by this hive.
    /// </summary>
    public double TotalHoneyProduced => _totalHoneyProduced;

    /// <summary>
    /// Gets the total number of bees ever produced by this hive.
    /// </summary>
    public int TotalBeesProduced => _totalBeesProduced;

    /// <summary>
    /// Gets a value indicating whether the colony is viable.
    /// A colony needs a queen and sufficient workers to survive.
    /// </summary>
    public bool IsViable => HasQueen && WorkerPopulation >= MinViableWorkerCount;

    /// <summary>
    /// Gets the current health status of the colony.
    /// </summary>
    public ColonyHealth HealthStatus => DetermineHealthStatus();

    /// <summary>
    /// Minimum worker count for colony viability.
    /// </summary>
    public const int MinViableWorkerCount = 50;

    /// <summary>
    /// Maximum sustainable population for this hive configuration.
    /// </summary>
    public const int MaxSustainablePopulation = 80000;

    /// <summary>
    /// Initializes a new instance of the <see cref="Beehive"/> class.
    /// </summary>
    /// <param name="location">The location where the hive is situated.</param>
    public Beehive(Position3D location)
    {
      _location = location ?? throw new ArgumentNullException(nameof(location));
      _foundedDate = DateTime.UtcNow;
      _bees = [];
      _broodChambers = [];
      _honeySupers = [];
      _totalHoneyProduced = 0;
      _totalBeesProduced = 0;

      InitializeDefaultStructure();
    }

    /// <summary>
    /// Initializes the hive with basic chamber structure.
    /// </summary>
    private void InitializeDefaultStructure()
    {
      // Create initial brood chambers
      for (int i = 0; i < 8; i++)
      {
        Position3D chamberPosition = _location.Move(0, i * 10.0, 0); // Stacked vertically
        _broodChambers.Add(new Chamber(chamberPosition));
      }
    }

    /// <summary>
    /// Introduces a queen to the hive.
    /// </summary>
    /// <param name="queen">The queen to introduce.</param>
    /// <exception cref="InvalidOperationException">Thrown when hive already has a queen.</exception>
    public void IntroduceQueen(Queen queen)
    {
      ArgumentNullException.ThrowIfNull(queen);

      if (HasQueen)
        throw new InvalidOperationException("Hive already has a queen.");

      _queen = queen;
      AddBee(queen);
    }

    /// <summary>
    /// Adds a bee to the hive population.
    /// </summary>
    /// <param name="bee">The bee to add.</param>
    public void AddBee(Bee bee)
    {
      ArgumentNullException.ThrowIfNull(bee);

      if (!_bees.Contains(bee))
      {
        _bees.Add(bee);
        _totalBeesProduced++;
      }
    }

    /// <summary>
    /// Removes dead bees from the hive.
    /// </summary>
    /// <returns>Number of dead bees removed.</returns>
    public int RemoveDeadBees()
    {
      List<Bee> deadBees = [.. _bees.Where(b => !b.IsAlive)];

      foreach (Bee deadBee in deadBees)
        _bees.Remove(deadBee);

      // Remove dead queen reference
      if (_queen != null && !_queen.IsAlive)
        _queen = null;

      return deadBees.Count;
    }

    /// <summary>
    /// Adds a honey super to the hive.
    /// </summary>
    /// <param name="honeySuper">The honey super to add.</param>
    public void AddHoneySuper(HoneySuper honeySuper)
    {
      ArgumentNullException.ThrowIfNull(honeySuper);

      if (!_honeySupers.Contains(honeySuper))
      {
        _honeySupers.Add(honeySuper);
        honeySuper.Install();
      }
    }

    /// <summary>
    /// Removes a honey super from the hive.
    /// </summary>
    /// <param name="honeySuper">The honey super to remove.</param>
    /// <returns>True if the super was removed; false if not found.</returns>
    public bool RemoveHoneySuper(HoneySuper honeySuper)
    {
      ArgumentNullException.ThrowIfNull(honeySuper);

      if (_honeySupers.Remove(honeySuper))
      {
        honeySuper.Remove();
        return true;
      }

      return false;
    }

    /// <summary>
    /// Processes egg laying by the queen if conditions are suitable.
    /// </summary>
    /// <param name="environment">Current environmental conditions.</param>
    /// <returns>Number of eggs laid.</returns>
    public int ProcessEggLaying(Environment environment)
    {
      if (!HasQueen || !_queen!.IsLayingEggs) return 0;

      int availableCells = GetAvailableBroodCells();
      if (availableCells == 0) return 0;

      Season currentSeason = DetermineSeasonFromEnvironment(environment);
      int eggsLaid = _queen.LayEggs(availableCells, currentSeason);

      // Add eggs to available cells
      int cellsToFill = Math.Min(eggsLaid, availableCells);
      int filledCells = 0;

      foreach (Chamber chamber in _broodChambers)
      {
        if (filledCells >= cellsToFill) break;

        List<Cell> emptyCells = [.. chamber.Cells.Where(c => c.IsEmpty)];
        foreach (Cell cell in emptyCells)
        {
          if (filledCells >= cellsToFill) break;

          cell.AddEgg();
          filledCells++;
        }
      }

      return eggsLaid;
    }

    /// <summary>
    /// Updates all hive activities for a simulation tick.
    /// </summary>
    /// <param name="environment">Current environmental conditions.</param>
    public void UpdateHiveActivities(Environment environment)
    {
      ArgumentNullException.ThrowIfNull(environment);

      // Update brood development
      UpdateBroodDevelopment();

      // Process egg laying
      ProcessEggLaying(environment);

      // Update all bee activities
      foreach (Bee bee in LivingBees.ToList()) // ToList to avoid collection modification
        bee.PerformActivity(environment);

      // Remove dead bees
      RemoveDeadBees();

      // Process honey production (simplified)
      ProcessHoneyProduction(environment);

      // Check for swarming conditions
      CheckSwarmingConditions(environment);
    }

    /// <summary>
    /// Updates brood development in all chambers.
    /// </summary>
    private void UpdateBroodDevelopment()
    {
      foreach (Chamber chamber in _broodChambers)
      {
        chamber.UpdateBroodDevelopment();

        // Check for emerging bees
        ProcessEmergingBees(chamber);
      }
    }

    /// <summary>
    /// Processes bees emerging from cells after pupation.
    /// </summary>
    /// <param name="chamber">The chamber to check for emerging bees.</param>
    private void ProcessEmergingBees(Chamber chamber)
    {
      List<Cell> emergingCells =
        [.. chamber.Cells.Where(c => c.ContentsType == CellContentsType.Pupa && c.ContentAge.TotalDays >= 21)];

      foreach (var cell in emergingCells)
      {
        // Create new bee based on probability (90% workers, 9% drones, 1% potential queens)
        Random random = new();
        double roll = random.NextDouble();

        Bee newBee = roll switch
        {
          < 0.90 => new Worker(DateTime.UtcNow, chamber.Position),
          < 0.99 => new Drone(DateTime.UtcNow, chamber.Position),
          _ => new Queen(DateTime.UtcNow, chamber.Position) // Very rare in normal conditions
        };

        AddBee(newBee);
        cell.Empty(); // Cell is now available for reuse
      }
    }

    /// <summary>
    /// Simulates honey production by foraging workers.
    /// </summary>
    /// <param name="environment">Current environmental conditions.</param>
    private void ProcessHoneyProduction(Environment environment)
    {
      if (!environment.IsFavorableForForaging) return;

      int foragingWorkers = Workers.Count(w => w.IsAlive &&
                                               w.LifecycleStage == LifecycleStage.Adult &&
                                               w.CurrentActivity == WorkerActivity.Foraging);

      // Each foraging worker produces honey based on conditions
      double honeyPerForager = CalculateHoneyProductionRate(environment);
      double totalHoneyProduced = foragingWorkers * honeyPerForager;

      if (totalHoneyProduced > 0)
      {
        StoreHoney(totalHoneyProduced);
        _totalHoneyProduced += totalHoneyProduced;
      }
    }

    /// <summary>
    /// Calculates honey production rate based on environmental conditions.
    /// </summary>
    /// <param name="environment">Current environmental conditions.</param>
    /// <returns>Honey production rate per forager.</returns>
    private double CalculateHoneyProductionRate(Environment environment)
    {
      double baseRate = 0.1; // Base honey units per forager per tick

      double temperatureModifier = environment.Temperature.IsOptimalForForaging() ? 1.0 : 0.5;
      double weatherModifier = environment.Weather switch
      {
        WeatherType.Clear => 1.0,
        WeatherType.PartlyCloudy => 0.8,
        WeatherType.Overcast => 0.6,
        _ => 0.3
      };

      double seasonModifier = environment.Season switch
      {
        Season.Spring => 1.2,
        Season.Summer => 1.0,
        Season.Autumn => 0.6,
        Season.Winter => 0.1,
        _ => 0.8
      };

      return baseRate * temperatureModifier * weatherModifier * seasonModifier;
    }

    /// <summary>
    /// Stores honey in available chambers and honey supers.
    /// </summary>
    /// <param name="amount">Amount of honey to store.</param>
    /// <returns>Actual amount stored.</returns>
    private double StoreHoney(double amount)
    {
      double totalStored = 0.0;
      double remainingAmount = amount;

      // First try to store in honey supers
      foreach (var honeySuper in _honeySupers.Where(hs => hs.IsInstalled))
      {
        if (remainingAmount <= 0) break;

        double stored = honeySuper.StoreHoney(remainingAmount);
        totalStored += stored;
        remainingAmount -= stored;
      }

      // Then store in brood chambers (in honey cells)
      foreach (var chamber in _broodChambers)
      {
        if (remainingAmount <= 0) break;

        List<Cell> honeyCells = [.. chamber.GetHoneyStorageCells()];
        foreach (Cell cell in honeyCells)
        {
          if (remainingAmount <= 0) break;

          double cellCapacity = Cell.MaxHoneyPerCell - cell.HoneyAmount;
          double amountToStore = Math.Min(remainingAmount, cellCapacity);

          if (amountToStore > 0)
          {
            double actualStored = cell.AddHoney(amountToStore);
            totalStored += actualStored;
            remainingAmount -= actualStored;
          }
        }
      }

      return totalStored;
    }

    /// <summary>
    /// Checks if conditions warrant swarming behavior.
    /// </summary>
    /// <param name="environment">Current environmental conditions.</param>
    private void CheckSwarmingConditions(Environment environment)
    {
      // Swarming occurs when population is high and conditions are good
      bool populationPressure = TotalPopulation > (MaxSustainablePopulation * 0.7);
      bool seasonalConditions = environment.Season == Season.Spring || environment.Season == Season.Summer;
      bool environmentalConditions = environment.IsFavorableForForaging;

      if (populationPressure && seasonalConditions && environmentalConditions && HasQueen)
      {
        // In a full implementation, this would trigger swarm preparation
        // For now, we just log the condition
        double swarmProbability = CalculateSwarmProbability();

        if (new Random().NextDouble() < swarmProbability)
        {
          // Swarm event would occur here
          // This would involve creating a new hive and moving part of the population
        }
      }
    }

    /// <summary>
    /// Calculates the probability of swarming based on current conditions.
    /// </summary>
    /// <returns>Swarm probability (0.0 to 1.0).</returns>
    private double CalculateSwarmProbability()
    {
      double baseProbability = 0.01; // 1% base chance per tick

      double populationFactor = Math.Min(TotalPopulation / (double)MaxSustainablePopulation, 1.0);
      double ageFactor = _queen?.Age.TotalDays > 365 ? 1.5 : 1.0; // Older queens more likely to swarm

      return baseProbability * populationFactor * ageFactor;
    }

    /// <summary>
    /// Gets the number of available brood cells for egg laying.
    /// </summary>
    /// <returns>Number of empty cells suitable for eggs.</returns>
    private int GetAvailableBroodCells() => _broodChambers.Sum(chamber => chamber.EmptyCells);

    /// <summary>
    /// Determines the current season from environmental conditions.
    /// </summary>
    /// <param name="environment">The environment state.</param>
    /// <returns>The determined season.</returns>
    private Season DetermineSeasonFromEnvironment(Environment environment) =>
      environment.Season;

    /// <summary>
    /// Determines the overall health status of the colony.
    /// </summary>
    /// <returns>The colony health status.</returns>
    private ColonyHealth DetermineHealthStatus()
    {
      if (!HasQueen) return ColonyHealth.Critical;
      if (WorkerPopulation < MinViableWorkerCount) return ColonyHealth.Poor;
      if (TotalPopulation < MaxSustainablePopulation * 0.3) return ColonyHealth.Fair;
      if (TotalPopulation > MaxSustainablePopulation * 0.8) return ColonyHealth.Overcrowded;

      return ColonyHealth.Good;
    }

    /// <summary>
    /// Gets comprehensive statistics about the hive's current state.
    /// </summary>
    /// <returns>Detailed hive statistics.</returns>
    public BeehiveStats GetStats() => new()
    {
      TotalPopulation = TotalPopulation,
      WorkerPopulation = WorkerPopulation,
      DronePopulation = DronePopulation,
      HasQueen = HasQueen,
      HealthStatus = HealthStatus,
      TotalHoneyProduced = TotalHoneyProduced,
      TotalBeesProduced = TotalBeesProduced,
      BroodChamberCount = _broodChambers.Count,
      HoneySuperCount = _honeySupers.Count,
      FoundedDate = FoundedDate,
      HiveAge = DateTime.UtcNow - FoundedDate,
      IsViable = IsViable,
      CurrentHoneyStored = GetCurrentHoneyStored(),
      AvailableBroodCells = GetAvailableBroodCells()
    };

    /// <summary>
    /// Gets the total amount of honey currently stored in the hive.
    /// </summary>
    /// <returns>Total honey stored across all chambers and supers.</returns>
    private double GetCurrentHoneyStored()
    {
      double broodChamberHoney = _broodChambers.Sum(c => c.GetCurrentHoneyAmount());
      double honeySuperHoney = _honeySupers.Sum(hs => hs.CurrentHoneyAmount);
      return broodChamberHoney + honeySuperHoney;
    }
  }

  /// <summary>
  /// Represents the health status of a bee colony.
  /// </summary>
  public enum ColonyHealth
  {
    /// <summary>
    /// Colony is in critical condition and unlikely to survive.
    /// </summary>
    Critical = 1,

    /// <summary>
    /// Colony is in poor health with survival concerns.
    /// </summary>
    Poor = 2,

    /// <summary>
    /// Colony health is fair with some challenges.
    /// </summary>
    Fair = 3,

    /// <summary>
    /// Colony is in good health and thriving.
    /// </summary>
    Good = 4,

    /// <summary>
    /// Colony is overcrowded and may need management.
    /// </summary>
    Overcrowded = 5
  }

  /// <summary>
  /// Statistics for a beehive's current state.
  /// </summary>
  public sealed class BeehiveStats
  {
    /// <summary>
    /// Gets or sets the total population count.
    /// </summary>
    public int TotalPopulation { get; set; }

    /// <summary>
    /// Gets or sets the worker bee population count.
    /// </summary>
    public int WorkerPopulation { get; set; }

    /// <summary>
    /// Gets or sets the drone bee population count.
    /// </summary>
    public int DronePopulation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the hive has a queen.
    /// </summary>
    public bool HasQueen { get; set; }

    /// <summary>
    /// Gets or sets the colony health status.
    /// </summary>
    public ColonyHealth HealthStatus { get; set; }

    /// <summary>
    /// Gets or sets the total honey ever produced.
    /// </summary>
    public double TotalHoneyProduced { get; set; }

    /// <summary>
    /// Gets or sets the total bees ever produced.
    /// </summary>
    public int TotalBeesProduced { get; set; }

    /// <summary>
    /// Gets or sets the number of brood chambers.
    /// </summary>
    public int BroodChamberCount { get; set; }

    /// <summary>
    /// Gets or sets the number of honey supers.
    /// </summary>
    public int HoneySuperCount { get; set; }

    /// <summary>
    /// Gets or sets the date the hive was founded.
    /// </summary>
    public DateTime FoundedDate { get; set; }

    /// <summary>
    /// Gets or sets the age of the hive.
    /// </summary>
    public TimeSpan HiveAge { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the colony is viable.
    /// </summary>
    public bool IsViable { get; set; }

    /// <summary>
    /// Gets or sets the current amount of honey stored.
    /// </summary>
    public double CurrentHoneyStored { get; set; }

    /// <summary>
    /// Gets or sets the number of available brood cells.
    /// </summary>
    public int AvailableBroodCells { get; set; }
  }
}