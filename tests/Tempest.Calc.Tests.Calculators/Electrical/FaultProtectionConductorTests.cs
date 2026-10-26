using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E5 1.3.0: elec.fault-protection-01 (R₁ + R₂ from a pure-copper resistivity), -03 (no conductor check against the let-through near the origin) and
/// -09 (the 'curve' rule says Block, the code warns). Expected values from python3.
/// 1.3.1 (chain.motor-circuit-06 remainder): a protective conductor equal to the line conductor above 16 mm2, as the pack link from the cable step enters it, is warned.
/// </summary>
public sealed class FaultProtectionConductorTests
{
    private static readonly FaultProtectionCalculator Fault = new();

    /// <summary>The first known-answer case (which states k_R 1, the pure resistivity) with k_R at the template's declared default, 1.125.</summary>
    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Fault.Template).With(CalcInputValue.Of("k_R", Fault.Template.FindInput("k_R")!.Default!.Value));

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Fault, overrides.Aggregate(Defaults(), (set, v) => set.With(v)));

    [Fact]
    public void The_Default_Conductor_Resistance_Is_Not_Below_The_Cable_Standard_Maximum()
    {
        // 20 m of 2.5 / 1.5 mm2 (the template defaults). The cable standard's class 1 maxima, 7.41 and 12.1 ohm/km, give 20 m x (7.41 + 12.1) e-3 = 0.3902 ohm at 20 degC
        // (python3); the pure-copper resistivity gave 0.3678 ohm, 5.7 % below. R_12 is at 70 degC: divide by 1 + 0.00393 x 50 = 1.1965.
        Assert.Equal(1.125, Fault.Template.FindInput("k_R")!.Default);
        var r = Run();
        var at20 = r.OutputSi("R_12") / 1.1965;
        Assert.True(at20 >= 0.3902, $"R1+R2 at 20 degC is {at20} ohm, below the cable standard's maximum 0.3902 ohm");
        Assert.Equal(17.241e-3 * 1.125 * 20 * (1 / 2.5 + 1 / 1.5), at20, 9);
        Assert.Equal(0.35 + 0.4950926, r.OutputSi("Z_s"), 6);
    }

    [Theory]
    [InlineData(35, 35, true)]
    [InlineData(25, 25, true)]
    [InlineData(16, 16, false)]
    [InlineData(10, 10, false)]
    [InlineData(35, 16, false)]
    [InlineData(50, 25, false)]
    public void A_Protective_Conductor_Equal_To_The_Line_Conductor_Above_16_mm2_Is_Warned(double sLine, double sCpc, bool warned)
    {
        var r = Run(CalcInputValue.Quantity("S_line", sLine, "mm^2"), CalcInputValue.Quantity("S_cpc", sCpc, "mm^2"));
        Assert.Equal(warned, r.Warnings.Any(w => w.RuleId == "resistance" && w.Message.StartsWith("S_cpc:", StringComparison.Ordinal)));

        if (warned)
            Assert.Contains("enter the real S_cpc", r.Warnings.Single(w => w.RuleId == "resistance" && w.Message.StartsWith("S_cpc:", StringComparison.Ordinal)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Full_Size_Protective_Conductor_Above_16_mm2_Understates_The_Loop_Resistance_The_Warning_Names()
    {
        // 20 m, k_R 1.125, 70 degC: R_12 = 0.017241 x 1.125 x 20 x 1.1965 x (1/35 + 1/35) = 0.026523 ohm with a full-size 35 mm2 cpc, against 0.042271 ohm with the
        // usual 16 mm2 (python3): Z_s is understated by 15.7 mohm, which is why the notice is raised.
        var full = Run(CalcInputValue.Quantity("S_line", 35, "mm^2"), CalcInputValue.Quantity("S_cpc", 35, "mm^2"));
        var reduced = Run(CalcInputValue.Quantity("S_line", 35, "mm^2"), CalcInputValue.Quantity("S_cpc", 16, "mm^2"));
        Assert.Equal(0.017241 * 1.125 * 20 * 1.1965 * (2.0 / 35), full.OutputSi("R_12"), 6);
        Assert.Equal(0.017241 * 1.125 * 20 * 1.1965 * (1.0 / 35 + 1.0 / 16), reduced.OutputSi("R_12"), 6);
        Assert.True(reduced.OutputSi("Z_s") > full.OutputSi("Z_s"));
        Assert.Contains(full.Warnings, w => w.RuleId == "resistance" && w.Message.StartsWith("S_cpc:", StringComparison.Ordinal));
        Assert.DoesNotContain(reduced.Warnings, w => w.RuleId == "resistance" && w.Message.StartsWith("S_cpc:", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Resistance_Factor_Of_One_Is_The_Pure_Resistivity_And_Is_Warned()
    {
        var pure = Run(CalcInputValue.Of("k_R", 1));
        Assert.Equal(17.241e-3 * 20 * (1 / 2.5 + 1 / 1.5), pure.OutputSi("R_12") / 1.1965, 9);
        Assert.Contains(pure.Warnings, w => w.RuleId == "resistance");
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "resistance");
        Assert.Equal(0.7900823, pure.OutputSi("Z_s"), 6);
    }

    [Fact]
    public void Without_A_Let_Through_Energy_The_Sheet_Fails_The_Check_Of_The_Conductors_At_The_Origin()
    {
        // B32 on 1.5 mm2 line and cpc, 500 kVA 5 %: the end-of-line check passes (u_cpc 0.5) and says nothing about a fault near the origin.
        var r = Run(CalcInputValue.Quantity("S_line", 1.5, "mm^2"), CalcInputValue.Quantity("S_r", 500, "kVA"), CalcInputValue.Quantity("u_k", 5, "%"));

        var origin = Assert.Single(r.Checks, c => c.Name == "Conductors against let-through at the origin");
        Assert.False(origin.Passed);
        Assert.Contains("I2t_let", origin.DemandText, StringComparison.Ordinal);
        Assert.Contains(r.Warnings, w => w.RuleId == "let-through" && w.Message.Contains("fails until I2t_let is entered", StringComparison.Ordinal));
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Adiabatic minimum against the protective", StringComparison.Ordinal) && c.Passed);
    }

    [Fact]
    public void A_Let_Through_Energy_Checks_Both_Conductors_At_The_Origin_And_The_Sheet_Fails_When_They_Are_Too_Small()
    {
        // 90 kA2s: S_min = sqrt(90000)/114.83611 = 2.6124 mm2, so a 1.5 mm2 cpc fails with u_cpc 1.7416 and a 1.5 mm2 line fails with u_line 1.7416 (python3).
        var r = Run(CalcInputValue.Quantity("S_line", 1.5, "mm^2"), CalcInputValue.Quantity("I2t_let", 90000, "A^2*s"));

        Assert.DoesNotContain(r.Checks, c => c.Name == "Conductors against let-through at the origin");
        Assert.Equal(Math.Sqrt(90000) / 114.83611 / 1.5, r.OutputSi("u_cpc"), 4);
        Assert.Equal(1.7416, r.OutputSi("u_cpc"), 3);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Adiabatic minimum against the protective", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Adiabatic minimum against the line", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void A_Curve_Device_That_Clears_Above_A_Tenth_Of_A_Second_Needs_No_Let_Through_Entry()
    {
        // Fuse points (5 s, 120 A), (0.4 s, 220 A), (0.1 s, 320 A), (0.01 s, 700 A): I_f 276.55 A clears in 0.1716 s, so I2t = I_f^2 t_d is the energy.
        var r = Run(CalcInputValue.Choice("device", FaultProtectionCalculator.Curve), FaultProtectionCalculator.Points((5, 120), (0.4, 220), (0.1, 320), (0.01, 700)), CalcInputValue.Of("k_R", 1));
        Assert.DoesNotContain(r.Checks, c => c.Name == "Conductors against let-through at the origin");
        Assert.Equal(0.9975407, r.OutputSi("S_min") * 1e6, 5);
    }

    [Fact]
    public void A_Fault_Current_Outside_The_Curve_Warns_Under_A_Rule_Declared_As_A_Warning()
    {
        // I_f above the highest point (220 A, 0.4 s): the fastest time entered is an upper bound and the sheet warns; the rule that carries the notice is a Warning,
        // the rule that blocks a curve that does not bracket t_req stays a Block.
        var r = Run(CalcInputValue.Choice("device", FaultProtectionCalculator.Curve), FaultProtectionCalculator.Points((5, 120), (0.4, 220)), CalcInputValue.Of("k_R", 1));
        var warning = Assert.Single(r.Warnings, w => w.Message.Contains("upper bound", StringComparison.Ordinal));
        var rule = Assert.Single(Fault.Template.ApplicabilityRules, x => x.Id == warning.RuleId);
        Assert.Equal(CalcNoticeSeverity.Warning, rule.Severity);

        var blocked = Run(CalcInputValue.Choice("device", FaultProtectionCalculator.Curve), FaultProtectionCalculator.Points((5, 120), (1, 180)));
        var block = Assert.Single(blocked.Blocks);
        Assert.Equal(CalcNoticeSeverity.Block, Assert.Single(Fault.Template.ApplicabilityRules, x => x.Id == block.RuleId).Severity);
    }
}
