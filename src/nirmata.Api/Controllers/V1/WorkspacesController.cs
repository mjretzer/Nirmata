using nirmata.Data.Dto.Models.Filesystem;
using nirmata.Data.Dto.Models.Spec;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Data.Dto.Requests.Workspaces;
using nirmata.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace nirmata.Api.Controllers.V1;

[Route("v1/workspaces")]
public class WorkspacesController : nirmataController
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ISpecService _specService;
    private readonly IFileSystemService _fileSystemService;

    public WorkspacesController(
        IWorkspaceService workspaceService,
        ISpecService specService,
        IFileSystemService fileSystemService)
    {
        _workspaceService = workspaceService;
        _specService = specService;
        _fileSystemService = fileSystemService;
    }

    /// <summary>
    /// Returns all registered workspaces with live-derived status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<WorkspaceSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkspaces(CancellationToken cancellationToken)
    {
        var workspaces = await _workspaceService.GetAllAsync(cancellationToken);
        return Ok(workspaces);
    }

    /// <summary>
    /// Registers a new workspace and returns its summary.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceSummary), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterWorkspace(
        [FromBody] WorkspaceCreateRequest request,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceService.RegisterAsync(request.Name, request.Path, cancellationToken);
        return CreatedAtAction(nameof(GetWorkspaceById), new { workspaceId = workspace.Id }, workspace);
    }

    /// <summary>
    /// Returns the workspace with the given ID.
    /// </summary>
    [HttpGet("{workspaceId}")]
    [ProducesResponseType(typeof(WorkspaceSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspaceById(string workspaceId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(workspaceId, out var parsedWorkspaceId))
            return BadRequestResult($"Workspace '{workspaceId}' is not a valid workspace ID.");

        var workspace = await _workspaceService.GetByIdAsync(parsedWorkspaceId, cancellationToken);
        if (workspace is null)
            return NotFoundResult($"Workspace '{parsedWorkspaceId}' not found.");

        return Ok(workspace);
    }

    /// <summary>
    /// Updates the registered root path for a workspace.
    /// </summary>
    [HttpPut("{workspaceId:guid}")]
    [ProducesResponseType(typeof(WorkspaceSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWorkspace(
        Guid workspaceId,
        [FromBody] WorkspaceUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceService.UpdatePathAsync(workspaceId, request.Path, cancellationToken);
        if (workspace is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        return Ok(workspace);
    }

    /// <summary>
    /// Deregisters a workspace by ID.
    /// </summary>
    [HttpDelete("{workspaceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWorkspace(Guid workspaceId, CancellationToken cancellationToken)
    {
        var deleted = await _workspaceService.DeleteAsync(workspaceId, cancellationToken);
        if (!deleted)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        return NoContent();
    }

    /// <summary>
    /// Returns all milestones from the workspace's <c>.aos/spec/milestones/</c> directory.
    /// </summary>
    [HttpGet("{workspaceId:guid}/spec/milestones")]
    [ProducesResponseType(typeof(IReadOnlyList<MilestoneSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMilestones(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var milestones = await _specService.GetMilestonesAsync(root, cancellationToken);
        return Ok(milestones);
    }

    /// <summary>
    /// Returns all phases from the workspace's <c>.aos/spec/phases/</c> directory.
    /// </summary>
    [HttpGet("{workspaceId:guid}/spec/phases")]
    [ProducesResponseType(typeof(IReadOnlyList<PhaseSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhases(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var phases = await _specService.GetPhasesAsync(root, cancellationToken);
        return Ok(phases);
    }

    /// <summary>
    /// Returns all tasks from the workspace's <c>.aos/spec/tasks/</c> directory.
    /// </summary>
    [HttpGet("{workspaceId:guid}/spec/tasks")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTasks(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var tasks = await _specService.GetTasksAsync(root, cancellationToken);
        return Ok(tasks);
    }

    /// <summary>
    /// Returns the project spec from the workspace's <c>.aos/spec/project.json</c> file.
    /// </summary>
    [HttpGet("{workspaceId:guid}/spec/project")]
    [ProducesResponseType(typeof(ProjectSpecDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProject(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var project = await _specService.GetProjectAsync(root, cancellationToken);
        if (project is null)
            return NotFoundResult($"Project spec not found for workspace '{workspaceId}'.");

        return Ok(project);
    }

    /// <summary>
    /// Returns a directory listing when <paramref name="path"/> resolves to a directory,
    /// or raw file bytes with an appropriate <c>Content-Type</c> when it resolves to a file.
    /// The path is workspace-relative and must not escape the registered workspace root.
    /// </summary>
    [HttpGet("{workspaceId:guid}/files/{*path}")]
    [ProducesResponseType(typeof(DirectoryListingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspaceFile(
        Guid workspaceId,
        string? path,
        CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var relativePath = path ?? string.Empty;

        var absolutePath = _fileSystemService.ValidateAndNormalizePath(root, relativePath);

        if (Directory.Exists(absolutePath))
        {
            var listing = await _fileSystemService.GetDirectoryListingAsync(root, relativePath, cancellationToken);
            return Ok(listing);
        }

        if (System.IO.File.Exists(absolutePath))
        {
            var (content, contentType) = await _fileSystemService.GetFileContentAsync(root, relativePath, cancellationToken);
            return new FileContentResult(content, contentType);
        }

        return NotFoundResult($"Path '{relativePath}' not found in workspace '{workspaceId}'.");
    }
}
