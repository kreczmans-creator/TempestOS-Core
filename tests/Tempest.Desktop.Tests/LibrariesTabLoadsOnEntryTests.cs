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
            // record — Manufacturing, Components, the rate-card library
            // (its own routing key stays "BusinessRateCards"; its screen
            // name is "Rate cards") carry no baseline seed (only the five
            // above do), so on a genuinely fresh root they are the three
            // that read "(0)" with "No records yet" beneath, rather than
            // being missing entirely. People moved to Business → Staff
            // (Product Owner runbook B1) and is not listed here at all.
            foreach (var library in new[] { "Manufacturing", "Components", "Rate cards" })
                Assert.Contains(headings, h => h == $"{library} (0)");
            Assert.DoesNotContain(headings, h => h.StartsWith("People (", StringComparison.Ordinal));

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

    /// <summary>
    /// Product Owner runbook F1 (2026-10-01): a library lists one compact
    /// row per record — its title and release status, nothing else — under
    /// collapsible family groups whose headings carry counts; a group's
    /// collapsed state survives a refresh for the rest of the session.
    /// </summary>
    [AvaloniaFact]
    public async Task Libraries_ListTitleAndStatusRows_UnderCollapsibleFamilyGroups_RememberedAcrossRefresh()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            const string recordId = "mat-f1-grouping";
            await host.Materials!.RegisterAsync(
                recordId,
                new Tempest.Core.Materials.MaterialDefinition { Name = "Runbook F1 Stainless", Family = Tempest.Core.Materials.MaterialFamily.StainlessSteel, Designation = recordId },
                new Tempest.Core.ReferenceData.ReferenceProvenance(SourceOrganisation: "Test Handbook Publisher", SourceDocument: "Test Handbook"));

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single().SelectNode("Reference data");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<LibrariesView>().Any());
            LayOut(window);

            var librariesView = window.GetLogicalDescendants().OfType<LibrariesView>().Single();

            // The row: title and status badge only — no id, revision or
            // source citation in the list.
            var openButton = librariesView.GetLogicalDescendants().OfType<Button>()
                .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == $"Open {recordId}");
            var row = (Grid)openButton.Parent!;
            var rowTexts = row.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).Where(t => t.Length > 0).ToList();
            Assert.Contains("Runbook F1 Stainless", rowTexts);
            Assert.Contains("Draft", rowTexts);
            Assert.DoesNotContain(rowTexts, t => t.Contains(recordId, StringComparison.Ordinal) || t.Contains("rev ", StringComparison.Ordinal) || t.Contains("source citation", StringComparison.Ordinal));
            Assert.DoesNotContain(row.GetLogicalDescendants().OfType<Button>(), b => Equals(b.Content, "Verify") || Equals(b.Content, "Revise"));

            // Its family group, with a count, collapses and expands.
            var group = librariesView.GetLogicalDescendants().OfType<CollapsibleSection>().Single(s => s.Title == "Stainless steels");
            Assert.Contains(group.Header.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? string.Empty).StartsWith("Stainless steels (", StringComparison.Ordinal));
            Assert.Equal("Stainless steels group", Avalonia.Automation.AutomationProperties.GetName(group.Header));

            var before = group.IsExpanded;
            group.Header.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(!before, group.IsExpanded);
            LayOut(window);
            Assert.Equal(!before, row.IsEffectivelyVisible);

            // Remembered across a refresh.
            await librariesView.RefreshAsync();
            LayOut(window);
            var rebuilt = librariesView.GetLogicalDescendants().OfType<CollapsibleSection>().Single(s => s.Title == "Stainless steels");
            Assert.Equal(!before, rebuilt.IsExpanded);

            // The library heading itself collapses too.
            var library = librariesView.GetLogicalDescendants().OfType<CollapsibleSection>().Single(s => s.Title == "Materials");
            Assert.Equal("Materials library", Avalonia.Automation.AutomationProperties.GetName(library.Header));
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

            // Runbook F1: a row is title and status only — Revise lives on
            // the open record.
            var openButton = librariesView.GetLogicalDescendants().OfType<Button>()
                .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == $"Open {recordId}");
            openButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () =>
                librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().FirstOrDefault() is { } opened
                && opened.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains(recordId, StringComparison.Ordinal)));
            var reviseButton = librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().First()
                .GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, "Revise"));
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

            // A content revision is revision 2 (runbook B2).
            Assert.Contains(
                detailView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("rev 2", StringComparison.Ordinal));
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
