using System.Text.RegularExpressions;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Workspace.Engineering;
using Tempest.Workspace.Shell;
using Tempest.Core.Calculations;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Engineering Calculations workspace as a product: what exists, how it
/// is selected, how it is opened, and what it says about itself.
/// </summary>
/// <remarks>
/// Companion to <see cref="EngineeringCalculationJourneyTests"/>, which
/// drives the calculation itself. This file covers the surrounding product
/// surface — the calculation list, the registered catalogue, opening a
/// persisted record read-only, and the verification panel — and applies the
/// same rule: <b>every control is checked visible, enabled and laid out at a
/// real size before it is used</b>, because a synthetic routed event reaches
/// a hidden control just as happily as a visible one.
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class EngineeringCalculationWorkspaceTests
{
    private const string EngineerId = "workspace-test-engineer";
    private const string RailEntry = "Engineering Calculations";

    // `WP 17.0A`. The `v0.16.0` build set Grid.Column on the StackPanels
    // inside the two ScrollViewers rather than on the viewers themselves,
    // so both columns rendered in column 0, one over the other, and every
    // "is it visible at non-zero size" assertion passed. This asserts
    // placement: the right column starts where the left column's declared
    // width ends, and neither is drawn over the other.
    [AvaloniaFact]
    public async Task TheTwoColumns_SitSideBySide_NeitherDrawnOverTheOther()
    {
        await InWorkspaceAsync(async (host, window, view) =>
        {
            LayOut(window);

            var columns = view.GetLogicalDescendants().OfType<ScrollViewer>()
                .Where(s => AutomationProperties.GetName(s) is EngineeringCalculationView.LeftColumnAutomationName
                    or EngineeringCalculationView.RightColumnAutomationName)
                .ToList();
            Assert.Equal(2, columns.Count);

            var left = columns.Single(c => AutomationProperties.GetName(c) == EngineeringCalculationView.LeftColumnAutomationName);
            var right = columns.Single(c => AutomationProperties.GetName(c) == EngineeringCalculationView.RightColumnAutomationName);

            DesktopTestHelpers.AssertPlaced(left, "the left column");
            DesktopTestHelpers.AssertPlaced(right, "the right column");
            Assert.True(
                right.Bounds.X >= left.Bounds.Right - 0.5,
                $"The right column starts at x={right.Bounds.X} but the left column ends at x={left.Bounds.Right}.");

            // Every section header is placed beside its neighbours, not on them.
            foreach (var expander in view.GetLogicalDescendants().OfType<Expander>())
                DesktopTestHelpers.AssertPlaced(expander, $"the '{expander.Header}' section");

            await Task.CompletedTask;
        });
    }

    [AvaloniaFact]
    public async Task TheWorkspaceOpensWithWhatExists_NotAnEmptyFeaturelessScreen()
    {
        await InWorkspaceAsync(async (host, window, view) =>
        {
            // Every section a first-time engineer needs is on screen at a
            // real size before they have done anything at all.
            AssertRendered(window, view, "Calculations");
            AssertRendered(window, view, "Reference Library");
            AssertRendered(window, view, "Inputs");
            AssertRendered(window, view, "Results");
            AssertRendered(window, view, "Traceability");
            AssertRendered(window, view, "Verification");

            // The New Calculation action is the first thing offered.
            AssertRendered(window, view, EngineeringCalculationView.NewCalculationCaption);

            // NOT "the list is empty": this test process references
            // Tempest.Samples, whose calculation sample module executes six
            // calculations while initialising, and the shipped Desktop does
            // not. Asserting emptiness here would assert the harness. What
            // IS true of both compositions is that the list shows whatever
            // the engine has actually recorded — which is the claim that
            // matters, and which was impossible before this build gave the
            // engine an index.
            Assert.All(view.Calculations, c => Assert.NotEqual(Guid.Empty, c.RecordId));

            // The catalogue is the product's real registered set.
            Assert.Equal(ProductCalculationCatalogue.CalculationIds.Count + 1, view.Catalogue.Count);
            Assert.Contains(view.Catalogue, c => c.CalculationId == BracketSectionCheckCalculationDefinition.Id && c.IsDrivableHere);

            // And the ones this surface cannot drive say so rather than
            // pretending to be ready — the `TD-159` shape.
            Assert.All(
                view.Catalogue.Where(c => c.CalculationId != BracketSectionCheckCalculationDefinition.Id),
                c =>
                {
                    Assert.False(c.IsDrivableHere);
                    Assert.NotNull(c.WhyNotDrivable);
                    Assert.Contains("not available here yet", c.Label, StringComparison.Ordinal);
                });

            await Task.CompletedTask;
        });
    }

    [AvaloniaFact]
    public async Task ACalculationAppearsInTheList_AndOpeningItShowsThePersistedRecordReadOnly()
    {
        await InWorkspaceAsync(async (host, window, view) =>
        {
            var executed = await RunTheKnownCheckAsync(window, view);
            view = SurfaceOf(window);

            // --- 3. It is in the list, identifiable without opening it ---
            var listed = view.Calculations.Single(c => c.RecordId == executed.CalculationRecordId);

            // Newest first, so the calculation just run is at the top.
            Assert.Equal(executed.CalculationRecordId, view.Calculations[0].RecordId);

            Assert.Equal(executed.CalculationRecordId, listed.RecordId);
            Assert.Equal(BracketSectionCheckCalculationDefinition.Id, listed.CalculationId);
            Assert.Equal("Meets criteria", listed.Outcome);
            Assert.True(listed.MeetsCriteria);
            Assert.Equal(MaterialSeed.Aluminium6082T6, listed.MaterialRecordId);
            Assert.Equal(executed.PinnedRevision, listed.PinnedRevision);
            Assert.Contains("200 MPa", listed.ResultSummary, StringComparison.Ordinal);

            AssertRendered(window, view, listed.Label);

            // --- 4. Select it, then Open it -------------------------------
            var list = CalculationListOf(view);
            AssertUsable(window, list, "the calculations list");

            list.SelectedItem = listed;
            Assert.Equal(listed.RecordId, view.SelectedCalculation!.RecordId);

            // Open becomes enabled only once something is selected.
            var open = ButtonOf(view, EngineeringCalculationView.OpenCaption);
            Assert.True(open.IsEnabled, "Open should be enabled once a calculation is selected.");

            await ClickAsync(window, view, EngineeringCalculationView.OpenCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).IsReadOnly);

            view = SurfaceOf(window);
            var opened = view.DisplayedOutcome!;

            // It shows the persisted record, and did not recalculate.
            Assert.Equal(executed.CalculationRecordId, opened.CalculationRecordId);
            Assert.Equal(executed.ExecutedAt, opened.ExecutedAt);
            Assert.Equal("200 MPa", opened.AppliedStress);
            Assert.Equal("0.3", opened.StressMargin);
            Assert.Contains("read-only", view.StatusMessage, StringComparison.OrdinalIgnoreCase);

            // --- Viewing is read-only: the inputs and Calculate are off ---
            Assert.True(view.IsReadOnly);
            Assert.False(ButtonOf(view, EngineeringCalculationView.CalculateCaption).IsEnabled);
            Assert.False(TextBoxOf(view, "Axial load in kilonewtons").IsEnabled);

            // Opening it produced no second record: viewing never writes.
            Assert.Single(view.Calculations, c => c.RecordId == executed.CalculationRecordId);

            // --- 5. New Calculation leaves read-only ----------------------
            await ClickAsync(window, view, EngineeringCalculationView.NewCalculationCaption);
            await RenderUntilAsync(window, () => !SurfaceOf(window).IsReadOnly);

            view = SurfaceOf(window);
            Assert.False(view.IsReadOnly);
            Assert.True(ButtonOf(view, EngineeringCalculationView.CalculateCaption).IsEnabled);
            Assert.True(TextBoxOf(view, "Axial load in kilonewtons").IsEnabled);
        });
    }

    [AvaloniaFact]
    public async Task TheVerificationPanel_ReportsTheAbsentArtefactHonestly_RatherThanFabricatingOne()
    {
        await InWorkspaceAsync(async (host, window, view) =>
        {
            await RunTheKnownCheckAsync(window, view);
            view = SurfaceOf(window);

            var evidence = view.DisplayedVerification;
            Assert.NotNull(evidence);

            // The shipped verification artefact is built only when a real
            // requirement exists to verify against — the model refuses one
            // that names none. The surface says so; it does not invent it.
            Assert.False(evidence!.Exists);
            Assert.NotNull(evidence.WhyAbsent);
            Assert.Contains("requirement", evidence.WhyAbsent!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("TD-165", evidence.WhyAbsent!, StringComparison.Ordinal);

            AssertRendered(window, view, "No verification artefact is held");
            await Task.CompletedTask;
        });
    }

    [Fact]
    public void NoCalculationBusinessLogic_LivesInTheDesktopLayer()
    {
        // `TD-159`'s sibling risk: the cheapest way to make a surface work
        // is to recompute in the view. This reads the two Desktop files that
        // make up the surface and fails if either grows arithmetic of its
        // own, or reaches for a formula's inputs directly.
        foreach (var path in new[]
                 {
                     "src/Tempest.Desktop/Views/EngineeringCalculationView.cs",
                     "src/Tempest.Desktop/Composition/EngineeringCalculationCoordinator.cs",
                 })
        {
            var source = File.ReadAllText(Path.Combine(RepositoryRoot(), path));

            Assert.DoesNotContain("Quantity<", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ConvertTo(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("BracketSectionCheck", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ICalculationEngine", source, StringComparison.Ordinal);
            Assert.DoesNotContain("IMaterialCatalog", source, StringComparison.Ordinal);

            // No arithmetic on engineering values: no division, and no
            // multiplication outside the layout constants a view legitimately
            // computes with.
            Assert.DoesNotMatch(new Regex(@"\b(stress|margin|mass|area|load)\w*\s*[*/]", RegexOptions.IgnoreCase), source);
        }
    }

    // ---- the journey these tests need a result from ----

    private static async Task<BracketCalculationOutcome> RunTheKnownCheckAsync(MainWindow window, EngineeringCalculationView view)
    {
        await ClickAsync(window, view, EngineeringCalculationView.PopulateCaption);
        await RenderUntilAsync(window, () => SurfaceOf(window).Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6));

        view = SurfaceOf(window);
        PickerOf(view).SelectedItem = view.Materials.Single(m => m.RecordId == MaterialSeed.Aluminium6082T6);
        TextBoxOf(view, "Source consulted").Text = "Aalco 6082-T6 extrusions datasheet";
        TextBoxOf(view, "Release rationale").Text = "Required for the bracket section check.";

        await ClickAsync(window, view, EngineeringCalculationView.ReleaseCaption);
        await RenderUntilAsync(window, () =>
            SurfaceOf(window).Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6 && m.IsUsableForEngineering));

        view = SurfaceOf(window);
        TextBoxOf(view, "Axial load in kilonewtons").Text = "12";
        TextBoxOf(view, "Section area in square millimetres").Text = "60";
        TextBoxOf(view, "Member length in millimetres").Text = "150";
        TextBoxOf(view, "Mass limit in grams").Text = "50";

        await ClickAsync(window, view, EngineeringCalculationView.CalculateCaption);
        await RenderUntilAsync(window, () => SurfaceOf(window).DisplayedOutcome is { Performed: true });

        return SurfaceOf(window).DisplayedOutcome!;
    }

    // ---- harness ----

    private static async Task InWorkspaceAsync(Func<WorkspaceHost, MainWindow, EngineeringCalculationView, Task> body)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var principals = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
            ((CurrentPrincipalAccessor)principals).SetCurrent(new PlatformPrincipal(new PlatformIdentity(EngineerId, EngineerId), []));

            var window = new MainWindow(host) { Width = 1600, Height = 1000 };
            window.Show();

            await OpenFromTheRailAsync(host, window);
            await body(host, window, SurfaceOf(window));
            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    private static async Task OpenFromTheRailAsync(WorkspaceHost host, MainWindow window)
    {
        LayOut(window);

        var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Distinct().Single();
        var entry = rail.GetLogicalDescendants().OfType<Button>().Distinct()
            .Single(b => string.Equals(AutomationProperties.GetName(b), RailEntry, StringComparison.Ordinal));

        AssertUsable(window, entry, $"the '{RailEntry}' rail entry");
        entry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var deadline = DesktopTestHelpers.Deadline(5);
        while (host.ShellNavigator!.Current.Area != ShellArea.EngineeringCalculation && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(ShellArea.EngineeringCalculation, host.ShellNavigator!.Current.Area);
        await window.RenderCurrentModuleAsync();
        LayOut(window);
    }

    private static EngineeringCalculationView SurfaceOf(MainWindow window) =>
        window.GetLogicalDescendants().OfType<EngineeringCalculationView>().Distinct().Single();

    private static ListBox PickerOf(EngineeringCalculationView view) =>
        view.GetLogicalDescendants().OfType<ListBox>().Distinct()
            .Single(l => string.Equals(AutomationProperties.GetName(l), "Reference library", StringComparison.Ordinal));

    private static ListBox CalculationListOf(EngineeringCalculationView view) =>
        view.GetLogicalDescendants().OfType<ListBox>().Distinct()
            .Single(l => string.Equals(AutomationProperties.GetName(l), "Existing calculations", StringComparison.Ordinal));

    private static Button ButtonOf(EngineeringCalculationView view, string caption) =>
        view.GetLogicalDescendants().OfType<Button>().Distinct()
            .Single(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));

    private static TextBox TextBoxOf(EngineeringCalculationView view, string automationName) =>
        view.GetLogicalDescendants().OfType<TextBox>().Distinct()
            .Single(b => string.Equals(AutomationProperties.GetName(b), automationName, StringComparison.Ordinal));

    /// <summary>Asserts a control is something a person could actually use.</summary>
    private static void AssertUsable(MainWindow window, Control control, string what)
    {
        LayOut(window);
        Assert.True(control.IsVisible, $"{what} exists but IsVisible is false.");
        Assert.True(control.IsEnabled, $"{what} is visible but disabled.");
        Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0, $"{what} rendered at {control.Bounds}.");
    }

    private static async Task ClickAsync(MainWindow window, Control surface, string caption)
    {
        LayOut(window);

        var button = surface.GetLogicalDescendants().OfType<Button>().Distinct()
            .FirstOrDefault(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));

        Assert.True(button is not null, $"No '{caption}' button. Present: {string.Join(", ", surface.GetLogicalDescendants().OfType<Button>().Select(b => b.Content?.ToString()))}");
        AssertUsable(window, button!, $"the '{caption}' button");

        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DesktopTestHelpers.Deadline(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Avalonia.Size(1600, 1000));
            window.Arrange(new Avalonia.Rect(0, 0, 1600, 1000));
        }
    }

    private static void AssertRendered(MainWindow window, Control surface, string fragment)
    {
        LayOut(window);

        var onScreen = surface.GetLogicalDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => (t.Text ?? string.Empty).Contains(fragment, StringComparison.Ordinal) && t.Bounds.Width > 0 && t.Bounds.Height > 0);

        if (onScreen is not null)
            return;

        // Section titles live on an Expander header rather than a TextBlock.
        var header = surface.GetLogicalDescendants().OfType<Expander>().Distinct()
            .FirstOrDefault(e => string.Equals(e.Header?.ToString(), fragment, StringComparison.Ordinal) && e.Bounds.Width > 0 && e.Bounds.Height > 0);

        if (header is not null)
            return;

        var button = surface.GetLogicalDescendants().OfType<Button>().Distinct()
            .FirstOrDefault(b => string.Equals(b.Content?.ToString(), fragment, StringComparison.Ordinal) && b.Bounds.Width > 0 && b.Bounds.Height > 0);

        Assert.True(button is not null, $"'{fragment}' is not on screen at a real size.");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root from the test output directory.");
        return directory!.FullName;
    }
}
