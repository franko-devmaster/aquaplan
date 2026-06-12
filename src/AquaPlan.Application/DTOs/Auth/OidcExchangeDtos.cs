using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Auth;

/// <summary>
/// Sprint Sec F-006 — payload for <c>POST /api/auth/oidc-exchange</c>: the SPA exchanges
/// the one-time code received on the OIDC redirect for the actual tokens. Tokens never
/// transit in a URL anymore (browser history, proxy logs, Referer).
/// </summary>
public record OidcExchangeRequestDto(
    [property: Required] string Code);

/// <summary>Tokens returned by the one-time code exchange.</summary>
public record OidcExchangeResponseDto(
    string AccessToken,
    string RefreshToken);
