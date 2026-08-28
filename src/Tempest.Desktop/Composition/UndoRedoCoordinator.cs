using Avalonia.Controls;
using Tempest.App.Workspace;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Owns the Undo/Redo Quick Access Toolbar experience (`WP 10.6A`,
/// `ADR-0099`) — the <see cref="IUndoRedoStack"/> itself, its two
/// buttons, and <see cref="UndoAsync"/>/<see cref="RedoAsync"/> —
/// extracted, `WP 12.0B` (`ADR-0103`), from <see cref="MainWindow"/>'s
/// own previous <c>_undoRedoStack</c>/<c>_undoButton</c>/<c>_redoButton</c>/
/// <c>UndoAsync</c>/<c>RedoAsync</c>/<c>RefreshUndoRedoButtons</c> members,
/// unmodified in behaviour. A collaborator under `ADR-0103`: constructed
/// once by <see cref="MainWindow"/> (the composition root), declaring
/// only the dependencies it actually needs, never DI-registered, never
/// referencing <see cref="MainWindow"/> or any sibling collaborator back.
/// </summary>
/// <remarks>
/// <see cref="Stack"/> is exposed publicly so every other collaborator
/// that records an undoable action (`WP 12.0B`'s own
/// <c>WorkspaceViewCoordinator</c>/<c>RibbonObjectActionHandlers</c>, and
/// <see cref="MainWindow"/>'s own retained <c>ToggleFavourite</c>) can
/// call <see cref="IUndoRedoStack.Record"/> directly — never a reference
/// to this collaborator itself (`ADR-0103`'s own "a collaborator never
/// depends on a sibling collaborator directly" rule). Button
/// enablement/tooltip refresh is fully reactive, subscribed once here to
/// <see cref="IUndoRedoStack.Changed"/> (raised by the concrete
/// <see cref="UndoRedoStack"/> after every <c>Record</c>/<c>UndoAsync</c>/
/// <c>RedoAsync</c>) — no caller anywhere needs to remember to refresh
/// these buttons itself, replacing the pre-decomposition source's own
/// several separate, explicit <c>RefreshUndoRedoButtons()</c> call sites
/// with one, identical in effect.
/// </remarks>
/// <remarks>
/// **`WP 12.4B` (`ADR-0104`).** Previously depended on a two-phase-
/// constructed <c>CockpitView</c> object reference (a nullable field
/// assigned post-construction via a now-removed <c>AttachCockpitView</c>
/// method) purely to call its own <c>Refresh()</c> — WP12.0B's own
/// architecture review, Finding 5, flagged this as heavier than the
/// actual need warranted, since nothing else about <see cref="CockpitView"/>
/// was ever used. Replaced with a plain <c>Action refreshCockpit</c>
/// constructor parameter — `ADR-0104`'s own "direct delegate over object
/// reference" default — supplied by <see cref="MainWindow"/> (the
/// composition root) via the same field-closure lazy-capture pattern
/// already established there for <c>_documentArea</c>. This removes a
/// genuine (if minor) construction-order coupling: this collaborator no
/// longer needs to know <see cref="CockpitView"/> exists at all.
/// </remarks>
internal sealed class UndoRedoCoordinator
{
    private readonly StatusBarView _statusBar;
    private readonly ToastHost _toastHost;
    private readonly ProjectExplorerView _explorerView;
    private readonly Action<string> _recordHistory;
    private readonly Action _refreshCockpit;

    /// <summary>Gets the session-only Undo/Redo stack (`ADR-0099`) — never persisted across a restart.</summary>
    public IUndoRedoStack Stack { get; } = new UndoRedoStack();

    /// <summary>Gets the Quick Access Toolbar's own Undo button.</summary>
    public Button UndoButton { get; } = new() { Content = "↶ Undo", MinHeight = DesignTokens.MinControlSize };

    /// <summary>Gets the Quick Access Toolbar's own Redo button.</summary>
    public Button RedoButton { get; } = new() { Content = "↷ Redo", MinHeight = DesignTokens.MinControlSize };

    /// <summary>Initialises a new instance of the <see cref="UndoRedoCoordinator"/> class.</summary>
    public UndoRedoCoordinator(StatusBarView statusBar, ToastHost toastHost, ProjectExplorerView explorerView, Action<string> recordHistory, Action refreshCockpit)
    {
        ArgumentNullException.ThrowIfNull(statusBar);
        ArgumentNullException.ThrowIfNull(toastHost);
        ArgumentNullException.ThrowIfNull(explorerView);
        ArgumentNullException.ThrowIfNull(recordHistory);
        ArgumentNullException.ThrowIfNull(refreshCockpit);

        _statusBar = statusBar;
        _toastHost = toastHost;
        _explorerView = explorerView;
        _recordHistory = recordHistory;
        _refreshCockpit = refreshCockpit;

        ToolTip.SetTip(UndoButton, "Nothing to undo");
        ToolTip.SetTip(RedoButton, "Nothing to redo");
        UndoButton.IsEnabled = false;
        RedoButton.IsEnabled = false;
        UndoButton.Click += (_, _) => _ = UndoAsync();
        RedoButton.Click += (_, _) => _ = RedoAsync();

        Stack.Changed += RefreshButtons;
    }

    /// <summary>Refreshes <see cref="UndoButton"/>/<see cref="RedoButton"/>'s own enablement/tooltip from <see cref="Stack"/>'s own real, current state.</summary>
    private void RefreshButtons()
    {
        UndoButton.IsEnabled = Stack.CanUndo;
        RedoButton.IsEnabled = Stack.CanRedo;
        ToolTip.SetTip(UndoButton, Stack.NextUndoDescription is { } undo ? $"Undo: {undo}" : "Nothing to undo");
        ToolTip.SetTip(RedoButton, Stack.NextRedoDescription is { } redo ? $"Redo: {redo}" : "Nothing to redo");
    }

    /// <summary>Reverses the most recently recorded action, if any (`WP 10.6A`, `ADR-0099`) — real feedback on both the Status Bar and as a Toast, exactly like every other completed action in this window.</summary>
    public async Task UndoAsync()
    {
        var result = await Stack.UndoAsync().ConfigureAwait(true);
        if (result is null)
            return;

        var message = result.Succeeded ? "Undo completed." : result.Message ?? "Undo failed.";
        _statusBar.SetText(message);
        _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
        _recordHistory(message);

        // Success-gated (`TD-58`): a failed undo/redo changed nothing.
        if (result.Succeeded)
        {
            await _explorerView.LoadAsync().ConfigureAwait(true);
            _refreshCockpit();
        }
    }

    /// <summary>Re-applies the most recently undone action, if any (`WP 10.6A`, `ADR-0099`) — mirrors <see cref="UndoAsync"/>'s own identical shape.</summary>
    public async Task RedoAsync()
    {
        var result = await Stack.RedoAsync().ConfigureAwait(true);
        if (result is null)
            return;

        var message = result.Succeeded ? "Redo completed." : result.Message ?? "Redo failed.";
        _statusBar.SetText(message);
        _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
        _recordHistory(message);

        // Success-gated (`TD-58`): a failed undo/redo changed nothing.
        if (result.Succeeded)
        {
            await _explorerView.LoadAsync().ConfigureAwait(true);
            _refreshCockpit();
        }
    }
}
