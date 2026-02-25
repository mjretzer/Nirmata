namespace Gmsd.Agents.Execution.ControlPlane.Tools.Firewall;

/// <summary>
/// Interface for validating file paths against allowed scopes.
/// Prevents tools from reading or writing outside the allowed task scope.
/// </summary>
public interface IScopeFirewall
{
    /// <summary>
    /// Validates that a file path is within the allowed scope.
    /// </summary>
    /// <param name="path">The file path to validate.</param>
    /// <exception cref="ScopeViolationException">Thrown if the path is outside allowed scopes.</exception>
    void ValidatePath(string path);
}
