using Tempest.Calc.BuildingServices;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Calculations.Modules.Diagrams;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// DI-08 (DC-21): the reference diagrams of the Addendum A and B structural
/// and building-services templates. Read against the defaults, every required
/// input a diagram labels shows its value; each choice that changes the
/// geometry draws the variant its value selects.
/// </summary>
public sealed class CalcDiagramsStructBsTests
{
    private static CalcTemplate TemplateOf(string id) => CalcCalculatorComposition.All().Single(c => c.Template.Id == id).Template;

    private static CalcInputSet Defaults(CalcTemplate template) => CalcTemplateHasher.DefaultInputs(template);

    public static TheoryData<string> StructBsIds =>
    [
        BasePlateCalculator.Id, ConnectionCalculator.Id, LiftingLugCalculator.Id, LoadCombinationsCalculator.Id, LtbInteractionCalculator.Id, PlateBendingCalculator.Id,
        GlaserCalculator.Id, HeatExchangerCalculator.Id, InsulationCalculator.Id,
    ];

    [Theory]
    [MemberData(nameof(StructBsIds))]
    public void Read_Against_The_Defaults_Every_Required_Input_Is_Labelled_With_Its_Value(string id)
    {
        var template = TemplateOf(id);
        var inputs = Defaults(template);
        var reading = CalcDiagramReader.Read(template, inputs)!;
        var labelled = reading.Shapes.Where(s => s.Element is { InputName: not null, Symbol: not null }).ToList();
        Assert.NotEmpty(labelled);

        foreach (var shape in labelled.Where(s => template.FindInput(s.Element.InputName!) is { IsOptional: false }))
        {
            var expected = $"{shape.Element.Symbol} = {CalcDiagramReader.ValueText(template, shape.Element.InputName!, inputs)}";
            Assert.Equal(expected, shape.Label);
            Assert.DoesNotContain($"= {CalculationDiagramReader.Unknown}", shape.Label!, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Base_Plate_Draws_The_Column_Type_Chosen_And_Labels_The_Plate_As_Entered()
    {
        var template = TemplateOf(BasePlateCalculator.Id);
        var inputs = Defaults(template).With(CalcInputValue.Quantity("B", 500, "mm"));

        var chs = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("column", BasePlateCalculator.ColumnChs)))!;
        Assert.Contains("CHS", chs.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(chs.Shapes, s => s.Element is DiagramCircle { InputName: "D" });
        Assert.Equal("B = 500 mm", chs.LabelFor("B"));
        Assert.Equal("D = 220 mm", chs.LabelFor("D"));

        var rhs = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("column", BasePlateCalculator.ColumnRhs)))!;
        Assert.Contains("RHS", rhs.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(rhs.Shapes, s => s.Element is DiagramPlate { InputName: "t_wall" });
        Assert.DoesNotContain(rhs.Shapes, s => s.Element.InputName == "t_w");

        var ih = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("column", BasePlateCalculator.ColumnIH)))!;
        Assert.Contains("I/H", ih.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(ih.Shapes, s => s.Element is DiagramPlate { InputName: "t_w" });
        Assert.Equal("N_Ed = 300 kN", ih.LabelFor("N_Ed"));
    }

