namespace HiveMind.Application.Models
{
  /// <summary>
  /// Statistics about the current simulation state
  /// </summary>
  public class SimulationStatistics
  {
    public long CurrentTick { get; set; }
    public int TotalPopulation { get; set; }
    public int ActiveColonies { get; set; }
    public double TotalFoodStored { get; set; }
    public double AvgEnergyLevel { get; set; }
    public int DeathCount { get; set; }
    public int BirthCount { get; set; }
    public Dictionary<string, int> PopulationByRole { get; set; } = [];
    public double SimulationTimeElapsed { get; set; }

    public override string ToString()
    {
      return $"Tick: {CurrentTick}, Population: {TotalPopulation}, Colonies: {ActiveColonies}, Food: {TotalFoodStored:F1}";
    }
  }
}
