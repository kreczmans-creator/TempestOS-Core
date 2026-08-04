namespace Tempest.App.Workspace;

/// <summary>
/// One panel's own docking arrangement — position, size, and visibility.
/// Immutable, mirroring <see cref="Tempest.Core.Navigation.NavigationItem"/>/
/// <see cref="Tempest.Core.Commands.CommandDescriptor"/>'s own established
/// shape for this platform's own registry-pattern data.
/// </summary>
/// <param name="PanelId">The <see cref="IWorkspacePanel.Id"/> this placement describes.</param>
/// <param name="DockPosition">Where the panel is docked.</param>
/// <param name="Size">
/// The panel's own size, in whatever unit the eventual rendering
/// implementation interprets it as (for example, a column count in a
/// terminal) — deliberately unitless at the contract level.
/// </param>
/// <param name="IsVisible">Whether the panel is currently shown.</param>
public sealed record WorkspacePanelPlacement(
    Guid PanelId,
    WorkspaceDockPosition DockPosition,
    double Size,
    bool IsVisible);
