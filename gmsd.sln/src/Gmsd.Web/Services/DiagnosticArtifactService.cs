using System.Text.Json;
using Gmsd.Web.Models;

namespace Gmsd.Web.Services;

public interface IDiagnosticArtifactService
{
    Task<DiagnosticArtifactViewModel?> GetDiagnosticForArtifactAsync(string workspacePath, string artifactPath);
    Task<List<DiagnosticArtifactViewModel>> ListDiagnosticsAsync(string workspacePath, string? phase = null);
    Task<ArtifactValidationStatusViewModel> GetValidationStatusAsync(string workspacePath, string artifactPath);
}

public class DiagnosticArtifactService : IDiagnosticArtifactService
{
    private readonly ILogger<DiagnosticArtifactService> _logger;
    private const string DiagnosticsDirectory = ".aos/diagnostics";

    public DiagnosticArtifactService(ILogger<DiagnosticArtifactService> logger)
    {
        _logger = logger;
    }

    public async Task<DiagnosticArtifactViewModel?> GetDiagnosticForArtifactAsync(string workspacePath, string artifactPath)
    {
        try
        {
            var diagnosticPath = GetDiagnosticPath(workspacePath, artifactPath);
            if (!File.Exists(diagnosticPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(diagnosticPath);
            var diagnostic = JsonSerializer.Deserialize<DiagnosticArtifactViewModel>(json);
            return diagnostic;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading diagnostic for artifact {ArtifactPath}", artifactPath);
            return null;
        }
    }

    public async Task<List<DiagnosticArtifactViewModel>> ListDiagnosticsAsync(string workspacePath, string? phase = null)
    {
        var diagnostics = new List<DiagnosticArtifactViewModel>();

        try
        {
            var diagnosticsPath = Path.Combine(workspacePath, DiagnosticsDirectory);
            if (!Directory.Exists(diagnosticsPath))
            {
                return diagnostics;
            }

            var searchPattern = phase != null ? $"{phase}/*.diagnostic.json" : "**/*.diagnostic.json";
            var diagnosticFiles = Directory.GetFiles(diagnosticsPath, searchPattern, SearchOption.AllDirectories);

            foreach (var file in diagnosticFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var diagnostic = JsonSerializer.Deserialize<DiagnosticArtifactViewModel>(json);
                    if (diagnostic != null)
                    {
                        diagnostics.Add(diagnostic);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading diagnostic file {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing diagnostics in workspace {WorkspacePath}", workspacePath);
        }

        return diagnostics;
    }

    public async Task<ArtifactValidationStatusViewModel> GetValidationStatusAsync(string workspacePath, string artifactPath)
    {
        var diagnostic = await GetDiagnosticForArtifactAsync(workspacePath, artifactPath);

        if (diagnostic == null)
        {
            return new ArtifactValidationStatusViewModel
            {
                IsValid = true,
                ValidationMessage = "Artifact is valid",
                ValidatedAt = DateTime.UtcNow
            };
        }

        return new ArtifactValidationStatusViewModel
        {
            IsValid = false,
            ValidationMessage = $"Validation failed: {diagnostic.ValidationErrors.Count} error(s) found",
            Diagnostic = diagnostic,
            ValidatedAt = diagnostic.Timestamp.UtcDateTime
        };
    }

    private string GetDiagnosticPath(string workspacePath, string artifactPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(artifactPath);
        var phase = ExtractPhaseFromPath(artifactPath);
        var diagnosticFileName = $"{fileName}.diagnostic.json";
        return Path.Combine(workspacePath, DiagnosticsDirectory, phase, diagnosticFileName);
    }

    private string ExtractPhaseFromPath(string artifactPath)
    {
        var parts = artifactPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length > 1 && parts[0] == ".aos")
        {
            return parts[1];
        }
        return "unknown";
    }
}
