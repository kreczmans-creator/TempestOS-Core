using Tempest.Companion.Client;

namespace Tempest.Companion.Tests;

// Proves CompanionApiClient's construction contract and the wire-shape
// constants that must never drift from the platform's own (the client
// deliberately carries no Tempest.Core reference, so equality with the
// server-side constants is proven here, where both assemblies are
// visible). Full request/response behaviour is proven end-to-end against
// a real Host in CompanionIntegrationTests.
public class CompanionApiClientTests
{
    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://server:21")]
    [InlineData("")]
    public void Constructor_RejectsNonHttpUrls(string url)
    {
        Assert.Throws<ArgumentException>(() => new CompanionApiClient(url, "user"));
    }

    [Fact]
    public void Constructor_AcceptsHttpAndHttps()
    {
        using var http = new CompanionApiClient("http://127.0.0.1:5080", "user");
        using var https = new CompanionApiClient("https://tempest.example", "user");
    }

    [Fact]
    public void IdentityHeaderName_MatchesThePlatformsOwnHeader()
    {
        // The one constant deliberately duplicated across the process
        // boundary (the client has no Tempest.Core reference by design,
        // ADR-0113) - this assertion is the drift guard.
        Assert.Equal(Tempest.Core.Api.ApiRequestHandler.IdentityHeaderName, CompanionApiClient.IdentityHeaderName);
    }

    [Fact]
    public async Task Unreachable_ThrowsTypedUnreachableFailure()
    {
        // A port nothing listens on - the connection is refused
        // immediately, proving the typed offline path without a listener.
        using var client = new CompanionApiClient("http://127.0.0.1:1", "user", TimeSpan.FromSeconds(5));

        var exception = await Assert.ThrowsAsync<CompanionApiException>(() => client.GetCockpitAsync());

        Assert.Equal(CompanionApiFailureReason.Unreachable, exception.Reason);
        Assert.Null(exception.StatusCode);
    }
}
