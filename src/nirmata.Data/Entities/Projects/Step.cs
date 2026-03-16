using System.ComponentModel.DataAnnotations; // Namespace for data validation attributes
using System.ComponentModel.DataAnnotations.Schema; // Namespace for database schema mapping attributes

namespace nirmata.Data.Entities.Projects; // Defines the logical grouping for the project models

[Table("Step")] // Specifies the database table name this class maps to
public class Step // Represents a step entity within a project
{
    [Key] // Marks this property as the primary key for the database table
    public required string StepId { get; set; } // Unique identifier for the step

    [Required] // Specifies that the Name property is mandatory
    [MaxLength(200)] // Restricts the Name to a maximum of 200 characters
    public required string Name { get; set; } // The display name of the step

    [Required] // Specifies that the ProjectId is mandatory for association
    public required string ProjectId { get; set; } // Foreign key property that links this step to a specific project

    [ForeignKey(nameof(ProjectId))] // Explicitly defines the foreign key relationship using the ProjectId property
    public required virtual Project Project { get; set; } // Navigation property to access the parent project entity
}
