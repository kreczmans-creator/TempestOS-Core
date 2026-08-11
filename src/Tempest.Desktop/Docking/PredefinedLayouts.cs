using Tempest.App.Workspace;

namespace Tempest.Desktop.Docking;

/// <summary>
/// Three named, fixed panel arrangements — Engineering, Review,
/// Documentation (`WP 10.2B`'s own named scope item) — each just a
/// different combination of already-existing, already-contracted values
/// (<see cref="WorkspacePanelPlacement"/>'s own Dock Position/Size/
/// Visibility, plus this Work Package's own Desktop-local Output panel
/// state). Applying one calls nothing but
/// <see cref="IWorkspaceLayout.SetPlacement"/> — the identical, already-
/// frozen `WP8.0B` member every ordinary resize/hide already calls
/// (`MainWindow`'s own resize/hide handlers) — so no new Workspace
/// contract surface exists to support "predefined layouts" at all.
/// </summary>
public static class PredefinedLayouts
{
    /// <summary>One of the three named, fixed panel arrangements this Work Package defines.</summary>
    public enum WorkspaceLayoutPreset
    {
        /// <summary>The day-to-day working arrangement — Explorer and Inspector both visible at their own ordinary widths, Output hidden. Matches <see cref="IWorkspaceLayout.ResetToDefault"/>'s own arrangement closely, but is a distinct, named preset in its own right, not merely an alias for it.</summary>
        Engineering,

        /// <summary>Optimised for reviewing existing work — a narrower Explorer, a wider Inspector (more facet detail visible at once), and the Output panel shown (module/host diagnostics visible while reviewing).</summary>
        Review,

        /// <summary>Optimised for browsing/authoring documentation — a wide Explorer (more tree visible at once), the Inspector Auto-Hidden (reclaims width for the Document Area, still one click away), Output hidden.</summary>
        Documentation,
    }

    /// <summary>The Project Explorer's own placement under <paramref name="preset"/>.</summary>
    public static WorkspacePanelPlacement ExplorerPlacement(WorkspaceLayoutPreset preset, Guid explorerId) => preset switch
    {
        WorkspaceLayoutPreset.Engineering => new WorkspacePanelPlacement(explorerId, WorkspaceDockPosition.Left, 240, true),
        WorkspaceLayoutPreset.Review => new WorkspacePanelPlacement(explorerId, WorkspaceDockPosition.Left, 180, true),
        WorkspaceLayoutPreset.Documentation => new WorkspacePanelPlacement(explorerId, WorkspaceDockPosition.Left, 320, true),
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };

    /// <summary>The Property Inspector's own placement under <paramref name="preset"/>.</summary>
    public static WorkspacePanelPlacement InspectorPlacement(WorkspaceLayoutPreset preset, Guid inspectorId) => preset switch
    {
        WorkspaceLayoutPreset.Engineering => new WorkspacePanelPlacement(inspectorId, WorkspaceDockPosition.Right, 240, true),
        WorkspaceLayoutPreset.Review => new WorkspacePanelPlacement(inspectorId, WorkspaceDockPosition.Right, 320, true),
        WorkspaceLayoutPreset.Documentation => new WorkspacePanelPlacement(inspectorId, WorkspaceDockPosition.Right, 240, true),
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };

    /// <summary>Whether the Property Inspector is pinned (docked) or Auto-Hidden under <paramref name="preset"/> — a Desktop-local concern, so not part of <see cref="InspectorPlacement"/>'s own <see cref="WorkspacePanelPlacement"/>.</summary>
    public static bool InspectorPinned(WorkspaceLayoutPreset preset) => preset != WorkspaceLayoutPreset.Documentation;

    /// <summary>The Output panel's own Desktop-local placement under <paramref name="preset"/>.</summary>
    public static OutputPlacement OutputPanelPlacement(WorkspaceLayoutPreset preset) => preset switch
    {
        WorkspaceLayoutPreset.Engineering => new OutputPlacement(Visible: false, Height: 160),
        WorkspaceLayoutPreset.Review => new OutputPlacement(Visible: true, Height: 180),
        WorkspaceLayoutPreset.Documentation => new OutputPlacement(Visible: false, Height: 160),
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };

    /// <summary>The Output panel's own placement — its own Desktop-local equivalent of <see cref="WorkspacePanelPlacement"/>, since the Output panel is not a Workspace-contract <see cref="Tempest.App.Workspace.IWorkspacePanel"/> registered anywhere <see cref="IWorkspaceLayout"/> itself tracks.</summary>
    public readonly record struct OutputPlacement(bool Visible, double Height);
}
