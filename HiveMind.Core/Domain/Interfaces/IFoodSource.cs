using HiveMind.Core.Domain.Common;

namespace HiveMind.Core.Domain.Interfaces
{
  /// <summary>
  /// Represents a food source in the environment
  /// </summary>
  public interface IFoodSource
  {
    Guid Id { get; }
    Position Position { get; }
    double AvailableFood { get; }
    bool IsExhausted { get; }

    /// <summary>
    /// Attempts to harvest food from this source
    /// </summary>
    double Harvest(double amount);
  }
}
