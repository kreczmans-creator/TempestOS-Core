using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E7 1.1.0, elec.pcb-trace-02: with the trace narrower than the minimum (u_w above 1) the resistance, drop and loss were taken at the allowed rise, but the drawn trace runs hotter.
/// Expected values from python3 for ka-half-oz-internal-narrow (1 A, 0.5 oz, internal, 200 mm at 0.5 mm, ambient 40 °C): A_act = 0.5 mm × 17.5 µm = 13.563 mil²,
/// ΔT = (1/(0.024·13.563^0.725))^(1/0.44) = 65.392 K, ρ = 0.01724·(1 + 0.00393·(40 + 65.392 − 20)) = 0.0230256 Ω·mm²/m, R = ρ·0.2 m/0.0087505 mm² = 526.3 mΩ (471.5 mΩ at the allowed 30 K).
/// </summary>
public sealed class PcbTraceNarrowTests
{
    private static readonly PcbTraceCalculator Pcb = new();

    private static CalcInputSet Narrow(params CalcInputValue[] overrides) => overrides.Aggregate(new CalcInputSet([.. Pcb.Template.TestCases[3].Inputs]), (set, v) => set.With(v));

    [Fact]
    public void A_Trace_That_Is_Too_Narrow_Is_Evaluated_At_The_Rise_It_Would_Actually_Have()
    {
        var r = CalcRunner.Run(Pcb, Narrow());

        Assert.Equal(1.604634351, r.OutputSi("u_w"), 8);
        Assert.Equal(65.392, r.OutputSi("dT_act"), 2);
        Assert.Equal(526.28, r.OutputSi("trace_R") * 1000, 1);
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "narrow");
        Assert.Contains("65.4 K above ambient", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Trace_That_Is_Wide_Enough_Keeps_The_Allowed_Rise_And_Narrowing_It_Past_The_Minimum_Heats_It()
    {
        // 3 A, 10 K, 1 oz external: w_min 1.367 mm. At 2 mm the allowed rise is used and nothing is warned; at 1 mm the trace is too narrow and runs hotter than 10 K.
        var wide = CalcRunner.Run(Pcb, new CalcInputSet([.. Pcb.Template.TestCases[0].Inputs]));
        Assert.DoesNotContain(wide.Warnings, w => w.RuleId == "narrow");

        var narrow = CalcRunner.Run(Pcb, new CalcInputSet([.. Pcb.Template.TestCases[0].Inputs]).With(CalcInputValue.Quantity("w_act", 1, "mm")));
        Assert.Contains(narrow.Warnings, w => w.RuleId == "narrow");
        Assert.True(narrow.OutputSi("dT_act") > 10);
    }
}
