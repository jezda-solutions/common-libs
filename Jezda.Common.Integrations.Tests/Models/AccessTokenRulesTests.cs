using Jezda.Common.Integrations.Abstractions;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Models;

public class AccessTokenRulesTests
{
    [Theory]
    [InlineData("ghp_abc-123.XYZ~")]
    [InlineData("abc 123")] // The server decides: it goes out, and comes back as 401.
    [InlineData("")]
    public void CanGoInHeader_PrintableAscii_ReturnsTrue(string token)
    {
        Assert.True(AccessTokenRules.CanGoInHeader(token));
    }

    [Theory]
    [InlineData("token”")]
    [InlineData("tokén")]
    [InlineData("token\n")]
    [InlineData("token\r\n")]
    [InlineData("abc\t123")]
    [InlineData("token\u007F")]
    public void CanGoInHeader_AnythingElse_ReturnsFalse(string token)
    {
        Assert.False(AccessTokenRules.CanGoInHeader(token));
    }

    [Fact]
    public void ThrowIfCannotGoInHeader_AnythingElse_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => AccessTokenRules.ThrowIfCannotGoInHeader("token\n"));

        Assert.Equal("accessToken", exception.ParamName);
    }

    [Fact]
    public void ThrowIfCannotGoInHeader_PrintableAscii_DoesNotThrow()
    {
        AccessTokenRules.ThrowIfCannotGoInHeader("ghp_abc-123");
    }
}
