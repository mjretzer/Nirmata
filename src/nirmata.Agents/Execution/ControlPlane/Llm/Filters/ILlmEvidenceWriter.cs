using nirmata.Aos.Engine.Evidence;

namespace nirmata.Agents.Execution.ControlPlane.Llm.Filters;

/// <summary>
/// Writes LLM call envelopes to the evidence store.
/// </summary>
internal interface ILlmEvidenceWriter
{
    /// <summary>
    /// Writes an LLM call envelope to the evidence store.
    /// </summary>
    /// <param name="envelope">The envelope to write.</param>
    void Write(LlmCallEnvelope envelope);
}
