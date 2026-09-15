using Tempest.Core.Invoicing.OAuth;

namespace Tempest.Core.Tests.Security;

/// <summary>
/// `WP 21.5F` Offensive Security Audit, OSA-04: <see cref="AccessTokenResult"/>
/// and <see cref="OAuthTokenResponse"/> are C# records — a compiler-generated
/// <c>ToString()</c> would print a real access/refresh token verbatim if any
/// future call site ever logged one of these directly (<c>$"{result}"</c> or
/// <c>logger.LogX("{Result}", result)</c> reads as entirely ordinary code and
/// would not visibly be a mistake). No current call site does this — see the
/// audit report — but the redaction is defence in depth against the day one
/// does.
/// </summary>
public sealed class OAuthTokenRedactionTests
{
    [Fact]
    public void AccessTokenResult_ToString_NeverContainsTheRealAccessToken()
    {
        var result = AccessTokenResult.Ok("super-secret-access-token-12345", "tenant-1");

        var text = result.ToString();

        Assert.DoesNotContain("super-secret-access-token-12345", text, StringComparison.Ordinal);
        Assert.Contains("redacted", text, StringComparison.OrdinalIgnoreCase);
        // Everything non-sensitive still shows, so this stays useful for
        // diagnostics rather than becoming a black box.
        Assert.Contains("tenant-1", text, StringComparison.Ordinal);
        Assert.Contains("Ok", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessTokenResult_WithNoToken_ToString_SaysNone_NotRedacted()
    {
        var result = AccessTokenResult.NotAuthorised();

        Assert.Contains("<none>", result.ToString(), StringComparison.Ordinal);
    }
}
