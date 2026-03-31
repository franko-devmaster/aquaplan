using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class OidcUserService(
    UserManager<AppUser> userManager,
    ILogger<OidcUserService> logger) : IOidcUserService
{
    public async Task<AppUser?> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await userManager.Users
            .FirstOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken);
    }

    public async Task<AppUser> FindOrCreateFromExternalLoginAsync(
        string externalId,
        string email,
        string firstName,
        string lastName,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var existingUser = await FindByExternalIdAsync(externalId, cancellationToken);
        if (existingUser is not null)
        {
            logger.LogInformation("OIDC login: existing user found for ExternalId {ExternalId}", externalId);
            return existingUser;
        }

        // Check if a user with the same email already exists (link accounts)
        var emailUser = await userManager.FindByEmailAsync(email);
        if (emailUser is not null)
        {
            emailUser.ExternalId = externalId;
            emailUser.UpdatedAt = DateTime.UtcNow;
            await userManager.UpdateAsync(emailUser);
            logger.LogInformation("OIDC login: linked ExternalId {ExternalId} to existing user {Email}", externalId, email);
            return emailUser;
        }

        // Create new user
        var newUser = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            ExternalId = externalId,
            TenantId = tenantId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await userManager.CreateAsync(newUser);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError("OIDC login: failed to create user for {Email}: {Errors}", email, errors);
            throw new InvalidOperationException($"Failed to create OIDC user: {errors}");
        }

        // Assign default role
        await userManager.AddToRoleAsync(newUser, RoleName.Requerant);
        logger.LogInformation("OIDC login: created new user {Email} with ExternalId {ExternalId}", email, externalId);

        return newUser;
    }
}
