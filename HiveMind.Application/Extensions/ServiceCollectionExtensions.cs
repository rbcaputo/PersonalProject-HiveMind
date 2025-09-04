using HiveMind.Application.Interfaces;
using HiveMind.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HiveMind.Application.Extensions
{
  /// <summary>
  /// Dependency injection extensions for the application layer
  /// </summary>
  public static class ServiceCollectionExtensions
  {
    /// <summary>
    /// Adds HiveMind application services to the DI container
    /// </summary>
    public static IServiceCollection AddHiveMindApplication(this IServiceCollection services)
    {
      services.AddSingleton<ISimulationEngine, SimulationEngine>();

      return services;
    }
  }
}
