using Tempest.Core.Identity;

namespace Tempest.Samples;

/// <summary>
/// Establishes a sample module's own demonstration principal directly on
/// <see cref="CurrentPrincipalAccessor"/> (`WP 17.2A`, ADR-0146).
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
/// the access a real signed-in session has.
/// </remarks>
internal static class SamplePrincipalFactory
{
    /// <summary>
    /// Builds a <see cref="PlatformPrincipal"/> for <paramref name="identityId"/>
    /// and establishes it as current on <paramref name="accessor"/>.
    /// </summary>
    /// <param name="accessor">The accessor to establish the principal on.</param>
    /// <param name="identityId">The sample identity id to establish (also used, unadorned, as its display name).</param>
    /// <returns>The established principal.</returns>
    public static IPrincipal Establish(CurrentPrincipalAccessor accessor, string identityId)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentException.ThrowIfNullOrWhiteSpace(identityId);

        var principal = new PlatformPrincipal(new PlatformIdentity(identityId, identityId), ApplicationPermissions.LocalSession);
        accessor.SetCurrent(principal);

        return principal;
    }
}
