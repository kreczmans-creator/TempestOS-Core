namespace Tempest.Core.Identity;

/// <summary>
/// A named collection of permissions, assignable to a principal as a
/// single unit.
/// </summary>
/// <remarks>
/// A role is purely a grouping convenience over <see cref="Permission"/> —
/// it carries no behaviour of its own, and a principal's own
/// <see cref="IPrincipal.Permissions"/> is always the flattened union of
/// every role it holds, resolved once by <see cref="IIdentityService"/>.
/// Not part of the original architecture's <c>Public Interface
/// Catalogue.md</c> draft (which named only <see cref="IIdentity"/>,
/// <see cref="IPrincipal"/>, <see cref="ICurrentPrincipalAccessor"/>,
/// <see cref="IPermissionEvaluator"/>, and <see cref="Permission"/>) — an
/// additive elaboration this Work Package's own implementation phase
/// introduces to satisfy the Role model deliverable, consistent with
/// <c>Platform Service Contracts.md</c>'s own note that the principal-
/// population mechanism was left for this Work Package to define.
/// </remarks>
public interface IRole
{
    /// <summary>Gets the role's stable, unique name.</summary>
    string Name { get; }

    /// <summary>
    /// Gets every permission this role grants. Never <see langword="null"/>;
    /// empty if the role grants none.
    /// </summary>
    IReadOnlyList<Permission> Permissions { get; }
}
