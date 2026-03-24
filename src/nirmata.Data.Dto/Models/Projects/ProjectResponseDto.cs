namespace nirmata.Data.Dto.Models.Projects;

/// <summary>
/// Data Transfer Object representing a response after project operations.
/// </summary>
public sealed class ProjectResponseDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the project.
    /// </summary>
    public required string ProjectId { get; init; }

    /// <summary>
    /// Gets or sets the name of the project.
    /// </summary>
    public required string Name { get; init; }
}
