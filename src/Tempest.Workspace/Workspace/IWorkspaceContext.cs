namespace Tempest.App.Workspace;

/// <summary>
/// Ambient, read-only, DI-resolvable state — mirrors
/// <see cref="Tempest.Core.Identity.ICurrentPrincipalAccessor"/>'s own
/// precedent (`ADR-0044`) exactly: a component needing to know what is
/// currently selected should not need a constructor dependency on the whole
/// <see cref="ISelectionService"/> just to read one ambient fact. Never a
/// service locator — see `WP8.0B Dependency Rules.md` §5.
/// </summary>
public interface IWorkspaceContext
{
    /// <summary>Gets the Workspace's own current selection, or <see langword="null"/>.</summary>
    WorkspaceSelection? CurrentSelection { get; }

    /// <summary>
    /// Gets every currently selected item, in selection order. Empty if
    /// nothing is selected; mirrors <see cref="CurrentSelection"/> alone
    /// whenever only <see cref="ISelectionService.SelectAsync"/>/
    /// <see cref="ISelectionService.ClearAsync"/> have ever been used —
    /// grows independently only once
    /// <see cref="ISelectionService.ToggleSelectionAsync"/> is used
    /// (`WP 9.1A`, `ADR-0085`).
    /// </summary>
    IReadOnlyList<WorkspaceSelection> SelectedItems { get; }

    /// <summary>Gets the currently active (focused) view's own Id, or <see langword="null"/> if none is open.</summary>
    Guid? ActiveViewId { get; }
}
