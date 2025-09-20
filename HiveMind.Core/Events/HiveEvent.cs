using HiveMind.Core.Entities;

namespace HiveMind.Core.Events
{
  /// <summary>
  /// Base class for all hive-related events in the simulation.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="HiveEvent"/> class.
  /// </remarks>
  /// <param name="hive">The hive involved in the event.</param>
  /// <param name="timestamp">The time when the event occurred.</param>
  public abstract class HiveEvent(Beehive hive, DateTime timestamp)
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
    /// Gets the beehive involved in the event.
    /// </summary>
    public Beehive Hive { get; } = hive ?? throw new ArgumentNullException(nameof(hive));

    /// <summary>
    /// Gets the type of hive event.
    /// </summary>
    public abstract HiveEventType EventType { get; }
  }

  /// <summary>
  /// Event raised when a hive is established.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="HiveEstablishedEvent"/> class.
  /// </remarks>
  /// <param name="hive">The newly established hive.</param>
  /// <param name="initialPopulation">The initial population count.</param>
  /// <param name="timestamp">The establishment timestamp.</param>
  public sealed class HiveEstablishedEvent(
    Beehive hive,
    int initialPopulation,
    DateTime timestamp
  ) : HiveEvent(hive, timestamp)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override HiveEventType EventType => HiveEventType.Established;

    /// <summary>
    /// Gets the initial population count.
    /// </summary>
    public int InitialPopulation { get; } = initialPopulation;
  }

  /// <summary>
  /// Event raised when a hive collapses (becomes non-viable).
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="HiveCollapseEvent"/> class.
  /// </remarks>
  /// <param name="hive">The collapsed hive.</param>
  /// <param name="reason">The reason for collapse.</param>
  /// <param name="timestamp">The collapse timestamp.</param>
  public sealed class HiveCollapseEvent(
    Beehive hive,
    CollapseReason
    reason,
    DateTime timestamp
  ) : HiveEvent(hive, timestamp)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override HiveEventType EventType => HiveEventType.Collapse;

    /// <summary>
    /// Gets the reason for the collapse.
    /// </summary>
    public CollapseReason Reason { get; } = reason;

    /// <summary>
    /// Gets the final population at collapse.
    /// </summary>
    public int FinalPopulation { get; } = hive.TotalPopulation;

    /// <summary>
    /// Gets the age of the hive at collapse.
    /// </summary>
    public TimeSpan HiveAge { get; } = timestamp - hive.FoundedDate;
  }

  /// <summary>
  /// Event raised when honey is produced and stored.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="HoneyProductionEvent"/> class.
  /// </remarks>
  /// <param name="hive">The hive producing honey.</param>
  /// <param name="honeyAmount">The amount of honey produced.</param>
  /// <param name="foragingWorkers">The number of foraging workers.</param>
  /// <param name="timestamp">The production timestamp.</param>
  public sealed class HoneyProductionEvent(
    Beehive hive,
    double honeyAmount,
    int foragingWorkers,
    DateTime timestamp
  ) : HiveEvent(hive, timestamp)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override HiveEventType EventType => HiveEventType.HoneyProduction;

    /// <summary>
    /// Gets the amount of honey produced.
    /// </summary>
    public double HoneyAmount { get; } = honeyAmount;

    /// <summary>
    /// Gets the number of foraging workers involved.
    /// </summary>
    public int ForagingWorkers { get; } = foragingWorkers;
  }

  /// <summary>
  /// Event raised when a honey super becomes ready for harvest.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="HoneyReadyForHarvestEvent"/> class.
  /// </remarks>
  /// <param name="hive">The hive with honey ready for harvest.</param>
  /// <param name="honeySuper">The honey super ready for harvest.</param>
  /// <param name="timestamp">The readiness timestamp.</param>
  public sealed class HoneyReadyForHarvestEvent(
    Beehive hive,
    HoneySuper honeySuper,
    DateTime timestamp
  ) : HiveEvent(hive, timestamp)
  {
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public override HiveEventType EventType => HiveEventType.HoneyReadyForHarvest;

    /// <summary>
    /// Gets the honey super ready for harvest.
    /// </summary>
    public HoneySuper HoneySuper { get; } = honeySuper ?? throw new ArgumentNullException(nameof(honeySuper));

    /// <summary>
    /// Gets the amount of honey ready for harvest.
    /// </summary>
    public double HarvestableAmount { get; } = honeySuper.CurrentHoneyAmount;
  }

  /// <summary>
  /// Types of hive events that can occur in the simulation.
  /// </summary>
  public enum HiveEventType
  {
    /// <summary>
    /// A new hive was established.
    /// </summary>
    Established = 1,

    /// <summary>
    /// The hive collapsed and became non-viable.
    /// </summary>
    Collapse = 2,

    /// <summary>
    /// Honey was produced and stored.
    /// </summary>
    HoneyProduction = 3,

    /// <summary>
    /// Honey is ready for harvest.
    /// </summary>
    HoneyReadyForHarvest = 4,

    /// <summary>
    /// A swarming event occurred.
    /// </summary>
    Swarming = 5,

    /// <summary>
    /// Population reached a milestone.
    /// </summary>
    PopulationMilestone = 6
  }

  /// <summary>
  /// Possible reasons for hive collapse.
  /// </summary>
  public enum CollapseReason
  {
    /// <summary>
    /// Loss of queen without replacement.
    /// </summary>
    QueenLoss = 1,

    /// <summary>
    /// Worker population fell below viable levels.
    /// </summary>
    PopulationCollapse = 2,

    /// <summary>
    /// Insufficient food/honey stores.
    /// </summary>
    Starvation = 3,

    /// <summary>
    /// Environmental conditions too harsh.
    /// </summary>
    EnvironmentalStress = 4,

    /// <summary>
    /// Disease or parasite outbreak.
    /// </summary>
    Disease = 5,

    /// <summary>
    /// Multiple contributing factors.
    /// </summary>
    MultipleCauses = 6
  }
}
