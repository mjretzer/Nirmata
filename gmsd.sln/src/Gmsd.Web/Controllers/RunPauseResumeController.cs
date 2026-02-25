using Gmsd.Aos.Public;
using Microsoft.AspNetCore.Mvc;

namespace Gmsd.Web.Controllers;

/// <summary>
/// API controller for pause/resume operations on runs.
/// </summary>
[ApiController]
[Route("api/runs")]
public class RunPauseResumeController : ControllerBase
{
    private readonly IRunManager _runManager;
    private readonly ILogger<RunPauseResumeController> _logger;

    public RunPauseResumeController(
        IRunManager runManager,
        ILogger<RunPauseResumeController> logger)
    {
        _runManager = runManager ?? throw new ArgumentNullException(nameof(runManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Pauses a running run.
    /// </summary>
    /// <param name="runId">The run ID to pause.</param>
    /// <returns>200 OK if successful, 400 Bad Request if invalid, 404 Not Found if run not found, 409 Conflict if run cannot be paused.</returns>
    [HttpPost("{runId}/pause")]
    public IActionResult PauseRun(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return BadRequest(new { error = "Run ID is required." });
        }

        try
        {
            _runManager.PauseRun(runId, DateTimeOffset.UtcNow);
            _logger.LogInformation("Run {RunId} paused successfully.", runId);
            return Ok(new { message = $"Run '{runId}' paused successfully." });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid run ID: {RunId}. Error: {Error}", runId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning("Run not found: {RunId}. Error: {Error}", runId, ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Cannot pause run {RunId}. Error: {Error}", runId, ex.Message);
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while pausing run {RunId}.", runId);
            return StatusCode(500, new { error = "An unexpected error occurred while pausing the run." });
        }
    }

    /// <summary>
    /// Resumes a paused run.
    /// </summary>
    /// <param name="runId">The run ID to resume.</param>
    /// <returns>200 OK if successful, 400 Bad Request if invalid, 404 Not Found if run not found, 409 Conflict if run cannot be resumed.</returns>
    [HttpPost("{runId}/resume")]
    public IActionResult ResumeRun(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return BadRequest(new { error = "Run ID is required." });
        }

        try
        {
            _runManager.ResumeRun(runId, DateTimeOffset.UtcNow);
            _logger.LogInformation("Run {RunId} resumed successfully.", runId);
            return Ok(new { message = $"Run '{runId}' resumed successfully." });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid run ID: {RunId}. Error: {Error}", runId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning("Run not found: {RunId}. Error: {Error}", runId, ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Cannot resume run {RunId}. Error: {Error}", runId, ex.Message);
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while resuming run {RunId}.", runId);
            return StatusCode(500, new { error = "An unexpected error occurred while resuming the run." });
        }
    }
}
