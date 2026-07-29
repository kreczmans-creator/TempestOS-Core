namespace Tempest.Core.Identity;

/// <summary>
/// The acting party for a given operation — an <see cref="IIdentity"/>
/// plus the flattened set of permissions currently granted to it.
/// </summary>
/// <remarks>
/// <see cref="Permissions"/> is already flattened from whatever roles the
/// identity holds at the point the principal was resolved (see
/// <see cref="IIdentityService"/>) — a consumer never needs to expand a
/// role itself to know what a principal may do.
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
