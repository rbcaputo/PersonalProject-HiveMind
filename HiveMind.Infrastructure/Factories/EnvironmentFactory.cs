using HiveMind.Application.Interfaces;
using HiveMind.Core.Domain.Interfaces;
using HiveMind.Infrastructure.Environment;

namespace HiveMind.Infrastructure.Factories
{
  /// <summary>
  /// Factory for creating environment instances
  /// </summary>
  public class EnvironmentFactory : IEnvironmentFactory
  {
    public IEnvironment CreateEnvironment(
      double width,
      double height,
      int initialFoodSources = 10,
      int? randomSeed = null
   ) =>
      new SimulationEnvironment(width, height, initialFoodSources, randomSeed);
  }
}
