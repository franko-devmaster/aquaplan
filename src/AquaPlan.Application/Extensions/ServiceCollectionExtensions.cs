using AquaPlan.Application.Services;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AquaPlan.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection WithApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        // Sprint Sec F-006 — singleton: holds the short-lived OIDC exchange codes in memory.
        services.AddSingleton<IOidcCodeExchangeService, OidcCodeExchangeService>();
        return services;
    }
}
