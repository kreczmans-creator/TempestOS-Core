using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Evidence;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.6A` acceptance — through the real window: opening a governed
/// reference record (`fst-m10-coarse`, `po-comments.md` item 5) shows its
/// definition's fields with their units, revision history, source
/// citation and "cited by"; Verify and Release move it through the same
/// governed acts <see cref="LibrariesView"/>'s own rows already call; an
/// Evidence record citing it, once Released, appears under Cited by and
/// opens right up.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ReferenceRecordViewTests
{
    [AvaloniaFact]
    public async Task OpeningFstM10Coarse_ShowsDefinitionHistoryAndCitation_VerifyReleaseAndCitedByAllWork()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;
            await navigator.GoToModuleAsync(ShellArea.Evidence);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var evidenceWorkspace = GetPrivateField<EvidenceWorkspaceView>(window, "_evidenceWorkspace");
            var tabs = (TabControl)evidenceWorkspace.Content!;
            tabs.SelectedIndex = 1;
            var librariesView = (LibrariesView)((TabItem)tabs.Items[1]!).Content!;
            LayOut(window);

            var recordRow = librariesView.GetLogicalDescendants().OfType<Grid>()
                .First(g => g.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).StartsWith("fst-m10-coarse", StringComparison.Ordinal)));
            var openButton = recordRow.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, "Open"));
            openButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () =>
                librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().FirstOrDefault() is { } d
                && d.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("fst-m10-coarse", StringComparison.Ordinal)));

            var detail = librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().First();
            var text = DetailText(detail);

            // Identity.
            Assert.Contains(text, t => t.Contains("fst-m10-coarse", StringComparison.Ordinal));
            Assert.Contains(text, t => t.Contains("Draft", StringComparison.Ordinal));

            // The definition's own fields, with units — Family at the top
            // level, NominalDiameter and Pitch nested inside Thread.
            Assert.Contains(text, t => t.Contains("Bolt", StringComparison.Ordinal));
            Assert.Contains(text, t => t == "10 mm");
            Assert.Contains(text, t => t == "1.5 mm");
            Assert.Contains(text, t => t.Contains("Hexagon", StringComparison.Ordinal));
            Assert.Contains(text, t => t.Contains("ISO 262", StringComparison.Ordinal));

            // Revision history: one revision so far, marked current.
            Assert.Contains(text, t => t.StartsWith("Rev 1 (current)", StringComparison.Ordinal) && t.Contains("Draft", StringComparison.Ordinal));

            // Source citation, exactly as `FastenerSeed.cs` records it.
            Assert.Contains(text, t => t == "Wikimedia Foundation");
            Assert.Contains(text, t => t.Contains("Wikipedia", StringComparison.Ordinal));
            Assert.Contains(text, t => t == "Selected sizes table");
            Assert.Contains(text, t => t == "M10");

            // Not yet cited.
            Assert.Contains(text, t => t.Contains("Not cited by any evidence", StringComparison.Ordinal));

            // Verify moves Draft to Checked.
            var verifyButton = detail.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Verify"));
            verifyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => DetailText(detail).Any(t => t.Contains("Checked", StringComparison.Ordinal)));
            Assert.Contains(DetailText(detail), t => t.Contains("Checked", StringComparison.Ordinal));

            // Release, so Evidence may cite it (`ADR-0148`).
            var releaseButton = detail.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Release"));
            releaseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => DetailText(detail).Any(t => t.Contains("Released", StringComparison.Ordinal)));

            // An Evidence record citing it appears under Cited by, and
            // opens right up.
            var project = await host.ProjectDirectory!.CreateAsync("P-REFVIEW", "Reference Record View Test");
            var evidence = await host.EvidenceService!.CreateAsync(project.Id, "Bolt selection note", EvidenceClassification.Calculation);
            var citeResult = await host.EvidenceService!.CiteAsync(evidence.Id, "Fasteners", "fst-m10-coarse");
            Assert.True(citeResult.Succeeded, citeResult.Reason);

            // Reopen the record — a second real Open click, the same
            // action a user takes to see a change made elsewhere; this
            // view reacts to an explicit reload, not a background poll.
            openButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => DetailText(detail).Any(t => t.Contains("Bolt selection note", StringComparison.Ordinal)));

            Assert.Contains(DetailText(detail), t => t.Contains("Bolt selection note", StringComparison.Ordinal) && t.Contains(project.Label, StringComparison.Ordinal));

            var citedByOpenButton = detail.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Open"));
            citedByOpenButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            ObjectEditorView? evidenceEditor = null;
            await RenderUntilAsync(window, () =>
            {
                evidenceEditor = window.GetLogicalDescendants().OfType<ObjectEditorView>()
                    .FirstOrDefault(e => e.GetLogicalDescendants().OfType<TextBox>().Any(t => t.Text == "Bolt selection note"));
                return evidenceEditor is not null;
            });
            Assert.NotNull(evidenceEditor);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Scope §2: an open record sits beside the list at a typical width,
    /// and in place of it, with a Back control, below
    /// <see cref="DesignTokens.CompactShellWidth"/> — the same threshold
    /// the rail, header and ribbon already fold at
    /// (`MainWindowComposer.Wire.cs`'s own single `window.SizeChanged`
    /// handler).
    /// </summary>
    [AvaloniaFact]
    public async Task OpeningARecord_SitsBesideTheListWhenWide_AndReplacesItWithBackWhenNarrow()
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
            var librariesView = (LibrariesView)((TabItem)tabs.Items[1]!).Content!;
            LayOut(window);

            var recordRow = librariesView.GetLogicalDescendants().OfType<Grid>()
                .First(g => g.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).StartsWith("fst-m10-coarse", StringComparison.Ordinal)));
            var openButton = recordRow.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, "Open"));
            openButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () =>
                librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().FirstOrDefault() is { } d
                && d.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("fst-m10-coarse", StringComparison.Ordinal)));

            // Wide (the window's own default in this suite, 1900px):
            // beside the list — both a row and the record are on screen,
            // no Back control.
            var listRows = librariesView.GetLogicalDescendants().OfType<Grid>()
                .Where(g => g.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).StartsWith("fst-m10-coarse", StringComparison.Ordinal)))
                .ToList();
            Assert.NotEmpty(listRows);
            Assert.DoesNotContain(
                librariesView.GetLogicalDescendants().OfType<Button>(),
                b => Equals(b.Content, "← Back to Libraries") && b.IsEffectivelyVisible);

            // Narrow: in place of the list, with Back.
            librariesView.SetCompact(true);
            LayOut(window);

            Assert.DoesNotContain(
                librariesView.GetLogicalDescendants().OfType<Grid>(),
                g => g.IsEffectivelyVisible && g.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).StartsWith("fst-m10-coarse", StringComparison.Ordinal)));
            var backButton = librariesView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "← Back to Libraries"));
            Assert.True(backButton.IsEffectivelyVisible);

            // Back returns to the list.
            backButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            LayOut(window);
            Assert.Contains(
                librariesView.GetLogicalDescendants().OfType<Grid>(),
                g => g.IsEffectivelyVisible && g.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).StartsWith("fst-m10-coarse", StringComparison.Ordinal)));
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    private static List<string> DetailText(ReferenceRecordView detail) =>
        detail.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();

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
