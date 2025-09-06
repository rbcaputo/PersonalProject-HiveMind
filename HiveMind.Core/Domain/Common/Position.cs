namespace HiveMind.Core.Domain.Common
{
  /// <summary>
  /// Represents a 2D position in the simulation space
  /// </summary>
  public readonly struct Position(double x, double y)
  {
    public double X { get; } = x;
    public double Y { get; } = y;

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

    public override string ToString() => $"({X:F2}, {Y:F2})";
  }
}
