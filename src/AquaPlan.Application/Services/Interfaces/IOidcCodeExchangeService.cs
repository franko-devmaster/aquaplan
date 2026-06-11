using AquaPlan.Application.DTOs.Auth;

namespace AquaPlan.Application.Services.Interfaces;

/// <summary>
/// Sprint Sec F-006 — short-lived, single-use exchange codes for the OIDC callback.
/// The callback stores the freshly issued tokens under a random code, redirects the SPA
/// with only that code in the URL, and the SPA redeems it once via
/// <c>POST /api/auth/oidc-exchange</c>.
/// </summary>
public interface IOidcCodeExchangeService
{
    /// <summary>Stores the tokens and returns a single-use code (valid ~60 seconds).</summary>
    string CreateCode(OidcExchangeResponseDto tokens);

    /// <summary>
    /// Redeems a code. Returns the tokens on the first call within the validity window,
    /// null for unknown, already-used, or expired codes.
    /// </summary>
    OidcExchangeResponseDto? RedeemCode(string code);
}
