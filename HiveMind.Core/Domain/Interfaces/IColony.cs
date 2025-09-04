using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Enums;

namespace HiveMind.Core.Domain.Interfaces
{
  /// <summary>
  /// Interface for insect colonies
  /// </summary>
  public interface IColony
  {
    Guid Id { get; }
    InsectType ColonyType { get; }
    Position CenterPosition { get; }
    IReadOnlyCollection<IInsect> Members { get; }
    int Population { get; }
    double TotalFoodStored { get; }
    bool IsActive { get; }

    /// <summary>
    /// Adds a new member to the colony
    /// </summary>
    void AddMember(IInsect insect);

    /// <summary>
    /// Removes a member from the colony
    /// </summary>
    void RemoveMember(Guid insectId);

    /// <summary>
    /// Updated the entire colony for the current simulation tick
    /// </summary>
    void Update(ISimulationContext context);
  }
}
