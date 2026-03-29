using AquaPlan.Application.DTOs.Users;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
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
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);

        var result = new List<UserListDto>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UserListDto(
                user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName,
                user.Organization, user.IsActive, user.TenantId, roles, user.CreatedAt));
        }
        return result;
    }

    public async Task<UserDetailDto?> GetUserByIdAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(u => u.UserDistributors)
                .ThenInclude(ud => ud.Distributor)
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        var distributors = user.UserDistributors
            .Where(ud => ud.Distributor is not null)
            .Select(ud => new DistributorSummaryDto(ud.DistributorId, ud.Distributor!.Name))
            .ToList();

        return new UserDetailDto(
            user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName,
            user.Organization, user.IsActive, user.TenantId, roles, distributors,
            user.CreatedAt, user.UpdatedAt);
    }

    public async Task<UserDetailDto> CreateUserAsync(UserCreateDto dto, string createdBy, CancellationToken cancellationToken = default)
    {
        var user = new AppUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Organization = dto.Organization,
            TenantId = dto.TenantId,
            CreatedBy = createdBy,
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        if (dto.Roles.Count > 0)
        {
            await userManager.AddToRolesAsync(user, dto.Roles);
        }

        foreach (var distributorId in dto.DistributorIds)
        {
            dbContext.UserDistributors.Add(new UserDistributor
            {
                UserId = user.Id,
                DistributorId = distributorId,
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Email} created by {CreatedBy}", dto.Email, createdBy);

        return (await GetUserByIdAsync(user.Id, dto.TenantId, cancellationToken))!;
    }

    public async Task<UserDetailDto?> UpdateUserAsync(string userId, UserUpdateDto dto, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(u => u.UserDistributors)
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Organization = dto.Organization;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = updatedBy;

        // Update roles
        var currentRoles = await userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(dto.Roles).ToList();
        var rolesToAdd = dto.Roles.Except(currentRoles).ToList();
        if (rolesToRemove.Count > 0) await userManager.RemoveFromRolesAsync(user, rolesToRemove);
        if (rolesToAdd.Count > 0) await userManager.AddToRolesAsync(user, rolesToAdd);

        // Update distributor assignments
        dbContext.UserDistributors.RemoveRange(user.UserDistributors);
        foreach (var distributorId in dto.DistributorIds)
        {
            dbContext.UserDistributors.Add(new UserDistributor
            {
                UserId = user.Id,
                DistributorId = distributorId,
            });
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
