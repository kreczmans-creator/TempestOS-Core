namespace Tempest.Workspace.Layout;

/// <summary>
/// One top-level window's own place in the arrangement — the entry
/// `ADR-0153` decision 1 generalises "a root plus a floating list" into: a
/// <see cref="WorkspaceLayoutTree"/> is an ordered set of these, not a tree
/// per window and not one privileged tree with a separate floating list.
/// </summary>
/// <remarks>
/// <para>
/// Exactly one entry in a tree's own <see cref="WorkspaceLayoutTree.Windows"/>
/// carries <see cref="IsPrimary"/> — the application's own main window,
/// where the rail, header and ribbon chrome live outside the tree entirely,
/// unchanged. It is the one entry allowed to hold a <see langword="null"/>
/// <see cref="Root"/> (every panel closed or moved elsewhere) without
/// ceasing to exist; a non-primary entry whose <see cref="Root"/> becomes
/// <see langword="null"/> is removed from the arrangement by
/// <see cref="WorkspaceLayoutTree.Normalised"/> — a window that held its
/// last panel is not an empty window, it is a window that should no longer
/// exist (the rule `WP 20.10D` established for a single floating list,
/// restated here for any number of secondary windows).
/// </para>
/// </remarks>
/// <param name="Id">This window's own identity in the layout model.</param>
/// <param name="Root">The docked arrangement inside this window, or <see langword="null"/> when it holds nothing.</param>
/// <param name="IsPrimary">Whether this is the shell's own main window.</param>
/// <param name="MonitorKey">
/// A best-effort stable identity for the monitor this window was last
/// placed against (`ADR-0153` decision 5) — a display's own name paired
/// with its bounds, since no platform this product ships on guarantees a
/// persistent hardware id. <see langword="null"/> for the primary window
/// (its own on-screen position is the operating system's session state,
/// not this model's) and for a secondary window never yet resolved against
/// a real screen.
/// </param>
/// <param name="X">
/// Left edge. Monitor-relative — measured from <see cref="MonitorKey"/>'s
/// own working-area origin — whenever <see cref="MonitorKey"/> is set;
/// absolute screen coordinates otherwise (a freshly floated window before
/// its first save, or a document migrated from the pre-`WP 21.0A` format,
/// where no monitor was ever recorded). Only <see cref="WorkspaceLayoutController"/>,
/// which alone has access to the real screens, interprets which convention
/// applies at a given moment; this record only ever carries the numbers it
/// is given.
/// </param>
/// <param name="Y">Top edge, on the same terms as <see cref="X"/>.</param>
/// <param name="Width">Window width, in device-independent pixels.</param>
/// <param name="Height">Window height, in device-independent pixels.</param>
public sealed record WorkspaceLayoutWindow(
    Guid Id,
    WorkspaceLayoutNode? Root,
    bool IsPrimary,
    string? MonitorKey,
    double X,
    double Y,
    double Width,
    double Height)
{
    /// <summary>Every panel this window holds.</summary>
    public IEnumerable<Guid> Panels => Root?.Panels ?? [];
}
