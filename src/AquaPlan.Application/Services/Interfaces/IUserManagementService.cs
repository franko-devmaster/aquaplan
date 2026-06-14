using AquaPlan.Application.DTOs.Users;

namespace AquaPlan.Application.Services.Interfaces;

public interface IUserManagementService
{
    Task<IList<UserListDto>> GetUsersAsync(Guid tenantId, string? role, Guid? distributorId, bool? isActive, CancellationToken cancellationToken);
    Task<IList<UserListDto>> GetPreleveursAsync(Guid tenantId, Guid? distributorId, CancellationToken cancellationToken);
    Task<UserDetailDto?> GetUserByIdAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<UserDetailDto> CreateUserAsync(UserCreateDto dto, string createdBy, Guid tenantId, CancellationToken cancellationToken = default);
    Task<UserDetailDto?> UpdateUserAsync(string userId, UserUpdateDto dto, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> DeactivateUserAsync(string userId, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ActivateUserAsync(string userId, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default);
}
