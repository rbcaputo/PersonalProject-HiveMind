namespace HiveMind.Core.Common
{
  /// <summary>
  /// Base class for value objects in the domain model.
  /// Value objects are immutable objects that are defined by their attributes rather than identity.
  /// </summary>
  public abstract class ValueObject : IEquatable<ValueObject>
  {
    /// <summary>
    /// Gets the equality components that define the value of this object.
    /// </summary>
    /// <returns>An enumerable of objects that represent the equality components.</returns>
    protected abstract IEnumerable<object> GetEqualityComponents();

    /// <summary>
    /// Determines whether the specified value object is equal to the current value object.
    /// </summary>
    /// <param name="other">The value object to compare with the current value object.</param>
    /// <returns>true if the specified value object is equal to the current value object; otherwise, false.</returns>
    public bool Equals(ValueObject? other)
    {
      if (other is null || other.GetType() != GetType())
        return false;

      return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current value object.
    /// </summary>
    /// <param name="obj">The object to compare with the current value object.</param>
    /// <returns>true if the specified object is equal to the current value object; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

    /// <summary>
    /// Returns the hash code for the current value object.
    /// </summary>
    /// <returns>A hash code for the current value object.</returns>
    public override int GetHashCode() => GetEqualityComponents()
                                         .Select(x => x.GetHashCode())
                                         .Aggregate((x, y) => x ^ y);

    /// <summary>
    /// Determines whether two value objects are equal.
    /// </summary>
    /// <param name="left">The first value object to compare.</param>
    /// <param name="right">The second value object to compare.</param>
    /// <returns>true if the value objects are equal; otherwise, false.</returns>
    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);

    /// <summary>
    /// Determines whether two value objects are not equal.
    /// </summary>
    /// <param name="left">The first value object to compare.</param>
    /// <param name="right">The second value object to compare.</param>
    /// <returns>true if the value objects are not equal; otherwise, false.</returns>
    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
  }
}
