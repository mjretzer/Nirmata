using System.Text.Json.Nodes;
using Json.Schema;

namespace Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Represents a structured output schema that can be enforced by an LLM provider.
/// </summary>
public sealed record LlmStructuredOutputSchema
{
    private JsonSchema? _compiledSchema;

    /// <summary>
    /// Unique, provider-friendly schema name. Must be compatible with OpenAI response_format names.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional human-readable description that some providers surface to the model.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Raw JSON schema definition that will be shared with the LLM and used for validation.
    /// </summary>
    public required JsonNode Schema { get; init; }

    /// <summary>
    /// When true, provider implementations MUST validate the response against <see cref="Schema"/>.
    /// </summary>
    public bool StrictValidation { get; init; } = true;

    /// <summary>
    /// Creates a schema from JSON text.
    /// </summary>
    /// <param name="name">Unique schema name.</param>
    /// <param name="schemaJson">JSON schema document.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="strictValidation">Whether to enforce validation after completion.</param>
    /// <returns>Structured output schema instance.</returns>
    /// <exception cref="ArgumentException">Thrown when inputs are invalid.</exception>
    public static LlmStructuredOutputSchema FromJson(
        string name,
        string schemaJson,
        string? description = null,
        bool strictValidation = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Schema name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            throw new ArgumentException("Schema JSON is required.", nameof(schemaJson));
        }

        var parsed = JsonNode.Parse(schemaJson) ??
                     throw new ArgumentException("Schema JSON parsed to null node.", nameof(schemaJson));

        return new LlmStructuredOutputSchema
        {
            Name = name,
            Description = description,
            Schema = parsed.DeepClone(),
            StrictValidation = strictValidation
        };
    }

    /// <summary>
    /// Builds the OpenAI-compatible response_format payload for this schema.
    /// </summary>
    public object ToResponseFormatPayload()
    {
        return new
        {
            type = "json_schema",
            json_schema = new
            {
                name = Name,
                description = Description,
                schema = Schema
            }
        };
    }

    /// <summary>
    /// Compiles the schema into a JsonSchema.Net object for validation.
    /// </summary>
    public JsonSchema GetCompiledSchema()
    {
        _compiledSchema ??= JsonSchema.FromText(Schema.ToJsonString());
        return _compiledSchema;
    }
}
