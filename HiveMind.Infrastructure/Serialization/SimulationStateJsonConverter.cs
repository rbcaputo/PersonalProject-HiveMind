using HiveMind.Core.Entities;
using HiveMind.Core.Simulation;
using HiveMind.Core.ValueObject;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HiveMind.Infrastructure.Serialization
{
  /// <summary>
  /// Custom JSON converter for SimulationState to handle complex object serialization.
  /// </summary>
  public sealed class SimulationStateJsonConverter : JsonConverter<SimulationState>
  {
    /// <summary>
    /// Reads SimulationState from JSON.
    /// </summary>
    /// <param name="reader">JSON reader.</param>
    /// <param name="typeToConvert">Type to convert.</param>
    /// <param name="options">Serializer options.</param>
    /// <returns>Deserialized SimulationState.</returns>
    public override SimulationState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      using JsonDocument doc = JsonDocument.ParseValue(ref reader);
      JsonElement root = doc.RootElement;

      // Create environment first
      JsonElement environmentElement = root.GetProperty("environment");
      Core.Entities.Environment environment = JsonSerializer.Deserialize<Core.Entities.Environment>(environmentElement.GetRawText(), options)
        ?? throw new JsonException("Failed to deserialize environment");

      SimulationState state = new(environment);

      // Restore basic properties
      if (root.TryGetProperty("totalTicks", out var ticksElement))
      {
        long ticks = ticksElement.GetInt64();
        // Set ticks through reflection or internal method
        SetPrivateField(state, "_totalTicks", ticks);
      }

      if (root.TryGetProperty("status", out var statusElement))
      {
        SimulationStatus status = JsonSerializer.Deserialize<SimulationStatus>(statusElement.GetRawText(), options);
        state.SetStatus(status);
      }

      // Restore beehives
      if (root.TryGetProperty("beehives", out var hivesElement))
      {
        List<Beehive>? hives = JsonSerializer.Deserialize<List<Beehive>>(hivesElement.GetRawText(), options);
        if (hives != null)
          foreach (var hive in hives)
            state.AddBeehive(hive);
      }

      return state;
    }

    /// <summary>
    /// Writes SimulationState to JSON.
    /// </summary>
    /// <param name="writer">JSON writer.</param>
    /// <param name="value">SimulationState to serialize.</param>
    /// <param name="options">Serializer options.</param>
    public override void Write(Utf8JsonWriter writer, SimulationState value, JsonSerializerOptions options)
    {
      writer.WriteStartObject();

      writer.WritePropertyName("totalTicks");
      writer.WriteNumberValue(value.TotalTicks);

      writer.WritePropertyName("status");
      JsonSerializer.Serialize(writer, value.Status, options);

      writer.WritePropertyName("environment");
      JsonSerializer.Serialize(writer, value.Environment, options);

      writer.WritePropertyName("beehives");
      JsonSerializer.Serialize(writer, value.Beehives, options);

      writer.WritePropertyName("lastSaveTime");
      JsonSerializer.Serialize(writer, value.LastSaveTime, options);

      writer.WriteEndObject();
    }

    /// <summary>
    /// Sets a private field value using reflection.
    /// </summary>
    /// <param name="obj">Target object.</param>
    /// <param name="fieldName">Field name.</param>
    /// <param name="value">Value to set.</param>
    private static void SetPrivateField(object obj, string fieldName, object value)
    {
      FieldInfo? field = obj.GetType().GetField(
        fieldName,
        BindingFlags.NonPublic | BindingFlags.Instance
      );
      field?.SetValue(obj, value);
    }
  }

  /// <summary>
  /// Custom JSON converter for Position3D value objects.
  /// </summary>
  public sealed class Position3DJsonConverter : JsonConverter<Position3D>
  {
    public override Position3D Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      using JsonDocument doc = JsonDocument.ParseValue(ref reader);
      JsonElement root = doc.RootElement;

      var x = root.GetProperty("x").GetDouble();
      var y = root.GetProperty("y").GetDouble();
      var z = root.GetProperty("z").GetDouble();

      return new(x, y, z);
    }

    public override void Write(Utf8JsonWriter writer, Position3D value, JsonSerializerOptions options)
    {
      writer.WriteStartObject();
      writer.WriteNumber("x", value.X);
      writer.WriteNumber("y", value.Y);
      writer.WriteNumber("z", value.Z);
      writer.WriteEndObject();
    }
  }

  /// <summary>
  /// Custom JSON converter for Temperature value objects.
  /// </summary>
  public sealed class TemperatureJsonConverter : JsonConverter<Temperature>
  {
    public override Temperature Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      double celsius = reader.GetDouble();
      return new(celsius);
    }

    public override void Write(Utf8JsonWriter writer, Temperature value, JsonSerializerOptions options) =>
      writer.WriteNumberValue(value.Celsius);
  }

  /// <summary>
  /// Custom JSON converter for Humidity value objects.
  /// </summary>
  public sealed class HumidityJsonConverter : JsonConverter<Humidity>
  {
    public override Humidity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      double percentage = reader.GetDouble();
      return new(percentage);
    }

    public override void Write(Utf8JsonWriter writer, Humidity value, JsonSerializerOptions options) =>
      writer.WriteNumberValue(value.Percentage);
  }
}
