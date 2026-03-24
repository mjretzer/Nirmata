using nirmata.Data.Dto.Models.Evidence;

namespace nirmata.Services.Interfaces;

public interface IEvidenceService
{
    /// <summary>Lists all run summaries from <c>.aos/evidence/runs/</c>, newest first. Returns an empty list if the directory does not exist.</summary>
    Task<IReadOnlyList<RunSummaryDto>> GetRunsAsync(string workspaceRoot, CancellationToken cancellationToken = default);

    /// <summary>Reads a single run folder by id, including artifact and log file names. Returns <c>null</c> if the run does not exist.</summary>
    Task<RunDetailDto?> GetRunAsync(string workspaceRoot, string runId, CancellationToken cancellationToken = default);
}
