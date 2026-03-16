using System.Text;
using System.Text.Json;
using nirmata.Aos.Public;

namespace nirmata.Aos.Engine;

/// <summary>
/// Deterministic JSON serializer implementation using canonical output format.
/// </summary>
/// <remarks>
/// Implements UTF-8 w/o BOM, LF endings, stable key ordering, atomic writes, and no-churn.
/// </remarks>
internal sealed class DeterministicJsonSerializer : IDeterministicJsonSerializer
{
    /// <inheritdoc />
    public byte[] Serialize<T>(T value, JsonSerializerOptions serializerOptions, bool writeIndented = true)
    {
        if (serializerOptions is null)
            throw new ArgumentNullException(nameof(serializerOptions));

        return DeterministicJsonFileWriter.SerializeToCanonicalUtf8Bytes(value, serializerOptions, writeIndented);
    }

    /// <inheritdoc />
    public string SerializeToString<T>(T value, JsonSerializerOptions serializerOptions, bool writeIndented = true)
    {
        var bytes = Serialize(value, serializerOptions, writeIndented);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <inheritdoc />
    public T Deserialize<T>(byte[] jsonBytes, JsonSerializerOptions serializerOptions)
    {
        if (jsonBytes is null)
            throw new ArgumentNullException(nameof(jsonBytes));
        if (serializerOptions is null)
            throw new ArgumentNullException(nameof(serializerOptions));

        var json = Encoding.UTF8.GetString(jsonBytes);
        return Deserialize<T>(json, serializerOptions);
    }

    /// <inheritdoc />
    public T Deserialize<T>(string json, JsonSerializerOptions serializerOptions)
    {
        if (json is null)
            throw new ArgumentNullException(nameof(json));
        if (serializerOptions is null)
            throw new ArgumentNullException(nameof(serializerOptions));

        var value = JsonSerializer.Deserialize<T>(json, serializerOptions);
        if (value is null)
        {
            throw new InvalidOperationException("Failed to deserialize JSON to the requested type.");
        }

        return value;
    }

    /// <inheritdoc />
    public void WriteAtomic<T>(string path, T value, JsonSerializerOptions serializerOptions, bool writeIndented = true)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (serializerOptions is null)
            throw new ArgumentNullException(nameof(serializerOptions));

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, value, serializerOptions, writeIndented);
    }

    /// <inheritdoc />
    public void WriteIfMissing<T>(string path, T value, JsonSerializerOptions serializerOptions, bool writeIndented = true)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (serializerOptions is null)
            throw new ArgumentNullException(nameof(serializerOptions));

        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(path, value, serializerOptions, writeIndented);
    }

    /// <inheritdoc />
    public T ReadFile<T>(string path, JsonSerializerOptions serializerOptions)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (serializerOptions is null)
            throw new ArgumentNullException(nameof(serializerOptions));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"JSON file not found: '{path}'", path);
        }

        var json = File.ReadAllText(path);
        return Deserialize<T>(json, serializerOptions);
    }
}
