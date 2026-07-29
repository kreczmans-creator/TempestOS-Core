namespace Tempest.Core.Navigation;

/// <summary>
/// Describes a single navigable destination — identity, display title, and
/// its place within an optional hierarchy and ordering. Carries no
/// rendering concern of any kind.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, caller-constructed data, mirroring <see cref="Modules.ModuleDescriptor"/>
/// and <see cref="Plugins.PluginManifest"/>'s own shape: the platform holds a
/// registry of things it did not create the meaning of. <see cref="Icon"/> is a
/// symbolic key only — never a rendered image, a font glyph, or a reference to
/// any UI framework resource. Resolving what, if anything, an <see cref="Id"/>
/// or <see cref="Icon"/> actually looks like on screen is entirely
/// <c>Tempest.App</c>'s (or any future UI shell's) own responsibility — see
/// ADR-0031 and <c>Navigation Framework Architecture.md</c>.
/// </para>
/// </remarks>
public sealed class NavigationItem
{
    /// <summary>
    /// Initialises a new instance of the <see cref="NavigationItem"/> class.
    /// </summary>
    /// <param name="id">The item's unique, caller-assigned identifier.</param>
    /// <param name="title">The item's display label.</param>
    /// <param name="order">
    /// Explicit ordering within its <paramref name="group"/>/parent. Ties are
    /// broken ascending ordinal by <paramref name="id"/>.
    /// </param>
    /// <param name="icon">
    /// An optional, symbolic icon key. <see langword="null"/> means no icon.
    /// </param>
    /// <param name="group">
    /// An optional grouping label. <see langword="null"/> means ungrouped.
    /// </param>
    /// <param name="parentId">
    /// An optional reference to another registered item's <see cref="Id"/>,
    /// establishing hierarchy. <see langword="null"/> means top-level.
    /// </param>
    /// <param name="isVisible">
    /// An optional predicate, evaluated by the caller at query time.
    /// <see langword="null"/> means always visible.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> or <paramref name="title"/> is <see langword="null"/>,
    /// empty, or whitespace.
    /// </exception>
    public NavigationItem(
        string id,
        string title,
        int order = 0,
        string? icon = null,
        string? group = null,
        string? parentId = null,
        Func<bool>? isVisible = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id must not be null, empty, or whitespace.", nameof(id));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title must not be null, empty, or whitespace.", nameof(title));

        Id = id;
        Title = title;
        Order = order;
        Icon = icon;
        Group = group;
        ParentId = parentId;
        IsVisible = isVisible;
    }

    /// <summary>
    /// Gets the item's unique, caller-assigned identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the item's display label.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the explicit ordering of this item within its <see cref="Group"/>/parent.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Gets an optional, symbolic icon key, or <see langword="null"/> if this
    /// item has no icon.
    /// </summary>
    public string? Icon { get; }

    /// <summary>
    /// Gets an optional grouping label, or <see langword="null"/> if this item
    /// is ungrouped.
    /// </summary>
    public string? Group { get; }

    /// <summary>
    /// Gets an optional reference to another registered item's <see cref="Id"/>,
    /// or <see langword="null"/> if this item is top-level.
    /// </summary>
    public string? ParentId { get; }

    /// <summary>
    /// Gets an optional visibility predicate, evaluated by the caller at query
    /// time, or <see langword="null"/> if this item is always visible.
    /// </summary>
    public Func<bool>? IsVisible { get; }
}
