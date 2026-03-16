using System;
using System.IO;
using System.Text.Json;
using nirmata.Agents.Models.Runtime;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Validation;

/// <summary>
/// Responsible for persisting diagnostic artifacts to the workspace.
/// </summary>
public static class DiagnosticArtifactWriter
{
    private const string DiagnosticsRoot = ".aos/diagnostics";

    /// <summary>
    /// Writes a diagnostic artifact to the deterministic location in the workspace.
    /// </summary>
    /// <param name="aosRootPath">The root path of the .aos directory.</param>
    /// <param name="diagnostic">The diagnostic artifact to persist.</param>
    /// <returns>The absolute path where the diagnostic was written.</returns>
    public static string Write(string aosRootPath, DiagnosticArtifact diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aosRootPath);
        ArgumentNullException.ThrowIfNull(diagnostic);

        // Path: .aos/diagnostics/{phase}/{id}.diagnostic.json
        // For artifact validation, we often use the filename or a sanitized path as the ID
        var artifactFileName = Path.GetFileNameWithoutExtension(diagnostic.ArtifactPath);
        var diagnosticId = string.IsNullOrWhiteSpace(artifactFileName) ? "unknown" : artifactFileName;

        var phaseDirectory = Path.Combine(aosRootPath, "diagnostics", diagnostic.Phase);
        Directory.CreateDirectory(phaseDirectory);

        var diagnosticFileName = $"{diagnosticId}.diagnostic.json";
        var diagnosticPath = Path.Combine(phaseDirectory, diagnosticFileName);

        var json = JsonSerializer.Serialize(diagnostic, DeterministicJsonOptions.Indented);
        File.WriteAllText(diagnosticPath, json);

        return diagnosticPath;
    }
}
