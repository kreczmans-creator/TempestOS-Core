namespace Tempest.Core.Identity;

/// <summary>
/// Answers whether a given principal holds a given permission — the single,
/// uniform authorization enforcement point every other service is expected
/// to call (ADR-0044).
/// </summary>
public interface IPermissionEvaluator
{
    /// <summary>
    /// Determines whether <paramref name="principal"/> holds
    /// <paramref name="permission"/>, without throwing.
    /// </summary>
    /// <param name="principal">The principal to check.</param>
    /// <param name="permission">The permission to check for.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="principal"/> or <paramref name="permission"/> is
    /// <see langword="null"/>.
    /// </exception>
    bool HasPermission(IPrincipal principal, Permission permission);

    /// <summary>
    /// Requires that <paramref name="principal"/> holds
    /// <paramref name="permission"/>, throwing if not.
    /// </summary>
    /// <param name="principal">The principal to check.</param>
    /// <param name="permission">The permission required.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="principal"/> or <paramref name="permission"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="PermissionDeniedException">
    /// <paramref name="principal"/> does not hold <paramref name="permission"/>.
    /// </exception>
    void RequirePermission(IPrincipal principal, Permission permission);
}
