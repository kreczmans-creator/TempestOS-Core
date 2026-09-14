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
/// The first Windows run of <c>v0.18.0</c> opened the Libraries tab and
/// found nothing but the add-material form: every earlier test had called
/// <see cref="LibrariesView.RefreshAsync"/> by hand, so the fact that the
/// application never did went unseen. This test reaches the tab the way
/// the application does — enter Engineering, render it, select Reference
/// data (`WP 19.7A`: moved off Evidence, which is a per-project tab now)
/// — and refreshes nothing itself.
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
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single().SelectNode("Reference data");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<LibrariesView>().Any());
            LayOut(window);

            var librariesView = window.GetLogicalDescendants().OfType<LibrariesView>().Single();
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

            // `WP 19.10P` (D15): every one of the eight governed libraries
            // gets its own heading, whether or not it currently holds a
            // record — Manufacturing, Components and the rate-card
            // library (its own routing key stays "BusinessRateCards";
            // its screen name is "Rate cards") carry no baseline seed
            // (only the five above do), so on a genuinely fresh root they
            // are the three that read "(0)" with "No records yet"
            // beneath, rather than being missing entirely.
            foreach (var library in new[] { "Manufacturing", "Components", "Rate cards" })
                Assert.Contains(headings, h => h == $"{library} (0)");

            var emptyLibraryTexts = librariesView.GetLogicalDescendants().OfType<TextBlock>()
                .Count(t => t.Text == "No records yet");
            Assert.Equal(3, emptyLibraryTexts);

            Assert.DoesNotContain(librariesView.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "No reference records are seeded.");
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>`WP 19.6A`: Add opens the new record right up — the Product Owner guard (`po-comments.md` item 5), exercised through the real Add-a-material form.</summary>
    [AvaloniaFact]
    public async Task AddingAMaterial_OpensItInTheDetailPaneRightUp()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single().SelectNode("Reference data");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<LibrariesView>().Any());
            LayOut(window);

            var librariesView = window.GetLogicalDescendants().OfType<LibrariesView>().Single();
            LayOut(window);

            var textBoxes = librariesView.GetLogicalDescendants().OfType<TextBox>().ToList();
            textBoxes.First(t => t.Watermark == "Name").Text = "Detail Pane Alloy";
            textBoxes.First(t => t.Watermark == "Designation").Text = "mat-detail-pane";
            textBoxes.First(t => t.Watermark == "Yield strength (MPa)").Text = "250";
            textBoxes.First(t => t.Watermark == "Density (g/cm3)").Text = "2.7";
            textBoxes.First(t => t.Watermark == "Source organisation").Text = "Test Handbook Publisher";
            textBoxes.First(t => t.Watermark == "Source document").Text = "Test Handbook";

            var addButton = librariesView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Add Material"));
            addButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () =>
                librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().FirstOrDefault() is { } detail
                && detail.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("Detail Pane Alloy", StringComparison.Ordinal)));

            var detailView = librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().First();
            Assert.Contains(
                detailView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("Detail Pane Alloy", StringComparison.Ordinal));
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>`WP 19.6A`: Revise opens the new revision right up, through the real dialog — the same guard as Add.</summary>
    [AvaloniaFact]
    public async Task RevisingARecord_OpensTheNewRevisionInTheDetailPaneRightUp()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            const string recordId = "mat-lib-detail-revise";
            var definition = new Tempest.Core.Materials.MaterialDefinition { Name = "Original Alloy", Family = Tempest.Core.Materials.MaterialFamily.Aluminium, Designation = recordId };
            var provenance = new Tempest.Core.ReferenceData.ReferenceProvenance(SourceOrganisation: "Test Handbook Publisher", SourceDocument: "Test Handbook");
            await host.Materials!.RegisterAsync(recordId, definition, provenance);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single().SelectNode("Reference data");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<LibrariesView>().Any());
            LayOut(window);

            var librariesView = window.GetLogicalDescendants().OfType<LibrariesView>().Single();
            LayOut(window);

            var recordRow = librariesView.GetLogicalDescendants().OfType<Grid>()
                .First(g => g.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains(recordId, StringComparison.Ordinal)));
            var reviseButton = recordRow.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, "Revise"));
            reviseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var reviseEntry = GetPrivateField<ReviseReferenceRecordEntry>(window, "_reviseReferenceRecordEntry");
            await RenderUntilAsync(window, () => reviseEntry.IsVisible);

            var jsonBox = reviseEntry.GetLogicalDescendants().OfType<TextBox>().First();
            jsonBox.Text = jsonBox.Text!.Replace("Original Alloy", "Detail Pane Revised Alloy");

            var reviseConfirm = reviseEntry.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Revise"));
            reviseConfirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !reviseEntry.IsVisible);

            await RenderUntilAsync(window, () =>
                librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().FirstOrDefault() is { } detail
                && detail.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("Detail Pane Revised Alloy", StringComparison.Ordinal)));

            var detailView = librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().First();
            Assert.Contains(
                detailView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("Detail Pane Revised Alloy", StringComparison.Ordinal));
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

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
            window.Measure(new Avalonia.Size(1900, 1050));
            window.Arrange(new Avalonia.Rect(0, 0, 1900, 1050));
        }
    }
}
