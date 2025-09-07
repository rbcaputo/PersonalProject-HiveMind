using HiveMind.Core.Domain.Common;

namespace HiveMind.Core.Domain.Interfaces
{
  /// <summary>
  /// Represents the simulation environment
  /// </summary>
  public interface IEnvironment
  {
    /// <summary>
    /// Width of the simulation space
    /// </summary>
    double Width { get; }

    /// <summary>
    /// Height of the simulation space
    /// </summary>
    double Height { get; }

    /// <summary>
    /// Temperature at a given position
    /// </summary>
    double GetTemperature(Position position);

    /// <summary>
    /// Food availability at a given position
    /// </summary>
    double GetFoodAvailability(Position position);

    /// <summary>
    /// Checks if a position is valid for movement
    /// </summary>
    bool IsValidPosition(Position position);

    /// <summary>
    /// Gets all food sources in the environment
    /// </summary>
    IReadOnlyCollection<IFoodSource> GetFoodSources();

    /// <summary>
    /// Updates the environment for the current simulation tick
    /// </summary>
    void Update(ISimulationContext context);
  }
}
