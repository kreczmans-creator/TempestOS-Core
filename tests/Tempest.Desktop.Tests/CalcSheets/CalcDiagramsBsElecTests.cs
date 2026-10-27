using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Calculations.Modules.Diagrams;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// DI-08 (DC-21): the reference diagrams of the building-services and
/// electrical templates Addenda A and B added, read against the form: every
/// required input labelled with its value at the defaults, and each choice
/// that changes the drawing selecting its own variant.
/// </summary>
public sealed class CalcDiagramsBsElecTests
{
    private static readonly IReadOnlyList<string> Ids =
    [
        PipeExpansionCalculator.Id, RoomHeatLossCalculator.Id, SealedSystemCalculator.Id,
        ConductorSizingCalculator.Id, ContainmentCalculator.Id, FaultProtectionCalculator.Id,
        LoadBudgetCalculator.Id, MotorStartCalculator.Id, PcbTraceCalculator.Id,
    ];

    private static CalcTemplate Template(string id) => CalcCalculatorComposition.All().Single(c => c.Template.Id == id).Template;

    private static CalculationDiagramReading Read(string id, params CalcInputValue[] with)
    {
        var template = Template(id);
        var inputs = CalcTemplateHasher.DefaultInputs(template);

        foreach (var value in with)
            inputs = inputs.With(value);

        return CalcDiagramReader.Read(template, inputs)!;
    }

