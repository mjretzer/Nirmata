using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Gmsd.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtifactValidationController : ControllerBase
{
    private readonly ILogger<ArtifactValidationController> _logger;

    public ArtifactValidationController(ILogger<ArtifactValidationController> logger)
    {
        _logger = logger;
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateArtifact([FromQuery] string artifactPath)
    {
        if (string.IsNullOrEmpty(artifactPath))
        {
            return BadRequest("artifactPath is required");
        }

        try
        {
            if (!System.IO.File.Exists(artifactPath))
            {
                return NotFound(new { error = "Artifact file not found" });
            }

            var json = await System.IO.File.ReadAllTextAsync(artifactPath);
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            return Ok(new
            {
                isValid = true,
                message = "Artifact is valid JSON",
                path = artifactPath,
                validatedAt = DateTime.UtcNow
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in artifact {ArtifactPath}", artifactPath);
            return BadRequest(new
            {
                isValid = false,
                message = "Invalid JSON format",
                error = ex.Message,
                path = artifactPath
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating artifact {ArtifactPath}", artifactPath);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("list-diagnostics")]
    public IActionResult ListDiagnostics([FromQuery] string workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            return BadRequest("workspacePath is required");
        }

        try
        {
            var diagnosticsDir = Path.Combine(workspacePath, ".aos", "diagnostics");
            if (!Directory.Exists(diagnosticsDir))
            {
                return Ok(new
                {
                    diagnostics = new List<object>(),
                    message = "No diagnostics found"
                });
            }

            var diagnosticFiles = Directory.GetFiles(diagnosticsDir, "*.diagnostic.json", SearchOption.AllDirectories);
            var diagnostics = diagnosticFiles.Select(f => new
            {
                path = f,
                fileName = Path.GetFileName(f),
                directory = Path.GetDirectoryName(f),
                lastModified = System.IO.File.GetLastWriteTimeUtc(f)
            }).ToList();

            return Ok(new
            {
                diagnostics = diagnostics,
                count = diagnostics.Count,
                workspacePath = workspacePath
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing diagnostics in workspace {WorkspacePath}", workspacePath);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
