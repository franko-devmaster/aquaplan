using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Auth;

public record LoginDto(
    [Required][EmailAddress] string Email,
    [Required] string Password);
