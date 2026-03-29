using AquaPlan.Domain.Entities;

namespace AquaPlan.Application.Services.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(AppUser user, IList<string> roles);
    string GenerateRefreshToken();
}
