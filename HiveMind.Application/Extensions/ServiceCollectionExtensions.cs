using HiveMind.Application.Interfaces;
using HiveMind.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
      // Register simulation engine
      services.AddSingleton<ISimulationEngine>(provider =>
      {
        IEnvironmentFactory environmentFactory = provider.GetRequiredService<IEnvironmentFactory>();
        ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        ILogger<SimulationEngine> logger = loggerFactory.CreateLogger<SimulationEngine>();

        return new SimulationEngine(environmentFactory, logger);
      });

      return services;
    }

    /// <summary>
    /// Adds HiveMind application services with validation
    /// </summary>
    public static IServiceCollection AddHiveMindApplicationWithValidation(this IServiceCollection services)
    {
      services.AddHiveMindApplication();

      // Add service validation
      services.AddSingleton(provider =>
      {
        // Validate that the simulation engine was properly registered
        var engine = provider.GetRequiredService<ISimulationEngine>() ?? throw new InvalidOperationException("SimulationEngine was not properly registered");

        return engine;
      });

      return services;
    }
  }
}
