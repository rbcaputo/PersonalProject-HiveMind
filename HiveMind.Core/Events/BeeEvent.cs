using HiveMind.Core.Entities;
using HiveMind.Core.Enums;

namespace HiveMind.Core.Events
{
  /// <summary>
  /// Base class for all bee-related events in the simulation.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="BeeEvent"/> class.
  /// </remarks>
  /// <param name="bee">The bee involved in the event.</param>
  /// <param name="timestamp">The time when the event occurred.</param>
  public abstract class BeeEvent(Bee bee, DateTime timestamp)
  {
    /// <summary>
    /// Gets the unique identifier of the event.
    /// </summary>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the time when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; } = timestamp;

    /// <summary>
    /// Gets the bee involved in the event.
    /// </summary>
    public Bee Bee { get; } = bee ?? throw new ArgumentNullException(nameof(bee));

    /// <summary>
    /// Gets the type of bee event.
    /// </summary>
    public abstract BeeEventType EventType { get; }
  }

  /// <summary>
  /// Event raised when a bee is born (emerges from pupa).
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="BeeBirthEvent"/> class.
  /// </remarks>
  /// <param name="bee">The newly born bee.</param>
  /// <param name="birthChamber">The chamber where birth occurred.</param>
  /// <param name="timestamp">The birth timestamp.</param>
  public sealed class BeeBirthEvent(Bee bee, Chamber birthChamber, DateTime timestamp) : BeeEvent(bee, timestamp)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override BeeEventType EventType => BeeEventType.Birth;

    /// <summary>
    /// Gets the chamber where the bee was born.
    /// </summary>
    public Chamber BirthChamber { get; } = birthChamber ?? throw new ArgumentNullException(nameof(birthChamber));
  }

  /// <summary>
  /// Event raised when a bee dies.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="BeeDeathEvent"/> class.
  /// </remarks>
  /// <param name="bee">The bee that died.</param>
  /// <param name="cause">The cause of death.</param>
  /// <param name="timestamp">The death timestamp.</param>
  public sealed class BeeDeathEvent(Bee bee, DeathCause cause, DateTime timestamp) : BeeEvent(bee, timestamp)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override BeeEventType EventType => BeeEventType.Death;

    /// <summary>
    /// Gets the cause of death.
    /// </summary>
    public DeathCause Cause { get; } = cause;

    /// <summary>
    /// Gets the age of the bee at time of death.
    /// </summary>
    public TimeSpan AgeAtDeath { get; } = bee.Age;
  }

  /// <summary>
  /// Event raised when a worker bee changes activity.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="WorkerActivityChangeEvent"/> class.
  /// </remarks>
  /// <param name="worker">The worker bee changing activity.</param>
  /// <param name="previousActivity">The previous activity.</param>
  /// <param name="newActivity">The new activity.</param>
  /// <param name="timestamp">The change timestamp.</param>
  public sealed class WorkerActivityChangeEvent(
    Worker worker, WorkerActivity previousActivity,
    WorkerActivity newActivity,
    DateTime timestamp
  ) : BeeEvent(worker, timestamp)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override BeeEventType EventType => BeeEventType.ActivityChange;

    /// <summary>
    /// Gets the worker bee (cast for convenience).
    /// </summary>
    public Worker Worker => (Worker)Bee;

    /// <summary>
    /// Gets the previous activity.
    /// </summary>
    public WorkerActivity PreviousActivity { get; } = previousActivity;

    /// <summary>
    /// Gets the new activity.
    /// </summary>
    public WorkerActivity NewActivity { get; } = newActivity;
  }

  /// <summary>
  /// Event raised when the queen lays eggs.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="QueenEggLayingEvent"/> class.
  /// </remarks>
  /// <param name="queen">The queen that laid eggs.</param>
  /// <param name="eggsLaid">The number of eggs laid.</param>
  /// <param name="chamber">The chamber where eggs were laid.</param>
  /// <param name="timestamp">The laying timestamp.</param>
  public sealed class QueenEggLayingEvent(
    Queen queen,
    int eggsLaid,
    Chamber chamber,
    DateTime timestamp
  ) : BeeEvent(queen, timestamp)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override BeeEventType EventType => BeeEventType.EggLaying;

    /// <summary>
    /// Gets the queen bee (cast for convenience).
    /// </summary>
    public Queen Queen => (Queen)Bee;

    /// <summary>
    /// Gets the number of eggs laid.
    /// </summary>
    public int EggsLaid { get; } = eggsLaid;

    /// <summary>
    /// Gets the chamber where eggs were laid.
    /// </summary>
    public Chamber Chamber { get; } = chamber ?? throw new ArgumentNullException(nameof(chamber));
  }

  /// <summary>
  /// Types of bee events that can occur in the simulation.
  /// </summary>
  public enum BeeEventType
  {
    /// <summary>
    /// A bee was born (emerged from pupa).
    /// </summary>
    Birth = 1,

    /// <summary>
    /// A bee died.
    /// </summary>
    Death = 2,

    /// <summary>
    /// A worker bee changed its activity.
    /// </summary>
    ActivityChange = 3,

    /// <summary>
    /// The queen laid eggs.
    /// </summary>
    EggLaying = 4,

    /// <summary>
    /// A drone attempted mating.
    /// </summary>
    Mating = 5,

    /// <summary>
    /// A drone was ejected from the hive.
    /// </summary>
    Ejection = 6
  }

  /// <summary>
  /// Possible causes of bee death.
  /// </summary>
  public enum DeathCause
  {
    /// <summary>
    /// Natural death from old age.
    /// </summary>
    OldAge = 1,

    /// <summary>
    /// Death from energy depletion.
    /// </summary>
    Exhaustion = 2,

    /// <summary>
    /// Death from environmental factors (cold, heat, etc.).
    /// </summary>
    Environmental = 3,

    /// <summary>
    /// Death from disease or parasites.
    /// </summary>
    Disease = 4,

    /// <summary>
    /// Death during mating (drones).
    /// </summary>
    Mating = 5,

    /// <summary>
    /// Ejection from hive (typically drones).
    /// </summary>
    Ejection = 6
  }
}
