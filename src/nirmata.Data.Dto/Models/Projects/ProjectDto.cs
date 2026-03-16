using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace nirmata.Data.Dto.Models.Projects;

/// <summary>
/// Data Transfer Object representing a Project.
/// </summary>
public class ProjectDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the project.
    /// </summary>
    public required string ProjectId { get; init; }

    /// <summary>
    /// Gets or sets the name of the project.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the collection of steps associated with this project.
    /// </summary>
    public ICollection<StepDto> Steps { get; init; } = new List<StepDto>();
}
