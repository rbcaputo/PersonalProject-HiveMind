using HiveMind.Core.Entities;
using HiveMind.Core.Enums;
using HiveMind.Core.ValueObject;
using Microsoft.Extensions.Logging;

namespace HiveMind.Core.Simulation
{
  /// <summary>
  /// Manages and schedules asynchronous tasks for bee activities in the simulation.
  /// Optimizes performance by batching similar activities and managing worker threads.
  /// </summary>
  public sealed class SimulationTaskScheduler
  {
    private readonly ILogger<SimulationTaskScheduler> _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConcurrency;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationTaskScheduler"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent tasks.</param>
    public SimulationTaskScheduler(
      ILogger<SimulationTaskScheduler> logger,
      int maxConcurrency = Entities.Environment.ProcessorCount
    )
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _maxConcurrency = Math.Max(1, maxConcurrency);
      _semaphore = new(_maxConcurrency, _maxConcurrency);
    }

    /// <summary>
    /// Schedules bee activities to be processed asynchronously.
    /// </summary>
    /// <param name="bees">The bees to process.</param>
    /// <param name="environment">The current environment.</param>
    /// <returns>A task representing the completion of all bee activities.</returns>
    public async Task ScheduleBeeActivitiesAsync(IEnumerable<Bee> bees, Entities.Environment environment)
    {
      ArgumentNullException.ThrowIfNull(bees);
      ArgumentNullException.ThrowIfNull(environment);

      List<Bee> beeList = [.. bees.Where(b => b.IsAlive)];
      if (beeList.Count == 0) return;

      // Group bees by type for batch processing
      List<IGrouping<BeeType, Bee>> beeGroups = [.. beeList.GroupBy(b => b.BeeType)];

      Task[] tasks = [.. beeGroups.Select(group => ProcessBeeGroup(group.Key, [.. group], environment))];

      try
      {
        await Task.WhenAll(tasks);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing bee activities");
        throw;
      }
    }

    /// <summary>
    /// Processes a group of bees of the same type for optimal performance.
    /// </summary>
    /// <param name="beeType">The type of bees being processed.</param>
    /// <param name="bees">The bees to process.</param>
    /// <param name="environment">The current environment.</param>
    /// <returns>A task representing the processing completion.</returns>
    private async Task ProcessBeeGroup(BeeType beeType, List<Bee> bees, Entities.Environment environment)
    {
      await _semaphore.WaitAsync();

      try
      {
        await Task.Run(() =>
        {
          foreach (Bee bee in bees)
            try
            {
              bee.PerformActivity(environment);
            }
            catch (Exception ex)
            {
              _logger.LogError(ex, "Error processing {BeeType} bee {BeeId}", beeType, bee.Id);
            }
        });
      }
      finally
      {
        _semaphore.Release();
      }
    }

    /// <summary>
    /// Schedules pathfinding calculations for moving bees.
    /// </summary>
    /// <param name="movingBees">Bees that need to move.</param>
    /// <param name="destinations">Target destinations for each bee.</param>
    /// <returns>A task representing the pathfinding completion.</returns>
    public async Task SchedulePathfindingAsync(IList<Bee> movingBees, IList<Position3D> destinations)
    {
      ArgumentNullException.ThrowIfNull(movingBees);
      ArgumentNullException.ThrowIfNull(destinations);

      if (movingBees.Count != destinations.Count)
        throw new ArgumentException("Number of bees must match number of destinations");

      Task[] pathfindingTasks =
        [.. movingBees.Zip(destinations, (bee, destination) => CalculatePathAsync(bee, destination))];

      await Task.WhenAll(pathfindingTasks);
    }

    /// <summary>
    /// Calculates the optimal path for a bee to reach its destination.
    /// </summary>
    /// <param name="bee">The bee that needs to move.</param>
    /// <param name="destination">The target destination.</param>
    /// <returns>A task representing the path calculation.</returns>
    private async Task CalculatePathAsync(Bee bee, Position3D destination)
    {
      await _semaphore.WaitAsync();

      try
      {
        await Task.Run(() =>
        {
          if (!bee.CanFly) return;

          try
          {
            // Simple pathfinding - move directly toward destination
            // In a more complex simulation, this could include obstacle avoidance
            double distance = bee.Position.DistanceTo(destination);
            double maxMoveDistance = CalculateMaxMoveDistance(bee);

            Position3D newPosition = distance <= maxMoveDistance
                                     ? destination
                                     : bee.Position.MoveTowards(destination, maxMoveDistance);

            bee.MoveTo(newPosition);
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error calculating path for bee {BeeId}", bee.Id);
          }
        });
      }
      finally
      {
        _semaphore.Release();
      }
    }

    /// <summary>
    /// Calculates the maximum distance a bee can move based on its energy and type.
    /// </summary>
    /// <param name="bee">The bee to calculate movement for.</param>
    /// <returns>The maximum movement distance.</returns>
    private static double CalculateMaxMoveDistance(Bee bee)
    {
      double baseSpeed = bee.BeeType switch
      {
        BeeType.Worker => 5.0, // Workers are fast and agile
        BeeType.Drone => 3.0,  // Drones are larger but slower
        BeeType.Queen => 1.0,  // Queens rarely move and are slow
        _ => 2.0
      };

      // Energy affects movement speed
      double energyModifier = Math.Max(0.1, bee.Energy);

      return baseSpeed * energyModifier;
    }

    /// <summary>
    /// Disposes the task scheduler and releases resources.
    /// </summary>
    public void Dispose() => _semaphore?.Dispose();
  }
}
