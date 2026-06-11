using System.Security.Cryptography;

namespace AquaPlan.Api.Configuration;

/// <summary>
/// Sprint Sec (F-001 backend / F-003 cross-cutting) — startup-time resolution of the JWT
/// signing secret. There is deliberately NO hardcoded fallback anymore:
/// <list type="bullet">
///   <item>Production (and any non-Development environment): the secret MUST be provided
///   via configuration (env var <c>Jwt__SecretKey</c> or a secret store). Startup fails
///   fast otherwise.</item>
///   <item>Development: a random ephemeral secret is generated when none is configured.
///   Tokens are invalidated on each restart, which is acceptable for local work.</item>
/// </list>
/// </summary>
public static class StartupSecurity
{
    /// <summary>Minimum secret length (chars). HS256 requires at least a 256-bit key.</summary>
    public const int MinimumSecretLength = 32;

    /// <summary>
    /// Resolves the JWT signing secret from configuration.
    /// </summary>
    /// <param name="configuredSecret">Value of <c>Jwt:SecretKey</c> (may be null/empty).</param>
    /// <param name="isDevelopment">Whether the host runs in the Development environment.</param>
    /// <returns>The secret to use for signing and validation.</returns>
    /// <exception cref="InvalidOperationException">
    /// When the secret is missing outside Development, or shorter than <see cref="MinimumSecretLength"/>.
    /// </exception>
    public static string ResolveJwtSecret(string? configuredSecret, bool isDevelopment)
    {
        if (!string.IsNullOrWhiteSpace(configuredSecret))
        {
            if (configuredSecret.Length < MinimumSecretLength)
            {
                throw new InvalidOperationException(
                    $"Jwt:SecretKey must be at least {MinimumSecretLength} characters long " +
                    $"(got {configuredSecret.Length}). Provide a strong secret via the " +
                    "Jwt__SecretKey environment variable or a secret store.");
            }
            return configuredSecret;
        }

        if (!isDevelopment)
        {
            throw new InvalidOperationException(
                "Jwt:SecretKey is not configured. In non-Development environments the JWT " +
                "signing secret is required and has no fallback. Set the Jwt__SecretKey " +
                "environment variable (e.g. JWT_SECRET in the deployment .env) before starting the API.");
        }

        // Development convenience: ephemeral random secret (never logged, never persisted).
        return GenerateRandomSecret();
    }

    /// <summary>Generates a cryptographically random 512-bit secret (base64, 88 chars).</summary>
    public static string GenerateRandomSecret()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}
