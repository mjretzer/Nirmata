namespace nirmata.Data.Dto.Requests.Projects;

/// <summary>
/// Data Transfer Object for searching projects with pagination.
/// </summary>
public sealed class ProjectSearchRequestDto
{
    /// <summary>
    /// Gets or sets the search term to filter projects by name.
    /// </summary>
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Gets or sets the 1-based page number.
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    public int PageSize { get; init; } = 20;
}
