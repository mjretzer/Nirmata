using System.Text.Json;
using System.Text.Json.Serialization;
using Gmsd.Aos.Engine;
using Gmsd.Aos.Engine.Evidence;
using Gmsd.Aos.Engine.Paths;

namespace Gmsd.Agents.Execution.ControlPlane.Llm.Filters;

/// <summary>
/// Default implementation of <see cref="ILlmEvidenceWriter"/> that writes
/// <see cref="LlmCallEnvelope"/> records to the AOS evidence store.
/// </summary>
internal sealed class AosLlmEvidenceWriter : ILlmEvidenceWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _aosRootPath;
    private readonly string _runId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AosLlmEvidenceWriter"/> class.
    /// </summary>
    /// <param name="aosRootPath">The absolute path to the AOS root directory.</param>
    /// <param name="runId">The run identifier for grouping evidence.</param>
    public AosLlmEvidenceWriter(string aosRootPath, string runId)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        _aosRootPath = aosRootPath;
        _runId = runId;
    }

    /// <inheritdoc />
    public void Write(LlmCallEnvelope envelope)
    {
        if (envelope is null) throw new ArgumentNullException(nameof(envelope));

        if (!AosRunId.IsValid(envelope.RunId))
        {
            throw new ArgumentException("Envelope run id is invalid.", nameof(envelope));
        }

        if (!string.Equals(envelope.RunId, _runId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Envelope run id does not match writer run id.", nameof(envelope));
        }

        var callId = NormalizeAndValidateCallId(envelope.CallId);

        // Use a dedicated LLM calls subdirectory
        var logPath = GetLlmCallLogPath(_aosRootPath, _runId, callId);
        var logsRoot = GetLlmCallLogsRootPath(_aosRootPath, _runId);
        Directory.CreateDirectory(logsRoot);

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            logPath,
            envelope,
            JsonOptions,
            writeIndented: true
        );
    }

    /// <summary>
    /// Gets the canonical root folder for LLM call envelope logs under a run:
    /// <c>.aos/evidence/runs/&lt;run-id&gt;/logs/llm/</c>.
    /// </summary>
    private static string GetLlmCallLogsRootPath(string aosRootPath, string runId) =>
        Path.Combine(AosPathRouter.GetRunLogsRootPath(aosRootPath, runId), "llm");

    /// <summary>
    /// Gets the canonical log file location for a single LLM call envelope record:
    /// <c>.aos/evidence/runs/&lt;run-id&gt;/logs/llm/&lt;call-id&gt;.json</c>.
    /// </summary>
    private static string GetLlmCallLogPath(string aosRootPath, string runId, string callId)
    {
        return Path.Combine(GetLlmCallLogsRootPath(aosRootPath, runId), callId + ".json");
    }

    private static string NormalizeAndValidateCallId(string value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) throw new ArgumentException("Missing call id.", nameof(value));

        if (trimmed is "." or "..")
        {
            throw new ArgumentException("Call id MUST NOT be '.' or '..'.", nameof(value));
        }

        if (trimmed.Contains('/') || trimmed.Contains('\\'))
        {
            throw new ArgumentException("Call id MUST NOT contain path separators.", nameof(value));
        }

        foreach (var ch in trimmed)
        {
            if (char.IsControl(ch))
            {
                throw new ArgumentException("Call id MUST NOT contain control characters.", nameof(value));
            }
        }

        var invalid = Path.GetInvalidFileNameChars();
        if (trimmed.IndexOfAny(invalid) >= 0)
        {
            throw new ArgumentException("Call id contains invalid filename characters.", nameof(value));
        }

        return trimmed;
    }
}
