using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E4 1.2.0, elec.conductor-sizing-01: the ambient temperature reaches the sizing only through C_a, so an ambient above the reference with C_a left at 1
/// sizes the conductor for the reference ambient. Expected values from python3: PVC, 7.5 kW three-phase on a type C MCB (I_n 16 A), synthetic table
/// 1.5→12.3, 2.5→17.7, 4→24.1 A: θ_a 50 °C with C_a 1 gives 2.5 mm² (I_z 17.7 A); the derived C_a = √((70−50)/(70−30)) = 0.70711 gives I_t,req = 22.627 A, so 4 mm².
/// </summary>
public sealed class ConductorAmbientTests
{
    private static readonly ConductorSizingCalculator Sizing = new();

    /// <summary>The first known-answer case with its factors reset to 1 (C_a, C_g), one circuit and a 30 °C ambient, so the ambient is the only thing that differs.</summary>
    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Sizing, overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Sizing.Template).With(CalcInputValue.Of("C_a", 1)).With(CalcInputValue.Of("C_g", 1)).With(CalcInputValue.Of("N_g", 1)).With(Q("theta_a", 30, "degC")), (set, v) => set.With(v)));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    [Fact]
    public void A_Hot_Ambient_With_C_a_Left_At_One_Is_Warned_Because_The_Ambient_Was_Not_Applied()
    {
        var r = Run(Q("theta_a", 50, "degC"));

        Assert.False(r.IsBlocked);
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "theta_a");
        Assert.Contains("50", warning.Message, StringComparison.Ordinal);
        Assert.Contains("C_a is 1", warning.Message, StringComparison.Ordinal);
        Assert.Equal(2.5, r.OutputSi("CSA") * 1e6, 9);
    }

    [Fact]
    public void Entering_C_a_Or_Deriving_It_Applies_The_Ambient_And_Raises_No_Warning()
    {
        var typed = Run(Q("theta_a", 50, "degC"), CalcInputValue.Of("C_a", Math.Sqrt(20.0 / 40.0)));
        Assert.DoesNotContain(typed.Warnings, w => w.RuleId == "theta_a");
        Assert.Equal(16 / Math.Sqrt(0.5), typed.OutputSi("I_t_req"), 9);
        Assert.Equal(4, typed.OutputSi("CSA") * 1e6, 9);

        var derived = Run(Q("theta_a", 50, "degC"), CalcInputValue.Switch("derive_Ca", true));
        Assert.DoesNotContain(derived.Warnings, w => w.RuleId == "theta_a");
        Assert.Equal(Math.Sqrt(0.5), derived.OutputSi("C_a"), 9);
    }

    [Fact]
    public void An_Ambient_At_Or_Below_The_Reference_Needs_No_Warning()
    {
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "theta_a");
        Assert.DoesNotContain(Run(Q("theta_a", 25, "degC")).Warnings, w => w.RuleId == "theta_a");
    }

    [Theory]
    [InlineData(ConductorSizingCalculator.Pvc, 70.0, true)]
    [InlineData(ConductorSizingCalculator.Pvc, 69.0, false)]
    [InlineData(ConductorSizingCalculator.Xlpe, 70.0, false)]
    public void An_Ambient_At_The_Conductor_Operating_Temperature_Is_Refused(string insulation, double ambient, bool blocked)
    {
        var r = Run(CalcInputValue.Choice("insulation", insulation), Q("theta_a", ambient, "degC"));

        Assert.Equal(blocked, r.IsBlocked);

        if (blocked)
        {
            var block = Assert.Single(r.Blocks);
            Assert.Equal("theta_a-limit", block.RuleId);
            Assert.StartsWith("theta_a:", block.Message, StringComparison.Ordinal);
        }
    }
}
