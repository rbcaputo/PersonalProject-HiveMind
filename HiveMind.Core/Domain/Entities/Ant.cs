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

    private Position _targetPosition;
    private readonly IAntBehavior _behavior;

    public Ant(AntRole role, Position startPosition, IAntBehavior behavior)
    {
      Role = role;
      Position = startPosition;
      _targetPosition = startPosition;
      _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));

      InitializeAttributes();
      CurrentState = ActivityState.Idle;
    }

    public void Update(ISimulationContext context)
    {
      if (!IsAlive) return;

      // Age the ant
      Age++;

      // Natural energy consumption
      ConsumeEnergy(0.1);

      // Update behavior
      _behavior.Update(this, context);

      // Handle movement
      UpdateMovement(context);

      // Check for death conditions
      CheckVitals();

      UpdateTimestamp();
    }

    public void MoveTo(Position newPosition)
    {
      if (CurrentState == ActivityState.Dead) return;

      _targetPosition = newPosition;
      CurrentState = ActivityState.Moving;
    }

    public void ConsumeEnergy(double amount)
    {
      Energy = Math.Max(0, Energy - amount);
      if (Energy == 0)
        Health -= 1;
    }

    public void RestoreEnergy(double amount) =>
      Energy = Math.Min(MaxEnergy, Energy + amount);

    public void CollectFood(double amount) =>
      CarriedFood += amount;

    public double DropFood()
    {
      var dropped = CarriedFood;
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

      var distance = Position.DistanceTo(_targetPosition);
      var moveDistance = Speed * context.DeltaTime;

      if (distance <= moveDistance)
      {
        Position = _targetPosition;
        CurrentState = ActivityState.Idle;
      }
      else
      {
        var ratio = moveDistance / distance;
        var deltaX = (_targetPosition.X - Position.X) * ratio;
        var deltaY = (_targetPosition.Y - Position.Y) * ratio;
        Position = Position.MoveTo(deltaX, deltaY);
      }
    }

    private void CheckVitals()
    {
      if (Health <= 0 || Age > GetMaxAge())
      {
        CurrentState = ActivityState.Dead;
        Health = 0;
        Energy = 0;
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
