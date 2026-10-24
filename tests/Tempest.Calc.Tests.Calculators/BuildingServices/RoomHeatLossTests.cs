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

    private static CalcInputSet Wall(double insulationMm, double lambda = 0.022) =>
        new CalcInputSet()
            .With(Q("theta_int", 21, "degC")).With(Q("theta_e", -3, "degC"))
            .With(RoomHeatLossCalculator.Layers(("Wall", "Brick", 102.5, 0.77), ("Wall", "Insulation", insulationMm, lambda), ("Wall", "Block", 100, 0.15)))
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
        var poor = CalcRunner.Run(Room, Wall(100, 0.04)).OutputSi("U_1");
        var good = CalcRunner.Run(Room, Wall(100, 0.022)).OutputSi("U_1");
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
        var inputs = Defaults().With(RoomHeatLossCalculator.Layers(("cavity wall ", "Brick", 102.5, 0.77), ("Cavity wall", "PIR", 100, 0.022), ("Cavity wall", "Block", 100, 0.15), ("Cavity wall", "Plaster", 13, 0.57), ("Shed", "Timber", 20, 0.13)));
        var r = CalcRunner.Run(Room, inputs);
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Warnings, w => w.RuleId == "orphan-layers");
        Assert.Equal(1 / (0.13 + 0.1025 / 0.77 + 0.1 / 0.022 + 0.1 / 0.15 + 0.013 / 0.57 + 0.04), r.OutputSi("U_1"), 9);
    }

    [Fact]
    public void A_Target_U_Is_Failed_When_Exceeded_And_A_Row_Target_Wins()
    {
        var pass = CalcRunner.Run(Room, Defaults());
        Assert.True(pass.Passed);

        var fail = CalcRunner.Run(Room, Defaults().With(Q("U_target", 0.15, "W/(m^2*K)")));
        Assert.False(fail.Passed);
        Assert.Contains(fail.Checks, c => !c.Passed && c.Name.Contains("Cavity wall", StringComparison.Ordinal));

        var rowTarget = Defaults().With(Q("U_target", 0.15, "W/(m^2*K)")).With(RoomHeatLossCalculator.Elements(("Cavity wall", 20, null, null, null, null, null, null, 0.2), ("Window", 3, null, null, null, null, null, 1.4, 1.2)));
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
    public void Thermal_Bridges_Apply_Only_To_Elements_Facing_Outside()
    {
        var inputs = Defaults().With(Q("y_tb", 0.1, "W/(m^2*K)")).With(RoomHeatLossCalculator.Elements(("Cavity wall", 20, null, null, null, null, null, null, null), ("Party wall", 10, 18, null, null, null, null, 0.5, null)));
        var r = CalcRunner.Run(Room, inputs);
        Assert.Equal(20, r.OutputSi("A_ext"), 9);
        Assert.Equal(0.1 * 20 * 24, r.OutputSi("Phi_tb"), 9);
        Assert.Equal(10 * 0.5 * 3, r.OutputSi("Phi_2"), 9);
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
}
