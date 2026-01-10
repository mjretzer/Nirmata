using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gmsd.Data.Model.Projects;

[Table("Project")]
public class Project
{
    [Key]
    public string ProjectId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; }
}
