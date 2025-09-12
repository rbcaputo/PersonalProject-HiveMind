using HiveMind.Core.Domain.Behaviors;
using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;
using HiveMind.Core.Domain.ValueObjects;

namespace HiveMind.Core.Domain.Entities
{
  // ------------------------------------------------
  //  Represents an individual ant in the simulation
  // ------------------------------------------------

  public abstract class Ant : BaseEntity, IInsect
  {
    private double _health;
    private double _energy;
    private NutritionalStatus _nutritionalStatus = NutritionalStatus.Adequate;
    private DevelopmentStage _developmentStage;
    private readonly List<string> _experienceLog = [];

    protected Ant(
      AntCaste caste,
      Position startPosition,
      IColony colony,
      AntPhysiology? customPhysiology = null,
      long birthTick = 0
    )
    {
      Caste = caste;
      Position = startPosition;
      Colony = colony ?? throw new ArgumentNullException(nameof(colony));
      BirthTick = birthTick;

      //  Initialize physiology - can be overridden by subclasses
      Physiology = customPhysiology ?? CreateDefaultPhysiology();

      //  Initialize vitals based on physiology
      MaxHealth = MaxEnergy = CalculateMaxVitals();
      Health = MaxHealth;
      Energy = MaxEnergy;

      //  Start as egg - will develop over time
      _developmentStage = DevelopmentStage.Egg;
      CurrentState = ActivityState.Idle;

      UpdateTimestamp();
    }

    //  Core IInsect interface
    public InsectType Type =>
      InsectType.Ant;
    public AntCaste Caste { get; protected set; }
    public Position Position { get; protected set; }
    public ActivityState CurrentState { get; protected set; }
    public IColony Colony { get; }

    //  Biological properties
    public AntPhysiology Physiology { get; protected set; }
    public long BirthTick { get; }
    public long CurrentTick { get; private set; }

    //  Dynamic vitals
    public double Health
    {
      get =>
        _health;
      protected set =>
        _health = Math.Max(0, Math.Min(MaxHealth, value));
    }

    public double Energy
    {
      get =>
        _energy;
      protected set
      {
        _energy = Math.Max(0, Math.Min(MaxEnergy, value));
        UpdateNutritionalStatus();
      }
    }

    public double MaxHealth { get; protected set; }
    public double MaxEnergy { get; protected set; }
    public bool IsAlive => Health > 0 && DevelopmentStage != DevelopmentStage.Dead;

    //  Lifecyle and development
    public DevelopmentStage DevelopmentStage =>
      _developmentStage;
    public int AgeDays =>
      (int)((CurrentTick - BirthTick) / TicksPerDay);
    public NutritionalStatus NutritionalStatus =>
      _nutritionalStatus;
    public bool IsAdult =>
      _developmentStage >= DevelopmentStage.YoungAdult;
    public bool IsProductive =>
      _developmentStage >= DevelopmentStage.Adult && _developmentStage <= DevelopmentStage.Elder;

    //  Carrying and interaction
    public double CarriedFood { get; protected set; }
    public double MaxCarryingCapacity =>
      Physiology.MaxCarryCapacity * GetBodyWeightMultiplier();
    public double CurrentCarryingLoad =>
      CarriedFood;  //  Could include other items in future

    //  Experience and learning
    public IReadOnlyList<string> ExperienceLog =>
      _experienceLog.AsReadOnly();
    public double OverallExperience { get; protected set; } = 0.0;

    //  Abstract methods for subclass specialization
    protected abstract AntPhysiology CreateDefaultPhysiology();
    protected abstract double GetBodyWeightMultiplier();
    protected abstract void OnDevelopmentStageChanged(DevelopmentStage newStage);
    protected abstract IAntBehavior? GetBehavior();

    // --------------------
    //  Main update method
    // --------------------

    public virtual void Update(ISimulationContext context)
    {
      if (!IsAlive)
        return;

      CurrentTick = context.CurrentTick;

      //  Development progression
      UpdateDevelopmentStage();
      //  Metabolic processes
      ProcessMetabolism(context.DeltaTime);

      //  Only productive adults perform behaviors
      if (IsProductive && GetBehavior() != null)
      {
        GetBehavior()!.Update(this, context);
        ProcessMovement(context);
      }

      //  Health and aging effects
      ProcessAging();
      ProcessEnvironmentalEffects(context);

      UpdateTimestamp();
    }

