namespace Tempest.Core.Invoicing.OAuth;

/// <summary>The outcome of one <see cref="OAuthAuthoriser.AuthoriseAsync"/> interactive round trip.</summary>
public enum OAuthOutcome
{
    /// <summary>The round trip completed; tokens are stored.</summary>
    Ok,

    /// <summary>No client id is configured for this provider (in <see cref="Tempest.Core.Configuration.IConfigurationProvider"/> or <see cref="Tempest.Core.Secrets.ISecretStore"/>) — nothing was attempted.</summary>
    NotConfigured,

    /// <summary>The operator denied consent, the state did not match, or the token endpoint refused the code.</summary>
    Failed,
}

/// <summary>What <see cref="OAuthAuthoriser.AuthoriseAsync"/> answers.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Reason">Why, for <see cref="OAuthOutcome.NotConfigured"/> or <see cref="OAuthOutcome.Failed"/>. <see langword="null"/> for <see cref="OAuthOutcome.Ok"/>.</param>
public sealed record OAuthResult(OAuthOutcome Outcome, string? Reason = null)
{
    /// <summary>The round trip completed; tokens are stored.</summary>
    public static OAuthResult Ok() => new(OAuthOutcome.Ok);

    /// <summary>No client id is configured for this provider.</summary>
    public static OAuthResult NotConfigured() => new(OAuthOutcome.NotConfigured, "not configured");

    /// <summary>The round trip failed, with <paramref name="reason"/>.</summary>
    public static OAuthResult Failed(string reason) => new(OAuthOutcome.Failed, reason);
}

/// <summary>The outcome of one <see cref="OAuthAuthoriser.EnsureAccessTokenAsync"/> call — every connector's own pre-flight before it makes a call the token guards.</summary>
public enum AccessTokenOutcome
{
    /// <summary>A current, usable access token is available.</summary>
    Ok,

    /// <summary>No token is stored — the operator has never authorised this connector.</summary>
    NotAuthorised,

    /// <summary>No client id is configured for this provider — the stored refresh token, if any, cannot be exchanged without one.</summary>
    NotConfigured,

    /// <summary>The stored refresh token was refused; nothing further is sent until the operator re-authorises.</summary>
    Reauthorise,
}

/// <summary>What <see cref="OAuthAuthoriser.EnsureAccessTokenAsync"/> answers.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="AccessToken">A current, usable access token, when <see cref="Outcome"/> is <see cref="AccessTokenOutcome.Ok"/>. <see langword="null"/> otherwise.</param>
/// <param name="TenantId">The provider's own tenant/company id (Xero's <c>tenantId</c>, QuickBooks Online's <c>realmId</c>), when one is stored. <see langword="null"/> for a provider that needs none, or when <see cref="Outcome"/> is not <see cref="AccessTokenOutcome.Ok"/>.</param>
/// <param name="Reason">Why, for <see cref="AccessTokenOutcome.NotConfigured"/> or <see cref="AccessTokenOutcome.Reauthorise"/>. <see langword="null"/> otherwise.</param>
public sealed record AccessTokenResult(AccessTokenOutcome Outcome, string? AccessToken = null, string? TenantId = null, string? Reason = null)
{
    /// <summary>A current, usable access token.</summary>
    public static AccessTokenResult Ok(string accessToken, string? tenantId) => new(AccessTokenOutcome.Ok, accessToken, tenantId);

    /// <summary>No token is stored.</summary>
    public static AccessTokenResult NotAuthorised() => new(AccessTokenOutcome.NotAuthorised);

    /// <summary>No client id is configured for this provider.</summary>
    public static AccessTokenResult NotConfigured() => new(AccessTokenOutcome.NotConfigured, Reason: "not configured");

    /// <summary>The stored refresh token was refused.</summary>
    public static AccessTokenResult Reauthorise(string? reason) => new(AccessTokenOutcome.Reauthorise, Reason: reason);
}
