using AquaPlan.Application.DTOs.Auth;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Application.Services;

internal class AuthService(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    ITokenService tokenService,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<LoginResponseDto?> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user is null || !user.IsActive)
        {
            logger.LogWarning("Login failed for email {Email}: user not found or inactive", dto.Email);
            return null;
        }

        // Sprint Robustesse F-111 — check the password through the SignInManager with
        // lockoutOnFailure so AccessFailedCount is incremented and the account is locked
        // after MaxFailedAccessAttempts (configured in WithInfrastructure).
        var signInResult = await signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
        if (signInResult.IsLockedOut)
        {
            logger.LogWarning("Login failed for email {Email}: account locked out", dto.Email);
            return null;
        }

        if (!signInResult.Succeeded)
        {
            logger.LogWarning("Login failed for email {Email}: invalid password", dto.Email);
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var refreshToken = tokenService.GenerateRefreshToken();
        var expiresIn = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60");
        var refreshTokenExpirationDays = int.Parse(configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
        await userManager.UpdateAsync(user);

        logger.LogInformation("User {Email} logged in successfully", dto.Email);

        return new LoginResponseDto(accessToken, refreshToken, expiresIn * 60);
    }

    public async Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var user = userManager.Users.FirstOrDefault(u => u.RefreshToken == refreshToken);
        if (user is null)
        {
            logger.LogWarning("Refresh token validation failed: token not found");
            return null;
        }

        if (user.RefreshTokenExpiryTime is null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            logger.LogWarning("Refresh token validation failed: token expired for user {Email}", user.Email);
            return null;
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Refresh token validation failed: user {Email} is inactive", user.Email);
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        var newAccessToken = tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = tokenService.GenerateRefreshToken();
        var expiresIn = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60");
        var refreshTokenExpirationDays = int.Parse(configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
        await userManager.UpdateAsync(user);

        logger.LogInformation("Token refreshed successfully for user {Email}", user.Email);

        return new LoginResponseDto(newAccessToken, newRefreshToken, expiresIn * 60);
    }

    public async Task LogoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is not null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await userManager.UpdateAsync(user);
        }

        logger.LogInformation("User {UserId} logged out", userId);
    }

    public async Task<UserInfoDto?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        return new UserInfoDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.Organization,
            user.TenantId,
            roles,
            user.DistributorId);
    }

    public async Task<IList<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return [];
        }

        return await userManager.GetRolesAsync(user);
    }
}
