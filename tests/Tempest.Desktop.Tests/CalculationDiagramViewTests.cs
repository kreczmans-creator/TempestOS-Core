using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
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
            Assert.Contains("L E = not readable yet", diagram.Summary, StringComparison.Ordinal);
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
            var spanLines = diagram.ShapesFor("Span").OfType<Line>().ToList();
            Assert.NotEmpty(spanLines);
            Assert.All(spanLines, line => Assert.Same(focusRing, line.Stroke));
            Assert.Equal(FontWeight.Bold, diagram.ShapesFor("Span").OfType<TextBlock>().Single().FontWeight);
            var loadShapes = diagram.ShapesFor("Load").OfType<Shape>().ToList();
            Assert.NotEmpty(loadShapes);
            Assert.All(loadShapes, shape => Assert.NotSame(focusRing, shape.Stroke));
            Assert.All(loadShapes.OfType<Polygon>(), head => Assert.NotSame(focusRing, head.Fill));

            Assert.True(load.Focus());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Load", diagram.HighlightedInput);
            Assert.False(diagram.IsHighlighted("Span"));
            var litLoad = diagram.ShapesFor("Load").OfType<Shape>().Where(s => s.Stroke is not null).ToList();
            Assert.NotEmpty(litLoad);
            Assert.All(litLoad, shape => Assert.Same(focusRing, shape.Stroke));
            var heads = diagram.ShapesFor("Load").OfType<Polygon>().ToList();
            Assert.NotEmpty(heads);
            Assert.All(heads, head => Assert.Same(focusRing, head.Fill)); // the arrowhead is lit with its shaft

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
            Assert.Equal("T₁ = 20 degC", diagram.LabelFor("HotSideTemperature"));

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
            // Every product calculation has a diagram, so the defensive fallback is shown a synthetic descriptor.
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

    [AvaloniaFact]
    public void ThePointer_OnAShape_MarksItsRow_AndAClickFocusesItsField()
    {
        // Real pointer input through hit-testing, not the Hover/Activate shortcuts.
        WithCalculators(BeamDeflectionCalculationDefinition.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            var label = diagram.ShapesFor("Span").OfType<TextBlock>().Single();
            var centre = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), window)!.Value;
            var label2 = ((Grid)view.FieldRow("Span")!).Children.OfType<TextBlock>().First();

            window.MouseMove(centre, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(diagram.IsHighlighted("Span"), "Pointing at the span's label highlights the span.");
            Assert.True(view.IsRowMarked("Span"), "Pointing at the span's label marks its row.");
            Assert.Equal(FontWeight.Bold, label2.FontWeight);
            Assert.Same(ResolveBrush(view, ApplicationPalette.FocusRingBrushKey), label2.Foreground);
            Assert.False(view.IsRowMarked("Load"));

            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.IsRowMarked("Span"), "Moving off the shape unmarks the row.");
            Assert.Equal(FontWeight.Normal, label2.FontWeight);

            window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.FieldControl("Span")!.IsFocused, "Clicking the span's shape focuses its box.");
        });
    }

    [AvaloniaFact]
    public void SwitchingToDark_RedrawsEveryShapeInTheDarkTokens_AndAMarkedRowFollows()
    {
        WithCalculators(LiftingLugPinJointCalculationDefinition.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            diagram.Hover("PinDiameter");
            var lightWash = view.FieldRow("PinDiameter")!.GetValue(Panel.BackgroundProperty);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            LayOut(window);
            Assert.Equal(ThemeVariant.Dark, diagram.ActualThemeVariant);

            var ink = ResolveBrush(diagram, BrandPalette.HeadingTextBrushKey);
            var plate = ResolveBrush(diagram, BrandPalette.HairlineStrongBrushKey);
            var ring = ResolveBrush(diagram, ApplicationPalette.FocusRingBrushKey);
            var hole = diagram.ShapesFor("HoleDiameter").OfType<Ellipse>().Single();
            Assert.Same(ResolveBrush(diagram, BrandPalette.SurfaceBackgroundBrushKey), hole.Fill);
            var lug = diagram.ShapesFor("LugWidth").OfType<Rectangle>().Single();
            Assert.Same(plate, lug.Fill);
            Assert.Same(ink, lug.Stroke);

            // The pin is solid, lit while pointed at; a marked row's wash is the dark theme's.
            var pin = diagram.ShapesFor("PinDiameter").OfType<Ellipse>().Single();
            Assert.Same(ring, pin.Fill);
            Assert.Same(ResolveBrush(view, ApplicationPalette.AccentPanelBackgroundBrushKey), view.FieldRow("PinDiameter")!.GetValue(Panel.BackgroundProperty));
            Assert.NotNull(lightWash);

            diagram.Hover(null);
            Assert.Same(ink, pin.Fill);
        });
    }

    [AvaloniaFact]
    public void EveryVariantOfEveryDiagram_IsDrawn_WithAShapeForEveryBoundInput()
    {
        WithCalculators(BeamDeflectionCalculationDefinition.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            foreach (var spec in CalculationDiagrams.All)
            {
                var module = CalculationModuleDescriptors.For(spec.CalculationId)!;
                foreach (var variant in spec.Variants)
                {
                    // The variant's own conditions; any other conditioned input a value no other variant is keyed on.
                    var kinds = module.Inputs.ToDictionary(i => i.Name, i => i.Kind, StringComparer.Ordinal);
                    var conditioned = spec.Variants.SelectMany(v => v.When.Keys).ToHashSet(StringComparer.Ordinal);
                    CalculationFormField? Field(string name)
                    {
                        if (!conditioned.Contains(name))
                            return null;

                        var value = variant.When.TryGetValue(name, out var keyed) ? keyed : kinds[name] == CalculationInputKind.Boolean ? CalculationDiagramReader.BooleanFalse : "3";
                        return kinds[name] switch
                        {
                            CalculationInputKind.Choice => new CalculationFormField(name, Choice: value),
                            CalculationInputKind.Boolean => new CalculationFormField(name, Flag: value == CalculationDiagramReader.BooleanTrue),
                            _ => new CalculationFormField(name, value),
                        };
                    }

                    diagram.Show(module, Field);

                    Assert.True(diagram.HasDiagram, $"{spec.CalculationId}: nothing drawn.");
                    Assert.Same(variant, diagram.Reading!.Variant);
                    var drawn = diagram.GetVisualDescendants().OfType<Shape>().Count();
                    Assert.True(drawn > 0, $"{spec.CalculationId} '{variant.Caption}': no shape drawn.");
                    Assert.All(variant.Elements.Where(e => e.InputName is not null && e is not DiagramLabel && e.Symbol is null),
                        e => Assert.True(diagram.Draws(e.InputName!), $"{spec.CalculationId}/{e.Id}: no shape for {e.InputName}."));
                }
            }
        });
    }

    [AvaloniaTheory]
    [InlineData(1180, 760)]
    [InlineData(1050, 760)]
    public void OnALaptopWindow_EveryValueBoxStaysReadable_AndTheDiagramOverlapsNothing(double width, double height)
    {
        // The board's F1: at 1180 x 760 the fixed panel beside the inputs squeezed every value box to ~64 px, under its unit picker.
        WithCalculators(BeamDeflectionCalculationDefinition.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            DesktopTestHelpers.AssertPlaced(diagram, "the reference diagram");
            var diagramBox = Box(diagram, window);
            Assert.True(diagramBox.Right <= width + 0.5, $"The diagram ends at x={diagramBox.Right}, past the {width} px window.");

            foreach (var name in new[] { "Span", "Load", "YoungsModulus", "SecondMomentOfArea", "DeflectionLimit" })
            {
                var box = view.FieldControl(name)!;
                var unit = view.UnitPicker(name)!;
                var label = ((Grid)view.FieldRow(name)!).Children.OfType<TextBlock>().First();
                var boxRect = Box(box, window);
                var labelRect = Box(label, window);
                var unitRect = Box(unit, window);

                Assert.True(boxRect.Width >= 120, $"{name}: the value box is {boxRect.Width:0} px wide at {width} x {height}; it must be at least 120 px to be read.");
                Assert.True(boxRect.Left >= labelRect.Right - 0.5, $"{name}: the value box (x={boxRect.Left:0}) overlaps its label (ends x={labelRect.Right:0}).");
                Assert.True(unitRect.Left >= boxRect.Right - 0.5, $"{name}: the unit picker (x={unitRect.Left:0}) covers the value box (ends x={boxRect.Right:0}).");
                Assert.False(boxRect.Intersects(diagramBox), $"{name}: the value box {boxRect} overlaps the diagram {diagramBox}.");
            }
        }, width, height);
    }

    private static Rect Box(Control control, Visual relativeTo)
    {
        var origin = control.TranslatePoint(new Point(0, 0), relativeTo)!.Value;
        return new Rect(origin, control.Bounds.Size);
    }

    private static void WithCalculators(string calculationId, Action<Window, CalculationModulesView> body, double width = 1600, double height = 1000)
    {
        var view = new CalculationModulesView();
        var window = new Window { Width = width, Height = height, Content = view };
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
            window.Measure(new Size(window.Width, window.Height));
            window.Arrange(new Rect(0, 0, window.Width, window.Height));
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
