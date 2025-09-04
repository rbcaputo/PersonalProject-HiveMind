using HiveMind.Core.Domain.Common;

namespace HiveMind.Application.Models
{
  /// <summary>
  /// Configuration settings for the simulation
  /// </summary>
  public class SimulationConfiguration
  {
    /// <summary>
    /// Width of the simulation environment
    /// </summary>
    public double EnvironmentWidth { get; set; } = 200.0;

    /// <summary>
    /// Height of the simulation environment
    /// </summary>
    public double EnvironmentHeight { get; set; } = 200.0;

    /// <summary>
    /// Time step for each simulation tick in seconds
    /// </summary>
    public double DeltaTime { get; set; } = 0.1;

    /// <summary>
    /// Target ticks per second
    /// </summary>
    public int TargetTPS { get; set; } = 30;

    /// <summary>
    /// Initial colony position
    /// </summary>
    public Position ColonyPosition { get; set; } = new Position(100, 100);

    /// <summary>
    /// Maximum colony population
    /// </summary>
    public int MaxColonyPopulation { get; set; } = 500;

    /// <summary>
    /// Number of initial food sources
    /// </summary>
    public int InitialFoodSources { get; set; } = 10;

    /// <summary>
    /// Random seed for reproducible simulations
    /// </summary>
    public int? RandomSeed { get; set; }

    /// <summary>
    /// Maximum simulation ticks (0 = unlimited)
    /// </summary>
    public long MaxTicks { get; set; } = 0;
  }
}
