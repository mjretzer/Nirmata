using Gmsd.Aos.Engine;

namespace Gmsd.Aos.Engine.Evidence.Calls;

/// <summary>
/// Minimal provider/tool call runtime wrapper.
/// </summary>
internal static class AosCallEnvelopeRuntime
{
    /// <summary>
    /// Record-only execution path (replay is disabled).
    /// Always invokes <paramref name="invoke"/> and records a call envelope for success/failure.
    /// </summary>
    public static T InvokeRecordOnly<T>(
        string runId,
        string provider,
        string tool,
        string callId,
        object? request,
        Func<T> invoke,
        ICallEnvelopeLogger logger,
        object? meta = null)
    {
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("Missing provider.", nameof(provider));
        if (string.IsNullOrWhiteSpace(tool)) throw new ArgumentException("Missing tool.", nameof(tool));
        if (string.IsNullOrWhiteSpace(callId)) throw new ArgumentException("Missing call id.", nameof(callId));
        if (invoke is null) throw new ArgumentNullException(nameof(invoke));
        if (logger is null) throw new ArgumentNullException(nameof(logger));

        try
        {
            var result = invoke();
            logger.Record(
                new CallEnvelope(
                    schemaVersion: 1,
                    runId: runId,
                    callId: callId.Trim(),
                    provider: provider.Trim(),
                    tool: tool.Trim(),
                    status: "succeeded")
                {
                    Request = request,
                    Response = new { value = result },
                    Meta = meta
                }
            );
            return result;
        }
        catch (Exception ex)
        {
            // Best-effort record; do not mask original failure if recording fails.
            try
            {
                logger.Record(
                    new CallEnvelope(
                        schemaVersion: 1,
                        runId: runId,
                        callId: callId.Trim(),
                        provider: provider.Trim(),
                        tool: tool.Trim(),
                        status: "failed")
                    {
                        Request = request,
                        Error = new { type = ex.GetType().FullName, message = ex.Message },
                        Meta = meta
                    }
                );
            }
            catch
            {
                // Best-effort only.
            }

            throw;
        }
    }
}

