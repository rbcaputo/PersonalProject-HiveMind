using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.ValueObjects;

namespace HiveMind.Core.Domain.Services
{
  // ----------------------------------------------------------------------------
  //  Manages the spatial distribution of pheromones across the environment
  //  Uses efficient spatial partitioning for high-performance pheromone queries
  //  Implements diffusion and decay modeling
  // ----------------------------------------------------------------------------

  public class PheromoneMap(
    double environmentWidth,
    double environmentHeight,
    double gridCellSize = 5.0,
    Random? random = null
  ) : IDisposable
  {
    private readonly Dictionary<GridCell, Dictionary<PheromoneType, List<PheromoneDeposit>>> _spatialGrid = [];
    private readonly double _gridCellSize = gridCellSize;
    private readonly int _gridWidth = (int)Math.Ceiling(environmentWidth / gridCellSize);
    private readonly int _gridHeight = (int)Math.Ceiling(environmentHeight / gridCellSize);
    private readonly double _environmentWidth = environmentWidth;
    private readonly double _environmentHeight = environmentHeight;
    private readonly Random _random = random ?? new();
    private bool _disposed = false;

    public DateTime LastUpdateTime { get; private set; } = DateTime.UtcNow;
    public long TotalDepositsProcessed { get; private set; } = 0;
    public int ActiveDeposits => _spatialGrid.Values
      .SelectMany(cell => cell.Values)
      .Sum(list => list.Count(d => d.IsActive));

    //  Deposits pheromone at a specific location with intelligent clustering
    public void DepositPheromone(
      Position position,
      PheromoneType type,
      double intensity,
      Guid depositorId,
      bool allowClustering = true
    )
    {
      ThrowIfDisposed();

      if (!IsValidPosition(position) || intensity <= 0)
        return;

      var deposit = new PheromoneDeposit(type, intensity, depositorId, DateTime.UtcNow, position);
      var gridCell = GetGridCell(position);

      EnsureCellExists(gridCell, type);

      if (allowClustering)
      {
        //  Try to find nearby deposit from same ant to reinforce instead of creating new
        var nearbyDeposit = FindNearbyDeposit(gridCell, type, depositorId, position, 2.0);
        if (nearbyDeposit != null)
        {
          var reinforced = nearbyDeposit.WithReinforcement(intensity * 0.5, depositorId);
          ReplaceDeposit(gridCell, type, nearbyDeposit, reinforced);

          return;
        }
      }

      _spatialGrid[gridCell][type].Add(deposit);
      TotalDepositsProcessed++;
    }

    //  Gets the total pheromone intensity at a position considering all nearby deposits
    public double GetPheromoneIntensity(
      Position position,
      PheromoneType type,
      Guid? excludeDepositor = null,
      double maxSearchRadius = 15.0
    )
    {
      ThrowIfDisposed();

      if (!IsValidPosition(position))
        return 0.0;

      var characteristics = PheromoneProperties.GetCharacteristics(type);
      var searchRadius = Math.Min(maxSearchRadius, characteristics.DiffusionRange);
      double totalIntensity = 0.0;

      var relevantCells = GetCellsWithinRadius(position, searchRadius);

      foreach (var cell in relevantCells)
      {
        if (!_spatialGrid.TryGetValue(cell, out var cellDeposits) ||
            !cellDeposits.TryGetValue(type, out var deposits))
          continue;

        foreach (var deposit in deposits.Where(d => d.IsActive))
        {
          //  Skip deposits from excluded depositor (prevents self-following)
          if (excludeDepositor.HasValue && deposit.DepositorId == excludeDepositor.Value)
            continue;

          totalIntensity += deposit.GetIntensityAtPosition(position);
        }
      }

      return Math.Min(totalIntensity, characteristics.MaxIntensity);
    }

    //  Calculates the pheromone gradient vector at a position for navigation
    public Vector2 GetPheromoneGradient(
      Position position,
      PheromoneType type,
      Guid? excludeDepositor = null,
      double sampleDistance = 1.0
    )
    {
      ThrowIfDisposed();

      if (!IsValidPosition(position))
        return Vector2.Zero;

      //  Sample pheromone intensity in multiple directions
      var gradientSum = Vector2.Zero;
      int sampleCount = 8;  //  8 directions for good resolution

      for (int i = 0; i < sampleCount; i++)
      {
        double angle = (2 * Math.PI * i) / sampleCount;
        Position samplePos = new(
          position.X + Math.Cos(angle) * sampleDistance,
          position.Y + Math.Sin(angle) * sampleDistance
        );

        if (IsValidPosition(samplePos))
        {
          double intensity = GetPheromoneIntensity(samplePos, type, excludeDepositor, sampleDistance * 2);
          Vector2 direction = new(Math.Cos(angle), Math.Sin(angle));
          gradientSum += direction * intensity;
        }
      }

      return gradientSum.Magnitude > 0.01
        ? gradientSum.Normalized
        : Vector2.Zero;
    }

    //  Finds the strongest pheromone trail within a radius for trail following
    public PheromoneTrailInfo? FindStrongestTrail(
      Position position,
      PheromoneType type,
      double searchRadius = 10.0,
      Guid? excludeDepositor = null
    )
    {
      ThrowIfDisposed();

      if (!IsValidPosition(position))
        return null;

      var relevantCells = GetCellsWithinRadius(position, searchRadius);
      PheromoneDeposit? strongestDeposit = null;
      double strongestIntensity = 0.0;

      foreach (var cell in relevantCells)
      {
        if (!_spatialGrid.TryGetValue(cell, out var cellDeposits) ||
            !cellDeposits.TryGetValue(type, out var deposits))
          continue;

        foreach (var deposit in deposits.Where(d => d.IsActive))
        {
          if (excludeDepositor.HasValue && deposit.DepositorId == excludeDepositor.Value)
            continue;

          double intensity = deposit.GetIntensityAtPosition(position);
          if (intensity > strongestIntensity)
          {
            strongestIntensity = intensity;
            strongestDeposit = deposit;
          }
        }
      }

      if (strongestDeposit == null)
        return null;

      var direction = strongestDeposit.GetGradientDirection(position);
      return new(
        strongestDeposit.Type,
        strongestIntensity,
        direction,
        strongestDeposit.Location,
        strongestDeposit.DepositorId
      );
    }

    //  Updates all pheromone deposits, applying decay and removing inactive ones
    public void UpdateDecay(
      double deltaTime,
      double temperature = 25.0,
      double humidity = 0.7,
      double windSpeed = 0.1
    )
    {
      ThrowIfDisposed();

      List<(GridCell cell, PheromoneType type, PheromoneDeposit deposit)> depositsToRemove = [];
      List<(GridCell cell, PheromoneType type, PheromoneDeposit old, PheromoneDeposit updated)> depositsToUpdate = [];

      foreach (var (cell, typeDeposits) in _spatialGrid.ToList())
        foreach (var (type, deposits) in typeDeposits.ToList())
          for (int i = 0; i < deposits.Count; i++)
          {
            var deposit = deposits[i];
            var decayedDeposit = deposit.WithDecay(deltaTime, temperature, humidity, windSpeed);

            if (!decayedDeposit.IsActive)
              depositsToRemove.Add((cell, type, deposit));
            else if (decayedDeposit.CurrentIntensity != deposit.CurrentIntensity)
              depositsToUpdate.Add((cell, type, deposit, decayedDeposit));
          }

      //  Apply updates
      foreach (var (cell, type, oldDeposit, newDeposit) in depositsToUpdate)
        ReplaceDeposit(cell, type, oldDeposit, newDeposit);

      //  Remove inactive deposits
      foreach (var (cell, type, deposit) in depositsToRemove)
      {
        _spatialGrid[cell][type].Remove(deposit);

        //  Clean up empty collections
        if (_spatialGrid[cell][type].Count == 0)
          _spatialGrid[cell].Remove(type);
        if (_spatialGrid[cell].Count == 0)
          _spatialGrid.Remove(cell);
      }

      LastUpdateTime = DateTime.UtcNow;
    }

    public IEnumerable<PheromoneVisualizationData> GetVisualizationData(PheromoneType? typeFilter = null)
    {
      ThrowIfDisposed();

      foreach (var (cell, typeDeposits) in _spatialGrid)
        foreach (var (type, deposits) in typeDeposits)
        {
          if (typeFilter.HasValue && type != typeFilter.Value)
            continue;

          foreach (var deposit in deposits.Where(d => d.IsActive))
            yield return new(
              deposit.Location,
              deposit.Type,
              deposit.CurrentIntensity,
              deposit.InitialIntensity,
              deposit.DiffusionRange,
              deposit.Age,
              deposit.DepositorId
            );
        }
    }

    //  Creates a pheromone heatmap for analysis and debugging
    public PheromoneHeatmap CreateHeatmap(
      PheromoneType type,
      int mapWidth = 100,
      int mapHeight = 100
    )
    {
      ThrowIfDisposed();

      double[,] heatmap = new double[mapWidth, mapHeight];
      double cellWidth = _environmentWidth / mapWidth;
      double cellHeight = _environmentHeight / mapHeight;

      for (int x = 0; x < mapWidth; x++)
        for (int y = 0; y < mapHeight; y++)
        {
          Position position = new(x * cellWidth + cellWidth / 2, y * cellHeight + cellHeight / 2);
          heatmap[x, y] = GetPheromoneIntensity(position, type);
        }

      return new(type, heatmap, cellWidth, cellHeight);
    }

    //  Clears all pheromone deposits (useful for testing or environmental disasters)
    public void ClearAllPheromones()
    {
      ThrowIfDisposed();
      _spatialGrid.Clear();
      LastUpdateTime = DateTime.UtcNow;
    }

    //  Clears pheromones in a specific area (e.g., after nest damage)
    public void ClearPheromonesInArea(Position center, double radius, PheromoneType? typeFilter = null)
    {
      ThrowIfDisposed();

      var affectedCells = GetCellsWithinRadius(center, radius);

      foreach (var cell in affectedCells)
      {
        if (!_spatialGrid.TryGetValue(cell, out var cellDeposits))
          continue;

        var typesToProcess = typeFilter.HasValue
          ? new[] { typeFilter.Value }
          : [.. cellDeposits.Keys];

        foreach (var type in typesToProcess)
        {
          if (!cellDeposits.TryGetValue(type, out var deposits))
            continue;

          deposits.RemoveAll(d => d.Location.DistanceTo(center) <= radius);

          if (deposits.Count == 0)
            cellDeposits.Remove(type);
        }

        if (cellDeposits.Count == 0)
          _spatialGrid.Remove(cell);
      }
    }

    // --------------------------------
    //  Private implementation methods
    // --------------------------------

    private GridCell GetGridCell(Position position)
    {
      var cellX = (int)Math.Max(0, Math.Min(_gridWidth - 1, position.X / _gridCellSize));
      var cellY = (int)Math.Max(0, Math.Min(_gridHeight - 1, position.Y / _gridCellSize));

      return new(cellX, cellY);
    }

    private bool IsValidPosition(Position position) =>
      position.IsValid &&
      position.X >= 0 && position.X <= _environmentWidth &&
      position.Y >= 0 && position.Y <= _environmentHeight;

    private void EnsureCellExists(GridCell cell, PheromoneType type)
    {
      if (!_spatialGrid.ContainsKey(cell))
        _spatialGrid[cell] = [];

      if (!_spatialGrid[cell].ContainsKey(type))
        _spatialGrid[cell][type] = [];
    }

    private PheromoneDeposit? FindNearbyDeposit(
      GridCell cell,
      PheromoneType type,
      Guid depositorId,
      Position position,
      double maxDistance
    )
    {
      if (!_spatialGrid.TryGetValue(cell, out var cellDeposits) ||
          !cellDeposits.TryGetValue(type, out var deposits))
        return null;

      return deposits
          .Where(d => d.DepositorId == depositorId &&
            d.IsActive &&
            d.Location.DistanceTo(position) <= maxDistance)
          .OrderBy(d => d.Location.DistanceTo(position))
          .FirstOrDefault();
    }

    private void ReplaceDeposit(
      GridCell cell,
      PheromoneType type,
      PheromoneDeposit oldDeposit,
      PheromoneDeposit newDeposit
    )
    {
      var deposits = _spatialGrid[cell][type];
      int index = deposits.IndexOf(oldDeposit);
      if (index >= 0)
        deposits[index] = newDeposit;
    }

    private List<GridCell> GetCellsWithinRadius(Position position, double radius)
    {
      var centerCell = GetGridCell(position);
      var cellRadius = (int)Math.Ceiling(radius / _gridCellSize);
      List<GridCell> cells = [];

      for (int dx = -cellRadius; dx <= cellRadius; dx++)
      {
        for (int dy = -cellRadius; dy <= cellRadius; dy++)
        {
          var cellX = centerCell.X + dx;
          var cellY = centerCell.Y + dy;
        }

        if (cellX >= 0 && cellX < _gridWidth && cellY >= 0 && cellY < _gridHeight)
        {
          var cell = new GridCell(cellX, cellY);
          var cellCenter = GetCellCenterPosition(cell);

          if (position.DistanceTo(cellCenter) <= radius + _gridCellSize * 0.5)
            cells.Add(cell);
        }
      }

      return cells;
    }

    private Position GetCellCenterPosition(GridCell cell)
    {
      var centerX = (cell.X + 0.5) * _gridCellSize;
      var centerY = (cell.Y + 0.5) * _gridCellSize;

      return new(centerX, centerY);
    }

    private void ThrowIfDisposed() =>
      ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
      if (!_disposed)
      {
        _spatialGrid.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
      }
    }
  }

  // ------------------------------------------------------------------
  //  Represents a cell in the spatial grid for pheromone partitioning
  // ------------------------------------------------------------------

  public readonly record struct GridCell(int X, int Y);

  // --------------------------------------------------------------
  //  Information about a pheromone trail for navigation decisions
  // --------------------------------------------------------------

  public record PheromoneTrailInfo(
    PheromoneType Type,
    double Intensity,
    Vector2 Direction,
    Position Source,
    Guid CreatedBy
  );

  // ----------------------------------------------------------
  //  Data structure for pheromone visualization and debugging
  // ----------------------------------------------------------

  public record PheromoneVisualizationData(
    Position Location,
    PheromoneType Type,
    double CurrentIntensity,
    double InitialIntensity,
    double DiffusionRange,
    double Age,
    Guid CreatedBy
  );

  // ------------------------------------------------
  //  2D heatmap of pheromone intensity for analysis
  // ------------------------------------------------

  public class PheromoneHeatmap
  {
    public PheromoneHeatmap(PheromoneType type, double[,] intensityMap, double cellWidth, double cellHeight)
    {
      Type = type;
      IntensityMap = intensityMap;
      CellWidth = cellWidth;
      CellHeight = cellHeight;
      Width = intensityMap.GetLength(0);
      Height = intensityMap.GetLength(1);

      CalculateStatistics();
    }

    public PheromoneType Type { get; }
    public double[,] IntensityMap { get; }
    public double CellWidth { get; }
    public double CellHeight { get; }
    public int Width { get; }
    public int Height { get; }

    //  Statistical properties
    public double MaxIntensity { get; private set; }
    public double MinIntensity { get; private set; }
    public double AverageIntensity { get; private set; }
    public int ActiveCells { get; private set; }

    private void CalculateStatistics()
    {
      double sum = 0;
      double min = double.MaxValue;
      double max = 0;
      int activeCells = 0;

      for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
          double intensity = IntensityMap[x, y];
          sum += intensity;

          if (intensity > 0)
          {
            activeCells++;
            min = Math.Min(min, intensity);
          }

          max = Math.Max(max, intensity);
        }

      MaxIntensity = max;
      MinIntensity = min == double.MaxValue
        ? 0
        : min;
      AverageIntensity = sum / (Width * Height);
      ActiveCells = activeCells;
    }

    public double GetIntensityAt(int x, int y) =>
      x >= 0 && x < Width && y >= 0 && y < Height
        ? IntensityMap[x, y]
        : 0;

    public override string ToString() =>
      $"{Type} heatmap: {Width}x{Height}, Max: {MaxIntensity:F2}, Avg: {AverageIntensity:F3}, Active: {ActiveCells}";
  }
}