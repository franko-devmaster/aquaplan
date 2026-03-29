using AquaPlan.Application.DTOs.Auth;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Application.Services;

internal class AuthService(
    UserManager<AppUser> userManager,
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

        var isValidPassword = await userManager.CheckPasswordAsync(user, dto.Password);
        if (!isValidPassword)
        {
            logger.LogWarning("Login failed for email {Email}: invalid password", dto.Email);
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var refreshToken = tokenService.GenerateRefreshToken();
        var expiresIn = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60");

        logger.LogInformation("User {Email} logged in successfully", dto.Email);

        return new LoginResponseDto(accessToken, refreshToken, expiresIn * 60);
    }

    public Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // Simplified: in production, validate refresh token from database
        logger.LogWarning("Refresh token validation not fully implemented yet");
        return Task.FromResult<LoginResponseDto?>(null);
    }

    public Task LogoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        // In production: invalidate refresh tokens, blacklist JWT
        logger.LogInformation("User {UserId} logged out", userId);
        return Task.CompletedTask;
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
            roles);
    }
}
