using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Auth;

public record RefreshTokenDto(
    [Required] string RefreshToken);
