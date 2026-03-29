using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Data.Seeds;

public static class RoleAndPermissionSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AquaPlanDbContext>();
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

        // Seed admin user
        var adminEmail = "admin@aquaplan.ch";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "AquaPlan",
                Organization = "Canton de Fribourg",
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                EmailConfirmed = true,
            };
            var result = await userManager.CreateAsync(admin, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, RoleName.Administrator);
                logger.LogInformation("Created admin user: {Email}", adminEmail);
            }
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
