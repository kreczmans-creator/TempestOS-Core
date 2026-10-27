using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Calculations.Modules.Diagrams;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// ADR-0158, Engineering Principle 33, D-41: every worksheet template is
/// complete only with its reference diagram, drawn beside the form and read
/// against it — the legacy calculators' diagrams raised into the worksheets.
/// </summary>
public sealed class CalcDiagramsTests
{
    private static IReadOnlyList<CalcTemplate> Templates => [.. CalcCalculatorComposition.All().Select(c => c.Template)];

    /// <summary>
    /// The templates that have no reference diagram yet. These are the ones Addendum A and Addendum B added to v0.26.0 after the diagrams were
    /// drawn on the legacy-replacement line (ADR-0167, D-41); folding that line into v0.26.0 left them without one, and Principle 33 says a
    /// template is not complete without it. The list is an explicit, reviewed exemption and nothing else is exempt: a template on it that gains a
    /// diagram must leave it (the test below fails until it does), and a template not on it must have exactly one. Drawing them is open work (TASKS).
    /// </summary>
    public static readonly IReadOnlyList<string> WithoutDiagramYet =
    [
        "bs.pipe-expansion",
        "bs.room-heat-loss",
        "bs.sealed-system",
        "elec.conductor-sizing",
        "elec.containment",
        "elec.fault-protection",
        "elec.load-budget",
        "elec.motor-start",
        "elec.pcb-trace",
        "fp.accumulator",
        "fp.cylinder",
        "fp.oring-gland",
        "mech.bearing-life",
        "mech.mechanism",
        "mech.shaft",
        "mech.snap-fit",
        "mech.spring",
        "mech.weld-group",
    ];

    public static TheoryData<string> TemplateIds => [.. Templates.Where(t => !WithoutDiagramYet.Contains(t.Id)).Select(t => t.Id)];

