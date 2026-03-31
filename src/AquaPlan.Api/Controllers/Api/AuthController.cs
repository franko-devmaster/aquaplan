using System.Security.Claims;
using AquaPlan.Application.DTOs.Auth;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
public class AuthController(
    IAuthService authService,
    ITokenService tokenService,
    IOidcUserService oidcUserService,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(dto, cancellationToken);
        if (result is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Authentication failed",
                Detail = "Invalid email or password",
                Status = StatusCodes.Status401Unauthorized,
            });
        }

        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshTokenAsync(dto.RefreshToken, cancellationToken);
        if (result is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Token refresh failed",
                Detail = "Invalid or expired refresh token",
                Status = StatusCodes.Status401Unauthorized,
            });
        }

        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            await authService.LogoutAsync(userId, cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Unauthorized();
        }

        var userInfo = await authService.GetCurrentUserAsync(userId, cancellationToken);
        if (userInfo is null)
        {
            return NotFound();
        }

        return Ok(userInfo);
    }

    [HttpGet("oidc-login")]
    [AllowAnonymous]
    public IActionResult OidcLogin()
    {
        var oidcEnabled = configuration.GetValue<bool>("Oidc:Enabled");
        if (!oidcEnabled)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "OIDC not enabled",
                Detail = "OIDC authentication is not configured on this instance",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = "/api/auth/oidc-callback",
        };
        return Challenge(properties, "oidc");
    }

    [HttpGet("oidc-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> OidcCallback(CancellationToken cancellationToken)
    {
        var authenticateResult = await HttpContext.AuthenticateAsync("oidc");
        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            logger.LogWarning("OIDC callback: authentication failed");
            return Redirect("/#/login?error=oidc_failed");
        }

        var principal = authenticateResult.Principal;
        var externalId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? principal.FindFirstValue("oid")
                         ?? principal.FindFirstValue("sub");

        if (string.IsNullOrEmpty(externalId))
        {
            logger.LogWarning("OIDC callback: no external ID claim found");
            return Redirect("/#/login?error=oidc_no_id");
        }

        var email = principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("preferred_username")
                    ?? string.Empty;
        var firstName = principal.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty;
        var lastName = principal.FindFirstValue(ClaimTypes.Surname) ?? string.Empty;

        var defaultTenantId = Guid.Parse(
            configuration["Oidc:DefaultTenantId"] ?? "00000000-0000-0000-0000-000000000001");

        var user = await oidcUserService.FindOrCreateFromExternalLoginAsync(
            externalId, email, firstName, lastName, defaultTenantId, cancellationToken);

        var roles = await authService.GetUserRolesAsync(user.Id, cancellationToken);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var refreshToken = tokenService.GenerateRefreshToken();

        logger.LogInformation("OIDC login successful for user {Email}", email);
        return Redirect($"/#/auth/callback?token={accessToken}&refresh={refreshToken}");
    }

    [HttpGet("oidc-config")]
    [AllowAnonymous]
    public IActionResult OidcConfig()
    {
        var oidcEnabled = configuration.GetValue<bool>("Oidc:Enabled");
        return Ok(new { enabled = oidcEnabled });
    }
}
