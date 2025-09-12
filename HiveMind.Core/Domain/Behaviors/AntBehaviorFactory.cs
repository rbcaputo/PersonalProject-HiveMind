using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Entities;
using HiveMind.Core.Domain.Enums;
using HiveMind.Core.Domain.Interfaces;
using HiveMind.Core.Domain.ValueObjects;

namespace HiveMind.Core.Domain.Behaviors
{
  // ---------------------------------------------------
  //  Factory for creating ant behaviors based on caste
  // ---------------------------------------------------

  public static class AntBehaviorFactory
  {
    //  Creates an ant with appropriate physiology and behavior
    public static Ant CreateAnt(
      AntCaste caste,
      Position startPosition,
      IColony colony,
      long birthTick = 0,
      AntPhysiology? customPhysiology = null
    ) =>
      caste switch
      {
        AntCaste.Queen =>
          new QueenAnt(startPosition, colony, birthTick),
        AntCaste.Worker =>
          new WorkerAnt(startPosition, colony, birthTick),
        AntCaste.Soldier =>
          new SoldierAnt(startPosition, colony, birthTick),
        AntCaste.Forager =>
          new ForagerAnt(startPosition, colony, birthTick),
        AntCaste.Nurse =>
          new WorkerAnt(startPosition, colony, birthTick),  //  Nurses are specialized workers
        AntCaste.Builder =>
          new WorkerAnt(startPosition, colony, birthTick),  //  Builders are specialized workers
        _ =>
          throw new ArgumentException($"Unknown ant caste: {caste}")
      };

    //  Creates behavior configuration optimized for enhanced ants
    public static BehaviorConfiguration GetConfiguration(AntCaste caste) =>
      caste switch
      {
        AntCaste.Queen =>
          new()
          {
            RestThreshold = 0.4,   //  Queens rest more frequently
            RestAmount = 2.5,
            UpdateInterval = 120,  //  Slower updates for queens
            MovementRange = 5.0,   //  Limited movement
            TaskCompletionDistance = 2.0
          },
        AntCaste.Worker =>
          new()
          {
            RestThreshold = 0.25,
            RestAmount = 1.8,
            UpdateInterval = 180,
            MovementRange = 20.0,
            TaskCompletionDistance = 1.0
          },
        AntCaste.Forager =>
          new()
          {
            RestThreshold = 0.2,
            RestAmount = 1.2,
            UpdateInterval = 60,   //  Very active
            MovementRange = 35.0,  //  Wide ranging
            TaskCompletionDistance = 1.5
          },
        AntCaste.Soldier =>
          new()
          {
            RestThreshold = 0.3,
            RestAmount = 1.5,
            UpdateInterval = 120,
            MovementRange = 25.0,
            TaskCompletionDistance = 2.0
          },
        _ =>
          new()  //  Default configuration
      };
  }
}
