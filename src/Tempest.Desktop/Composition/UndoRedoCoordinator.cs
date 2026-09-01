using Avalonia.Controls;
using Avalonia.Threading;
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
    private readonly ProjectExplorerView _explorerView;
    private readonly Action _refreshCockpit;
    private readonly ActionOutcomeReporter _reporter;

    /// <summary>Gets the session-only Undo/Redo stack (`ADR-0099`) — never persisted across a restart.</summary>
    public IUndoRedoStack Stack { get; } = new UndoRedoStack();

    /// <summary>Gets the Quick Access Toolbar's own Undo button.</summary>
    public Button UndoButton { get; } = new() { Content = "↶ Undo", MinHeight = DesignTokens.MinControlSize };

    /// <summary>Gets the Quick Access Toolbar's own Redo button.</summary>
    public Button RedoButton { get; } = new() { Content = "↷ Redo", MinHeight = DesignTokens.MinControlSize };

    /// <summary>Initialises a new instance of the <see cref="UndoRedoCoordinator"/> class.</summary>
    public UndoRedoCoordinator(ProjectExplorerView explorerView, Action refreshCockpit, ActionOutcomeReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(explorerView);
        ArgumentNullException.ThrowIfNull(refreshCockpit);
        ArgumentNullException.ThrowIfNull(reporter);

        _explorerView = explorerView;
        _refreshCockpit = refreshCockpit;
        _reporter = reporter;

        ToolTip.SetTip(UndoButton, "Nothing to undo");
        ToolTip.SetTip(RedoButton, "Nothing to redo");
        UndoButton.IsEnabled = false;
        RedoButton.IsEnabled = false;
        UndoButton.Click += (_, _) => _ = UndoAsync();
        RedoButton.Click += (_, _) => _ = RedoAsync();

        Stack.Changed += RefreshButtons;
    }

    /// <summary>
    /// Refreshes <see cref="UndoButton"/>/<see cref="RedoButton"/>'s own
    /// enablement/tooltip from <see cref="Stack"/>'s own real, current
    /// state — on the UI thread, whichever thread raised
    /// <see cref="IUndoRedoStack.Changed"/> (`TD-117`, `ADR-0119`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this marshals.</b> <see cref="UndoRedoStack.UndoAsync"/> awaits
    /// the undone action with <c>ConfigureAwait(false)</c> — correct for a
    /// <c>Tempest.App</c> type, which knows nothing of a dispatcher and must
    /// not — and then raises <c>Changed</c> on whatever thread that
    /// continuation landed on. An action that genuinely yields lands on the
    /// thread pool, and both real ones do: the favourite toggle writes a
    /// file, and the Object Editor's rename undo dispatches through the
    /// document store. Setting <c>Button.IsEnabled</c> from
    /// there throws <see cref="InvalidOperationException"/> out of
    /// <c>Changed</c>, which faulted the fire-and-forget undo and left the
    /// data changed with no toast, no status bar, no refresh and stale
    /// buttons.
    /// </para>
    /// <para>
    /// <b>Why the fast path is not decoration.</b> <c>Record</c> is
    /// synchronous and raises <c>Changed</c> on its caller's thread, which is
    /// always the UI thread; callers rely on the buttons being correct the
    /// instant it returns. Posting unconditionally would make that
    /// asynchronous and break them. <see cref="Dispatcher.CheckAccess"/>
    /// keeps the synchronous case exactly as it was and marshals only the
    /// case that was broken — the same shape
    /// <c>PlatformNotificationToastBridge</c> and <c>ThemeService</c> already
    /// use for the identical problem.
    /// </para>
    /// </remarks>
    private void RefreshButtons()
    {
        if (Dispatcher.UIThread.CheckAccess())
            RefreshButtonsCore();
        else
            Dispatcher.UIThread.Post(RefreshButtonsCore);
    }

    /// <summary>The refresh itself — always executed on the UI thread, never called directly except through <see cref="RefreshButtons"/>.</summary>
    private void RefreshButtonsCore()
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

        // Reported through the one shared tail (`WP-D1`); success-gated
        // (`TD-58`), because a failed undo changed nothing.
        await _reporter.ReportAsync(result, "Undo completed.", "Undo failed.", refresh: async () =>
        {
            await _explorerView.LoadAsync().ConfigureAwait(true);
            _refreshCockpit();
        }).ConfigureAwait(true);
    }

    /// <summary>Re-applies the most recently undone action, if any (`WP 10.6A`, `ADR-0099`) — mirrors <see cref="UndoAsync"/>'s own identical shape.</summary>
    public async Task RedoAsync()
    {
        var result = await Stack.RedoAsync().ConfigureAwait(true);
        if (result is null)
            return;

        // Reported through the one shared tail (`WP-D1`); success-gated
        // (`TD-58`), because a failed redo changed nothing.
        await _reporter.ReportAsync(result, "Redo completed.", "Redo failed.", refresh: async () =>
        {
            await _explorerView.LoadAsync().ConfigureAwait(true);
            _refreshCockpit();
        }).ConfigureAwait(true);
    }
}
