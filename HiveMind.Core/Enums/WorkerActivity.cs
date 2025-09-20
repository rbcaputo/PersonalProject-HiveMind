namespace HiveMind.Core.Enums
{
  /// <summary>
  /// Defines the various activities that worker bees can perform during their lifecycle.
  /// Activities change based on age and colony needs.
  /// </summary>
  public enum WorkerActivity
  {
    /// <summary>
    /// Newly emerged worker cleaning cells and performing basic maintenance.
    /// </summary>
    HouseCleaning = 1,

    /// <summary>
    /// Feeding larvae and caring for developing bees.
    /// </summary>
    NurseDuty = 2,

    /// <summary>
    /// Building wax comb and constructing new cells.
    /// </summary>
    WaxProduction = 3,

    /// <summary>
    /// Storing honey and processing nectar.
    /// </summary>
    FoodStorage = 4,

    /// <summary>
    /// Guarding the hive entrance and protecting the colony.
    /// </summary>
    GuardDuty = 5,

    /// <summary>
    /// Foraging for nectar, pollen, water, and propolis.
    /// </summary>
    Foraging = 6,

    /// <summary>
    /// Resting or transitioning between activities.
    /// </summary>
    Resting = 7
  }
}
