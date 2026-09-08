namespace Tempest.Core.Identity;

/// <summary>
/// The acting party for a given operation — an <see cref="IIdentity"/>
/// plus the flattened set of permissions currently granted to it.
/// </summary>
/// <remarks>
/// <see cref="Permissions"/> is already the flat, resolved set a consumer
/// checks directly — never a role to expand itself. For the platform's own
/// single session principal (<see cref="SessionPrincipalSource"/>) this is
/// the fixed <see cref="ApplicationPermissions.LocalSession"/> set; a
/// future, more elaborate <see cref="ISessionPrincipalSource"/> may resolve
/// it differently without this contract changing.
/// </remarks>
public interface IPrincipal
{
    /// <summary>Gets the identity this principal represents.</summary>
    IIdentity Identity { get; }

    /// <summary>
    /// Gets every permission currently granted to this principal. Never
    /// <see langword="null"/>; empty if none are granted.
    /// </summary>
    IReadOnlyList<Permission> Permissions { get; }
}
