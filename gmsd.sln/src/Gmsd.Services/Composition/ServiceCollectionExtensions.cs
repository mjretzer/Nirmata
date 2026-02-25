using Gmsd.Services.Implementations;
using Gmsd.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Gmsd.Services.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGmsdServices(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();

        return services;
    }
}
