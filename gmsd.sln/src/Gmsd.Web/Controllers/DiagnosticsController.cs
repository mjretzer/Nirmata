using Microsoft.AspNetCore.Mvc;
using Gmsd.Web.Services;

namespace Gmsd.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly IDiagnosticArtifactService _diagnosticService;
    private readonly ILogger<DiagnosticsController> _logger;
    private readonly IConfiguration _configuration;

    public DiagnosticsController(
        IDiagnosticArtifactService diagnosticService,
        ILogger<DiagnosticsController> logger,
        IConfiguration configuration)
    {
        _diagnosticService = diagnosticService;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("artifact")]
    public async Task<IActionResult> GetDiagnosticForArtifact([FromQuery] string workspacePath, [FromQuery] string artifactPath)
    {
        if (string.IsNullOrEmpty(workspacePath) || string.IsNullOrEmpty(artifactPath))
        {
            return BadRequest("workspacePath and artifactPath are required");
        }

        try
        {
            var diagnostic = await _diagnosticService.GetDiagnosticForArtifactAsync(workspacePath, artifactPath);
            if (diagnostic == null)
            {
                return NotFound();
            }

            return Ok(diagnostic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving diagnostic for artifact");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("list")]
    public async Task<IActionResult> ListDiagnostics([FromQuery] string workspacePath, [FromQuery] string? phase = null)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            return BadRequest("workspacePath is required");
        }

        try
        {
            var diagnostics = await _diagnosticService.ListDiagnosticsAsync(workspacePath, phase);
            return Ok(diagnostics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing diagnostics");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetValidationStatus([FromQuery] string workspacePath, [FromQuery] string artifactPath)
    {
        if (string.IsNullOrEmpty(workspacePath) || string.IsNullOrEmpty(artifactPath))
        {
            return BadRequest("workspacePath and artifactPath are required");
        }

        try
        {
            var status = await _diagnosticService.GetValidationStatusAsync(workspacePath, artifactPath);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving validation status");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
