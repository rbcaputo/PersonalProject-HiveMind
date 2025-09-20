using HiveMind.Core.Common;
using HiveMind.Core.ValueObject;

namespace HiveMind.Core.Entities
{
  /// <summary>
  /// Represents a chamber within the beehive containing hexagonal cells.
  /// Chambers serve different purposes: brood rearing, honey storage, or pollen storage.
  /// </summary>
  public sealed class Chamber : Entity
  {
    private readonly List<Cell> _cells;
    private Position3D _position;
    private double _temperature;

    /// <summary>
    /// Gets the position of the chamber within the hive.
    /// </summary>
    public Position3D Position => _position;

    /// <summary>
    /// Gets the current temperature within the chamber in Celsius.
    /// </summary>
    public double Temperature => _temperature;

    /// <summary>
    /// Gets the total number of cells in this chamber.
    /// </summary>
    public int TotalCells => _cells.Count;

    /// <summary>
    /// Gets the number of empty cells available for use.
    /// </summary>
    public int EmptyCells => _cells.Count(c => c.IsEmpty);

    /// <summary>
    /// Gets the number of cells containing eggs.
    /// </summary>
    public int EggCells => _cells.Count(c => c.ContentsType == CellContentsType.Egg);

    /// <summary>
    /// Gets the number of cells containing larvae.
    /// </summary>
    public int LarvaCells => _cells.Count(c => c.ContentsType == CellContentsType.Larva);

    /// <summary>
    /// Gets the number of cells containing pupae.
    /// </summary>
    public int PupaCells => _cells.Count(c => c.ContentsType == CellContentsType.Pupa);

    /// <summary>
    /// Gets the number of cells containing honey.
    /// </summary>
    public int HoneyCells => _cells.Count(c => c.ContentsType == CellContentsType.Honey);

    /// <summary>
    /// Gets the number of cells containing pollen.
    /// </summary>
    public int PollenCells => _cells.Count(c => c.ContentsType == CellContentsType.Pollen);

    /// <summary>
    /// Gets a read-only collection of all cells in this chamber.
    /// </summary>
    public IReadOnlyList<Cell> Cells => _cells.AsReadOnly();

    /// <summary>
    /// Initializes a new instance of the <see cref="Chamber"/> class.
    /// </summary>
    /// <param name="position">The position of the chamber within the hive.</param>
    /// <param name="cellCount">The number of cells to create in this chamber.</param>
    public Chamber(Position3D position, int cellCount = 100)
    {
      _position = position ?? throw new ArgumentNullException(nameof(position));
      _cells = new(cellCount);
      _temperature = 35.0; // Default brood temperature

      // Initialize cells
      for (int i = 0; i < cellCount; i++)
        _cells.Add(item: new());
    }

    /// <summary>
    /// Updates the chamber temperature. Bees regulate temperature for optimal brood development.
    /// </summary>
    /// <param name="targetTemperature">The target temperature to maintain.</param>
    public void UpdateTemperature(double targetTemperature)
    {
      // Simulate gradual temperature change
      double temperatureDifference = targetTemperature - _temperature;
      _temperature += temperatureDifference * 0.1; // 10% adjustment per update
    }

    /// <summary>
    /// Attempts to find and reserve an empty cell for egg laying.
    /// </summary>
    /// <returns>An empty cell if available; otherwise, null.</returns>
    public Cell? GetEmptyCell() => _cells.FirstOrDefault(c => c.IsEmpty);

    /// <summary>
    /// Gets all cells containing developing brood (eggs, larvae, pupae).
    /// </summary>
    /// <returns>Collection of cells with developing bees.</returns>
    public IEnumerable<Cell> GetBroodCells() => _cells.Where(c =>
      c.ContentsType == CellContentsType.Egg ||
      c.ContentsType == CellContentsType.Larva ||
      c.ContentsType == CellContentsType.Pupa
    );

    /// <summary>
    /// Gets all cells that can be used for honey storage.
    /// </summary>
    /// <returns>Collection of cells suitable for honey storage.</returns>
    public IEnumerable<Cell> GetHoneyStorageCells() => _cells.Where(c =>
      c.IsEmpty ||
      c.ContentsType == CellContentsType.Honey
    );

    /// <summary>
    /// Advances the development of all brood cells in the chamber.
    /// </summary>
    public void UpdateBroodDevelopment()
    {
      foreach (Cell cell in GetBroodCells())
        cell.UpdateDevelopment();
    }

    /// <summary>
    /// Calculates the total honey storage capacity of this chamber.
    /// </summary>
    /// <returns>Storage capacity in arbitrary honey units.</returns>
    public double GetHoneyCapacity() => _cells.Count(c =>
      c.IsEmpty ||
      c.ContentsType == CellContentsType.Honey
    ) * Cell.MaxHoneyPerCell;

    /// <summary>
    /// Gets the current honey stored in this chamber.
    /// </summary>
    /// <returns>Current honey amount in arbitrary units.</returns>
    public double GetCurrentHoneyAmount() =>
      _cells.Where(c => c.ContentsType == CellContentsType.Honey)
            .Sum(c => c.HoneyAmount);
  }

  /// <summary>
  /// Represents a single hexagonal cell within a chamber.
  /// Cells can contain eggs, larvae, pupae, honey, or pollen.
  /// </summary>
  public sealed class Cell
  {
    private CellContentsType _contentsType;
    private DateTime _contentAddedTime;
    private double _honeyAmount;
    private double _pollenAmount;

