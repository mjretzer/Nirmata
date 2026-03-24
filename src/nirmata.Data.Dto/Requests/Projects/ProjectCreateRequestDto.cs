using System.ComponentModel.DataAnnotations;

namespace nirmata.Data.Dto.Requests.Projects;

/// <summary>
/// Data Transfer Object for creating a new project.
/// </summary>
public sealed class ProjectCreateRequestDto
{
    /// <summary>
    /// Gets or sets the name of the project.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }
}
