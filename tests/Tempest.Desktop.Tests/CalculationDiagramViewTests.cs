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
    public void ACalculationWithNoDiagram_SaysSoHonestly_AndDrawsNothing()
    {
        WithCalculators(FatigueMinerCalculationDefinition.Id, (window, view) =>
        {
            Assert.Contains(FatigueMinerCalculationDefinition.Id, CalculationDiagrams.NoDiagramYet);

            var diagram = view.Diagram;
            Assert.False(diagram.HasDiagram);
            Assert.False(diagram.Draws("Slope"));
            AssertDrawn(window, diagram, CalculationDiagramView.NoDiagramYetText);
            Assert.Contains(CalculationDiagramView.NoDiagramYetText, diagram.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain(diagram.GetVisualDescendants().OfType<Shape>(), s => s.IsEffectivelyVisible);

            // Choosing a diagrammed calculation afterwards draws it.
            view.SelectModule(ThermalExpansionStressCalculationDefinition.Id);
            Assert.True(diagram.HasDiagram);
            Assert.Equal("k_s = not given", diagram.LabelFor("RestraintStiffness"));
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
