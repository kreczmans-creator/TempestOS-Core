using CsCheck;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

public sealed class SealedSystemTests
{
    private static readonly SealedSystemCalculator Sealed = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Sealed.Template);

    [Fact]
    public void Log_Mean_Is_Never_Above_The_Arithmetic_Mean()
    {
        Gen.Select(Gen.Double[35, 90], Gen.Double[1, 30]).Sample((tf, drop) =>
        {
            var inputs = Defaults().With(CalcInputValue.Quantity("t_f", tf, "degC")).With(CalcInputValue.Quantity("t_r", Math.Max(tf - drop, 30), "degC"));
            var amtd = CalcRunner.Run(Sealed, inputs);
            var lmtd = CalcRunner.Run(Sealed, inputs.With(CalcInputValue.Choice("dT_method", SealedSystemCalculator.Lmtd)));
            return amtd.IsBlocked || lmtd.OutputSi("dT") <= amtd.OutputSi("dT") + 1e-9;
        }, iter: 60);
    }

    [Fact]
    public void Lower_Flow_Temperature_Needs_Bigger_Emitters()
    {
        var hot = CalcRunner.Run(Sealed, Defaults()).OutputSi("R1");
        var cool = CalcRunner.Run(Sealed, Defaults().With(CalcInputValue.Quantity("t_f", 50, "degC")).With(CalcInputValue.Quantity("t_r", 40, "degC"))).OutputSi("R1");
        Assert.True(cool > 2 * hot);
    }

    [Fact]
    public void Vessel_Grows_With_Volume_And_Static_Head()
    {
        var baseVe = CalcRunner.Run(Sealed, Defaults()).OutputSi("V_e");
        Assert.True(CalcRunner.Run(Sealed, Defaults().With(CalcInputValue.Quantity("V_pipe", 200, "L"))).OutputSi("V_e") > baseVe);
        Assert.True(CalcRunner.Run(Sealed, Defaults().With(CalcInputValue.Quantity("h_static", 12, "m"))).OutputSi("V_e") > baseVe);
    }

    [Fact]
    public void A_Glycol_Record_Replaces_The_Water_Expansion()
    {
        var r = CalcRunner.Run(Sealed, Defaults().With(CalcInputValue.Of("e_fluid", 0.045)));
        Assert.Equal(0.045, r.OutputSi("e_x"), 12);
    }

    [Fact]
    public void Pre_Charge_At_Or_Above_Final_Pressure_Blocks()
    {
        var r = CalcRunner.Run(Sealed, Defaults().With(CalcInputValue.Quantity("h_static", 25, "m")));
        Assert.True(r.IsBlocked);
        Assert.Equal("pressures", r.Blocks[0].RuleId);
    }

    [Fact]
    public void Final_Pressure_At_The_Safety_Valve_Blocks()
    {
        Assert.True(CalcRunner.Run(Sealed, Defaults().With(CalcInputValue.Quantity("p_f", 3, "bar"))).IsBlocked);
    }

    [Fact]
    public void Return_At_Room_Temperature_Blocks()
    {
        var r = CalcRunner.Run(Sealed, Defaults().With(CalcInputValue.Quantity("t_f", 40, "degC")).With(CalcInputValue.Quantity("t_r", 30, "degC")).With(CalcInputValue.Quantity("t_i", 24, "degC")));
        Assert.False(r.IsBlocked);
        var blocked = CalcRunner.Run(Sealed, Defaults().With(CalcInputValue.Quantity("t_r", 85, "degC")));
        Assert.True(blocked.IsBlocked);
    }

    [Fact]
    public void An_Undersized_Emitter_Fails_Its_Check()
    {
        var r = CalcRunner.Run(Sealed, Defaults().With(SealedSystemCalculator.Rooms(("Bedroom", 900, 800, 4))));
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Selected emitters", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void A_Selected_Output_In_Kilowatts_Is_Read_In_Its_Unit()
    {
        // Opus review finding 9.
        var rooms = CalcInputValue.Table("rooms", [CalcRow.Of(("name", new CalcCell(Text: "Lounge")), ("Phi", new CalcCell(1500, "W")), ("Phi50", new CalcCell(1.6, "kW")))]);
        var r = CalcRunner.Run(Sealed, Defaults().With(rooms));
        Assert.Equal(0.9375, r.OutputSi("u_emit"), 9);
    }

    [Fact]
    public void The_Flow_To_Return_Difference_And_Mean_Water_Temperature_Are_Offered()
    {
        // The generator keeps 30 °C ≤ return < flow ≤ 90 °C, so the run can never block: a block fails the test.
        Gen.Select(Gen.Double[45, 90], Gen.Double[1, 15]).Sample((tf, drop) =>
        {
            var tr = tf - drop;
            var r = CalcRunner.Run(Sealed, Defaults().With(CalcInputValue.Quantity("t_f", tf, "degC")).With(CalcInputValue.Quantity("t_r", tr, "degC")));
            Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
            Assert.Equal(tf - tr, r.OutputSi("dT_fr"), 1e-9);
            Assert.Equal((tf + tr) / 2 + 273.15, r.OutputSi("t_m"), 1e-9);
        }, iter: 60);

        // At 75/65/20 the emitter mean difference is 50 K but the loop drops 10 K, and the mean water temperature is 70 °C.
        var catalogue = CalcRunner.Run(Sealed, Defaults());
        Assert.Equal(50, catalogue.OutputSi("dT"), 1e-9);
        Assert.Equal(10, catalogue.OutputSi("dT_fr"), 1e-9);
        Assert.Equal(343.15, catalogue.OutputSi("t_m"), 1e-9);

        Assert.Contains(Sealed.Template.OutputPorts!, p => p is { Symbol: "dT_fr", Unit: "K" });
        Assert.Contains(Sealed.Template.OutputPorts!, p => p is { Symbol: "t_m", Unit: "degC" });
    }

    [Theory]
    [InlineData(40, 30)]
    [InlineData(30, 30)]
    public void A_Fill_Temperature_At_Or_Above_The_Maximum_Blocks_Instead_Of_A_Negative_Vessel(double fill, double max)
    {
        // Black B-A-08(a): t_fill 40, t_max 30 gave e_x = −0.0034, V_e = −0.27 L and a FAIL with a utilisation of −0.05.
        var r = CalcRunner.Run(Sealed, Defaults().With(Q("t_fill", fill, "degC")).With(Q("t_max", max, "degC")).With(Q("V_vessel", 5, "L")));
        Assert.True(r.IsBlocked);
        Assert.Equal("t-fill", r.Blocks[0].RuleId);
        Assert.StartsWith("t_fill:", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Vessel_Is_Sized_For_The_Flow_Temperature_When_It_Is_Above_The_Maximum()
    {
        // Black B-A-08(b): t_max 30 with a 75 °C flow gave V_e = 0.32 L against 2.50 L at the actual flow temperature.
        var low = CalcRunner.Run(Sealed, Defaults().With(Q("t_max", 30, "degC")));
        var atFlow = CalcRunner.Run(Sealed, Defaults().With(Q("t_max", 75, "degC")));
        Assert.Contains(low.Warnings, w => w.RuleId == "t-max");
        Assert.Equal(atFlow.OutputSi("V_e"), low.OutputSi("V_e"), 12);
        Assert.Equal(atFlow.OutputSi("e_x"), low.OutputSi("e_x"), 12);
    }

    [Fact]
    public void A_Selected_Emitter_Of_Zero_Watts_Is_Refused_Not_Treated_As_None_Selected()
    {
        // Black B-A-21: Phi50 = 0 for a 1500 W room removed the emitter check ("No pass/fail checks in this calculation").
        var zero = CalcInputValue.Table("rooms", [CalcRow.Of(("name", new CalcCell(Text: "Lounge")), ("Phi", new CalcCell(1500, "W")), ("Phi50", new CalcCell(0, "W")), ("V_em", new CalcCell(8, "L")))]);
        var r = CalcRunner.Run(Sealed, Defaults().With(zero));
        Assert.True(r.IsBlocked);
        Assert.Equal("emitter", r.Blocks[0].RuleId);
        Assert.Contains("Lounge", r.Blocks[0].Message, StringComparison.Ordinal);

        var none = CalcRunner.Run(Sealed, Defaults().With(SealedSystemCalculator.Rooms(("Lounge", 1500, 0, 8))));
        Assert.False(none.IsBlocked);
    }

    [Fact]
    public void The_Vessel_States_That_It_Excludes_The_Water_Reserve_And_Cites_Only_What_It_Implements()
    {
        // Green G-B-08: the sheet cited BS EN 12828 for a vessel volume that omits that standard's water reserve and safety-valve margin.
        Assert.DoesNotContain(Sealed.Template.ClauseRefs, c => c.Standard.Contains("12828", StringComparison.Ordinal));
        Assert.Contains(Sealed.Template.Assumptions, a => a.Statement.Contains("water reserve", StringComparison.Ordinal));
        var r = CalcRunner.Run(Sealed, Defaults().With(Q("V_vessel", 8, "L")));
        Assert.All(r.Checks, c => Assert.DoesNotContain("12828", c.Clause ?? string.Empty, StringComparison.Ordinal));
    }

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);
}