    // ----------------------------------
    //  IInsect interface implementation
    // ----------------------------------

    public void MoveTo(Position newPosition)
    {
      if (!IsAlive || !IsAdult)
        return;

      //  Calculate movement cost based on load and distance
      double distance = Position.DistanceTo(newPosition);
      double movementCost = CalculateMovementEnergyCost(distance);

      if (Energy >= movementCost)
      {
        Position = newPosition;
        ConsumeEnergy(movementCost);
        CurrentState = ActivityState.Moving;

        LogExperience($"Moved {distance:F1} units");
      }
    }

    public void ConsumeEnergy(double amount)
    {
      if (!IsAlive || amount < 0)
        return;

      Energy -= amount;

      //  Track starvation effects
      if (Energy <= 0)
        ProcessStarvation();
    }

    public void RestoreEnergy(double amount)
    {
      if (!IsAlive || amount < 0)
        return;

      double previousEnergy = Energy;
      Energy += amount;

      //  Log significant energy restoration
      if (amount > MaxEnergy * 0.1)
        LogExperience($"Restored {amount:F1} energy");
    }

    //  Biological methods
    public void CollectFood(double amount)
    {
      if (!IsAlive || !IsAdult || amount <= 0)
        return;

      double availableCapacity = MaxCarryingCapacity - CurrentCarryingLoad;
      double actualAmount = Math.Min(amount, availableCapacity);

      if (actualAmount > 0)
      {
        CarriedFood += actualAmount;
        LogExperience($"Collected {actualAmount:F1} food");
      }
    }

    public double DropFood()
    {
      if (!IsAlive || CarriedFood <= 0)
        return 0.0;

      double droppedAmount = CarriedFood;
      CarriedFood = 0;

      LogExperience($"Dropped {droppedAmount:F1} food");

      return droppedAmount;
    }

    public void SetState(ActivityState newState)
    {
      if (!IsAlive)
        return;

      ActivityState previousState = CurrentState;
      CurrentState = newState;

      //  Log significant state changes
      if (previousState != newState && IsProductive)
        LogExperience($"Changed from {previousState} to {newState}");
    }

    // --------------------------------
    //  Private implementation methods
    // --------------------------------

    protected virtual void UpdateDevelopmentStage()
    {
      DevelopmentStage newStage = CalculateDevelopmentStage();
      if (newStage != _developmentStage)
      {
        DevelopmentStage previousStage = _developmentStage;
        _developmentStage = newStage;

        OnDevelopmentStageChanged(newStage);
        RecalculateVitalsForStage();

        LogExperience($"Developed from {previousStage} to {newStage}");
      }
    }

    protected DevelopmentStage CalculateDevelopmentStage()
    {
      //  Temperature-dependent develpment  - simplified for now
      int adjustedAge = AgeDays;

      return adjustedAge switch
      {
        < 14 =>
          DevelopmentStage.Egg,
        < 35 =>
          DevelopmentStage.Larva,
        < 49 =>
          DevelopmentStage.Pupa,
        < 79 =>
          DevelopmentStage.YoungAdult,
        < (int)(Physiology.LongevityDays * 0.8) =>
          DevelopmentStage.Adult,
        < Physiology.LongevityDays =>
          DevelopmentStage.Elder,
        _ =>
          DevelopmentStage.Dead
      };
    }

    private void RecalculateVitalsForStage()
    {
      double stageMultiplier = _developmentStage switch
      {
        DevelopmentStage.Egg or DevelopmentStage.Larva =>
          0.3,
        DevelopmentStage.Pupa =>
          0.5,
        DevelopmentStage.YoungAdult =>
          0.8,
        DevelopmentStage.Adult =>
          1.0,
        DevelopmentStage.Elder =>
          0.9,
        _ =>
          1.0
      };

      double baseVitals = CalculateMaxVitals();
      MaxHealth = MaxEnergy = baseVitals * stageMultiplier;

      //  Ensure current values don't exceed new maximums
      if (Health > MaxHealth)
        Health = MaxHealth;
      if (Energy > MaxEnergy)
        Energy = MaxEnergy;
    }

