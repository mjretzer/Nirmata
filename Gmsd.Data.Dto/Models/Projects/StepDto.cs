using System.ComponentModel.DataAnnotations; // Attributes for data validation
using System.ComponentModel.DataAnnotations.Schema; // Attributes for database schema mapping

namespace Gmsd.Data.Dto.Models.Projects; // Organized under the Projects DTO namespace

public class StepDto // DTO for transferring Step data
{
    public required string StepId { get; init; } // Unique identifier for the Step
    public required string Name { get; init; } // Descriptive name of the Step
}
