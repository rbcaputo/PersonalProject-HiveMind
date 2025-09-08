using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Base class for ant behaviors providing common error handling and safety methods
  /// </summary>
  public abstract class BaseBehavior : IAntBehavior
  {
    protected const double MAX_REASONABLE_ENERGY_CONSUMPTION = 10.0;
    protected const double MAX_REASONABLE_FOOD_AMOUNT = 1000.0;
    protected const int MAX_POSITION_GENERATION_ATTEMPTS = 10;

    public abstract void Update(Ant ant, ISimulationContext context);

    /// <summary>
    /// Validates input parameters for behavior methods
    /// </summary>
    protected static bool ValidateInputs(Ant ant, ISimulationContext context) =>
      ant.IsAlive && context.Environment != null && context.Random != null;

    /// <summary>
    /// Checks if ant is in a valid state for behavior operations
    /// </summary>
    protected static bool IsAntOperational(Ant ant) =>
      ant.IsAlive && ant.CurrentState != ActivityState.Dead && ant.Health > 0;

    /// <summary>
    /// Safely calculates distance between two positions
    /// </summary>
    protected static double SafeCalculateDistance(Position pos1, Position pos2)
    {
      try
      {
        if (!pos1.IsValid || pos2.IsValid)
          return double.MaxValue;

        return pos1.DistanceTo(pos2);
      }
      catch
      {
        return double.MaxValue; // Safe fallback - indicates unreachable
      }
    }

    /// <summary>
    /// Safely moves ant to target position with bounds checking
    /// </summary>
    protected static bool SafeMoveTo(Ant ant, Position target, ISimulationContext context)
    {
      try
      {
        if (!IsAntOperational(ant))
          return false;

        // Validate target position is within environment bounds
        if (!target.IsValid || !context.Environment.IsValidPosition(target))
          return false;

        ant.MoveTo(target);

        return true;
      }
      catch
      {
        return false; // Movement failed
      }
    }

    /// <summary>
    /// Safely sets ant state with validation
    /// </summary>
    protected static bool SafeSetState(Ant ant, ActivityState newState)
    {
      try
      {
        if (!IsAntOperational(ant))
          return false;

        ant.SetState(newState);

        return true;
      }
      catch
      {
        return false; // State change failed
      }
    }

    /// <summary>
    /// Safely consumes energy with bounds checking
    /// </summary>
    protected static bool SafeConsumeEnergy(Ant ant, double amount)
    {
      try
      {
        if (!IsAntOperational(ant))
          return false;

        if (amount < 0 || amount > MAX_REASONABLE_ENERGY_CONSUMPTION)
          return false;

        if (amount > ant.Energy + 1.0) // Allow slight overconsumption for realism
          amount = Math.Max(0, ant.Energy); // Consume all remaining energy

        ant.ConsumeEnergy(amount);

        return true;
      }
      catch
      {
        return false; // Energy consumption failed
      }
    }

    /// <summary>
    /// Safely restores energy with bounds checking
    /// </summary>
    protected static bool SafeRestoreEnergy(Ant ant, double amount)
    {
      try
      {
        if (!IsAntOperational(ant))
          return false;

        if (amount < 0 || amount > ant.MaxEnergy)
          return false;

        ant.RestoreEnergy(amount);

        return true;
      }
      catch
      {
        return false; // Energy restoration failed
      }
    }

    /// <summary>
    /// Safely drops food from ant
    /// </summary>
    protected static double SafeDropFood(Ant ant)
    {
      try
      {
        if (!IsAntOperational(ant))
          return 0;

        return ant.DropFood();
      }
      catch
      {
        return 0; // No food dropped
      }
    }

    /// <summary>
    /// Safely collects food for ant
    /// </summary>
    protected static bool SafeCollectFood(Ant ant, double amount)
    {
      try
      {
        if (!IsAntOperational(ant))
          return false;

        if (amount < 0 || amount > MAX_REASONABLE_FOOD_AMOUNT)
          return false;

        ant.CollectFood(amount);

        return true;
      }
      catch
      {
        return false; // Food collection failed
      }
    }

    /// <summary>
    /// Safely harvests food from source
    /// </summary>
    protected static double SafeHarvestFood(IFoodSource source, double amount)
    {
      try
      {
        if (source == null || source.IsExhausted)
          return 0;

        if (amount <= 0 || amount > MAX_REASONABLE_FOOD_AMOUNT)
          return 0;

        return source.Harvest(amount);
      }
      catch
      {
        return 0; // No food harvested
      }
    }

    /// <summary>
    /// Safely adds food to colony
    /// </summary>
    protected static bool SafeAddFoodToColony(IColony colony, double amount)
    {
      try
      {
        if (colony == null || amount <= 0)
          return false;

        colony.AddFood(amount);

        return true;
      }
      catch
      {
        return false; // Food addition failed
      }
    }

    /// <summary>
    /// Safely gets food sources from environment
    /// </summary>
    protected static IReadOnlyCollection<IFoodSource> SafeGetFoodSources(IEnvironment environment)
    {
      try
      {
        return environment.GetFoodSources() ?? [];
      }
      catch
      {
        return []; // Empty collection
      }
    }

    /// <summary>
    /// Generates a safe position within environment bounds
    /// </summary>
    protected static Position? GenerateSafePosition(Position center, double maxDistance, ISimulationContext context)
    {
      try
      {
        IEnvironment environment = context.Environment;
        Random random = context.Random;

        for (int attempts = 0; attempts < MAX_POSITION_GENERATION_ATTEMPTS; attempts++)
        {
          double angle = random.NextDouble() * 2 * Math.PI;
          double distance = random.NextDouble() * maxDistance;

          double x = center.X + Math.Cos(angle) * distance;
          double y = center.Y + Math.Sin(angle) * distance;

          Position candidatePosition = new(x, y);
          if (candidatePosition.IsValid && environment.IsValidPosition(candidatePosition))
            return candidatePosition;
        }

        // Fallback - return center position if no valid position found
        return environment.IsValidPosition(center) ? center : null;
      }
      catch
      {
        return null; // Position generation failed
      }
    }

    /// <summary>
    /// Gets the nest position safely
    /// </summary>
    protected static Position? GetSafeNestPosition(Ant ant)
    {
      try
      {
        return ant.Colony.CenterPosition;
      }
      catch
      {
        return null; // Nest position not available
      }
    }

    /// <summary>
    /// Handles behavior errors gracefully with error recovery
    /// </summary>
    protected static void HandleBehaviorError(Ant ant, Exception ex, string methodName)
    {
      try
      {
        // Set ant to safe idle state if possible
        if (ant.IsAlive && ant.CurrentState != ActivityState.Dead)
          SafeSetState(ant, ActivityState.Idle);

        // Log error information (would integrate with logging system)
        // Could implement error telemetry here
      }
      catch
      {
        // Ultimate fallback - do nothing to prevent cascading failures
      }
    }

    /// <summary>
    /// Performs safe behavior update with comprehensive error handling
    /// </summary>
    protected static void SafeUpdate(Ant ant, ISimulationContext context, Action<Ant, ISimulationContext> updateAction)
    {
      try
      {
        // Pre-update validation
        if (!ValidateInputs(ant, context) || !IsAntOperational(ant))
          return;

        // Execute the behavior-specific update logic
        updateAction(ant, context);
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(SafeUpdate));
      }
    }
  }
}
