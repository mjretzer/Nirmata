using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Public.Catalogs;
using System.Text.RegularExpressions;

using nirmata.Aos.Public.Models;
using nirmata.Aos.Public.Services;

namespace nirmata.Aos.Engine.Commands.Spec;

/// <summary>
/// Handler for openspec validate commands.
/// Validates OpenSpec change proposals and specifications.
/// </summary>
internal sealed class OpenspecValidateCommandHandler : ICommandHandler
{
    /// <inheritdoc />
    public CommandMetadata Metadata { get; } = new()
    {
        Group = "core",
        Command = "openspec-validate",
        Id = CommandIds.Validate,
        Description = "Validate OpenSpec changes and specs with --strict mode support."
    };

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        try
        {
            var isStrict = context.Arguments.Contains("--strict") || context.Options.ContainsKey("strict");
            var changeId = context.Arguments.FirstOrDefault(a => !a.StartsWith("--"));

            if (string.IsNullOrEmpty(changeId))
            {
                // Bulk validation mode - validate all changes
                return HandleBulkValidation(context, isStrict);
            }

            return HandleSingleValidation(context, changeId, isStrict);
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Failure(
                $"OpenSpec validation failed: {ex.Message}",
                1
            ));
        }
    }

    private static Task<CommandResult> HandleBulkValidation(CommandContext context, bool isStrict)
    {
        var changesDir = Path.Combine(context.Workspace.RepositoryRootPath, "openspec", "changes");
        if (!Directory.Exists(changesDir))
        {
            return Task.FromResult(CommandResult.Failure(
                "OpenSpec changes directory not found.",
                1
            ));
        }

        var issues = new List<ValidationIssue>();
        var changeDirs = Directory.GetDirectories(changesDir)
            .Where(d => !d.EndsWith("archive"))
            .ToList();

        foreach (var changeDir in changeDirs)
        {
            var changeId = Path.GetFileName(changeDir);
            var changeIssues = ValidateChange(changeDir, changeId, isStrict);
            issues.AddRange(changeIssues);
        }

        return BuildValidationResult(issues, isStrict);
    }

    private static Task<CommandResult> HandleSingleValidation(CommandContext context, string changeId, bool isStrict)
    {
        var changeDir = Path.Combine(context.Workspace.RepositoryRootPath, "openspec", "changes", changeId);
        if (!Directory.Exists(changeDir))
        {
            // Check archive
            changeDir = Path.Combine(context.Workspace.RepositoryRootPath, "openspec", "changes", "archive", changeId);
            if (!Directory.Exists(changeDir))
            {
                return Task.FromResult(CommandResult.Failure(
                    $"Change '{changeId}' not found in changes or archive.",
                    1
                ));
            }
        }

        var issues = ValidateChange(changeDir, changeId, isStrict);
        return BuildValidationResult(issues, isStrict);
    }

    private static List<ValidationIssue> ValidateChange(string changeDir, string changeId, bool isStrict)
    {
        var issues = new List<ValidationIssue>();

        // Check required files
        var proposalPath = Path.Combine(changeDir, "proposal.md");
        var tasksPath = Path.Combine(changeDir, "tasks.md");

        if (!File.Exists(proposalPath))
        {
            issues.Add(new ValidationIssue(changeId, "Missing proposal.md", ValidationSeverity.Error));
        }
        else
        {
            issues.AddRange(ValidateProposal(proposalPath, changeId, isStrict));
        }

        if (!File.Exists(tasksPath))
        {
            issues.Add(new ValidationIssue(changeId, "Missing tasks.md", ValidationSeverity.Error));
        }
        else
        {
            issues.AddRange(ValidateTasks(tasksPath, changeId, isStrict));
        }

        // Validate spec deltas
        var specsDir = Path.Combine(changeDir, "specs");
        if (Directory.Exists(specsDir))
        {
            var specFiles = Directory.GetFiles(specsDir, "*.md", SearchOption.AllDirectories);
            foreach (var specFile in specFiles)
            {
                issues.AddRange(ValidateSpecDelta(specFile, changeId, isStrict));
            }
        }
        else if (isStrict)
        {
            issues.Add(new ValidationIssue(changeId, "Missing specs/ directory (required in strict mode)", ValidationSeverity.Warning));
        }

        return issues;
    }

    private static List<ValidationIssue> ValidateProposal(string proposalPath, string changeId, bool isStrict)
    {
        var issues = new List<ValidationIssue>();
        var content = File.ReadAllText(proposalPath);

        // Check title format
        if (!content.StartsWith("# Change:"))
        {
            issues.Add(new ValidationIssue(changeId, "Proposal must start with '# Change: ' title", ValidationSeverity.Error));
        }

        // Check required sections
        var requiredSections = new[] { "## Why", "## What Changes", "## Impact" };
        foreach (var section in requiredSections)
        {
            if (!content.Contains(section))
            {
                issues.Add(new ValidationIssue(changeId, $"Missing required section: {section}", ValidationSeverity.Error));
            }
        }

        // Check ADDED/MODIFIED/REMOVED markers in What Changes
        if (content.Contains("## What Changes") && isStrict)
        {
            var whatChangesMatch = Regex.Match(content, @"## What Changes\s*\n(.*?)(?=\n## |$)", RegexOptions.Singleline);
            if (whatChangesMatch.Success)
            {
                var whatChangesContent = whatChangesMatch.Groups[1].Value;
                if (!Regex.IsMatch(whatChangesContent, @"(ADDED|MODIFIED|REMOVED|BREAKING)"))
                {
                    issues.Add(new ValidationIssue(changeId, "What Changes section should use ADDED/MODIFIED/REMOVED/BREAKING markers", ValidationSeverity.Warning));
                }
            }
        }

        return issues;
    }

    private static List<ValidationIssue> ValidateTasks(string tasksPath, string changeId, bool isStrict)
    {
        var issues = new List<ValidationIssue>();
        var content = File.ReadAllText(tasksPath);

        // Check for task sections
        if (!content.Contains("## "))
        {
            issues.Add(new ValidationIssue(changeId, "Tasks file should have numbered sections (## 1. Section)", ValidationSeverity.Error));
        }

        // Check for checklist items
        if (!content.Contains("- [ ]") && !content.Contains("- [x]"))
        {
            issues.Add(new ValidationIssue(changeId, "Tasks should use checkbox format '- [ ]' or '- [x]'", ValidationSeverity.Warning));
        }

        return issues;
    }

    private static List<ValidationIssue> ValidateSpecDelta(string specPath, string changeId, bool isStrict)
    {
        var issues = new List<ValidationIssue>();
        var content = File.ReadAllText(specPath);

        // Check for delta operation headers
        var deltaPattern = @"##\s*(ADDED|MODIFIED|REMOVED|RENAMED)\s*Requirements";
        if (!Regex.IsMatch(content, deltaPattern, RegexOptions.IgnoreCase))
        {
            issues.Add(new ValidationIssue(changeId, $"Spec delta {Path.GetFileName(specPath)} missing ADDED/MODIFIED/REMOVED/RENAMED Requirements section", ValidationSeverity.Error));
        }

        // Check for requirements
        var requirementPattern = @"###\s*Requirement:";
        if (!Regex.IsMatch(content, requirementPattern))
        {
            issues.Add(new ValidationIssue(changeId, $"Spec delta {Path.GetFileName(specPath)} has no ### Requirement: entries", ValidationSeverity.Error));
        }

        // Check for scenarios with proper format (#### Scenario:)
        var scenarioPattern = @"####\s*Scenario:";
        var scenarios = Regex.Matches(content, scenarioPattern);
        if (scenarios.Count == 0)
        {
            issues.Add(new ValidationIssue(changeId, $"Spec delta {Path.GetFileName(specPath)} has no #### Scenario: entries (4 hashtags required)", ValidationSeverity.Error));
        }

        // In strict mode, ensure every requirement has at least one scenario
        if (isStrict)
        {
            var requirementMatches = Regex.Matches(content, @"###\s*Requirement:(.*?)(?=###\s*Requirement:|##\s*|$)", RegexOptions.Singleline);
            foreach (Match reqMatch in requirementMatches)
            {
                var reqContent = reqMatch.Value;
                if (!reqContent.Contains("#### Scenario:"))
                {
                    var reqName = Regex.Match(reqContent, @"###\s*Requirement:\s*(.+)")?.Groups[1]?.Value?.Trim() ?? "Unknown";
                    issues.Add(new ValidationIssue(changeId, $"Requirement '{reqName}' missing #### Scenario: entry", ValidationSeverity.Error));
                }
            }
        }

        return issues;
    }

    private static Task<CommandResult> BuildValidationResult(List<ValidationIssue> issues, bool isStrict)
    {
        var errors = issues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
        var warnings = issues.Where(i => i.Severity == ValidationSeverity.Warning).ToList();

        if (errors.Count == 0 && warnings.Count == 0)
        {
            return Task.FromResult(CommandResult.Success("OpenSpec validation passed."));
        }

        var lines = new List<string>();
        lines.Add($"OpenSpec validation completed with {errors.Count} error(s) and {warnings.Count} warning(s):");
        lines.Add("");

        foreach (var error in errors)
        {
            lines.Add($"  [ERROR] [{error.ChangeId}] {error.Message}");
        }

        foreach (var warning in warnings)
        {
            lines.Add($"  [WARN]  [{warning.ChangeId}] {warning.Message}");
        }

        var output = string.Join("\n", lines);
        var exitCode = errors.Count > 0 ? 1 : 0;

        return Task.FromResult(exitCode > 0
            ? CommandResult.Failure(output, exitCode)
            : CommandResult.Success(output));
    }

    private sealed record ValidationIssue(string ChangeId, string Message, ValidationSeverity Severity);

    private enum ValidationSeverity
    {
        Error,
        Warning
    }
}
