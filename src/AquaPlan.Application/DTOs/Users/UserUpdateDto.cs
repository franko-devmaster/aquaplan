namespace AquaPlan.Application.DTOs.Users;

public record UserUpdateDto(
    string FirstName,
    string LastName,
    string? Organization,
    List<string> Roles,
    List<Guid> DistributorIds);