    /// <summary>
    /// Maximum honey amount a single cell can hold.
    /// </summary>
    public const double MaxHoneyPerCell = 1.0;

    /// <summary>
    /// Maximum pollen amount a single cell can hold.
    /// </summary>
    public const double MaxPollenPerCell = 0.8;

    /// <summary>
    /// Gets the type of contents in this cell.
    /// </summary>
    public CellContentsType ContentsType => _contentsType;

    /// <summary>
    /// Gets the time when the current contents were added.
    /// </summary>
    public DateTime ContentAddedTime => _contentAddedTime;

    /// <summary>
    /// Gets the amount of honey in this cell (0.0 to 1.0).
    /// </summary>
    public double HoneyAmount => _honeyAmount;

    /// <summary>
    /// Gets the amount of pollen in this cell (0.0 to 0.8).
    /// </summary>
    public double PollenAmount => _pollenAmount;

    /// <summary>
    /// Gets a value indicating whether this cell is empty.
    /// </summary>
    public bool IsEmpty => _contentsType == CellContentsType.Empty;

    /// <summary>
    /// Gets a value indicating whether this cell is capped (sealed with wax).
    /// </summary>
    public bool IsCapped =>
      _contentsType == CellContentsType.Pupa ||
      (_contentsType == CellContentsType.Honey && _honeyAmount >= MaxHoneyPerCell);

    /// <summary>
    /// Gets the age of the current contents.
    /// </summary>
    public TimeSpan ContentAge => DateTime.UtcNow - _contentAddedTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="Cell"/> class.
    /// </summary>
    public Cell()
    {
      _contentsType = CellContentsType.Empty;
      _contentAddedTime = DateTime.UtcNow;
      _honeyAmount = 0;
      _pollenAmount = 0;
    }

    /// <summary>
    /// Adds an egg to this cell.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when cell is not empty.</exception>
    public void AddEgg()
    {
      if (!IsEmpty)
        throw new InvalidOperationException("Cannot add egg to non-empty cell.");

      _contentsType = CellContentsType.Egg;
      _contentAddedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds honey to this cell.
    /// </summary>
    /// <param name="amount">Amount of honey to add.</param>
    /// <returns>Actual amount added (limited by cell capacity).</returns>
    public double AddHoney(double amount)
    {
      ArgumentOutOfRangeException.ThrowIfNegative(amount);

      if (_contentsType != CellContentsType.Empty && _contentsType != CellContentsType.Honey)
        return 0; // Cannot add honey to cells with other contents

      double spaceAvailable = MaxHoneyPerCell - _honeyAmount;
      double actualAmount = Math.Min(amount, spaceAvailable);

      _honeyAmount += actualAmount;

      if (_honeyAmount > 0)
      {
        _contentsType = CellContentsType.Honey;
        if (_contentAddedTime == default || _contentsType != CellContentsType.Honey)
          _contentAddedTime = DateTime.UtcNow;
      }

      return actualAmount;
    }

    /// <summary>
    /// Removes honey from this cell.
    /// </summary>
    /// <param name="amount">Amount of honey to remove.</param>
    /// <returns>Actual amount removed.</returns>
    public double RemoveHoney(double amount)
    {
      ArgumentOutOfRangeException.ThrowIfNegative(amount);

      double actualAmount = Math.Min(amount, _honeyAmount);
      _honeyAmount -= actualAmount;

      if (_honeyAmount <= 0)
      {
        _honeyAmount = 0;
        if (_pollenAmount <= 0)
          _contentsType = CellContentsType.Empty;
      }

      return actualAmount;
    }

    /// <summary>
    /// Updates the development of brood contents in this cell.
    /// </summary>
    public void UpdateDevelopment()
    {
      if (_contentsType == CellContentsType.Empty) return;

      double age = ContentAge.TotalDays;

      _contentsType = _contentsType switch
      {
        CellContentsType.Egg when age >= 3 => CellContentsType.Larva,
        CellContentsType.Larva when age >= 9 => CellContentsType.Pupa,
        CellContentsType.Pupa when age >= 21 => CellContentsType.Empty, // Bee has emerged
        _ => _contentsType
      };

      // Reset time when transitioning stages
      if (_contentsType != CellContentsType.Empty &&
          (age >= 3 && _contentsType == CellContentsType.Larva) ||
          (age >= 9 && _contentsType == CellContentsType.Pupa))
        _contentAddedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Empties the cell of all contents.
    /// </summary>
    public void Empty()
    {
      _contentsType = CellContentsType.Empty;
      _honeyAmount = 0;
      _pollenAmount = 0;
      _contentAddedTime = DateTime.UtcNow;
    }
  }

  /// <summary>
  /// Defines the types of contents that can be stored in a cell.
  /// </summary>
  public enum CellContentsType
  {
    /// <summary>
    /// Cell is empty and available for use.
    /// </summary>
    Empty = 0,

    /// <summary>
    /// Cell contains a bee egg (first 3 days).
    /// </summary>
    Egg = 1,

    /// <summary>
    /// Cell contains a developing larva (days 3-9).
    /// </summary>
    Larva = 2,

    /// <summary>
    /// Cell contains a pupa (days 9-21, cell is capped).
    /// </summary>
    Pupa = 3,

    /// <summary>
    /// Cell contains honey storage.
    /// </summary>
    Honey = 4,

    /// <summary>
    /// Cell contains pollen storage.
    /// </summary>
    Pollen = 5
  }
}