    [Fact]
    public void Every_Template_Has_Exactly_One_Diagram_And_No_Diagram_Is_Orphaned()
    {
        foreach (var template in Templates)
        {
            var count = CalcDiagrams.All.Count(d => d.CalculationId == template.Id);

            if (WithoutDiagramYet.Contains(template.Id))
                Assert.True(count == 0, $"{template.Id} has a diagram now: take it off WithoutDiagramYet.");
            else
                Assert.True(count == 1, $"{template.Id} is not complete without exactly one reference diagram (Principle 33).");
        }

        Assert.All(WithoutDiagramYet, id => Assert.Contains(Templates, t => t.Id == id));
        Assert.Equal(WithoutDiagramYet.Count, WithoutDiagramYet.Distinct(StringComparer.Ordinal).Count());
        Assert.All(CalcDiagrams.All, d => Assert.Contains(Templates, t => t.Id == d.CalculationId));
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Every_Bound_Name_Is_An_Input_And_Every_Condition_Names_A_Real_Value(string templateId)
    {
        var template = Templates.Single(t => t.Id == templateId);
        var spec = CalcDiagrams.For(templateId)!;

        foreach (var name in spec.BoundInputNames)
            Assert.True(template.FindInput(name) is not null, $"{templateId}: '{name}' is bound on the diagram but is not an input.");

        foreach (var variant in spec.Variants)
        {
            Assert.Equal(variant.Elements.Count, variant.Elements.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count());

            foreach (var (name, value) in variant.When)
            {
                var input = template.FindInput(name)!;

                switch (input.Kind)
                {
                    case CalcInputKind.Choice:
                        Assert.Contains(value, input.Choices ?? []);
                        break;
                    case CalcInputKind.Boolean:
                        Assert.Contains(value, new[] { CalcDiagramReader.BooleanTrue, CalcDiagramReader.BooleanFalse });
                        break;
                    default:
                        Assert.True(value.Length == 0 && input.IsOptional, $"{templateId}: a condition on '{name}' may only test that the optional input is empty.");
                        break;
                }
            }

            foreach (var element in variant.Elements)
            {
                var at = element.LabelAnchor;
                Assert.True(at.X is >= 0 and <= CalculationDiagramSpec.Width && at.Y is >= 0 and <= CalculationDiagramSpec.Height, $"{templateId}/{element.Id} is placed off the sheet at ({at.X}, {at.Y}).");
            }
        }

        // A choice that selects variants has a drawing for each of its members.
        foreach (var key in spec.Variants.SelectMany(v => v.When.Keys).Distinct())
        {
            if (template.FindInput(key) is { Kind: CalcInputKind.Choice, Choices: { } choices })
                Assert.All(choices, c => Assert.Contains(spec.Variants, v => v.When.TryGetValue(key, out var w) && w == c));
        }
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Read_Against_The_Defaults_Every_Required_Label_Shows_A_Value(string templateId)
    {
        var template = Templates.Single(t => t.Id == templateId);
        var reading = CalcDiagramReader.Read(template, CalcTemplateHasher.DefaultInputs(template))!;

        Assert.StartsWith($"{template.Title}. {CalculationDiagramReader.NotToScale}", reading.Summary, StringComparison.Ordinal);

        foreach (var shape in reading.Shapes.Where(s => s.Element.InputName is { } n && s.Label is not null && template.FindInput(n) is { IsOptional: false }))
            Assert.DoesNotContain($"= {CalculationDiagramReader.Unknown}", shape.Label!, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Beam_Diagram_Follows_The_Case_And_Labels_The_Values_As_Entered()
    {
        var beam = new BeamFormulasCalculator().Template;
        var inputs = CalcTemplateHasher.DefaultInputs(beam).With(CalcInputValue.Quantity("L", 2, "m")).With(CalcInputValue.Choice("case", BeamFormulasCalculator.CantPoint));
        var reading = CalcDiagramReader.Read(beam, inputs)!;

        Assert.StartsWith("Cantilever fixed at the left, point load", reading.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("L = 2 m", reading.LabelFor("L"));
        Assert.Contains(reading.Shapes, s => s.Element is DiagramSupport { Kind: DiagramSupportKind.Fixed });
        Assert.DoesNotContain(reading.Shapes, s => s.Element is DiagramSupport { Kind: DiagramSupportKind.Roller });
    }

    [Fact]
    public void An_Empty_Optional_Input_Selects_Its_Drawing_And_Reads_Not_Given()
    {
        var wall = new Tempest.Calc.BuildingServices.PlaneWallCalculator().Template;
        var noFilms = new CalcInputSet(CalcTemplateHasher.DefaultInputs(wall).Values.Where(v => v.Symbol is not ("h_1" or "h_2")));
        var reading = CalcDiagramReader.Read(wall, noFilms)!;

        Assert.Contains("no films", reading.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("h_1 = not given", reading.LabelFor("h_1"));
        Assert.Equal("layers = 1 row", reading.LabelFor("layers"));
    }

    private static (CalcSheetView View, CalcCatalogue Catalogue) NewView()
    {
        var catalogue = new CalcCatalogue(CalcCalculatorComposition.All());
        var view = new CalcSheetView();
        view.ShowCatalogue(catalogue.Current());
        return (view, catalogue);
    }

    [AvaloniaFact]
    public void The_Form_Draws_The_Diagram_Beside_The_Inputs_And_Redraws_As_They_Change()
    {
        var (view, catalogue) = NewView();
        var window = new Window { Width = 1600, Height = 1000, Content = view };
        window.Show();

        try
        {
            view.ShowTemplate(catalogue.Get(BeamFormulasCalculator.Id));
            Assert.True(view.Diagram.IsVisible);
            Assert.True(view.Diagram.HasDiagram);
            Assert.Contains("L = 6 m", view.Diagram.Summary, StringComparison.Ordinal);

            view.SetField("L", text: "4");
            Assert.Equal("L = 4 m", view.Diagram.LabelFor("L"));

            view.SetField("case", choice: BeamFormulasCalculator.FixedUdl);
            Assert.StartsWith("Fixed at both ends", view.Diagram.Reading!.Variant.Caption, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Highlights_The_Shapes_And_A_Shape_Focuses_Its_Field()
    {
        var (view, catalogue) = NewView();
        var window = new Window { Width = 1600, Height = 1000, Content = view };
        window.Show();

        try
        {
            view.ShowTemplate(catalogue.Get(BeamFormulasCalculator.Id));
            var span = (TextBox)view.FieldControl("L")!;
            span.Focus();
            Assert.Equal("L", view.Diagram.HighlightedInput);
            Assert.True(view.Diagram.Draws("L"));

            var modulus = (TextBox)view.FieldControl("E")!;
            view.Diagram.Activate("E");
            Assert.True(modulus.IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Archive_Shows_No_Diagram()
    {
        var (view, catalogue) = NewView();
        view.ShowTemplate(catalogue.Get(BeamFormulasCalculator.Id));
        var at = DateTimeOffset.UnixEpoch;
        var archive = new CalcInstance(Guid.NewGuid(), CalcInstance.LegacyReferencePrefix + "calc.beam-deflection@1", "legacy", null, "legacy run", "A", [], [], [], [], [], CalcInstanceStatus.Draft, null, null, "s.k", at, at);
        view.ShowArchive(archive, "archive sheet");
        Assert.False(view.Diagram.IsVisible);
        Assert.Contains(view.GetLogicalDescendants().OfType<SelectableTextBlock>(), t => AutomationProperties.GetName(t) == CalcSheetView.ArchiveSheetName);
    }
}
