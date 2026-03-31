using AquaPlan.Application.DTOs.Auth;
using AquaPlan.Domain.Entities;

namespace AquaPlan.Application.Services.Interfaces;

public interface IOidcUserService
{
    Task<AppUser?> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<AppUser> FindOrCreateFromExternalLoginAsync(
        string externalId,
        string email,
        string firstName,
        string lastName,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
