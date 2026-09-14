using Avalonia.Headless.XUnit;
using Tempest.Core.Logging;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `TD-154`: CI's `linux-launch-smoke` job used to grep only for
/// <c>TempestHost.EnterRunning</c>'s own "Host -> Running." line, which
/// fires deep inside <see cref="WorkspaceHost.StartAsync"/> — before
/// <c>MainWindowComposer</c> has built a single view — so the job proved
/// only that the Runtime Host's hosted-service pipeline started, never
/// that the Desktop shell itself composed. <c>MainWindowComposer.Layout</c>
/// now logs a second, later marker once every view, dialog, overlay and
/// the docking workspace it assembles already exists — the true end of
/// Desktop composition, reached on the identical real construction path
/// (<c>new MainWindow(host)</c>) every Desktop test in this suite already
/// drives, unlike <c>App.OnFrameworkInitializationCompleted</c>'s own
/// later <c>desktop.MainWindow = window;</c>, which no test in this suite
/// exercises (<see cref="NoBlockingPersistenceCallsTests"/>'s own remarks
/// name <c>App.cs</c> as running entirely pre-dispatcher-loop, excepted by
/// file rather than driven directly) and which does nothing more than
/// assign this already-fully-built window to a property — an inert set,
/// not further composition.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class DesktopCompositionMarkerTests
{
    [AvaloniaFact]
    public async Task ConstructingTheRealMainWindow_LogsDesktopComposed_AfterHostRunning()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();

            // The real construction path — exactly what `App.cs` itself
            // does with the Host it already started, and exactly what
            // every journey test in this suite already drives.
            _ = new MainWindow(host, new StubFilePicker());

            var logsDirectory = Path.Combine(root, "logs");
            var logFile = Assert.Single(Directory.GetFiles(
                logsDirectory, $"{RollingFileLogSink.FileNamePrefix}*{RollingFileLogSink.FileNameExtension}"));

            // `RollingFileLogSink` keeps this file open for the host's own
            // lifetime under `FileShare.ReadWrite` (it never closes it
            // between writes — see that class's own remarks) — a plain
            // `File.ReadAllLines` opens with only `FileShare.Read`, which
            // Windows refuses against the writer's still-open `Write`
            // handle, so this reads with the matching share explicitly.
            string logText;
            using (var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
                logText = reader.ReadToEnd();
            var lines = logText.Split('\n');

            var runningIndex = Array.FindIndex(lines, line => line.Contains("Host -> Running.", StringComparison.Ordinal));
            var composedIndex = Array.FindIndex(lines, line => line.Contains("Desktop -> Composed.", StringComparison.Ordinal));

            Assert.True(runningIndex >= 0, "The Runtime Host's own 'Host -> Running.' marker never logged.");
            Assert.True(composedIndex >= 0, "The Desktop shell's own 'Desktop -> Composed.' marker never logged.");
            Assert.True(
                composedIndex > runningIndex,
                "'Desktop -> Composed.' logged at or before 'Host -> Running.' - TD-154's own fix requires the Desktop " +
                "shell's composition marker to fire strictly after the Runtime Host's, not merely to exist.");
        }
        finally
        {
            await host.DisposeAsync();
        }
    }
}
