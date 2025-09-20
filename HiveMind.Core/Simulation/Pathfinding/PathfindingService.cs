using HiveMind.Core.ValueObject;
using Microsoft.Extensions.Logging;

namespace HiveMind.Core.Simulation.Pathfinding
{
  /// <summary>
  /// Service for calculating 3D paths for bee movement within the simulation space.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="PathfindingService"/> class.
  /// </remarks>
  /// <param name="logger">The logger instance.</param>
  public sealed class PathfindingService(ILogger<PathfindingService> logger)
  {
    private readonly ILogger<PathfindingService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly List<Obstacle> _obstacles = [];

    /// <summary>
    /// Calculates the optimal path from start to destination, avoiding obstacles.
    /// </summary>
    /// <param name="start">Starting position.</param>
    /// <param name="destination">Target destination.</param>
    /// <param name="maxDistance">Maximum distance that can be traveled.</param>
    /// <returns>The optimal next position to move toward the destination.</returns>
    public Position3D FindPath(Position3D start, Position3D destination, double maxDistance)
    {
      ArgumentNullException.ThrowIfNull(start);
      ArgumentNullException.ThrowIfNull(destination);
      ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxDistance, 0, "Max distance must be positive");

      double totalDistance = start.DistanceTo(destination);

      // If destination is within reach, move directly there
      if (totalDistance <= maxDistance) return destination;

      // Check for obstacles in direct path
      Position3D directPath = start.MoveTowards(destination, maxDistance);

      if (!HasObstacleInPath(start, directPath))
        return directPath;

      // Find alternative path around obstacles
      return FindAlternativePath(start, destination, maxDistance);
    }

    /// <summary>
    /// Adds an obstacle to the pathfinding system.
    /// </summary>
    /// <param name="obstacle">The obstacle to add.</param>
    public void AddObstacle(Obstacle obstacle)
    {
      ArgumentNullException.ThrowIfNull(obstacle);
      _obstacles.Add(obstacle);
    }

    /// <summary>
    /// Removes an obstacle from the pathfinding system.
    /// </summary>
    /// <param name="obstacle">The obstacle to remove.</param>
    /// <returns>True if the obstacle was removed; otherwise, false.</returns>
    public bool RemoveObstacle(Obstacle obstacle)
    {
      ArgumentNullException.ThrowIfNull(obstacle);
      return _obstacles.Remove(obstacle);
    }

    /// <summary>
    /// Checks if there are any obstacles in the path between two positions.
    /// </summary>
    /// <param name="start">Starting position.</param>
    /// <param name="end">Ending position.</param>
    /// <returns>True if there are obstacles in the path; otherwise, false.</returns>
    private bool HasObstacleInPath(Position3D start, Position3D end) =>
      _obstacles.Any(obstacle => obstacle.IntersectsPath(start, end));

    /// <summary>
    /// Finds an alternative path around obstacles.
    /// </summary>
    /// <param name="start">Starting position.</param>
    /// <param name="destination">Target destination.</param>
    /// <param name="maxDistance">Maximum travel distance.</param>
    /// <returns>Alternative position that avoids obstacles.</returns>
    private Position3D FindAlternativePath(Position3D start, Position3D destination, double maxDistance)
    {
      // Simple obstacle avoidance - try moving in different directions
      object[] directions =
      [
        new { X = 0.8, Y = 0.6, Z = 0.0 },  // Slight upward angle
        new { X = 0.8, Y = -0.6, Z = 0.0 }, // Slight downward angle
        new { X = 0.6, Y = 0.8, Z = 0.0 },  // More upward
        new { X = 0.6, Y = -0.8, Z = 0.0 }, // More downward
        new { X = 0.8, Y = 0.0, Z = 0.6 },  // Sideways up
        new { X = 0.8, Y = 0.0, Z = -0.6 }  // Sideways down
      ];

      foreach (object direction in directions)
      {
        double testDistance = maxDistance * 0.8; // Reduce distance for safety
        Position3D testPosition = start.Move(
          direction.X * testDistance,
          direction.Y * testDistance,
          direction.Z * testDistance
        );

        if (!HasObstacleInPath(start, testPosition))
          return testPosition;
      }

      // If all directions blocked, move minimally toward destination
      return start.MoveTowards(destination, maxDistance * 0.1);
    }
  }

  /// <summary>
  /// Represents an obstacle in the 3D space that bees must navigate around.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="Obstacle"/> class.
  /// </remarks>
  /// <param name="center">Center position of the obstacle.</param>
  /// <param name="radius">Radius of the obstacle.</param>
  /// <param name="height">Height of the obstacle.</param>
  public sealed class Obstacle(Position3D center, double radius, double height = 0)
  {
    /// <summary>
    /// Gets the center position of the obstacle.
    /// </summary>
    public Position3D Center { get; } = center ?? throw new ArgumentNullException(nameof(center));

    /// <summary>
    /// Gets the radius of the spherical obstacle.
    /// </summary>
    public double Radius { get; } = Math.Max(0, radius);

    /// <summary>
    /// Gets the height of the obstacle.
    /// </summary>
    public double Height { get; } = Math.Max(0, height);

    /// <summary>
    /// Determines if this obstacle intersects with a path between two positions.
    /// </summary>
    /// <param name="start">Path start position.</param>
    /// <param name="end">Path end position.</param>
    /// <returns>True if the obstacle intersects the path; otherwise, false.</returns>
    public bool IntersectsPath(Position3D start, Position3D end)
    {
      ArgumentNullException.ThrowIfNull(start);
      ArgumentNullException.ThrowIfNull(end);

      // Simple intersection test - check if path passes within obstacle radius
      double pathLength = start.DistanceTo(end);
      if (pathLength == 0) return false;

      // Vector from start to end
      double pathX = end.X - start.X;
      double pathY = end.Y - start.Y;
      double pathZ = end.Z - start.Z;

      // Vector from start to obstacle center
      double obstacleX = Center.X - start.X;
      double obstacleY = Center.Y - start.Y;
      double obstacleZ = Center.Z - start.Z;

      // Project obstacle center onto path line
      double projectionRatio = (pathX * obstacleX + pathY * obstacleY + pathZ * obstacleZ) / (pathLength * pathLength);
      projectionRatio = Math.Max(0, Math.Min(1, projectionRatio)); // Clamp to path segment

      // Find closest point on path to obstacle center
      double closestX = start.X + pathX * projectionRatio;
      double closestY = start.Y + pathY * projectionRatio;
      double closestZ = start.Z + pathZ * projectionRatio;

      Position3D closestPoint = new(closestX, closestY, closestZ);
      double distanceToClosest = Center.DistanceTo(closestPoint);

      return distanceToClosest <= Radius;
    }
  }
}
