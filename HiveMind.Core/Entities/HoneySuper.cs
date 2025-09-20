using HiveMind.Core.Common;
using HiveMind.Core.ValueObject;

namespace HiveMind.Core.Entities
{
  /// <summary>
  /// Represents a honey super - a specialized storage area for excess honey production.
  /// Honey supers are added above the brood chambers when honey flow is abundant.
  /// </summary>
  public sealed class HoneySuper : Entity
  {
    private readonly List<Chamber> _chambers;
    private Position3D _position;
    private bool _isInstalled;
    private DateTime _installationDate;

    /// <summary>
    /// Gets the position of the honey super within the hive structure.
    /// </summary>
    public Position3D Position => _position;

    /// <summary>
    /// Gets a value indicating whether the honey super is currently installed.
    /// </summary>
    public bool IsInstalled => _isInstalled;

    /// <summary>
    /// Gets the date when the honey super was installed.
    /// </summary>
    public DateTime InstallationDate => _installationDate;

    /// <summary>
    /// Gets the chambers that make up this honey super.
    /// </summary>
    public IReadOnlyList<Chamber> Chambers => _chambers.AsReadOnly();

    /// <summary>
    /// Gets the total honey storage capacity of this super.
    /// </summary>
    public double TotalCapacity => _chambers.Sum(c => c.GetHoneyCapacity());

    /// <summary>
    /// Gets the current amount of honey stored in this super.
    /// </summary>
    public double CurrentHoneyAmount => _chambers.Sum(c => c.GetCurrentHoneyAmount());

    /// <summary>
    /// Gets the percentage of capacity currently filled with honey.
    /// </summary>
    public double FillPercentage => TotalCapacity > 0 ? (CurrentHoneyAmount / TotalCapacity) * 100 : 0;

    /// <summary>
    /// Gets a value indicating whether the super is ready for harvest.
    /// Typically ready when 80% or more is filled and capped.
    /// </summary>
    public bool IsReadyForHarvest => FillPercentage >= 80.0 && GetCappedHoneyPercentage() >= 80.0;

    /// <summary>
    /// Standard number of chambers in a honey super.
    /// </summary>
    public const int StandardChamberCount = 8;

    /// <summary>
    /// Initializes a new instance of the <see cref="HoneySuper"/> class.
    /// </summary>
    /// <param name="position">The position of the honey super.</param>
    /// <param name="chamberCount">The number of chambers in this super.</param>
    public HoneySuper(Position3D position, int chamberCount = StandardChamberCount)
    {
      _position = position ?? throw new ArgumentNullException(nameof(position));
      _chambers = new(chamberCount);
      _isInstalled = false;
      _installationDate = default;

      // Initialize chambers positioned within the super
      for (int i = 0; i < chamberCount; i++)
      {
        Position3D chamberPosition = position.Move(i * 10.0, 0, 0); // Chambers spaced apart
        _chambers.Add(new Chamber(chamberPosition, 150)); // More cells for honey storage
      }
    }

    /// <summary>
    /// Installs the honey super, making it available for honey storage.
    /// </summary>
    public void Install()
    {
      if (_isInstalled) return;

      _isInstalled = true;
      _installationDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes the honey super, typically for harvest or end of season.
    /// </summary>
    public void Remove() => _isInstalled = false;

    /// <summary>
    /// Attempts to store honey in the super.
    /// </summary>
    /// <param name="amount">Amount of honey to store.</param>
    /// <returns>Actual amount stored (limited by available capacity).</returns>
    public double StoreHoney(double amount)
    {
      if (!_isInstalled || amount <= 0) return 0;

      double totalStored = 0.0;
      double remainingAmount = amount;

      foreach (Chamber chamber in _chambers)
      {
        if (remainingAmount <= 0) break;

        List<Cell> honeyCells = [.. chamber.GetHoneyStorageCells()];
        foreach (Cell cell in honeyCells)
        {
          if (remainingAmount <= 0) break;

          double cellCapacity = Cell.MaxHoneyPerCell - cell.HoneyAmount;
          double amountToStore = Math.Min(remainingAmount, cellCapacity);

          if (amountToStore > 0)
          {
            double actualStored = cell.AddHoney(amountToStore);
            totalStored += actualStored;
            remainingAmount -= actualStored;
          }
        }
      }

      return totalStored;
    }

    /// <summary>
    /// Harvests honey from the super, removing a specified amount.
    /// </summary>
    /// <param name="amount">Amount of honey to harvest.</param>
    /// <returns>Actual amount harvested.</returns>
    public double HarvestHoney(double amount)
    {
      if (!_isInstalled || amount <= 0) return 0;

      double totalHarvested = 0.0;
      double remainingAmount = amount;

      foreach (var chamber in _chambers)
      {
        if (remainingAmount <= 0) break;

        List<Cell> honeyCells = [.. chamber.Cells.Where(c => c.ContentsType == CellContentsType.Honey)];
        foreach (Cell cell in honeyCells)
        {
          if (remainingAmount <= 0) break;

          double amountToHarvest = Math.Min(remainingAmount, cell.HoneyAmount);
          if (amountToHarvest > 0)
          {
            double actualHarvested = cell.RemoveHoney(amountToHarvest);
            totalHarvested += actualHarvested;
            remainingAmount -= actualHarvested;
          }
        }
      }

      return totalHarvested;
    }

    /// <summary>
    /// Gets the percentage of honey cells that are capped (sealed).
    /// </summary>
    /// <returns>Percentage of capped honey cells.</returns>
    public double GetCappedHoneyPercentage()
    {
      List<Cell> honeyCells =
        [.. _chambers.SelectMany(c => c.Cells).Where(cell => cell.ContentsType == CellContentsType.Honey)];

      if (honeyCells.Count == 0) return 0;

      int cappedCells = honeyCells.Count(cell => cell.IsCapped);
      return (double)cappedCells / honeyCells.Count * 100;
    }

    /// <summary>
    /// Gets statistics about the honey super's current state.
    /// </summary>
    /// <returns>Honey super statistics.</returns>
    public HoneySuperStats GetStats() => new()
    {
      TotalCapacity = TotalCapacity,
      CurrentHoneyAmount = CurrentHoneyAmount,
      FillPercentage = FillPercentage,
      CappedPercentage = GetCappedHoneyPercentage(),
      IsReadyForHarvest = IsReadyForHarvest,
      ChamberCount = _chambers.Count,
      InstallationDate = _installationDate,
      IsInstalled = _isInstalled
    };
  }

  /// <summary>
  /// Statistics for a honey super's current state.
  /// </summary>
  public sealed class HoneySuperStats
  {
    /// <summary>
    /// Gets or sets the total honey storage capacity.
    /// </summary>
    public double TotalCapacity { get; set; }

    /// <summary>
    /// Gets or sets the current amount of honey stored.
    /// </summary>
    public double CurrentHoneyAmount { get; set; }

    /// <summary>
    /// Gets or sets the percentage of capacity filled.
    /// </summary>
    public double FillPercentage { get; set; }

    /// <summary>
    /// Gets or sets the percentage of honey cells that are capped.
    /// </summary>
    public double CappedPercentage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the super is ready for harvest.
    /// </summary>
    public bool IsReadyForHarvest { get; set; }

    /// <summary>
    /// Gets or sets the number of chambers in the super.
    /// </summary>
    public int ChamberCount { get; set; }

    /// <summary>
    /// Gets or sets the installation date.
    /// </summary>
    public DateTime InstallationDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the super is installed.
    /// </summary>
    public bool IsInstalled { get; set; }
  }
}
