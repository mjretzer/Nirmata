using System.Text.Json;
using System.Text.Json.Serialization;
using nirmata.Aos.Engine;
using nirmata.Aos.Engine.Paths;

namespace nirmata.Aos.Engine.Evidence.Calls;

/// <summary>
/// Records call envelopes as deterministic JSON files under:
/// <c>.aos/evidence/runs/&lt;run-id&gt;/logs/calls/&lt;call-id&gt;.json</c>.
/// </summary>
internal sealed class AosCallEnvelopeFileLogger : ICallEnvelopeLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _aosRootPath;
    private readonly string _runId;

    public AosCallEnvelopeFileLogger(string aosRootPath, string runId)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        _aosRootPath = aosRootPath;
        _runId = runId;
    }

    public void Record(CallEnvelope envelope)
    {
        if (envelope is null) throw new ArgumentNullException(nameof(envelope));

        if (!AosRunId.IsValid(envelope.RunId))
        {
            throw new ArgumentException("Envelope run id is invalid.", nameof(envelope));
        }

        if (!string.Equals(envelope.RunId, _runId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Envelope run id does not match logger run id.", nameof(envelope));
        }

        var callId = NormalizeAndValidateCallId(envelope.CallId);

        var logPath = AosPathRouter.GetRunCallEnvelopeLogPath(_aosRootPath, _runId, callId);
        var callsRoot = AosPathRouter.GetRunCallEnvelopesLogsRootPath(_aosRootPath, _runId);
        Directory.CreateDirectory(callsRoot);

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            logPath,
            envelope,
            JsonOptions,
            writeIndented: true
        );
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

