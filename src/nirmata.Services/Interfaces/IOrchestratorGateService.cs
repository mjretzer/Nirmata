using nirmata.Data.Dto.Models.OrchestratorGate;
using nirmata.Data.Dto.Models.WorkspaceStatus;

namespace nirmata.Services.Interfaces;

/// <summary>
/// Derives the orchestrator gate and timeline from workspace spec, state, evidence,
/// and UAT artifacts under a workspace root directory.
/// </summary>
public interface IOrchestratorGateService
{
    /// <summary>
    /// Computes the orchestrator gate for the workspace — identifying the current task,
    /// evaluating dependency, evidence, and UAT checks, and deriving whether the workspace
    /// is ready to advance (<see cref="OrchestratorGateDto.Runnable"/>).
    /// </summary>
    /// <param name="workspaceRoot">Absolute path to the workspace root directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<OrchestratorGateDto> GetGateAsync(
        string workspaceRoot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the workspace's ordered orchestrator timeline derived from
    /// <c>.aos/spec/phases/</c> and the current state cursor.
    /// </summary>
    /// <param name="workspaceRoot">Absolute path to the workspace root directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<OrchestratorTimelineDto> GetTimelineAsync(
        string workspaceRoot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Derives the workspace gate summary from canonical <c>.aos/spec</c>, <c>.aos/state</c>,
    /// and <c>.aos/codebase</c> artifacts.
    /// Maps the current blocking gate to a <see cref="WorkspaceGate"/> constant and includes
    /// brownfield codebase readiness details when the <c>.aos/codebase/</c> directory is present
    /// and the map is missing or stale.
    /// </summary>
    /// <param name="workspaceRoot">Absolute path to the workspace root directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WorkspaceGateSummaryDto> GetGateSummaryAsync(
        string workspaceRoot, CancellationToken cancellationToken = default);
}
