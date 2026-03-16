using System.ComponentModel.DataAnnotations;

namespace nirmata.Aos.Contracts.State;

/// <summary>
/// Represents a structured fix plan mapping issues to proposed changes.
/// Enforced via JSON schema and DataAnnotations.
/// </summary>
public sealed class FixPlan
{
    /// <summary>
    /// The list of fixes proposed.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one fix is required in the plan")]
    public required List<FixEntry> Fixes { get; init; } = new();

    /// <summary>
    /// Validates the plan structure.
    /// </summary>
    public ValidationResult Validate()
    {
        var context = new ValidationContext(this);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        
        bool isValid = Validator.TryValidateObject(this, context, results, true);
        
        // Deep validation for fixes
        if (Fixes != null)
        {
            foreach (var fix in Fixes)
            {
                var fixContext = new ValidationContext(fix);
                var fixResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
                if (!Validator.TryValidateObject(fix, fixContext, fixResults, true))
                {
                    isValid = false;
                    results.AddRange(fixResults.Select(r => new System.ComponentModel.DataAnnotations.ValidationResult(
                        $"Fix {fix.IssueId}: {r.ErrorMessage}", r.MemberNames)));
                }
                
                // Deep validation for ProposedChanges within fixes
                if (fix.ProposedChanges != null)
                {
                    foreach (var change in fix.ProposedChanges)
                    {
                        var changeContext = new ValidationContext(change);
                        var changeResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
                        if (!Validator.TryValidateObject(change, changeContext, changeResults, true))
                        {
                            isValid = false;
                            results.AddRange(changeResults.Select(r => new System.ComponentModel.DataAnnotations.ValidationResult(
                                $"Fix {fix.IssueId} Change {change.File}: {r.ErrorMessage}", r.MemberNames)));
                        }
                    }
                }
            }
        }
        
        if (!isValid)
        {
            var errors = results.Select(r => r.ErrorMessage ?? "Validation failed").ToList();
            return new ValidationResult { IsValid = false, Errors = errors };
        }

        return new ValidationResult { IsValid = true };
    }
}

/// <summary>
/// Represents a fix for a specific issue.
/// </summary>
public sealed class FixEntry
{
    /// <summary>
    /// The ID of the issue being fixed.
    /// </summary>
    [Required]
    public required string IssueId { get; init; }

    /// <summary>
    /// High-level description of the fix strategy.
    /// </summary>
    [Required]
    [MinLength(10)]
    public required string Description { get; init; }

    /// <summary>
    /// Specific file changes proposed.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one proposed change is required")]
    public required List<ProposedChange> ProposedChanges { get; init; } = new();

    /// <summary>
    /// Tests to run to verify the fix.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one test or verification step is required")]
    public required List<string> Tests { get; init; } = new();
}

/// <summary>
/// Represents a proposed change to a file.
/// </summary>
public sealed class ProposedChange
{
    /// <summary>
    /// The file path to be changed.
    /// </summary>
    [Required]
    public required string File { get; init; }

    /// <summary>
    /// Description of the change (e.g., "Add null check", "Refactor method").
    /// </summary>
    [Required]
    public required string ChangeDescription { get; init; }
}
