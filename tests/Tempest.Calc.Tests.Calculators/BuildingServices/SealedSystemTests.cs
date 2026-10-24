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
    public void The_Flow_To_Return_Difference_Is_Offered_And_Is_Not_The_Emitter_Mean_Difference()
    {
        Gen.Select(Gen.Double[40, 90], Gen.Double[1, 30]).Sample((tf, drop) =>
        {
            var tr = Math.Max(tf - drop, 30.0);
            var r = CalcRunner.Run(Sealed, Defaults().With(CalcInputValue.Quantity("t_f", tf, "degC")).With(CalcInputValue.Quantity("t_r", tr, "degC")));

            if (r.IsBlocked)
                return;

            Assert.Equal(tf - tr, r.OutputSi("dT_fr"), 1e-9);
        }, iter: 60);

        // At 75/65/20 the emitter mean difference is 50 K but the loop drops 10 K.
        var catalogue = CalcRunner.Run(Sealed, Defaults());
        Assert.Equal(50, catalogue.OutputSi("dT"), 1e-9);
        Assert.Equal(10, catalogue.OutputSi("dT_fr"), 1e-9);

        Assert.Contains(Sealed.Template.OutputPorts!, p => p is { Symbol: "dT_fr", Unit: "K" });
    }
}
