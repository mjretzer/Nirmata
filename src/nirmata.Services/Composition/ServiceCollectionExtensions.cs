using nirmata.Services.Implementations;
using nirmata.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace nirmata.Services.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddnirmataServices(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();

        return services;
    }
}
