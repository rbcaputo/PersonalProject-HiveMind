using HiveMind.Application.Interfaces;
using HiveMind.Infrastructure.Configuration;
using HiveMind.Infrastructure.Data;
using HiveMind.Infrastructure.Factories;
using HiveMind.Infrastructure.Logging;
using HiveMind.Infrastructure.Monitoring;
using HiveMind.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Environment;

namespace HiveMind.Infrastructure.Extensions
{
  /// <summary>
  /// Dependency injection extensions for the infrastructure layer
  /// </summary>
  public static class ServiceCollectionExtensions
  {
    /// <summary>
    /// Adds HiveMind infrastructure services to the DI container
    /// </summary>
    public static IServiceCollection AddHiveMindInfrastructure(this IServiceCollection services, IConfiguration? configuration = null)
    {
      // Add configuration
      if (configuration != null)
        services.Configure<SimulationSettings>(configuration.GetSection(SimulationSettings.SectionName));
      else
        services.Configure<SimulationSettings>(options => { }); // Use defaults

      // Add logging infrastructure and ensure it's available
      services.AddLogging(builder =>
      {
        if (configuration != null)
          builder.AddConfiguration(configuration.GetSection("Logging"));

        builder.AddConsole();
        builder.AddDebug();
      });

      // Register environment factory
      services.AddSingleton<IEnvironmentFactory, EnvironmentFactory>();

      // Add simulation-specific logging
      services.AddSingleton<SimulationLoggerProvider>();

      // Add persistence services
      services.AddSingleton<ISimulationPersistence>(provider =>
      {
        IOptions<SimulationSettings>? settingOptions = provider.GetService<IOptions<SimulationSettings>>();
        SimulationSettings settings = settingOptions?.Value ?? new SimulationSettings();

        // Use GetRequiredService to ensure logger is available
        ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        ILogger<FileSystemPersistence> logger = loggerFactory.CreateLogger<FileSystemPersistence>();

        string snapshotsPath = !string.IsNullOrEmpty(settings.SnapshotsPath)
          ? settings.SnapshotsPath
          : Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData), "HiveMind", "Snapshots");

        return new FileSystemPersistence(logger, snapshotsPath);
      });

      // Add data export services
      services.AddSingleton<IPerformanceMonitor>(provider =>
      {
        ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        ILogger<SimulationPerformanceMonitor> logger = loggerFactory.CreateLogger<SimulationPerformanceMonitor>();

        return new SimulationPerformanceMonitor(logger);
      });

      return services;
    }

    /// <summary>
    /// Adds HiveMind infrastructure with custom persistence path
    /// </summary>
    public static IServiceCollection AddHiveMindInfrastructure(this IServiceCollection services, string snapshotsPath)
    {
      if (string.IsNullOrWhiteSpace(snapshotsPath))
        throw new ArgumentException("Snapshots path cannot be null or empty", nameof(snapshotsPath));

      services.Configure<SimulationSettings>(options =>
      {
        options.SnapshotsPath = snapshotsPath;
      });

      return services.AddHiveMindInfrastructure();
    }

    /// <summary>
    /// Validates that all required services are properly registered
    /// </summary>
    public static void ValidateHiveMindServices(this IServiceProvider serviceProvider)
    {
      Type[] requiredServices =
      [
        typeof(ISimulationPersistence),
        typeof(IDataExporter),
        typeof(IPerformanceMonitor),
        typeof(SimulationLoggerProvider),
        typeof(ILoggerFactory)
      ];

      List<Type> missingServices = [];
      foreach (Type serviceType in requiredServices)
      {
        try
        {
          object service = serviceProvider.GetRequiredService(serviceType);
          if (service == null)
            missingServices.Add(serviceType);
        }
        catch (InvalidOperationException)
        {
          missingServices.Add(serviceType);
        }
      }

      if (missingServices.Count > 0)
        throw new InvalidOperationException($"The following required services are not registered: {string.Join(", ", missingServices.Select(t => t.Name))}");
    }
  }
}
