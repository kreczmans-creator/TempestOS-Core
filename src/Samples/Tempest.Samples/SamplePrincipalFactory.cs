using Tempest.Core.Identity;

namespace Tempest.Samples;

/// <summary>
/// Establishes a sample module's own demonstration principal through
/// <see cref="PrincipalSession"/> (`WP 17.2A`, ADR-0146; narrowed to this
/// one seam by `WP 21.6A`, OSA-12).
/// </summary>
/// <remarks>
/// Samples used to reach this through <c>IIdentityService</c>, deleted
/// alongside <c>IRoleProvider</c>/<c>RoleProvider</c> once nothing outside
/// Samples still called it — the shipped product establishes its own
/// principal through <c>Tempest.Desktop</c>'s <c>WorkspaceHost</c> and its
/// <see cref="SessionPrincipalSource"/>, never through a Host-registered
/// identity service. Every sample module's own principal carries the same
/// flat <see cref="ApplicationPermissions.LocalSession"/> set a real
/// session principal does, so a sample continues to demonstrate exactly
/// the access a real signed-in session has. Constructor-injecting
/// <see cref="PrincipalSession"/> — never the concrete
/// <see cref="CurrentPrincipalAccessor"/>, whose own <c>SetCurrent</c> is
/// internal to <c>Tempest.Core</c> regardless — is this namespace's own
/// disclosed, first-party demonstration of the identical, narrower seam
/// the shipped product's two legitimate callers use; <c>Tempest.Samples</c>
/// is never shipped (`WP 21.5F` confirmed this directly), so this is not a
/// live reach for an untrusted actor, the same "no live untrusted actor"
/// disposition OSA-12 was originally deferred under.
/// </remarks>
internal static class SamplePrincipalFactory
{
    /// <summary>
    /// Builds a <see cref="PlatformPrincipal"/> for <paramref name="identityId"/>
    /// and establishes it as current through <paramref name="session"/>.
    /// </summary>
    /// <param name="session">The seam to establish the principal through.</param>
    /// <param name="identityId">The sample identity id to establish (also used, unadorned, as its display name).</param>
    /// <returns>The established principal.</returns>
    public static IPrincipal Establish(PrincipalSession session, string identityId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(identityId);

        var principal = new PlatformPrincipal(new PlatformIdentity(identityId, identityId), ApplicationPermissions.LocalSession);
        session.Establish(principal);

        return principal;
    }
}
