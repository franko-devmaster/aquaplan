using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Security;
using AquaPlan.Infrastructure.Services;
using AquaPlan.Infrastructure.Services.MockLims;
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
        services.AddScoped<ISectorService, SectorService>();
        services.AddScoped<IOidcUserService, OidcUserService>();
        services.AddScoped<IAnalysisProfileService, AnalysisProfileService>();
        services.AddScoped<IAnalysisProgramService, AnalysisProgramService>();
        services.AddScoped<IContainerService, ContainerService>();
        services.AddScoped<ISamplingLocationChangeRequestService, SamplingLocationChangeRequestService>();
        services.AddScoped<IDelegationService, DelegationService>();
        services.AddScoped<ISamplingPlanService, SamplingPlanService>();
        services.AddScoped<ISamplingRoundService, SamplingRoundService>();
        services.AddScoped<ISamplingService, SamplingService>();
        services.AddScoped<IOrderAuditService, OrderAuditService>();

        // AQ-32 / AQ-33 — Mock LIMS
        services.AddSingleton<IMockLimsResultGenerator, MockLimsResultGenerator>();
        services.AddScoped<IMockLimsService, MockLimsService>();

        // AQ-34 / AQ-35 — Mock LIMS inbound + sync journal
        services.AddScoped<ILimsResultService, LimsResultService>();
        services.AddScoped<ILimsSyncService, LimsSyncService>();

        // AQ-404 — Retroactive backfill of Transmitted orders without LimsOrderId
        services.AddScoped<IMockLimsBackfillService, MockLimsBackfillService>();

        // AQ-43 — Notifications (mock email + in-app bell)
        services.AddScoped<INotificationService, NotificationService>();

        // AQ-415 — Results screen (LDP × dates matrix + recent zone)
        services.AddScoped<IResultsService, ResultsService>();

        return services;
    }
}
