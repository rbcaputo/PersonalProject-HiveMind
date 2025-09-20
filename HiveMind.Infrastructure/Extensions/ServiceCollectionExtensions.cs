using HiveMind.Core.Repositories;
using HiveMind.Infrastructure.Configuration;
using HiveMind.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HiveMind.Infrastructure.Extensions
{
  /// <summary>
  /// Extension methods for registering infrastructure services.
  /// </summary>
  public static class ServiceCollectionExtensions
  {
    /// <summary>
    /// Registers infrastructure layer services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
      // Register configuration
      services.Configure<PersistenceConfiguration>(
        configuration.GetSection("HiveMind:Persistence")
      );

      // Register persistence services based on configuration
      PersistenceConfiguration persistenceConfig = configuration.GetSection("HiveMind:Persistence").Get<PersistenceConfiguration>()
          ?? new();

      switch (persistenceConfig.Provider)
      {
        case PersistenceProvider.Json:
          services.AddSingleton<ISimulationRepository, JsonSimulationRepository>();
          break;
        case PersistenceProvider.Sqlite:
          services.AddSingleton<ISimulationRepository, SqliteSimulationRepository>();
          break;
        default:
          services.AddSingleton<ISimulationRepository, JsonSimulationRepository>();
          break;
      }

      // Register high-level persistence service
      services.AddSingleton<PersistenceService>();

      return services;
    }
  }
}
