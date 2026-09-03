using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tempest.App.Workspace.Layout;
using Tempest.Desktop.Diagnostics;
using Tempest.Desktop.Docking;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The UI-thread boundary `WP-Z4` Stage 28 closes — the confirmed Windows
/// start-up crash.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WorkspaceLayoutController.RestoreAsync"/> awaits
/// <see cref="IWorkspaceLayoutStore.LoadAsync"/>, which reaches
/// <c>Tempest.Core</c>'s settings substrate — and Core's async methods
/// <c>ConfigureAwait(false)</c> internally. When that read genuinely
/// completes asynchronously the continuation resumes on a thread-pool
/// thread, and the <c>Load</c> that follows synchronously drives the
/// visual tree. Every <c>AvaloniaObject</c> read there calls
/// <c>Dispatcher.VerifyAccess</c>, so off the UI thread it threw
/// <see cref="InvalidOperationException"/> ("Call from invalid thread") —
/// inside an <c>async void</c> <c>Window.Opened</c> handler, which killed
/// the process moments after the window appeared.
/// </para>
/// <para>
/// <b>The store here is the real defect, not a mock.</b> It is a genuine
/// <see cref="IWorkspaceLayoutStore"/> that completes its read on a
/// thread-pool thread — exactly what the production settings substrate
/// does on Windows. Nothing about the controller is stubbed: the test
/// drives the real <c>RestoreAsync</c> against a real
/// <see cref="WorkspaceLayoutHost"/> in a shown window, which is what
/// makes it a regression test rather than a restatement of the fix.
/// </para>
/// </remarks>
public sealed class WorkspaceLayoutThreadAffinityTests
{
    private static readonly Guid Explorer = Guid.NewGuid();
    private static readonly Guid Document = Guid.NewGuid();
    private static readonly Guid Inspector = Guid.NewGuid();
    private static readonly Guid Output = Guid.NewGuid();

    /// <summary>
    /// The regression itself: a store whose read completes off the UI
    /// thread must still leave the arrangement rendered, and must not
    /// throw. Before the fix this threw "Call from invalid thread".
    /// </summary>
    [AvaloniaFact]
    public async Task RestoreAsync_WhenTheStoreCompletesOffTheUiThread_StillRendersAndDoesNotThrow()
    {
        var (controller, window, store) = BuildRig();

        try
        {
            // The saved arrangement differs from the fallback by one panel,
            // so "which tree won" is observable rather than a coincidence:
            // the default (Engineering) preset carries the Inspector.
            store.Saved = WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output).Remove(Inspector);

            var exception = await Record.ExceptionAsync(() =>
                controller.RestoreAsync(WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output)));

            Assert.Null(exception);

            // The read really did resume away from the UI thread — without
            // this the test could pass on a synchronous fast path and prove
            // nothing about the boundary it exists to guard.
            Assert.True(store.CompletedOffUiThread);

            // And the restore actually took effect: the saved arrangement
            // is the one now rendered, not the fallback.
            Assert.False(controller.Tree.Contains(Inspector));
            Assert.True(controller.Tree.Contains(Explorer));
            Assert.True(controller.Tree.Contains(Document));
            Assert.NotEmpty(controller.Host.TabGroups);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The same call from the UI thread must keep working unchanged — the
    /// fix marshals only when it has to, so the synchronous path this
    /// platform has always taken is not paying for a dispatcher hop.
    /// </summary>
    [AvaloniaFact]
    public async Task RestoreAsync_WhenTheStoreCompletesOnTheUiThread_RestoresWithoutMarshalling()
    {
        var (controller, window, store) = BuildRig();

        try
        {
            store.CompleteSynchronously = true;
            store.Saved = WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output).Remove(Inspector);

            await controller.RestoreAsync(WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output));

