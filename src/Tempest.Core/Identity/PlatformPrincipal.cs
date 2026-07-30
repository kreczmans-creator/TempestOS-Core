namespace Tempest.Core.Identity;

/// <summary>
/// The concrete, immutable <see cref="IPrincipal"/> implementation used by
/// <see cref="IIdentityService"/>.
/// </summary>
public sealed class PlatformPrincipal : IPrincipal
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PlatformPrincipal"/> class.
    /// </summary>
    /// <param name="identity">The identity this principal represents.</param>
    /// <param name="permissions">
    /// The flattened set of permissions granted to this principal.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identity"/> or <paramref name="permissions"/> is
    /// <see langword="null"/>.
    /// </exception>
    public PlatformPrincipal(IIdentity identity, IReadOnlyList<Permission> permissions)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(permissions);

        Identity = identity;
        Permissions = permissions;
    }

    /// <inheritdoc />
    public IIdentity Identity { get; }

    /// <inheritdoc />
    public IReadOnlyList<Permission> Permissions { get; }
}
