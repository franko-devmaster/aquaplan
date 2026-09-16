using AquaPlan.Api.Configuration;

namespace AquaPlan.Api.Tests.Configuration;

public class DatabaseConnectionTest
{
    private const string ConfiguredConnectionString =
        "Host=aquaplan-db;Port=5432;Database=aquaplan;Username=aquaplan;Password=secret";

    [Fact]
    public void Resolve_WhenConnectionStringIsConfigured_ShouldReturnItUnchanged()
    {
        var result = DatabaseConnection.Resolve(ConfiguredConnectionString, databaseUrl: null);

        result.Should().Be(ConfiguredConnectionString);
    }

    [Fact]
    public void Resolve_WhenBothAreProvided_ShouldPreferDatabaseUrl()
    {
        var result = DatabaseConnection.Resolve(
            ConfiguredConnectionString,
            "postgresql://user:pass@dpg-abc.frankfurt-postgres.render.com/aquaplan_2");

        result.Should().Contain("Host=dpg-abc.frankfurt-postgres.render.com");
        result.Should().Contain("Database=aquaplan_2");
    }

    /// <summary>
    /// Regression : appsettings.json embarque une chaine locale non vide dans l'image.
    /// Si la valeur configuree l'emportait, DATABASE_URL serait ignoree dans tout
    /// deploiement et l'API tenterait silencieusement localhost.
    /// </summary>
    [Fact]
    public void Resolve_WhenTheBakedInLocalDefaultIsPresent_ShouldStillUseDatabaseUrl()
    {
        const string bakedInDefault =
            "Host=localhost;Port=5432;Database=aquaplan_dev;Username=aquaplan;Password=aquaplan_dev";

        var result = DatabaseConnection.Resolve(
            bakedInDefault,
            "postgresql://u:p@dpg-daleo2rl550s73b16e60-a/aquaplan_2");

        result.Should().NotContain("localhost");
        result.Should().Contain("Host=dpg-daleo2rl550s73b16e60-a");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WhenNothingIsProvided_ShouldReturnTheConfiguredValue(string? configured)
    {
        var result = DatabaseConnection.Resolve(configured, databaseUrl: null);

        result.Should().Be(configured);
    }

    [Fact]
    public void Resolve_WhenOnlyDatabaseUrlIsProvided_ShouldConvertItToNpgsqlSyntax()
    {
        var result = DatabaseConnection.Resolve(
            configuredConnectionString: null,
            "postgresql://aquaplan:s3cret@dpg-abc.frankfurt-postgres.render.com:5432/aquaplan_2");

        result.Should().Contain("Host=dpg-abc.frankfurt-postgres.render.com");
        result.Should().Contain("Database=aquaplan_2");
        result.Should().Contain("Username=aquaplan");
        result.Should().Contain("Password=s3cret");
        result.Should().Contain("SSL Mode=Require");
        result.Should().Contain("Trust Server Certificate=True");
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("postgresql")]
    public void Resolve_ShouldAcceptBothPostgresUrlSchemes(string scheme)
    {
        var result = DatabaseConnection.Resolve(null, $"{scheme}://user:pass@db.example.com/aquaplan");

        result.Should().Contain("Database=aquaplan");
    }

    [Fact]
    public void Resolve_WhenUrlOmitsThePort_ShouldDefaultTo5432()
    {
        var result = DatabaseConnection.Resolve(null, "postgresql://user:pass@db.example.com/aquaplan");

        result.Should().Contain("Port=5432");
    }

    [Fact]
    public void Resolve_WhenUrlUsesANonDefaultPort_ShouldKeepIt()
    {
        var result = DatabaseConnection.Resolve(null, "postgresql://user:pass@db.example.com:6543/aquaplan");

        result.Should().Contain("Port=6543");
    }

    [Fact]
    public void Resolve_WhenCredentialsArePercentEncoded_ShouldDecodeThem()
    {
        var result = DatabaseConnection.Resolve(null, "postgresql://us%40er:p%40ss%3Aword@db.example.com/aquaplan");

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(result);
        builder.Username.Should().Be("us@er");
        builder.Password.Should().Be("p@ss:word");
    }

    [Fact]
    public void Resolve_WhenUrlHasNoPassword_ShouldProduceAnEmptyPassword()
    {
        var result = DatabaseConnection.Resolve(null, "postgresql://user@db.example.com/aquaplan");

        // Npgsql omet le mot de passe vide de la chaine produite : il se relit donc a null.
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(result);
        builder.Username.Should().Be("user");
        builder.Password.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Resolve_WhenUrlCarriesNoSslMode_ShouldRequireSsl()
    {
        var result = DatabaseConnection.Resolve(null, "postgresql://user:pass@db.example.com/aquaplan");

        new Npgsql.NpgsqlConnectionStringBuilder(result).SslMode.Should().Be(Npgsql.SslMode.Require);
    }

    [Theory]
    [InlineData("prefer", Npgsql.SslMode.Prefer)]
    [InlineData("disable", Npgsql.SslMode.Disable)]
    [InlineData("VerifyFull", Npgsql.SslMode.VerifyFull)]
    public void Resolve_WhenUrlCarriesAnSslMode_ShouldHonourIt(string requested, Npgsql.SslMode expected)
    {
        var result = DatabaseConnection.Resolve(
            null, $"postgresql://user:pass@db.example.com/aquaplan?sslmode={requested}");

        new Npgsql.NpgsqlConnectionStringBuilder(result).SslMode.Should().Be(expected);
    }

    [Fact]
    public void Resolve_WhenUrlCarriesAnUnknownSslMode_ShouldThrow()
    {
        var act = () => DatabaseConnection.Resolve(
            null, "postgresql://user:pass@db.example.com/aquaplan?sslmode=banana");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*unknown sslmode 'banana'*");
    }

    [Theory]
    [InlineData("Host=db;Database=aquaplan")]
    [InlineData("mysql://user:pass@db.example.com/aquaplan")]
    [InlineData("not a url at all")]
    public void Resolve_WhenDatabaseUrlIsNotAPostgresUrl_ShouldThrow(string databaseUrl)
    {
        var act = () => DatabaseConnection.Resolve(null, databaseUrl);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a valid PostgreSQL URL*");
    }

    [Fact]
    public void Resolve_WhenDatabaseUrlNamesNoDatabase_ShouldThrow()
    {
        var act = () => DatabaseConnection.Resolve(null, "postgresql://user:pass@db.example.com/");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not name a database*");
    }
}
