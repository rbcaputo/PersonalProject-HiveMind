using HiveMind.Application.Configuration;
using HiveMind.Application.Services;
using HiveMind.Core.Monitoring;
using HiveMind.Core.Services;
using HiveMind.Core.Simulation;
using HiveMind.Core.Simulation.Pathfinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiveMind.Application.Extensions
{
  /// <summary>
  /// Extension methods for configuring dependency injection services for the HiveMind application.
  /// </summary>
  public static class ServiceCollectionExtensions
  {
    /// <summary>
    /// Registers all HiveMind-related services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHiveMindServices(this IServiceCollection services, IConfiguration configuration)
    {
      // Configure logging
      services.AddLogging(builder =>
      {
        builder.AddConsole();
        builder.AddDebug();
      });

      // Register configuration sections
      services.Configure<SimulationConfiguration>(
        configuration.GetSection("HiveMind:Simulation"));
      services.Configure<EnvironmentConfiguration>(
        configuration.GetSection("HiveMind:Environment"));
      services.Configure<MonitoringConfiguration>(
        configuration.GetSection("HiveMind:Monitoring"));

      // Register core services
      services.AddSingleton<ISimulationTimeService, SimulationTimeService>();
      services.AddSingleton<PathfindingService>();
      services.AddSingleton<SimulationTaskScheduler>();

      // Register monitoring services
      services.AddSingleton<MonitoringService>();
      services.AddSingleton<EventLogger>();
      services.AddSingleton<AlertManager>();
      services.AddSingleton<OutputGenerator>();

      // Register hosted services
      services.AddHostedService<SimulationHostedService>();
      services.AddHostedService<MonitoringHostedService>();

      return services;
    }
  }
}
