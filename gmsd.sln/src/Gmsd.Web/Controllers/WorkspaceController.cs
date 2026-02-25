using Gmsd.Web.Models;
using Gmsd.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gmsd.Web.Controllers;

[ApiController]
[Route("api/v1/workspaces")]
public class WorkspaceController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ILogger<WorkspaceController>? _logger;

    public WorkspaceController(IWorkspaceService workspaceService, ILogger<WorkspaceController>? logger = null)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkspaceDto>>> List(CancellationToken cancellationToken)
    {
        try
        {
            var workspaces = await _workspaceService.ListWorkspacesAsync(cancellationToken);
            return Ok(workspaces);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list workspaces");
            return StatusCode(500, MapErrorResponse("InternalServerError", "Failed to list workspaces"));
        }
    }

    [HttpPost("open")]
    public async Task<ActionResult<WorkspaceDto>> Open([FromBody] OpenWorkspaceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Path))
            {
                return BadRequest(MapErrorResponse("InvalidPath", "Path cannot be empty"));
            }

            var workspace = await _workspaceService.OpenWorkspaceAsync(request.Path, cancellationToken);
            return Ok(workspace);
        }
        catch (ArgumentException ex)
        {
            _logger?.LogWarning(ex, "Invalid argument when opening workspace");
            return BadRequest(MapErrorResponse("InvalidArgument", ex.Message));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open workspace");
            return StatusCode(500, MapErrorResponse("InternalServerError", "Failed to open workspace"));
        }
    }

    [HttpPost("init")]
    public async Task<ActionResult<WorkspaceDto>> Init([FromBody] InitWorkspaceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Path))
            {
                return BadRequest(MapErrorResponse("InvalidPath", "Path cannot be empty"));
            }

            var workspace = await _workspaceService.InitWorkspaceAsync(request.Path, request.Name, cancellationToken);
            return Ok(workspace);
        }
        catch (ArgumentException ex)
        {
            _logger?.LogWarning(ex, "Invalid argument when initializing workspace");
            return BadRequest(MapErrorResponse("InvalidArgument", ex.Message));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize workspace");
            return StatusCode(500, MapErrorResponse("InternalServerError", "Failed to initialize workspace"));
        }
    }

    [HttpGet("validate")]
    public async Task<ActionResult<WorkspaceValidationReport>> Validate([FromQuery] string path, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(MapErrorResponse("InvalidPath", "Path cannot be empty"));
            }

            var report = await _workspaceService.ValidateWorkspaceAsync(path, cancellationToken);
            return Ok(report);
        }
        catch (ArgumentException ex)
        {
            _logger?.LogWarning(ex, "Invalid argument when validating workspace");
            return BadRequest(MapErrorResponse("InvalidArgument", ex.Message));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to validate workspace");
            return StatusCode(500, MapErrorResponse("InternalServerError", "Failed to validate workspace"));
        }
    }

    [HttpPost("repair")]
    public async Task<IActionResult> Repair([FromBody] OpenWorkspaceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Path))
            {
                return BadRequest(MapErrorResponse("InvalidPath", "Path cannot be empty"));
            }

            await _workspaceService.RepairWorkspaceAsync(request.Path, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger?.LogWarning(ex, "Invalid argument when repairing workspace");
            return BadRequest(MapErrorResponse("InvalidArgument", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning(ex, "Repair operation failed");
            return StatusCode(409, MapErrorResponse("RepairFailed", ex.Message));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to repair workspace");
            return StatusCode(500, MapErrorResponse("InternalServerError", "Failed to repair workspace"));
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<object>> GetActive(CancellationToken cancellationToken)
    {
        try
        {
            var path = await _workspaceService.GetActiveWorkspacePathAsync();
            return Ok(new { path });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get active workspace");
            return StatusCode(500, MapErrorResponse("InternalServerError", "Failed to get active workspace"));
        }
    }

    private static object MapErrorResponse(string code, string message)
    {
        return new
        {
            error = new
            {
                code,
                message,
                timestamp = DateTimeOffset.UtcNow
            }
        };
    }
}
