namespace Gmsd.Agents.Execution.ControlPlane.Tools.Contracts;

/// <summary>
/// Capability flags indicating tool behaviors and characteristics.
/// </summary>
[Flags]
public enum ToolCapability
{
    /// <summary>
    /// No special capabilities.
    /// </summary>
    None = 0,

    /// <summary>
    /// Tool supports caching of results.
    /// </summary>
    Caching = 1 << 0,

    /// <summary>
    /// Tool supports retry on failure.
    /// </summary>
    Retry = 1 << 1,

    /// <summary>
    /// Tool supports streaming responses.
    /// </summary>
    Streaming = 1 << 2,

    /// <summary>
    /// Tool is idempotent (safe to retry).
    /// </summary>
    Idempotent = 1 << 3,

    /// <summary>
    /// Tool may have side effects (mutating operation).
    /// </summary>
    SideEffects = 1 << 4,

    /// <summary>
    /// Tool requires authentication/authorization.
    /// </summary>
    RequiresAuth = 1 << 5
}
