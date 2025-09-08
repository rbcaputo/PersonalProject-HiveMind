using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Core.Domain.Behaviors
{
  /// <summary>
  /// Base class for task-oriented ant behaviors
  /// </summary>
  public abstract class TaskBasedBehavior : BaseBehavior
  {
    private BehaviorTask? _currentTask;
    private long _lastTaskUpdate = 0;

    /// <summary>
    /// How often this behavior should reassess tasks (in ticks)
    /// </summary>
    protected abstract int TaskUpdateInterval { get; }

    /// <summary>
    /// Default movement threshold for task completion
    /// </summary>
    protected virtual double TaskCompletionDistance => 1.0;

    public override void Update(Ant ant, ISimulationContext context)
    {
      SafeUpdate(ant, context, (a, ctx) =>
      {
        // Check if ant needs rest first
        if (ShouldRest(a))
        {
          HandleRestBehavior(a);

          return;
        }

        // Update or assign task
        if (ShouldUpdateTask(a, ctx))
        {
          AssignNewTask(a, ctx);
          _lastTaskUpdate = ctx.CurrentTick;
        }

        // Execute current task
        if (_currentTask != null)
          ExecuteTask(a, ctx);
        else
          // No task - default to idle
          SafeSetState(a, ActivityState.Idle);
      });
    }

    /// <summary>
    /// Determines if ant should rest based on energy levels
    /// </summary>
    protected virtual bool ShouldRest(Ant ant) =>
        ant.Energy < ant.MaxEnergy * GetRestThreshold();

    /// <summary>
    /// Gets the energy threshold below which the ant should rest
    /// </summary>
    protected abstract double GetRestThreshold();

    /// <summary>
    /// Gets the energy restoration amount when resting
    /// </summary>
    protected abstract double GetRestAmount();


    /// <summary>
    /// Handles rest behavior
    /// </summary>
    protected virtual void HandleRestBehavior(Ant ant)
    {
      SafeSetState(ant, ActivityState.Resting);
      SafeRestoreEnergy(ant, GetRestAmount());
      ClearCurrentTask(); // Clear task while resting
    }

    /// <summary>
    /// Determines if a new task should be assigned
    /// </summary>
    protected virtual bool ShouldUpdateTask(Ant ant, ISimulationContext context) =>
      context.CurrentTick - _lastTaskUpdate >= TaskUpdateInterval ||
      ant.CurrentState == ActivityState.Idle ||
      _currentTask == null || _currentTask.IsCompleted;

    /// <summary>
    /// Assigns a new task to the ant - implemented by subclasses
    /// </summary>
    protected abstract void AssignNewTask(Ant ant, ISimulationContext context);

    /// <summary>
    /// Executes the current task
    /// </summary>
    protected virtual void ExecuteTask(Ant ant, ISimulationContext context)
    {
      if (!ValidateInputs(ant, context) || _currentTask == null)
        return;

      try
      {
        // Check if task is still valid
        if (!_currentTask.IsValid(context))
        {
          ClearCurrentTask();

          return;
        }

        // Handle movement-based tasks
        if (_currentTask.HasTargetPosition)
          ExecuteMovementTask(ant, context);
        else
          // Execute stationary task
          ExecuteStationaryTask(ant, context);
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(ExecuteTask));
        ClearCurrentTask();
      }
    }

    /// <summary>
    /// Executes a movement-based task
    /// </summary>
    protected virtual void ExecuteMovementTask(Ant ant, ISimulationContext context)
    {
      if (_currentTask?.TargetPosition == null)
        return;

      double distance = SafeCalculateDistance(ant.Position, _currentTask.TargetPosition.Value);
      if (distance > TaskCompletionDistance && distance != double.MaxValue)
      {
        // Move towards target
        SafeSetState(ant, ActivityState.Moving);

        if (!SafeMoveTo(ant, _currentTask.TargetPosition.Value, context))
          // Movement failed - mark task as failed
          _currentTask.MarkAsFailed();
      }
      else
        // Arrived at target - complete task
        CompleteCurrentTask(ant, context);
    }

    /// <summary>
    /// Executes a stationary task (no movement required)
    /// </summary>
    protected virtual void ExecuteStationaryTask(Ant ant, ISimulationContext context) =>
      // Default implementation - just complete the task
      CompleteCurrentTask(ant, context);

    /// <summary>
    /// Completes the current task
    /// </summary>
    protected virtual void CompleteCurrentTask(Ant ant, ISimulationContext context)
    {
      if (_currentTask == null)
        return;

      try
      {
        // Set appropriate activity state
        SafeSetState(ant, _currentTask.ActivityState);

        // Consume energy for the task
        SafeConsumeEnergy(ant, _currentTask.EnergyCost);

        // Perform task-specific completion logic
        OnTaskCompleted(ant, context, _currentTask);

        // Mark task as completed
        _currentTask.MarkAsCompleted();
      }
      catch (Exception ex)
      {
        HandleBehaviorError(ant, ex, nameof(CompleteCurrentTask));
      }
      finally
      {
        // Clear completed task
        ClearCurrentTask();
      }
    }

    /// <summary>
    /// Called when a task is completed - override for task-specific logic
    /// </summary>
    protected virtual void OnTaskCompleted(Ant ant, ISimulationContext context, BehaviorTask task) =>
      // Default implementation - set to idle
      SafeSetState(ant, ActivityState.Idle);

    /// <summary>
    /// Creates a new task for the ant
    /// </summary>
    protected static BehaviorTask CreateTask(ActivityState activityState, double energyCost, Position? targetPosition = null) =>
      new()
      {
        ActivityState = activityState,
        EnergyCost = energyCost,
        TargetPosition = targetPosition,
        CreatedAt = DateTime.UtcNow
      };

    /// <summary>
    /// Sets the current task
    /// </summary>
    protected void SetCurrentTask(BehaviorTask task) =>
      _currentTask = task;

    /// <summary>
    /// Clears the current task
    /// </summary>
    protected void ClearCurrentTask() =>
      _currentTask = null;

    /// <summary>
    /// Gets the current task (readonly)
    /// </summary>
    protected BehaviorTask? CurrentTask => _currentTask;

    /// <summary>
    /// Represents a task that an ant can perform
    /// </summary>
    public class BehaviorTask
    {
      public ActivityState ActivityState { get; set; }
      public double EnergyCost { get; set; }
      public Position? TargetPosition { get; set; }
      public DateTime CreatedAt { get; set; }
      public bool IsCompleted { get; set; }
      public bool IsFailed { get; set; }

      /// <summary>
      /// Whether this task requires movement to a target position
      /// </summary>
      public bool HasTargetPosition => TargetPosition.HasValue;

      /// <summary>
      /// Whether this task is still valid to execute
      /// </summary>
      public virtual bool IsValid(ISimulationContext context)
      {
        // Basic validation - can be overridden for task-specific validation
        if (IsFailed || IsCompleted)
          return false;

        // Check if target position is still valid
        if (HasTargetPosition && TargetPosition.HasValue)
          return TargetPosition.Value.IsValid && context.Environment.IsValidPosition(TargetPosition.Value);

        return true;
      }

      public void MarkAsCompleted() =>
        IsCompleted = true;

      public void MarkAsFailed() =>
        IsFailed = true;
    }
  }
}
