using System.Text.Json.Serialization;

namespace nirmata.Aos.Engine.Workspace;

internal sealed record AosWorkspaceConfigDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("agentPreferences")] IDictionary<string, object> AgentPreferences,
    [property: JsonPropertyName("engineOverrides")] IDictionary<string, object> EngineOverrides,
    [property: JsonPropertyName("excludedPaths")] IReadOnlyList<string> ExcludedPaths);
