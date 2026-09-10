using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Tempest.Core.Configuration;
using Tempest.Core.Secrets;

namespace Tempest.Core.Invoicing.OAuth;

/// <summary>
/// OAuth 2.0 authorisation code with PKCE, over a loopback redirect and the
/// system browser — the shared machinery <c>XeroConnector</c> and
/// <c>QuickBooksOnlineConnector</c> each drive through their own
/// <see cref="OAuthProviderProfile"/> (`WP 19.1A` part 2, brief §1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two entry points, two audiences.</b> <see cref="AuthoriseAsync"/> is
/// the interactive round trip — starts the loopback listener, opens the
/// system browser, waits for the redirect, exchanges the code — the act a
/// Settings screen's own "Connect" button drives (`WP 19.2B`, out of this
/// part's own scope; exercised directly by this part's own tests instead).
/// <see cref="EnsureAccessTokenAsync"/> is what a connector calls before
/// every outbound call: answers a currently-valid access token, refreshing
/// silently first if the stored one has expired, and reports
/// <see cref="AccessTokenOutcome.Reauthorise"/> rather than throwing when
/// the refresh token itself is refused — a connector maps that straight to
/// <see cref="ConnectorOutcome.Reauthorise"/>, never a crash.
/// </para>
/// <para>
/// <b>Tokens live only in <see cref="ISecretStore"/>, never the database.</b>
/// Every value this class stores — access token, refresh token, expiry, and
/// the provider's own tenant/company id — is written under
/// <c>Invoicing:&lt;Provider&gt;:*</c> keys, exactly the shape
/// <c>ISecretStore</c>'s own remarks describe; nothing here ever touches
/// <c>EngineeringDomainContext</c>.
/// </para>
/// <para>
/// <b>Client id and secret: configuration first, the secret store second,
/// never the database.</b> Each is read from
/// <c>Invoicing:&lt;Provider&gt;:ClientId</c>/<c>ClientSecret</c> in
/// <see cref="IConfigurationProvider"/> (an operator's own
/// <c>appsettings.json</c> or environment variable) if present, falling
/// back to the identical key name in <see cref="ISecretStore"/> — where a
/// Settings screen (`WP 19.2B`) writes what the operator types rather than
/// requiring a config file edit. A client id resolved from neither source
/// is <see cref="AccessTokenOutcome.NotConfigured"/>/<see cref="OAuthOutcome.NotConfigured"/>,
/// never a crash — the brief's own "Selection" acceptance (§4).
/// </para>
/// </remarks>
public sealed class OAuthAuthoriser
{
    /// <summary>
    /// The <see cref="IConfigurationProvider"/> key naming the exact
    /// loopback port <see cref="OAuthLoopbackListener"/> binds
    /// (`WP 19.1A-R1` disclosure #1) — <see cref="DefaultLoopbackPort"/>
    /// when unset, <c>0</c> to keep the original ephemeral-port behaviour
    /// (a test's own choice; never the right value for a real sandbox
    /// registration, which needs one exact redirect URI to register ahead
    /// of time).
    /// </summary>
    public const string LoopbackPortConfigurationKey = "Invoicing:OAuth:LoopbackPort";

