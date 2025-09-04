namespace HiveMind.Core.Domain.Entities
{
  /// <summary>
  /// Base class for all domain entities
  /// </summary>
  public abstract class BaseEntity
  {
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime LastUpdatedAt { get; protected set; }

    protected BaseEntity()
    {
      Id = Guid.NewGuid();
      CreatedAt = DateTime.UtcNow;
      LastUpdatedAt = DateTime.UtcNow;
    }

    protected void UpdateTimestamp() =>
      LastUpdatedAt = DateTime.UtcNow;
  }
}
