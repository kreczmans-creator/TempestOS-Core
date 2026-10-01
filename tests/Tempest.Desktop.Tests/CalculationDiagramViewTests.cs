using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Calculations.Modules.Diagrams;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;
using Tempest.Workspace.Engineering;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The calculator reference diagram (`PO-2`, work packages B and C,
/// `ADR-0158`) on the calculator page itself: a panel beside the inputs,
/// redrawn as a field is edited (<c>L = 2000 mm</c>, <c>L = ?</c>), a
/// choice redrawing the variant, a focused field highlighting its shapes
/// and a shape marking or focusing its field, and an honest "No diagram
/// yet" for a calculation that has none. The view is hosted alone in a
/// window — no workspace host — so each test lays out in milliseconds and
/// never waits on a deadline.
/// </summary>
public sealed class CalculationDiagramViewTests
{
    [AvaloniaFact]
    public void EditingAField_UpdatesItsLabel_AndAChoiceRedrawsTheVariant()
    {
        WithCalculators(BeamDeflectionCalculationDefinition.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            Assert.True(diagram.HasDiagram);
            Assert.Equal("L = ?", diagram.LabelFor("Span"));

            view.SetField("Span", "2000", "mm");
            Assert.Equal("L = 2000 mm", diagram.LabelFor("Span"));
            AssertDrawn(window, diagram, "L = 2000 mm");
            Assert.Contains("L = 2000 mm", diagram.Summary, StringComparison.Ordinal);

            view.SetField("Span", "2,000");
            Assert.Equal("L = ?", diagram.LabelFor("Span"));

            view.SetField("Span", "3", "m");
            Assert.Equal("L = 3 m", diagram.LabelFor("Span"));

            // A choice selects the variant: a cantilever is drawn built in, with no roller.
            Assert.StartsWith("Simply supported", diagram.Reading!.Variant.Caption, StringComparison.Ordinal);
            view.SetField("Support", choice: nameof(BeamSupport.Cantilever));
            view.SetField("Loading", choice: nameof(BeamLoading.UniformlyDistributed));
            Assert.Equal("Cantilever, uniformly distributed load", diagram.Reading!.Variant.Caption);
            Assert.Equal("W (total) = ?", diagram.LabelFor("Load"));
            Assert.Equal("L = 3 m", diagram.LabelFor("Span"));
            AssertDrawn(window, diagram, "Cantilever, uniformly distributed load");
        });
    }

    [AvaloniaFact]
    public void TheDiagram_IsAPanelBesideTheInputs_MarkedNotToScale_WithItsSummaryForAScreenReader()
    {
        WithCalculators(ColumnBucklingCalculationDefinition.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            var box = view.FieldControl("EffectiveLength")!;
            DesktopTestHelpers.AssertPlaced(diagram, "the reference diagram");
            DesktopTestHelpers.AssertPlaced(box, "the effective length box");

            var diagramLeft = diagram.TranslatePoint(new Point(0, 0), window)!.Value.X;
            var boxRight = box.TranslatePoint(new Point(box.Bounds.Width, 0), window)!.Value.X;
            Assert.True(diagramLeft >= boxRight, $"The diagram (x={diagramLeft}) sits beside the inputs, right of the box (ends x={boxRight}).");
            Assert.Equal(CalculationModulesView.DiagramPanelWidth, diagram.Bounds.Width, 1);

            AssertDrawn(window, diagram, CalculationDiagramView.NotToScaleText);
            Assert.Equal(CalculationDiagramView.Heading, AutomationProperties.GetName(diagram));
            Assert.StartsWith(view.SelectedModule!.Title, diagram.Summary, StringComparison.Ordinal);
            Assert.Contains(CalculationDiagramReader.NotToScale, diagram.Summary, StringComparison.Ordinal);
            Assert.Contains("L_E = ?", diagram.Summary, StringComparison.Ordinal);
        });
    }

    [AvaloniaFact]
    public void FocusingAField_HighlightsItsShape_AndAShapeMarksOrFocusesItsField()
    {
        WithCalculators(BeamDeflectionCalculationDefinition.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            var span = view.FieldControl("Span")!;
            var load = view.FieldControl("Load")!;
            Assert.True(diagram.Draws("Span"));
            Assert.False(diagram.IsHighlighted("Span"));

            Assert.True(span.Focus());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Span", diagram.HighlightedInput);
            Assert.True(diagram.IsHighlighted("Span"));
            var focusRing = ResolveBrush(diagram, ApplicationPalette.FocusRingBrushKey);
            Assert.All(diagram.ShapesFor("Span").OfType<Line>(), line => Assert.Same(focusRing, line.Stroke));
            Assert.Equal(FontWeight.Bold, diagram.ShapesFor("Span").OfType<TextBlock>().Single().FontWeight);
            Assert.All(diagram.ShapesFor("Load").OfType<Shape>(), shape => Assert.NotSame(focusRing, shape.Stroke));

            Assert.True(load.Focus());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Load", diagram.HighlightedInput);
            Assert.False(diagram.IsHighlighted("Span"));
            Assert.All(diagram.ShapesFor("Load").OfType<Shape>().Where(s => s.Stroke is not null), shape => Assert.Same(focusRing, shape.Stroke));

            // The reverse: pointing at the span's shape marks its row; clicking it focuses its box.
            diagram.Hover("Span");
            Assert.True(view.IsRowMarked("Span"));
            Assert.True(diagram.IsHighlighted("Span"));
            diagram.Hover(null);
            Assert.False(view.IsRowMarked("Span"));

            diagram.Activate("Span");
            Dispatcher.UIThread.RunJobs();
            Assert.True(span.IsFocused);
            Assert.Equal("Span", diagram.HighlightedInput);
        });
    }

    [AvaloniaFact]
    public void ARowListInput_RedrawsItsLabelAsRowsAreTyped_AndFocusHighlightsEveryLayer()
    {
        WithCalculators(PlaneWallHeatTransferCalculationDefinition.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            Assert.True(diagram.HasDiagram);
            Assert.Equal("Layers = ?", diagram.LabelFor("Layers"));

            view.SetField("Layers", rows: ["Glass, 8 mm, 0.78 W/(m.K)", "Air, 10 mm, 0.026 W/(m.K)"]);
            Assert.Equal("Layers = 2 rows", diagram.LabelFor("Layers"));
            AssertDrawn(window, diagram, "Layers = 2 rows");

            view.SetField("HotSideTemperature", "20", "degC");
            Assert.Equal("T_1 = 20 degC", diagram.LabelFor("HotSideTemperature"));

            Assert.True(view.FieldControl("Layers")!.Focus());
            Dispatcher.UIThread.RunJobs();
            var focusRing = ResolveBrush(diagram, ApplicationPalette.FocusRingBrushKey);
            var layers = diagram.ShapesFor("Layers").OfType<Rectangle>().ToList();
            Assert.Equal(3, layers.Count);
            Assert.All(layers, layer => Assert.Same(focusRing, layer.Stroke));

            view.SelectModule(ToleranceStackCalculationDefinition.Id);
            Assert.True(diagram.HasDiagram);
            view.SetField("MinimumResult", "0.1", "mm");
            Assert.Equal("min gap = 0.1 mm", diagram.LabelFor("MinimumResult"));

            view.SelectModule(ThermalResistanceChainCalculationDefinition.Id);
            Assert.True(diagram.HasDiagram);
            view.SetField("Stages", rows: ["Junction to case, 0.5 K/W"]);
            Assert.Equal("R stages = 1 row", diagram.LabelFor("Stages"));
        });
    }

    [AvaloniaFact]
    public void TickingAYesOrNoInput_RedrawsTheVariant_AndTheHarderGeometryDiagramsAreDrawn()
    {
        WithCalculators(ThickWalledCylinderCalculationDefinition.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            Assert.True(diagram.HasDiagram);
            Assert.Contains("open ends", diagram.Reading!.Variant.Caption, StringComparison.Ordinal);
            Assert.Equal("closed ends = no", diagram.LabelFor("ClosedEnds"));
            Assert.Single(diagram.ShapesFor("ClosedEnds"));

            view.SetField("ClosedEnds", flag: true);
            Assert.Contains("closed ends", diagram.Reading!.Variant.Caption, StringComparison.Ordinal);
            Assert.Equal("closed ends = yes", diagram.LabelFor("ClosedEnds"));
            Assert.Equal(2, diagram.ShapesFor("ClosedEnds").OfType<Rectangle>().Count());
            AssertDrawn(window, diagram, "closed ends = yes");

            view.SetField("InnerRadius", "50", "mm");
            Assert.Equal("a = 50 mm", diagram.LabelFor("InnerRadius"));

            view.SelectModule(BoltGroupEccentricShearCalculationDefinition.Id);
            Assert.True(diagram.HasDiagram);
            view.SetField("Bolts", rows: ["0 mm, 0 mm", "75 mm, 0 mm", "0 mm, 75 mm"]);
            Assert.Equal("Bolts = 3 rows", diagram.LabelFor("Bolts"));
            AssertDrawn(window, diagram, "Bolts = 3 rows");

            view.SelectModule(BoltedJointPreloadCalculationDefinition.Id);
            Assert.True(diagram.HasDiagram);
            view.SetField("Preload", "20", "kN");
            Assert.Equal("F_i = 20 kN", diagram.LabelFor("Preload"));

            view.SelectModule(FilletWeldThroatStressCalculationDefinition.Id);
            Assert.True(diagram.HasDiagram);
            view.SelectModule(LiftingLugPinJointCalculationDefinition.Id);
            Assert.True(diagram.HasDiagram);
        });
    }

    [AvaloniaFact]
    public void ACalculationWithNoDiagram_SaysSoHonestly_AndDrawsNothing()
    {
        WithCalculators(ThermalExpansionStressCalculationDefinition.Id, (window, view) =>
        {
            // A calculation no diagram is declared for (whatever NoDiagramYet holds today).
            var undrawn = CalculationModuleDescriptors.For(FatigueMinerCalculationDefinition.Id)! with { Id = "calc.not-yet-drawn", Title = "Not yet drawn" };
            Assert.Null(CalculationDiagrams.For(undrawn.Id));

            var diagram = view.Diagram;
            diagram.Show(undrawn, _ => null);
            Assert.False(diagram.HasDiagram);
            Assert.False(diagram.Draws("Slope"));
            AssertDrawn(window, diagram, CalculationDiagramView.NoDiagramYetText);
            Assert.Contains(CalculationDiagramView.NoDiagramYetText, diagram.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain(diagram.GetVisualDescendants().OfType<Shape>(), s => s.IsEffectivelyVisible);

            // Choosing a diagrammed calculation afterwards draws it.
            view.SelectModule(FatigueMinerCalculationDefinition.Id);
            view.SelectModule(ThermalExpansionStressCalculationDefinition.Id);
            Assert.True(diagram.HasDiagram);
            Assert.Equal("k_s = not given", diagram.LabelFor("RestraintStiffness"));
        });
    }

    [AvaloniaFact]
    public void TheChartLikeDiagrams_DrawAxesAndLines_AndHighlightThemWithTheirInputs()
    {
        WithCalculators(FatigueMinerCalculationDefinition.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            Assert.True(diagram.HasDiagram);
            Assert.Equal("m = ?", diagram.LabelFor("Slope"));

            view.SetField("Slope", "3");
            view.SetField("ReferenceStressRange", "71", "MPa");
            view.SetField("Blocks", rows: ["100 MPa, 100000", "60 MPa, 2000000"]);
            Assert.Equal("m = 3", diagram.LabelFor("Slope"));
            Assert.Equal("Δσ_C = 71 MPa", diagram.LabelFor("ReferenceStressRange"));
            Assert.Equal("blocks = 2 rows", diagram.LabelFor("Blocks"));
            AssertDrawn(window, diagram, "Δσ_C = 71 MPa");
            AssertDrawn(window, diagram, "log N");

            var limit = diagram.ShapesFor("EnduranceLimit").OfType<Polyline>().Single();
            Assert.NotNull(limit.StrokeDashArray);
            Assert.Equal(3, diagram.ShapesFor("Blocks").OfType<Ellipse>().Count());

            Assert.True(view.FieldControl("Slope")!.Focus());
            Dispatcher.UIThread.RunJobs();
            var focusRing = ResolveBrush(diagram, ApplicationPalette.FocusRingBrushKey);
            Assert.All(diagram.ShapesFor("Slope").OfType<Polyline>(), line => Assert.Same(focusRing, line.Stroke));
            Assert.Equal(2, diagram.ShapesFor("Slope").OfType<Polyline>().Count());

            // The axes are context: drawn in ink, never hit-tested, never highlighted.
            var ink = ResolveBrush(diagram, BrandPalette.HeadingTextBrushKey);
            var unbound = diagram.GetVisualDescendants().OfType<Line>().Where(l => !l.IsHitTestVisible).ToList();
            Assert.Contains(unbound, l => ReferenceEquals(l.Stroke, ink));

            view.SelectModule(BearingRatingLifeCalculationDefinition.Id);
            Assert.True(diagram.HasDiagram);
            view.SetField("RadialLoad", "2", "kN");
            view.SetField("BearingType", choice: nameof(RollingBearingType.Roller));
            Assert.Equal("F_r = 2 kN", diagram.LabelFor("RadialLoad"));
            Assert.StartsWith("A roller bearing", diagram.Reading!.Variant.Caption, StringComparison.Ordinal);
            Assert.Equal(2, diagram.ShapesFor("BearingType").OfType<Rectangle>().Count());

            view.SelectModule(MaterialSelectionMarginCalculationDefinition.Id);
            Assert.True(diagram.HasDiagram);
            view.SetField("AppliedStress", "120", "MPa");
            Assert.Equal("σ = 120 MPa", diagram.LabelFor("AppliedStress"));
            AssertDrawn(window, diagram, "σ_allow = ?");
        });
    }

    private static void WithCalculators(string calculationId, Action<Window, CalculationModulesView> body)
    {
        var view = new CalculationModulesView();
        var window = new Window { Width = 1600, Height = 1000, Content = view };
        try
        {
            window.Show();
            view.ShowCatalogue(CalculationModuleWorkbench.Catalogue());
            view.SelectModule(calculationId);
            LayOut(window);
            body(window, view);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void LayOut(Window window)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1600, 1000));
            window.Arrange(new Rect(0, 0, 1600, 1000));
        }
    }

    private static void AssertDrawn(Window window, CalculationDiagramView diagram, string text)
    {
        LayOut(window);
        var block = diagram.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == text && t.IsEffectivelyVisible);
        Assert.True(block is not null, $"'{text}' is not drawn on the reference diagram.");
        Assert.True(block!.Bounds.Width > 0 && block.Bounds.Height > 0, $"'{text}' is drawn at {block.Bounds}.");
    }

    private static IBrush ResolveBrush(Control control, string key)
    {
        Assert.True(Application.Current!.TryGetResource(key, control.ActualThemeVariant, out var value));
        return (IBrush)value!;
    }
}