    /// <summary>The loopback port used when <see cref="LoopbackPortConfigurationKey"/> is not configured — also the port <c>docs/adr/ADR-0151-addendum.md</c> and the release notes name as the one to register.</summary>
    public const int DefaultLoopbackPort = 49301;

    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OAuthProviderProfile _profile;
    private readonly IConfigurationProvider _configuration;
    private readonly ISecretStore _secretStore;
    private readonly IBrowserLauncher _browserLauncher;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="OAuthAuthoriser"/> class.</summary>
    public OAuthAuthoriser(
        OAuthProviderProfile profile, IConfigurationProvider configuration, ISecretStore secretStore,
        IBrowserLauncher browserLauncher, HttpClient httpClient, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(browserLauncher);
        ArgumentNullException.ThrowIfNull(httpClient);

        _profile = profile;
        _configuration = configuration;
        _secretStore = secretStore;
        _browserLauncher = browserLauncher;
        _httpClient = httpClient;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The provider this instance authorises — <see cref="OAuthProviderProfile.Provider"/>, verbatim.</summary>
    public string Provider => _profile.Provider;

    /// <summary>
    /// Runs the full interactive authorisation-code round trip: resolves
    /// the client id/secret, starts a loopback listener, builds the
    /// authorisation URL with a fresh <c>state</c> and PKCE
    /// <c>code_challenge</c>, opens it in the system browser, waits for the
    /// single redirect, exchanges the code for tokens, and stores them.
    /// </summary>
    public async Task<OAuthResult> AuthoriseAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveClientCredentialsAsync(cancellationToken).ConfigureAwait(false);
        if (credentials is null)
            return OAuthResult.NotConfigured();

        var loopbackPort = ResolveLoopbackPort();

        OAuthLoopbackListener loopbackListener;
        try
        {
            loopbackListener = new OAuthLoopbackListener(loopbackPort);
        }
        catch (HttpListenerException)
        {
            // Never a crash (`WP 19.1A-R1` disclosure #1): named so an
            // operator can act on it directly — the exact port attempted
            // and the configuration key that picked it, whether that was
            // this key's own configured value or its default.
            return OAuthResult.Failed(
                $"Port {loopbackPort} is already in use; free it or configure a different port under '{LoopbackPortConfigurationKey}'.");
        }

        using var loopback = loopbackListener;

        var state = Guid.NewGuid().ToString("N");
        var (verifier, challenge) = PkceGenerator.Generate();
        var authorisationUrl = BuildAuthorizationUrl(credentials.Value.ClientId, loopback.RedirectUri, state, challenge);

        _browserLauncher.Open(authorisationUrl);

        var callback = await loopback.WaitForCallbackAsync(cancellationToken).ConfigureAwait(false);

        if (callback.Error is not null)
            return OAuthResult.Failed($"The provider reported: {callback.Error}.");

        if (string.IsNullOrEmpty(callback.Code))
            return OAuthResult.Failed("The authorisation response carried no code.");

        if (!string.Equals(callback.State, state, StringComparison.Ordinal))
            return OAuthResult.Failed("The authorisation response carried the wrong state; the redirect is not trusted.");

        var tokenResponse = await ExchangeCodeAsync(credentials.Value, callback.Code, loopback.RedirectUri, verifier, cancellationToken).ConfigureAwait(false);
        if (tokenResponse is null)
            return OAuthResult.Failed("The token endpoint refused the authorisation code.");

        var tenantId = callback.RealmId;
        if (string.IsNullOrEmpty(tenantId) && _profile.TenantResolutionEndpoint is { } tenantEndpoint)
            tenantId = await ResolveTenantIdAsync(tenantEndpoint, tokenResponse.AccessToken, cancellationToken).ConfigureAwait(false);

        await StoreTokensAsync(tokenResponse, tenantId, cancellationToken).ConfigureAwait(false);

        return OAuthResult.Ok();
    }

    /// <summary>
    /// Answers a currently-valid access token for a connector's own next
    /// call — refreshing first, silently, if the stored one has expired (or
    /// is within <see cref="RefreshSkew"/> of expiring).
    /// </summary>
    public async Task<AccessTokenResult> EnsureAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await _secretStore.GetAsync(Key("RefreshToken"), cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(refreshToken))
        {
            // Never authorised. A missing client id is reported as
            // NotConfigured rather than a bare NotAuthorised — the brief's
            // own "Selection" acceptance (§4): an operator who has never
            // even registered a sandbox app gets a diagnosis naming that,
            // not a generic "sign in" prompt with nothing to act on.
            var neverAuthorisedCredentials = await ResolveClientCredentialsAsync(cancellationToken).ConfigureAwait(false);
            return neverAuthorisedCredentials is null ? AccessTokenResult.NotConfigured() : AccessTokenResult.NotAuthorised();
        }

        var accessToken = await _secretStore.GetAsync(Key("AccessToken"), cancellationToken).ConfigureAwait(false);
        var tenantId = await _secretStore.GetAsync(Key("TenantId"), cancellationToken).ConfigureAwait(false);
        var expiresRaw = await _secretStore.GetAsync(Key("ExpiresAtUtc"), cancellationToken).ConfigureAwait(false);

        var stillValid = !string.IsNullOrEmpty(accessToken)
            && DateTimeOffset.TryParse(expiresRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt)
            && expiresAt > _time.GetUtcNow() + RefreshSkew;

        if (stillValid)
            return AccessTokenResult.Ok(accessToken!, tenantId);

        var credentials = await ResolveClientCredentialsAsync(cancellationToken).ConfigureAwait(false);
        if (credentials is null)
            return AccessTokenResult.NotConfigured();

        var refreshed = await RefreshAsync(credentials.Value, refreshToken, cancellationToken).ConfigureAwait(false);
        if (refreshed is null)
            return AccessTokenResult.Reauthorise("The stored refresh token was refused; sign in again to re-authorise.");

        await StoreTokensAsync(refreshed, tenantId, cancellationToken).ConfigureAwait(false);

