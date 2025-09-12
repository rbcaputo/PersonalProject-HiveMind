using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.ValueObjects;

namespace HiveMind.Core.Domain.Services
{
  // ==================================================================
  //  Manages the pheromone trail network across the environment
  //  Uses spatial partitioning for efficient trail lookup and updates
  // ==================================================================

  public class PheromoneMap(double environmentWidth, double environmentHeight, double gridCellSize = 5.0) : IDisposable
  {
    //  Spatial grid for efficient pheromone lookups
    private readonly Dictionary<GridCell, Dictionary<PheromoneType, List<PheromoneDeposit>>> _spatialGrid = [];
    private readonly double _gridCellSize = gridCellSize;
    private readonly int _gridWidth = (int)Math.Ceiling(environmentWidth / gridCellSize);
    private readonly int _gridHeight = (int)Math.Ceiling(environmentHeight / gridCellSize);
    private bool _disposed = false;

    public DateTime LastUpdateTime { get; private set; } = DateTime.UtcNow;
    public int TotalDeposits => _spatialGrid.Values.SelectMany(cell => cell.Values).Sum(list => list.Count);

    //  Deposits pheromone at a specific position
    public void DepositPheromone(
      Position position,
      PheromoneType type,
      double intensity,
      Guid depositorId
    )
    {
      var characteristics = PheromoneProperties.GetCharacteristics(type);
      PheromoneDeposit deposit = new(
        type,
        Math.Min(intensity, characteristics.MaxIntensity),
        depositorId,
        DateTime.UtcNow,
        characteristics.DecayRate
      );

      var gridCell = GetGridCell(position);
      EnsureCellExists(gridCell, type);

      //  Check if we can reinforce an existing deposit from the same ant
      var existingDeposit = _spatialGrid[gridCell][type]
        .FirstOrDefault(d => d.DepositorId == depositorId &&
          GetDistance(position, GetCellCenterPosition(gridCell)) < _gridCellSize * 0.5);
      if (existingDeposit != null)
        //  Reinforce existing deposit
        existingDeposit.Reinforce(intensity * 0.5);  //  Partial reinforcement to prevent saturation
      else
        //  Add new deposit
        _spatialGrid[gridCell][type].Add(deposit);
    }

    //  Gets the pheromone intensity at a specific position
    //  Considers diffusion from nearby deposits
    public double GetPheromoneIntensity(Position position, PheromoneType type, Guid? excludeDepositor = null)
    {
      var characteristics = PheromoneProperties.GetCharacteristics(type);
      var searchRadius = characteristics.DiffusionRange;
      double totalIntensity = 0.0;

      //  Get all grid cells within search radius
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

          var depositPosition = GetCellCenterPosition(cell);
          var distance = position.DistanceTo(depositPosition);
          if (distance <= searchRadius)
          {
            //  Apply distance-based intensity falloff
            var intensityAtPosition = CalculateIntensityWithFalloff(
              deposit.CurrentIntensity,
              distance,
              searchRadius
            );

            totalIntensity += intensityAtPosition;
          }
        }
      }

