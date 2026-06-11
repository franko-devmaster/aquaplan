using AquaPlan.Application.DTOs.Auth;
using AquaPlan.Application.Services;

namespace AquaPlan.Application.Tests.Services;

/// <summary>
/// Sprint Sec F-006 — single-use, short-lived OIDC exchange codes.
/// </summary>
public class OidcCodeExchangeServiceTest
{
    private static readonly OidcExchangeResponseDto Tokens = new("access-token", "refresh-token");

    private sealed class TestTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }

    [Fact]
    public void CreateCode_ShouldReturnUrlSafeUnguessableCode()
    {
        var sut = new OidcCodeExchangeService();

        var code = sut.CreateCode(Tokens);

        code.Should().NotBeNullOrWhiteSpace();
        code.Length.Should().BeGreaterThanOrEqualTo(40); // 32 random bytes base64url
        code.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public void CreateCode_ShouldReturnDifferentCodesForEachCall()
    {
        var sut = new OidcCodeExchangeService();

        var first = sut.CreateCode(Tokens);
        var second = sut.CreateCode(Tokens);

        first.Should().NotBe(second);
    }

    [Fact]
    public void RedeemCode_ShouldReturnTokens_OnFirstUse()
    {
        var sut = new OidcCodeExchangeService();
        var code = sut.CreateCode(Tokens);

        var result = sut.RedeemCode(code);

        result.Should().Be(Tokens);
    }

    [Fact]
    public void RedeemCode_ShouldReturnNull_OnSecondUse()
    {
        var sut = new OidcCodeExchangeService();
        var code = sut.CreateCode(Tokens);

        sut.RedeemCode(code);
        var second = sut.RedeemCode(code);

        second.Should().BeNull();
    }

    [Theory]
    [InlineData("unknown-code")]
    [InlineData("")]
    [InlineData("   ")]
    public void RedeemCode_ShouldReturnNull_ForUnknownOrEmptyCode(string code)
    {
        var sut = new OidcCodeExchangeService();

        var result = sut.RedeemCode(code);

        result.Should().BeNull();
    }

    [Fact]
    public void RedeemCode_ShouldReturnNull_WhenCodeExpired()
    {
        var time = new TestTimeProvider();
        var sut = new OidcCodeExchangeService(time);
        var code = sut.CreateCode(Tokens);

        time.Now = time.Now.Add(OidcCodeExchangeService.CodeLifetime + TimeSpan.FromSeconds(1));

        sut.RedeemCode(code).Should().BeNull();
    }

    [Fact]
    public void RedeemCode_ShouldReturnTokens_JustBeforeExpiry()
    {
        var time = new TestTimeProvider();
        var sut = new OidcCodeExchangeService(time);
        var code = sut.CreateCode(Tokens);

        time.Now = time.Now.Add(OidcCodeExchangeService.CodeLifetime - TimeSpan.FromSeconds(1));

        sut.RedeemCode(code).Should().Be(Tokens);
    }
}