        return AccessTokenResult.Ok(refreshed.AccessToken, tenantId);
    }

    /// <summary>Forgets every token this instance has stored — a Settings "Disconnect" act (`WP 19.2B`, out of this part's own scope) would call this; exposed now so a test can assert a clean slate.</summary>
    public async Task ForgetTokensAsync(CancellationToken cancellationToken = default)
    {
        await _secretStore.RemoveAsync(Key("AccessToken"), cancellationToken).ConfigureAwait(false);
        await _secretStore.RemoveAsync(Key("RefreshToken"), cancellationToken).ConfigureAwait(false);
        await _secretStore.RemoveAsync(Key("ExpiresAtUtc"), cancellationToken).ConfigureAwait(false);
        await _secretStore.RemoveAsync(Key("TenantId"), cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string ClientId, string? ClientSecret)?> ResolveClientCredentialsAsync(CancellationToken cancellationToken)
    {
        var clientId = await ResolveConfiguredOrSecretAsync("ClientId", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(clientId))
            return null;

        var clientSecret = await ResolveConfiguredOrSecretAsync("ClientSecret", cancellationToken).ConfigureAwait(false);

        return (clientId, clientSecret);
    }

    private async Task<string?> ResolveConfiguredOrSecretAsync(string suffix, CancellationToken cancellationToken)
    {
        var key = Key(suffix);

        if (_configuration.TryGetValue(key, out var configured) && !string.IsNullOrWhiteSpace(configured))
            return configured;

        return await _secretStore.GetAsync(key, cancellationToken).ConfigureAwait(false);
    }

    private Uri BuildAuthorizationUrl(string clientId, Uri redirectUri, string state, string codeChallenge)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("response_type", "code"),
            new("client_id", clientId),
            new("redirect_uri", redirectUri.ToString()),
            new("scope", string.Join(' ', _profile.Scopes)),
            new("state", state),
            new("code_challenge", codeChallenge),
            new("code_challenge_method", "S256"),
        };

        var queryString = string.Join('&', query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return new Uri($"{_profile.AuthorizationEndpoint}?{queryString}");
    }

    private async Task<OAuthTokenResponse?> ExchangeCodeAsync(
        (string ClientId, string? ClientSecret) credentials, string code, Uri redirectUri, string codeVerifier, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri.ToString(),
            ["client_id"] = credentials.ClientId,
            ["code_verifier"] = codeVerifier,
        };

        if (!string.IsNullOrEmpty(credentials.ClientSecret))
            form["client_secret"] = credentials.ClientSecret;

        return await PostForTokenAsync(form, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OAuthTokenResponse?> RefreshAsync(
        (string ClientId, string? ClientSecret) credentials, string refreshToken, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = credentials.ClientId,
        };

        if (!string.IsNullOrEmpty(credentials.ClientSecret))
            form["client_secret"] = credentials.ClientSecret;

        return await PostForTokenAsync(form, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OAuthTokenResponse?> PostForTokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _profile.TokenEndpoint) { Content = new FormUrlEncodedContent(form) };

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<string?> ResolveTenantIdAsync(Uri endpoint, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(body);

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("tenantId", out var tenantIdProperty))
                    return tenantIdProperty.GetString();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or OperationCanceledException)
        {
            // Best-effort only: AuthoriseAsync still succeeds without a
            // tenant id here — a connector that genuinely needs one refuses
            // with Reauthorise at call time instead of this method ever
            // failing the whole round trip over it.
        }

        return null;
    }

    private async Task StoreTokensAsync(OAuthTokenResponse token, string? tenantId, CancellationToken cancellationToken)
    {
        await _secretStore.SetAsync(Key("AccessToken"), token.AccessToken, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(token.RefreshToken))
            await _secretStore.SetAsync(Key("RefreshToken"), token.RefreshToken, cancellationToken).ConfigureAwait(false);

        var expiresAt = _time.GetUtcNow() + TimeSpan.FromSeconds(Math.Max(0, token.ExpiresIn));
        await _secretStore.SetAsync(Key("ExpiresAtUtc"), expiresAt.ToString("O", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(tenantId))
            await _secretStore.SetAsync(Key("TenantId"), tenantId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves <see cref="LoopbackPortConfigurationKey"/> — the configured
    /// value if one parses as a non-negative integer, <see cref="DefaultLoopbackPort"/>
    /// otherwise. <c>0</c> is a valid, deliberate configured value: it
    /// keeps the original ephemeral-port behaviour rather than binding a
    /// fixed port at all.
    /// </summary>
    private int ResolveLoopbackPort() =>
        _configuration.TryGetValue(LoopbackPortConfigurationKey, out var raw)
        && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var configured)
        && configured >= 0
            ? configured
            : DefaultLoopbackPort;

    private string Key(string suffix) => $"Invoicing:{_profile.Provider}:{suffix}";
}
