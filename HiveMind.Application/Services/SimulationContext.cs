using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Application.Services
{
  /// <summary>
  /// Implementation of simulation context
  /// </summary>
  internal class SimulationContext(IEnvironment environment, double deltaTime, int? randomSeed = null) : ISimulationContext
  {
    private long _currentTick = 0;

    public long CurrentTick => _currentTick;
    public double DeltaTime { get; } = deltaTime;
    public IEnvironment Environment { get; } = environment ?? throw new ArgumentNullException(nameof(environment));
    public Random Random { get; } = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();

    public void UpdateTick(long newTick) =>
      _currentTick = newTick;
  }
}
