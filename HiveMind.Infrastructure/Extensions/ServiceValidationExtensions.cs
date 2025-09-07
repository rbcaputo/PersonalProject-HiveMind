using HiveMind.Application.Interfaces;
using HiveMind.Application.Services;
using HiveMind.Infrastructure.Data;
using HiveMind.Infrastructure.Monitoring;
using HiveMind.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiveMind.Infrastructure.Extensions
{
  /// <summary>
  /// Extensions for validating service registration
  /// </summary>
  public static class ServiceValidationExtensions
  {
    /// <summary>
    /// Validates that all HiveMind services are properly registered and configured
    /// </summary>
    public static IServiceProvider ValidateHiveMindRegistration(this IServiceProvider services)
    {
      // Test critical service resolution
      try
      {
        ISimulationEngine simulationEngine = services.GetRequiredService<ISimulationEngine>();
        ISimulationPersistence persistence = services.GetRequiredService<ISimulationPersistence>();
        IDataExporter dataExporter = services.GetRequiredService<IDataExporter>();
        IPerformanceMonitor performanceMonitor = services.GetRequiredService<IPerformanceMonitor>();
        ILoggerFactory loggerFactory = services.GetRequiredService<ILoggerFactory>();

        // Verify services are not null
        if (simulationEngine == null)
          throw new InvalidOperationException("SimulationEngine is null");
        if (persistence == null)
          throw new InvalidOperationException("SimulationPersistence is null");
        if (dataExporter == null)
          throw new InvalidOperationException("DataExporter is null");
        if (performanceMonitor == null)
          throw new InvalidOperationException("PerformanceMonitor is null");
        if (loggerFactory == null)
          throw new InvalidOperationException("LoggerFactory is null");

        return services;
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException("HiveMind service registration validation failed", ex);
      }
    }

    /// <summary>
    /// Validates logger configuration for all HiveMind services
    /// </summary>
    public static IServiceProvider ValidateHiveMindLogging(this IServiceProvider services)
    {
      ILoggerFactory loggerFactory = services.GetRequiredService<ILoggerFactory>();

      // Test logger creation for all major services
      Type[] loggerTypes =
      [
        typeof(SimulationEngine),
        typeof(FileSystemPersistence),
        typeof(SimulationDataExporter),
        typeof(SimulationPerformanceMonitor)
      ];

      foreach (Type type in loggerTypes)
        if (loggerFactory.CreateLogger(type) == null)
          throw new InvalidOperationException($"Failed to create logger for {type.Name}");

      return services;
    }
  }
}
