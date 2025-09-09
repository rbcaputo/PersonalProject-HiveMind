using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Entities
{
  /// <summary>
  /// Represents an individual ant in the simulation
  /// </summary>
  public class Ant : BaseEntity, IInsect
  {
    public InsectType Type => InsectType.Ant;
    public AntRole Role { get; private set; }
    public Position Position { get; private set; }
    public ActivityState CurrentState { get; private set; }
    public double Health { get; private set; }
    public double Energy { get; private set; }
    public int Age { get; private set; }
    public bool IsAlive => Health > 0;
    public double CarriedFood { get; private set; }
    public double MaxEnergy { get; private set; }
    public double MaxHealth { get; private set; }
    public double Speed { get; private set; }
    public IColony Colony { get; private set; }

    private Position _targetPosition;
    private readonly IAntBehavior _behavior;
    private int _zeroEnergyTicks = 0;
    private const int MAX_ZERO_ENERGY_TICKS = 10; // Allow 10 ticks of zero energy before health damage
    private const double STARVATION_HEALTH_DAMAGE = 2.0; // Health damage per starvation cycle

    public Ant(AntRole role, Position startPosition, IAntBehavior behavior, IColony colony)
    {
      if (!Enum.IsDefined(typeof(AntRole), role))
        throw new ArgumentException($"Invalid ant role: {role}", nameof(role));
      if (!startPosition.IsValid)
        throw new ArgumentException("Start position must be valid", nameof(startPosition));

      _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
      Colony = colony ?? throw new ArgumentNullException(nameof(colony));

      Role = role;
      Position = startPosition;
      _targetPosition = startPosition;

      InitializeAttributes();
      CurrentState = ActivityState.Idle;
    }

    public void Update(ISimulationContext context)
    {
      if (!IsAlive)
        return;

      // Age the ant
      Age++;

      // Natural energy consumption
      ConsumeEnergy(0.1);

      // Update behavior
      _behavior.Update(this, context);

      // Handle movement
      UpdateMovement(context);

      // Check for starvation and health effects
      HandleStarvation();

      // Check for death conditions
      CheckVitals();

      UpdateTimestamp();
    }

    public void MoveTo(Position newPosition)
    {
      if (CurrentState == ActivityState.Dead)
        return;

      _targetPosition = newPosition;
      CurrentState = ActivityState.Moving;
    }

    public void ConsumeEnergy(double amount)
    {
      if (!IsAlive)
        return;

      double previousEnergy = Energy;
      Energy = Math.Max(0, Energy - amount);

      // If energy hits zero, start tracking starvation
      if (Energy == 0 && previousEnergy > 0)
        _zeroEnergyTicks = 1;
    }

    public void RestoreEnergy(double amount)
    {
      if (!IsAlive)
        return;

      Energy = Math.Min(MaxEnergy, Energy + amount);

      // Reset starvation counter when energy is restored
      if (Energy > 0)
        _zeroEnergyTicks = 0;
    }

    public bool IsStarving =>
      Energy == 0 && _zeroEnergyTicks > 0;

    public double HealthRatio =>
      MaxHealth > 0
        ? Energy / MaxHealth
        : 0;

    public double EnergyRatio =>
      MaxEnergy > 0
        ? Energy / MaxEnergy
        : 0;

    private void HandleStarvation()
    {
      if (Energy == 0)
      {
        _zeroEnergyTicks++;

        // Apply health damage after prolonged zero energy
        if (_zeroEnergyTicks >= MAX_ZERO_ENERGY_TICKS)
        {
          Health = Math.Max(0, Health - STARVATION_HEALTH_DAMAGE);
          _zeroEnergyTicks = 0; // Reset counter after applying damage

          // Set starving state for behavior awareness
          // Force rest when starving
          if (CurrentState != ActivityState.Dead)
            CurrentState = ActivityState.Resting;
        }
      }
      else if (Energy < MaxEnergy * 0.1) // Very low energy (below 10%)
      {
        // Gradual health degradation from very low energy
        if (Health > 0)
        {
          double healthLoss = 0.1 * (1.0 - Energy / (MaxEnergy * 0.1)); // More damage the lower the energy
          Health = Math.Max(0, Health - healthLoss);
        }
      }
    }

    public void CollectFood(double amount) =>
      CarriedFood += amount;

    public double DropFood()
    {
      double dropped = CarriedFood;
      CarriedFood = 0;

      return dropped;
    }

    public void SetState(ActivityState newState) =>
      CurrentState = newState;

    private void InitializeAttributes()
    {
      // Role-based attribute initialization
      switch (Role)
      {
        case AntRole.Queen:
          MaxHealth = MaxEnergy = 100;
          Speed = 0.5;
          break;
        case AntRole.Worker:
          MaxHealth = MaxEnergy = 80;
          Speed = 1.2;
          break;
        case AntRole.Soldier:
          MaxHealth = MaxEnergy = 90;
          Speed = 1.0;
          break;
        case AntRole.Forager:
          MaxHealth = MaxEnergy = 75;
          Speed = 1.5;
          break;
        default:
          MaxHealth = MaxEnergy = 70;
          Speed = 1.0;
          break;
      }

      Health = MaxHealth;
      Energy = MaxEnergy;
    }

    private void UpdateMovement(ISimulationContext context)
    {
      if (CurrentState != ActivityState.Moving || Position.Equals(_targetPosition))
        return;

      double distance = Position.DistanceTo(_targetPosition);
      double moveDistance = Speed * context.DeltaTime;

      if (distance <= moveDistance)
      {
        Position = _targetPosition;
        CurrentState = ActivityState.Idle;
      }
      else
      {
        double ratio = moveDistance / distance;
        double deltaX = (_targetPosition.X - Position.X) * ratio;
        double deltaY = (_targetPosition.Y - Position.Y) * ratio;
        Position = Position.MoveTo(deltaX, deltaY);
      }
    }

    private void CheckVitals()
    {
      int maxAge = GetMaxAge();

      if (Health <= 0 || Age > maxAge)
      {
        CurrentState = ActivityState.Dead;
        Health = 0;
        Energy = 0;
        _zeroEnergyTicks = 0;
      }

      // Extreme old age causes gradual health decline
      if (Age > maxAge * 0.8) // After 80% of max age
      {
        double agingFactor = (double)(Age - maxAge * 0.8) / (maxAge * 0.2);
        double agingDamage = 0.05 * agingFactor; // Gradual aging damage
        Health = Math.Max(Health, Health - agingDamage);
      }
    }

    private int GetMaxAge() =>
      Role switch
      {
        AntRole.Queen => 10000,
        _ => 1000
      };
  }
}
