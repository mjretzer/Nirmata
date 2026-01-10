using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gmsd.Data.Dto.Model.Projects;


public class StepDto
{
    public string StepId { get; set; }
    public string Name { get; set; }
}
