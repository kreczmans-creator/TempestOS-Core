namespace Tempest.Core.Identity;

/// <summary>
/// Thrown when a principal is configured to hold a role name that
/// <see cref="IRoleProvider"/> has no definition for.
/// </summary>
/// <remarks>
/// A configuration-validity failure, not a runtime authorization failure —
/// it means the platform's own <c>Identity:Principals:*:Roles</c>
/// configuration references a role that no
/// <c>Identity:Roles:*:Permissions</c> entry defines, a genuine
/// configuration defect an operator must fix. This is deliberately
/// distinct from <see cref="PermissionDeniedException"/>, which reports a
/// correctly-configured principal simply lacking a permission.
/// </remarks>
public sealed class RoleNotFoundException : IdentityException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="RoleNotFoundException"/> class.
    /// </summary>
    /// <param name="roleName">The role name that has no matching definition.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="roleName"/> is <see langword="null"/>, empty, or
    /// whitespace.
    /// </exception>
    public RoleNotFoundException(string roleName)
        : base(BuildMessage(roleName))
    {
        RoleName = roleName;
    }

    private static string BuildMessage(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("Role name must not be null, empty, or whitespace.", nameof(roleName));

        return $"No role is defined with name '{roleName}'.";
    }

    /// <summary>Gets the role name that has no matching definition.</summary>
    public string RoleName { get; }
}
