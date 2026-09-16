using Npgsql;

namespace AquaPlan.Api.Configuration;

/// <summary>
/// Startup-time resolution of the PostgreSQL connection string.
/// <para>
/// Managed PostgreSQL providers (Render, Heroku, Fly, Neon, Supabase…) expose the database
/// as a URL — <c>postgresql://user:password@host:port/database</c> — which Npgsql does not
/// accept. Requiring the value to be converted by hand into Npgsql keyword syntax is a step
/// that has to be redone every time the database is recreated, and getting it wrong makes
/// the API die during migrations, before Kestrel binds a port.
/// </para>
/// <para>
/// <c>ConnectionStrings:DefaultConnection</c> keeps priority when it is set, so existing
/// deployments (docker-compose, NAS) are unaffected. <c>DATABASE_URL</c> is only a fallback.
/// </para>
/// </summary>
public static class DatabaseConnection
{
    private const int DefaultPostgresPort = 5432;

    /// <summary>
    /// Resolves the connection string the DbContext should use.
    /// </summary>
    /// <param name="configuredConnectionString">Value of <c>ConnectionStrings:DefaultConnection</c>.</param>
    /// <param name="databaseUrl">Value of the <c>DATABASE_URL</c> environment variable.</param>
    /// <returns>
    /// The configured connection string when present, otherwise <paramref name="databaseUrl"/>
    /// converted to Npgsql keyword syntax, otherwise <c>null</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">When <paramref name="databaseUrl"/> is not a usable PostgreSQL URL.</exception>
    public static string? Resolve(string? configuredConnectionString, string? databaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString;
        }

        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            return configuredConnectionString;
        }

        return ConvertUrl(databaseUrl);
    }

    private static string ConvertUrl(string databaseUrl)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            throw new InvalidOperationException(
                "DATABASE_URL is not a valid PostgreSQL URL. Expected " +
                "postgresql://user:password@host:port/database, or set " +
                "ConnectionStrings__DefaultConnection in Npgsql keyword syntax instead.");
        }

        var database = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrEmpty(database))
        {
            throw new InvalidOperationException(
                "DATABASE_URL does not name a database. Expected " +
                "postgresql://user:password@host:port/database.");
        }

        var credentials = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? DefaultPostgresPort : uri.Port,
            Database = Uri.UnescapeDataString(database),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty,
            // Managed providers terminate TLS with their own CA. Requiring SSL without
            // trusting the certificate would reject every hosted database.
            SslMode = SslMode.Require,
            TrustServerCertificate = true,
        };

        return builder.ConnectionString;
    }
}
