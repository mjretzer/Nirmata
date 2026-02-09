using System.Text;
using Gmsd.Aos.Public;

namespace Gmsd.Agents.Execution.Planning.PhasePlanner.Assumptions;

/// <summary>
/// Default implementation of the phase assumption lister.
/// Extracts and documents assumptions from phase planning.
/// </summary>
public sealed class PhaseAssumptionLister : IPhaseAssumptionLister
{
    private readonly IWorkspace _workspace;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseAssumptionLister"/> class.
    /// </summary>
    public PhaseAssumptionLister(IWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PhaseAssumption>> ExtractAssumptionsAsync(
        PhaseBrief brief,
        TaskPlan taskPlan,
        string runId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(brief);
        ArgumentNullException.ThrowIfNull(taskPlan);
        ArgumentException.ThrowIfNullOrEmpty(runId);

        var assumptions = new List<PhaseAssumption>();
        var assumptionId = 1;

        // Extract assumptions from phase brief scope
        assumptions.AddRange(ExtractScopeAssumptions(brief, ref assumptionId));

        // Extract assumptions from task file scopes
        assumptions.AddRange(ExtractFileScopeAssumptions(brief, taskPlan, ref assumptionId));

        // Extract assumptions from technology choices
        assumptions.AddRange(ExtractTechnologyAssumptions(brief, ref assumptionId));

        // Extract assumptions from dependencies
        assumptions.AddRange(ExtractDependencyAssumptions(taskPlan, ref assumptionId));

        // Extract assumptions from verification approach
        assumptions.AddRange(ExtractVerificationAssumptions(taskPlan, ref assumptionId));

        return Task.FromResult<IReadOnlyList<PhaseAssumption>>(assumptions.AsReadOnly());
    }

    /// <inheritdoc />
    public async Task<string> GenerateAssumptionsDocumentAsync(
        IReadOnlyList<PhaseAssumption> assumptions,
        string runId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(assumptions);
        ArgumentException.ThrowIfNullOrEmpty(runId);

        if (assumptions.Count == 0)
        {
            // Return empty document path when no assumptions
            return string.Empty;
        }

        // Build the assumptions markdown document
        var markdown = BuildAssumptionsMarkdown(assumptions, runId);

        // Determine the path for the assumptions document
        var runEvidencePath = Path.Combine(
            _workspace.AosRootPath,
            "evidence",
            "runs",
            runId,
            "artifacts");

        if (!Directory.Exists(runEvidencePath))
        {
            Directory.CreateDirectory(runEvidencePath);
        }

        var assumptionsPath = Path.Combine(runEvidencePath, "assumptions.md");
        await File.WriteAllTextAsync(assumptionsPath, markdown, ct);

        return assumptionsPath;
    }

    private static List<PhaseAssumption> ExtractScopeAssumptions(PhaseBrief brief, ref int assumptionId)
    {
        var assumptions = new List<PhaseAssumption>();

        // Assumption: Items in scope are correct and complete
        if (brief.Scope.InScope.Count > 0)
        {
            assumptions.Add(new PhaseAssumption
            {
                AssumptionId = $"ASM-{assumptionId++:D3}",
                PhaseId = brief.PhaseId,
                Category = "scope",
                Statement = "The items identified as 'in scope' for this phase are correct and complete.",
                Rationale = "Scope was defined based on phase description and goals. If scope is incorrect, deliverables may be incomplete or incorrect.",
                ImpactIfIncorrect = "Deliverables may not meet requirements; may require rework or additional phases.",
                VerificationApproach = "Review deliverables against original phase goals; stakeholder validation.",
                Source = "phase_brief",
                Confidence = "medium"
            });
        }

        // Assumption: Out of scope items are properly excluded
        if (brief.Scope.OutOfScope.Count > 0)
        {
            assumptions.Add(new PhaseAssumption
            {
                AssumptionId = $"ASM-{assumptionId++:D3}",
                PhaseId = brief.PhaseId,
                Category = "scope",
                Statement = "Items marked as 'out of scope' are correctly excluded and will not be needed for this phase.",
                Rationale = "Explicit scope boundaries prevent scope creep and keep phases focused.",
                ImpactIfIncorrect = "Missing functionality; may require emergency additions or new phase.",
                VerificationApproach = "Review with stakeholders that excluded items are intentionally deferred.",
                Source = "phase_brief",
                Confidence = "medium"
            });
        }

        return assumptions;
    }

    private static List<PhaseAssumption> ExtractFileScopeAssumptions(PhaseBrief brief, TaskPlan taskPlan, ref int assumptionId)
    {
        var assumptions = new List<PhaseAssumption>();

        // Collect all file scopes from tasks
        var allFileScopes = taskPlan.Tasks.SelectMany(t => t.FileScopes).ToList();

        // Assumption about file existence
        var filesToRead = allFileScopes.Where(fs => fs.ScopeType == "read" || fs.MustExist).ToList();
        if (filesToRead.Count > 0)
        {
            assumptions.Add(new PhaseAssumption
            {
                AssumptionId = $"ASM-{assumptionId++:D3}",
                PhaseId = brief.PhaseId,
                Category = "technical",
                Statement = $"The {filesToRead.Count} file(s) marked as 'must exist' or 'read' scope exist and are accessible at the specified paths.",
                Rationale = "File scopes were derived from phase requirements and relevant file analysis. Tasks assume these files contain expected content.",
                ImpactIfIncorrect = "Tasks may fail to compile, tests may fail, or implementation may reference non-existent types.",
                VerificationApproach = "Check file existence before task execution; validate file content matches expectations.",
                Source = "task_plan",
                Confidence = filesToRead.Count > 5 ? "low" : "medium"
            });
        }

        // Assumption about file modifications
        var filesToModify = allFileScopes.Where(fs => fs.ScopeType is "write" or "modify").ToList();
        if (filesToModify.Count > 0)
        {
            assumptions.Add(new PhaseAssumption
            {
                AssumptionId = $"ASM-{assumptionId++:D3}",
                PhaseId = brief.PhaseId,
                Category = "technical",
                Statement = $"The {filesToModify.Count} file(s) marked for modification can be safely modified without breaking existing functionality.",
                Rationale = "File modification scope assumes existing tests and dependencies will remain compatible.",
                ImpactIfIncorrect = "Breaking changes to existing functionality; test failures; integration issues.",
                VerificationApproach = "Run full test suite after modifications; check for breaking changes.",
                Source = "task_plan",
                Confidence = "medium"
            });
        }

        // Assumption about new file creation
        var filesToCreate = allFileScopes.Where(fs => fs.ScopeType == "create").ToList();
        if (filesToCreate.Count > 0)
        {
            assumptions.Add(new PhaseAssumption
            {
                AssumptionId = $"ASM-{assumptionId++:D3}",
                PhaseId = brief.PhaseId,
                Category = "technical",
                Statement = $"The {filesToCreate.Count} new file(s) can be created at the specified paths without conflicts.",
                Rationale = "New file paths were determined based on project conventions and existing structure.",
                ImpactIfIncorrect = "File creation may fail due to conflicts; may require path adjustments.",
                VerificationApproach = "Verify target directories exist and are writable; check for naming conflicts.",
                Source = "task_plan",
                Confidence = "high"
            });
        }

        return assumptions;
    }

    private static List<PhaseAssumption> ExtractTechnologyAssumptions(PhaseBrief brief, ref int assumptionId)
    {
        var assumptions = new List<PhaseAssumption>();

        if (!string.IsNullOrEmpty(brief.ProjectContext.TechnologyStack))
        {
            assumptions.Add(new PhaseAssumption
            {
                AssumptionId = $"ASM-{assumptionId++:D3}",
                PhaseId = brief.PhaseId,
                Category = "technical",
                Statement = $"The technology stack '{brief.ProjectContext.TechnologyStack}' is correctly identified and available in the development environment.",
                Rationale = "Technology stack affects implementation approach, available libraries, and build process.",
                ImpactIfIncorrect = "Implementation may use incompatible patterns or unavailable features.",
                VerificationApproach = "Verify SDK/runtime versions match requirements; check package availability.",
                Source = "phase_brief",
                Confidence = "high"
            });
        }

        // Assumption about architecture patterns
        if (brief.ProjectContext.ArchitecturePatterns.Count > 0)
        {
            assumptions.Add(new PhaseAssumption
            {
                AssumptionId = $"ASM-{assumptionId++:D3}",
                PhaseId = brief.PhaseId,
                Category = "technical",
                Statement = "The identified architecture patterns are correctly applied throughout the codebase.",
                Rationale = "Consistent application of architecture patterns ensures implementation fits existing structure.",
                ImpactIfIncorrect = "New code may not integrate properly; may violate architectural constraints.",
                VerificationApproach = "Review existing code for pattern adherence; validate new code follows patterns.",
                Source = "phase_brief",
                Confidence = "medium"
            });
        }

        return assumptions;
    }

    private static List<PhaseAssumption> ExtractDependencyAssumptions(TaskPlan taskPlan, ref int assumptionId)
    {
        var assumptions = new List<PhaseAssumption>();

        // Collect all task dependencies
        var allDependencies = taskPlan.Tasks.SelectMany(t => t.Dependencies).Distinct().ToList();

        if (allDependencies.Count > 0)
        {
            assumptions.Add(new PhaseAssumption
            {
                AssumptionId = $"ASM-{assumptionId++:D3}",
                PhaseId = taskPlan.PhaseId,
                Category = "technical",
                Statement = $"The {allDependencies.Count} task dependency/dependencies are correctly identified and will be completed before dependent tasks.",
                Rationale = "Task dependencies determine execution order. Incorrect dependencies may lead to attempting tasks before prerequisites are ready.",
                ImpactIfIncorrect = "Tasks may be attempted out of order, leading to errors or incomplete work.",
                VerificationApproach = "Validate dependency task completion status before starting dependent tasks.",
                Source = "task_plan",
                Confidence = "high"
            });
        }

        // Check for external dependencies (packages, services)
        var hasExternalDeps = taskPlan.Tasks.Any(t =>
            t.FileScopes.Any(fs => fs.RelativePath.Contains("package", StringComparison.OrdinalIgnoreCase) ||
                                  fs.RelativePath.Contains("dependency", StringComparison.OrdinalIgnoreCase)));

        if (hasExternalDeps)
        {
            assumptions.Add(new PhaseAssumption
            {
                AssumptionId = $"ASM-{assumptionId++:D3}",
                PhaseId = taskPlan.PhaseId,
                Category = "external",
                Statement = "Required external dependencies (packages, services) are available and compatible.",
                Rationale = "External dependencies may affect build, runtime, or integration capabilities.",
                ImpactIfIncorrect = "Build failures, runtime errors, or incompatible API responses.",
                VerificationApproach = "Verify package versions; test external service availability.",
                Source = "task_plan",
                Confidence = "medium"
            });
        }

        return assumptions;
    }

    private static List<PhaseAssumption> ExtractVerificationAssumptions(TaskPlan taskPlan, ref int assumptionId)
    {
        var assumptions = new List<PhaseAssumption>();

        // Check if tests are expected
        var hasTestVerification = taskPlan.Tasks.SelectMany(t => t.VerificationSteps)
            .Any(v => v.VerificationType == "test");

        if (hasTestVerification)
        {
            assumptions.Add(new PhaseAssumption
            {
                AssumptionId = $"ASM-{assumptionId++:D3}",
                PhaseId = taskPlan.PhaseId,
                Category = "technical",
                Statement = "Test infrastructure is available and tests can be executed to verify implementation.",
                Rationale = "Test-based verification requires working test framework and test environment.",
                ImpactIfIncorrect = "Cannot verify correctness; may miss regressions or defects.",
                VerificationApproach = "Execute tests and verify they run correctly; check test coverage.",
                Source = "task_plan",
                Confidence = "high"
            });
        }

        // Check for compile verification
        var hasCompileVerification = taskPlan.Tasks.SelectMany(t => t.VerificationSteps)
            .Any(v => v.VerificationType == "compile");

        if (hasCompileVerification)
        {
            assumptions.Add(new PhaseAssumption
            {
                AssumptionId = $"ASM-{assumptionId++:D3}",
                PhaseId = taskPlan.PhaseId,
                Category = "technical",
                Statement = "The codebase compiles successfully before and after modifications.",
                Rationale = "Compilation is a basic sanity check. If baseline doesn't compile, modifications can't be verified.",
                ImpactIfIncorrect = "Cannot establish baseline; modifications may compound existing issues.",
                VerificationApproach = "Run compilation before making changes; verify no pre-existing errors.",
                Source = "task_plan",
                Confidence = "high"
            });
        }

        return assumptions;
    }

    private static string BuildAssumptionsMarkdown(IReadOnlyList<PhaseAssumption> assumptions, string runId)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Phase Planning Assumptions");
        builder.AppendLine();
        builder.AppendLine($"- **Run ID:** {runId}");
        builder.AppendLine($"- **Generated:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        builder.AppendLine($"- **Total Assumptions:** {assumptions.Count}");
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine();

        // Group by category
        var groupedAssumptions = assumptions.GroupBy(a => a.Category).OrderBy(g => g.Key);

        foreach (var group in groupedAssumptions)
        {
            builder.AppendLine($"## {char.ToUpper(group.Key[0])}{group.Key[1..]} Assumptions");
            builder.AppendLine();

            foreach (var assumption in group)
            {
                builder.AppendLine($"### {assumption.AssumptionId}: {assumption.Statement}");
                builder.AppendLine();
                builder.AppendLine($"**Rationale:** {assumption.Rationale}");
                builder.AppendLine();

                if (!string.IsNullOrEmpty(assumption.ImpactIfIncorrect))
                {
                    builder.AppendLine($"**Impact if Incorrect:** {assumption.ImpactIfIncorrect}");
                    builder.AppendLine();
                }

                if (!string.IsNullOrEmpty(assumption.VerificationApproach))
                {
                    builder.AppendLine($"**Verification Approach:** {assumption.VerificationApproach}");
                    builder.AppendLine();
                }

                builder.AppendLine($"- **Source:** {assumption.Source}");
                builder.AppendLine($"- **Confidence:** {assumption.Confidence}");
                builder.AppendLine($"- **Phase:** {assumption.PhaseId}");
                builder.AppendLine();
                builder.AppendLine("---");
                builder.AppendLine();
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("This document captures the key assumptions made during phase planning. ");
        builder.AppendLine("Each assumption should be verified during execution to ensure the plan remains valid. ");
        builder.AppendLine("If any assumption proves incorrect, the plan may need to be revised.");
        builder.AppendLine();

        return builder.ToString();
    }
}
