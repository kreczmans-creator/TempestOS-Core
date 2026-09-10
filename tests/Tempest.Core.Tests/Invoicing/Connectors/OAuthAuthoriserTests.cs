using System.Globalization;
using System.Net;
using System.Text;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Secrets;
using Tempest.Core.Tests.Invoicing;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing.Connectors;

/// <summary>
/// <see cref="OAuthAuthoriser"/>'s own acceptance (`WP 19.1A` part 2 brief
/// §1/§3): the authorisation-code round trip with a fake browser and a
/// stubbed token endpoint, silent refresh, <c>Reauthorise</c> on a refused
/// refresh, and tokens that are stored only through <see cref="ISecretStore"/>
/// — never <c>tempest.db</c>.
/// </summary>
public sealed class OAuthAuthoriserTests
{
    private const string Provider = "TestProvider";
    private static readonly Uri AuthorizationEndpoint = new("https://provider.example.test/authorize");
    private static readonly Uri TokenEndpoint = new("https://provider.example.test/token");
    private static readonly Uri TenantEndpoint = new("https://provider.example.test/connections");

    [Fact]
    public async Task AuthoriseAsync_RoundTrip_BuildsAPkceAuthorisationUrl_AndStoresTheReturnedTokens()
    {
        var (authoriser, handler, secretStore, launcher) = Build(withTenantResolution: false);

        handler.When(HttpMethod.Post, "provider.example.test/token", (_, _) =>
            JsonResponse(HttpStatusCode.OK, """{"access_token":"access-1","refresh_token":"refresh-1","expires_in":3600,"token_type":"Bearer"}"""));

        var result = await authoriser.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Ok, result.Outcome);
        await AwaitLauncherAsync(launcher);

        Assert.NotNull(launcher.LastAuthorizationUrl);
        var query = launcher.LastAuthorizationUrl!.Query;
        Assert.Contains("response_type=code", query, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", query, StringComparison.Ordinal);
        Assert.Contains("client_id=client-abc", query, StringComparison.Ordinal);
        Assert.Contains("redirect_uri=http", query, StringComparison.Ordinal);

        Assert.Equal("access-1", await secretStore.GetAsync($"Invoicing:{Provider}:AccessToken"));
        Assert.Equal("refresh-1", await secretStore.GetAsync($"Invoicing:{Provider}:RefreshToken"));
        Assert.NotNull(await secretStore.GetAsync($"Invoicing:{Provider}:ExpiresAtUtc"));
    }