            Assert.False(store.CompletedOffUiThread);
            Assert.False(controller.Tree.Contains(Inspector));
            Assert.True(controller.Tree.Contains(Explorer));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Nothing saved is still the fallback, and it is still rendered on the
    /// UI thread — the branch a genuinely first-run machine takes.
    /// </summary>
    [AvaloniaFact]
    public async Task RestoreAsync_WithNothingSaved_AdoptsTheFallbackOffTheUiThread()
    {
        var (controller, window, store) = BuildRig();

        try
        {
            store.Saved = null;

            var fallback = WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output);
            var exception = await Record.ExceptionAsync(() => controller.RestoreAsync(fallback));

            Assert.Null(exception);
            Assert.True(store.CompletedOffUiThread);

            // The fallback — the Engineering three-column preset — is what
            // is now rendered, and it rendered without a thread violation.
            Assert.True(controller.Tree.Contains(Explorer));
            Assert.True(controller.Tree.Contains(Document));
            Assert.True(controller.Tree.Contains(Inspector));
            Assert.NotEmpty(controller.Host.TabGroups);
        }
        finally
        {
            window.Close();
        }
    }

    // ----------------------------------------------------------------
    // The crash record (`WP-Z4` Stage 28)
    // ----------------------------------------------------------------

    /// <summary>The record carries the four facts a start-up crash report needs.</summary>
    [Fact]
    public void CrashLog_Format_CapturesTimestampTypeMessageAndStackTrace()
    {
        Exception caught;
        try
        {
            throw new InvalidOperationException("Call from invalid thread");
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        var text = CrashLog.Format("Dispatcher.UnhandledException", caught);

        Assert.Contains("Timestamp (UTC)", text, StringComparison.Ordinal);
        Assert.Contains(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), text, StringComparison.Ordinal);
        Assert.Contains("Dispatcher.UnhandledException", text, StringComparison.Ordinal);
        Assert.Contains(typeof(InvalidOperationException).FullName!, text, StringComparison.Ordinal);
        Assert.Contains("Call from invalid thread", text, StringComparison.Ordinal);
        Assert.Contains(nameof(CrashLog_Format_CapturesTimestampTypeMessageAndStackTrace), text, StringComparison.Ordinal);
    }

    /// <summary>An inner exception is recorded too — the outer type alone rarely names the real fault.</summary>
    [Fact]
    public void CrashLog_Format_IncludesInnerExceptions()
    {
        var inner = new TimeoutException("the settings read did not complete");
        var outer = new InvalidOperationException("restore failed", inner);

        var text = CrashLog.Format("AppDomain.UnhandledException", outer);

        Assert.Contains("Inner exception", text, StringComparison.Ordinal);
        Assert.Contains("the settings read did not complete", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The recorder's own contract: it can never become the failure. A
    /// null exception is a silent return, never a secondary crash.
    /// </summary>
    [Fact]
    public void CrashLog_Record_WithNoException_DoesNotThrow() =>
        Assert.Null(Record.Exception(() => CrashLog.Record("test", null)));

    /// <summary>The record is anchored beside the executable, not to the working directory.</summary>
    [Fact]
    public void CrashLog_FilePath_IsAnchoredToTheApplicationDirectory()
    {
        Assert.StartsWith(AppContext.BaseDirectory, CrashLog.FilePath, StringComparison.Ordinal);
        Assert.EndsWith(CrashLog.FileName, CrashLog.FilePath, StringComparison.Ordinal);
    }

    /// <summary>
    /// The write path itself, not just the formatting: recording really
    /// creates the folder and file and really appends the exception. This
    /// is the half that matters on a machine where the crash has already
    /// happened and the file is the only evidence left.
    /// </summary>
    [Fact]
    public void CrashLog_Record_WritesTheExceptionToTheCrashFile()
    {
        var marker = $"stage28-write-probe-{Guid.NewGuid():N}";

        Exception caught;
        try
        {
            throw new InvalidOperationException(marker);
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        CrashLog.Record("Test.WritePath", caught);

        Assert.True(File.Exists(CrashLog.FilePath), $"No crash record at {CrashLog.FilePath}.");

        // Opened share-all: the recorder appends, and other tests in this
        // assembly may append concurrently — reading must not lock it out.
        using var stream = new FileStream(CrashLog.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var contents = reader.ReadToEnd();

        Assert.Contains(marker, contents, StringComparison.Ordinal);
        Assert.Contains("Test.WritePath", contents, StringComparison.Ordinal);
        Assert.Contains(nameof(CrashLog_Record_WritesTheExceptionToTheCrashFile), contents, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------

    private static (WorkspaceLayoutController Controller, Window Window, OffThreadLayoutStore Store) BuildRig()
    {
        var registry = new WorkspacePanelRegistry();
        registry.Register(new WorkspacePanelDescriptor(Explorer, "Explorer", new TextBlock()));
        registry.Register(new WorkspacePanelDescriptor(Document, "Documents", new TextBlock(), CanClose: false));
        registry.Register(new WorkspacePanelDescriptor(Inspector, "Inspector", new TextBlock()));
        registry.Register(new WorkspacePanelDescriptor(Output, "Output", new TextBlock()));

        var store = new OffThreadLayoutStore();
        var controller = new WorkspaceLayoutController(registry, store);

        var window = new Window { Content = controller.Host, Width = 1280, Height = 800 };
        window.Show();

        return (controller, window, store);
    }

    /// <summary>
    /// A real store whose read completes on a thread-pool thread — the
    /// production behaviour of Core's <c>ConfigureAwait(false)</c> settings
    /// substrate once the underlying file I/O genuinely goes async, which
    /// on Windows it reliably does.
    /// </summary>
    private sealed class OffThreadLayoutStore : IWorkspaceLayoutStore
    {
        /// <summary>The arrangement the read returns, or <see langword="null"/> for "nothing saved".</summary>
        public WorkspaceLayoutTree? Saved { get; set; }

        /// <summary>Completes the read inline instead, exercising the fast path.</summary>
        public bool CompleteSynchronously { get; set; }

        /// <summary>Whether the read's continuation genuinely resumed away from the UI thread.</summary>
        public bool CompletedOffUiThread { get; private set; }

        public Task SaveAsync(WorkspaceLayoutTree tree, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task<WorkspaceLayoutTree?> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (CompleteSynchronously)
            {
                CompletedOffUiThread = false;
                return Saved;
            }

            // A real timer-backed delay, not a yield over an already-
            // completed task: an awaiter that subscribes to a task which
            // has *already* finished resumes inline on the calling thread,
            // which would leave this test passing on the UI thread by
            // accident and asserting nothing. A delay cannot be complete at
            // the await point, so with ConfigureAwait(false) the
            // continuation is always scheduled onto the thread pool — the
            // production shape, deterministically.
            await Task.Delay(5, cancellationToken).ConfigureAwait(false);

            CompletedOffUiThread = !Dispatcher.UIThread.CheckAccess();
            return Saved;
        }
    }
}
