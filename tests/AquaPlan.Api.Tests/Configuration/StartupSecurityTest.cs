using AquaPlan.Api.Configuration;

namespace AquaPlan.Api.Tests.Configuration;

public class StartupSecurityTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveJwtSecret_WhenConfiguredSecretIsValid_ShouldReturnIt(bool isDevelopment)
    {
        var configured = "A-Strong-Secret-Key-Of-At-Least-32-Chars!";

        var result = StartupSecurity.ResolveJwtSecret(configured, isDevelopment);

        result.Should().Be(configured);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveJwtSecret_WhenMissingInProduction_ShouldThrow(string? configured)
    {
        var act = () => StartupSecurity.ResolveJwtSecret(configured, isDevelopment: false);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:SecretKey is not configured*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveJwtSecret_WhenMissingInDevelopment_ShouldGenerateRandomSecret(string? configured)
    {
        var result = StartupSecurity.ResolveJwtSecret(configured, isDevelopment: true);

        result.Should().NotBeNullOrWhiteSpace();
        result.Length.Should().BeGreaterThanOrEqualTo(StartupSecurity.MinimumSecretLength);
    }

    [Fact]
    public void ResolveJwtSecret_WhenGeneratedInDevelopment_ShouldDifferBetweenCalls()
    {
        var first = StartupSecurity.ResolveJwtSecret(null, isDevelopment: true);
        var second = StartupSecurity.ResolveJwtSecret(null, isDevelopment: true);

        first.Should().NotBe(second);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveJwtSecret_WhenConfiguredSecretIsTooShort_ShouldThrow(bool isDevelopment)
    {
        var act = () => StartupSecurity.ResolveJwtSecret("short!", isDevelopment);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 32 characters*");
    }

    [Fact]
    public void GenerateRandomSecret_ShouldReturn512BitBase64Secret()
    {
        var secret = StartupSecurity.GenerateRandomSecret();

        var bytes = Convert.FromBase64String(secret);
        bytes.Should().HaveCount(64);
    }
}
