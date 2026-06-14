using AquaPlan.Application.DTOs.Users;
using AquaPlan.Application.Exceptions;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class UserManagementService(
    UserManager<AppUser> userManager,
    AquaPlanDbContext dbContext,
    ILogger<UserManagementService> logger) : IUserManagementService
{
    public async Task<IList<UserListDto>> GetUsersAsync(Guid tenantId, string? role, Guid? distributorId, bool? isActive, CancellationToken cancellationToken)
    {
        var users = await BuildUserListAsync(tenantId, distributorId, isActive, cancellationToken);
        return users
            .Where(u => role == null || u.Role == role)
            .ToList();
    }

    /// <summary>
    /// Polish F-226 — returns active users holding a préleveur-capable role (Préleveur or
    /// Requérant-Préleveur), matched on the exact role name via the UserRoles join rather than a
    /// fragile accent-insensitive substring check in the controller.
    /// </summary>
    public async Task<IList<UserListDto>> GetPreleveursAsync(Guid tenantId, Guid? distributorId, CancellationToken cancellationToken)
    {
        var users = await BuildUserListAsync(tenantId, distributorId, isActive: true, cancellationToken);
        return users
            .Where(u => u.Role == RoleName.Preleveur || u.Role == RoleName.RequerantPreleveur)
            .ToList();
    }

    /// <summary>
    /// AQ-429 — materialise the user rows first (a query Npgsql can translate), then resolve each
    /// user's role with a single set-based query over the Identity UserRoles/Roles tables and map
    /// in memory. This replaces the correlated subquery inside the projection (Polish F-209), which
    /// Npgsql could not translate once combined with the downstream OrderBy/Where and made
    /// GET /api/users return 500. Role resolution stays a single extra round-trip (no N+1).
    /// </summary>
    private async Task<List<UserListDto>> BuildUserListAsync(Guid tenantId, Guid? distributorId, bool? isActive, CancellationToken cancellationToken)
    {
        var query = dbContext.Users.Where(u => u.TenantId == tenantId);

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        if (distributorId.HasValue)
        {
            query = query.Where(u => u.DistributorId == distributorId.Value);
        }

        var users = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new
            {
                u.Id,
                u.UserNumber,
                u.Email,
                u.FirstName,
                u.LastName,
                u.DistributorId,
                DistributorName = u.Distributor != null ? u.Distributor.Name : null,
                u.IsActive,
                u.TenantId,
                u.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();
        var roleByUserId = (await (
                from ur in dbContext.UserRoles
                join r in dbContext.Roles on ur.RoleId equals r.Id
                where userIds.Contains(ur.UserId)
                select new { ur.UserId, r.Name })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().Name);

        return users
            .Select(u => new UserListDto(
                u.Id,
                u.UserNumber,
                u.Email ?? string.Empty,
                u.FirstName,
                u.LastName,
                roleByUserId.GetValueOrDefault(u.Id),
                u.DistributorId,
                u.DistributorName,
                u.IsActive,
                u.TenantId,
                u.CreatedAt))
            .ToList();
    }

    public async Task<UserDetailDto?> GetUserByIdAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(u => u.Distributor)
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);

        return new UserDetailDto(
            user.Id, user.UserNumber, user.Email ?? string.Empty, user.FirstName, user.LastName,
            roles.FirstOrDefault(), user.DistributorId, user.Distributor?.Name,
            user.IsActive, user.TenantId, user.CreatedAt, user.UpdatedAt);
    }

    /// <summary>
    /// Sprint Sec F-004 — the new user is always created in the caller's tenant
    /// (<paramref name="tenantId"/> from the JWT). The role is validated against the
    /// known role names and the distributor (when provided) must belong to the same tenant.
    /// </summary>
    public async Task<UserDetailDto> CreateUserAsync(UserCreateDto dto, string createdBy, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(dto.Role) && !RoleName.All.Contains(dto.Role))
        {
            throw new BusinessRuleException($"Unknown role: {dto.Role}");
        }

        if (dto.DistributorId.HasValue)
        {
            var distributorInTenant = await dbContext.Distributors
                .AnyAsync(d => d.Id == dto.DistributorId.Value && d.TenantId == tenantId, cancellationToken);
            if (!distributorInTenant)
            {
                throw new BusinessRuleException("The distributor does not exist in the caller's tenant.");
            }
        }

        var user = new AppUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            DistributorId = dto.DistributorId,
            TenantId = tenantId,
            CreatedBy = createdBy,
        };

        // Polish F-217 — UserNumber is "max + 1" per tenant, a classic race. There is a unique
        // index on (UserNumber, TenantId), so two concurrent creations collide on the second insert.
        // Retry with a freshly computed number on a unique-constraint violation instead of bubbling
        // a raw 500.
        const int maxAttempts = 5;
        IdentityResult result = IdentityResult.Success;
        for (var attempt = 1; ; attempt++)
        {
            var maxNumber = await dbContext.Users
                .Where(u => u.TenantId == tenantId)
                .Select(u => (int?)u.UserNumber)
                .MaxAsync(cancellationToken) ?? 100000;
            user.UserNumber = maxNumber + 1;

            try
            {
                result = await userManager.CreateAsync(user, dto.Password);
                break;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                logger.LogWarning("UserNumber {UserNumber} collided on attempt {Attempt}; retrying", user.UserNumber, attempt);
            }
        }

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BusinessRuleException($"Failed to create user: {errors}");
        }

        if (!string.IsNullOrEmpty(dto.Role))
        {
            // Polish F-217 — check the role-assignment result instead of ignoring it.
            EnsureIdentitySucceeded(await userManager.AddToRoleAsync(user, dto.Role), "assign role");
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Email} created by {CreatedBy}", dto.Email, createdBy);

        return (await GetUserByIdAsync(user.Id, tenantId, cancellationToken))!;
    }

    public async Task<UserDetailDto?> UpdateUserAsync(string userId, UserUpdateDto dto, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = updatedBy;

        // Polish F-217 — validate the requested role up front so we don't silently strip every
        // role (RemoveFromRolesAsync) and then fail to add an unknown one, leaving a roleless user.
        if (!string.IsNullOrEmpty(dto.Role) && !RoleName.All.Contains(dto.Role))
        {
            throw new BusinessRuleException($"Unknown role: {dto.Role}");
        }

        // Update email if changed
        if (!string.IsNullOrEmpty(dto.Email) && !string.Equals(user.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
        {
            // Polish F-217 — Identity results must be checked; a failed email/username change
            // (e.g. duplicate) was previously swallowed.
            var emailResult = await userManager.SetEmailAsync(user, dto.Email);
            EnsureIdentitySucceeded(emailResult, "update email");
            var userNameResult = await userManager.SetUserNameAsync(user, dto.Email);
            EnsureIdentitySucceeded(userNameResult, "update username");
        }

        // Update role (single role)
        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            // Polish F-217 — check the Identity result instead of ignoring it.
            EnsureIdentitySucceeded(await userManager.RemoveFromRolesAsync(user, currentRoles), "remove roles");
        }
        if (!string.IsNullOrEmpty(dto.Role))
        {
            EnsureIdentitySucceeded(await userManager.AddToRoleAsync(user, dto.Role), "assign role");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} updated by {UpdatedBy}", userId, updatedBy);

        return await GetUserByIdAsync(userId, tenantId, cancellationToken);
    }

    public async Task<bool> DeactivateUserAsync(string userId, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            return false;
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = updatedBy;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} deactivated by {UpdatedBy}", userId, updatedBy);
        return true;
    }

    public async Task<bool> ActivateUserAsync(string userId, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            return false;
        }

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = updatedBy;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} activated by {UpdatedBy}", userId, updatedBy);
        return true;
    }

    /// <summary>
    /// Polish F-217 — throws a <see cref="BusinessRuleException"/> when an ASP.NET Identity
    /// operation fails, so callers can no longer silently ignore the result and leave the user in
    /// an inconsistent state (e.g. roles stripped but the new role never assigned).
    /// </summary>
    private static void EnsureIdentitySucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BusinessRuleException($"Failed to {operation}: {errors}");
        }
    }
}
