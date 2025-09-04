using HiveMind.Core.Domain.Common;
using HiveMind.Core.Domain.Interfaces;

namespace HiveMind.Infrastructure.Environment
{
  /// <summary>
  /// Implementation of the simulation environment
  /// </summary>
  public class SimulationEnvironment : IEnvironment
  {
    private readonly List<IFoodSource> _foodSources;
    private readonly Random _random;
    private readonly double[,] _temperatureMap;
    private readonly double[,] _foodMap;
    private readonly int _mapResolution = 20; // Grid resolution for environmental maps

    public double Width { get; }
    public double Height { get; }

    public SimulationEnvironment(double width, double height, int initialFoodSources = 10, int? randomSeed = null)
    {
      Width = width;
      Height = height;
      _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
      _foodSources = [];

      // Initialize environmental maps
      _temperatureMap = new double[_mapResolution, _mapResolution];
      _foodMap = new double[_mapResolution, _mapResolution];

      InitializeEnvironmentalMaps();
      GenerateInitialFoodSources(initialFoodSources);
    }

    public double GetTemperature(Position position)
    {
      var gridX = Math.Min(_mapResolution - 1, Math.Max(0, (int)(position.X / Width * _mapResolution)));
      var gridY = Math.Min(_mapResolution - 1, Math.Max(0, (int)(position.Y / Height * _mapResolution)));

      return _temperatureMap[gridX, gridY];
    }

    public double GetFoodAvailability(Position position)
    {
      // Base environmental food availability
      var gridX = Math.Min(_mapResolution - 1, Math.Max(0, (int)(position.X / Width * _mapResolution)));
      var gridY = Math.Min(_mapResolution - 1, Math.Max(0, (int)(position.Y / Height * _mapResolution)));
      var baseFoodLevel = _foodMap[gridX, gridY];

      // Add food from nearby food sources
      var nearbyFoodSources = _foodSources
          .Where(fs => !fs.IsExhausted && fs.Position.DistanceTo(position) <= 5.0)
          .Sum(fs => fs.AvailableFood / (1 + fs.Position.DistanceTo(position)));

      return baseFoodLevel + nearbyFoodSources;
    }

    public bool IsValidPosition(Position position) =>
      position.X >= 0 && position.X <= Width &&
      position.Y >= 0 && position.Y <= Height;

    public IReadOnlyCollection<IFoodSource> GetFoodSources() =>
      _foodSources.Where(fs => !fs.IsExhausted).ToList().AsReadOnly();

    public void Update(ISimulationContext context)
    {
      // Remove exhausted food sources
      _foodSources.RemoveAll(fs => fs.IsExhausted);

      // Occasionally spawn new food sources
      if (context.CurrentTick % 500 == 0 && _foodSources.Count < 15)
        SpawnNewFoodSource();

      // Update environmental conditions (simplified)
      if (context.CurrentTick % 100 == 0)
        UpdateEnvironmentalConditions();
    }

    private void InitializeEnvironmentalMaps()
    {
      // Generate temperature map with some variation
      for (int x = 0; x < _mapResolution; x++)
        for (int y = 0; y < _mapResolution; y++)
        {
          // Base temperature with some noise
          var baseTemp = 20.0; // 20°C base temperature
          var noise = (_random.NextDouble() - 0.5) * 10.0; // +- 5°C variation
          _temperatureMap[x, y] = baseTemp + noise;

          // Base food availability (higher in certain areas)
          var centerX = _mapResolution / 2.0;
          var centerY = _mapResolution / 2.0;
          var distanceFromCenter = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
          var normalizedDistance = distanceFromCenter / (_mapResolution / 2.0);

          // Higher food availability away from center (simulating forest edges)
          _foodMap[x, y] = Math.Max(0.1, 1.0 - normalizedDistance + (_random.NextDouble() - 0.5) * 0.3);
        }
    }

    private void GenerateInitialFoodSources(int count)
    {
      for (int i = 0; i < count; i++)
        SpawnNewFoodSource();
    }

    private void SpawnNewFoodSource()
    {
      var position = new Position(_random.NextDouble() * Width, _random.NextDouble() * Height);

      var foodAmount = 20.0 + _random.NextDouble() * 80.0; // 20-100 units of food
      var foodSource = new FoodSource(position, foodAmount);
      _foodSources.Add(foodSource);
    }

    private void UpdateEnvironmentalConditions()
    {
      // Simulate gradual environmental changes
      // This could include seasonal changes, weather patterns, etc.
      // For now, just add some minor fluctuations

      for (int x = 0; x < _mapResolution; x++)
        for (int y = 0; y < _mapResolution; y++)
        {
          // Minor temperature fluctuations
          var tempChange = (_random.NextDouble() - 0.5) * 0.1;
          _temperatureMap[x, y] = Math.Max(0, Math.Min(40, _temperatureMap[x, y] + tempChange));

          // Food regeneration in some areas
          if (_random.NextDouble() < 0.01) // 1% chance per grid cell
            _foodMap[x, y] = Math.Min(2.0, _foodMap[x, y] + 0.1);
        }
    }
  }
}
