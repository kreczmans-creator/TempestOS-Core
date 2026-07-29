using Tempest.Core.Logging;

namespace Tempest.Core.Identity;

/// <summary>
/// The concrete <see cref="IPermissionEvaluator"/> implementation.
/// </summary>
/// <remarks>
/// Checks directly against <see cref="IPrincipal.Permissions"/> —
/// permission resolution (flattening a principal's roles into that list)
/// has already happened once, by <see cref="IIdentityService"/>, at the
/// point the principal was resolved, so this evaluator never itself
/// consults <see cref="IRoleProvider"/>. A denied check is logged at
/// <see cref="LogLevel.Warning"/> with the principal Id and permission
/// key only — never a credential or any other sensitive detail
/// (<c>Platform Service Contracts.md</c>'s own Logging Requirements).
/// </remarks>
public sealed class PermissionEvaluator : IPermissionEvaluator
{
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="PermissionEvaluator"/> class.
    /// </summary>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public PermissionEvaluator(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool HasPermission(IPrincipal principal, Permission permission)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(permission);

        return principal.Permissions.Contains(permission);
    }

    /// <inheritdoc />
    public void RequirePermission(IPrincipal principal, Permission permission)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(permission);

        if (HasPermission(principal, permission))
            return;

        _logger?.Warning(
            $"Permission denied: principal '{principal.Identity.Id}' does not hold '{permission.Key}'.",
            properties: new Dictionary<string, object?>
            {
                ["PrincipalId"] = principal.Identity.Id,
                ["PermissionKey"] = permission.Key,
            });

        throw new PermissionDeniedException(principal, permission);
    }
}
