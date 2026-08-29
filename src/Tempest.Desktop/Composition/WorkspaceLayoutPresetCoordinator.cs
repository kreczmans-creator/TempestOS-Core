using Tempest.App.Workspace.Layout;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Applies and resets the named workspace arrangements (`TD-72`).
/// </summary>
/// <remarks>
/// <para>
/// A preset used to be a fixed combination of three panel placements
/// against three named docks, plus a handful of Desktop-local flags, set
/// one property at a time. It is now a whole layout tree, replaced in one
/// operation — so applying a preset cannot leave the arrangement half
/// converted, and a preset can express anything a user could build by
/// hand, including tabs and nested splits.
/// </para>
/// <para>
/// A collaborator under `ADR-0103`: constructed once by
/// <see cref="MainWindow"/>, declaring only what it needs, never
/// DI-registered, and never referencing a sibling collaborator back — it
/// receives the two operations it drives as delegates.
/// </para>
/// </remarks>
internal sealed class WorkspaceLayoutPresetCoordinator
{
    private readonly Action<WorkspaceLayoutPreset> _applyPreset;
    private readonly Action _resetLayout;
    private readonly StatusBarView _statusBar;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceLayoutPresetCoordinator"/> class.</summary>
    public WorkspaceLayoutPresetCoordinator(
        Action<WorkspaceLayoutPreset> applyPreset, Action resetLayout, StatusBarView statusBar)
    {
        ArgumentNullException.ThrowIfNull(applyPreset);
        ArgumentNullException.ThrowIfNull(resetLayout);
        ArgumentNullException.ThrowIfNull(statusBar);

        _applyPreset = applyPreset;
        _resetLayout = resetLayout;
        _statusBar = statusBar;
    }

    /// <summary>Applies <paramref name="preset"/>, replacing the whole arrangement.</summary>
    public void Apply(WorkspaceLayoutPreset preset)
    {
        _applyPreset(preset);
        _statusBar.SetHint($"{preset} layout applied.");
    }

    /// <summary>Returns to the default arrangement.</summary>
    public void Reset()
    {
        _resetLayout();
        _statusBar.SetHint("Layout reset to the default arrangement.");
    }
}
