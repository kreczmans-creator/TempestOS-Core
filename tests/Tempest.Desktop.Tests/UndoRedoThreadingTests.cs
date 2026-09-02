using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Commands;
using Tempest.Desktop.Composition;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP-Z2` (`TD-117`, `ADR-0119`) — an Undo or Redo whose action genuinely
/// yields still refreshes the Quick Access Toolbar, and does it on the UI
/// thread.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these pin.</b> <see cref="UndoRedoStack.UndoAsync"/> awaits
/// the action with <c>ConfigureAwait(false)</c> and then raises
/// <c>Changed</c>. When the action genuinely yields — and both real ones do,
/// the favourite toggle writing a file and the Object Editor's rename undo
/// dispatching through the document store — that continuation resumes on the
/// thread pool, so <c>Changed</c> was raised there and
/// <c>UndoRedoCoordinator.RefreshButtons</c> set
/// <c>Button.IsEnabled</c> from a non-UI thread. Avalonia's
/// <c>VerifyAccess</c> threw, the exception escaped through the
/// fire-and-forget <c>_ = UndoAsync()</c>, and the user was left with the
/// data changed, no toast, no status bar, no refresh and stale buttons.
/// Present and reachable in every release from `v0.10.0` to `v0.13.1`.
/// </para>
/// <para>
/// <b>Why each assertion is here.</b> Asserting only that
/// <c>UndoAsync()</c> completes would pass against the broken code the
/// moment the action stopped yielding, so each test proves its own premise —
/// that the action really did resume off the UI thread — before asserting
/// the outcome. And asserting the outcome means asserting the coordinator's
/// UI state, not the stack's: the stack was never broken.
/// </para>
/// <para>
/// <b>Why a delay rather than a real save.</b> The production actions yield
/// because they write files, but a test that depends on persistence to
/// produce a thread hop is testing the persistence layer's timing. An
/// explicit <c>Task.Delay</c> with <c>ConfigureAwait(false)</c> yields by
/// construction, which is the property under test.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public class UndoRedoThreadingTests
{
    /// <summary>
    /// An action that is guaranteed to resume off the UI thread, and records
    /// whether it actually did so.
    /// </summary>
    private sealed class YieldingAction
    {
        public bool ResumedOffUiThread { get; private set; }

        public async Task<CommandResult> RunAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            ResumedOffUiThread = !Dispatcher.UIThread.CheckAccess();
            return CommandResult.Success();
        }
    }

    private static async Task WithCoordinatorAsync(Func<UndoRedoCoordinator, Task> body)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            await host.Workspace!.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var window = new MainWindow(host);

            await body(GetPrivateField<UndoRedoCoordinator>(window, "_undoRedo"));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AnUndoActionThatGenuinelyYields_StillRefreshesTheToolbar_WithoutAThreadViolation()
    {
        await WithCoordinatorAsync(async coordinator =>
        {
            var action = new YieldingAction();
            coordinator.Stack.Record(new UndoableAction("Yielding action", undo: action.RunAsync, redo: action.RunAsync));

            // Record is synchronous and on the UI thread, so the toolbar is
            // correct the instant it returns — the fast path, unchanged.
            Assert.True(coordinator.UndoButton.IsEnabled);
            Assert.False(coordinator.RedoButton.IsEnabled);

            // Before `WP-Z2` this threw InvalidOperationException
            // ("Call from invalid thread") out of Changed.
            await coordinator.UndoAsync();

            // The premise: this test is worthless if the action did not
            // actually leave the UI thread.
            Assert.True(action.ResumedOffUiThread, "The undo action did not resume off the UI thread — this test can no longer detect the defect it exists for.");

            // Drain the marshalled refresh, then assert the coordinator's own
            // UI state — the thing that was broken.
            Dispatcher.UIThread.RunJobs();
            Assert.False(coordinator.UndoButton.IsEnabled);
            Assert.True(coordinator.RedoButton.IsEnabled);
        });
    }

    [AvaloniaFact]
    public async Task ARedoActionThatGenuinelyYields_StillRefreshesTheToolbar_WithoutAThreadViolation()
    {
        await WithCoordinatorAsync(async coordinator =>
        {
            var undo = new YieldingAction();
            var redo = new YieldingAction();
            coordinator.Stack.Record(new UndoableAction("Yielding action", undo: undo.RunAsync, redo: redo.RunAsync));

            await coordinator.UndoAsync();
            Dispatcher.UIThread.RunJobs();
            Assert.True(coordinator.RedoButton.IsEnabled);

            // RedoAsync has the identical ConfigureAwait(false)-then-Changed
            // shape, so it was broken identically and is fixed by the same
            // single subscription path.
            await coordinator.RedoAsync();

            Assert.True(redo.ResumedOffUiThread, "The redo action did not resume off the UI thread — this test can no longer detect the defect it exists for.");

            Dispatcher.UIThread.RunJobs();
            Assert.True(coordinator.UndoButton.IsEnabled);
            Assert.False(coordinator.RedoButton.IsEnabled);
        });
    }
}
