namespace HiveMind.Infrastructure.Persistence
{
  /// <summary>
  /// Represents a snapshot of the simulation state for persistence/loading
  /// </summary>
  public class SimulationSnapshot
  {
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long TickNumber { get; set; }
    public string ConfigurationJson { get; set; } = string.Empty;
    public string EnvironmentStateJson { get; set; } = string.Empty;
    public string ColoniesStateJson { get; set; } = string.Empty;
    public string StatisticsJson { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public override string ToString() =>
      $"Snapshot {Id:D} - Tick {TickNumber} at {CreatedAt:yyyy-MM-dd HH:mm:ss}";
  }
}
