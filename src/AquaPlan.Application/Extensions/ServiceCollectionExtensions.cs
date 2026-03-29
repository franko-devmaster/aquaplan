using AquaPlan.Application.Services;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AquaPlan.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection WithApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
