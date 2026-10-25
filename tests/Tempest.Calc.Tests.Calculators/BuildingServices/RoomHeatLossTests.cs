using CsCheck;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

public sealed class RoomHeatLossTests
{
    private static readonly RoomHeatLossCalculator Room = new();

    /// <summary>The first known-answer case: a 20 m² cavity wall and a 3 m² window with U 1.4, 21 / −3 °C, 0.5 h⁻¹, 40 m³.</summary>
    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Room.Template);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputSet Wall(double insulationMm, double lambda = 0.03) =>
        new CalcInputSet()
            .With(Q("theta_int", 21, "degC")).With(Q("theta_e", -3, "degC"))
            .With(RoomHeatLossCalculator.Layers(("Wall", "Brick", 102.5, 0.84), ("Wall", "Insulation", insulationMm, lambda), ("Wall", "Block", 100, 0.2)))
            .With(RoomHeatLossCalculator.Elements(("Wall", 20, null, null, null, null, null, null, null)));

    [Fact]
    public void Adding_Insulation_Lowers_U_Monotonically()
    {
        Gen.Select(Gen.Double[1, 250], Gen.Double[0.5, 250], Gen.Double[0.015, 0.05]).Sample((d, extra, lambda) =>
        {
            var thin = CalcRunner.Run(Room, Wall(d, lambda)).OutputSi("U_1");
            var thick = CalcRunner.Run(Room, Wall(Math.Min(d + extra, 500), lambda)).OutputSi("U_1");
            return thick < thin;
        }, iter: 80);
    }

    [Fact]
    public void A_Better_Conductivity_Lowers_U()
    {
        var poor = CalcRunner.Run(Room, Wall(100, 0.05)).OutputSi("U_1");
        var good = CalcRunner.Run(Room, Wall(100, 0.03)).OutputSi("U_1");
        Assert.True(good < poor);
    }

    [Fact]
    public void Room_Load_Is_Linear_In_The_Temperature_Difference()
    {
        var reference = CalcRunner.Run(Room, Defaults());
        var hlc = reference.OutputSi("HLC");

        Gen.Select(Gen.Double[-15, 5], Gen.Double[16, 24]).Sample((te, ti) =>
        {
            var r = CalcRunner.Run(Room, Defaults().With(Q("theta_e", te, "degC")).With(Q("theta_int", ti, "degC")));
            return !r.IsBlocked && Math.Abs(r.OutputSi("HLC") - hlc) < 1e-9 && Math.Abs(r.OutputSi("Phi_HL") - hlc * (ti - te)) < 1e-6;
        }, iter: 60);
    }

    [Fact]
    public void An_Mvhr_Supply_Lowers_The_Ventilation_Loss()
    {
        var natural = CalcRunner.Run(Room, Defaults());
        var mvhr = CalcRunner.Run(Room, Defaults().With(Q("theta_supply", 16, "degC")));
        Assert.True(mvhr.OutputSi("Phi_V") < natural.OutputSi("Phi_V"));
        Assert.Equal(0.34 * 20 * 5, mvhr.OutputSi("Phi_V"), 9);
        Assert.Equal(natural.OutputSi("Phi_T"), mvhr.OutputSi("Phi_T"), 9);
        Assert.Equal(0, CalcRunner.Run(Room, Defaults().With(Q("theta_supply", 21, "degC"))).OutputSi("Phi_V"), 9);
    }

    [Fact]
    public void A_Supply_Warmer_Than_The_Room_Blocks()
    {
        var r = CalcRunner.Run(Room, Defaults().With(Q("theta_supply", 25, "degC")));
        Assert.True(r.IsBlocked);
        Assert.Equal("supply", r.Blocks[0].RuleId);
    }

    [Fact]
    public void A_U_Override_Is_Used_Instead_Of_The_Layers()
    {
        var inputs = Wall(100).With(RoomHeatLossCalculator.Elements(("Wall", 20, null, null, null, null, null, 0.25, null)));
        var r = CalcRunner.Run(Room, inputs);
        Assert.Equal(0.25, r.OutputSi("U_1"), 12);
        Assert.Equal(20 * 0.25 * 24, r.OutputSi("Phi_1"), 9);
        Assert.Contains(r.Warnings, w => w.RuleId == "u-override-layers");
        Assert.Null(r.FindOutput("R_1"));
    }

    [Fact]
    public void Values_Outside_The_Hard_Ranges_Block()
    {
        foreach (var (symbol, value, unit) in new[] { ("theta_int", 25.0, "degC"), ("theta_int", 15.0, "degC"), ("theta_e", 6.0, "degC"), ("theta_e", -16.0, "degC"), ("n", 3.5, "1/h"), ("n", 0.05, "1/h") })
        {
            var r = CalcRunner.Run(Room, Defaults().With(Q(symbol, value, unit)));
            Assert.True(r.IsBlocked, $"{symbol} = {value} should block");
            Assert.StartsWith(symbol, r.Blocks[0].Message, StringComparison.Ordinal);
        }

        foreach (var lambda in new[] { 0.005, 6.0 })
        {
            var r = CalcRunner.Run(Room, Wall(100, lambda));
            Assert.True(r.IsBlocked, $"lambda = {lambda} should block");
            Assert.Contains("layers", r.Blocks[0].Message, StringComparison.Ordinal);
        }

        Assert.True(CalcRunner.Run(Room, Wall(0.5)).IsBlocked);
        Assert.True(CalcRunner.Run(Room, Wall(501)).IsBlocked);
        Assert.True(CalcRunner.Run(Room, Defaults().With(RoomHeatLossCalculator.Elements(("Wall", 0.05, null, null, null, null, null, 1.4, null)))).IsBlocked);
    }

    [Fact]
    public void An_Empty_Elements_Table_Blocks()
    {
        var r = CalcRunner.Run(Room, Defaults().With(CalcInputValue.Table("elements", [])));
        Assert.True(r.IsBlocked);
        Assert.Equal("elements", r.Blocks[0].RuleId);
    }

    [Fact]
    public void A_Built_Up_Element_Without_Layers_Blocks_And_Names_Are_Unique()
    {
        var noLayers = CalcRunner.Run(Room, Defaults().With(RoomHeatLossCalculator.Elements(("Roof", 30, null, null, null, null, null, null, null))));
        Assert.True(noLayers.IsBlocked);
        Assert.Equal("layers", noLayers.Blocks[0].RuleId);

        var twice = CalcRunner.Run(Room, Defaults().With(RoomHeatLossCalculator.Elements(("Wall", 5, null, null, null, null, null, 1.4, null), ("wall", 5, null, null, null, null, null, 1.4, null))));
        Assert.True(twice.IsBlocked);
        Assert.Equal("elements", twice.Blocks[0].RuleId);
    }

    [Fact]
    public void Layers_Are_Matched_By_Name_And_Unmatched_Layers_Warn()
    {
        var inputs = Defaults().With(RoomHeatLossCalculator.Layers(("cavity wall ", "Brick", 102.5, 0.84), ("Cavity wall", "Insulation", 100, 0.03), ("Cavity wall", "Block", 100, 0.2), ("Cavity wall", "Plaster", 13, 0.4), ("Shed", "Timber", 20, 0.12)));
        var r = CalcRunner.Run(Room, inputs);
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Warnings, w => w.RuleId == "orphan-layers");
        Assert.Equal(1 / (0.13 + 0.1025 / 0.84 + 0.1 / 0.03 + 0.1 / 0.2 + 0.013 / 0.4 + 0.04), r.OutputSi("U_1"), 9);
    }

    [Fact]
    public void A_Target_U_Is_Failed_When_Exceeded_And_A_Row_Target_Wins()
    {
        var pass = CalcRunner.Run(Room, Defaults());
        Assert.True(pass.Passed);

        var fail = CalcRunner.Run(Room, Defaults().With(Q("U_target", 0.15, "W/(m^2*K)")));
        Assert.False(fail.Passed);
        Assert.Contains(fail.Checks, c => !c.Passed && c.Name.Contains("Cavity wall", StringComparison.Ordinal));

        var rowTarget = Defaults().With(Q("U_target", 0.15, "W/(m^2*K)")).With(RoomHeatLossCalculator.Elements(("Cavity wall", 20, null, null, null, null, null, null, 0.3), ("Window", 3, null, null, null, null, null, 1.4, 1.2)));
        var mixed = CalcRunner.Run(Room, rowTarget);
        Assert.Equal(2, mixed.Checks.Count);
        Assert.Contains(mixed.Checks, c => c.Name.Contains("Window", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(mixed.Checks, c => c.Name.Contains("Cavity wall", StringComparison.Ordinal) && c.Passed);
    }

    [Fact]
    public void Elements_Can_Share_One_Construction()
    {
        var single = CalcRunner.Run(Room, Defaults());
        var inputs = Defaults().With(RoomHeatLossCalculator.WithConstructions(RoomHeatLossCalculator.Elements(("Front wall", 12, null, null, null, null, null, null, null), ("Gable wall", 8, null, null, null, null, null, null, null)), "Cavity wall", "cavity wall"));
        var r = CalcRunner.Run(Room, inputs);
        Assert.False(r.IsBlocked);
        Assert.Equal(single.OutputSi("U_1"), r.OutputSi("U_1"), 12);
        Assert.Equal(single.OutputSi("U_1"), r.OutputSi("U_2"), 12);
        Assert.Equal(20 * single.OutputSi("U_1") * 24, r.OutputSi("Phi_1") + r.OutputSi("Phi_2"), 9);
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "orphan-layers");
    }

    [Fact]
    public void Thermal_Bridges_Apply_Only_To_Elements_Facing_Outside_Weighted_By_f()
    {
        var inputs = Defaults().With(Q("y_tb", 0.1, "W/(m^2*K)")).With(RoomHeatLossCalculator.Elements(("Cavity wall", 20, null, null, null, null, null, null, null), ("Floor", 10, null, 0.5, null, null, null, 0.25, null), ("Party wall", 10, 18, null, null, null, null, 0.5, null)));
        var r = CalcRunner.Run(Room, inputs);
        Assert.Equal(30, r.OutputSi("A_ext"), 9);
        Assert.Equal(25, r.OutputSi("A_tb"), 9);
        Assert.Equal(0.1 * 25 * 24, r.OutputSi("Phi_tb"), 9);
        Assert.Equal(10 * 0.5 * 3, r.OutputSi("Phi_3"), 9);
    }

    [Fact]
    public void An_Adjacent_Temperature_Equal_To_The_External_One_Is_Treated_As_External()
    {
        var blank = CalcRunner.Run(Room, Defaults().With(Q("y_tb", 0.1, "W/(m^2*K)")));
        var typed = CalcRunner.Run(Room, Defaults().With(Q("y_tb", 0.1, "W/(m^2*K)")).With(RoomHeatLossCalculator.Elements(("Cavity wall", 20, -3, null, null, null, null, null, null), ("Window", 3, null, null, null, null, null, 1.4, null))));
        Assert.Equal(blank.OutputSi("U_1"), typed.OutputSi("U_1"), 12);
        Assert.Equal(blank.OutputSi("Phi_tb"), typed.OutputSi("Phi_tb"), 9);
        Assert.Equal(23, typed.OutputSi("A_ext"), 9);
        Assert.Equal(blank.OutputSi("Phi_HL"), typed.OutputSi("Phi_HL"), 9);
    }

    [Fact]
    public void Infiltration_Is_Added_At_The_Outside_Temperature_With_Mvhr_Only()
    {
        var withInf = CalcRunner.Run(Room, Defaults().With(Q("theta_supply", 16, "degC")).With(Q("n_inf", 0.1, "1/h")));
        Assert.Equal(0.34 * (20 * 5 + 0.1 * 40 * 24), withInf.OutputSi("Phi_V"), 9);
        var without = CalcRunner.Run(Room, Defaults().With(Q("theta_supply", 16, "degC")));
        Assert.True(withInf.OutputSi("Phi_V") > without.OutputSi("Phi_V"));

        var natural = CalcRunner.Run(Room, Defaults().With(Q("n_inf", 0.1, "1/h")));
        Assert.Contains(natural.Warnings, w => w.RuleId == "n-inf-ignored");
        Assert.Equal(CalcRunner.Run(Room, Defaults()).OutputSi("Phi_V"), natural.OutputSi("Phi_V"), 9);
        Assert.True(CalcRunner.Run(Room, Defaults().With(Q("n_inf", 3.5, "1/h"))).IsBlocked);
    }

    [Fact]
    public void A_Room_That_Gains_Heat_Warns()
    {
        var gain = CalcRunner.Run(Room, Defaults().With(Q("theta_supply", 21, "degC")).With(RoomHeatLossCalculator.Elements(("Party wall", 10, 40, null, null, null, null, 0.5, null))));
        Assert.True(gain.OutputSi("Phi_HL") <= 0);
        Assert.Contains(gain.Warnings, w => w.RuleId == "gain");
        Assert.DoesNotContain(CalcRunner.Run(Room, Defaults()).Warnings, w => w.RuleId == "gain");
    }

    [Fact]
    public void The_Mvhr_Supply_Temperature_Is_Entered_Once_On_The_Sheet()
    {
        var r = CalcRunner.Run(Room, Defaults().With(Q("theta_supply", 16, "degC")));
        Assert.Single(r.Steps, s => s.Symbol == "theta_supply");
    }

    [Fact]
    public void The_Dhdg_Allowance_Applies_Only_To_That_Method()
    {
        var elemental = CalcRunner.Run(Room, Defaults().With(Q("f_add", 10, "%")));
        Assert.Contains(elemental.Warnings, w => w.RuleId == "f-add-ignored");
        var baseLoad = elemental.OutputSi("Phi_HL");
        Assert.Equal(baseLoad, elemental.OutputSi("Phi_T") + elemental.OutputSi("Phi_V"), 9);

        var dhdg = CalcRunner.Run(Room, Defaults().With(Q("f_add", 10, "%")).With(CalcInputValue.Choice("method", RoomHeatLossCalculator.Dhdg)));
        Assert.Equal(baseLoad * 1.1, dhdg.OutputSi("Phi_HL"), 9);
        Assert.DoesNotContain(dhdg.Warnings, w => w.RuleId == "f-add-ignored");
    }

    [Fact]
    public void The_Floor_Area_Gives_The_Load_Per_Square_Metre()
    {
        var r = CalcRunner.Run(Room, Defaults().With(Q("A_floor", 16, "m^2")));
        Assert.Equal(r.OutputSi("Phi_HL") / 16, r.OutputSi("Phi_A"), 9);
    }

    private static CalcInputValue Table(params (string Name, string? Flow, double Area, double? ThetaAdj)[] rows) =>
        CalcInputValue.Table("elements", rows.Select(r =>
        {
            var cells = new List<(string, CalcCell)> { ("name", new CalcCell(Text: r.Name)), ("area", new CalcCell(r.Area, "m^2")) };

            if (r.Flow is { } flow)
                cells.Add(("flow", new CalcCell(Text: flow)));

            if (r.ThetaAdj is { } t)
                cells.Add(("theta_adj", new CalcCell(t, "degC")));

            return CalcRow.Of([.. cells]);
        }).ToList());

    [Fact]
    public void The_Global_Target_U_Is_Not_Applied_To_A_Partition_To_A_Heated_Neighbour()
    {
        // Black B-A-04 / Green G-B-09: an 18 °C partition (U about 1.8) carried an unexplained FAIL against the 0.30 outside target.
        var inputs = Defaults().With(RoomHeatLossCalculator.Layers(("Cavity wall", "Brick", 102.5, 0.84), ("Cavity wall", "Insulation", 100, 0.03), ("Partition", "Board", 25, 0.22)))
            .With(Table(("Cavity wall", null, 20, null), ("Partition", null, 12, 18)));
        var r = CalcRunner.Run(Room, inputs);
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Contains(r.Checks, c => c.Name.Contains("Cavity wall", StringComparison.Ordinal));
        Assert.DoesNotContain(r.Checks, c => c.Name.Contains("Partition", StringComparison.Ordinal));
        Assert.Null(r.FindOutput("u_2"));
        Assert.True(r.Passed);

        // Its own target still applies wherever it faces.
        var own = CalcRunner.Run(Room, inputs.With(RoomHeatLossCalculator.Elements(("Cavity wall", 20, null, null, null, null, null, null, null), ("Partition", 12, 18, null, null, null, null, null, 1.2))));
        Assert.Contains(own.Checks, c => c.Name.Contains("Partition", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, 0.13)]
    [InlineData("horizontal", 0.13)]
    [InlineData("upward", 0.10)]
    [InlineData("downward", 0.17)]
    public void The_Conventional_Internal_Surface_Resistance_Follows_The_Heat_Flow_Direction(string? flow, double rsi)
    {
        // Green G-B-14: a roof lost heat through R_si = 0.13 instead of the upward-flow 0.10.
        var inputs = new CalcInputSet().With(Q("theta_int", 21, "degC")).With(Q("theta_e", -3, "degC"))
            .With(RoomHeatLossCalculator.Layers(("Roof", "Insulation", 100, 0.05)))
            .With(Table(("Roof", flow, 10, null)));
        var r = CalcRunner.Run(Room, inputs);
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(rsi + 0.1 / 0.05 + 0.04, r.OutputSi("R_1"), 12);
    }

    [Fact]
    public void An_Internal_Floor_Takes_The_Same_Resistance_On_Both_Faces_For_Its_Direction()
    {
        var inputs = new CalcInputSet().With(Q("theta_int", 21, "degC")).With(Q("theta_e", -3, "degC"))
            .With(RoomHeatLossCalculator.Layers(("Ceiling", "Board", 25, 0.22)))
            .With(Table(("Ceiling", "upward", 10, 18)));
        var r = CalcRunner.Run(Room, inputs);
        Assert.Equal(0.10 + 0.025 / 0.22 + 0.10, r.OutputSi("R_1"), 12);
    }

    [Fact]
    public void The_Elements_Table_Is_Not_Record_Data_And_A_New_Sheet_Asks_For_It()
    {
        // Black B-A-10: the elements table (areas, adjacent temperatures) was stripped from a new sheet as if it were a licensed
        // record. It is not record data; a new sheet opens every table blank (DA-23) and says what to enter.
        Assert.DoesNotContain(Room.Template.RecordKinds!, k => k.Symbols.Contains("elements"));
        var run = CalcRunner.Run(Room, CalcTemplateHasher.NewSheetInputs(Room.Template));
        Assert.True(run.IsBlocked);
        Assert.Contains(run.Blocks, b => b.Message.StartsWith("elements", StringComparison.Ordinal));
    }

    [Fact]
    public void The_U_Value_Of_The_First_Element_Is_A_Declared_Port()
    {
        Assert.Contains(Room.Template.OutputPorts!, p => p is { Symbol: "U_1", Unit: "W/(m^2*K)" });
        var inputs = new CalcInputSet().With(Q("theta_int", 21, "degC")).With(Q("theta_e", -3, "degC"))
            .With(RoomHeatLossCalculator.Layers(("Wall", "Brick", 215, 0.8), ("Wall", "Board", 12.5, 0.25)))
            .With(Table(("Wall", "horizontal", 10, null)));
        var r = CalcRunner.Run(Room, inputs);
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(1 / (0.13 + 0.215 / 0.8 + 0.0125 / 0.25 + 0.04), r.OutputSi("U_1"), 12);
        Assert.Contains(Room.Template.OutputPorts!, p => p.Symbol == "U_1");
    }
}
