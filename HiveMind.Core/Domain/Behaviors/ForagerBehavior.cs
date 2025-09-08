using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Behavior for forager ants - focused on finding and collecting food
  /// </summary>
  public class ForagerBehavior : IAntBehavior
  {
    private IFoodSource? _targetFoodSource;
    private Position? _exploreTarget;
    private bool _returningToNest = false;
    private readonly double _forageRadius = 30.0;

    public void Update(Ant ant, ISimulationContext context)
    {
      try
      {
        if (!ValidateInputs(ant, context))
          return;

        // Check ant state before proceeding
        if (!ant.IsAlive || ant.CurrentState == ActivityState.Dead)
        {
          ResetBehaviorState();

          return;
        }

        // Check if ant needs rest
        if (ant.Energy < ant.MaxEnergy * 0.15)
        {
          SafeSetState(ant, ActivityState.Resting);
          SafeRestoreEnergy(ant, 1.0);

          return;
        }

        // Main behavior logic
        if (ant.CarriedFood > 0)
          ReturnToNest(ant, context);
        else if (_targetFoodSource != null && !_targetFoodSource.IsExhausted)
          CollectFoodFromSource(ant, context);
        else
          SearchForFood(ant, context);
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(Update)); // Log and reset to safe state
      }
    }

    private void ReturnToNest(Ant ant, ISimulationContext context)
    {
      try
      {
        if (!ValidateInputs(ant, context))
          return;

        _returningToNest = true;
        SafeSetState(ant, ActivityState.Moving);

        // Validate colony exists
        if (ant.Colony != null)
        {
          ResetBehaviorState();
          return;
        }

        Position nestPosition = ant.Colony.CenterPosition;
        double distanceToNest = SafeCalculateDistance(ant.Position, nestPosition);
        if (distanceToNest > 2.0)
          SafeMoveTo(ant, nestPosition, context);
        else
        {
          // Arrived at nest - drop food and reset state
          double droppedFood = SafeDropFood(ant);
          if (droppedFood > 0)
            SafeAddFoodToColony(ant.Colony, droppedFood);

          ResetBehaviorState();
          SafeSetState(ant, ActivityState.Idle);
        }
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(ReturnToNest));
      }
    }

    private void CollectFoodFromSource(Ant ant, ISimulationContext context)
    {
      try
      {
        if (!ValidateInputs(ant, context) || _targetFoodSource == null)
          return;

        double distanceToFood = SafeCalculateDistance(ant.Position, _targetFoodSource.Position);
        if (distanceToFood > 1.0)
        {
          SafeSetState(ant, ActivityState.Moving);
          SafeMoveTo(ant, _targetFoodSource.Position, context);
        }
        else
        {
          // At food source - collect food
          SafeSetState(ant, ActivityState.Foraging);

          double harvestedAmount = SafeHarvestFood(_targetFoodSource, 5.0);
          if (harvestedAmount > 0)
            SafeCollectFood(ant, harvestedAmount);
        }

        SafeConsumeEnergy(ant, 0.3);

        if (_targetFoodSource.IsExhausted || ant.CarriedFood >= 10)
          _targetFoodSource = null;
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(CollectFoodFromSource));
        _targetFoodSource = null; // Reset target on error
      }
    }

    private void SearchForFood(Ant ant, ISimulationContext context)
    {
      try
      {
        if (!ValidateInputs(ant, context))
          return;

        // Safe food source enumeration
        IReadOnlyCollection<IFoodSource> foodSources = SafeGetFoodSources(context.Environment);
        List<(IFoodSource source, double distance)> nearbyFoodSources = [];
        foreach (IFoodSource fs in foodSources)
          if (!fs.IsExhausted)
          {
            double distance = SafeCalculateDistance(fs.Position, ant.Position);
            if (distance <= _forageRadius)
              nearbyFoodSources.Add((fs, distance));
          }

        _targetFoodSource = nearbyFoodSources.OrderBy(item => item.distance).FirstOrDefault().source;
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(SearchForFood));
      }
    }

    private void ExploreRandomly(Ant ant, ISimulationContext context)
    {
      try
      {
        if (!ValidateInputs(ant, context))
          return;

        if (_exploreTarget == null || SafeCalculateDistance(ant.Position, _exploreTarget.Value) < 2.0)
          // Generate safe exploration target within environment bounds
          _exploreTarget = GenerateSafeExplorationTarget(ant, context);

        if (_exploreTarget.HasValue)
        {
          SafeSetState(ant, ActivityState.Moving);
          SafeMoveTo(ant, _exploreTarget.Value, context);
          SafeConsumeEnergy(ant, 0.2);
        }
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(ExploreRandomly));
        _exploreTarget = null; // Reset exploration target on error
      }
    }

    /// <summary>
    /// Validates input parameters for behavior methods
    /// </summary>
    private static bool ValidateInputs(Ant ant, ISimulationContext context)
    {
      if (ant == null)
        return false;
      if (context == null)
        return false;
      if (context.Environment == null)
        return false;
      if (context.Random == null)
        return false;

      return true;
    }

    /// <summary>
    /// Generates a safe exploration target within environment bounds
    /// </summary>
    private Position? GenerateSafeExplorationTarget(Ant ant, ISimulationContext context)
    {
      try
      {
        Position nestPosition = ant.Colony?.CenterPosition ?? ant.Position;
        IEnvironment environment = context.Environment;

        // Try multiple times to generate a valid position
        for (int attempts = 0; attempts < 10; attempts++)
        {
          double angle = context.Random.NextDouble() * 2 * Math.PI;
          double distance = context.Random.NextDouble() * _forageRadius;

          double x = nestPosition.X + Math.Cos(angle) * distance;
          double y = nestPosition.Y + Math.Sin(angle) * distance;

          Position candidatePosition = new(x, y);

          // Validate position is within environment bounds
          if (environment.IsValidPosition(candidatePosition))
            return candidatePosition;
        }

        // Fallback - stay near current position
        return ant.Position;
      }
      catch
      {
        return ant.Position; // Safe fallback
      }
    }

    /// <summary>
    /// Safely calculates distance between two positions
    /// </summary>
    private static double SafeCalculateDistance(Position pos1, Position pos2)
    {
      try
      {
        return pos1.DistanceTo(pos2);
      }
      catch
      {
        return double.MaxValue; // Safe fallback - indicates positions are unreachable
      }
    }

    /// <summary>
    /// Safely moves ant to target position with bounds checking
    /// </summary>
    private static void SafeMoveTo(Ant ant, Position target, ISimulationContext context)
    {
      try
      {
        // Validate target position is within environment bounds
        if (context.Environment.IsValidPosition(target))
          ant.MoveTo(target);
        // If target is invalid, don't move
      }
      catch
      {
        // Silently fail - ant stays at current position
      }
    }

    /// <summary>
    /// Safely sets ant state with validation
    /// </summary>
    private static void SafeSetState(Ant ant, ActivityState newState)
    {
      try
      {
        if (ant.IsAlive && ant.CurrentState != ActivityState.Dead)
          ant.SetState(newState);
      }
      catch
      {
        // Silently fail - ant keeps current state
      }
    }

    /// <summary>
    /// Safely consumes energy with bounds checking
    /// </summary>
    private static void SafeConsumeEnergy(Ant ant, double amount)
    {
      try
      {
        if (ant.IsAlive && ant.CurrentState != ActivityState.Dead && amount >= 0 && amount <= ant.Energy + 10)
          ant.ConsumeEnergy(amount);
      }
      catch
      {
        // Silently fail - energy unchanged
      }
    }

    /// <summary>
    /// Safely restores energy with bounds checking
    /// </summary>
    private static void SafeRestoreEnergy(Ant ant, double amount)
    {
      try
      {
        if (ant.IsAlive && ant.CurrentState != ActivityState.Dead && amount >= 0 && amount <= ant.MaxHealth)
          ant.RestoreEnergy(amount);
      }
      catch
      {
        // Silently fail - energy unchanged
      }
    }

    /// <summary>
    /// Safely drops food from ant
    /// </summary>
    private static double SafeDropFood(Ant ant)
    {
      try
      {
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
    private static void SafeCollectFood(Ant ant, double amount)
    {
      try
      {
        if (ant.IsAlive && ant.CurrentState != ActivityState.Dead && amount >= 0 && amount <= 1000) // Reasonable bounds
          ant.CollectFood(amount);
      }
      catch
      {
        // Silently fail - food not collected
      }
    }

    /// <summary>
    /// Safely harvests food from source
    /// </summary>
    private static double SafeHarvestFood(IFoodSource source, double amount)
    {
      try
      {
        if (!source.IsExhausted && amount >= 0)
          return source.Harvest(amount);

        return 0;
      }
      catch
      {
        return 0; // No food harvested
      }
    }

    /// <summary>
    /// Safely adds food to colony
    /// </summary>
    private static void SafeAddFoodToColony(IColony colony, double amount)
    {
      try
      {
        if (amount > 0)
          colony.AddFood(amount);
      }
      catch
      {
        // Silently fail - food not added
      }
    }

    /// <summary>
    /// Safely gets food sources from environment
    /// </summary>
    private static IReadOnlyCollection<IFoodSource> SafeGetFoodSources(IEnvironment environment)
    {
      try
      {
        return environment.GetFoodSources();
      }
      catch
      {
        return []; // Empty collection
      }
    }

    /// <summary>
    /// Handles behavior errors gracefully
    /// </summary>
    private void HandleBehaviorError(Ant ant, Exception ex, string methodName)
    {
      try
      {
        // Reset behavior to safe state
        ResetBehaviorState();

        // Set ant to idle state if possible
        if (ant?.IsAlive == true)
          SafeSetState(ant, ActivityState.Idle);

        // Could log error here if logger was available
        // For now, just ensure the behavior doesn't crash the simulation
      }
      catch
      {
        // Ultimate fallback - do nothing to prevent cascading failures
      }
    }

    /// <summary>
    /// Resets behavior state to safe defaults
    /// </summary>
    private void ResetBehaviorState()
    {
      _returningToNest = false;
      _targetFoodSource = null;
      _exploreTarget = null;
    }
  }
}
