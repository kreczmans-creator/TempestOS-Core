namespace Tempest.Core.Identity;

/// <summary>
/// Resolves the <see cref="IPrincipal"/> performing the current operation.
/// </summary>
/// <remarks>
/// DI-public, consumed exactly like <see cref="Logging.ILogger"/> or
/// <see cref="Events.IEventBus"/> by any service or module needing to know
/// who is acting. Read-only by design — establishing a principal as
/// current is the presentation layer's own responsibility (`WP 17.2A`'s
/// <c>WorkspaceHost</c>, via the concrete <see cref="CurrentPrincipalAccessor"/>), not
/// something an arbitrary consumer of this interface can do.
/// </remarks>
public interface ICurrentPrincipalAccessor
{
    /// <summary>
    /// Gets the current principal, or <see langword="null"/> if no
    /// principal has been established for the current operation. This is
    /// a normal, honestly-reported state — not every caller is expected to
    /// have a principal established at every point in time (for example,
    /// before <see cref="CurrentPrincipalAccessor.SetCurrent"/> is
    /// first called) — mirroring <c>ITempestHost.Services</c>'s own
    /// null-before-ready convention (ADR-0034).
    /// </summary>
    IPrincipal? Current { get; }
}