      return Math.Min(totalIntensity, characteristics.MaxIntensity);
    }

    //  Gets the direction of strongest pheromone gradient
    //  Ants follow pheromone gradients to navigate
    public Vector2 GetPheromoneGradient(Position position, PheromoneType type, Guid? excludeDepositor = null)
    {
      const double sampleDistance = 0.5;  //  Distance for gradient sampling

      //  Sample pheromone instensity in cardinal directions
      var intensityNorth = GetPheromoneIntensity(new(position.X, position.Y + sampleDistance), type, excludeDepositor);
      var intensitySouth = GetPheromoneIntensity(new(position.X, position.Y - sampleDistance), type, excludeDepositor);
      var intensityEast = GetPheromoneIntensity(new(position.X + sampleDistance, position.Y), type, excludeDepositor);
      var intensityWest = GetPheromoneIntensity(new(position.X - sampleDistance, position.Y), type, excludeDepositor);

      //  Calculate gradient vector
      var gradientX = (intensityEast - intensityWest) / (2 * sampleDistance);
      var gradientY = (intensityNorth - intensitySouth) / (2 * sampleDistance);

      return new(gradientX, gradientY);
    }

    //  Updates all pheromone deposits, applying decay and removing inactive deposits
    public void UpdateDecay(double deltaTime, double temperature = 20.0, double humidity = 0.7)
    {
      //  Environmental factors affect decay rates
      var temperatureModifier = CalculateTemperatureModifier(temperature);
      var humidityModifier = CalculateHumidityModifier(humidity);

      List<(GridCell cell, PheromoneType type, PheromoneDeposit deposit)> depositsToRemove = [];

      foreach (var (cell, typeDeposits) in _spatialGrid)
        foreach (var (type, deposits) in typeDeposits)
          foreach (var deposit in deposits)
          {
            deposit.ApplyDecay(deltaTime, temperatureModifier, humidityModifier);
            if (!deposit.IsActive)
              depositsToRemove.Add((cell, type, deposit));
          }

      //  Remove inactive deposits
      foreach (var (cell, type, deposit) in depositsToRemove)
      {
        _spatialGrid[cell][type].Remove(deposit);

        //  Clean up empty lists
        if (_spatialGrid[cell][type].Count == 0)
          _spatialGrid[cell].Remove(type);

        //  Clean up empty cells
        if (_spatialGrid[cell].Count == 0)
          _spatialGrid.Remove(cell);
      }

      LastUpdateTime = DateTime.UtcNow;
    }

    //  Gets all active pheromone trails of a specific type
    //  Useful for visualization and analysis
    public IEnumerable<(Position position, double intensity)> GetActiveTrails(PheromoneType type)
    {
      foreach (var (cell, typeDeposits) in _spatialGrid)
      {
        if (typeDeposits.TryGetValue(type, out var deposits))
          foreach (var deposit in deposits.Where(d => d.IsActive))
          {
            var position = GetCellCenterPosition(cell);

            yield return (position, deposit.CurrentIntensity);
          }
      }
    }

    //  Clears all pheromone deposits (useful for testing or environmental disasters)
    public void ClearAllPheromones()
    {
      _spatialGrid.Clear();
      LastUpdateTime = DateTime.UtcNow;
    }

    // ========================
    //  Private helper methods
    // ========================

    private GridCell GetGridCell(Position position)
    {
      var cellX = (int)(position.X / _gridCellSize);
      var cellY = (int)(position.Y / _gridCellSize);

      return new(
        Math.Max(0, Math.Min(_gridWidth - 1, cellX)),
        Math.Max(0, Math.Min(_gridHeight - 1, cellY))
      );
    }

    private Position GetCellCenterPosition(GridCell cell)
    {
      var centerX = (cell.X + 0.5) * _gridCellSize;
      var centerY = (cell.Y + 0.5) * _gridCellSize;

      return new(centerX, centerY);
    }

    private void EnsureCellExists(GridCell cell, PheromoneType type)
    {
      if (!_spatialGrid.ContainsKey(cell))
        _spatialGrid[cell] = [];
      if (!_spatialGrid[cell].ContainsKey(type))
        _spatialGrid[cell][type] = [];
    }

    private List<GridCell> GetCellsWithinRadius(Position position, double radius)
    {
      var centerCell = GetGridCell(position);
      var cellRedius = (int)Math.Ceiling(radius / _gridCellSize);
      List<GridCell> cells = [];

      for (int dx = -cellRedius; dx <= cellRedius; dx++)
        for (int dy = -cellRedius; dy <= cellRedius; dy++)
        {
          var cellX = centerCell.X + dx;
          var cellY = centerCell.Y + dy;

          if (cellX >= 0 && cellX < _gridWidth &&
              cellY >= 0 && cellY < _gridHeight)
          {
            var cell = new GridCell(cellX, cellY);
            var cellCenter = GetCellCenterPosition(cell);

            if (position.DistanceTo(cellCenter) <= radius + _gridCellSize * 0.5)
              cells.Add(cell);
          }
        }

      return cells;
    }

    private static double CalculateIntensityWithFalloff(double intensity, double distance, double maxRange)
    {
      if (distance >= maxRange)
        return 0.0;

      //  Exponential falloff - pheromones are strongest at source
      var falloffFactor = Math.Exp(-2.0 * distance / maxRange);

      return intensity * falloffFactor;
    }

    private static double CalculateTemperatureModifier(double temperature)
    {
      //  Higher temperatures increase evaporation rate
      const double referenceTemperature = 25.0;  //  °C
      const double temperatureSensitivity = 0.05;  //  5% change per degree

      return 1.0 + (temperature - referenceTemperature) * temperatureSensitivity;
    }

    private static double CalculateHumidityModifier(double humidity)
    {
      //  Higher humidity decreases evaporation rate
      const double referenceHumidity = 0.7;  //  70%
      const double humiditySensitivity = 0.3;  //  30% effect range

      return 1.0 - (humidity - referenceHumidity) * humiditySensitivity;
    }

    private static double GetDistance(Position pos1, Position pos2) =>
      pos1.DistanceTo(pos2);

    public void Dispose()
    {
      if (_disposed)
      {
        _spatialGrid.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
      }
    }
  }

  // =======================================
  //  Represents a cell in the spatial grid
  // =======================================

  public readonly record struct GridCell(int X, int Y);

  // ============================================
  //  Simple 2D vector for gradient calculations
  // ============================================

  public readonly record struct Vector2(double X, double Y)
  {
    public double Magnitude => Math.Sqrt(X * X + Y * Y);

    public Vector2 Normalized
    {
      get
      {
        var mag = Magnitude;

        return mag > 0 ? new(X / mag, Y / mag) : new(0, 0);
      }
    }

    //  Operators overload
    public static Vector2 operator +(Vector2 a, Vector2 b) =>
      new(a.X + b.X, a.Y + b.Y);

    public static Vector2 operator -(Vector2 a, Vector2 b) =>
      new(a.X - b.X, a.Y - b.Y);

    public static Vector2 operator *(Vector2 v, double scalar) =>
      new(v.X * scalar, v.Y * scalar);
  }
}
