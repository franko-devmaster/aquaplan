using System.Security.Claims;
using AquaPlan.Application.DTOs.Auth;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
public class AuthController(
    IAuthService authService,
    ITokenService tokenService,
    IOidcUserService oidcUserService,
    IOidcCodeExchangeService oidcCodeExchangeService,
    UserManager<AppUser> userManager,
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

        // Sprint Robustesse F-101 — only an explicitly verified e-mail may link or create a
        // local account. The OIDC standard claim is "email_verified" (string "true"/"false").
        var emailVerified = string.Equals(
            principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);

        var defaultTenantId = Guid.Parse(
            configuration["Oidc:DefaultTenantId"] ?? "00000000-0000-0000-0000-000000000001");

        AppUser user;
        try
        {
            user = await oidcUserService.FindOrCreateFromExternalLoginAsync(
                externalId, email, emailVerified, firstName, lastName, defaultTenantId, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            // F-101 — IdP returned an unverified e-mail; refuse to link/create.
            logger.LogWarning("OIDC callback: unverified e-mail rejected for {Email}", email);
            return Redirect("/#/login?error=oidc_email_unverified");
        }

        // Sprint Robustesse F-102 — a deactivated account must not obtain tokens through OIDC,
        // mirroring the IsActive check already enforced on the password login path.
        if (!user.IsActive)
        {
            logger.LogWarning("OIDC callback: inactive account {Email} denied", email);
            return Redirect("/#/login?error=account_disabled");
        }

        var roles = await authService.GetUserRolesAsync(user.Id, cancellationToken);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var refreshToken = tokenService.GenerateRefreshToken();
        var refreshTokenExpirationDays = int.Parse(configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
        await userManager.UpdateAsync(user);

        // Sprint Sec F-006 — never put tokens in a redirect URL (browser history, proxy
        // logs, Referer). The SPA receives a short-lived single-use code and redeems it
        // via POST /api/auth/oidc-exchange.
        var exchangeCode = oidcCodeExchangeService.CreateCode(
            new OidcExchangeResponseDto(accessToken, refreshToken));

        logger.LogInformation("OIDC login successful for user {Email}", email);
        return Redirect($"/#/auth/callback?code={exchangeCode}");
    }

    /// <summary>
    /// Sprint Sec F-006 — redeems the single-use code issued by the OIDC callback for
    /// the actual tokens. The code is invalidated on first use and expires after ~60s.
    /// </summary>
    [HttpPost("oidc-exchange")]
    [AllowAnonymous]
    public IActionResult OidcExchange([FromBody] OidcExchangeRequestDto dto)
    {
        var tokens = oidcCodeExchangeService.RedeemCode(dto.Code);
        if (tokens is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Code exchange failed",
                Detail = "Invalid, expired, or already used exchange code",
                Status = StatusCodes.Status401Unauthorized,
            });
        }

        return Ok(tokens);
    }

    [HttpGet("oidc-config")]
    [AllowAnonymous]
    public IActionResult OidcConfig()
    {
        var oidcEnabled = configuration.GetValue<bool>("Oidc:Enabled");
        return Ok(new { enabled = oidcEnabled });
    }
}
