using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using nirmata.Agents.Models.Runtime;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Validation;

/// <summary>
/// Responsible for discovering and reading diagnostic artifacts from the workspace.
/// </summary>
public static class DiagnosticArtifactReader
{
    private const string DiagnosticsRoot = ".aos/diagnostics";

    /// <summary>
    /// Lists all diagnostic artifacts found in the workspace.
    /// </summary>
    /// <param name="aosRootPath">The root path of the .aos directory.</param>
    /// <returns>A collection of diagnostic artifacts.</returns>
    public static IEnumerable<DiagnosticArtifact> ListAll(string aosRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aosRootPath);

        var diagnosticsDir = Path.Combine(aosRootPath, "diagnostics");
        if (!Directory.Exists(diagnosticsDir))
        {
            return Enumerable.Empty<DiagnosticArtifact>();
        }

        var diagnosticFiles = Directory.GetFiles(diagnosticsDir, "*.diagnostic.json", SearchOption.AllDirectories);
        var results = new List<DiagnosticArtifact>();

        foreach (var file in diagnosticFiles)
        {
            var diagnostic = Read(file);
            if (diagnostic != null)
            {
                results.Add(diagnostic);
            }
        }

        return results;
    }

    /// <summary>
    /// Lists diagnostic artifacts for a specific phase.
    /// </summary>
    /// <param name="aosRootPath">The root path of the .aos directory.</param>
    /// <param name="phase">The workflow phase (e.g., "phase-planning").</param>
    /// <returns>A collection of diagnostic artifacts for the phase.</returns>
    public static IEnumerable<DiagnosticArtifact> ListByPhase(string aosRootPath, string phase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aosRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);

        var phaseDir = Path.Combine(aosRootPath, "diagnostics", phase);
        if (!Directory.Exists(phaseDir))
        {
            return Enumerable.Empty<DiagnosticArtifact>();
        }

        var diagnosticFiles = Directory.GetFiles(phaseDir, "*.diagnostic.json", SearchOption.TopDirectoryOnly);
        var results = new List<DiagnosticArtifact>();

        foreach (var file in diagnosticFiles)
        {
            var diagnostic = Read(file);
            if (diagnostic != null)
            {
                results.Add(diagnostic);
            }
        }

        return results;
    }

    /// <summary>
    /// Reads a diagnostic artifact from a specific file path.
    /// </summary>
    /// <param name="filePath">The absolute path to the diagnostic file.</param>
    /// <returns>The diagnostic artifact, or null if it could not be read.</returns>
    public static DiagnosticArtifact? Read(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<DiagnosticArtifact>(json, DeterministicJsonOptions.Standard);
        }
        catch
        {
            // Log or handle deserialization failure if needed
            return null;
        }
    }
}
