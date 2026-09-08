namespace Tempest.App.Workspace;

/// <summary>
/// The Workspace's own closed, four-value status vocabulary
/// (`WP8.0C UX Specification.md` §3, "Status indicators" — "a closed,
/// small vocabulary reused everywhere... never a screen-specific status
/// vocabulary invented ad hoc"). Not one of the twelve `WP8.0B Workspace
/// Contracts.md` interfaces — a genuine, disclosed implementation-phase
/// addition, first used by <see cref="EngineeringCockpit"/>.
/// </summary>
internal enum EngineeringHealthStatus
{
    /// <summary>No signal exists yet to derive a status from.</summary>
    Unknown,

    /// <summary>Everything tracked is in a good state.</summary>
    Healthy,

    /// <summary>Something tracked needs a user's attention soon, not urgently.</summary>
    Attention,

    /// <summary>Something tracked is blocking progress.</summary>
    Blocked,
}