    [Fact]
    public void Every_Group_Template_Has_A_Diagram_Whose_Required_Labels_Read_A_Value_At_The_Defaults()
    {
        foreach (var id in Ids)
        {
            var template = Template(id);
            var reading = Read(id);
            Assert.DoesNotContain(id, CalcDiagramsTests.WithoutDiagramYet);

            foreach (var shape in reading.Shapes.Where(s => s.Element.InputName is { } n && s.Label is not null && template.FindInput(n) is { IsOptional: false }))
                Assert.DoesNotContain($"= {CalculationDiagramReader.Unknown}", shape.Label!, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Pipe_Expansion_Diagram_Follows_The_Layout_And_The_Second_Run()
    {
        var lBend = Read(PipeExpansionCalculator.Id);
        Assert.Contains("L-bend", lBend.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("L = 50 m", lBend.LabelFor("L"));
        Assert.Equal("H = 6 m", lBend.LabelFor("H"));
        Assert.Equal("OD = 88.9 mm", lBend.LabelFor("OD"));
        Assert.DoesNotContain(lBend.Shapes, s => s.Element.Id == "run-2");

        var zBend = Read(PipeExpansionCalculator.Id, CalcInputValue.Quantity("L2", 20, "m"));
        Assert.Contains("Z-bend", zBend.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("L2 = 20 m", zBend.LabelFor("L2"));

        var loop = Read(PipeExpansionCalculator.Id, CalcInputValue.Choice("layout", PipeExpansionCalculator.UpLoop));
        Assert.StartsWith("A U-loop", loop.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(loop.Shapes, s => s.Element.Id == "loop-base");

        var bellows = Read(PipeExpansionCalculator.Id, CalcInputValue.Choice("layout", PipeExpansionCalculator.AxialBellows));
        Assert.StartsWith("An axial bellows", bellows.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(bellows.Shapes, s => s.Element is DiagramSpring { InputName: "k_b" });
        Assert.Equal("P_b = not given", bellows.LabelFor("P_b"));
    }

    [Fact]
    public void The_Room_Heat_Loss_Diagram_Counts_The_Elements_And_Layers()
    {
        var reading = Read(RoomHeatLossCalculator.Id);
        Assert.Equal("elements = 2 rows", reading.LabelFor("elements"));
        Assert.EndsWith(" rows", reading.LabelFor("layers"), StringComparison.Ordinal);
        Assert.Equal("V = 40 m³", reading.LabelFor("V"));
        Assert.Equal("T_int = 21 °C", reading.LabelFor("T_int"));
        Assert.Equal("T_supply = not given", reading.LabelFor("T_supply"));
    }

    [Fact]
    public void The_Sealed_System_Diagram_Counts_The_Rooms_And_Reads_The_Static_Height()
    {
        var reading = Read(SealedSystemCalculator.Id);
        Assert.Equal("rooms = 1 row", reading.LabelFor("rooms"));
        Assert.Equal("h_static = 5 m", reading.LabelFor("h_static"));
        Assert.Equal("T_flow = 75 °C", reading.LabelFor("T_flow"));
        Assert.Equal("V_vessel = not given", reading.LabelFor("V_vessel"));
    }

    [Theory]
    [InlineData(ConductorSizingCalculator.ThreePhase, 3)]
    [InlineData(ConductorSizingCalculator.SinglePhase, 2)]
    [InlineData(ConductorSizingCalculator.Dc, 2)]
    public void The_Conductor_Sizing_Diagram_Draws_The_Conductors_Of_The_Supply(string supply, int conductors)
    {
        var reading = Read(ConductorSizingCalculator.Id, CalcInputValue.Choice("supply", supply));
        Assert.Equal(supply, reading.Variant.When["supply"]);
        Assert.Equal(conductors, reading.Shapes.Count(s => s.Element is DiagramMember { Id: var id } && id.StartsWith("conductor-", StringComparison.Ordinal)));
        Assert.Equal("L = 20 m", reading.LabelFor("L"));
        Assert.Equal("P = 7.5 kW", reading.LabelFor("P"));
        Assert.Equal("device = MCB type C", reading.LabelFor("device"));
    }

    [Fact]
    public void The_Containment_Diagram_Follows_The_Containment_Type()
    {
        var conduit = Read(ContainmentCalculator.Id);
        Assert.StartsWith("A round conduit", conduit.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("cables = 1 row", conduit.LabelFor("cables"));
        Assert.Equal("D_int = 25 mm", conduit.LabelFor("D_int"));
        Assert.Equal("route = 5 rows", conduit.LabelFor("route"));

        var trunking = Read(ContainmentCalculator.Id, CalcInputValue.Choice("cont_type", ContainmentCalculator.Trunking));
        Assert.StartsWith("A trunking or tray", trunking.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(trunking.Shapes, s => s.Element is DiagramPlate { Id: "trunking" });
        Assert.Equal("W_int = not given", trunking.LabelFor("W_int"));
    }

    [Theory]
    [InlineData(FaultProtectionCalculator.McbB, false)]
    [InlineData(FaultProtectionCalculator.McbC, false)]
    [InlineData(FaultProtectionCalculator.McbD, false)]
    [InlineData(FaultProtectionCalculator.Curve, true)]
    public void The_Fault_Protection_Diagram_Adds_The_Curve_For_A_Fuse_Or_Mccb(string device, bool curve)
    {
        var reading = Read(FaultProtectionCalculator.Id, CalcInputValue.Choice("device", device));
        Assert.Equal(device, reading.Variant.When["device"]);
        Assert.Equal(curve, reading.Shapes.Any(s => s.Element is DiagramAxes));
        Assert.Equal("I_n = 32 A", reading.LabelFor("I_n"));
        Assert.Equal("L = 20 m", reading.LabelFor("L"));
        Assert.Equal("S_line = 2.5 mm²", reading.LabelFor("S_line"));
    }

    [Fact]
    public void The_Load_Budget_Diagram_Feeds_The_Power_Supply_From_The_Chosen_Bus()
    {
        var none = Read(LoadBudgetCalculator.Id);
        Assert.Equal("loads = 4 rows", none.LabelFor("loads"));
        Assert.Equal("U_3ph = 400 V", none.LabelFor("U_3ph"));
        Assert.DoesNotContain(none.Shapes, s => s.Element.Id == "psu-feed");

        var threePhase = Read(LoadBudgetCalculator.Id, CalcInputValue.Choice("psu_bus", LoadBudgetCalculator.Ac3));
        Assert.Contains(threePhase.Shapes, s => s.Element is DiagramMember { Id: "psu-feed", From.Y: 46 });

        var singlePhase = Read(LoadBudgetCalculator.Id, CalcInputValue.Choice("psu_bus", LoadBudgetCalculator.Ac1));
        Assert.Contains(singlePhase.Shapes, s => s.Element is DiagramMember { Id: "psu-feed", From.Y: 110 });
    }

    [Fact]
    public void The_Motor_Start_Diagram_Follows_The_Starter()
    {
        var dol = Read(MotorStartCalculator.Id);
        Assert.StartsWith("A motor started direct on line", dol.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("L_cab = 40 m", dol.LabelFor("L_cab"));
        Assert.Equal("P = 75 kW", dol.LabelFor("P"));
        Assert.Null(dol.LabelFor("k_soft"));

        var starDelta = Read(MotorStartCalculator.Id, CalcInputValue.Choice("starter", MotorStartCalculator.StarterStarDelta));
        Assert.Contains(starDelta.Shapes, s => s.Element.Id == "starter-2");

        var soft = Read(MotorStartCalculator.Id, CalcInputValue.Choice("starter", MotorStartCalculator.StarterSoft));
        Assert.Equal("k_soft = 3.5", soft.LabelFor("k_soft"));

        var drive = Read(MotorStartCalculator.Id, CalcInputValue.Choice("starter", MotorStartCalculator.StarterVfd));
        Assert.Equal("k_vfd = 1.5", drive.LabelFor("k_vfd"));
        Assert.Equal("cos φ_vfd = 0.95", drive.LabelFor("cos_phi_vfd"));
    }

    [Fact]
    public void The_Pcb_Trace_Diagram_Places_The_Copper_On_The_Chosen_Layer()
    {
        var external = Read(PcbTraceCalculator.Id);
        Assert.StartsWith("A trace of width w_act and length L carrying I on an external layer", external.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("w_act = 2 mm", external.LabelFor("w_act"));
        Assert.Equal("I = 3 A", external.LabelFor("I"));
        var face = Assert.IsType<DiagramPlate>(external.Shapes.Single(s => s.Element.Id == "copper").Element);

        var internalLayer = Read(PcbTraceCalculator.Id, CalcInputValue.Choice("layer", PcbTraceCalculator.Internal));
        var inside = Assert.IsType<DiagramPlate>(internalLayer.Shapes.Single(s => s.Element.Id == "copper").Element);
        Assert.True(inside.Y > face.Y, "The internal copper is drawn inside the board, below the external copper on its face.");
    }
}
