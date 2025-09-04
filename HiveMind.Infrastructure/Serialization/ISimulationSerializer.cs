namespace HiveMind.Infrastructure.Serialization
{
  /// <summary>
  /// Interface for serializing simulation objects
  /// </summary>
  public interface ISimulationSerializer
  {
    /// <summary>
    /// Serializes an object to a string representation
    /// </summary>
    string Serialize<T>(T obj);

    /// <summary>
    /// Deserializes a string back to an object
    /// </summary>
    T? Deserialize<T>(string data);

    /// <summary>
    /// Gets the file extension for this serializer
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Gets the MIME type for this serializer
    /// </summary>
    string MimeType { get; }
  }
}
