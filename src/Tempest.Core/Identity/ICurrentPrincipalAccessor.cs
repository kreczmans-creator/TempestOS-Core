namespace Tempest.Core.Identity;

/// <summary>
/// Resolves the <see cref="IPrincipal"/> performing the current operation.
/// </summary>
/// <remarks>
/// DI-public, consumed exactly like <see cref="Logging.ILogger"/> or
/// <see cref="Events.IEventBus"/> by any service or module needing to know
/// who is acting. Read-only by design — establishing a principal as
/// current is <see cref="IIdentityService"/>'s own responsibility, not
/// something an arbitrary consumer of this interface can do.
/// </remarks>
public interface ICurrentPrincipalAccessor
{
    /// <summary>
    /// Gets the current principal, or <see langword="null"/> if no
    /// principal has been established for the current operation. This is
    /// a normal, honestly-reported state — not every caller is expected to
    /// have a principal established at every point in time (for example,
    /// before <see cref="IIdentityService.EstablishCurrentPrincipal"/> is
    /// first called) — mirroring <c>ITempestHost.Services</c>'s own
    /// null-before-ready convention (ADR-0034).
    /// </summary>
    IPrincipal? Current { get; }
}
