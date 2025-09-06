using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Infrastructure.Environment
{
  /// <summary>
  /// Implementation of a food source in the environment
  /// </summary>
  public class FoodSource(Position position, double initialAmount) : IFoodSource
  {
    public Guid Id { get; } = Guid.NewGuid();
    public Position Position { get; } = position;
    public double AvailableFood { get; private set; } = initialAmount;
    public bool IsExhausted => AvailableFood <= 0;
    public double InitialAmount { get; } = initialAmount;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public double Harvest(double amount)
    {
      if (IsExhausted || amount <= 0)
        return 0;

      double harvestedAmount = Math.Min(amount, AvailableFood);
      AvailableFood -= harvestedAmount;

      return harvestedAmount;
    }

    // Returns how much of the original food source remains(0.0 to 1.0)
    public double GetHarvestEfficiency() =>
      AvailableFood / InitialAmount;

    public override string ToString() =>
      $"FoodSource [{Id:D}] at {Position} - {AvailableFood:F1}/{InitialAmount:F1} food";
  }
}
