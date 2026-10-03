using System.Text;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>
/// The credentials set-up of <see cref="XeroLiveConnection.CreateLiveAsync(XeroLiveSettings)"/>
/// for a supplied access token (`v0.24.0` task X8): the token is used as it
/// is and never refreshed, so nothing — not the placeholder refresh token,
/// not the client credentials — ever reaches Xero's token endpoint; and its
/// granted scopes come from the JWT when it names any, else from
/// <see cref="XeroLiveSettings.ScopesVariable"/>. No network: both handlers
/// are recording stand-ins.
/// </summary>
public sealed class XeroLiveConnectionTests
{
    private const string Tenant = "demo-tenant";

    [Theory]
    [InlineData(1)]   // inside the authoriser's 2-minute refresh skew
    [InlineData(-5)]  // already expired
    public async Task SuppliedToken_NearOrPastExpiry_WithAClientIdGiven_NeverReachesTheTokenEndpoint(int minutesLeft)
    {
        var clock = new XeroSimulatorClock();
        var token = Jwt($"{{\"exp\":{clock.GetUtcNow().AddMinutes(minutesLeft).ToUnixTimeSeconds()}}}");
        var tokenEndpoint = new TerminalHandler();
        var settings = Settings(
            (XeroLiveSettings.AccessTokenVariable, token),
            (XeroLiveSettings.TenantIdVariable, Tenant),
            (XeroLiveSettings.ClientIdVariable, "client-id-from-the-environment"),
            (XeroLiveSettings.ClientSecretVariable, "client-secret-from-the-environment"));

        using var connection = await XeroLiveConnection.CreateLiveAsync(settings, new TerminalHandler(), tokenEndpoint, clock);
        var access = await connection.Authoriser.EnsureAccessTokenAsync();

        Assert.Empty(tokenEndpoint.Received);
        Assert.NotEqual(AccessTokenOutcome.Ok, access.Outcome);
        Assert.Equal(AccessTokenOutcome.NotConfigured, access.Outcome);
    }

    [Fact]
    public async Task SuppliedToken_WithTimeLeft_IsUsedAsItIs()
    {
        var clock = new XeroSimulatorClock();
        var token = Jwt($"{{\"exp\":{clock.GetUtcNow().AddMinutes(20).ToUnixTimeSeconds()}}}");
        var tokenEndpoint = new TerminalHandler();
        var settings = Settings((XeroLiveSettings.AccessTokenVariable, token), (XeroLiveSettings.TenantIdVariable, Tenant));

        using var connection = await XeroLiveConnection.CreateLiveAsync(settings, new TerminalHandler(), tokenEndpoint, clock);
        var access = await connection.Authoriser.EnsureAccessTokenAsync();

        Assert.Equal(AccessTokenOutcome.Ok, access.Outcome);
        Assert.Equal(token, access.AccessToken);
        Assert.Equal(Tenant, access.TenantId);
        Assert.Empty(tokenEndpoint.Received);
    }

    [Fact]
    public async Task SuppliedToken_WithAnEmptyScopeClaim_RecordsTheScopesGiven()
    {
        var clock = new XeroSimulatorClock();
        var token = Jwt($"{{\"exp\":{clock.GetUtcNow().AddMinutes(20).ToUnixTimeSeconds()},\"scope\":[]}}");
        var settings = Settings(
            (XeroLiveSettings.AccessTokenVariable, token),
            (XeroLiveSettings.TenantIdVariable, Tenant),
            (XeroLiveSettings.ScopesVariable, "offline_access accounting.contacts"));

        using var connection = await XeroLiveConnection.CreateLiveAsync(settings, new TerminalHandler(), new TerminalHandler(), clock);

        Assert.Equal(["offline_access", "accounting.contacts"], await connection.Authoriser.ReadGrantedScopesAsync());
    }

    [Fact]
    public async Task SuppliedToken_WithAScopeClaim_RecordsTheClaim_OverTheScopesGiven()
    {
        var clock = new XeroSimulatorClock();
        var token = Jwt($"{{\"exp\":{clock.GetUtcNow().AddMinutes(20).ToUnixTimeSeconds()},\"scope\":[\"accounting.invoices\"]}}");
        var settings = Settings(
            (XeroLiveSettings.AccessTokenVariable, token),
            (XeroLiveSettings.TenantIdVariable, Tenant),
            (XeroLiveSettings.ScopesVariable, "offline_access"));

        using var connection = await XeroLiveConnection.CreateLiveAsync(settings, new TerminalHandler(), new TerminalHandler(), clock);

        Assert.Equal(["accounting.invoices"], await connection.Authoriser.ReadGrantedScopesAsync());
    }

    private static string Jwt(string payloadJson) =>
        "eyJhbGciOiJub25lIn0." + Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".sig";

    private static XeroLiveSettings Settings(params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);
        return new XeroLiveSettings(name => map.TryGetValue(name, out var value) ? value : null);
    }
}
