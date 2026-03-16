using System.Text.Json;

namespace nirmata.Aos.Public;

/// <summary>
/// Provides shared <see cref="JsonSerializerOptions"/> configured for deterministic JSON serialization.
/// </summary>
/// <remarks>
/// Use these options with <see cref="IDeterministicJsonSerializer"/> to ensure consistent output
/// across all AOS artifact writes. The serializer implementation handles key ordering and LF endings.
/// </remarks>
public static class DeterministicJsonOptions
{
    /// <summary>
    /// Gets the standard options for AOS artifact serialization.
    /// </summary>
    /// <remarks>
    /// Configured with:
    /// - CamelCase property naming
    /// - No indentation (compact output)
    /// - Case-insensitive property matching for deserialization
    /// </remarks>
    public static JsonSerializerOptions Standard { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>
    /// Gets options for indented (pretty-printed) JSON output.
    /// </summary>
    /// <remarks>
    /// Use when human-readable output is desired. Still deterministic with stable key ordering.
    /// </remarks>
    public static JsonSerializerOptions Indented { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
