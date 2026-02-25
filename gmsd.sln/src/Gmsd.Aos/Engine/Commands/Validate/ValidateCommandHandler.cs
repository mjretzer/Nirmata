using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Engine.Validation;
using Gmsd.Aos.Engine.Workspace;
using Gmsd.Aos.Public.Catalogs;

using Gmsd.Aos.Public.Models;
using Gmsd.Aos.Public.Services;

namespace Gmsd.Aos.Engine.Commands.Validate;

/// <summary>
/// Handler for validate commands.
/// </summary>
internal sealed class ValidateCommandHandler : ICommandHandler
{
    /// <inheritdoc />
    public CommandMetadata Metadata { get; } = new()
    {
        Group = "core",
        Command = "validate",
        Id = CommandIds.Validate,
        Description = "Validate schemas, workspace, or configuration."
    };

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        try
        {
            if (context.Arguments.Count == 0)
            {
                return Task.FromResult(CommandResult.Failure(
                    "Usage: validate <schemas|workspace|spec|config>",
                    1
                ));
            }

            var subcommand = context.Arguments[0];
            return subcommand.ToLowerInvariant() switch
            {
                "schemas" => HandleValidateSchemas(context),
                "workspace" => HandleValidateWorkspace(context),
                "spec" => HandleValidateSpec(context),
                "config" => HandleValidateConfig(context),
                _ => Task.FromResult(CommandResult.Failure(
                    $"Unknown validate subcommand: {subcommand}",
                    1
                ))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Failure(
                $"Validation failed: {ex.Message}",
                1
            ));
        }
    }

    private static Task<CommandResult> HandleValidateSchemas(CommandContext context)
    {
        // TODO: Implement actual schema validation
        return Task.FromResult(CommandResult.Success("Schema validation: OK (not implemented)"));
    }

    private static Task<CommandResult> HandleValidateWorkspace(CommandContext context)
    {
        var compliance = AosWorkspaceBootstrapper.CheckCompliance(context.Workspace.RepositoryRootPath);

        if (compliance.IsCompliant)
        {
            return Task.FromResult(CommandResult.Success("Workspace validation: OK"));
        }

        var issues = new List<string>();
        issues.AddRange(compliance.MissingDirectories.Select(d => $"Missing directory: {d}"));
        issues.AddRange(compliance.InvalidDirectories.Select(d => $"Invalid directory: {d}"));
        issues.AddRange(compliance.MissingFiles.Select(f => $"Missing file: {f}"));
        issues.AddRange(compliance.InvalidFiles.Select(f => $"Invalid file: {f}"));
        issues.AddRange(compliance.ExtraTopLevelEntries.Select(e => $"Extra entry: {e}"));

        var issuesText = issues.Count == 0
            ? "Unknown compliance issues"
            : string.Join("\n", issues.Select(i => $"  - {i}"));

        return Task.FromResult(CommandResult.Failure(
            $"Workspace validation failed:\n{issuesText}",
            1
        ));
    }

    private static Task<CommandResult> HandleValidateSpec(CommandContext context)
    {
        var report = AosWorkspaceValidator.Validate(
            context.Workspace.RepositoryRootPath,
            [AosWorkspaceLayer.Spec]
        );

        if (report.Issues.Count == 0)
        {
            return Task.FromResult(CommandResult.Success("Spec validation: OK"));
        }

        var issues = report.Issues.Select(i =>
            $"FAIL [{i.Layer?.ToString().ToLowerInvariant() ?? "unknown"}] {i.ContractPath} - {i.Message}");

        return Task.FromResult(CommandResult.Failure(
            $"Spec validation failed:\n{string.Join("\n", issues)}",
            1
        ));
    }

    private static Task<CommandResult> HandleValidateConfig(CommandContext context)
    {
        // TODO: Implement actual config validation
        return Task.FromResult(CommandResult.Success("Config validation: OK (not implemented)"));
    }
}
