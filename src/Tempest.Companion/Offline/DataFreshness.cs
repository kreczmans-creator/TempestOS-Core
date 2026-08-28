namespace Tempest.Companion.Offline;

/// <summary>
/// How current a piece of Companion data is — the explicit freshness
/// vocabulary `WP 14.0A`'s offline model (<c>ADR-0115</c>) requires every
/// screen to disclose. The phone never has authoritative data; the most
/// it can honestly claim is "fetched from the authoritative platform at
/// this moment", and this enum is that claim made visible.
/// </summary>
public enum DataFreshness
{
    /// <summary>Fetched from the platform on this refresh — current as of now.</summary>
    Live,

    /// <summary>The platform is unreachable; showing the last snapshot, which is recent enough to still be useful.</summary>
    Cached,

    /// <summary>The platform is unreachable and the last snapshot is older than the staleness threshold — shown, but flagged as old.</summary>
    Stale,

    /// <summary>The platform is unreachable and no snapshot has ever been stored — nothing to show.</summary>
    Unavailable,
}