    [Fact]
    public void The_Connection_Draws_The_Hole_Type_Chosen_And_Labels_The_Distances_As_Entered()
    {
        var template = TemplateOf(ConnectionCalculator.Id);
        var inputs = Defaults(template).With(CalcInputValue.Quantity("e_1", 55, "mm"));

        var slot = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("hole_type", ConnectionCalculator.HoleSlot)))!;
        Assert.Contains("slots", slot.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(slot.Shapes, s => s.Element is DiagramPolyline { InputName: "hole_type" });
        Assert.Equal("e_1 = 55 mm", slot.LabelFor("e_1"));

        var oversize = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("hole_type", ConnectionCalculator.HoleOversize)))!;
        Assert.Contains(oversize.Shapes, s => s.Element is DiagramCircle { InputName: "hole_type", Radius: 13 });

        var normal = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("hole_type", ConnectionCalculator.HoleNormal)))!;
        Assert.Contains(normal.Shapes, s => s.Element is DiagramCircle { InputName: "hole_type", Radius: 10 });
        Assert.Equal("size = M20", normal.LabelFor("size"));
    }

    [Fact]
    public void The_Lifting_Diagram_Draws_One_Sling_Per_Lug_On_The_Load_Or_On_A_Spreader_Beam()
    {
        var template = TemplateOf(LiftingLugCalculator.Id);
        var inputs = Defaults(template).With(CalcInputValue.Quantity("theta", 25, "deg"));

        var three = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("n_lugs", "3")).With(CalcInputValue.Switch("beam", true)))!;
        Assert.Contains("3 lugs on a spreader beam", three.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal(3, three.Shapes.Count(s => s.Element is DiagramMember { InputName: "theta" }));
        Assert.Contains(three.Shapes, s => s.Element is DiagramMember { Id: "spreader", InputName: "A_beam" });
        Assert.StartsWith("θ = 25", three.LabelFor("theta"), StringComparison.Ordinal);

        var one = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("n_lugs", "1")).With(CalcInputValue.Switch("beam", false)))!;
        Assert.Contains("one lug on the load", one.Variant.Caption, StringComparison.Ordinal);
        Assert.Single(one.Shapes, s => s.Element is DiagramMember { InputName: "theta" });
        Assert.Contains(one.Shapes, s => s.Element is DiagramPlate { Id: "load" });
        Assert.DoesNotContain(one.Shapes, s => s.Element.InputName == "A_beam");
        Assert.Equal("n_lugs = 1", one.LabelFor("n_lugs"));
    }

    [Fact]
    public void The_Load_Combinations_Diagram_Labels_The_Reduction_Factor_Only_For_6_10a_And_6_10b()
    {
        var template = TemplateOf(LoadCombinationsCalculator.Id);
        var inputs = Defaults(template).With(CalcInputValue.Of("gamma_Q", 1.4));

        var ab = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("method", LoadCombinationsCalculator.Method610ab)))!;
        Assert.Contains("6.10b", ab.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("ξ = 0.85", ab.LabelFor("xi"));
        Assert.Equal("γ_Q = 1.4", ab.LabelFor("gamma_Q"));
        Assert.Equal(4, ab.Shapes.Count(s => s.Element.InputName == "actions" && s.Element is DiagramPointLoad or DiagramDistributedLoad));

        var single = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("method", LoadCombinationsCalculator.Method610)))!;
        Assert.Contains("expression 6.10 with", single.Variant.Caption, StringComparison.Ordinal);
        Assert.Null(single.LabelFor("xi"));
    }

    [Fact]
    public void The_Ltb_Diagram_Draws_The_End_Moments_The_Mode_Gives()
    {
        var template = TemplateOf(LtbInteractionCalculator.Id);
        var inputs = Defaults(template).With(CalcInputValue.Quantity("L", 4, "m"));

        var ends = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("moment_mode", LtbInteractionCalculator.ModeEnds)))!;
        Assert.Contains("M_y,A and M_y,B", ends.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(ends.Shapes, s => s.Element is DiagramMoment { InputName: "M_y_A" });
        Assert.Contains(ends.Shapes, s => s.Element is DiagramMoment { InputName: "M_y_B" });
        Assert.DoesNotContain(ends.Shapes, s => s.Element.InputName == "M_y_Ed");
        Assert.Equal("L = 4 m", ends.LabelFor("L"));

        var psi = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("moment_mode", LtbInteractionCalculator.ModePsi)))!;
        Assert.Contains("ψ_y", psi.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(psi.Shapes, s => s.Element is DiagramMoment { InputName: "M_y_Ed" });
        Assert.Contains(psi.Shapes, s => s.Element is DiagramMoment { InputName: "psi_y" });
        Assert.Equal($"h = {CalcDiagramReader.ValueText(template, "h", inputs)}", psi.LabelFor("h"));
    }

    [Fact]
    public void The_Plate_Diagram_Follows_The_Shape_Support_Load_And_Basis()
    {
        var template = TemplateOf(PlateBendingCalculator.Id);
        var inputs = Defaults(template).With(CalcInputValue.Quantity("a", 300, "mm"));

        var ring = CalcDiagramReader.Read(template, inputs
            .With(CalcInputValue.Choice("shape", PlateBendingCalculator.Circular))
            .With(CalcInputValue.Choice("support", PlateBendingCalculator.Clamped))
            .With(CalcInputValue.Choice("load_type", PlateBendingCalculator.Ring)))!;
        Assert.StartsWith("A circular plate of radius a, clamped all round, under a ring load F", ring.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(ring.Shapes, s => s.Element is DiagramPolyline { InputName: "r_ring" });
        Assert.Equal(2, ring.Shapes.Count(s => s.Element is DiagramSupport { Kind: DiagramSupportKind.Fixed }));
        Assert.Equal("a = 300 mm", ring.LabelFor("a"));
        Assert.Null(ring.LabelFor("b"));
        Assert.Null(ring.LabelFor("q"));

        var patch = CalcDiagramReader.Read(template, inputs
            .With(CalcInputValue.Choice("shape", PlateBendingCalculator.Rectangular))
            .With(CalcInputValue.Choice("support", PlateBendingCalculator.Simple))
            .With(CalcInputValue.Choice("load_type", PlateBendingCalculator.Patch))
            .With(CalcInputValue.Choice("load_basis", PlateBendingCalculator.BasisForce)))!;
        Assert.Contains("central patch u by v given as the total force F", patch.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(patch.Shapes, s => s.Element is DiagramPlate { InputName: "u" });
        Assert.Contains(patch.Shapes, s => s.Element is DiagramDistributedLoad { InputName: "F" });
        Assert.Equal(2, patch.Shapes.Count(s => s.Element is DiagramSupport { Kind: DiagramSupportKind.Pinned }));
        Assert.Equal("b = 400 mm", patch.LabelFor("b"));

        var pressure = CalcDiagramReader.Read(template, inputs
            .With(CalcInputValue.Choice("shape", PlateBendingCalculator.Rectangular))
            .With(CalcInputValue.Choice("load_type", PlateBendingCalculator.Uniform))
            .With(CalcInputValue.Choice("load_basis", PlateBendingCalculator.BasisPressure)))!;
        Assert.Contains(pressure.Shapes, s => s.Element is DiagramDistributedLoad { InputName: "q" });
        Assert.Null(pressure.LabelFor("F"));

        // A ring load on a rectangle is refused by the sheet; the drawing falls back to the last variant.
        var fallback = CalcDiagramReader.Read(template, inputs
            .With(CalcInputValue.Choice("shape", PlateBendingCalculator.Rectangular))
            .With(CalcInputValue.Choice("load_type", PlateBendingCalculator.Ring)))!;
        Assert.Same(CalcDiagrams.For(PlateBendingCalculator.Id)!.Variants[^1], fallback.Variant);
    }

    [Fact]
    public void The_Glaser_Diagram_Labels_The_Inside_Humidity_The_Mode_Uses()
    {
        var template = TemplateOf(GlaserCalculator.Id);
        var inputs = Defaults(template).With(CalcInputValue.Quantity("T_int", 21, "degC"));

        var constant = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("humidity_mode", GlaserCalculator.ConstantRh)))!;
        Assert.Contains("constant relative humidity", constant.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("RH_int = 50 %", constant.LabelFor("RH_int"));
        Assert.Null(constant.LabelFor("dv_a"));
        Assert.Equal("T_int = 21 °C", constant.LabelFor("T_int"));
        Assert.Equal("climate = 12 rows", constant.LabelFor("climate"));

        var excess = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("humidity_mode", GlaserCalculator.MoistureExcess)))!;
        Assert.Contains("moisture excess", excess.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("Δv_a = not given", excess.LabelFor("dv_a"));
        Assert.Equal("θ_b = not given", excess.LabelFor("theta_b"));
        Assert.Null(excess.LabelFor("RH_int"));
    }

    [Fact]
    public void The_Heat_Exchanger_Diagram_Follows_The_Arrangement_And_Labels_The_Area_Or_The_Duty()
    {
        var template = TemplateOf(HeatExchangerCalculator.Id);
        var inputs = Defaults(template).With(CalcInputValue.Quantity("m_h", 2, "kg/s"));

        var cross = CalcDiagramReader.Read(template, inputs
            .With(CalcInputValue.Choice("arrangement", HeatExchangerCalculator.Crossflow))
            .With(CalcInputValue.Choice("mode", HeatExchangerCalculator.Sizing)))!;
        Assert.StartsWith("Crossflow", cross.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains("sizing", cross.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(cross.Shapes, s => s.Element is DiagramPointLoad { Id: "cold-in", Direction: DiagramDirection.Up });
        Assert.NotNull(cross.LabelFor("Q_req"));
        Assert.Null(cross.LabelFor("A"));
        Assert.Equal("ṁ_h = 2 kg/s", cross.LabelFor("m_h"));

        var cold = CalcDiagramReader.Read(template, inputs
            .With(CalcInputValue.Choice("arrangement", HeatExchangerCalculator.Isothermal))
            .With(CalcInputValue.Choice("const_side", HeatExchangerCalculator.ColdConstant))
            .With(CalcInputValue.Choice("mode", HeatExchangerCalculator.Rating)))!;
        Assert.StartsWith("The cold stream at constant temperature", cold.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains("rating", cold.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal($"A = {CalcDiagramReader.ValueText(template, "A", inputs)}", cold.LabelFor("A"));
        Assert.Null(cold.LabelFor("Q_req"));

        var shell = CalcDiagramReader.Read(template, inputs.With(CalcInputValue.Choice("arrangement", HeatExchangerCalculator.ShellTube)))!;
        Assert.StartsWith("Shell and tube", shell.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(shell.Shapes, s => s.Element is DiagramPolyline { Id: "tubes" });
    }

    [Fact]
    public void The_Insulation_Diagram_Draws_The_Surface_And_The_Source_Of_The_Surface_Coefficient()
    {
        var template = TemplateOf(InsulationCalculator.Id);
        var inputs = Defaults(template).With(CalcInputValue.Quantity("t_chosen", 50, "mm"));

        var pipe = CalcDiagramReader.Read(template, inputs
            .With(CalcInputValue.Choice("geometry", InsulationCalculator.Pipe))
            .With(CalcInputValue.Choice("h_mode", InsulationCalculator.HGiven)))!;
        Assert.StartsWith("A pipe", pipe.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(pipe.Shapes, s => s.Element is DiagramCircle { InputName: "D_i" });
        Assert.Equal("t_chosen = 50 mm", pipe.LabelFor("t_chosen"));
        Assert.NotNull(pipe.LabelFor("h_se"));
        Assert.Null(pipe.LabelFor("eps"));
        Assert.NotNull(pipe.LabelFor("q_max_m"));
        Assert.Null(pipe.LabelFor("L_surf"));

        var flat = CalcDiagramReader.Read(template, inputs
            .With(CalcInputValue.Choice("geometry", InsulationCalculator.Flat))
            .With(CalcInputValue.Choice("h_mode", InsulationCalculator.HCorrelation)))!;
        Assert.StartsWith("A flat surface", flat.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(flat.Shapes, s => s.Element is DiagramDimension { InputName: "L_surf" });
        Assert.Equal("L_surf = 1 m", flat.LabelFor("L_surf"));
        Assert.Equal("ε = 0.9", flat.LabelFor("eps"));
        Assert.Null(flat.LabelFor("h_se"));
        Assert.NotNull(flat.LabelFor("q_max_A"));
    }
}
