using System.ComponentModel.DataAnnotations; // Namespace for data validation attributes
using System.ComponentModel.DataAnnotations.Schema; // Namespace for database schema mapping attributes

namespace nirmata.Data.Entities.Projects; // Defines the logical grouping for the project models

[Table("Project")] // Specifies the database table name this class maps to
public class Project // Represents a project entity in the system
{
    [Key] // Marks this property as the primary key for the database table
    public required string ProjectId { get; set; } // Unique identifier for the project

    [Required] // Specifies that the Name property is mandatory
    [MaxLength(200)] // Restricts the Name to a maximum of 200 characters
    public required string Name { get; set; } // The display name of the project

    public virtual ICollection<Step> Steps { get; set; } = new List<Step>(); // A collection of related steps, enabling a one-to-many relationship
}
