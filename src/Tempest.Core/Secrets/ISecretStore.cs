namespace Tempest.Core.Secrets;

/// <summary>
/// Where a connector's own token lives — never the persistence database
/// (`WP 19.1A`, `ADR-0151`). Three plain acts, keyed by a caller-chosen
/// string (for example <c>"Xero:AccessToken"</c>); this contract carries no
/// opinion about what a key names or how many a caller keeps.
/// </summary>
/// <remarks>
/// <b>Not a second persistence store.</b> <see cref="EngineeringDomainContext"/>'s
/// own <c>tempest.db</c> is the one durable authority for engineering state
/// (`ADR-0145`); a connector's bearer token, refresh token or API key is
/// not engineering state, and storing it there would put a credential
/// inside a file this platform backs up, exports and hands to support
/// without a second thought. <see cref="Tempest.Core.Runtime.TempestHost"/>
/// selects <c>WindowsDpapiSecretStore</c> on Windows and
/// <c>FileSecretStore</c> elsewhere; neither implementation lives in this
/// file, so a caller depending only on this interface never knows or cares
/// which backs it.
/// </remarks>
public interface ISecretStore
{
    /// <summary>Reads the value stored under <paramref name="key"/>, or <see langword="null"/> if nothing is stored there.</summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>, creating or overwriting as needed.</summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes whatever is stored under <paramref name="key"/>. A no-op if nothing is stored there.</summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
