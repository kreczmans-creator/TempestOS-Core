namespace Tempest.Core.Invoicing.OAuth;

/// <summary>
/// One provider's own fixed OAuth 2.0 endpoints and scopes — everything
/// <see cref="OAuthAuthoriser"/> needs that is not a per-installation
/// secret (`WP 19.1A` part 2). <see cref="Provider"/> also names the
/// <see cref="Tempest.Core.Secrets.ISecretStore"/> key prefix
/// (<c>Invoicing:&lt;Provider&gt;:*</c>) and the configuration key prefix
/// (<c>Invoicing:&lt;Provider&gt;:ClientId</c> etc.) this provider's own
/// credentials and tokens live under.
/// </summary>
/// <param name="Provider">The exact segment naming this provider in every configuration and secret-store key — <c>"Xero"</c> or <c>"QuickBooksOnline"</c>.</param>
/// <param name="AuthorizationEndpoint">Where the system browser is sent to let the operator sign in and consent.</param>
/// <param name="TokenEndpoint">Where an authorisation code or refresh token is exchanged for tokens.</param>
/// <param name="Scopes">The scopes requested — space-joined into the authorisation URL's own <c>scope</c> parameter.</param>
/// <param name="TenantResolutionEndpoint">
/// Where the provider's own tenant/company id is read back after a fresh
/// token exchange, for a provider (Xero) whose redirect itself carries no
/// such id — <see langword="null"/> for a provider (QuickBooks Online)
/// whose redirect already carries one as its own <c>realmId</c> query
/// parameter, which <see cref="OAuthLoopbackListener"/> reads directly.
/// </param>
public sealed record OAuthProviderProfile(
    string Provider,
    Uri AuthorizationEndpoint,
    Uri TokenEndpoint,
    IReadOnlyList<string> Scopes,
    Uri? TenantResolutionEndpoint = null);
