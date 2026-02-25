using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Gmsd.Aos.Contracts.Commands;

/// <summary>
/// Represents a structured command proposal from an agent.
/// Enforced via JSON schema and DataAnnotations.
/// </summary>
public sealed class CommandIntentProposal
{
    /// <summary>
    /// Schema version of this command proposal contract.
    /// </summary>
    [Required]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// The intent object describing the proposed action.
    /// </summary>
    [Required]
    public required CommandIntent Intent { get; init; }

    /// <summary>
    /// The suggested command string (e.g., "/execute").
    /// </summary>
    [Required]
    [RegularExpression(@"^/[a-z-]+$", ErrorMessage = "Command must start with / and contain only lowercase letters and hyphens")]
    public required string Command { get; init; }

    /// <summary>
    /// The command group (e.g., "run", "spec").
    /// </summary>
    [Required]
    public required string Group { get; init; }

    /// <summary>
    /// A short explanation of why this action is chosen.
    /// </summary>
    [Required]
    [MinLength(10)]
    public required string Rationale { get; init; }

    /// <summary>
    /// What the agent expects to happen after execution.
    /// </summary>
    [Required]
    [MinLength(10)]
    public required string ExpectedOutcome { get; init; }

    /// <summary>
    /// Validates the proposal structure.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();
        
        bool isValid = Validator.TryValidateObject(this, context, results, true);
        
        errors = results.Select(r => r.ErrorMessage ?? "Validation failed").ToList();
        return isValid;
    }
}

/// <summary>
/// Detailed intent description.
/// </summary>
public sealed class CommandIntent
{
    /// <summary>
    /// Primary goal of the command (e.g., "Run task execution", "Create phase plan").
    /// </summary>
    [Required]
    public required string Goal { get; init; }
    
    /// <summary>
    /// Arguments or parameters for the command.
    /// </summary>
    public Dictionary<string, string> Parameters { get; init; } = new();
}
