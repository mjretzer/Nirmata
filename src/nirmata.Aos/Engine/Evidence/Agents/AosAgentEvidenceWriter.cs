using System.Text.Json;
using System.Text.Json.Serialization;
using nirmata.Aos.Engine.Errors;
using nirmata.Aos.Engine.Paths;

namespace nirmata.Aos.Engine.Evidence.Agents;

/// <summary>
/// Deterministic evidence writers for specialist agent request/result artifacts.
/// </summary>
internal static class AosAgentEvidenceWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // Ensure optional members are omitted (schemas typically model "optional" via omission, not null).
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonElement EmptyObject = CreateEmptyObject();

    /// <summary>
    /// Contract path for an agent request document:
    /// <c>.aos/evidence/runs/&lt;run-id&gt;/agents/&lt;agent-id&gt;/&lt;request-id&gt;/request.json</c>.
    /// </summary>
    public static string GetAgentRequestContractPath(string runId, string agentId, string requestId)
    {
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        agentId = NormalizeAndValidateSegment(agentId, nameof(agentId));
        requestId = NormalizeAndValidateSegment(requestId, nameof(requestId));

        return $".aos/evidence/runs/{runId}/agents/{agentId}/{requestId}/request.json";
    }

    /// <summary>
    /// Contract path for an agent result document:
    /// <c>.aos/evidence/runs/&lt;run-id&gt;/agents/&lt;agent-id&gt;/&lt;request-id&gt;/result.json</c>.
    /// </summary>
    public static string GetAgentResultContractPath(string runId, string agentId, string requestId)
    {
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        agentId = NormalizeAndValidateSegment(agentId, nameof(agentId));
        requestId = NormalizeAndValidateSegment(requestId, nameof(requestId));

        return $".aos/evidence/runs/{runId}/agents/{agentId}/{requestId}/result.json";
    }

    public static void WriteRequest(
        string aosRootPath,
        string runId,
        string agentId,
        string requestId,
        JsonElement? input = null)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        agentId = NormalizeAndValidateSegment(agentId, nameof(agentId));
        requestId = NormalizeAndValidateSegment(requestId, nameof(requestId));

        var normalizedInput = NormalizeObjectOrEmpty(input, nameof(input));

        var contractPath = GetAgentRequestContractPath(runId, agentId, requestId);
        var fullPath = AosPathRouter.ToAosRootPath(aosRootPath, contractPath);

        // Canonical deterministic JSON (stable bytes + atomic write semantics).
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            fullPath,
            new AgentRequestDocument(
                SchemaVersion: 1,
                RunId: runId,
                AgentId: agentId,
                RequestId: requestId,
                Input: normalizedInput
            ),
            JsonOptions,
            writeIndented: true
        );
    }

    public static void WriteResultSuccess(
        string aosRootPath,
        string runId,
        string agentId,
        string requestId,
        JsonElement? output = null)
    {
        WriteResultInternal(
            aosRootPath,
            runId,
            agentId,
            requestId,
            outcome: "success",
            error: null,
            output: output
        );
    }

    public static void WriteResultFailure(
        string aosRootPath,
        string runId,
        string agentId,
        string requestId,
        AosErrorEnvelope error,
        JsonElement? output = null)
    {
        if (error is null) throw new ArgumentNullException(nameof(error));
        if (string.IsNullOrWhiteSpace(error.Code)) throw new ArgumentException("Missing error code.", nameof(error));
        if (string.IsNullOrWhiteSpace(error.Message)) throw new ArgumentException("Missing error message.", nameof(error));

        WriteResultInternal(
            aosRootPath,
            runId,
            agentId,
            requestId,
            outcome: "failure",
            error: error,
            output: output
        );
    }

    private static void WriteResultInternal(
        string aosRootPath,
        string runId,
        string agentId,
        string requestId,
        string outcome,
        AosErrorEnvelope? error,
        JsonElement? output)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (string.IsNullOrWhiteSpace(outcome)) throw new ArgumentException("Missing outcome.", nameof(outcome));
        agentId = NormalizeAndValidateSegment(agentId, nameof(agentId));
        requestId = NormalizeAndValidateSegment(requestId, nameof(requestId));

        var normalizedOutput = NormalizeObjectOrEmpty(output, nameof(output));

        var contractPath = GetAgentResultContractPath(runId, agentId, requestId);
        var fullPath = AosPathRouter.ToAosRootPath(aosRootPath, contractPath);

        // Canonical deterministic JSON (stable bytes + atomic write semantics).
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            fullPath,
            new AgentResultDocument(
                SchemaVersion: 1,
                RunId: runId,
                AgentId: agentId,
                RequestId: requestId,
                Outcome: outcome,
                Error: error is null ? null : new AgentResultErrorDocument(error.Code.Trim(), error.Message.Trim(), error.Details),
                Output: normalizedOutput
            ),
            JsonOptions,
            writeIndented: true
        );
    }

    private static string NormalizeAndValidateSegment(string value, string paramName)
    {
        if (value is null) throw new ArgumentNullException(paramName);
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) throw new ArgumentException("Missing value.", paramName);

        if (trimmed is "." or "..")
        {
            throw new ArgumentException("Value MUST NOT be '.' or '..'.", paramName);
        }

        if (trimmed.Contains('/') || trimmed.Contains('\\'))
        {
            throw new ArgumentException("Value MUST NOT contain path separators.", paramName);
        }

        foreach (var ch in trimmed)
        {
            if (char.IsControl(ch))
            {
                throw new ArgumentException("Value MUST NOT contain control characters.", paramName);
            }
        }

        return trimmed;
    }

    private static JsonElement NormalizeObjectOrEmpty(JsonElement? value, string paramName)
    {
        if (value is null)
        {
            return EmptyObject;
        }

        var v = value.Value;
        if (v.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Value must be a JSON object when provided.", paramName);
        }

        return v;
    }

    private static JsonElement CreateEmptyObject()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }

    private sealed record AgentRequestDocument(
        int SchemaVersion,
        string RunId,
        string AgentId,
        string RequestId,
        JsonElement Input);

    private sealed record AgentResultDocument(
        int SchemaVersion,
        string RunId,
        string AgentId,
        string RequestId,
        string Outcome,
        AgentResultErrorDocument? Error,
        JsonElement Output);

    private sealed record AgentResultErrorDocument(
        string Code,
        string Message,
        object? Details);
}

