namespace HiveMind.Core.ValueObject
{
  /// <summary>
  /// Represents a position in 3D space with X, Y, and Z coordinates.
  /// Used for bee positioning and movement within the simulated environment.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="Position3D"/> class.
  /// </remarks>
  /// <param name="x">The X coordinate.</param>
  /// <param name="y">The Y coordinate.</param>
  /// <param name="z">The Z coordinate.</param>
  public sealed class Position3D(double x, double y, double z) : Common.ValueObject
  {
    /// <summary>
    /// Gets the X coordinate (horizontal position).
    /// </summary>
    public double X { get; } = x;

    /// <summary>
    /// Gets the Y coordinate (vertical position).
    /// </summary>
    public double Y { get; } = y;

    /// <summary>
    /// Gets the Z coordinate (depth position).
    /// </summary>
    public double Z { get; } = z;

    /// <summary>
    /// Gets the origin position (0, 0, 0).
    /// </summary>
    public static Position3D Origin => new(0, 0, 0);

    /// <summary>
    /// Calculates the Euclidean distance to another position.
    /// </summary>
    /// <param name="other">The other position.</param>
    /// <returns>The distance between the two positions.</returns>
    public double DistanceTo(Position3D other)
    {
      ArgumentNullException.ThrowIfNull(other);

      var dx = X - other.X;
      var dy = Y - other.Y;
      var dz = Z - other.Z;

      return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// Creates a new position by adding the specified offsets.
    /// </summary>
    /// <param name="deltaX">The X offset.</param>
    /// <param name="deltaY">The Y offset.</param>
    /// <param name="deltaZ">The Z offset.</param>
    /// <returns>A new position with the offsets applied.</returns>
    public Position3D Move(double deltaX, double deltaY, double deltaZ) =>
      new(X + deltaX, Y + deltaY, Z + deltaZ);

    /// <summary>
    /// Creates a new position by moving towards the target position by the specified distance.
    /// </summary>
    /// <param name="target">The target position.</param>
    /// <param name="distance">The distance to move.</param>
    /// <returns>A new position moved towards the target.</returns>
    public Position3D MoveTowards(Position3D target, double distance)
    {
      ArgumentNullException.ThrowIfNull(target);

      double currentDistance = DistanceTo(target);
      if (currentDistance <= distance || currentDistance == 0)
        return target;

      double ratio = distance / currentDistance;
      double deltaX = (target.X - X) * ratio;
      double deltaY = (target.Y - Y) * ratio;
      double deltaZ = (target.Z - Z) * ratio;

      return Move(deltaX, deltaY, deltaZ);
    }

    /// <summary>
    /// Gets the equality components for value object comparison.
    /// </summary>
    /// <returns>The components that define equality.</returns>
    protected override IEnumerable<object> GetEqualityComponents()
    {
      yield return X;
      yield return Y;
      yield return Z;
    }

    /// <summary>
    /// Returns a string representation of the position.
    /// </summary>
    /// <returns>A string in the format "(X, Y, Z)".</returns>
    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
  }
}
