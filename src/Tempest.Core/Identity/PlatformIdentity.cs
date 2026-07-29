namespace Tempest.Core.Identity;

/// <summary>
/// The concrete, immutable <see cref="IIdentity"/> implementation used by
/// <see cref="IIdentityService"/>.
/// </summary>
/// <remarks>
/// Named <c>PlatformIdentity</c>, not <c>Identity</c>, to avoid a
/// confusingly-named type sharing its simple name with the enclosing
/// <c>Tempest.Core.Identity</c> namespace.
/// </remarks>
public sealed class PlatformIdentity : IIdentity
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PlatformIdentity"/> class.
    /// </summary>
    /// <param name="id">The identity's stable, unique identifier.</param>
    /// <param name="displayName">The identity's human-readable display name.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> or <paramref name="displayName"/> is
    /// <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public PlatformIdentity(string id, string displayName)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Identity id must not be null, empty, or whitespace.", nameof(id));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name must not be null, empty, or whitespace.", nameof(displayName));

        Id = id;
        DisplayName = displayName;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string DisplayName { get; }
}
