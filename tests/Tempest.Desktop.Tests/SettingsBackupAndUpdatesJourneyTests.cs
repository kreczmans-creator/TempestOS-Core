using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Settings → Data's own backup and restore, and Settings → Updates,
/// through the real window (`WP 21.5A`, `WP RC.0A` scope items 1 and 4).
/// The full restore-and-restart round trip (moving the current database
/// aside, copying a backup in) is proven once, directly, in
/// <c>Tempest.Core.Tests.Persistence.BackupServiceTests</c> — the exact
/// "back up and restore round-trip on a real SQLite file in a temp folder"
/// this Work Package's brief asks for; this class proves the two things
/// only the real window can: the Back up now button drives that same
/// mechanism end to end via the real Settings picker, and Restore is
/// refused, honestly, while a project is open.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SettingsBackupAndUpdatesJourneyTests
{
    [AvaloniaFact]
    public async Task Settings_BackUpNow_CreatesAVerifiedBackupThroughTheRealPicker()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        var filePicker = new StubFilePicker();
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, filePicker);
            LayOut(window);
            await RenderUntilAsync(window, () => window.Ready.IsCompleted);

            await host.ShellNavigator!.GoToModuleAsync(ShellArea.Settings);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var settingsView = GetPrivateField<SettingsView>(window, "_settingsView");
            await RenderUntilAsync(window, () => BackUpNowButton(settingsView) is not null);

            var destination = Path.Combine(Path.GetTempPath(), $"tempestos-settings-backup-{Guid.NewGuid():N}.db");
            filePicker.SetNextSavePath(destination);

            var backUpNowButton = BackUpNowButton(settingsView)!;
            backUpNowButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => BackupStatus(settingsView) is { Text: { Length: > 0 } text } && text.StartsWith("Backed up", StringComparison.Ordinal));

            try
            {
                Assert.True(File.Exists(destination));

                var status = BackupStatus(settingsView)!.Text!;
                Assert.Contains("verified", status, StringComparison.Ordinal);
            }
            finally
            {
                if (File.Exists(destination))
                    File.Delete(destination);
            }
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Settings_RestoreFromBackup_IsRefusedWhileAProjectIsOpen()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        var filePicker = new StubFilePicker();
        try
        {
            await host.StartAsync();

            var project = await host.ProjectDirectory!.CreateAsync("P-RESTORE-GUARD", "Restore Guard");
            await host.ShellNavigator!.OpenProjectAsync(project.Id);

            var window = new MainWindow(host, filePicker);
            LayOut(window);
            await RenderUntilAsync(window, () => window.Ready.IsCompleted);

            await host.ShellNavigator!.GoToModuleAsync(ShellArea.Settings);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var settingsView = GetPrivateField<SettingsView>(window, "_settingsView");
            await RenderUntilAsync(window, () => RestoreButton(settingsView) is not null);

            var restartCalled = false;
            settingsView.RestartProcess = () => restartCalled = true;

            var restoreButton = RestoreButton(settingsView)!;
            restoreButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => BackupStatus(settingsView) is { Text: { Length: > 0 } });

            Assert.Equal("Close the open project before restoring a backup.", BackupStatus(settingsView)!.Text);
            Assert.False(restartCalled);

            // The picker was never even reached — refused before asking
            // which file to restore from.
            Assert.Empty(filePicker.PickRequests);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Settings_Updates_ReportsUnavailableForARunTheInstallerNeverInstalled()
    {
        // No test process ever calls Velopack.VelopackApp.Build().Run()
        // (only Tempest.Desktop.Program.Main does) - so this is exactly the
        // "not installed" case every dotnet test run, dotnet run, and the
        // plain zip share.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await RenderUntilAsync(window, () => window.Ready.IsCompleted);

            await host.ShellNavigator!.GoToModuleAsync(ShellArea.Settings);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var settingsView = GetPrivateField<SettingsView>(window, "_settingsView");
            await RenderUntilAsync(window, () => CheckForUpdatesNowButton(settingsView) is not null);

            var checkNowButton = CheckForUpdatesNowButton(settingsView)!;
            checkNowButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () =>
                UpdateStatus(settingsView) is { Text: { Length: > 0 } text } && text != "Checking…");

            Assert.Equal("Not installed via the installer — updates unavailable.", UpdateStatus(settingsView)!.Text);

            var applyButton = ApplyUpdateButton(settingsView);
            Assert.NotNull(applyButton);
            Assert.False(applyButton!.IsVisible);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static Button? BackUpNowButton(SettingsView settingsView) =>
        settingsView.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => Equals(b.Content, "Back up now…"));

    private static Button? RestoreButton(SettingsView settingsView) =>
        settingsView.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => Equals(b.Content, "Restore from backup…"));

    private static Button? CheckForUpdatesNowButton(SettingsView settingsView) =>
        settingsView.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => Equals(b.Content, "Check now"));

    private static Button? ApplyUpdateButton(SettingsView settingsView) =>
        settingsView.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => Equals(AutomationProperties.GetName(b), "Apply update"));

    private static TextBlock? BackupStatus(SettingsView settingsView) =>
        settingsView.GetLogicalDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.Text is { Length: > 0 } text && (text.StartsWith("Backed up", StringComparison.Ordinal) || text.StartsWith("Backup failed", StringComparison.Ordinal) || text.StartsWith("Close the open project", StringComparison.Ordinal)));

    private static TextBlock? UpdateStatus(SettingsView settingsView) =>
        settingsView.GetLogicalDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.Text is { } text && (
                text.Contains("installed via the installer", StringComparison.Ordinal)
                || text.Contains("is available", StringComparison.Ordinal)
                || text == "No update checked yet."
                || text == "Checking…"));

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = Deadline(20);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }
    }

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1900, 1050));
            window.Arrange(new Rect(0, 0, 1900, 1050));
        }
    }
}
