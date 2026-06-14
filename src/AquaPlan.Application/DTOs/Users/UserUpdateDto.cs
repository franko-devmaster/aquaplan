using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Users;

// Polish F-220 — names are required and bounded; email (when changed) must be well-formed.
public record UserUpdateDto(
    [property: EmailAddress] string? Email,
    [property: Required, StringLength(100)] string FirstName,
    [property: Required, StringLength(100)] string LastName,
    string? Role);
