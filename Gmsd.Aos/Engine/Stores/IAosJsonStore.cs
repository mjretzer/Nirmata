using System.Text.Json;

namespace Gmsd.Aos.Engine.Stores;

/// <summary>
/// Contract-path based JSON file access under <c>.aos/**</c>.
/// All writes MUST be canonical and deterministic (stable bytes + atomic + avoid churn).
/// </summary>
internal interface IAosJsonStore : IAosStore
{
    /// <summary>
    /// Returns true when the contract-path target exists as a file.
    /// </summary>
    bool Exists(string contractPath);

    /// <summary>
    /// Reads and parses a JSON file, returning a detached <see cref="JsonElement"/>.
    /// </summary>
    JsonElement ReadJsonElement(string contractPath);

    /// <summary>
    /// Reads and deserializes a JSON file.
    /// </summary>
    T ReadJson<T>(string contractPath, JsonSerializerOptions serializerOptions);

    /// <summary>
    /// Writes a canonical deterministic JSON file if missing (no-op if present).
    /// </summary>
    void WriteJsonIfMissing<T>(string contractPath, T value, JsonSerializerOptions serializerOptions, bool writeIndented = true);

    /// <summary>
    /// Writes a canonical deterministic JSON file, atomically overwriting and avoiding churn.
    /// </summary>
    void WriteJsonOverwrite<T>(string contractPath, T value, JsonSerializerOptions serializerOptions, bool writeIndented = true);

    /// <summary>
    /// Deletes the target file if it exists. No-op when missing.
    /// </summary>
    void DeleteFile(string contractPath);
}

