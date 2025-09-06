using HiveMind.Core.Domain.Common;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HiveMind.Infrastructure.Serialization
{
  /// <summary>
  /// JSON implementation of simulation serialization
  /// </summary>
  public class JsonSimulationSerializer : ISimulationSerializer
  {
    private readonly JsonSerializerOptions _options;

    public string FileExtension => ".json";
    public string MimeType => "application/json";

    public JsonSimulationSerializer()
    {
      _options = new()
      {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
          new JsonStringEnumConverter(), // Convert enums to strings
          new PositionJsonConverter(), // Custom converter for position struct
          new GuidJsonConverter() // Custom converter for Guid formatting
        },
        ReferenceHandler = ReferenceHandler.IgnoreCycles, // Handle circular references that might occur with colony/ant relationships
        MaxDepth = 64 // Prevent infinite recursion
      };
    }

    public string Serialize<T>(T obj)
    {
      if (obj == null) throw new ArgumentNullException(nameof(obj));

      try
      {
        return JsonSerializer.Serialize(obj, _options);
      }
      catch (JsonException ex)
      {
        throw new InvalidOperationException($"Failed to serialize object of type {typeof(T).Name}", ex);
      }
      catch (NotSupportedException ex)
      {
        throw new InvalidOperationException($"Serialization not supported for type {typeof(T).Name}", ex);
      }
    }

    public T? Deserialize<T>(string data)
    {
      if (string.IsNullOrWhiteSpace(data)) return default;

      try
      {
        return JsonSerializer.Deserialize<T>(data, _options);
      }
      catch (JsonException ex)
      {
        throw new InvalidOperationException($"Failed to deserialize JSON to type {typeof(T).Name}", ex);
      }
      catch (NotSupportedException ex)
      {
        throw new InvalidOperationException($"Deserialization not supported for type {typeof(T).Name}", ex);
      }
    }

    /// <summary>
    /// Creates a copy of the serializer with custom options
    /// </summary>
    public JsonSimulationSerializer WithCustomOptions(Action<JsonSerializerOptions> options)
    {
      JsonSerializerOptions customOptions = new(_options);
      options(customOptions);

      return new(customOptions);
    }

    private JsonSimulationSerializer(JsonSerializerOptions options) =>
      _options = options;

    /// <summary>
    /// Custom JSON converter for Position struct
    /// </summary>
    public class PositionJsonConverter : JsonConverter<Position>
    {
      public override Position Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
      {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected StartObject token");

        double x = 0, y = 0;
        while (reader.Read())
        {
          if (reader.TokenType == JsonTokenType.EndObject) break;
          if (reader.TokenType == JsonTokenType.PropertyName)
          {
            string? propertyName = reader.GetString();
            reader.Read();

            switch (propertyName?.ToLower())
            {
              case "x":
                x = reader.GetDouble();
                break;
              case "y":
                y = reader.GetDouble();
                break;
            }
          }
        }

        return new(x, y);
      }

      public override void Write(Utf8JsonWriter writer, HiveMind.Core.Domain.Common.Position value, JsonSerializerOptions options)
      {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteEndObject();
      }
    }

    /// <summary>
    /// Custom JSON converter for Guid formatting
    /// </summary>
    public class GuidJsonConverter : JsonConverter<Guid>
    {
      public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
      {
        string? guidString = reader.GetString();

        return string.IsNullOrEmpty(guidString) ? Guid.Empty : Guid.Parse(guidString);
      }

      public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("D")); // Use standard format
    }
  }

  /// <summary>
  /// Extension methods for JsonSimulationSerializer
  /// </summary>
  public static class JsonSimulationSerializerExtensions
  {
    /// <summary>
    /// Serializes object to compressed JSON (single line, no indentation)
    /// </summary>
    public static string SerializeCompact<T>(this JsonSimulationSerializer serializer, T obj)
    {
      JsonSimulationSerializer compactSerializer = serializer.WithCustomOptions(options =>
      {
        options.WriteIndented = false;
      });

      return compactSerializer.Serialize(obj);
    }

    /// <summary>
    /// Serializes object with minimal information (excludes null values and defaults)
    /// </summary>
    public static string SerializeMinimal<T>(this JsonSimulationSerializer serializer, T obj)
    {
      JsonSimulationSerializer minimalSerializer = serializer.WithCustomOptions(options =>
      {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
        options.WriteIndented = false;
      });

      return minimalSerializer.Serialize(obj);
    }

    /// <summary>
    /// Attempts to deserialize with fallback to default value on failure
    /// </summary>
    public static T? DeserializeOrDefault<T>(this JsonSimulationSerializer serializer, string data, T? defaultValue = default)
    {
      try
      {
        T? result = serializer.Deserialize<T>(data);

        return result ?? defaultValue;
      }
      catch
      {
        return defaultValue;
      }
    }
  }
}
