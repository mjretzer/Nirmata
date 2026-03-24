using System.ComponentModel.DataAnnotations;

namespace nirmata.Data.Dto.Requests.Issues;

/// <summary>
/// Request body for patching only the status field of a workspace issue.
/// Valid values: <c>open</c>, <c>investigating</c>, <c>resolved</c>, <c>deferred</c>, <c>wontfix</c>.
/// </summary>
public sealed class IssueStatusUpdateRequest
{
    [Required]
    public required string Status { get; init; }
}
