using System.ComponentModel.DataAnnotations;

namespace nirmata.Data.Dto.Requests.Projects;

/// <summary>
/// Data Transfer Object for updating an existing project.
/// </summary>
public sealed class ProjectUpdateRequestDto
{
    /// <summary>
    /// Gets or sets the updated name of the project.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the updated description of the project.
    /// </summary>
    public string? Description { get; init; }
}
