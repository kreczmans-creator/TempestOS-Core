namespace Tempest.Core.Identity;

/// <summary>
/// Thrown by <see cref="IPermissionEvaluator.RequirePermission"/> when the
/// given principal does not hold the required permission.
/// </summary>
/// <remarks>
/// The single enforcement-point failure this release's authorization model
/// is built around (ADR-0044) — a caller that needs to fail loudly on a
/// denied check should use <see cref="IPermissionEvaluator.RequirePermission"/>;
/// a caller that needs to branch instead (for example, hiding a menu item)
/// should use the non-throwing <see cref="IPermissionEvaluator.HasPermission"/>.
/// </remarks>
public sealed class PermissionDeniedException : IdentityException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PermissionDeniedException"/> class.
    /// </summary>
    /// <param name="principal">The principal that was denied.</param>
    /// <param name="permission">The permission that was required.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="principal"/> or <paramref name="permission"/> is
    /// <see langword="null"/>.
    /// </exception>
    public PermissionDeniedException(IPrincipal principal, Permission permission)
        : base(BuildMessage(principal, permission))
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(permission);

        Principal = principal;
        RequiredPermission = permission;
    }

    private static string BuildMessage(IPrincipal principal, Permission permission)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(permission);

        return $"Principal '{principal.Identity.Id}' does not hold permission '{permission.Key}'.";
    }

    /// <summary>Gets the principal that was denied.</summary>
    public IPrincipal Principal { get; }

    /// <summary>Gets the permission that was required.</summary>
    public Permission RequiredPermission { get; }
}
