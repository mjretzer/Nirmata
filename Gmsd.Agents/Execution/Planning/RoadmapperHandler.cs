using System.Text.Json;
using Gmsd.Agents.Models.Runtime;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Public;
using Gmsd.Aos.Public.Services;

namespace Gmsd.Agents.Execution.Planning;

/// <summary>
/// Command handler for the Roadmapper phase of the orchestrator workflow.
/// </summary>
public sealed class RoadmapperHandler
{
    private readonly IRoadmapper _roadmapper;
    private readonly IRoadmapGenerator _roadmapGenerator;
    private readonly IWorkspace _workspace;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoadmapperHandler"/> class.
    /// </summary>
    public RoadmapperHandler(
        IRoadmapper roadmapper,
        IRoadmapGenerator roadmapGenerator,
        IWorkspace workspace)
    {
        _roadmapper = roadmapper ?? throw new ArgumentNullException(nameof(roadmapper));
        _roadmapGenerator = roadmapGenerator ?? throw new ArgumentNullException(nameof(roadmapGenerator));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    /// <summary>
    /// Handles the roadmapper phase command.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result.</returns>
    public async Task<CommandRouteResult> HandleAsync(CommandRequest request, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Extract workspace path from request options or use default
            var workspacePath = request.Options.TryGetValue("workspacePath", out var wp) 
                ? wp?.ToString() ?? string.Empty 
                : string.Empty;

            if (string.IsNullOrWhiteSpace(workspacePath))
            {
                workspacePath = _workspace.RepositoryRootPath;
            }

            // Extract project spec reference from request options
            string projectSpecPath = ".aos/spec/project.json";
            if (request.Options.TryGetValue("projectSpecPath", out var psp))
            {
                projectSpecPath = psp?.ToString() ?? projectSpecPath;
            }

            // Create roadmap context
            var context = new RoadmapContext
            {
                RunId = runId,
                WorkspacePath = workspacePath,
                ProjectSpec = new ProjectSpecReference
                {
                    SpecPath = projectSpecPath,
                    ProjectId = "PRJ-0001",
                    ProjectName = "Untitled Project",
                    SchemaVersion = "gmsd:aos:schema:project:v1"
                },
                CorrelationId = runId,
                CancellationToken = ct
            };

            // Generate the roadmap
            var result = await _roadmapper.GenerateRoadmapAsync(context, ct);

            if (result.IsSuccess)
            {
                return new CommandRouteResult
                {
                    IsSuccess = true,
                    Output = $"Roadmap generated successfully. Created {result.MilestoneSpecs.Count} milestone(s) and {result.PhaseSpecs.Count} phase(s). " +
                             $"Roadmap ID: {result.RoadmapId}. Artifacts: {result.RoadmapSpecPath}"
                };
            }
            else
            {
                return new CommandRouteResult
                {
                    IsSuccess = false,
                    ErrorOutput = result.Error ?? "Roadmap generation failed without specific error."
                };
            }
        }
        catch (Exception ex)
        {
            return new CommandRouteResult
            {
                IsSuccess = false,
                ErrorOutput = $"Roadmapper handler failed: {ex.Message}"
            };
        }
    }
}