    private void ProcessMetabolism(double deltaTime)
    {
      if (_developmentStage < DevelopmentStage.YoungAdult)
        return;

      //  Base metabolic cost
      double baseCost = Physiology.MetabolismRate * deltaTime;
      //  Activity modifier
      double activityMultiplier = Physiology.GetMetabolicCostMultiplier(CurrentState);
      //  Load modifier - carrying things costs more energy
      double loadMultiplier = 1.0 + (CurrentCarryingLoad / MaxCarryingCapacity) * 0.3;
      //  Environmental modifier (temperatura, etc.)
      double environmentalMultiplier = GetEnvironmentalMatabolismModifier();

      double totalCost = baseCost * activityMultiplier * loadMultiplier * environmentalMultiplier;
      ConsumeEnergy(totalCost);
    }

    private void ProcessStarvation()
    {
      //  Gradual health loss when energy is depleted
      double starvationDamage = MaxHealth * 0.01;  //  1% health loss per starvation tick

      Health -= starvationDamage;
      if (Health <= 0)
      {
        _developmentStage = DevelopmentStage.Dead;
        CurrentState = ActivityState.Dead;
        LogExperience("Died from starvation");
      }
    }

    private void ProcessAging()
    {
      //  Gradual aging effects
      if (AgeDays > Physiology.LongevityDays * 0.8)  //  After 80% of lifespan
      {
        double agingFactor = (double)(AgeDays - Physiology.LongevityDays * 0.8) / (Physiology.LongevityDays * 0.2);
        double agingDamage = 0.001 * agingFactor;  //  Very gradual health decline

        Health -= agingDamage;
      }

      //  Natural death from extreme old age
      if (AgeDays > Physiology.LongevityDays)
      {
        _developmentStage = DevelopmentStage.Dead;
        CurrentState = ActivityState.Dead;
        LogExperience("Died from old age");
      }
    }

    private void ProcessEnvironmentalEffects(ISimulationContext context)
    {
      //  Simplified environmental effects - could be enhanced with actual temperature/humidity
      //  This would integrate with the environment system for realistic effects
    }

    private void ProcessMovement(ISimulationContext context)
    {
      //  Handle any ongoing movement animations or position updates
      //  This would be enhanced with actual pathfinding and movement smoothing
    }

    private void UpdateNutritionalStatus()
    {
      double energyRatio = Energy / MaxEnergy;
      _nutritionalStatus = energyRatio switch
      {
        < 0.1 =>
          NutritionalStatus.Starving,
        < 0.3 =>
          NutritionalStatus.Hungry,
        < 0.7 =>
          NutritionalStatus.Adequate,
        < 0.95 =>
          NutritionalStatus.WellFed,
        _ =>
          NutritionalStatus.Overfed
      };
    }

    private double CalculateMovementEnergyCost(double distance)
    {
      double baseCost = distance * 0.1;  //  Base energy per distance unit
      double speedFactor = Physiology.MovementSpeed;  //  Faster ants use more energy
      double loadFactor = 1.0 + (CurrentCarryingLoad / MaxCarryingCapacity);  //  Load increases cost

      return baseCost * speedFactor * loadFactor;
    }

    private double GetEnvironmentalMetabolismModifier()
    {
      //  Simplified - would be enhanced with actual environmental data
      return 1.0;  //  TODO: Implement temperature/humidity effects
    }

    private void LogExperience(string experience)
    {
      if (_experienceLog.Count >= 100)  //  Limit experience log size
        _experienceLog.RemoveAt(0);

      _experienceLog.Add($"Day {AgeDays}: {experience}");
      OverallExperience += 0.1;  //  Small experience gain
    }

    protected virtual double CalculateMaxVitals() =>
      Caste switch
      {
        AntCaste.Queen =>
          200.0,  //  Queens are larger and more robust
        AntCaste.Soldier =>
          120.0,  //  Soldiers are tough
        AntCaste.Worker =>
          100.0,  //  Standard worker vitals
        AntCaste.Forager =>
          90.0,   //  Foragers trade vitals for speed
        _ =>
          80.0
      };

    // -----------
    //  Constants
    // -----------

    protected static readonly int TicksPerDay = 86400;  //  Assuming 1 tick = 1 second (could be configurable)

    //  Prevent direct instantiation - must be subclasses
    private static readonly DevelopmentStage Dead = DevelopmentStage.Egg;  //  Private field to prevent enum extension
  }

  // ---------------------------------------------------------------------------
  //  Specialized QUEEN ant with reproductive capabilities and enhanced biology
  // ---------------------------------------------------------------------------

