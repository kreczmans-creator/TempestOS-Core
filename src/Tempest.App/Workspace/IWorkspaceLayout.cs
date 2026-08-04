namespace Tempest.App.Workspace;

/// <summary>
/// The docking arrangement — panel positions, sizes, and visibility.
/// Distinct from <see cref="IWorkspaceState"/>: this is the structural
/// arrangement alone; <see cref="IWorkspaceState"/> is the complete session
/// snapshot (layout, open tabs, last selection) that gets persisted.
/// </summary>
public interface IWorkspaceLayout
{
    /// <summary>Gets every panel's own current placement.</summary>
    IReadOnlyList<WorkspacePanelPlacement> PanelPlacements { get; }

    /// <summary>Gets <paramref name="panelId"/>'s own current placement.</summary>
    /// <exception cref="ArgumentException"><paramref name="panelId"/> is not a known panel.</exception>
    WorkspacePanelPlacement GetPlacement(Guid panelId);

    /// <summary>Sets <paramref name="panelId"/>'s own placement.</summary>
    /// <exception cref="ArgumentException"><paramref name="placement"/>'s own <c>PanelId</c> does not match <paramref name="panelId"/>, or does not identify a known panel.</exception>
    void SetPlacement(Guid panelId, WorkspacePanelPlacement placement);

    /// <summary>Returns a new layout matching this Workspace's own documented default arrangement (`WP8.0A UI Architecture.md` §1).</summary>
    IWorkspaceLayout ResetToDefault();
}
