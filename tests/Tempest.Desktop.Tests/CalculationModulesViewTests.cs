using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Engineering Calculators (`WP 21.7B`) as a product: every product
/// calculation listed by category, a form generated from the chosen
/// calculation's own descriptor with unit pickers, a released material
/// filling the material inputs, the result with its working, a refusal
/// shown as the outcome, and a typing mistake named on the form. Every
/// control is checked visible and laid out at a real size before it is
/// used, as the sibling Engineering Calculations tests do.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CalculationModulesViewTests
{
    private const string EngineerId = "calculators-test-engineer";
    private const string EngineeringRailEntry = "Engineering";
    private const string NodeName = "Calculators";

    [AvaloniaFact]
    public async Task TheCatalogue_ListsEveryProductCalculationByCategory_AndTheColumnsSitSideBySide()
    {
        await InCalculatorsAsync(async (host, window, view) =>
        {
            LayOut(window);

            Assert.Equal(ProductCalculationCatalogue.CalculationIds.Count, view.Catalogue.Sum(g => g.Modules.Count));
            Assert.Equal(16, view.Catalogue.Sum(g => g.Modules.Count));

            var columns = view.GetLogicalDescendants().OfType<ScrollViewer>()
                .Where(s => AutomationProperties.GetName(s) is CalculationModulesView.LeftColumnAutomationName or CalculationModulesView.RightColumnAutomationName)
                .ToList();
            Assert.Equal(2, columns.Count);
            var left = columns.Single(c => AutomationProperties.GetName(c) == CalculationModulesView.LeftColumnAutomationName);
            var right = columns.Single(c => AutomationProperties.GetName(c) == CalculationModulesView.RightColumnAutomationName);
            DesktopTestHelpers.AssertPlaced(left, "the left column");
            DesktopTestHelpers.AssertPlaced(right, "the right column");
            Assert.True(right.Bounds.X >= left.Bounds.Right - 0.5, $"The right column starts at x={right.Bounds.X} but the left column ends at x={left.Bounds.Right}.");

            // Every category node and every calculation under it is on screen.
            var tree = view.GetLogicalDescendants().OfType<TreeView>().Single(t => AutomationProperties.GetName(t) == CalculationModulesView.CatalogueAutomationName);
            var categories = tree.Items.OfType<TreeViewItem>().ToList();
            Assert.Equal(view.Catalogue.Count, categories.Count);
            foreach (var category in categories)
            {
                DesktopTestHelpers.AssertPlaced(category, $"the '{category.Header}' category");
                Assert.NotEmpty(category.Items);
            }

            AssertRendered(window, view, CalculationModulesView.Heading);
            AssertRendered(window, view, "Catalogue");
            AssertRendered(window, view, "Material");
            AssertRendered(window, view, "Inputs");
            await Task.CompletedTask;
        });
    }

    [AvaloniaFact]
    public async Task ChoosingACalculation_GeneratesItsForm_WithUnitPickersAndTheMethodReference()
    {
        await InCalculatorsAsync(async (host, window, view) =>
        {
            view.SelectModule(BeamDeflectionCalculationDefinition.Id);
            LayOut(window);

            Assert.Equal(BeamDeflectionCalculationDefinition.Id, view.SelectedModule!.Id);

            var span = Assert.IsType<TextBox>(view.FieldControl("Span"));
            AssertUsable(window, span, "the span box");
            Assert.Equal("Span L", AutomationProperties.GetName(span));

            var unit = view.UnitPicker("Span")!;
            AssertUsable(window, unit, "the span unit picker");
            Assert.Equal("mm", unit.SelectedItem);
            Assert.Contains("in", unit.Items.OfType<string>());

            var support = Assert.IsType<ComboBox>(view.FieldControl("Support"));
            AssertUsable(window, support, "the support picker");
            Assert.Equal(nameof(BeamSupport.SimplySupported), support.SelectedItem);

            // A material-sourced input is filled, never typed.
            var modulus = Assert.IsType<TextBox>(view.FieldControl("YoungsModulus"));
            Assert.True(modulus.IsReadOnly);

            AssertRendered(window, view, "Roark");
            AssertRendered(window, view, "calc.beam-deflection.md");
            AssertRendered(window, view, CalculationModulesView.CalculateCaption);
            await Task.CompletedTask;
        });
    }

    [AvaloniaFact]
    public async Task TheBeamExample_RunsFromTheForm_OnAReleasedMaterial_AndShowsTheResultAndTheWorking()
    {
        await InCalculatorsAsync(async (host, window, view) =>
        {
            await ReleaseSeededSteelAsync(host, window);
            view = SurfaceOf(window);

            Assert.Contains(view.Materials, m => m.RecordId == MaterialSeed.S355J2);

            view.SelectModule(BeamDeflectionCalculationDefinition.Id);
            view.PickMaterial(MaterialSeed.S355J2);
            await RenderUntilAsync(window, () => (SurfaceOf(window).FieldControl("YoungsModulus") as TextBox)?.Text == "210");
            view = SurfaceOf(window);

            // The seeded S355J2 record: 210 GPa, yield 355 MPa, in the form's own units.
            Assert.Equal("210", ((TextBox)view.FieldControl("YoungsModulus")!).Text);
            Assert.Equal("355", ((TextBox)view.FieldControl("AllowableBendingStress")!).Text);

            view.SetField("Load", "10", "kN");
            view.SetField("Span", "2000", "mm");
            view.SetField("SecondMomentOfArea", "2000000", "mm^4");
            view.SetField("ExtremeFibreDistance", "50", "mm");
            view.SetField("DeflectionLimit", "8", "mm");

            await ClickCalculateAsync(window, view);
            await RenderUntilAsync(window, () => SurfaceOf(window).LastRun is not null);
            view = SurfaceOf(window);

            var run = view.LastRun!;
            Assert.False(run.IsRefused);
            Assert.Equal("Meets its criteria", run.OutcomeSummary);
            Assert.Contains(MaterialSeed.S355J2, run.ReferencedMaterialIds);

            AssertRendered(window, view, "Meets its criteria");
            AssertRendered(window, view, "3.96825 mm");
            AssertRendered(window, view, "125 MPa");
            AssertRendered(window, view, "Maximum bending moment");
            AssertRendered(window, view, MaterialSeed.S355J2);
            AssertRendered(window, view, "Results");
            AssertRendered(window, view, "Working");
        });
    }

    [AvaloniaFact]
    public async Task ARefusal_IsShownAsTheOutcome_NotAsAnError()
    {
        await InCalculatorsAsync(async (host, window, view) =>
        {
            await ReleaseSeededSteelAsync(host, window);
            view = SurfaceOf(window);

            view.SelectModule(BeamDeflectionCalculationDefinition.Id);
            view.PickMaterial(MaterialSeed.S355J2);
            await RenderUntilAsync(window, () => (SurfaceOf(window).FieldControl("YoungsModulus") as TextBox)?.Text == "210");
            view = SurfaceOf(window);

            view.SetField("Load", "10", "kN");
            view.SetField("Span", "200", "mm");
            view.SetField("SecondMomentOfArea", "2000000", "mm^4");
            view.SetField("ExtremeFibreDistance", "50", "mm");
            view.SetField("DeflectionLimit", "8", "mm");

            await ClickCalculateAsync(window, view);
            await RenderUntilAsync(window, () => SurfaceOf(window).LastRun is not null);
            view = SurfaceOf(window);

            Assert.True(view.LastRun!.IsRefused);
            Assert.Contains("span-to-depth", view.LastRun.RefusalReason, StringComparison.Ordinal);
            AssertRendered(window, view, "refused");
            AssertRendered(window, view, "span-to-depth");
        });
    }

    [AvaloniaFact]
    public async Task ATypingMistake_IsNamedOnTheForm_AndNothingIsRecorded()
    {
        await InCalculatorsAsync(async (host, window, view) =>
        {
            // Bolt shear takes no material: the form runs with none picked.
            view.SelectModule(BoltShearCapacityCalculationDefinition.Id);
            LayOut(window);
            view.SetField("Diameter", "abc", "mm");
            view.SetField("UltimateShearStrength", "400", "MPa");
            view.SetField("ShearPlanes", "2");
            view.SetField("SafetyFactor", "1.5");

            await ClickCalculateAsync(window, view);
            await RenderUntilAsync(window, () => SurfaceOf(window).GetLogicalDescendants().OfType<TextBlock>()
                .Any(t => AutomationProperties.GetName(t) == "Input problems" && (t.Text ?? string.Empty).Contains("'abc'", StringComparison.Ordinal)));
            view = SurfaceOf(window);

            Assert.Null(view.LastRun);
            AssertRendered(window, view, "Bolt diameter: 'abc' is not a number");

            // Corrected, the original definition runs from the same form.
            view.SetField("Diameter", "20", "mm");
            await ClickCalculateAsync(window, view);
            await RenderUntilAsync(window, () => SurfaceOf(window).LastRun is not null);
            view = SurfaceOf(window);

            Assert.Equal("Computed", view.LastRun!.OutcomeSummary);
            AssertRendered(window, view, "167552 N");
        });
    }

    // ---- Helpers ----

    private static async Task ReleaseSeededSteelAsync(WorkspaceHost host, MainWindow window)
    {
        await host.BracketCalculations!.PopulateMaterialLibraryAsync();
        await host.BracketCalculations!.VerifyAndReleaseAsync(MaterialSeed.S355J2, "Siderticino datasheet, mechanical properties table", "Needed for the calculator test.");

        // Re-enter the node so the picker re-reads the released materials.
        var area = window.GetLogicalDescendants().OfType<EngineeringAreaView>().Distinct().Single();
        area.SelectNode("Engineering Calculations");
        await RenderUntilAsync(window, () => !window.GetLogicalDescendants().OfType<CalculationModulesView>().Any(v => v.IsVisible && v.Bounds.Width > 0));
        area.SelectNode(NodeName);
        await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<CalculationModulesView>().Any(v => v.Materials.Count > 0));
    }

    private static async Task ClickCalculateAsync(MainWindow window, CalculationModulesView view)
    {
        LayOut(window);
        var button = view.GetLogicalDescendants().OfType<Button>().Distinct()
            .Single(b => AutomationProperties.GetName(b) == CalculationModulesView.CalculateCaption);
        AssertUsable(window, button, "the Calculate button");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
    }

    private static async Task InCalculatorsAsync(Func<WorkspaceHost, MainWindow, CalculationModulesView, Task> body)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var principals = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
            ((CurrentPrincipalAccessor)principals).SetCurrent(new PlatformPrincipal(new PlatformIdentity(EngineerId, EngineerId), ApplicationPermissions.LocalSession));

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
            .Single(b => string.Equals(AutomationProperties.GetName(b), EngineeringRailEntry, StringComparison.Ordinal));

        AssertUsable(window, entry, $"the '{EngineeringRailEntry}' rail entry");
        entry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var deadline = DesktopTestHelpers.Deadline(5);
        while (host.ShellNavigator!.Current.Area != ShellArea.EngineeringDepartment && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(ShellArea.EngineeringDepartment, host.ShellNavigator!.Current.Area);
        await window.RenderCurrentModuleAsync();
        LayOut(window);

        window.GetLogicalDescendants().OfType<EngineeringAreaView>().Distinct().Single().SelectNode(NodeName);
        await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<CalculationModulesView>().Any(v => v.Catalogue.Count > 0));
    }

    private static CalculationModulesView SurfaceOf(MainWindow window) =>
        window.GetLogicalDescendants().OfType<CalculationModulesView>().Distinct().Single();

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DesktopTestHelpers.Deadline(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            LayOut(window);
        }

        Assert.True(condition(), "The surface did not reach the expected state in time.");

        // Whatever the condition observed, the surface is laid out before
        // the caller asserts placement on it.
        LayOut(window);
    }

    private static void AssertUsable(MainWindow window, Control control, string what)
    {
        LayOut(window);
        Assert.True(control.IsVisible, $"{what} exists but IsVisible is false.");
        Assert.True(control.IsEnabled, $"{what} is visible but disabled.");
        Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0, $"{what} rendered at {control.Bounds}.");
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

        var header = surface.GetLogicalDescendants().OfType<Expander>().Distinct()
            .FirstOrDefault(e => string.Equals(e.Header?.ToString(), fragment, StringComparison.Ordinal) && e.Bounds.Width > 0 && e.Bounds.Height > 0);
        if (header is not null)
            return;

        var button = surface.GetLogicalDescendants().OfType<Button>().Distinct()
            .FirstOrDefault(b => string.Equals(b.Content?.ToString(), fragment, StringComparison.Ordinal) && b.Bounds.Width > 0 && b.Bounds.Height > 0);

        Assert.True(button is not null, $"Nothing on screen shows '{fragment}'.");
    }
}
