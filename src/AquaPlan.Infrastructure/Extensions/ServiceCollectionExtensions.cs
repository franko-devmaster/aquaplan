using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Security;
using AquaPlan.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AquaPlan.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection WithInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AquaPlanDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            options.UseSnakeCaseNamingConvention();
        });

        services.AddIdentity<AppUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AquaPlanDbContext>()
        .AddDefaultTokenProviders();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderStatusService, OrderStatusService>();
        services.AddScoped<ISamplingLocationService, SamplingLocationService>();
        services.AddScoped<IDistributorService, DistributorService>();
        services.AddScoped<IOidcUserService, OidcUserService>();
        services.AddScoped<IAnalysisProfileService, AnalysisProfileService>();
        services.AddScoped<IAnalysisProgramService, AnalysisProgramService>();
        services.AddScoped<ISamplingLocationChangeRequestService, SamplingLocationChangeRequestService>();

        return services;
    }
}