  public class QueenAnt(Position startPosition, IColony colony, long birthTick = 0)
    : Ant(AntCaste.Queen, startPosition, colony, null, birthTick)
  {
    private int _eggsLaidToday = 0;
    private int _eggsLaidTotal = 0;
    private double _fertilityRate = 1.0;

    //  Queen-specific properties
    public int EggsLaidToday =>
      _eggsLaidToday;
    public int EggsLaidTotal =>
      _eggsLaidTotal;
    public double FertilityRate =>
      _fertilityRate;
    public bool CanLayEggs =>
      IsProductive && Energy > MaxEnergy * 0.4 && NutritionalStatus >= NutritionalStatus.Adequate;

    protected override AntPhysiology CreateDefaultPhysiology() =>
      AntPhysiology.CreateQueenPhysiology();
    protected override double GetBodyWeightMultiplier() =>
      3.0;  //  Queens are much larger
    protected override IAntBehavior? GetBehavior() =>
      AntBehaviorFactory.CreateBehavior(AntCaste.Queen);

    protected override void OnDevelopmentStageChanged(DevelopmentStage newStage)
    {
      if (newStage == DevelopmentStage.Adult)
        _fertilityRate = 1.0;  //  Reach peak fertility
      else if (newStage == DevelopmentStage.Elder)
        _fertilityRate *= 0.7;  //  Reduced fertility in old age
    }

    public int LayEggs(int maxEggs)
    {
      if (!CanLayEggs)
        return 0;

      int eggsToLay = Math.Min(maxEggs, GetMaxEggsForCurrentCondition());
      double energyCost = eggsToLay * 2.0;  //  Each egg costs energy

      if (Energy >= energyCost)
      {
        ConsumeEnergy(energyCost);

        _eggsLaidToday += eggsToLay;
        _eggsLaidTotal += eggsToLay;

        LogExperience($"Laid {eggsToLay} eggs");

        return eggsToLay;
      }

      return 0;
    }

    private int GetMaxEggsForCurrentCondition()
    {
      int baseEggs = 50;  //  Base egg laying capacity per day
      double multiplier = _fertilityRate;

      //  Nutritional status affects egg laying
      multiplier *= NutritionalStatus switch
      {
        NutritionalStatus.Starving =>
          0.0,
        NutritionalStatus.Hungry =>
          0.3,
        NutritionalStatus.Adequate =>
          1.0,
        NutritionalStatus.WellFed =>
          1.5,
        NutritionalStatus.Overfed =>
          1.2,  //  Slightly reduced due to sluggishness
        _ =>
          1.0
      };

      return Math.Max(0, (int)(baseEggs * multiplier));
    }

    public override void Update(ISimulationContext context)
    {
      //  Reset daily egg count at start of new day
      int currentDay = (int)(context.CurrentTick / TicksPerDay);
      int previousDay = (int)((context.CurrentTick - 1) / TicksPerDay);
      if (currentDay != previousDay)
        _eggsLaidToday = 0;

      base.Update(context);
    }
  }

  // ------------------------------------------------------------------
  //  Specialized WORKER ant with experience, learning, specialization
  // ------------------------------------------------------------------

