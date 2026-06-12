using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Users;

/// <summary>
/// Sprint Sec F-004 — <c>TenantId</c> was removed from the payload: the created user is
/// always attached to the caller's tenant (taken from the JWT), never to a
/// client-supplied tenant.
/// </summary>
public record UserCreateDto(
    [property: Required, EmailAddress] string Email,
    [property: Required, StringLength(100)] string FirstName,
    [property: Required, StringLength(100)] string LastName,
    [property: Required] string Password,
    string? Role,
    Guid? DistributorId);
