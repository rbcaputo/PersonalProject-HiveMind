using HiveMind.Core.Domain.Behaviors;
using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;
using HiveMind.Core.ValueObjects;
using static HiveMind.Core.ValueObjects.AntPhysiology;

namespace HiveMind.Core.Domain.Entities
{
  // ================================================
  //  Represents an individual ant in the simulation
  // ================================================

  public abstract class Ant : BaseEntity, IInsect
  {
    private double _health;
    private double _energy;
    private NutritionalStatus _nutritionalStatus = NutritionalStatus.Adequate;

    protected Ant(
      AntCaste caste,
      Position startPosition,
      IColony colony,
      AntPhysiology? customPhysiology = null
    )
    {
      Caste = caste;
      Position = startPosition;
      Colony = colony ?? throw new ArgumentNullException(nameof(colony));

      //  Initialize physiology - can be overridden by subclasses
      Physiology = customPhysiology ?? CreateDefaultPhysiology();

      //  Initialize vitals based on physiology
      MaxHealth = MaxEnergy = CalculateMaxVitals();
      Health = MaxHealth;
      Energy = MaxEnergy;

      DevelopmentStage = DevelopmentStage.Egg;  //  Start as egg
      BirthTick = 0;  //  Set by simulation context

    }

    //  Core properties
    public InsectType Type => InsectType.Ant;
    public AntCaste Caste { get; protected set; }
    public Position Position { get; protected set; }
    public ActivityState CurrentState { get; protected set; } = ActivityState.Idle;
    public IColony Colony { get; }
    public AntPhysiology Physiology { get; protected set; }

    //  Vitals
    public double Health
    {
      get => _health;
      protected set => _health = Math.Max(0, Math.Min(MaxHealth, value));
    }

    public double Energy
    {
      get => _energy;
      protected set
      {
        _energy = Math.Max(0, Math.Min(MaxEnergy, value));
        UpdateNutritionalStatus();
      }
    }

    public double MaxHealth { get; protected set; }
    public double MaxEnergy { get; protected set; }
    public bool IsAlive => Health > 0 && DevelopmentStage != DevelopmentStage.Dead;

    //  Lifecyle properties
    public DevelopmentStage DevelopmentStage { get; protected set; }
    public long BirthTick { get; set; }
    public int AgeDays => (int)((CurrentTick - BirthTick) / TicksPerDay);
    public NutritionalStatus NutritionalStatus => _nutritionalStatus;

    //  Carrying capacity
    public double CarriedFood { get; protected set; }
    public double MaxCarryingCapacity => Physiology.MaxCarryCapacity * GetBodyWeightMultiplier();

    //  Abstract methods for subclass specialization
    protected abstract AntPhysiology CreateDefaultPhysiology();
    protected abstract double GetBodyWeightMultiplier();
    protected abstract void OnDevelopmentStageChanged(DevelopmentStage newStage);

    //  Metabolism and aging
    public virtual void Update(ISimulationContext context)
    {
      if (!IsAlive)
        return;

      CurrentTick = context.CurrentTick;

      //  Age-Based development
      UpdateDevelopmentStage();

      //  Metabolic processes
      ProcessMetabolism(context.DeltaTime);

      //  Stage-specific behaviors
      if (DevelopmentStage >= DevelopmentStage.YoungAdult)
      {
        GetBehavior()?.Update(this, context);
        ProcessMovement(context);
      }

      //  Health effects
      ProcessAging();
      ProcessEnvironmentalEffects(context);

      UpdateTimestamp();
    }

    protected virtual void UpdateDevelopmentState()
    {
      var newStage = CalculateDevelopmentStage();
      if (newStage != DevelopmentStage)
      {
        var previousStage = DevelopmentStage;
        DevelopmentStage = newStage;
        OnDevelopmentStageChanged(newStage);

        //  Adjust vitals for new stage
        if (newStage == DevelopmentStage.YoungAdult)
        {
          //  Full health and energy when becoming adult
          Health = MaxHealth;
          Energy = MaxEnergy;
        }
      }
    }

