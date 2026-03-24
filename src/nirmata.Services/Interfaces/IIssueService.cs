using nirmata.Data.Dto.Models.Spec;
using nirmata.Data.Dto.Requests.Issues;

namespace nirmata.Services.Interfaces;

public interface IIssueService
{
    /// <summary>
    /// Lists all issues from <c>.aos/spec/issues/</c> under the given workspace root.
    /// Optional filter parameters are applied when provided; omitted parameters are ignored.
    /// </summary>
    Task<IReadOnlyList<IssueDto>> GetAllAsync(
        string workspaceRoot,
        string? status = null,
        string? severity = null,
        string? phaseId = null,
        string? taskId = null,
        string? milestoneId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a single issue by ID from <c>.aos/spec/issues/{issueId}.json</c>.
    /// Returns <c>null</c> if the file does not exist.
    /// </summary>
    Task<IssueDto?> GetByIdAsync(
        string workspaceRoot,
        string issueId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new issue under <c>.aos/spec/issues/</c> and returns the persisted record.
    /// Assigns the next available <c>ISS-####</c> identifier automatically.
    /// </summary>
    Task<IssueDto> CreateAsync(
        string workspaceRoot,
        IssueCreateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the mutable fields of an existing issue.
    /// Returns the updated record, or <c>null</c> if the issue does not exist.
    /// </summary>
    Task<IssueDto?> UpdateAsync(
        string workspaceRoot,
        string issueId,
        IssueUpdateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an issue record.
    /// Returns <c>true</c> if the issue existed and was removed; <c>false</c> if not found.
    /// </summary>
    Task<bool> DeleteAsync(
        string workspaceRoot,
        string issueId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only the <c>status</c> field of an existing issue.
    /// Returns the updated record, or <c>null</c> if the issue does not exist.
    /// </summary>
    Task<IssueDto?> UpdateStatusAsync(
        string workspaceRoot,
        string issueId,
        string status,
        CancellationToken cancellationToken = default);
}
