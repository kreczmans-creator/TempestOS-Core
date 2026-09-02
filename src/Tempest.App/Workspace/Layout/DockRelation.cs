namespace Tempest.App.Workspace.Layout;

/// <summary>
/// Where a dragged panel lands relative to the target it is dropped on
/// (`TD-72`) — the five drop zones a docking overlay offers.
/// </summary>
public enum DockRelation
{
    /// <summary>Into the target's own tab group, as another tab.</summary>
    Into,

    /// <summary>Into a new split to the left of the target.</summary>
    Left,

    /// <summary>Into a new split to the right of the target.</summary>
    Right,

    /// <summary>Into a new split above the target.</summary>
    Above,

    /// <summary>Into a new split below the target.</summary>
    Below,
}

/// <summary>How a panel behaves when it is not simply docked and visible.</summary>
/// <param name="IsPinned">
/// <see langword="false"/> means Auto-Hide: the panel keeps its place in
/// the layout but renders as an edge strip, expanding as a flyout on
/// demand.
/// </param>
/// <param name="IsCollapsed">Whether the panel is collapsed to its own strip in place.</param>
public sealed record PanelPresentation(bool IsPinned = true, bool IsCollapsed = false)
{
    /// <summary>An ordinary docked, pinned, expanded panel.</summary>
    public static readonly PanelPresentation Default = new();
}

/// <summary>
/// A panel, or a subtree of panels, living in its own top-level window
/// (`TD-72`).
/// </summary>
/// <remarks>
/// Geometry here is in screen coordinates, not window-relative, which is
/// what makes multi-monitor work: a floating panel dragged onto a second
/// display is restored onto that display, and a layout is not silently
/// re-anchored to the main window's origin.
/// </remarks>
/// <param name="Id">This floating window's own identity.</param>
/// <param name="Content">The subtree it hosts — a tab group, or a whole split of its own.</param>
/// <param name="X">Screen X, in device-independent pixels.</param>
/// <param name="Y">Screen Y, in device-independent pixels.</param>
/// <param name="Width">Window width, in device-independent pixels.</param>
/// <param name="Height">Window height, in device-independent pixels.</param>
public sealed record FloatingLayoutWindow(
    Guid Id,
    WorkspaceLayoutNode Content,
    double X,
    double Y,
    double Width,
    double Height);
