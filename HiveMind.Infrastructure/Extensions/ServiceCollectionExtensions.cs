using HiveMind.Infrastructure.Configuration;
using HiveMind.Infrastructure.Data;
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

      // Add simulation logging
      services.AddSingleton<SimulationLoggerProvider>();
      services.AddLogging();

      // Add persistence services
      services.AddSingleton<ISimulationPersistence>(provider =>
      {
        SimulationSettings settings = provider.GetService<IOptions<SimulationSettings>>()?.Value ?? new SimulationSettings();
        ILogger<FileSystemPersistence>? logger = provider.GetService<ILogger<FileSystemPersistence>>();

        var snapshotsPath = !string.IsNullOrEmpty(settings.SnapshotsPath)
          ? settings.SnapshotsPath
          : Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData), "HiveMind", "Snapshots");

        return new FileSystemPersistence(snapshotsPath, logger);
      });

      // Add data exports services
      services.AddTransient<IDataExporter, SimulationDataExporter>();

      // Add performance monitoring
      services.AddSingleton<IPerformanceMonitor, SimulationPerformanceMonitor>();

      return services;
    }

    /// <summary>
    /// Adds HiveMind infrastructure with custom configuration
    /// </summary>
    public static IServiceCollection AddHiveMindInfrastructure(this IServiceCollection services, Action<SimulationSettings> configureSettings)
    {
      services.Configure(configureSettings);
      return services.AddHiveMindInfrastructure();
    }

    /// <summary>
    /// Adds HiveMind infrastructure with custom persistence path
    /// </summary>
    public static IServiceCollection AddHiveMindInfrastructure(this IServiceCollection services, string snapshotsPath)
    {
      services.Configure<SimulationSettings>(options =>
      {
        options.SnapshotsPath = snapshotsPath;
      });

      return services.AddHiveMindInfrastructure();
    }
  }
}
