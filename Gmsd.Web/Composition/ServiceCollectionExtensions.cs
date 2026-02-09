using Gmsd.Agents.Configuration;
using Gmsd.Web.AgentRunner;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Gmsd.Web.Composition;

/// <summary>
/// Extension methods for registering GMSD services with the Web application.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string DefaultWorkspacePathConfigKey = "GmsdAgents:WorkspacePath";

    private static string? TryGetSelectedWorkspacePath()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var configPath = Path.Combine(appData, "Gmsd", "workspace-config.json");
            if (!File.Exists(configPath))
            {
                return null;
            }

            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("selectedWorkspacePath", out var prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                var path = prop.GetString();
                return string.IsNullOrWhiteSpace(path) ? null : path;
            }
        }
        catch
        {
            // Ignore selection config errors; fall back to configured workspace.
        }

        return null;
    }

    /// <summary>
    /// Adds GMSD Agents services to the dependency injection container for direct agent execution.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration to bind Agents options from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGmsdAgents(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolve workspace path:
        // - Prefer the selected workspace (Workspace page) so Orchestrator is grounded in the same .aos tree.
        // - Fall back to configured path.
        // - Finally fall back to a temp workspace.
        var workspacePath = TryGetSelectedWorkspacePath() ?? configuration[DefaultWorkspacePathConfigKey];
        if (string.IsNullOrEmpty(workspacePath))
        {
            workspacePath = Path.Combine(Path.GetTempPath(), "gmsd-web-runs");
        }

        // Ensure the workspace directory exists
        Directory.CreateDirectory(workspacePath);

        // Register GMSD Agents services with the resolved workspace path
        services.AddGmsdAgents(workspacePath, configuration);

        // Register WorkflowClassifier for in-process agent execution from Web UI
        services.AddSingleton<WorkflowClassifier>();

        return services;
    }
}