    [Fact]
    public async Task AuthoriseAsync_ResolvesTenantId_ViaTheTenantResolutionEndpoint_WhenTheRedirectCarriesNoRealmId()
    {
        var (authoriser, handler, secretStore, launcher) = Build(withTenantResolution: true);

        handler.When(HttpMethod.Post, "provider.example.test/token", (_, _) =>
            JsonResponse(HttpStatusCode.OK, """{"access_token":"access-1","refresh_token":"refresh-1","expires_in":3600}"""));
        handler.When(HttpMethod.Get, "provider.example.test/connections", (_, _) =>
            JsonResponse(HttpStatusCode.OK, """[{"tenantId":"tenant-xyz","tenantName":"Acme"}]"""));

        var result = await authoriser.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Ok, result.Outcome);
        await AwaitLauncherAsync(launcher);
        Assert.Equal("tenant-xyz", await secretStore.GetAsync($"Invoicing:{Provider}:TenantId"));
    }

    [Fact]
    public async Task AuthoriseAsync_UsesRealmIdFromTheCallback_NeverCallsTheTenantResolutionEndpoint()
    {
        var (authoriser, handler, secretStore, launcher) = Build(withTenantResolution: true);
        launcher.RealmId = "realm-123";

        handler.When(HttpMethod.Post, "provider.example.test/token", (_, _) =>
            JsonResponse(HttpStatusCode.OK, """{"access_token":"access-1","refresh_token":"refresh-1","expires_in":3600}"""));

        var result = await authoriser.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Ok, result.Outcome);
        await AwaitLauncherAsync(launcher);
        Assert.Equal("realm-123", await secretStore.GetAsync($"Invoicing:{Provider}:TenantId"));
        Assert.DoesNotContain(handler.Calls, c => c.Uri.ToString().Contains("connections", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AuthoriseAsync_OperatorDeniesConsent_ReturnsFailed_NoTokensStored()
    {
        var (authoriser, _, secretStore, launcher) = Build(withTenantResolution: false);
        launcher.Error = "access_denied";

        var result = await authoriser.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Failed, result.Outcome);
        Assert.Contains("access_denied", result.Reason, StringComparison.Ordinal);
        await AwaitLauncherAsync(launcher);
        Assert.Null(await secretStore.GetAsync($"Invoicing:{Provider}:AccessToken"));
    }

    [Fact]
    public async Task AuthoriseAsync_StateMismatch_ReturnsFailed_NoTokensStored()
    {
        var (authoriser, _, secretStore, launcher) = Build(withTenantResolution: false);
        launcher.CorruptState = true;

        var result = await authoriser.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Failed, result.Outcome);
        await AwaitLauncherAsync(launcher);
        Assert.Null(await secretStore.GetAsync($"Invoicing:{Provider}:AccessToken"));
    }

    [Fact]
    public async Task AuthoriseAsync_TokenEndpointRefusesTheCode_ReturnsFailed()
    {
        var (authoriser, handler, secretStore, launcher) = Build(withTenantResolution: false);
        handler.When(HttpMethod.Post, "provider.example.test/token", (_, _) => JsonResponse(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));

        var result = await authoriser.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Failed, result.Outcome);
        await AwaitLauncherAsync(launcher);
        Assert.Null(await secretStore.GetAsync($"Invoicing:{Provider}:AccessToken"));
    }

    [Fact]
    public async Task AuthoriseAsync_NoClientIdConfigured_ReturnsNotConfigured_NeverOpensTheBrowser()
    {
        var configuration = BuildConfiguration(clientId: null);
        var secretStore = new InMemorySecretStore();
        var launcher = new FakeBrowserLauncher();
        var handler = new StubHttpMessageHandler();
        var authoriser = new OAuthAuthoriser(
            new OAuthProviderProfile(Provider, AuthorizationEndpoint, TokenEndpoint, ["scope-a"]),
            configuration, secretStore, launcher, new HttpClient(handler));

        var result = await authoriser.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.NotConfigured, result.Outcome);
        Assert.Null(launcher.LastAuthorizationUrl);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task AuthoriseAsync_NoPortConfigured_UsesTheDefaultLoopbackPort()
    {
        var (authoriser, handler, _, launcher) = Build(withTenantResolution: false);

        handler.When(HttpMethod.Post, "provider.example.test/token", (_, _) =>
            JsonResponse(HttpStatusCode.OK, """{"access_token":"access-1","refresh_token":"refresh-1","expires_in":3600}"""));

        var result = await authoriser.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Ok, result.Outcome);
        await AwaitLauncherAsync(launcher);
        AssertRedirectUriPort(launcher, OAuthAuthoriser.DefaultLoopbackPort);
    }

    [Fact]
    public async Task AuthoriseAsync_AConfiguredPort_IsHonoured()
    {
        var configuredPort = FindAFreeTcpPort();
        var configuration = BuildConfiguration("client-abc", loopbackPort: configuredPort);
        var secretStore = new InMemorySecretStore();
        var launcher = new FakeBrowserLauncher();
        var handler = new StubHttpMessageHandler();
        var authoriser = new OAuthAuthoriser(
            new OAuthProviderProfile(Provider, AuthorizationEndpoint, TokenEndpoint, ["scope-a"]),
            configuration, secretStore, launcher, new HttpClient(handler));

        handler.When(HttpMethod.Post, "provider.example.test/token", (_, _) =>
            JsonResponse(HttpStatusCode.OK, """{"access_token":"access-1","refresh_token":"refresh-1","expires_in":3600}"""));

        var result = await authoriser.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Ok, result.Outcome);
        await AwaitLauncherAsync(launcher);
        AssertRedirectUriPort(launcher, configuredPort);
    }

    [Fact]
    public async Task AuthoriseAsync_PortConfiguredAsZero_PicksAFreeEphemeralPort_NeverTheLiteralZero()
    {
        var configuration = BuildConfiguration("client-abc", loopbackPort: 0);
        var secretStore = new InMemorySecretStore();
        var launcher = new FakeBrowserLauncher();
        var handler = new StubHttpMessageHandler();
        var authoriser = new OAuthAuthoriser(
            new OAuthProviderProfile(Provider, AuthorizationEndpoint, TokenEndpoint, ["scope-a"]),
            configuration, secretStore, launcher, new HttpClient(handler));

        handler.When(HttpMethod.Post, "provider.example.test/token", (_, _) =>
            JsonResponse(HttpStatusCode.OK, """{"access_token":"access-1","refresh_token":"refresh-1","expires_in":3600}"""));

        var result = await authoriser.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Ok, result.Outcome);
        await AwaitLauncherAsync(launcher);
        Assert.NotEqual(0, RedirectUriPort(launcher));
    }

    [Fact]
    public async Task AuthoriseAsync_ThePortIsAlreadyInUse_ReturnsFailed_NamingThePortAndTheConfigurationKey_NeverThrows()
    {
        var busyPort = FindAFreeTcpPort();
        using var occupier = new System.Net.HttpListener();
        occupier.Prefixes.Add($"http://127.0.0.1:{busyPort}/callback/");
        occupier.Start();

        var configuration = BuildConfiguration("client-abc", loopbackPort: busyPort);
        var authoriser = new OAuthAuthoriser(
            new OAuthProviderProfile(Provider, AuthorizationEndpoint, TokenEndpoint, ["scope-a"]),
            configuration, new InMemorySecretStore(), new FakeBrowserLauncher(), new HttpClient(new StubHttpMessageHandler()));

        var result = await authoriser.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Failed, result.Outcome);
        Assert.Contains(busyPort.ToString(CultureInfo.InvariantCulture), result.Reason, StringComparison.Ordinal);
        Assert.Contains(OAuthAuthoriser.LoopbackPortConfigurationKey, result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureAccessTokenAsync_NotAuthorised_WhenNothingIsStored()
    {
        var (authoriser, _, _, _) = Build(withTenantResolution: false);

        var result = await authoriser.EnsureAccessTokenAsync();

        Assert.Equal(AccessTokenOutcome.NotAuthorised, result.Outcome);
    }

    [Fact]
    public async Task EnsureAccessTokenAsync_ReturnsTheStoredToken_WithoutARefreshCall_WhenNotExpired()
    {
        var (authoriser, handler, secretStore, _) = Build(withTenantResolution: false);
        var future = DateTimeOffset.UtcNow.AddHours(1);
        await SeedAsync(secretStore, accessToken: "still-valid", refreshToken: "refresh-1", expiresAtUtc: future, tenantId: "tenant-1");

        var result = await authoriser.EnsureAccessTokenAsync();

        Assert.Equal(AccessTokenOutcome.Ok, result.Outcome);
        Assert.Equal("still-valid", result.AccessToken);
        Assert.Equal("tenant-1", result.TenantId);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task EnsureAccessTokenAsync_RefreshesSilently_WhenTheStoredTokenHasExpired()
    {
        var (authoriser, handler, secretStore, _) = Build(withTenantResolution: false);
        var past = DateTimeOffset.UtcNow.AddHours(-1);
        await SeedAsync(secretStore, accessToken: "expired", refreshToken: "refresh-1", expiresAtUtc: past, tenantId: "tenant-1");

        handler.When(HttpMethod.Post, "provider.example.test/token", (_, body) =>
        {
            Assert.Contains("grant_type=refresh_token", body, StringComparison.Ordinal);
            Assert.Contains("refresh_token=refresh-1", body, StringComparison.Ordinal);
            return JsonResponse(HttpStatusCode.OK, """{"access_token":"fresh-access","expires_in":3600}""");
        });

        var result = await authoriser.EnsureAccessTokenAsync();

        Assert.Equal(AccessTokenOutcome.Ok, result.Outcome);
        Assert.Equal("fresh-access", result.AccessToken);
        Assert.Equal("fresh-access", await secretStore.GetAsync($"Invoicing:{Provider}:AccessToken"));

        // The refresh response omitted its own refresh_token — the
        // existing one is kept rather than discarded.
        Assert.Equal("refresh-1", await secretStore.GetAsync($"Invoicing:{Provider}:RefreshToken"));
    }

    [Fact]
    public async Task EnsureAccessTokenAsync_MarksReauthorise_WhenTheRefreshTokenIsRefused()
    {
        var (authoriser, handler, secretStore, _) = Build(withTenantResolution: false);
        var past = DateTimeOffset.UtcNow.AddHours(-1);
        await SeedAsync(secretStore, accessToken: "expired", refreshToken: "revoked-refresh", expiresAtUtc: past, tenantId: "tenant-1");

        handler.When(HttpMethod.Post, "provider.example.test/token", (_, _) => JsonResponse(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));

        var result = await authoriser.EnsureAccessTokenAsync();

        Assert.Equal(AccessTokenOutcome.Reauthorise, result.Outcome);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task EnsureAccessTokenAsync_NotConfigured_WhenARefreshIsNeededButNoClientIdExists()
    {
        var configuration = BuildConfiguration(clientId: null);
        var secretStore = new InMemorySecretStore();
        var handler = new StubHttpMessageHandler();
        var authoriser = new OAuthAuthoriser(
            new OAuthProviderProfile(Provider, AuthorizationEndpoint, TokenEndpoint, ["scope-a"]),
            configuration, secretStore, new FakeBrowserLauncher(), new HttpClient(handler));

        await SeedAsync(secretStore, "expired", "refresh-1", DateTimeOffset.UtcNow.AddHours(-1), "tenant-1");

        var result = await authoriser.EnsureAccessTokenAsync();

        Assert.Equal(AccessTokenOutcome.NotConfigured, result.Outcome);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task ForgetTokensAsync_ClearsEveryStoredValue()
    {
        var (authoriser, _, secretStore, _) = Build(withTenantResolution: false);
        await SeedAsync(secretStore, "access-1", "refresh-1", DateTimeOffset.UtcNow.AddHours(1), "tenant-1");

        await authoriser.ForgetTokensAsync();

        Assert.Null(await secretStore.GetAsync($"Invoicing:{Provider}:AccessToken"));
        Assert.Null(await secretStore.GetAsync($"Invoicing:{Provider}:RefreshToken"));
        Assert.Null(await secretStore.GetAsync($"Invoicing:{Provider}:ExpiresAtUtc"));
        Assert.Null(await secretStore.GetAsync($"Invoicing:{Provider}:TenantId"));
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task AStoredToken_IsNeverWrittenToTempestDb()
    {
        using var temp = new TempDirectory();
        var secretsDirectory = Path.Combine(temp.Path, "secrets");
        var secretStore = new WindowsDpapiSecretStore(secretsDirectory);
        var launcher = new FakeBrowserLauncher();
        var handler = new StubHttpMessageHandler();
        const string accessTokenMarker = "oauth-access-token-marker-b7f13a9c";
        const string refreshTokenMarker = "oauth-refresh-token-marker-2d4e881f";

        handler.When(HttpMethod.Post, "provider.example.test/token", (_, _) =>
            JsonResponse(HttpStatusCode.OK, $$"""{"access_token":"{{accessTokenMarker}}","refresh_token":"{{refreshTokenMarker}}","expires_in":3600}"""));

        var authoriser = new OAuthAuthoriser(
            new OAuthProviderProfile(Provider, AuthorizationEndpoint, TokenEndpoint, ["scope-a"]),
            BuildConfiguration("client-abc"), secretStore, launcher, new HttpClient(handler));

        var result = await authoriser.AuthoriseAsync(TestTimeout());
        Assert.Equal(OAuthOutcome.Ok, result.Outcome);
        await AwaitLauncherAsync(launcher);

        // Drive a real host over the same persistence root, with real,
        // committed engineering data to grep — SecretStoreTests's own
        // pattern, applied to a token OAuthAuthoriser itself produced
        // rather than one a test sets directly.
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);
        await InvoicingTestHost.CreateProjectAsync(host, "OAUTH-DB-PROBE");
        await manager.ShutdownAsync();
        await host.DisposeAsync();

        var databasePath = Path.Combine(temp.Path, Tempest.Core.Persistence.SqlitePersistenceStore.DatabaseFileName);
        Assert.True(File.Exists(databasePath));

        var databaseBytes = await File.ReadAllBytesAsync(databasePath);
        Assert.True(databaseBytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(accessTokenMarker)) < 0, "The access token's own bytes were found inside tempest.db.");
        Assert.True(databaseBytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(refreshTokenMarker)) < 0, "The refresh token's own bytes were found inside tempest.db.");
    }

    // ====================================================================
    // Fixtures
    // ====================================================================

    private static (OAuthAuthoriser Authoriser, StubHttpMessageHandler Handler, InMemorySecretStore SecretStore, FakeBrowserLauncher Launcher) Build(bool withTenantResolution)
    {
        var handler = new StubHttpMessageHandler();
        var secretStore = new InMemorySecretStore();
        var launcher = new FakeBrowserLauncher();
        var configuration = BuildConfiguration("client-abc");

        var profile = new OAuthProviderProfile(
            Provider, AuthorizationEndpoint, TokenEndpoint, ["scope-a", "scope-b"],
            withTenantResolution ? TenantEndpoint : null);

        var authoriser = new OAuthAuthoriser(profile, configuration, secretStore, launcher, new HttpClient(handler));

        return (authoriser, handler, secretStore, launcher);
    }

    private static IConfigurationProvider BuildConfiguration(string? clientId, int? loopbackPort = null)
    {
        var entries = new List<KeyValuePair<string, string>>();
        if (clientId is not null)
            entries.Add(new($"Invoicing:{Provider}:ClientId", clientId));

        if (loopbackPort is { } port)
            entries.Add(new(OAuthAuthoriser.LoopbackPortConfigurationKey, port.ToString(CultureInfo.InvariantCulture)));

        return new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(entries)).Build();
    }

    /// <summary>The loopback port the last authorisation URL's own <c>redirect_uri</c> query parameter named.</summary>
    private static int RedirectUriPort(FakeBrowserLauncher launcher)
    {
        Assert.NotNull(launcher.LastAuthorizationUrl);
        var redirectUriValue = launcher.LastAuthorizationUrl!.Query
            .TrimStart('?')
            .Split('&')
            .Select(pair => pair.Split(['='], 2))
            .Where(parts => parts.Length == 2 && parts[0] == "redirect_uri")
            .Select(parts => Uri.UnescapeDataString(parts[1]))
            .Single();

        return new Uri(redirectUriValue).Port;
    }

    private static void AssertRedirectUriPort(FakeBrowserLauncher launcher, int expectedPort) =>
        Assert.Equal(expectedPort, RedirectUriPort(launcher));

    /// <summary>Asks the OS for a free loopback port and releases it immediately — <see cref="OAuthLoopbackListener"/>'s own accepted probe-then-rebind race, used here only to name a port a real listener can then occupy.</summary>
    private static int FindAFreeTcpPort()
    {
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            return ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }

    private static async Task SeedAsync(ISecretStore secretStore, string accessToken, string refreshToken, DateTimeOffset expiresAtUtc, string? tenantId)
    {
        await secretStore.SetAsync($"Invoicing:{Provider}:AccessToken", accessToken);
        await secretStore.SetAsync($"Invoicing:{Provider}:RefreshToken", refreshToken);
        await secretStore.SetAsync($"Invoicing:{Provider}:ExpiresAtUtc", expiresAtUtc.ToString("O", CultureInfo.InvariantCulture));

        if (tenantId is not null)
            await secretStore.SetAsync($"Invoicing:{Provider}:TenantId", tenantId);
    }

    private static Task AwaitLauncherAsync(FakeBrowserLauncher launcher) => launcher.LastSimulatedRedirect ?? Task.CompletedTask;

    /// <summary>A generous but finite timeout for a real loopback wait, so a genuine defect fails the test outright rather than hanging the run.</summary>
    private static CancellationToken TestTimeout() => new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
