using System.Text;
using System.Text.Json;
using nirmata.Aos.Public;

namespace nirmata.Agents.Tests.Fakes;

/// <summary>
/// Fake deterministic JSON serializer for unit testing.
/// Uses standard JSON serialization without the Engine internals.
/// </summary>
public sealed class FakeDeterministicJsonSerializer : IDeterministicJsonSerializer
{
    private static readonly JsonSerializerOptions StandardOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <inheritdoc />
    public byte[] Serialize<T>(T value, JsonSerializerOptions serializerOptions, bool writeIndented = true)
    {
        var options = writeIndented
            ? new JsonSerializerOptions(serializerOptions) { WriteIndented = true }
            : serializerOptions;
        var json = JsonSerializer.Serialize(value, options);
        return Encoding.UTF8.GetBytes(json.ReplaceLineEndings("\n"));
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
        var json = Encoding.UTF8.GetString(jsonBytes);
        return Deserialize<T>(json, serializerOptions);
    }

    /// <inheritdoc />
    public T Deserialize<T>(string json, JsonSerializerOptions serializerOptions)
    {
        var result = JsonSerializer.Deserialize<T>(json, serializerOptions);
        if (result is null)
        {
            throw new InvalidOperationException("Failed to deserialize JSON to the requested type.");
        }
        return result;
    }

    /// <inheritdoc />
    public void WriteAtomic<T>(string path, T value, JsonSerializerOptions serializerOptions, bool writeIndented = true)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Create options preserving the naming policy
        var options = new JsonSerializerOptions(serializerOptions)
        {
            WriteIndented = writeIndented
        };

        var bytes = Serialize(value, options, writeIndented);

        // Check if content is identical (no-churn semantics)
        if (File.Exists(path))
        {
            var existing = File.ReadAllBytes(path);
            if (existing.SequenceEqual(bytes))
            {
                return;
            }
        }

        var tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, bytes);
        File.Move(tempPath, path, overwrite: true);
    }

    /// <inheritdoc />
    public void WriteIfMissing<T>(string path, T value, JsonSerializerOptions serializerOptions, bool writeIndented = true)
    {
        if (File.Exists(path))
        {
            return;
        }

        WriteAtomic(path, value, serializerOptions, writeIndented);
    }

    /// <inheritdoc />
    public T ReadFile<T>(string path, JsonSerializerOptions serializerOptions)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"JSON file not found: '{path}'", path);
        }

        var json = File.ReadAllText(path);
        return Deserialize<T>(json, serializerOptions);
    }
}
