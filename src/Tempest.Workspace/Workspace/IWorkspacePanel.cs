namespace Tempest.App.Workspace;

/// <summary>
/// A dockable container — the Project Explorer and Property Inspector both
/// implement this; the always-present Document Area does not, since it is
/// never hideable/dockable away (`WP8.0A UI Architecture.md` §2).
/// </summary>
public interface IWorkspacePanel
{
    /// <summary>Gets this panel's own unique identifier.</summary>
    Guid Id { get; }

    /// <summary>Gets this panel's own display title.</summary>
    string Title { get; }

    /// <summary>Gets where this panel is currently docked.</summary>
    WorkspaceDockPosition DockPosition { get; }

    /// <summary>Gets a value indicating whether this panel is currently shown.</summary>
    bool IsVisible { get; }

    /// <summary>Shows this panel, preserving whatever internal state it holds.</summary>
    Task ShowAsync(CancellationToken cancellationToken = default);

    /// <summary>Hides this panel, preserving whatever internal state it holds.</summary>
    Task HideAsync(CancellationToken cancellationToken = default);
}
