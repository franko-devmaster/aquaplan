namespace AquaPlan.Application.DTOs.Users;

public record UserUpdateDto(
    string? Email,
    string FirstName,
    string LastName,
    string? Role);
