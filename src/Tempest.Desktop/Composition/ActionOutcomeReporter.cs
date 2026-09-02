using Tempest.Core.Commands;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// The one implementation of the Desktop's own report-then-refresh tail —
/// `WP-D1` (`TD-111`, audit finding `F-08`).
/// </summary>
/// <remarks>
/// <para>
/// <b>What this consolidates.</b> Seven places wrote the same four steps by
/// hand: set the status bar, raise a toast whose severity comes from
/// success, record the message in Command History, and refresh the surfaces
/// that depend on workspace data — the last one gated so a refused action
/// does not rebuild the world. <see cref="ActionOutcome"/> was introduced to
/// carry exactly the two facts that tail needs; what it never had was a
/// single consumer. This is that consumer.
/// </para>
/// <para>
/// <b>The refresh set is the caller's, deliberately.</b> Each migrated site
/// refreshes a different set of dependents, and those differences are
/// correct rather than accidental: the Explorer's own action does not reload
/// the Explorer (it has just reloaded itself), the Inspector's does not
/// re-read the Inspector, and the Object Editor's touches all three. A
/// reporter that chose one common set would be a behaviour change wearing a
/// cleanup's clothes, so the refresh arrives as a delegate and this type
/// only decides <i>whether</i> to run it.
/// </para>
/// <para>
/// <b>Gated on <see cref="ActionOutcome.WorkspaceChanged"/>, never on
/// <see cref="ActionOutcome.Succeeded"/>.</b> Those two are usually equal
/// and must not be assumed to be: <c>ObjectEditorView</c>'s own
/// Owner/Priority save reports a <i>failure that did change the
/// workspace</i> when the first half commits and the second is refused. Its
/// dependents must still refresh, or the shell shows values that are no
/// longer true.
/// </para>
/// <para>
/// <b>Presentation only.</b> This constructs no command, dispatches
/// nothing, touches no domain state, and decides no history policy — the
/// <c>recordHistory</c> callback it is given is <c>MainWindow</c>'s own
/// existing one, whose success heuristic (`AT-21`) is unchanged and not
/// this type's to reinterpret.
/// </para>
/// <para>
/// <b>Not every reporting site converges here, and that is deliberate.</b>
/// The Command Palette reports through <c>RefreshStatusBar</c> rather than
/// the message, and raises no toast; the Digital Thread graph sets the
/// status bar alone; <c>CommandUnavailable</c> reports a third severity
/// (Warning); and <c>MainWindow</c>'s project/task/risk CRUD is a different
/// shape entirely, with no <see cref="ActionOutcome"/> and no dependent
/// refresh. Absorbing any of them would mean either changing what a user
/// sees or growing a mode for one caller. See `TD-111`.
/// </para>
/// </remarks>
internal sealed class ActionOutcomeReporter
{
    private readonly StatusBarView _statusBar;
    private readonly ToastHost _toastHost;
    private readonly Action<string> _recordHistory;

    /// <summary>Initialises a new instance of the <see cref="ActionOutcomeReporter"/> class.</summary>
    /// <param name="statusBar">The shell's own status bar.</param>
    /// <param name="toastHost">The shell's own toast host.</param>
    /// <param name="recordHistory">The existing Command History callback — this type never decides what "recorded" means.</param>
    public ActionOutcomeReporter(StatusBarView statusBar, ToastHost toastHost, Action<string> recordHistory)
    {
        ArgumentNullException.ThrowIfNull(statusBar);
        ArgumentNullException.ThrowIfNull(toastHost);
        ArgumentNullException.ThrowIfNull(recordHistory);

        _statusBar = statusBar;
        _toastHost = toastHost;
        _recordHistory = recordHistory;
    }

    /// <summary>
    /// Reports <paramref name="message"/> and, when
    /// <paramref name="outcome"/> says the workspace changed, runs
    /// <paramref name="refresh"/>.
    /// </summary>
    /// <param name="message">The human-readable message the action produced.</param>
    /// <param name="outcome">The action's own outcome, as its View declared it.</param>
    /// <param name="refresh">The dependent surfaces this caller refreshes, or <see langword="null"/> if it refreshes none.</param>
    /// <remarks>
    /// Order is status bar, toast, history, refresh — the order every
    /// migrated site already used, preserved so a caller's observable
    /// sequence is unchanged.
    /// </remarks>
    public Task ReportAsync(string message, ActionOutcome outcome, Func<Task>? refresh = null) =>
        ReportAsync(message, outcome, refresh, recordHistory: true);

    /// <summary>
    /// Reports a <see cref="CommandResult"/>-shaped outcome, mapping
    /// success to <paramref name="successMessage"/> and failure to the
    /// result's own message, falling back to
    /// <paramref name="failureFallback"/> when it carries none.
    /// </summary>
    /// <param name="result">The result to report.</param>
    /// <param name="successMessage">What to say when it succeeded.</param>
    /// <param name="failureFallback">What to say when it failed and said nothing itself.</param>
    /// <param name="refresh">The dependent surfaces this caller refreshes.</param>
    public Task ReportAsync(CommandResult result, string successMessage, string failureFallback, Func<Task>? refresh = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        return ReportAsync(
            result.Succeeded ? successMessage : result.Message ?? failureFallback,
            ActionOutcome.From(result.Succeeded),
            refresh,
            recordHistory: true);
    }

    /// <summary>
    /// The same tail without the history entry — for the one migrated
    /// caller that has never recorded one.
    /// </summary>
    /// <param name="result">The result to report.</param>
    /// <param name="successMessage">What to say when it succeeded.</param>
    /// <param name="failureFallback">What to say when it failed and said nothing itself.</param>
    /// <param name="refresh">The dependent surfaces this caller refreshes.</param>
    /// <remarks>
    /// Drag-and-drop reparenting in the Project Explorer reports on the
    /// status bar and as a toast but writes nothing to Command History, and
    /// it did so before this consolidation. Recording one here would be a
    /// history-semantics change made only to make the code look uniform,
    /// which is the opposite of what this Work Package is for — so the
    /// difference is carried as an explicit entry point rather than
    /// normalised away.
    /// </remarks>
    public Task ReportWithoutHistoryAsync(CommandResult result, string successMessage, string failureFallback, Func<Task>? refresh = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        return ReportAsync(
            result.Succeeded ? successMessage : result.Message ?? failureFallback,
            ActionOutcome.From(result.Succeeded),
            refresh,
            recordHistory: false);
    }

    private async Task ReportAsync(string message, ActionOutcome outcome, Func<Task>? refresh, bool recordHistory)
    {
        _statusBar.SetText(message);
        _toastHost.Show(message, outcome.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);

        if (recordHistory)
            _recordHistory(message);

        // `TD-58`: a refused action changed nothing, so its dependents keep
        // their current — still correct — state.
        if (outcome.WorkspaceChanged && refresh is not null)
            await refresh().ConfigureAwait(true);
    }
}
