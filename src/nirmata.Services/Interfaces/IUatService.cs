using nirmata.Data.Dto.Models.Spec;

namespace nirmata.Services.Interfaces;

public interface IUatService
{
    /// <summary>
    /// Returns all UAT records from <c>.aos/spec/uat/UAT-*.json</c> and
    /// <c>.aos/spec/tasks/TSK-*/uat.json</c> under the given workspace root,
    /// together with derived pass/fail summaries per task and phase.
    /// </summary>
    Task<UatSummaryDto> GetSummaryAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default);
}
