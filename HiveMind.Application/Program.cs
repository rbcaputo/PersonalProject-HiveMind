using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HiveMind.Application
{
  /// <summary>
  /// Main entry point for the HiveMind application.
  /// Configures services, logging, and starts the simulation host.
  /// </summary>
  public static class Program
  {
    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code indicating success (0) or failure (non-zero).</returns>
    public static async Task<int> Main(string[] args)
    {
      try
      {
        IHost host = CreateHostBuilder(args).Build();

        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Starting HiveMind simulation...");

        await host.RunAsync();

        logger.LogInformation("HiveMind simulation completed successfully.");
        return 0;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Fatal error: {ex.Message}");
        return 1;
      }
    }

    /// <summary>
    /// Creates and configures the host builder with all necessary services.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Configured host builder.</returns>
    private static IHostBuilder CreateHostBuilder(string[] args) =>
      Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
          services.AddHiveMindServices(context.Configuration);
        });
  }
}
