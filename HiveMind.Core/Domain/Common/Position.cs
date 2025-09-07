using System.Diagnostics.CodeAnalysis;

namespace HiveMind.Core.Domain.Common
{
  /// <summary>
  /// Represents a 2D position in the simulation space
  /// </summary>
  public readonly struct Position : IEquatable<Position>
  {
    public double X { get; }
    public double Y { get; }

    public Position(double x, double y)
    {
      // Validate inputs
      ValidateCoordinate(x, nameof(x));
      ValidateCoordinate(y, nameof(y));

      X = x;
      Y = y;
    }

    /// <summary>
    /// Creates a position with validation and clamping
    /// </summary>
    public static Position Create(
      double x,
      double y,
      double? minX = null,
      double? maxX = null,
      double? minY = null,
      double? maxY = null
    )
    {
      ValidateCoordinate(x, nameof(x));
      ValidateCoordinate(y, nameof(y));

      // Apply bounds if specified
      if (minX.HasValue)
        x = Math.Max(x, minX.Value);
      if (maxX.HasValue)
        x = Math.Min(x, maxX.Value);
      if (minY.HasValue)
        y = Math.Max(y, minY.Value);
      if (maxY.HasValue)
        y = Math.Min(y, maxY.Value);

      return new(x, y);
    }

    /// <summary>
    /// Validates that a coordinate value is finite and not NaN
    /// </summary>
    private static void ValidateCoordinate(double value, string paramName)
    {
      if (double.IsNaN(value))
        throw new ArgumentException($"Position coordinate cannot be NaN", paramName);
      if (double.IsInfinity(value))
        throw new ArgumentException($"Position coordinate cannot be infinite", paramName);
    }

    // Distance calculation
    public double DistanceTo(Position other)
    {
      double deltaX = X - other.X;
      double deltaY = Y - other.Y;

      return Math.Sqrt(deltaX * deltaX + deltaY * deltaY); // Faster than Math.Pow
    }

    public double SquaredDistanceTo(Position other)
    {
      double deltaX = X - other.X;
      double deltaY = Y - other.Y;

      return deltaX * deltaX + deltaY * deltaY; // Faster for comparisons
    }

    public Position MoveTo(double deltaX, double deltaY) => new(X + deltaX, Y + deltaY);

    /// <summary>
    /// Checks if this position is within the specified bounds
    /// </summary>
    public bool IsWithinBounds(double minX, double minY, double maxX, double maxY) =>
      X >= minX && X <= maxX && Y >= minY && Y <= maxY;

    /// <summary>
    /// Clamps position to the specified bounds
    /// </summary>
    public Position ClampToBounds(double minX, double minY, double maxX, double maxY)
    {
      if (minX >= maxX)
        throw new ArgumentException("minX must be less than maxX");
      if (minY >= maxY)
        throw new ArgumentException("minY must be less than maxY");

      double clampedX = Math.Max(minX, Math.Min(maxX, X));
      double clampedY = Math.Max(minY, Math.Min(maxY, Y));

      return new(clampedX, clampedY);
    }

    public bool Equals(Position other) =>
      Math.Abs(X - other.X) < 1e-10 && Math.Abs(Y - other.Y) < 1e-10;

    public override bool Equals([NotNullWhen(true)] object? obj) =>
      obj is Position other && Equals(other);

    public override int GetHashCode() =>
      HashCode.Combine(X, Y);

    public static bool operator ==(Position left, Position right) => left.Equals(right);
    public static bool operator !=(Position left, Position right) => !left.Equals(right);

    public override string ToString() =>
      $"({X:F2}, {Y:F2})";

    /// <summary>
    /// Origin position (0, 0)
    /// </summary>
    public static Position Origin =>
      new(0, 0);

    /// <summary>
    /// Checks if position has valid coordinates
    /// </summary>
    public bool IsValid =>
      !double.IsNaN(X) && !double.IsNaN(Y) && !double.IsInfinity(X) && !double.IsInfinity(Y);
  }
}
