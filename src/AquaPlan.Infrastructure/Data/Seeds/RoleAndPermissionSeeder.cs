using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Data.Seeds;

public static class RoleAndPermissionSeeder
{
    /// <summary>Configuration key (env var) holding the initial admin e-mail for non-dev environments.</summary>
    public const string InitialAdminEmailKey = "INITIAL_ADMIN_EMAIL";

    /// <summary>Configuration key (env var) holding the initial admin password for non-dev environments.</summary>
    public const string InitialAdminPasswordKey = "INITIAL_ADMIN_PASSWORD";

    private const string DefaultDevAdminEmail = "admin@aquaplan.ch";
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Seeds roles, permissions, the default tenant, and the admin account.
    /// Sprint Sec F-002 — <paramref name="seedDefaultDevAdmin"/> must only be true in
    /// Development/Test: it creates the well-known dev admin (admin@aquaplan.ch / Admin123!).
    /// In every other environment the initial admin is created once from the
    /// <c>INITIAL_ADMIN_EMAIL</c> / <c>INITIAL_ADMIN_PASSWORD</c> environment variables.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider serviceProvider, bool seedDefaultDevAdmin)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AquaPlanDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AquaPlanDbContext>>();

        // Seed roles
        foreach (var roleName in RoleName.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
                logger.LogInformation("Created role: {Role}", roleName);
            }
        }

        // Seed permissions
        var allPermissions = new[]
        {
            PermissionName.CreateOrder, PermissionName.EditOwnOrder, PermissionName.ViewOwnOrder,
            PermissionName.AssignPreleveur, PermissionName.ViewResults,
            PermissionName.ViewAssignedOrder, PermissionName.EditPrelevement,
            PermissionName.ValidateOrder, PermissionName.CreateUnplannedOrder,
            PermissionName.ManageUsers, PermissionName.ManageRoles,
            PermissionName.ManageDistributors, PermissionName.ManageLdp,
            PermissionName.ManageAnalysisPrograms, PermissionName.ViewAllOrders,
            PermissionName.ViewAllResults, PermissionName.AdministerSystem,
        };

        foreach (var permName in allPermissions)
        {
            if (!await dbContext.Permissions.AnyAsync(p => p.Name == permName))
            {
                dbContext.Permissions.Add(new Permission { Id = Guid.NewGuid(), Name = permName });
            }
        }
        await dbContext.SaveChangesAsync();

        // Assign permissions to roles
        var requerantPerms = new[] { PermissionName.CreateOrder, PermissionName.EditOwnOrder, PermissionName.ViewOwnOrder, PermissionName.AssignPreleveur, PermissionName.ViewResults };
        var preleveurPerms = new[] { PermissionName.ViewAssignedOrder, PermissionName.EditPrelevement, PermissionName.ValidateOrder, PermissionName.CreateUnplannedOrder };
        var adminPerms = allPermissions;

        await AssignPermissionsToRoleAsync(dbContext, roleManager, RoleName.Requerant, requerantPerms);
        await AssignPermissionsToRoleAsync(dbContext, roleManager, RoleName.Preleveur, preleveurPerms);
        await AssignPermissionsToRoleAsync(dbContext, roleManager, RoleName.RequerantPreleveur, requerantPerms.Union(preleveurPerms).ToArray());
        await AssignPermissionsToRoleAsync(dbContext, roleManager, RoleName.Administrator, adminPerms);

        // Seed default tenant
        if (!await dbContext.Tenants.AnyAsync())
        {
            dbContext.Tenants.Add(new Tenant
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "Canton de Fribourg",
                Code = "FR",
                Description = "Canton de Fribourg - Service de la sécurité alimentaire",
            });
            await dbContext.SaveChangesAsync();
        }

        // Seed admin user — dev/test gets the well-known dev admin, every other
        // environment requires INITIAL_ADMIN_EMAIL / INITIAL_ADMIN_PASSWORD (one-shot).
        if (seedDefaultDevAdmin)
        {
            await CreateAdminIfMissingAsync(userManager, logger, DefaultDevAdminEmail, "Admin123!");
        }
        else
        {
            await SeedInitialAdminFromConfigurationAsync(userManager, configuration, logger);
        }
    }

    /// <summary>
    /// Creates the initial production admin from configuration. No-op when the account
    /// already exists. Logs a warning when no admin can be created and none exists, so
    /// operators notice the missing INITIAL_ADMIN_* variables instead of silently
    /// running an instance without administrator.
    /// </summary>
    public static async Task SeedInitialAdminFromConfigurationAsync(
        UserManager<AppUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var email = configuration[InitialAdminEmailKey];
        var password = configuration[InitialAdminPasswordKey];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            var hasAdmin = (await userManager.GetUsersInRoleAsync(RoleName.Administrator)).Count > 0;
            if (!hasAdmin)
            {
                logger.LogWarning(
                    "No administrator account exists and {EmailKey}/{PasswordKey} are not set. " +
                    "Set both environment variables and restart to create the initial admin (one-shot).",
                    InitialAdminEmailKey, InitialAdminPasswordKey);
            }
            return;
        }

        await CreateAdminIfMissingAsync(userManager, logger, email, password);
    }

    private static async Task CreateAdminIfMissingAsync(
        UserManager<AppUser> userManager,
        ILogger logger,
        string adminEmail,
        string password)
    {
        if (await userManager.FindByEmailAsync(adminEmail) is not null)
        {
            return;
        }

        var admin = new AppUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "Admin",
            LastName = "AquaPlan",
            Organization = "Canton de Fribourg",
            TenantId = DefaultTenantId,
            EmailConfirmed = true,
        };
        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, RoleName.Administrator);
            logger.LogInformation("Created admin user: {Email}", adminEmail);
        }
        else
        {
            // Never log the password — only Identity error descriptions.
            logger.LogError(
                "Failed to create admin user {Email}: {Errors}",
                adminEmail,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private static async Task AssignPermissionsToRoleAsync(
        AquaPlanDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        string roleName,
        string[] permissionNames)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null) return;

        var permissions = await dbContext.Permissions
            .Where(p => permissionNames.Contains(p.Name))
            .ToListAsync();

        foreach (var permission in permissions)
        {
            if (!await dbContext.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id))
            {
                dbContext.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            }
        }
        await dbContext.SaveChangesAsync();
    }
}
