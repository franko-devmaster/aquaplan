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
    public async Task<IList<UserListDto>> GetUsersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var users = await dbContext.Users
            .Include(u => u.Distributor)
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);

        var result = new List<UserListDto>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UserListDto(
                user.Id, user.UserNumber, user.Email ?? string.Empty, user.FirstName, user.LastName,
                roles.FirstOrDefault(), user.DistributorId, user.Distributor?.Name,
                user.IsActive, user.TenantId, user.CreatedAt));
        }
        return result;
    }

    public async Task<IList<UserListDto>> GetUsersAsync(Guid tenantId, string? role, Guid? distributorId, bool? isActive, CancellationToken cancellationToken)
    {
        var query = dbContext.Users
            .Include(u => u.Distributor)
            .Where(u => u.TenantId == tenantId);

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
            .ToListAsync(cancellationToken);

        var result = new List<UserListDto>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var userRole = roles.FirstOrDefault();
            if (role is not null && !string.Equals(userRole, role, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            result.Add(new UserListDto(
                user.Id, user.UserNumber, user.Email ?? string.Empty, user.FirstName, user.LastName,
                userRole, user.DistributorId, user.Distributor?.Name,
                user.IsActive, user.TenantId, user.CreatedAt));
        }
        return result;
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

        // Generate next UserNumber for this tenant
        var maxNumber = await dbContext.Users
            .Where(u => u.TenantId == tenantId)
            .Select(u => (int?)u.UserNumber)
            .MaxAsync(cancellationToken) ?? 100000;
        user.UserNumber = maxNumber + 1;

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BusinessRuleException($"Failed to create user: {errors}");
        }

        if (!string.IsNullOrEmpty(dto.Role))
        {
            await userManager.AddToRoleAsync(user, dto.Role);
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

        // Update email if changed
        if (!string.IsNullOrEmpty(dto.Email) && !string.Equals(user.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
        {
            await userManager.SetEmailAsync(user, dto.Email);
            await userManager.SetUserNameAsync(user, dto.Email);
        }

        // Update role (single role)
        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            await userManager.RemoveFromRolesAsync(user, currentRoles);
        }
        if (!string.IsNullOrEmpty(dto.Role))
        {
            await userManager.AddToRoleAsync(user, dto.Role);
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
}
