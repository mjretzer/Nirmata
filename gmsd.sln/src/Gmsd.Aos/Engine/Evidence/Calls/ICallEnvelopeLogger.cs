namespace Gmsd.Aos.Engine.Evidence.Calls;

/// <summary>
/// Minimal abstraction for recording provider/tool call envelopes.
/// </summary>
internal interface ICallEnvelopeLogger
{
    void Record(CallEnvelope envelope);
}

