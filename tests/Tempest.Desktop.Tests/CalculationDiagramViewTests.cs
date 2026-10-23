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
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Calculations.Modules.Diagrams;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The reference diagram (`PO-2`, work packages B and C, `ADR-0158`) on the
/// calculation page itself — since v0.27.0 the worksheet form (ADR-0167,
/// D-41), ported from the retired legacy calculators with every behaviour
/// kept: a panel beside the inputs (above them on a laptop window), redrawn
/// as a field is edited, a choice or a tick redrawing the variant, a
/// focused field highlighting its shapes and a shape marking or focusing
/// its field, and an honest "No diagram yet" where there is none.
/// </summary>
public sealed class CalculationDiagramViewTests
{
    [AvaloniaFact]
    public void EditingAField_UpdatesItsLabel_AndAChoiceRedrawsTheVariant()
    {
        WithWorksheet(BeamFormulasCalculator.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            Assert.True(diagram.HasDiagram);
            Assert.Equal("L = 6 m", diagram.LabelFor("L"));

            view.SetField("L", unit: "mm");
            view.SetField("L", "2000");
            Assert.Equal("L = 2000 mm", diagram.LabelFor("L"));
            AssertDrawn(window, diagram, "L = 2000 mm");
            Assert.Contains("L = 2000 mm", diagram.Summary, StringComparison.Ordinal);

            view.SetField("L", "2,000");
            Assert.Equal("L = ?", diagram.LabelFor("L"));

            view.SetField("L", "3");
            Assert.StartsWith("L = ", diagram.LabelFor("L"), StringComparison.Ordinal);

            // A choice selects the variant: a cantilever is drawn built in, with no roller.
            Assert.StartsWith("Simply supported", diagram.Reading!.Variant.Caption, StringComparison.Ordinal);
            view.SetField("case", choice: BeamFormulasCalculator.CantUdl);
            Assert.Equal("Cantilever fixed at the left, uniformly distributed load w", diagram.Reading!.Variant.Caption);
            Assert.Equal("w = 10 kN/m", diagram.LabelFor("w"));
            Assert.DoesNotContain(diagram.Reading.Shapes, s => s.Element is DiagramSupport { Kind: DiagramSupportKind.Roller });
            AssertDrawn(window, diagram, "Cantilever fixed at the left, uniformly distributed load w");
        });
    }

    [AvaloniaFact]
    public void TheDiagram_IsAPanelBesideTheInputs_MarkedNotToScale_WithItsSummaryForAScreenReader()
    {
        WithWorksheet(ColumnBucklingCalculator.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            var box = view.FieldControl("L_E")!;
            DesktopTestHelpers.AssertPlaced(diagram, "the reference diagram");
            DesktopTestHelpers.AssertPlaced(box, "the effective length box");

            var diagramLeft = diagram.TranslatePoint(new Point(0, 0), window)!.Value.X;
            var boxRight = box.TranslatePoint(new Point(box.Bounds.Width, 0), window)!.Value.X;
            Assert.True(diagramLeft >= boxRight, $"The diagram (x={diagramLeft}) sits beside the inputs, right of the box (ends x={boxRight}).");
            Assert.Equal(CalculationDiagramView.PanelWidth, diagram.Bounds.Width, 1);

            AssertDrawn(window, diagram, CalculationDiagramView.NotToScaleText);
            Assert.Equal(CalculationDiagramView.Heading, AutomationProperties.GetName(diagram));
            Assert.StartsWith(view.SelectedTemplate!.Template.Title, diagram.Summary, StringComparison.Ordinal);
            Assert.Contains(CalculationDiagramReader.NotToScale, diagram.Summary, StringComparison.Ordinal);
            Assert.Contains("L E = 3 m", diagram.Summary, StringComparison.Ordinal);
        });
    }

    [AvaloniaFact]
    public void FocusingAField_HighlightsItsShape_AndAShapeMarksOrFocusesItsField()
    {
        WithWorksheet(BeamFormulasCalculator.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            var span = view.FieldControl("L")!;
            var load = view.FieldControl("w")!;
            Assert.True(diagram.Draws("L"));
            Assert.False(diagram.IsHighlighted("L"));

            Assert.True(span.Focus());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("L", diagram.HighlightedInput);
            Assert.True(diagram.IsHighlighted("L"));
            var focusRing = ResolveBrush(diagram, ApplicationPalette.FocusRingBrushKey);
            var spanLines = diagram.ShapesFor("L").OfType<Line>().ToList();
            Assert.NotEmpty(spanLines);
            Assert.All(spanLines, line => Assert.Same(focusRing, line.Stroke));
            Assert.Equal(FontWeight.Bold, diagram.ShapesFor("L").OfType<TextBlock>().Single().FontWeight);
            var loadShapes = diagram.ShapesFor("w").OfType<Shape>().ToList();
            Assert.NotEmpty(loadShapes);
            Assert.All(loadShapes, shape => Assert.NotSame(focusRing, shape.Stroke));

            Assert.True(load.Focus());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("w", diagram.HighlightedInput);
            Assert.False(diagram.IsHighlighted("L"));
            var litLoad = diagram.ShapesFor("w").OfType<Shape>().Where(s => s.Stroke is not null).ToList();
            Assert.NotEmpty(litLoad);
            Assert.All(litLoad, shape => Assert.Same(focusRing, shape.Stroke));

            // The reverse: pointing at the span's shape marks its row; clicking it focuses its box.
            diagram.Hover("L");
            Assert.True(view.IsRowMarked("L"));
            Assert.True(diagram.IsHighlighted("L"));
            diagram.Hover(null);
            Assert.False(view.IsRowMarked("L"));

            diagram.Activate("L");
            Dispatcher.UIThread.RunJobs();
            Assert.True(span.IsFocused);
            Assert.Equal("L", diagram.HighlightedInput);
        });
    }

    [AvaloniaFact]
    public void ATableInput_RedrawsItsLabelAsRowsChange_AndHighlightsEveryLayer()
    {
        WithWorksheet(PlaneWallCalculator.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            Assert.True(diagram.HasDiagram);
            Assert.Equal("layers = 1 row", diagram.LabelFor("layers"));

            view.ApplyImportedInputs(CalcTemplateHasher.DefaultInputs(new PlaneWallCalculator().Template).With(PlaneWallCalculator.Layers(("glass", 4, 0.78), ("air", 10, 0.026))));
            Assert.Equal("layers = 2 rows", diagram.LabelFor("layers"));
            AssertDrawn(window, diagram, "layers = 2 rows");

            view.SetField("T_1", "21", "degC");
            Assert.Equal("T_1 = 21 °C", diagram.LabelFor("T_1"));

            diagram.Highlight("layers");
            var focusRing = ResolveBrush(diagram, ApplicationPalette.FocusRingBrushKey);
            var layers = diagram.ShapesFor("layers").OfType<Rectangle>().ToList();
            Assert.Equal(3, layers.Count);
            Assert.All(layers, layer => Assert.Same(focusRing, layer.Stroke));

            view.SelectTemplate(ToleranceStackCalculator.Id);
            Assert.True(diagram.HasDiagram);
            view.SetField("minimum", unit: "mm");
            view.SetField("minimum", "0.1");
            Assert.Equal("min = 0.1 mm", diagram.LabelFor("minimum"));

            view.SelectTemplate(ThermalChainCalculator.Id);
            Assert.True(diagram.HasDiagram);
            Assert.Equal("stages = 3 rows", diagram.LabelFor("stages"));
        });
    }

    [AvaloniaFact]
    public void TickingAYesOrNoInput_RedrawsTheVariant_AndTheHarderGeometryDiagramsAreDrawn()
    {
        WithWorksheet(ThickCylinderCalculator.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            Assert.True(diagram.HasDiagram);

            view.SetField("closed", flag: false);
            Assert.Contains("open ends", diagram.Reading!.Variant.Caption, StringComparison.Ordinal);
            Assert.Equal("closed ends = no", diagram.LabelFor("closed"));
            Assert.Single(diagram.ShapesFor("closed"));

            view.SetField("closed", flag: true);
            Assert.Contains("closed ends carry", diagram.Reading!.Variant.Caption, StringComparison.Ordinal);
            Assert.Equal("closed ends = yes", diagram.LabelFor("closed"));
            Assert.Equal(2, diagram.ShapesFor("closed").OfType<Rectangle>().Count());
            AssertDrawn(window, diagram, "closed ends = yes");

            view.SetField("a", "50", "mm");
            Assert.Equal("a = 50 mm", diagram.LabelFor("a"));

            view.SelectTemplate(BoltGroupCalculator.Id);
            Assert.True(diagram.HasDiagram);
            Assert.Matches(@"^bolts = \d+ rows$", diagram.LabelFor("bolts"));

            view.SelectTemplate(BoltedJointCalculator.Id);
            Assert.True(diagram.HasDiagram);
            view.SetField("F_A", "20", "kN");
            Assert.Equal("F_A = 20 kN", diagram.LabelFor("F_A"));

            view.SelectTemplate(FilletWeldCalculator.Id);
            Assert.True(diagram.HasDiagram);
            view.SelectTemplate(LiftingLugCalculator.Id);
            Assert.True(diagram.HasDiagram);
        });
    }

    [AvaloniaFact]
    public void ACalculationWithNoDiagram_SaysSoHonestly_AndDrawsNothing()
    {
        WithWorksheet(ThermalExpansionCalculator.Id, (window, view) =>
        {
            // Every worksheet template has a diagram, so the defensive fallback is shown a reader that finds none.
            Assert.Null(CalcDiagrams.For("mech.not-yet-drawn"));

            var diagram = view.Diagram;
            diagram.ShowReading("Not yet drawn", () => null);
            Assert.False(diagram.HasDiagram);
            Assert.False(diagram.Draws("L"));
            AssertDrawn(window, diagram, CalculationDiagramView.NoDiagramYetText);
            Assert.Contains(CalculationDiagramView.NoDiagramYetText, diagram.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain(diagram.GetVisualDescendants().OfType<Shape>(), s => s.IsEffectivelyVisible);

            // Choosing a diagrammed template afterwards draws it.
            view.SelectTemplate(FatigueMinerCalculator.Id);
            view.SelectTemplate(ThermalExpansionCalculator.Id);
            Assert.True(diagram.HasDiagram);
            Assert.Equal("k_s = not given", diagram.LabelFor("k_s"));
        });
    }

    [AvaloniaFact]
    public void TheChartLikeDiagrams_DrawAxesAndLines_AndHighlightThemWithTheirInputs()
    {
        WithWorksheet(FatigueMinerCalculator.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            Assert.True(diagram.HasDiagram);
            Assert.Equal("m = 3", diagram.LabelFor("m"));

            view.SetField("S_ref", "80", "MPa");
            Assert.Equal("S_ref = 80 MPa", diagram.LabelFor("S_ref"));
            Assert.Equal("blocks = 3 rows", diagram.LabelFor("blocks"));
            AssertDrawn(window, diagram, "S_ref = 80 MPa");
            AssertDrawn(window, diagram, "log N");

            var limit = diagram.ShapesFor("S_e").OfType<Polyline>().Single();
            Assert.NotNull(limit.StrokeDashArray);
            Assert.Equal(3, diagram.ShapesFor("blocks").OfType<Ellipse>().Count());

            Assert.True(view.FieldControl("m")!.Focus());
            Dispatcher.UIThread.RunJobs();
            var focusRing = ResolveBrush(diagram, ApplicationPalette.FocusRingBrushKey);
            Assert.All(diagram.ShapesFor("m").OfType<Polyline>(), line => Assert.Same(focusRing, line.Stroke));
            Assert.Equal(2, diagram.ShapesFor("m").OfType<Polyline>().Count());

            // The axes are context: drawn in ink, never hit-tested, never highlighted.
            var ink = ResolveBrush(diagram, BrandPalette.HeadingTextBrushKey);
            var unbound = diagram.GetVisualDescendants().OfType<Line>().Where(l => !l.IsHitTestVisible).ToList();
            Assert.Contains(unbound, l => ReferenceEquals(l.Stroke, ink));

            view.SelectTemplate(BearingLifeCalculator.Id);
            Assert.True(diagram.HasDiagram);
            view.SetField("F_r", "2", "kN");
            view.SetField("type", choice: BearingLifeCalculator.Roller);
            Assert.Equal("F_r = 2 kN", diagram.LabelFor("F_r"));
            Assert.StartsWith("A roller bearing", diagram.Reading!.Variant.Caption, StringComparison.Ordinal);
            Assert.Equal(2, diagram.ShapesFor("type").OfType<Rectangle>().Count());

            view.SelectTemplate(MaterialMarginCalculator.Id);
            Assert.True(diagram.HasDiagram);
            view.SetField("sigma", "130", "MPa");
            Assert.Equal("σ = 130 MPa", diagram.LabelFor("sigma"));
            AssertDrawn(window, diagram, "f_y = 300 MPa");
        });
    }

    [AvaloniaFact]
    public void ThePointer_OnAShape_MarksItsRow_AndAClickFocusesItsField()
    {
        // Real pointer input through hit-testing, not the Hover/Activate shortcuts.
        WithWorksheet(BeamFormulasCalculator.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            var label = diagram.ShapesFor("L").OfType<TextBlock>().Single();
            var centre = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), window)!.Value;
            var rowLabel = ((Grid)view.FieldRow("L")!).Children.OfType<TextBlock>().First();

            window.MouseMove(centre, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(diagram.IsHighlighted("L"), "Pointing at the span's label highlights the span.");
            Assert.True(view.IsRowMarked("L"), "Pointing at the span's label marks its row.");
            Assert.Equal(FontWeight.Bold, rowLabel.FontWeight);
            Assert.Same(ResolveBrush(view, ApplicationPalette.FocusRingBrushKey), rowLabel.Foreground);
            Assert.False(view.IsRowMarked("w"));

            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.IsRowMarked("L"), "Moving off the shape unmarks the row.");
            Assert.Equal(FontWeight.Normal, rowLabel.FontWeight);

            window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.FieldControl("L")!.IsFocused, "Clicking the span's shape focuses its box.");
        });
    }

    [AvaloniaFact]
    public void SwitchingToDark_RedrawsEveryShapeInTheDarkTokens_AndAMarkedRowFollows()
    {
        WithWorksheet(LiftingLugCalculator.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            diagram.Hover("d_p");
            var lightWash = view.FieldRow("d_p")!.GetValue(Panel.BackgroundProperty);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            LayOut(window);
            Assert.Equal(ThemeVariant.Dark, diagram.ActualThemeVariant);

            var ink = ResolveBrush(diagram, BrandPalette.HeadingTextBrushKey);
            var plate = ResolveBrush(diagram, BrandPalette.HairlineStrongBrushKey);
            var ring = ResolveBrush(diagram, ApplicationPalette.FocusRingBrushKey);
            var hole = diagram.ShapesFor("d_h").OfType<Ellipse>().Single();
            Assert.Same(ResolveBrush(diagram, BrandPalette.SurfaceBackgroundBrushKey), hole.Fill);
            var lug = diagram.ShapesFor("W").OfType<Rectangle>().Single();
            Assert.Same(plate, lug.Fill);
            Assert.Same(ink, lug.Stroke);

            // The pin is solid, lit while pointed at; a marked row's wash is the dark theme's.
            var pin = diagram.ShapesFor("d_p").OfType<Ellipse>().Single();
            Assert.Same(ring, pin.Fill);
            Assert.Same(ResolveBrush(view, ApplicationPalette.AccentPanelBackgroundBrushKey), view.FieldRow("d_p")!.GetValue(Panel.BackgroundProperty));
            Assert.NotNull(lightWash);

            diagram.Hover(null);
            Assert.Same(ink, pin.Fill);
        });
    }

    [AvaloniaFact]
    public void EveryVariantOfEveryDiagram_IsDrawn_WithAShapeForEveryBoundInput()
    {
        WithWorksheet(BeamFormulasCalculator.Id, (window, view) =>
        {
            var diagram = view.Diagram;

            foreach (var template in CalcCalculatorComposition.All().Select(c => c.Template))
            {
                var spec = CalcDiagrams.For(template.Id)!;
                var conditioned = spec.Variants.SelectMany(v => v.When.Keys).ToHashSet(StringComparer.Ordinal);

                foreach (var variant in spec.Variants)
                {
                    // The variant's own conditions; any other conditioned input a value no variant is keyed on.
                    var values = CalcTemplateHasher.DefaultInputs(template).Values.Where(v => !conditioned.Contains(v.Symbol)).ToList();

                    foreach (var name in conditioned)
                    {
                        var input = template.FindInput(name)!;
                        var keyed = variant.When.TryGetValue(name, out var w) ? w : null;

                        if (keyed is { Length: 0 })
                            continue;

                        values.Add(input.Kind switch
                        {
                            CalcInputKind.Choice => CalcInputValue.Choice(name, keyed ?? input.Choices![0]),
                            CalcInputKind.Boolean => CalcInputValue.Switch(name, keyed == CalcDiagramReader.BooleanTrue),
                            _ => new CalcInputValue(name, Number: 3, Unit: input.DefaultUnit.Length > 0 ? input.DefaultUnit : null),
                        });
                    }

                    var inputs = new CalcInputSet(values);
                    diagram.ShowReading(template.Title, () => CalcDiagramReader.Read(template, inputs));

                    Assert.True(diagram.HasDiagram, $"{template.Id}: nothing drawn.");
                    Assert.Same(variant, diagram.Reading!.Variant);
                    Assert.True(diagram.GetVisualDescendants().OfType<Shape>().Any(), $"{template.Id} '{variant.Caption}': no shape drawn.");
                    Assert.All(variant.Elements.Where(e => e.InputName is not null && e is not DiagramLabel && e.Symbol is null),
                        e => Assert.True(diagram.Draws(e.InputName!), $"{template.Id}/{e.Id}: no shape for {e.InputName}."));
                }
            }
        });
    }

    [AvaloniaTheory]
    [InlineData(1180, 760)]
    [InlineData(1050, 760)]
    public void OnALaptopWindow_TheDiagramStacksAboveTheInputs_InView_AndEveryValueBoxStaysReadable(double width, double height)
    {
        // The board's F1: a fixed panel beside the inputs squeezed every value box; stacked, the diagram goes above them.
        WithWorksheet(BeamFormulasCalculator.Id, (window, view) =>
        {
            var diagram = view.Diagram;
            DesktopTestHelpers.AssertPlaced(diagram, "the reference diagram");
            var diagramBox = Box(diagram, window);
            Assert.True(diagramBox.Right <= width + 0.5, $"The diagram ends at x={diagramBox.Right}, past the {width} px window.");
            Assert.True(diagramBox.Top >= 0 && diagramBox.Top < height, $"The diagram's top edge (y={diagramBox.Top:0}) is not in the {height} px window without scrolling.");
            Assert.True(diagramBox.Bottom <= height + 0.5, $"The diagram ends at y={diagramBox.Bottom:0}, below the {height} px window; it must be seen without scrolling.");

            var firstRow = Box(view.FieldRow("case")!, window);
            Assert.True(diagramBox.Bottom <= firstRow.Top + 0.5, $"The diagram (ends y={diagramBox.Bottom:0}) is not above the first input row (y={firstRow.Top:0}).");

            foreach (var name in new[] { "L", "E", "I", "w", "delta_limit" })
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
                Assert.True(boxRect.Top >= diagramBox.Bottom - 0.5, $"{name}: the value box (y={boxRect.Top:0}) is not under the diagram (ends y={diagramBox.Bottom:0}).");
            }
        }, width, height);
    }

    [AvaloniaTheory]
    [InlineData(1180, 760)]
    [InlineData(1050, 760)]
    public void OnALaptopWindow_TabStillReachesTheFirstInputFirst_AndFocusHighlightsBothWays(double width, double height)
    {
        WithWorksheet(BeamFormulasCalculator.Id, (window, view) =>
        {
            var diagram = view.Diagram;

            // The diagram holds no tab stop, and above the inputs it still is not reached before them.
            Assert.DoesNotContain(diagram.GetVisualDescendants().Prepend(diagram).OfType<InputElement>(),
                e => e.Focusable && KeyboardNavigation.GetIsTabStop(e) && e.IsEffectivelyVisible && e is not CalculationDiagramView);
            var inputsSection = view.GetVisualDescendants().OfType<Expander>().Single(e => Equals(e.Header, "Inputs"));
            var firstInput = view.SelectedTemplate!.Template.Inputs.Select(i => view.FieldControl(i.Symbol)).OfType<Control>()
                .First(c => c.Focusable && c.IsEffectivelyEnabled && c.IsEffectivelyVisible);
            IInputElement current = inputsSection;
            IInputElement? reached = null;

            for (var step = 0; step < 6 && reached is null; step++)
            {
                var next = KeyboardNavigationHandler.GetNext(current, NavigationDirection.Next);
                Assert.NotNull(next);
                Assert.False(next is Visual v && (ReferenceEquals(v, diagram) || diagram.IsVisualAncestorOf(v)), "Tab reaches the diagram before the first input.");

                if (ReferenceEquals(next, firstInput) || (next is Visual nv && firstInput.IsVisualAncestorOf(nv)))
                    reached = next;

                current = next!;
            }

            Assert.True(reached is not null, "Tab from the Inputs section does not reach the first input field first.");

            // Field to shape.
            var span = view.FieldControl("L")!;
            Assert.True(span.Focus());
            Dispatcher.UIThread.RunJobs();
            Assert.True(diagram.IsHighlighted("L"));
            Assert.All(diagram.ShapesFor("L").OfType<Line>(), line => Assert.Same(ResolveBrush(diagram, ApplicationPalette.FocusRingBrushKey), line.Stroke));
            Assert.True(view.FieldControl("w")!.Focus());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("w", diagram.HighlightedInput);

            // Shape to field, by real pointer input on the stacked panel.
            var label = diagram.ShapesFor("L").OfType<TextBlock>().Single();
            var centre = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), window)!.Value;
            Assert.True(centre.Y > 0 && centre.Y < height, $"The span's label (y={centre.Y:0}) is not in view.");
            window.MouseMove(centre, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.IsRowMarked("L"), "Pointing at the span's label marks its row.");
            window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(span.IsFocused, "Clicking the span's shape focuses its box.");
            Assert.Equal("L", diagram.HighlightedInput);
        }, width, height);
    }

    private static Rect Box(Control control, Visual relativeTo)
    {
        var origin = control.TranslatePoint(new Point(0, 0), relativeTo)!.Value;
        return new Rect(origin, control.Bounds.Size);
    }

    private static void WithWorksheet(string templateId, Action<Window, CalcSheetView> body, double width = 1600, double height = 1000)
    {
        var view = new CalcSheetView();
        var catalogue = new CalcCatalogue(CalcCalculatorComposition.All());
        var window = new Window { Width = width, Height = height, Content = view };

        try
        {
            window.Show();
            view.ShowCatalogue(catalogue.Current());
            view.SelectTemplate(templateId);
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
