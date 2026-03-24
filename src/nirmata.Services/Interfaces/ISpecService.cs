using nirmata.Data.Dto.Models.Spec;

namespace nirmata.Services.Interfaces;

public interface ISpecService
{
    /// <summary>Reads milestones from <c>.aos/spec/milestones/**</c> under the given workspace root.</summary>
    Task<IReadOnlyList<MilestoneSummaryDto>> GetMilestonesAsync(string workspaceRoot, CancellationToken cancellationToken = default);

    /// <summary>Reads phases from <c>.aos/spec/phases/**</c> under the given workspace root.</summary>
    Task<IReadOnlyList<PhaseSummaryDto>> GetPhasesAsync(string workspaceRoot, CancellationToken cancellationToken = default);

    /// <summary>Reads tasks from <c>.aos/spec/tasks/**</c> under the given workspace root.</summary>
    Task<IReadOnlyList<TaskSummaryDto>> GetTasksAsync(string workspaceRoot, CancellationToken cancellationToken = default);

    /// <summary>Reads the project spec from <c>.aos/spec/project.json</c> under the given workspace root. Returns <c>null</c> if the file does not exist.</summary>
    Task<ProjectSpecDto?> GetProjectAsync(string workspaceRoot, CancellationToken cancellationToken = default);
}
