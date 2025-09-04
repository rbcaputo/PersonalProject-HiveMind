namespace HiveMind.Core.Domain.Common
{
  /// <summary>
  /// Represents a 2D position in the simulation space
  /// </summary>
  public readonly struct Position(double x, double y)
  {
    public double X { get; } = x;
    public double Y { get; } = y;

    public double DistanceTo(Position other) =>
      Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));

    public Position MoveTo(double deltaX, double deltaY) =>
      new(X + deltaX, Y + deltaY);

    public override string ToString() =>
      $"({X:F2}, {Y:F2})";
  }
}
