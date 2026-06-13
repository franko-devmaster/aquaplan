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
        bool emailVerified,
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

        // Sprint Robustesse F-101 — never bind or create a local account from an
        // unverified e-mail: an attacker who controls an IdP account with an arbitrary
        // (unverified) e-mail could otherwise take over an existing local account or
        // squat a future one.
        if (!emailVerified)
        {
            logger.LogWarning(
                "OIDC login: rejected unverified e-mail {Email} for ExternalId {ExternalId}",
                email, externalId);
            throw new UnauthorizedAccessException(
                "The identity provider did not return a verified e-mail address.");
        }

        // Check if a user with the same (verified) email already exists (link accounts)
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
