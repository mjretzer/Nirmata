using nirmata.Data.Repositories;
using nirmata.Services.Implementations;
using nirmata.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace nirmata.Services.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddnirmataServices(this IServiceCollection services)
    {
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectService, ProjectService>();

        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();

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
