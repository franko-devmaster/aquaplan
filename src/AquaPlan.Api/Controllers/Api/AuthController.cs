using System.Security.Claims;
using AquaPlan.Application.DTOs.Auth;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
public class AuthController(IAuthService authService, ILogger<AuthController> logger) : ControllerBase
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
}
