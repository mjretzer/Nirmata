using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gmsd.Data.Dto.Model.Projects;


public class ProjectDto
{
    public string ProjectId { get; set; }

    public string Name { get; set; }

    public ICollection<StepDto> Steps { get; set; }
}
