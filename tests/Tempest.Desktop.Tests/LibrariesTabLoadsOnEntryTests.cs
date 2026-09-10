using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The first Windows run of <c>v0.18.0</c> opened the Libraries tab and
/// found nothing but the add-material form: every earlier test had called
/// <see cref="LibrariesView.RefreshAsync"/> by hand, so the fact that the
/// application never did went unseen. This test reaches the tab the way
/// the application does — enter the Evidence area, render it, select the
/// tab — and refreshes nothing itself.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class LibrariesTabLoadsOnEntryTests
{
    [AvaloniaFact]
    public async Task EnteringTheEvidenceArea_ListsEverySeededLibrary_WithoutAnExplicitRefresh()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.Evidence);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var evidenceWorkspace = GetPrivateField<EvidenceWorkspaceView>(window, "_evidenceWorkspace");
            var tabs = (TabControl)evidenceWorkspace.Content!;
            tabs.SelectedIndex = 1;
            LayOut(window);

            var librariesView = (LibrariesView)((TabItem)tabs.Items[1]!).Content!;
            var headings = librariesView.GetLogicalDescendants().OfType<TextBlock>()
                .Select(t => t.Text ?? string.Empty)
                .Where(text => text.EndsWith(")", StringComparison.Ordinal))
                .ToList();

            // Every library is listed with at least one record. The exact counts
            // are the shipped seed's (6, 7, 2, 14, 12) only in the shipped
            // application: this headless run's own Tempest.Samples module
            // registers its own materials first, and a library that is not
            // empty is deliberately left alone by the start-up seeding.
            foreach (var library in new[] { "Materials", "Fasteners", "Bearings", "Standards", "Constants" })
            {
                var heading = Assert.Single(headings, h => h.StartsWith(library + " (", StringComparison.Ordinal));
                var count = int.Parse(heading[(library.Length + 2)..^1], System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(count >= 1, $"{library} lists no records.");
            }

            Assert.DoesNotContain(librariesView.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "No reference records are seeded.");
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Avalonia.Size(1900, 1050));
            window.Arrange(new Avalonia.Rect(0, 0, 1900, 1050));
        }
    }
}
