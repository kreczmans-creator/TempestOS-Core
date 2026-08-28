namespace Tempest.Companion.Client;

/// <summary>
/// The Companion's own connection/appearance settings — persisted locally
/// by <see cref="CompanionSettingsStore"/>. Deliberately carries no
/// secret: the platform's identity model is a configured identity id, not
/// a credential (<c>ADR-0043</c>/<c>ADR-0052</c>) — there is no token or
/// password anywhere in this application to store, so nothing here needs
/// a secure enclave. The moment the platform gains real credentials
/// (<c>FCR-0003</c>), this store must move to platform secure storage —
/// recorded as part of `WP 14.0A`'s security review.
/// </summary>
/// <param name="ServerUrl">The TempestOS host's own REST API base URL — loopback by default, matching the platform's own deliberate bind (<c>TD-14</c>).</param>
/// <param name="IdentityId">The identity id sent as <c>X-Identity-Id</c> — empty until the user configures one.</param>
/// <param name="Theme">The requested theme variant name — <c>"Dark"</c> (the instrument theme, the brand default) or <c>"Light"</c> (paper).</param>
public sealed record CompanionClientSettings(string ServerUrl, string IdentityId, string Theme)
{
    /// <summary>The settings a fresh install starts from.</summary>
    public static CompanionClientSettings Default { get; } = new("http://127.0.0.1:5080", string.Empty, "Dark");
}