    private DevelopmentStage CalculateDevelopmentState() =>
      AgeDays switch
      {
        < 14 => DevelopmentStage.Egg,
        < 35 => DevelopmentStage.Larva,
        < 49 => DevelopmentStage.Pupa,
        < 79 => DevelopmentStage.YoungAdult,
        < Physiology.LongevityDays * 0.8 => DevelopmentStage.Adult,
        < Physiology.LongevityDays => DevelopmentStage.Elder,
        _ => DevelopmentStage.Dead
      };

    protected virtual void ProcessMetabolism(double deltaTime)
    {
      if (DevelopmentStage < DevelopmentStage.YoungAdult)
        return;

      //  Base metabolic cost
      double metabolicCost = Physiology.MetabolismRate * deltaTime;

      //  Activity modifiers
      metabolicCost *= GetActivityEnergyMultiplier();

      //  Temperature effects
      metabolicCost *= GetTemperatureMatabolismMultiplier();

      ConsumeEnergy(metabolicCost);
    }

    private double GetActivityEnergyMultiplier() =>
      CurrentState switch
      {
        ActivityState.Moving => 1.5,
        ActivityState.Fighting => 2.0,
        ActivityState.Building => 1.8,
        ActivityState.Foraging => 1.3,
        ActivityState.Resting => 0.3,
        _ => 1.0
      };

    private double GetTemperatureMatabolismMultiplier()
    {
      //  Simplified temperature effect - could be enhanced with environment data
      return 1.0;  //  TODO: Implement with environmental temperature
    }

    private void UpdateNutritionalStatus()
    {
      double energyRatio = Energy / MaxEnergy;
      _nutritionalStatus = energyRatio switch
      {
        < 0.1 => NutritionalStatus.Starving,
        < 0.3 => NutritionalStatus.Hungry,
        < 0.9 => NutritionalStatus.Adequate,
        < 1.0 => NutritionalStatus.WellFed,
        _ => NutritionalStatus.Overfed
      };
    }

    //  Constants
    protected static readonly int TicksPerDay = 86400;  //  1 tick = 1 second
    protected long CurrentTick { get; private set; }

    //  Abstract behavior retrieval
    protected abstract IAntBehavior? GetBehavior();

    //  Vitals calculation based on caste
    protected virtual double CalculateMaxVitals() =>
      Caste switch
      {
        AntCaste.Queen => 200.0,    //  Queens are larger and more robust
        AntCaste.Soldier => 120.0,  //  Soldiers are tough
        AntCaste.Worker => 100.0,   //  Standard worker vitals
        AntCaste.Forager => 90.0,   //  Foragers trade vitals for speed
        _ => 80.0
      };

    // TODO: Add remaining methods (MoveTo, ConsumeEnergy, etc.) with enhanced logic

    // ======================================================
    //  Specialized QUEEN ant with reproductive capabilities
    // ======================================================

    public class QueenAnt(Position startPosition, IColony colony) : Ant(AntCaste.Queen, startPosition, colony)
    {
      public int EggsLaidToday { get; private set; }
      public int EggsLaidTotal { get; private set; }
      public double FertilityRate { get; private set; } = 1.0;

      //  Queens live much longer and have different physiology
      protected override AntPhysiology CreateDefaultPhysiology() =>
        new(
          metabolismRate: 0.3,      //  Very slow metabolism
          maxCarryCapacity: 0.0,    //  Queens don't carry items
          movementSpeed: 0.2,       //  Very slow movement
          longevityDays: 7300,      //  ~20 years
          pheromoneProduction: 5.0, //  Strong pheromone production
          scentSensitivity: 0.3     //  Don't need to follow trails
        );

      protected override double GetBodyWeightMultiplier() =>
        3.0;  //  Queens are much larger

