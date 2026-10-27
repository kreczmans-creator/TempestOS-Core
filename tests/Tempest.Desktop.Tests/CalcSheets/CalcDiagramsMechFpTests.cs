using Tempest.Calc.FluidPower;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Core.Calculations.Modules.Diagrams;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// DI-08: the reference diagrams of the mechanical and fluid-power templates
/// drawn in v0.26.0 (bearing life, mechanism, shaft, snap-fit, spring, weld
/// group, accumulator, cylinder, O-ring gland). Read against the defaults,
/// every required input the diagram binds is labelled with its value; a
/// choice that changes the geometry draws the variant its value selects.
/// </summary>
public sealed class CalcDiagramsMechFpTests
{
    private static readonly CalcCatalogue Catalogue = new(CalcCalculatorComposition.All());

    public static TheoryData<string> TemplateIds =>
    [
        BearingLifeCalculator.Id, MechanismCalculator.Id, ShaftCalculator.Id, SnapFitCalculator.Id, SpringCalculator.Id, WeldGroupCalculator.Id,
        AccumulatorCalculator.Id, CylinderCalculator.Id, ORingGlandCalculator.Id,
    ];

    private static CalcTemplate Template(string id) => Catalogue.Find(id)!.Template;

    private static CalculationDiagramReading Read(string id, params CalcInputValue[] overrides)
    {
        var template = Template(id);
        var inputs = CalcTemplateHasher.DefaultInputs(template);

        foreach (var value in overrides)
            inputs = inputs.With(value);

        return CalcDiagramReader.Read(template, inputs)!;
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Read_Against_The_Defaults_Every_Required_Input_The_Diagram_Binds_Is_Labelled_With_Its_Value(string templateId)
    {
        var template = Template(templateId);
        var reading = Read(templateId);
        var labelled = reading.Shapes.Where(s => s.Element is { InputName: not null, Symbol: not null }).ToList();

        Assert.NotEmpty(labelled);

        foreach (var shape in labelled)
        {
            var input = template.FindInput(shape.Element.InputName!)!;
            Assert.StartsWith(shape.Element.Symbol + " = ", shape.Label, StringComparison.Ordinal);

            if (!input.IsOptional)
                Assert.DoesNotContain(CalculationDiagramReader.Unknown, shape.Label!, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Bearing_Diagram_Labels_The_Loads_And_Follows_The_Bearing_Type()
    {
        var ball = Read(BearingLifeCalculator.Id);
        Assert.StartsWith("A ball bearing", ball.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("F_r = 2 kN", ball.LabelFor("F_r"));
        Assert.Equal("n = 1000 rpm", ball.LabelFor("n"));
        Assert.Equal("L_req = 10000 h", ball.LabelFor("L_req"));
        Assert.Equal("duty = not given", ball.LabelFor("duty"));
        Assert.Contains(ball.Shapes, s => s.Element is DiagramCircle { InputName: "type", Solid: true });

        var roller = Read(BearingLifeCalculator.Id, CalcInputValue.Choice("type", BearingLifeCalculator.Roller));
        Assert.StartsWith("A roller bearing", roller.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(roller.Shapes, s => s.Element is DiagramPlate { InputName: "type" });
        Assert.DoesNotContain(roller.Shapes, s => s.Element is DiagramCircle { InputName: "type" });
    }

    [Fact]
    public void The_Mechanism_Diagram_Follows_The_Kind_Of_Mechanism_And_The_Follower()
    {
        var fourBar = Read(MechanismCalculator.Id);
        Assert.StartsWith("A four-bar linkage", fourBar.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("L1 = 120 mm", fourBar.LabelFor("L1"));
        Assert.Equal("n_in = 120 rpm", fourBar.LabelFor("n_in"));
        Assert.Contains(fourBar.Shapes, s => s.Element is DiagramMember { InputName: "L4" });

        var slider = Read(MechanismCalculator.Id, CalcInputValue.Choice("mode", MechanismCalculator.SliderCrank));
        Assert.StartsWith("A slider-crank", slider.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(slider.Shapes, s => s.Element is DiagramPlate { InputName: "m_slider" });
        Assert.DoesNotContain(slider.Shapes, s => s.Element.InputName == "L4");

        var roller = Read(MechanismCalculator.Id, CalcInputValue.Choice("mode", MechanismCalculator.CamMode), CalcInputValue.Choice("follower", MechanismCalculator.Roller));
        Assert.Contains("roller follower", roller.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(roller.Shapes, s => s.Element is DiagramCircle { InputName: "R_r" });

        var flat = Read(MechanismCalculator.Id, CalcInputValue.Choice("mode", MechanismCalculator.CamMode), CalcInputValue.Choice("follower", MechanismCalculator.FlatFaced));
        Assert.Contains("flat-faced follower", flat.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(flat.Shapes, s => s.Element is DiagramPlate { InputName: "follower" });
        Assert.DoesNotContain(flat.Shapes, s => s.Element.InputName == "R_r");
    }

    [Fact]
    public void The_Shaft_Diagram_Labels_The_Bearings_And_The_Row_Lists()
    {
        var reading = Read(ShaftCalculator.Id);
        Assert.Equal("x_A = 0 mm", reading.LabelFor("x_A"));
        Assert.Equal("x_B = 400 mm", reading.LabelFor("x_B"));
        Assert.Equal("segments = 1 row", reading.LabelFor("segments"));
        Assert.Equal("loads = 1 row", reading.LabelFor("loads"));
        Assert.Equal("n = 1500 rpm", reading.LabelFor("n"));
        Assert.Contains(reading.Shapes, s => s.Element is DiagramSupport { Kind: DiagramSupportKind.Pinned, InputName: "x_A" });
        Assert.Contains(reading.Shapes, s => s.Element is DiagramSupport { Kind: DiagramSupportKind.Roller, InputName: "x_B" });
    }

    [Theory]
    [InlineData(SnapFitCalculator.Constant, "constant section", "h")]
    [InlineData(SnapFitCalculator.TaperedThickness, "tapering in thickness", "h_ratio")]
    [InlineData(SnapFitCalculator.TaperedWidth, "tapering in width", "b_ratio")]
    [InlineData(SnapFitCalculator.Annular, "annular", "D_ring")]
    [InlineData(SnapFitCalculator.Torsion, "torsion-bar", "r_bar")]
    public void The_Snap_Fit_Diagram_Follows_The_Form(string mode, string captionPart, string drawnInput)
    {
        var reading = Read(SnapFitCalculator.Id, CalcInputValue.Choice("mode", mode));
        Assert.Contains(captionPart, reading.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(reading.Shapes, s => s.Element.InputName == drawnInput);
        Assert.Equal("E_s = 2400 MPa", reading.LabelFor("Es"));
        Assert.Equal("μ = 0.3", reading.LabelFor("mu"));
    }

    [Fact]
    public void The_Spring_Diagram_Follows_The_Kind_Of_Spring()
    {
        var compression = Read(SpringCalculator.Id);
        Assert.StartsWith("A compression spring", compression.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("d = 2 mm", compression.LabelFor("d"));
        Assert.Equal("L0 = 45 mm", compression.LabelFor("L0"));
        Assert.Equal("F_2 = 40 N", compression.LabelFor("F_2"));
        Assert.Contains(compression.Shapes, s => s.Element is DiagramSpring { InputName: "n_active" });

        var extension = Read(SpringCalculator.Id, CalcInputValue.Choice("mode", SpringCalculator.Extension));
        Assert.StartsWith("An extension spring", extension.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(extension.Shapes, s => s.Element is DiagramCircle { InputName: "r_h1" });

        var torsion = Read(SpringCalculator.Id, CalcInputValue.Choice("mode", SpringCalculator.Torsion));
        Assert.StartsWith("A torsion spring", torsion.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(torsion.Shapes, s => s.Element is DiagramMember { InputName: "l_leg_1" });
        Assert.Contains(torsion.Shapes, s => s.Element is DiagramMoment { InputName: "M_2" });
    }

    [Fact]
    public void The_Weld_Group_Diagram_Labels_The_Welds_And_The_Load_Point()
    {
        var reading = Read(WeldGroupCalculator.Id);
        Assert.Equal("welds = 2 rows", reading.LabelFor("welds"));
        Assert.Equal("e_x = 200 mm", reading.LabelFor("e_x"));
        Assert.Equal("e_y = 50 mm", reading.LabelFor("e_y"));
        Assert.Equal("s = 6 mm", reading.LabelFor("s"));
        Assert.Equal("f_w = 220 MPa", reading.LabelFor("f_w"));
        Assert.Equal(2, reading.Shapes.Count(s => s.Element is DiagramMember { InputName: "welds" }));
    }

    [Fact]
    public void The_Accumulator_Diagram_Labels_The_Pressures_And_The_Volume()
    {
        var reading = Read(AccumulatorCalculator.Id);
        Assert.Equal("p_0 = 90 bar", reading.LabelFor("p_0"));
        Assert.Equal("p_1 = 100 bar", reading.LabelFor("p_1"));
        Assert.Equal("p_2 = 210 bar", reading.LabelFor("p_2"));
        Assert.Equal("ΔV = 14 L", reading.LabelFor("dV"));
        Assert.Equal("mode = " + AccumulatorCalculator.ModeEnergy, reading.LabelFor("mode"));
    }

    [Fact]
    public void The_Cylinder_Diagram_Follows_The_Extend_Connection()
    {
        var standard = Read(CylinderCalculator.Id);
        Assert.Contains("extend connection standard", standard.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("D = 100 mm", standard.LabelFor("D"));
        Assert.Equal("d_r = 70 mm", standard.LabelFor("d_r"));
        Assert.Equal("F_push = 80 kN", standard.LabelFor("F_push"));
        Assert.Contains(standard.Shapes, s => s.Element is DiagramPointLoad { InputName: "p_back" });
        Assert.DoesNotContain(standard.Shapes, s => s.Element is DiagramPolyline { InputName: "mode" });

        var regenerative = Read(CylinderCalculator.Id, CalcInputValue.Choice("mode", CylinderCalculator.Regenerative));
        Assert.Contains("regenerative extend", regenerative.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(regenerative.Shapes, s => s.Element is DiagramPolyline { InputName: "mode" });
        Assert.DoesNotContain(regenerative.Shapes, s => s.Element.InputName == "p_back");
    }

    [Theory]
    [InlineData(ORingGlandCalculator.TypePiston, "A piston seal", "D_c")]
    [InlineData(ORingGlandCalculator.TypeRod, "A rod seal", "D_c")]
    [InlineData(ORingGlandCalculator.TypeFace, "A face seal", "h_f")]
    public void The_O_Ring_Gland_Diagram_Follows_The_Gland_Arrangement(string type, string captionStart, string drawnInput)
    {
        var reading = Read(ORingGlandCalculator.Id, CalcInputValue.Choice("type", type));
        Assert.StartsWith(captionStart, reading.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(reading.Shapes, s => s.Element is DiagramDimension && s.Element.InputName == drawnInput);
        Assert.Equal("W = 3.5 mm", reading.LabelFor("W"));
        Assert.Equal("b = 4.8 mm", reading.LabelFor("b"));
        Assert.Equal("p_op = 160 bar", reading.LabelFor("p_op"));
    }
}
