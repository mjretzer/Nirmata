using System.ComponentModel.DataAnnotations;

namespace nirmata.Data.Dto.Requests.Projects;

public sealed class ProjectCreateRequestDto
{
    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }
}
