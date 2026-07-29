namespace Tempest.Core.Identity;

/// <summary>
/// Resolves the platform's own defined roles.
/// </summary>
public interface IRoleProvider
{
    /// <summary>
    /// Finds the role named <paramref name="name"/>, or <see langword="null"/>
    /// if no such role is defined.
    /// </summary>
    /// <param name="name">The role name to look up.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is <see langword="null"/>, empty, or
    /// whitespace.
    /// </exception>
    IRole? FindRole(string name);

    /// <summary>
    /// Gets every currently defined role. Never <see langword="null"/>;
    /// empty if none are defined.
    /// </summary>
    IReadOnlyList<IRole> Roles { get; }
}
