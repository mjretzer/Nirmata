using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Gmsd.Aos.Contracts.State;

/// <summary>
/// Represents a structured phase plan containing decomposed tasks.
/// Enforced via JSON schema and DataAnnotations.
/// </summary>
public sealed class PhasePlan
{
    /// <summary>
    /// The unique identifier for this plan.
    /// </summary>
    [Required]
    public required string PlanId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The phase identifier this plan is for.
    /// </summary>
    [Required]
    public required string PhaseId { get; init; }

    /// <summary>
    /// The list of tasks decomposed for this phase.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one task is required in the plan")]
    public required List<PhaseTask> Tasks { get; init; } = new();

    /// <summary>
    /// Validates the plan structure.
    /// </summary>
    public ValidationResult Validate()
    {
        var context = new ValidationContext(this);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        
        bool isValid = Validator.TryValidateObject(this, context, results, true);
        
        // Deep validation for tasks
        if (Tasks != null)
        {
            foreach (var task in Tasks)
            {
                var taskContext = new ValidationContext(task);
                var taskResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
                if (!Validator.TryValidateObject(task, taskContext, taskResults, true))
                {
                    isValid = false;
                    results.AddRange(taskResults.Select(r => new System.ComponentModel.DataAnnotations.ValidationResult(
                        $"Task {task.Id}: {r.ErrorMessage}", r.MemberNames)));
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
/// Represents a single task within a phase plan.
/// </summary>
public sealed class PhaseTask
{
    /// <summary>
    /// Unique identifier for the task (e.g., TSK-001).
    /// </summary>
    [Required]
    public required string Id { get; init; }

    /// <summary>
    /// Short title of the task.
    /// </summary>
    [Required]
    [MinLength(5)]
    [MaxLength(100)]
    public required string Title { get; init; }

    /// <summary>
    /// Detailed description of the task work.
    /// </summary>
    [Required]
    [MinLength(10)]
    public required string Description { get; init; }

    /// <summary>
    /// File scope entries for this task.
    /// </summary>
    [Required]
    public List<PhaseFileScope> FileScopes { get; init; } = new();

    /// <summary>
    /// Steps to verify the task completion.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one verification step is required")]
    public List<string> VerificationSteps { get; init; } = new();
}

/// <summary>
/// Represents a canonical file scope object in phase/task plans.
/// </summary>
public sealed class PhaseFileScope
{
    /// <summary>
    /// Workspace-relative path in scope for the task.
    /// </summary>
    [Required]
    public required string Path { get; init; }
}
