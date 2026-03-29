namespace AquaPlan.Application.DTOs.Auth;

public record LoginResponseDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);
