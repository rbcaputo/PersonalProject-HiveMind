using HiveMind.Core.Common;
using HiveMind.Core.Enums;
using HiveMind.Core.ValueObject;

namespace HiveMind.Core.Entities
{
  /// <summary>
  /// Base class for all bee entities in the colony.
  /// Implements common bee behaviors and properties shared across all bee types.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="Bee"/> class.
  /// </remarks>
  /// <param name="birthTime">The time when the bee was born.</param>
  /// <param name="position">The initial position of the bee.</param>
  public abstract class Bee(DateTime birthTime, Position3D position) : Entity
  {
    private DateTime _birthTime = birthTime;
    private LifecycleStage _lifecycleStage = LifecycleStage.Egg;
    private Position3D _position = position ?? throw new ArgumentNullException(nameof(position));
    private double _energy = 1.0;
    private bool _isAlive = true;

    /// <summary>
    /// Gets the type of bee (Worker, Drone, or Queen).
    /// </summary>
    public abstract BeeType BeeType { get; }

    /// <summary>
    /// Gets the time when the bee was born (hatched from egg).
    /// </summary>
    public DateTime BirthTime => _birthTime;

    /// <summary>
    /// Gets the current age of the bee.
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - _birthTime;

    /// <summary>
    /// Gets the current lifecycle stage of the bee.
    /// </summary>
    public LifecycleStage LifecycleStage => _lifecycleStage;

    /// <summary>
    /// Gets the current position of the bee in 3D space.
    /// </summary>
    public Position3D Position => _position;

    /// <summary>
    /// Gets the current energy level of the bee (0.0 to 1.0).
    /// </summary>
    public double Energy => _energy;

    /// <summary>
    /// Gets a value indicating whether the bee is alive.
    /// </summary>
    public bool IsAlive => _isAlive && _lifecycleStage != LifecycleStage.Dead;

    /// <summary>
    /// Gets the maximum lifespan for this type of bee.
    /// </summary>
    public abstract TimeSpan MaxLifespan { get; }

    /// <summary>
    /// Gets a value indicating whether the bee is capable of flight.
    /// Only adult bees can fly.
    /// </summary>
    public bool CanFly => IsAlive && _lifecycleStage == LifecycleStage.Adult;

    /// <summary>
    /// Updates the bee's lifecycle stage based on its age and bee type.
    /// </summary>
    public virtual void UpdateLifecycleStage()
    {
      if (!_isAlive) return;

      TimeSpan age = Age;

      if (age >= MaxLifespan)
      {
        Die();
        return;
      }

      _lifecycleStage = age.TotalDays switch
      {
        < 3 => LifecycleStage.Egg,
        < GetLarvalPeriodDays() => LifecycleStage.Larva,
        < GetPupalPeriodDays() => LifecycleStage.Pupa,
        _ => LifecycleStage.Adult
      };
    }

    /// <summary>
    /// Moves the bee to a new position.
    /// </summary>
    /// <param name="newPosition">The new position to move to.</param>
    /// <exception cref="InvalidOperationException">Thrown when the bee cannot move (dead or not adult).</exception>
    public virtual void MoveTo(Position3D newPosition)
    {
      ArgumentNullException.ThrowIfNull(newPosition);

      if (!CanFly)
        throw new InvalidOperationException("Bee cannot move - either dead or not yet adult.");

      _position = newPosition;
      ConsumeEnergy(0.01); // Moving consumes a small amount of energy
    }

    /// <summary>
    /// Consumes the specified amount of energy.
    /// </summary>
    /// <param name="amount">The amount of energy to consume (0.0 to 1.0).</param>
    public void ConsumeEnergy(double amount)
    {
      ArgumentOutOfRangeException.ThrowIfNegative(amount, "Energy consumption cannot be negative.");

      _energy = Math.Max(0, _energy - amount);

      if (_energy <= 0)
        Die();
    }

    /// <summary>
    /// Restores the specified amount of energy.
    /// </summary>
    /// <param name="amount">The amount of energy to restore (0.0 to 1.0).</param>
    public void RestoreEnergy(double amount)
    {
      ArgumentOutOfRangeException.ThrowIfNegative(amount, "Energy restoration cannot be negative.");

      _energy = Math.Min(1.0, _energy + amount);
    }

    /// <summary>
    /// Marks the bee as dead and updates its lifecycle stage.
    /// </summary>
    public virtual void Die()
    {
      _isAlive = false;
      _lifecycleStage = LifecycleStage.Dead;
      _energy = 0;
    }

    /// <summary>
    /// Gets the larval period duration in days for this bee type.
    /// </summary>
    /// <returns>The larval period duration in days.</returns>
    protected abstract double GetLarvalPeriodDays();

    /// <summary>
    /// Gets the pupal period duration in days for this bee type.
    /// </summary>
    /// <returns>The pupal period duration in days.</returns>
    protected abstract double GetPupalPeriodDays();

    /// <summary>
    /// Performs bee-specific activities during simulation tick.
    /// Override in derived classes to implement specific behaviors.
    /// </summary>
    /// <param name="environment">The current environment state.</param>
    public abstract void PerformActivity(Environment environment);
  }
}
