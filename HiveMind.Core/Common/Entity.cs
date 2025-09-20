namespace HiveMind.Core.Common
{
  /// <summary>
  /// Base class for all entities in the domain model.
  /// Provides common functionality for unique identification and equality comparison.
  /// </summary>
  public abstract class Entity : IEquatable<Entity>
  {
    /// <summary>
    /// Gets the unique identifier for this entity.
    /// </summary>
    public Guid Id { get; protected set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Entity"/> class with a new unique identifier.
    /// </summary>
    protected Entity() => Id = Guid.NewGuid();

    /// <summary>
    /// Initializes a new instance of the <see cref="Entity"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the entity.</param>
    protected Entity(Guid id) => Id = id;

    /// <summary>
    /// Determines whether the specified entity is equal to the current entity.
    /// </summary>
    /// <param name="other">The entity to compare with the current entity.</param>
    /// <returns>true if the specified entity is equal to the current entity; otherwise, false.</returns>
    public bool Equals(Entity? other)
    {
      if (other is null) return false;
      if (ReferenceEquals(this, other)) return true;
      return Id.Equals(other.Id);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current entity.
    /// </summary>
    /// <param name="obj">The object to compare with the current entity.</param>
    /// <returns>true if the specified object is equal to the current entity; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    /// <summary>
    /// Returns the hash code for the current entity.
    /// </summary>
    /// <returns>A hash code for the current entity.</returns>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>
    /// Determines whether two entities are equal.
    /// </summary>
    /// <param name="left">The first entity to compare.</param>
    /// <param name="right">The second entity to compare.</param>
    /// <returns>true if the entities are equal; otherwise, false.</returns>
    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    /// <summary>
    /// Determines whether two entities are not equal.
    /// </summary>
    /// <param name="left">The first entity to compare.</param>
    /// <param name="right">The second entity to compare.</param>
    /// <returns>true if the entities are not equal; otherwise, false.</returns>
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
  }
}