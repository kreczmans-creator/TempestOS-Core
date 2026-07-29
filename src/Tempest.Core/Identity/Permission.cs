namespace Tempest.Core.Identity;

/// <summary>
/// A single, named, granular permission.
/// </summary>
/// <remarks>
/// Immutable, value-equal record — two <see cref="Permission"/> values with
/// the same <see cref="Key"/> are the same permission, regardless of which
/// service defined it or which instance created it. Expected to be
/// extended additively — any service may define new permission keys
/// without changing this type's own shape.
/// </remarks>
public sealed record Permission
{
    /// <summary>
    /// Initialises a new instance of the <see cref="Permission"/> record.
    /// </summary>
    /// <param name="key">
    /// The permission's stable key (for example, <c>"reports.generate"</c>).
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="key"/> is <see langword="null"/>, empty, or
    /// whitespace.
    /// </exception>
    public Permission(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Permission key must not be null, empty, or whitespace.", nameof(key));

        Key = key;
    }

    /// <summary>Gets the permission's stable key.</summary>
    public string Key { get; }
}
