using System.Text.Json;

namespace nirmata.Aos.Public;

/// <summary>
/// Registry for loading and accessing JSON schemas by their canonical $id.
/// </summary>
/// <remarks>
/// The schema registry provides access to both embedded schemas (shipped with the engine)
/// and local schemas (materialized in the workspace under .aos/schemas/).
/// Schemas are indexed by their JSON Schema $id value for deterministic lookup.
/// </remarks>
public interface ISchemaRegistry
{
    /// <summary>
    /// Gets a schema by its canonical $id.
    /// </summary>
    /// <param name="schemaId">The schema $id (e.g., "nirmata:aos:schema:project:v1").</param>
    /// <returns>The schema document as a parsed JsonDocument.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the schema $id is not found in either embedded or local schemas.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the schema cannot be parsed as valid JSON.</exception>
    JsonDocument GetSchema(string schemaId);

    /// <summary>
    /// Attempts to get a schema by its canonical $id.
    /// </summary>
    /// <param name="schemaId">The schema $id (e.g., "nirmata:aos:schema:project:v1").</param>
    /// <param name="schema">When this method returns, contains the schema document if found; otherwise, null.</param>
    /// <returns>True if the schema was found; otherwise, false.</returns>
    bool TryGetSchema(string schemaId, out JsonDocument? schema);

    /// <summary>
    /// Lists all available schema $ids in the registry.
    /// </summary>
    /// <returns>A sorted list of all schema $ids from both embedded and local schema packs.</returns>
    /// <remarks>
    /// Returns the union of embedded schema $ids and local schema $ids.
    /// Local schema $ids take precedence over embedded schemas with the same $id.
    /// </remarks>
    IReadOnlyList<string> ListSchemaIds();

    /// <summary>
    /// Gets all embedded schema $ids.
    /// </summary>
    /// <returns>A sorted list of embedded schema $ids shipped with the engine.</returns>
    IReadOnlyList<string> ListEmbeddedSchemaIds();

    /// <summary>
    /// Gets all local schema $ids from the workspace.
    /// </summary>
    /// <returns>A sorted list of local schema $ids from .aos/schemas/.</returns>
    /// <remarks>
    /// Returns an empty list if no local schema pack exists (e.g., before 'aos init').
    /// </remarks>
    IReadOnlyList<string> ListLocalSchemaIds();

    /// <summary>
    /// Determines whether a schema with the specified $id exists in the registry.
    /// </summary>
    /// <param name="schemaId">The schema $id to check.</param>
    /// <returns>True if the schema exists in either embedded or local schemas; otherwise, false.</returns>
    bool HasSchema(string schemaId);
}
