using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gmsd.Data.Model.Projects;

[Table("Step")]
public class Step
{
    [Key]
    public string StepId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; }

    [Required]
    public string ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; }
}
