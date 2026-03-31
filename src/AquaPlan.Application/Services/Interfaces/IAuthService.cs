using AquaPlan.Application.DTOs.Auth;

namespace AquaPlan.Application.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
    Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserInfoDto?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<IList<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default);
}