      protected override void OnDevelopmentStageChanged(DevelopmentStage newStage)
      {
        if (newStage == DevelopmentStage.Adult)
          FertilityRate = 1.0;  //  Reach peak fertility
        else if (newStage == DevelopmentStage.Elder)
          FertilityRate *= 0.7; //  Reduced fertility in old age 
      }

      public bool CanLayEggs() =>
        DevelopmentStage >= DevelopmentStage.Adult &&
        Energy > MaxEnergy * 0.4 &&
        NutritionalStatus >= NutritionalStatus.Adequate;

      public int LayEggs(int maxEggs)
      {
        if (!CanLayEggs())
          return 0;

        int eggsToLay = Math.Min(maxEggs, GetMaxEggsForCurrentCondition());
        double energyCost = eggsToLay * 2.0;  //  Each egg costs energy

        if (Energy >= energyCost)
        {
          ConsumeEnergy(energyCost);
          EggsLaidToday += eggsToLay;
          EggsLaidTotal += eggsToLay;

          return eggsToLay;
        }

        return 0;
      }

      private int GetMaxEggsForCurrentCondition()
      {
        int baseEggs = 50;  //  Base egg laying capacity

        //  Modifiers based on condition
        double multiplier = FertilityRate;

        if (NutritionalStatus == NutritionalStatus.WellFed)
          multiplier *= 1.5;
        else if (NutritionalStatus == NutritionalStatus.Hungry)
          multiplier *= 0.5;
        else if (NutritionalStatus == NutritionalStatus.Starving)
          multiplier *= 0.1;

        return (int)(baseEggs * multiplier);
      }

      protected override IAntBehavior? GetBehavior() =>
        //  Queens have specialized behavior focused on reproduction
        AntBehaviorFactory.CreateBehavior(AntCaste.Queen);
    }

    // ==============================================================
    //  Specialized WORKER ant with experience and skill development
    // ==============================================================

    public class WorkerAnt(Position startPosition, IColony colony) : Ant(AntCaste.Worker, startPosition, colony)
    {
      public double ExperienceLevel { get; private set; } = 0.0;
      public HashSet<WorkerSpecialization> Specializations { get; } = [];
      public int TasksCompleted { get; private set; }

      protected override AntPhysiology CreateDefaultPhysiology() =>
        new(
          metabolismRate: 1.0,      //  Standard metabolism
          maxCarryCapacity: 10.0,   //  Can carry 10x body weight
          movementSpeed: 1.0,       //  Standard speed
          longevityDays: 120,       //  ~4 months
          scentSensitivity: 0.8,    //  Good trail following
          pheromoneProduction: 1.0  //  Standard pheromone production
        );

      protected override double GetBodyWeightMultiplier() =>
        1.0;  //  Standard body size

      protected override void OnDevelopmentStageChanged(DevelopmentStage newStage)
      {
        if (newStage == DevelopmentStage.Adult)
          //  Workers become more efficient as adults
          ExperienceLevel = Math.Min(1.0, ExperienceLevel + 0.1);
      }

      public void GainExperience(double amount) =>
        ExperienceLevel = Math.Min(2.0, ExperienceLevel + amount); //  Cap at 2x normal efficiency

      public void CompleteTask(WorkerSpecialization specialization)
      {
        TasksCompleted++;
        GainExperience(0.05);

        //  Develop specialization over time
        if (TasksCompleted % 10 == 0)
          Specializations.Add(specialization);
      }

      protected override IAntBehavior? GetBehavior() =>
        AntBehaviorFactory.CreateBehavior(AntCaste.Worker);

      public enum WorkerSpecialization
      {
        Construction,  //  Building and maintenance
        Nursing,       //  Brood care
        Foraging,      //  Food collection
        Maintenance,   //  Nest upkeep
        Defense,       //  Guard duties
        Excavation     //  Tunnel digging
      }
    }
  }
}