  public class WorkerAnt(Position startPosition, IColony colony, long birthTick = 0)
    : Ant(AntCaste.Worker, startPosition, colony, null, birthTick)
  {
    private double _experienceLevel = 0.0;
    private readonly HashSet<WorkerSpecialization> _specializations = [];
    private int _tasksCompleted = 0;
    private readonly Dictionary<WorkerSpecialization, double> _specializationExperience = [];

    // ----------------------------
    //  Worker-specific properties
    // ----------------------------

    public double ExperienceLevel =>
      _experienceLevel;
    public IReadOnlySet<WorkerSpecialization> Specializations =>
      _specializations.ToHashSet();
    public int TasksCompleted =>
      _tasksCompleted;
    public double GetSpecializationLevel(WorkerSpecialization specialization) =>
      _specializationExperience.GetValueOrDefault(specialization, 0.0);

    protected override AntPhysiology CreateDefaultPhysiology() =>
      AntPhysiology.CreateWorkerPhysiology();
    protected override double GetBodyWeightMultiplier() =>
      1.0;
    protected override IAntBehavior? GetBehavior() =>
      AntBehaviorFactory.CreateBehavior(AntCaste.Worker);

    protected override void OnDevelopmentStageChanged(DevelopmentStage newStage)
    {
      if (newStage == DevelopmentStage.Adult)
        //  Workers become more efficient as adults
        GainExperience(0.2);
      else if (newStage == DevelopmentStage.Elder)
      {
        //  Elder workers become mentors but less physically capable
        _experienceLevel += 0.5;
        //  Could reduce some physical capabilities here
      }
    }

    public void GainSpecializationExperience(WorkerSpecialization specialization, double amount)
    {
      if (!_specializationExperience.ContainsKey(specialization))
        _specializationExperience[specialization] = 0.0;

      _specializationExperience[specialization] = Math.Min(5.0, _specializationExperience[specialization] + amount);

      //  Develop specialization after sufficient experience
      if (_specializationExperience[specialization] >= 2.0 && !_specializations.Contains(specialization))
      {
        _specializations.Add(specialization);
        LogExperience($"Developed {specialization} specialization");
      }
    }

    public void CompleteTask(WorkerSpecialization taskType, double efficiency = 1.0)
    {
      _tasksCompleted++;

      //  Gain general experience
      GainExperience(0.05 * efficiency);
      //  Gain specialization-specific experience
      GainExperienceSpecializationExperience(taskType, 0.1 * efficiency);

      LogExperience($"Completed {taskType} task (efficiency: {efficiency:F2})");
    }

    //  Gets the efficiency multiplier for a specific task type
    //  Based on experience and specialization
    public double GetTaskEfficiency(WorkerSpecialization taskType)
    {
      double baseEfficiency = 1.0 + (_experienceLevel * 0.1);  //  General experience bonus

      if (_specializations.Contains(taskType))
      {
        double specializationBonus = _specializationExperience[taskType] * 0.2;

        return baseEfficiency + specializationBonus;
      }

      return baseEfficiency;
    }

    //  Determines if this worker can mentor other workers in a specialization
    public bool CanMentor(WorkerSpecialization specialization) =>
      DevelopmentStage == DevelopmentStage.Elder &&
        _specializations.Contains(specialization) &&
        GetSpecializationLevel(specialization) >= 3.0;
  }

  // ----------------------------------------------------------------------
  //  Specialized SOLDIER ant with combat capabilities and patrol behavior
  // ----------------------------------------------------------------------

  public class SoldierAnt(Position startPosition, IColony colony, long birthTick = 0)
    : Ant(AntCaste.Soldier, startPosition, colony, null, birthTick)
  {
    private double _combatExperience = 0.0;
    private int _threatsDetected = 0;
    private int _combatVictories = 0;
    private DateTime _lastPatrol = DateTime.MinValue;

    // -----------------------------
    //  Soldier-specific properties
    // -----------------------------

    public double CombatExperience =>
      _combatExperience;
    public int ThreatsDetected =>
      _threatsDetected;
    public int CombatVictories =>
      _combatVictories;
    public double CombatEffectiveness =>
      2.5 + (_combatExperience * 0.5);  //  Base 2.5x damage
    public bool IsOnPatrol =>
      (DateTime.UtcNow - _lastPatrol).TotalSeconds < 300;  //  5 minutes patrol cycles

    protected override AntPhysiology CreateDefaultPhysiology() =>
      AntPhysiology.CreateSoldierPhysiology();
    protected override double GetBodyWeightMultiplier() =>
      1.8;  //  Soldiers are larger than workers
    protected override IAntBehavior? GetBehavior() =>
      AntBehaviorFactory.CreateBehavior(AntCaste.Soldier);

    protected override void UpdateDevelopmentStageChanged(DevelopmentStage newStage)
    {
      if (newStage == DevelopmentStage.Adult)
        //  Soldiers reach peak combat capability as adults
        _combatExperience += 0.5;
    }

    public void DetectThreat(Position threatLocation, double threatLevel)
    {
      _threatsDetected++;
      _combatExperience += 0.1;

      LogExperience($"Detected threat level {threatLevel:F1} at {threatLocation}");
    }

    public bool EngageInCombat(double enemyStrength)
    {
      double myStrength = CombatEffectiveness * (Health / MaxHealth);  //  Reduced by injuries
      bool victory = myStrength > enemyStrength;
      if (victory)
      {
        _combatVictories++;
        _combatExperience += 0.3;

        //  Minor injuries even in victory
        Health -= MaxHealth * 0.05;

        LogExperience($"Victory in combat against strength {enemyStrength:F1}");
      }
      else
      {
        //  Defeat causes significant damage but some experience
        Health -= MaxHealth * 0.3;
        _combatExperience += 0.1;

        LogExperience($"Defeated by enemy strength {enemyStrength:F1}");
      }

      //  Combat is exhausting
      ConsumeEnergy(MaxEnergy * 0.4);

      return victory;
    }

    public void StartPatrol()
    {
      _lastPatrol = DateTime.UtcNow;
      SetState(ActivityState.Moving);

      LogExperience("Started patrol");
    }

    //  Gets the detection range for threats based on experience
    public double GetThreatDetectionRange() =>
      15.0 + (_combatExperience * 2.0);  //  Base 15 units, +2 per experience point
  }

