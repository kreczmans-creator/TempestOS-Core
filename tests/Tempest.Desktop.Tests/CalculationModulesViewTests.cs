using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Engineering Calculators (`WP 21.7B`, completed by `WP 21.7C`) as a
/// product: every product calculation listed by category, a form generated
/// from the chosen calculation's own descriptor with unit pickers, a
/// released record picked per reference input (the lug's two materials
/// each from their own picker, a bearing from its own library), the result
/// with its working, a refusal shown as the outcome, a typing mistake named
/// on the form, and the record's own Re-run and Compare commands with the
/// comparison as a table. Every control is checked visible and laid out at
/// a real size before it is used, as the sibling Engineering Calculations
/// tests do.
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
            AssertRendered(window, view, "Libraries");
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

            // The material picker is the reference input's own control,
            // and a material-sourced input is filled, never typed.
            var picker = Assert.IsType<ComboBox>(view.FieldControl("MaterialPin"));
            Assert.Same(picker, view.RecordPicker("MaterialPin"));
            Assert.Equal("Material record", AutomationProperties.GetName(picker));
            var modulus = Assert.IsType<TextBox>(view.FieldControl("YoungsModulus"));
            Assert.True(modulus.IsReadOnly);

            AssertRendered(window, view, "Roark");
            AssertRendered(window, view, "calc.beam-deflection.md");
            AssertRendered(window, view, CalculationModulesView.CalculateCaption);
            await Task.CompletedTask;
        });
    }

    [AvaloniaFact]
    public async Task TheBeamExample_RunsFromTheForm_OnAReleasedMaterial_AndShowsTheResultTheWorkingAndTheRecordsName()
    {
        await InCalculatorsAsync(async (host, window, view) =>
        {
            await ReleaseSeededSteelAsync(host, window);
            view = SurfaceOf(window);

            Assert.Contains(view.Released(ReferenceLibrary.Materials), m => m.RecordId == MaterialSeed.S355J2);

            view.SelectModule(BeamDeflectionCalculationDefinition.Id);
            view.PickRecord("MaterialPin", MaterialSeed.S355J2);
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

            await ClickAsync(window, view, CalculationModulesView.CalculateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).LastRun is not null);
            view = SurfaceOf(window);

            var run = view.LastRun!;
            Assert.False(run.IsRefused);
            Assert.Equal("Meets its criteria", run.OutcomeSummary);
            Assert.Contains(MaterialSeed.S355J2, run.ReferencedMaterialIds);

            // The run is named as a Calculation the register lists.
            var current = view.CurrentRun!;
            Assert.StartsWith("Beam", current.DisplayName, StringComparison.Ordinal);
            Assert.Equal(1, current.RunCount);
            var named = await host.BracketCalculations!.FindNamedAsync(run.RecordId);
            Assert.NotNull(named);
            Assert.Equal(current.CalculationObjectId, named!.ObjectId);

            AssertRendered(window, view, "Meets its criteria");
            AssertRendered(window, view, "3.96825 mm");
            AssertRendered(window, view, "125 MPa");
            AssertRendered(window, view, "Maximum bending moment");
            AssertRendered(window, view, MaterialSeed.S355J2);
            AssertRendered(window, view, "Results");
            AssertRendered(window, view, "Working");
            AssertRendered(window, view, current.DisplayName);
        });
    }

    [AvaloniaFact]
    public async Task TheLug_HasItsOwnPickerForEachMaterial_AndBothArePinnedAndCited()
    {
        await InCalculatorsAsync(async (host, window, view) =>
        {
            await ReleaseSeededSteelAsync(host, window);
            view = SurfaceOf(window);

            view.SelectModule(LiftingLugPinJointCalculationDefinition.Id);
            LayOut(window);

            var lugPicker = view.RecordPicker("LugMaterialPin")!;
            var pinPicker = view.RecordPicker("PinMaterialPin")!;
            Assert.NotSame(lugPicker, pinPicker);
            AssertUsable(window, lugPicker, "the lug material picker");
            AssertUsable(window, pinPicker, "the pin material picker");
            Assert.Equal("Lug material record", AutomationProperties.GetName(lugPicker));
            Assert.Equal("Pin material record", AutomationProperties.GetName(pinPicker));
            var lugTop = ((Control)lugPicker.Parent!.Parent!).Bounds.Y; // the form row that holds the picker
            var pinTop = ((Control)pinPicker.Parent!.Parent!).Bounds.Y;
            Assert.True(pinTop > lugTop, $"The pin picker (y={pinTop}) sits below the lug picker (y={lugTop}).");

            // The lug's allowables are the engineer's own derived values,
            // cited against the records: picking fills nothing, and the
            // status names the record each picker stood on.
            view.PickRecord("LugMaterialPin", MaterialSeed.S355J2);
            await RenderUntilAsync(window, () => StatusOf(window).Contains("Lug material record", StringComparison.Ordinal) && StatusOf(window).Contains(MaterialSeed.S355J2, StringComparison.Ordinal));
            Assert.Null(SurfaceOf(window).PickedRecord("PinMaterialPin"));

            view = SurfaceOf(window);
            view.PickRecord("PinMaterialPin", MaterialSeed.S355J2);
            await RenderUntilAsync(window, () => StatusOf(window).Contains("Pin material record", StringComparison.Ordinal));
            view = SurfaceOf(window);

            view.SetField("AllowableTensileStress", "150", "MPa");
            view.SetField("AllowableBearingStress", "200", "MPa");
            view.SetField("AllowableShearStress", "90", "MPa");
            view.SetField("PinAllowableBendingStress", "250", "MPa");
            view.SetField("PinAllowableShearStress", "150", "MPa");
            view.SetField("Load", "50", "kN");
            view.SetField("LugThickness", "20", "mm");
            view.SetField("LugWidth", "100", "mm");
            view.SetField("HoleDiameter", "32", "mm");
            view.SetField("PinDiameter", "30", "mm");
            view.SetField("EdgeDistance", "40", "mm");
            view.SetField("CheekPlateThickness", "12", "mm");
            view.SetField("Clearance", "2", "mm");

            await ClickAsync(window, view, CalculationModulesView.CalculateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).LastRun is not null);
            view = SurfaceOf(window);

            var form = view.ReadForm();
            Assert.Equal(MaterialSeed.S355J2, form.Single(f => f.Name == "LugMaterialPin").RecordId);
            Assert.Equal(MaterialSeed.S355J2, form.Single(f => f.Name == "PinMaterialPin").RecordId);
            Assert.Contains(MaterialSeed.S355J2, view.LastRun!.ReferencedMaterialIds);
            Assert.Contains(view.LastRun.Working, w => w.Label == "Lug material reference" || w.Label.Contains("material", StringComparison.OrdinalIgnoreCase));
            AssertRendered(window, view, MaterialSeed.S355J2);
        });
    }

    [AvaloniaFact]
    public async Task ABearing_IsPickedFromItsOwnReleasedLibrary_AndFillsTheDesignationTypeAndRating()
    {
        await InCalculatorsAsync(async (host, window, view) =>
        {
            await ReleaseSeededBearingAsync(host, window);
            view = SurfaceOf(window);

            Assert.Contains(view.Released(ReferenceLibrary.Bearings), b => b.RecordId == BearingSeed.Rhd6205);
            Assert.DoesNotContain(view.Released(ReferenceLibrary.Bearings), b => b.RecordId == BearingSeed.Rhd6305);

            view.SelectModule(BearingRatingLifeCalculationDefinition.Id);
            LayOut(window);
            var picker = view.RecordPicker("BearingPin")!;
            AssertUsable(window, picker, "the bearing picker");
            Assert.Equal("Bearing record", AutomationProperties.GetName(picker));

            view.PickRecord("BearingPin", BearingSeed.Rhd6205);
            await RenderUntilAsync(window, () => (SurfaceOf(window).FieldControl("BasicDynamicLoadRating") as TextBox)?.Text is { Length: > 0 });
            view = SurfaceOf(window);

            // The seeded 6205: a deep-groove ball bearing with C = 14 kN.
            Assert.Equal("14", ((TextBox)view.FieldControl("BasicDynamicLoadRating")!).Text);
            Assert.Equal("kN", view.UnitPicker("BasicDynamicLoadRating")!.SelectedItem);
            Assert.Equal(nameof(RollingBearingType.Ball), ((ComboBox)view.FieldControl("BearingType")!).SelectedItem);
            Assert.False(string.IsNullOrWhiteSpace(((TextBox)view.FieldControl("BearingDesignation")!).Text));

            view.SetField("RadialLoad", "2", "kN");
            view.SetField("AxialLoad", "0", "kN");
            view.SetField("RadialFactor", "1");
            view.SetField("AxialFactor", "0");
            view.SetField("Speed", "1500", "r/min");
            view.SetField("ReliabilityFactor", "1");
            view.SetField("RequiredLife", "1000", "h");

            await ClickAsync(window, view, CalculationModulesView.CalculateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).LastRun is not null);
            view = SurfaceOf(window);

            // L10 = (14/2)^3 = 343 million revolutions; at 1500 r/min, 3811.1 h.
            Assert.Equal("Meets its criteria", view.LastRun!.OutcomeSummary);
            Assert.Contains(view.LastRun.Results, r => r.Label == "Basic rating life million revolutions" && r.Display == "343");
            Assert.Contains(view.LastRun.Working, w => w.Label == "Bearing reference" && w.Display.Contains(BearingSeed.Rhd6205, StringComparison.Ordinal));
            AssertRendered(window, view, BearingSeed.Rhd6205);
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
            view.PickRecord("MaterialPin", MaterialSeed.S355J2);
            await RenderUntilAsync(window, () => (SurfaceOf(window).FieldControl("YoungsModulus") as TextBox)?.Text == "210");
            view = SurfaceOf(window);

            view.SetField("Load", "10", "kN");
            view.SetField("Span", "200", "mm");
            view.SetField("SecondMomentOfArea", "2000000", "mm^4");
            view.SetField("ExtremeFibreDistance", "50", "mm");
            view.SetField("DeflectionLimit", "8", "mm");

            await ClickAsync(window, view, CalculationModulesView.CalculateCaption);
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
            // Bolt shear takes no record: the form runs with none picked.
            view.SelectModule(BoltShearCapacityCalculationDefinition.Id);
            LayOut(window);
            view.SetField("Diameter", "abc", "mm");
            view.SetField("UltimateShearStrength", "400", "MPa");
            view.SetField("ShearPlanes", "2");
            view.SetField("SafetyFactor", "1.5");

            await ClickAsync(window, view, CalculationModulesView.CalculateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).GetLogicalDescendants().OfType<TextBlock>()
                .Any(t => AutomationProperties.GetName(t) == "Input problems" && (t.Text ?? string.Empty).Contains("'abc'", StringComparison.Ordinal)));
            view = SurfaceOf(window);

            Assert.Null(view.LastRun);
            AssertRendered(window, view, "Bolt diameter: 'abc' is not a number");

            // Corrected, the original definition runs from the same form.
            view.SetField("Diameter", "20", "mm");
            await ClickAsync(window, view, CalculationModulesView.CalculateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).LastRun is not null);
            view = SurfaceOf(window);

            Assert.Equal("Computed", view.LastRun!.OutcomeSummary);
            AssertRendered(window, view, "167552 N");
        });
    }

    [AvaloniaFact]
    public async Task RerunAndCompare_AreOfferedOnTheResult_AndTheComparisonIsATableOfWhatChanged()
    {
        await InCalculatorsAsync(async (host, window, view) =>
        {
            view.SelectModule(BoltShearCapacityCalculationDefinition.Id);
            LayOut(window);
            view.SetField("Diameter", "20", "mm");
            view.SetField("UltimateShearStrength", "400", "MPa");
            view.SetField("ShearPlanes", "2");
            view.SetField("SafetyFactor", "1.5");

            // Before any run, neither command is offered.
            Assert.False(ButtonNamed(view, CalculationModulesView.RerunCaption).IsEnabled);
            Assert.False(ButtonNamed(view, CalculationModulesView.CompareCaption).IsEnabled);

            await ClickAsync(window, view, CalculationModulesView.CalculateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).CurrentRun is not null);
            view = SurfaceOf(window);
            var first = view.CurrentRun!;
            Assert.Equal(1, first.RunCount);
            AssertUsable(window, ButtonNamed(view, CalculationModulesView.RerunCaption), "the Re-run button");
            Assert.False(ButtonNamed(view, CalculationModulesView.CompareCaption).IsEnabled, "Compare needs two runs.");

            // Re-run: the canonical command, a new record on the same calculation.
            await ClickAsync(window, view, CalculationModulesView.RerunCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).CurrentRun?.RunCount == 2);
            view = SurfaceOf(window);
            var second = view.CurrentRun!;
            Assert.Equal(first.CalculationObjectId, second.CalculationObjectId);
            Assert.Equal(first.Run.RecordId, second.Run.PredecessorRecordId);
            AssertRendered(window, view, "Re-ran and recorded");
            AssertUsable(window, ButtonNamed(view, CalculationModulesView.CompareCaption), "the Compare button");

            // Compare on an identical re-run: nothing differs, said so.
            await ClickAsync(window, view, CalculationModulesView.CompareCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).LastComparison is not null);
            view = SurfaceOf(window);
            Assert.False(view.LastComparison!.HasChanges);
            AssertRendered(window, view, "nothing differs");

            // Calculate again with a changed input: another run on the same
            // calculation, and the comparison is a table of what changed.
            view.SetField("SafetyFactor", "2");
            await ClickAsync(window, view, CalculationModulesView.CalculateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).CurrentRun?.RunCount == 3);
            view = SurfaceOf(window);
            Assert.Equal(first.CalculationObjectId, view.CurrentRun!.CalculationObjectId);
            AssertRendered(window, view, "125664 N");

            await ClickAsync(window, view, CalculationModulesView.CompareCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).LastComparison?.HasChanges == true);
            view = SurfaceOf(window);
            var rows = view.LastComparison!.Rows;
            Assert.Contains(rows, r => r.Section == "Input" && r.Field == "Safety factor" && r.Before.Contains("1.5", StringComparison.Ordinal) && r.After.Contains("2", StringComparison.Ordinal));
            Assert.Contains(rows, r => r.Section == "Result" && r.Field == "Allowable shear capacity");
            AssertRendered(window, view, "Before");
            AssertRendered(window, view, "After");
            AssertRendered(window, view, "Safety factor");
            AssertRendered(window, view, "Allowable shear capacity");
            AssertRendered(window, view, "Comparison with the previous run");

            // Start a new calculation: the next Calculate names a new one.
            await ClickAsync(window, view, CalculationModulesView.NewCalculationCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).CurrentRun is null);
            view = SurfaceOf(window);
            Assert.False(ButtonNamed(view, CalculationModulesView.RerunCaption).IsEnabled);

            await ClickAsync(window, view, CalculationModulesView.CalculateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).CurrentRun is not null);
            view = SurfaceOf(window);
            Assert.NotEqual(first.CalculationObjectId, view.CurrentRun!.CalculationObjectId);
            Assert.Equal(1, view.CurrentRun.RunCount);
        });
    }

    // ---- Helpers ----

    private static async Task ReleaseSeededSteelAsync(WorkspaceHost host, MainWindow window)
    {
        await host.BracketCalculations!.PopulateMaterialLibraryAsync();
        await host.BracketCalculations!.VerifyAndReleaseAsync(MaterialSeed.S355J2, "Siderticino datasheet, mechanical properties table", "Needed for the calculator test.");
        await ReenterAsync(window, ReferenceLibrary.Materials);
    }

    private static async Task ReleaseSeededBearingAsync(WorkspaceHost host, MainWindow window)
    {
        var principals = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
        var review = new ReferenceReviewService(principals);
        await review.VerifyAsync(host.Bearings!, BearingSeed.Rhd6205, new ReferenceReviewStatement("Manufacturer catalogue, deep-groove ball bearings table"));
        await review.ReleaseAsync(host.Bearings!, BearingSeed.Rhd6205, "Needed for the calculator test.");
        await ReenterAsync(window, ReferenceLibrary.Bearings);
    }

    /// <summary>Re-enters the node so every picker re-reads the released records of <paramref name="library"/>.</summary>
    private static async Task ReenterAsync(MainWindow window, ReferenceLibrary library)
    {
        var area = window.GetLogicalDescendants().OfType<EngineeringAreaView>().Distinct().Single();
        area.SelectNode("Engineering Calculations");
        await RenderUntilAsync(window, () => !window.GetLogicalDescendants().OfType<CalculationModulesView>().Any(v => v.IsVisible && v.Bounds.Width > 0));
        area.SelectNode(NodeName);
        await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<CalculationModulesView>().Any(v => v.Released(library).Count > 0));
    }

    private static Button ButtonNamed(CalculationModulesView view, string name) =>
        view.GetLogicalDescendants().OfType<Button>().Distinct().Single(b => AutomationProperties.GetName(b) == name);

    private static async Task ClickAsync(MainWindow window, CalculationModulesView view, string buttonName)
    {
        LayOut(window);
        var button = ButtonNamed(view, buttonName);
        AssertUsable(window, button, $"the {buttonName} button");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
    }

    private static async Task InCalculatorsAsync(Func<WorkspaceHost, MainWindow, CalculationModulesView, Task> body)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var principalSession = (PrincipalSession)host.Services!.GetService(typeof(PrincipalSession));
            principalSession.Establish(new PlatformPrincipal(new PlatformIdentity(EngineerId, EngineerId), ApplicationPermissions.LocalSession));

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

    private static string StatusOf(MainWindow window) =>
        SurfaceOf(window).GetLogicalDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == "Calculator status").Text ?? string.Empty;

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DesktopTestHelpers.Deadline(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            LayOut(window);
        }

        if (!condition())
        {
            var surface = window.GetLogicalDescendants().OfType<CalculationModulesView>().Distinct().SingleOrDefault();
            var said = surface?.GetLogicalDescendants().OfType<TextBlock>()
                .Where(t => AutomationProperties.GetName(t) is "Calculator status" or "Input problems" && !string.IsNullOrWhiteSpace(t.Text))
                .Select(t => $"{AutomationProperties.GetName(t)}: {t.Text}")
                .ToList() ?? [];
            Assert.Fail($"The surface did not reach the expected state in time. {string.Join(" | ", said)}");
        }

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
