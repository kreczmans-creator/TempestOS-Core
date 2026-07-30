namespace Tempest.Core.Identity;

/// <summary>
/// The concrete, immutable <see cref="IRole"/> implementation.
/// </summary>
public sealed class Role : IRole
{
    /// <summary>
    /// Initialises a new instance of the <see cref="Role"/> class.
    /// </summary>
    /// <param name="name">The role's stable, unique name.</param>
    /// <param name="permissions">Every permission this role grants.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is <see langword="null"/>, empty, or
    /// whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="permissions"/> is <see langword="null"/>.
    /// </exception>
    public Role(string name, IReadOnlyList<Permission> permissions)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name must not be null, empty, or whitespace.", nameof(name));

        ArgumentNullException.ThrowIfNull(permissions);

        Name = name;
        Permissions = permissions;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IReadOnlyList<Permission> Permissions { get; }
}
