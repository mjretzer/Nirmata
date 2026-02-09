using System.Text.Json.Serialization;

namespace Gmsd.Aos.Engine.Evidence.Calls;

/// <summary>
/// Auditable envelope for provider/tool calls.
/// Mirrors the <c>call-envelope.schema.json</c> contract (v1).
/// </summary>
internal sealed record CallEnvelope
{
    public CallEnvelope(
        int schemaVersion,
        string runId,
        string callId,
        string provider,
        string tool,
        string status)
    {
        SchemaVersion = schemaVersion;
        RunId = runId;
        CallId = callId;
        Provider = provider;
        Tool = tool;
        Status = status;
    }

    public int SchemaVersion { get; init; }
    public string RunId { get; init; }
    public string CallId { get; init; }
    public string Provider { get; init; }
    public string Tool { get; init; }
    public string Status { get; init; }

    // Schemas typically model "optional" via omission rather than null.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Request { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Response { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Error { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Meta { get; init; }
}

