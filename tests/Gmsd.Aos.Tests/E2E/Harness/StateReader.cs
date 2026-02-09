namespace Gmsd.Aos.Tests.E2E.Harness;

using System;
using System.IO;
using System.Text.Json;

/// <summary>
/// Provides utilities for reading state files from the .aos/ directory.
/// </summary>
public static class StateReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Reads and deserializes a state file from the .aos/ directory.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="repoRoot">The root path of the repository.</param>
    /// <param name="relativePath">The relative path within .aos/ (e.g., "state/cursor.json").</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is invalid.</exception>
    public static T Read<T>(string repoRoot, string relativePath)
    {
        var filePath = Path.Combine(repoRoot, ".aos", relativePath);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"State file not found: {filePath}");
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) 
            ?? throw new JsonException($"Failed to deserialize {filePath}");
    }

    /// <summary>
    /// Attempts to read and deserialize a state file, returning null if not found or invalid.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="repoRoot">The root path of the repository.</param>
    /// <param name="relativePath">The relative path within .aos/.</param>
    /// <returns>The deserialized object, or null if not found or invalid.</returns>
    public static T? TryRead<T>(string repoRoot, string relativePath)
    {
        try
        {
            return Read<T>(repoRoot, relativePath);
        }
        catch
        {
            return default;
        }
    }
}
