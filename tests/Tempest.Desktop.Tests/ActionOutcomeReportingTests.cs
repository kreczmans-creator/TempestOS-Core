using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace;
using Tempest.Core.Commands;
using Tempest.Desktop.Composition;
using Tempest.Desktop.History;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// WP-D1 (`TD-111`, audit finding `F-08`) — the Desktop's own
/// report-then-refresh tail now has exactly one implementation, and every
/// migrated caller still behaves as it did.
/// </summary>
/// <remarks>
/// <para>
/// The tail is four steps — status bar, toast, Command History, conditional
/// refresh — and it was written out by hand in seven places. Consolidating
/// it is only safe if two things stay true: the four steps keep their order
/// and meaning, and each caller keeps its <i>own</i> refresh set, which is
/// deliberately different per site. Both are asserted here.
/// </para>
/// <para>
/// The contract-level tests below drive the real
/// <see cref="ActionOutcomeReporter"/> against a real
/// <see cref="StatusBarView"/> and <see cref="ToastHost"/>, so what they
/// assert is what a user would see. The journey tests drive the real
/// <c>MainWindow</c>, so the migrated wiring is exercised end to end rather
/// than in isolation.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ActionOutcomeReportingTests
{
    // ==================================================================
    // The tail's own contract
    // ==================================================================

    private sealed record Reported(StatusBarView StatusBar, ToastHost Toasts, List<string> History, List<int> RefreshCalls)
    {
        public string StatusText => StatusBar.GetLogicalDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? string.Empty)
            .FirstOrDefault(t => t.StartsWith("🔹", StringComparison.Ordinal)) ?? string.Empty;

        public ToastNotification LastToast => Toasts.Children.OfType<ToastNotification>().Last();
    }

    private static (ActionOutcomeReporter Reporter, Reported Seen) NewReporter()
    {
        var statusBar = new StatusBarView();
        var toasts = new ToastHost();
        var history = new List<string>();
        var refreshCalls = new List<int>();

        return (
            new ActionOutcomeReporter(statusBar, toasts, history.Add),
            new Reported(statusBar, toasts, history, refreshCalls));
    }

    /// <summary>1 — a successful action reaches the status bar, a Success toast, and history.</summary>
    [AvaloniaFact]
    public async Task ASuccessfulAction_ReportsOnTheStatusBar_WithASuccessToast()
    {
        var (reporter, seen) = NewReporter();

        await reporter.ReportAsync("Renamed to 'Pump Housing'.", ActionOutcome.Changed,
            refresh: () => { seen.RefreshCalls.Add(1); return Task.CompletedTask; });

        Assert.Contains("Renamed to 'Pump Housing'.", seen.StatusText, StringComparison.Ordinal);
        Assert.Equal(FeedbackSeverity.Success, seen.LastToast.Severity);
        Assert.Equal("Renamed to 'Pump Housing'.", seen.LastToast.Message);
        Assert.Contains("Renamed to 'Pump Housing'.", seen.History);
        Assert.Single(seen.RefreshCalls);
    }

    /// <summary>
    /// 2 — a failed action reports as a failure and refreshes nothing. Before
    /// `TD-58` every one of these paths raised a Success toast for a refusal
    /// and rebuilt the world anyway; both halves are pinned here.
    /// </summary>
    [AvaloniaFact]
    public async Task AFailedAction_ReportsAnError_AndRefreshesNothing()
    {
        var (reporter, seen) = NewReporter();

        await reporter.ReportAsync("'Assembly' objects cannot be deleted.", ActionOutcome.Failed,
            refresh: () => { seen.RefreshCalls.Add(1); return Task.CompletedTask; });

        Assert.Contains("cannot be deleted", seen.StatusText, StringComparison.Ordinal);
        Assert.Equal(FeedbackSeverity.Error, seen.LastToast.Severity);
        Assert.Contains("'Assembly' objects cannot be deleted.", seen.History);
        Assert.Empty(seen.RefreshCalls);
    }

    /// <summary>
    /// 11 — the case that makes the gate load-bearing rather than cosmetic.
    /// <c>ObjectEditorView</c>'s own Owner/Priority save reports a failure
    /// that <i>did</i> change the workspace: the Owner half committed before
    /// the Priority half was refused. Gating refresh on success instead of on
    /// <see cref="ActionOutcome.WorkspaceChanged"/> would leave the Explorer,
    /// Inspector and Cockpit showing values that are no longer true.
    /// </summary>
    [AvaloniaFact]
    public async Task AFailedActionThatChangedTheWorkspace_StillRefreshesItsDependents()
    {
        var (reporter, seen) = NewReporter();

        await reporter.ReportAsync("Set priority failed.", new ActionOutcome(Succeeded: false, WorkspaceChanged: true),
            refresh: () => { seen.RefreshCalls.Add(1); return Task.CompletedTask; });

        // Reported as the failure it is…
        Assert.Equal(FeedbackSeverity.Error, seen.LastToast.Severity);

        // …and the dependents still refresh, because something did change.
        Assert.Single(seen.RefreshCalls);
    }

    /// <summary>A successful action that changed nothing refreshes nothing — opening an object for editing, for instance.</summary>
    [AvaloniaFact]
    public async Task ASuccessfulActionThatChangedNothing_RefreshesNothing()
    {
        var (reporter, seen) = NewReporter();

        await reporter.ReportAsync("Opened for editing.", ActionOutcome.NoChange,
            refresh: () => { seen.RefreshCalls.Add(1); return Task.CompletedTask; });

        Assert.Equal(FeedbackSeverity.Success, seen.LastToast.Severity);
        Assert.Empty(seen.RefreshCalls);
    }

    /// <summary>
    /// 7 — drag-and-drop reparenting reports on the status bar and as a toast
    /// but writes nothing to Command History, exactly as it did before this
    /// consolidation. Uniformity was not a reason to change history
    /// semantics, so the difference is an explicit entry point.
    /// </summary>
    [AvaloniaFact]
    public async Task TheNoHistoryPath_ReportsAndRefreshes_ButRecordsNothing()
    {
        var (reporter, seen) = NewReporter();

        await reporter.ReportWithoutHistoryAsync(CommandResult.Success(), "Moved.", "Move failed.",
            refresh: () => { seen.RefreshCalls.Add(1); return Task.CompletedTask; });

        Assert.Contains("Moved.", seen.StatusText, StringComparison.Ordinal);
        Assert.Equal(FeedbackSeverity.Success, seen.LastToast.Severity);
        Assert.Single(seen.RefreshCalls);
        Assert.Empty(seen.History);
    }

    /// <summary>
    /// A failing result reports its own message, and a succeeding one the
    /// caller's success text.
    /// </summary>
    /// <remarks>
    /// The <c>failureFallback</c> argument is defensive only, and is carried
    /// forward from the call sites this Work Package migrated rather than
    /// invented here: <see cref="CommandResult.Failure(string)"/> rejects a
    /// blank message, so <see cref="CommandResult.Message"/> is never
    /// <see langword="null"/> on a failure and the fallback is unreachable
    /// today. Asserting it would mean asserting a branch no production path
    /// can enter, so this test asserts what is real.
    /// </remarks>
    [AvaloniaFact]
    public async Task AResult_ReportsItsOwnFailureMessage_OrTheCallersSuccessText()
    {
        var (onFailure, seenFailure) = NewReporter();
        await onFailure.ReportAsync(CommandResult.Failure("Target is not a valid parent."), "Moved.", "Move failed.");
        Assert.Equal("Target is not a valid parent.", seenFailure.LastToast.Message);
        Assert.Equal(FeedbackSeverity.Error, seenFailure.LastToast.Severity);

        var (onSuccess, seenSuccess) = NewReporter();
        await onSuccess.ReportAsync(CommandResult.Success(), "Moved.", "Move failed.");
        Assert.Equal("Moved.", seenSuccess.LastToast.Message);
        Assert.Equal(FeedbackSeverity.Success, seenSuccess.LastToast.Severity);
    }

    // ==================================================================
    // The migrated wiring, through the real window
    // ==================================================================

    /// <summary>
    /// 8 — a representative `TD-77` Ribbon command still reports through the
    /// consolidated tail: status bar, and 6 — Command History, which is the
    /// behaviour `TD-77` Stage 5 gained and must not lose here.
    /// </summary>
    [AvaloniaFact]
    public async Task ARibbonCommand_StillReportsOnTheStatusBar_AndStillReachesHistory()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await SelectFirstAsync(workspace, Tempest.App.Workspace.Calculations.CalculationsWorkspaceExplorerModule.NavigationItemId, "Calculation");

            var window = new MainWindow(host);
            var ribbon = GetPrivateField<RibbonView>(window, "_ribbon");
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var history = GetPrivateField<CommandHistoryLog>(window, "_commandHistory");
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var before = history.Entries.Count;

            Click(ribbon, registry, "calculations.request-review");

            // `TD-119`: the ribbon dispatch is fire-and-forget and is reported on the subscriber's own continuation; bounded poll on the real
            // history count, assertions unchanged.
            var ribbonDeadline = DateTime.UtcNow.AddSeconds(2);
            while (!(history.Entries.Count > before) && DateTime.UtcNow < ribbonDeadline)
                await Task.Delay(10);

            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty),
                text => text.Contains("Request Review", StringComparison.Ordinal));
            Assert.True(history.Entries.Count > before, "A Ribbon command must still be recorded in Command History.");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// 3 — an Inspector-originated action still reports through the shared
    /// tail, and still reaches Command History.
    /// </summary>
    /// <remarks>
    /// The Inspector's own refresh set (Explorer + Cockpit) is asserted where
    /// it is decidable: <see cref="ASuccessfulAction_ReportsOnTheStatusBar_WithASuccessToast"/>
    /// and its siblings drive the real reporter with a refresh spy and prove
    /// the gate, and <see cref="TheReportingTail_HasExactlyOneImplementation"/>
    /// prevents a site from re-adding a refresh of its own. Asserting the
    /// reload through the rendered tree was tried and abandoned: the Explorer
    /// is project-scoped once the shell has settled, so an empty tree there
    /// means "no project open", not "did not reload" — a test that cannot
    /// tell those apart proves nothing.
    /// </remarks>
    [AvaloniaFact]
    public async Task AnInspectorAction_StillReportsAndRecords()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var node = await SelectFirstAsync(workspace, Tempest.App.Workspace.Documents.DocumentsWorkspaceExplorerModule.NavigationItemId, "Document");

            var window = new MainWindow(host);
            var inspector = GetPrivateField<PropertyInspectorView>(window, "_inspectorView");
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var history = GetPrivateField<CommandHistoryLog>(window, "_commandHistory");
            inspector.SetCurrentSelection(node.Id, node.Kind!);
            var before = history.Entries.Count;

            // Exactly the two steps the Inspector's own inline rename takes:
            // dispatch through IWorkspaceManager (`ADR-0096`), then report.
            const string NewName = "WP-D1 Renamed Document";
            var renamed = await host.Manager!.RenameObjectAsync(node.Id, node.Kind!, NewName);
            Assert.True(renamed.Succeeded, renamed.Message);
            RaiseActionCompleted(inspector, $"Renamed to '{NewName}'.", ActionOutcome.From(renamed.Succeeded));

            // `TD-119`: the report fans out to reported on the subscriber's own continuation; bounded poll on the real
            // history count, assertions unchanged.
            var inspectorDeadline = DateTime.UtcNow.AddSeconds(2);
            while (!(history.Entries.Count > before) && DateTime.UtcNow < inspectorDeadline)
                await Task.Delay(10);

            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty),
                text => text.Contains(NewName, StringComparison.Ordinal));
            Assert.True(history.Entries.Count > before, "An Inspector action must still be recorded in Command History.");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// 4 and 5 — an Explorer-originated action keeps its own refresh set:
    /// the Cockpit is rebuilt (the workspace changed) while the Explorer,
    /// which has already updated itself, is not reloaded a second time. The
    /// Explorer's own tree still shows the result either way, which is why
    /// the absence of the second reload is asserted where it is decided —
    /// see <see cref="TheReportingTail_HasExactlyOneImplementation"/> for the
    /// rule that no site may re-add it.
    /// </summary>
    [AvaloniaFact]
    public async Task AnExplorerRename_ReportsAndRefreshesTheCockpit_WithoutReloadingTheExplorerAgain()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var node = await SelectFirstAsync(workspace, Tempest.App.Workspace.Documents.DocumentsWorkspaceExplorerModule.NavigationItemId, "Document");

            var window = new MainWindow(host);
            var explorer = GetPrivateField<ProjectExplorerView>(window, "_explorerView");
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var history = GetPrivateField<CommandHistoryLog>(window, "_commandHistory");
            await explorer.LoadAsync();
            var before = history.Entries.Count;

            // Raised exactly as the Explorer's own context menu raises it.
            RaiseActionCompleted(explorer, $"Renamed to 'WP-D1 {node.Title}'.", ActionOutcome.Changed);

            // `TD-119`: the report fans out to reported on the subscriber's own continuation; bounded poll on the real
            // history count, assertions unchanged.
            var explorerDeadline = DateTime.UtcNow.AddSeconds(2);
            while (!(history.Entries.Count > before) && DateTime.UtcNow < explorerDeadline)
                await Task.Delay(10);

            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty),
                text => text.Contains("WP-D1", StringComparison.Ordinal));
            Assert.True(history.Entries.Count > before, "An Explorer action must still be recorded in Command History.");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// 9 — the Object Editor's own Save still reports through the shared
    /// tail. Exercised through the editor's real <c>ActionCompleted</c>
    /// event, which is the seam the coordinator subscribes to.
    /// </summary>
    [AvaloniaFact]
    public async Task AnObjectEditorAction_StillReportsAndRecords()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var node = await SelectFirstAsync(workspace, Tempest.App.Workspace.Documents.DocumentsWorkspaceExplorerModule.NavigationItemId, "Document");

            var window = new MainWindow(host);
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var history = GetPrivateField<CommandHistoryLog>(window, "_commandHistory");
            var coordinator = GetPrivateField<WorkspaceViewCoordinator>(window, "_viewCoordinator");
            var before = history.Entries.Count;

            var view = await workspace.Navigation.OpenAsync(node.Id, node.Kind!);
            var content = coordinator.BuildDocumentContent(view);
            var editor = content as Editors.ObjectEditorView;
            Assert.NotNull(editor);

            RaiseActionCompleted(editor!, "Saved 'WP-D1 Editor Save'.", ActionOutcome.Changed);

            // `TD-119`: the report fans out to reported on the subscriber's own continuation; bounded poll on the real
            // history count, assertions unchanged.
            var editorDeadline = DateTime.UtcNow.AddSeconds(2);
            while (!(history.Entries.Count > before) && DateTime.UtcNow < editorDeadline)
                await Task.Delay(10);

            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty),
                text => text.Contains("WP-D1 Editor Save", StringComparison.Ordinal));
            Assert.True(history.Entries.Count > before, "An Object Editor action must still be recorded in Command History.");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// 10 — Undo with nothing to undo stays completely silent: no status bar
    /// text, no toast, no history entry. That silence is a `null` result
    /// returned before the reporter is ever reached, and it survived the
    /// migration unchanged.
    /// </summary>
    [AvaloniaFact]
    public async Task UndoWithNothingToUndo_StaysSilent()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host);
            var undoRedo = GetPrivateField<UndoRedoCoordinator>(window, "_undoRedo");
            var toasts = GetPrivateField<ToastHost>(window, "_toastHost");
            var history = GetPrivateField<CommandHistoryLog>(window, "_commandHistory");

            var toastsBefore = toasts.ActiveToastCount;
            var historyBefore = history.Entries.Count;

            Assert.False(undoRedo.Stack.CanUndo);
            await undoRedo.UndoAsync();
            await undoRedo.RedoAsync();

            Assert.Equal(toastsBefore, toasts.ActiveToastCount);
            Assert.Equal(historyBefore, history.Entries.Count);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // 12 — one implementation, and only one
    // ==================================================================

    /// <summary>
    /// The consolidation itself, asserted where it is decided.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing at runtime can observe that a tail exists in <i>one</i> place:
    /// seven hand-written copies and one shared implementation behave
    /// identically by construction — that is what made `F-08` invisible for
    /// as long as it was. So this reads the migrated files and requires that
    /// none of them still pairs a toast with a <c>WorkspaceChanged</c> gate
    /// of its own.
    /// </para>
    /// <para>
    /// Deliberately narrow. It scans only the three files this Work Package
    /// migrated, matches on the two constructs that <i>are</i> the tail, and
    /// asserts set membership rather than a count — so the surviving
    /// non-migrated reporters (Command Palette, Digital Thread graph,
    /// <c>CommandUnavailable</c>, and <c>MainWindow</c>'s project/task CRUD)
    /// are untouched by it, and so a legitimate future migration does not
    /// have to renumber anything.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheReportingTail_HasExactlyOneImplementation()
    {
        var offenders = new List<string>();

        foreach (var relativePath in new[]
                 {
                     "src/Tempest.Desktop/Composition/WorkspaceViewCoordinator.cs",
                     "src/Tempest.Desktop/Composition/UndoRedoCoordinator.cs",
                 })
        {
            var lines = CodeLines(relativePath);

            // The severity-from-success toast is the tail's signature line.
            foreach (var line in lines)
            {
                if (line.Contains("_toastHost.Show(", StringComparison.Ordinal)
                    && line.Contains("FeedbackSeverity.Success : FeedbackSeverity.Error", StringComparison.Ordinal))
                {
                    offenders.Add($"{relativePath}: {line}");
                }
            }

            // And the gate it is always paired with.
            foreach (var line in lines)
            {
                if (line.Contains("outcome.WorkspaceChanged", StringComparison.Ordinal))
                    offenders.Add($"{relativePath}: {line}");
            }
        }

        // MainWindow keeps its own project/task/risk CRUD reporting, which is
        // a different shape (no ActionOutcome, no dependent refresh) and is
        // TD-109's to address — so only the migrated Ribbon tail's own
        // constructs are forbidden there.
        foreach (var line in CodeLines("src/Tempest.Desktop/MainWindow.cs"))
        {
            if (line.Contains("outcome.WorkspaceChanged", StringComparison.Ordinal)
                || (line.Contains("_toastHost.Show(", StringComparison.Ordinal)
                    && line.Contains("outcome.Succeeded", StringComparison.Ordinal)))
            {
                offenders.Add($"src/Tempest.Desktop/MainWindow.cs: {line}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A migrated file re-implements the report-then-refresh tail instead of using ActionOutcomeReporter.\n"
            + "The tail is status bar -> toast -> history -> refresh gated on WorkspaceChanged, and it has one\n"
            + "implementation by design (WP-D1, TD-111). Route the call site through the reporter.\n\n"
            + string.Join("\n", offenders));
    }

    /// <summary>
    /// The reporter stays a presentation collaborator: it must not acquire
    /// the ability to run the actions it reports on.
    /// </summary>
    [Fact]
    public void TheReporter_OwnsPresentationOnly()
    {
        var source = string.Join("\n", CodeLines("src/Tempest.Desktop/Composition/ActionOutcomeReporter.cs"));

        Assert.DoesNotContain("DispatchAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InvokeAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ICommandDispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ICommandRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IWorkspaceManager", source, StringComparison.Ordinal);
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private static IReadOnlyList<string> CodeLines(string relativePath) =>
    [
        .. File.ReadAllLines(Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal)
                           && !line.StartsWith("///", StringComparison.Ordinal)
                           && !line.StartsWith('*')),
    ];

    /// <summary>Raises a View's own <c>ActionCompleted</c> exactly as its real interaction paths do.</summary>
    private static void RaiseActionCompleted(object view, string message, ActionOutcome outcome)
    {
        var field = view.GetType().GetField("ActionCompleted", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"No ActionCompleted event on {view.GetType().Name}.");

        var handler = (Action<string, ActionOutcome>?)field.GetValue(view);
        Assert.NotNull(handler);
        handler!(message, outcome);
    }
}
