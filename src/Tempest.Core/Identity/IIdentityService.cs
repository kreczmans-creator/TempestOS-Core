namespace Tempest.Core.Identity;

/// <summary>
/// Resolves an <see cref="IPrincipal"/> for a given identity, and
/// establishes the current principal for the calling async flow.
/// </summary>
/// <remarks>
/// Not part of the original architecture's <c>Public Interface
/// Catalogue.md</c> draft — an additive elaboration this Work Package's
/// own implementation phase introduces to satisfy the Identity service
/// deliverable, consistent with <c>Platform Service Contracts.md</c>'s own
/// note that the principal-population mechanism was left for this Work
/// Package to define. This release's identity model is local-only
/// (ADR-0043) — there is no authentication (verifying a password, token,
/// or credential); a caller supplies an identity id it already trusts
/// (for example, an operating-system user name, or a fixed local
/// identity), and this service resolves what that identity is permitted
/// to do.
/// </remarks>
public interface IIdentityService
{
    /// <summary>
    /// Resolves the principal for <paramref name="identityId"/>.
    /// </summary>
    /// <param name="identityId">The identity to resolve.</param>
    /// <returns>
    /// The resolved principal. An identity id with no matching
    /// <c>Identity:Principals:*</c> configuration entry resolves to a
    /// principal with zero permissions — fail-closed, not an error — so
    /// that an unrecognised identity is safely inert rather than
    /// exceptional.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="identityId"/> is <see langword="null"/>, empty, or
    /// whitespace.
    /// </exception>
    /// <exception cref="RoleNotFoundException">
    /// <paramref name="identityId"/> is configured to hold a role that
    /// <see cref="IRoleProvider"/> has no definition for.
    /// </exception>
    IPrincipal GetPrincipal(string identityId);

    /// <summary>
    /// Resolves the principal for <paramref name="identityId"/> (see
    /// <see cref="GetPrincipal"/>) and establishes it as current for the
    /// calling async flow.
    /// </summary>
    /// <param name="identityId">The identity to resolve and establish.</param>
    /// <returns>The established principal.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="identityId"/> is <see langword="null"/>, empty, or
    /// whitespace.
    /// </exception>
    /// <exception cref="RoleNotFoundException">
    /// <paramref name="identityId"/> is configured to hold a role that
    /// <see cref="IRoleProvider"/> has no definition for.
    /// </exception>
    IPrincipal EstablishCurrentPrincipal(string identityId);
}
