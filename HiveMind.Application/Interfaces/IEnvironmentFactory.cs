using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Application.Interfaces
{
  /// <summary>
  /// Factory for creating environment instances
  /// </summary>
  public interface IEnvironmentFactory
  {
    /// <summary>
    /// Creates a new simulation environment with the specified parameters
    /// </summary>
    IEnvironment CreateEnvironment(double width, double height, int initialFoodSources = 10, int? randomSeed = null);
  }
}
