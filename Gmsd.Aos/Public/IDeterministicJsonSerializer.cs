using System.Text.Json;

namespace Gmsd.Aos.Public;

/// <summary>
/// Provides deterministic JSON serialization and deserialization with canonical output.
/// </summary>
/// <remarks>
/// Guarantees:
/// - UTF-8 encoding without BOM
/// - LF line endings (normalized from CRLF)
/// - Stable key ordering (ordinal string sort)
/// - Atomic file writes (temp file + replace)
/// - No-churn semantics (skip write if byte-identical)
/// </remarks>
public interface IDeterministicJsonSerializer
{
    /// <summary>
    /// Serializes an object to a canonical JSON byte array.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <param name="serializerOptions">Options for serialization.</param>
    /// <param name="writeIndented">Whether to write indented (pretty-printed) JSON.</param>
    /// <returns>UTF-8 encoded JSON bytes with LF line endings and sorted keys.</returns>
    byte[] Serialize<T>(T value, JsonSerializerOptions serializerOptions, bool writeIndented = true);

    /// <summary>
    /// Serializes an object to a canonical JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <param name="serializerOptions">Options for serialization.</param>
    /// <param name="writeIndented">Whether to write indented (pretty-printed) JSON.</param>
    /// <returns>JSON string with LF line endings and sorted keys.</returns>
    string SerializeToString<T>(T value, JsonSerializerOptions serializerOptions, bool writeIndented = true);

    /// <summary>
    /// Deserializes JSON bytes to an object of type T.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="jsonBytes">The UTF-8 encoded JSON bytes.</param>
    /// <param name="serializerOptions">Options for deserialization.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="InvalidOperationException">If deserialization fails or returns null.</exception>
    T Deserialize<T>(byte[] jsonBytes, JsonSerializerOptions serializerOptions);

    /// <summary>
    /// Deserializes a JSON string to an object of type T.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string.</param>
    /// <param name="serializerOptions">Options for deserialization.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="InvalidOperationException">If deserialization fails or returns null.</exception>
    T Deserialize<T>(string json, JsonSerializerOptions serializerOptions);

    /// <summary>
    /// Atomically writes JSON to a file with no-churn semantics.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="path">The target file path.</param>
    /// <param name="value">The object to serialize and write.</param>
    /// <param name="serializerOptions">Options for serialization.</param>
    /// <param name="writeIndented">Whether to write indented (pretty-printed) JSON.</param>
    /// <remarks>
    /// Writes to a temporary file first, then atomically replaces the target.
    /// If the target file exists and has byte-identical content, no write occurs.
    /// </remarks>
    void WriteAtomic<T>(string path, T value, JsonSerializerOptions serializerOptions, bool writeIndented = true);

    /// <summary>
    /// Atomically writes JSON to a file only if the file does not exist.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="path">The target file path.</param>
    /// <param name="value">The object to serialize and write.</param>
    /// <param name="serializerOptions">Options for serialization.</param>
    /// <param name="writeIndented">Whether to write indented (pretty-printed) JSON.</param>
    /// <remarks>
    /// Skips writing if the target file already exists.
    /// </remarks>
    void WriteIfMissing<T>(string path, T value, JsonSerializerOptions serializerOptions, bool writeIndented = true);

    /// <summary>
    /// Reads JSON from a file and deserializes it.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="path">The file path to read from.</param>
    /// <param name="serializerOptions">Options for deserialization.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="FileNotFoundException">If the file does not exist.</exception>
    /// <exception cref="InvalidOperationException">If deserialization fails.</exception>
    T ReadFile<T>(string path, JsonSerializerOptions serializerOptions);
}
