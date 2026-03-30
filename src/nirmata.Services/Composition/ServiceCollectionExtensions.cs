using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using nirmata.Data.Repositories;
using nirmata.Services.Configuration;
using nirmata.Services.Implementations;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddnirmataServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GitHubOptions>(configuration.GetSection(GitHubOptions.SectionName));
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectService, ProjectService>();

        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IWorkspaceBootstrapService, WorkspaceBootstrapService>();
        services.AddScoped<IGitHubRepositoryProvisioningService, GitHubRepositoryProvisioningService>();
        services.AddScoped<IGitHubWorkspaceBootstrapService, GitHubWorkspaceBootstrapService>();

        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();

        services.AddScoped<ISpecService, SpecService>();
        services.AddScoped<IFileSystemService, FileSystemService>();

        services.AddScoped<IStateService, StateService>();
        services.AddScoped<IEvidenceService, EvidenceService>();

        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IUatService, UatService>();

        services.AddScoped<ICodebaseService, CodebaseService>();
        services.AddScoped<IOrchestratorGateService, OrchestratorGateService>();

        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}
