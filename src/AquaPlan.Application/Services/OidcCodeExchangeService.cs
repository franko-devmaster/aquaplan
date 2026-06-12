using System.Collections.Concurrent;
using System.Security.Cryptography;
using AquaPlan.Application.DTOs.Auth;
using AquaPlan.Application.Services.Interfaces;

namespace AquaPlan.Application.Services;

/// <summary>
/// Sprint Sec F-006 — in-memory, thread-safe store of single-use OIDC exchange codes.
/// Registered as a singleton. Codes are 256-bit random values valid for
/// <see cref="CodeLifetime"/> and removed on first redemption (single use).
/// Expired entries are purged opportunistically on every call.
/// NOTE: in-memory storage assumes a single API instance (current Synology target);
/// a distributed cache will be needed for the multi-replica K8S target.
/// </summary>
internal class OidcCodeExchangeService(TimeProvider? timeProvider = null) : IOidcCodeExchangeService
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromSeconds(60);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    private sealed record Entry(OidcExchangeResponseDto Tokens, DateTimeOffset ExpiresAt);

    public string CreateCode(OidcExchangeResponseDto tokens)
    {
        PurgeExpired();

        // URL-safe base64 of 32 random bytes (256 bits) — unguessable.
        var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var expiresAt = _timeProvider.GetUtcNow().Add(CodeLifetime);
        _entries[code] = new Entry(tokens, expiresAt);
        return code;
    }

    public OidcExchangeResponseDto? RedeemCode(string code)
    {
        PurgeExpired();

        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        if (!_entries.TryRemove(code, out var entry))
        {
            return null;
        }

        if (entry.ExpiresAt < _timeProvider.GetUtcNow())
        {
            return null;
        }

        return entry.Tokens;
    }

    private void PurgeExpired()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var (key, entry) in _entries)
        {
            if (entry.ExpiresAt < now)
            {
                _entries.TryRemove(key, out _);
            }
        }
    }
}