  // ------------------------------------------------------------------------------
  //  Specialized FORAGER ant with enhanced scouting and food collection abilities
  // ------------------------------------------------------------------------------

  public class ForagerAnt(Position startPosition, IColony colony, long birthTick = 0)
    : Ant(AntCaste.Forager, startPosition, colony, null, birthTick)
  {
    private double _scoutingExperience = 0.0;
    private int _foodSourcesDiscovered = 0;
    private double _totalFoodCollected = 0.0;
    private readonly Dictionary<Guid, double> _knownFoodSources = [];
    private Position? _lastKnownFoodLocation;

    // -----------------------------
    //  Forager-specific properties
    // -----------------------------

    public double ScoutingExperience =>
      _scoutingExperience;
    public int FoodSourcesDiscovered =>
      _foodSourcesDiscovered;
    public double TotalFoodCollected =>
      _totalFoodCollected;
    public double ScoutingRange =>
      30.0 + (_scoutingExperience * 5.0);
    public IReadOnlyDictionary<Guid, double> KnownFoodSources =>
      _knownFoodSources.AsReadOnly();

    protected override AntPhysiology CreateDefaultPhysiology() =>
      AntPhysiology.CreateForagerPhysiology();
    protected override double GetBodyWeightMultiplier() =>
      0.8;  //  Foragers are typically smaller/lighter
    protected override IAntBehavior? GetBehavior() =>
      AntBehaviorFactory.CreateBehavior(AntCaste.Forager);

    protected override void OnDevelopmentStageChanged(DevelopmentStage newStage)
    {
      if (newStage == DevelopmentStage.Adult)
        //  Adult foragers gain enhanced scouting abilities
        _scoutingExperience += 0.3;
    }

    public void DiscoverFoodSource(IFoodSource foodSource)
    {
      if (_knownFoodSources.ContainsKey(foodSource.Id))
        return;

      _knownFoodSources[foodSource.Id] = foodSource.AvailableFood;
      _foodSourcesDiscovered++;
      _scoutingExperience += 0.2;
      _lastKnownFoodLocation = foodSource.Position;

      LogExperience($"Discovered food source with {foodSource.AvailableFood:F1} food");
    }

    public override void CollectFood(double amount)
    {
      base.CollectFood(amount);
      _totalFoodCollected += Math.Min(amount, MaxCarryingCapacity - CurrentCarryingLoad);

      //  Gain experience from successful foraging
      _scoutingExperience += 0.5;
    }

    public void UpdateFoodSourceInfo(Guid foodSourceId, double remainingFood)
    {
      if (_knownFoodSources.ContainsKey(foodSourceId))
      {
        _knownFoodSources[foodSourceId] = remainingFood;

        //  Remove exhausted sources
        if (remainingFood <= 0)
          _knownFoodSources.Remove(foodSourceId);
      }
    }

    //  Gets the best known food source based on distance and available food
    public IFoodSource? GetBestKnownFoodSources(IEnumerable<IFoodSource> availableSources)
    {
      var knownSources = availableSources
        .Where(fs => _knownFoodSources.ContainsKey(fs.Id) && !fs.IsExhausted)
        .ToList();

      if (knownSources.Count == 0)
        return null;

      //  Score based on food amount and distance
      return knownSources
        .Select(fs => new
        {
          Source = fs,
          Score = CalculateFoodSourceScore(fs)
        })
        .OrderByDescending(x => x.Score)
        .First()
        .Source;
    }

    private double CalculateFoodSourceScore(IFoodSource foodSource)
    {
      double distance = Position.DistanceTo(foodSource.Position);
      double speed = Physiology.GetEffectiveMovementSpeed(CurrentCarryingLoad);

      //  Experience reduces estimation error
      double experienceModifier = 1.0 + (_scoutingExperience * 0.1);

      return (distance / speed) * experienceModifier;
    }
  }
}
