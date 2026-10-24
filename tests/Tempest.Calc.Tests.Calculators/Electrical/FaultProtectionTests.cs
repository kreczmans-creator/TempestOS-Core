using CsCheck;
using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

public sealed class FaultProtectionTests
{
    private static readonly FaultProtectionCalculator Fault = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Fault.Template);

    [Fact]
    public void Demands_An_Independent_Check() => Assert.True(Fault.Template.IndependentCheck);

    [Theory]
    [InlineData(FaultProtectionCalculator.McbB, 5)]
    [InlineData(FaultProtectionCalculator.McbC, 10)]
    [InlineData(FaultProtectionCalculator.McbD, 20)]
    public void Mcb_Maximum_Loop_Impedance_Follows_The_Trip_Multiple(string device, int multiple)
    {
        var r = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Choice("device", device)));
        Assert.Equal(0.95 * 230 / (multiple * 32.0), r.OutputSi("Z_s_max"), 9);
    }

    [Fact]
    public void Inputs_Outside_Their_Range_Are_A_Hard_Fail()
    {
        foreach (var (symbol, value, unit) in new[] { ("Z_e", 3.5, "ohm"), ("theta", 95.0, "degC"), ("t_req", 6.0, "s"), ("U_0", 20.0, "V") })
            Assert.Contains(CalcInputValidator.Validate(Fault.Template, Defaults().With(CalcInputValue.Quantity(symbol, value, unit))), n => n.Severity == CalcNoticeSeverity.Block);
    }

    [Fact]
    public void Longer_Circuits_Raise_The_Loop_Impedance()
    {
        Gen.Double[0.1, 500].Sample(l =>
        {
            var shorter = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Quantity("L", l, "m"))).OutputSi("Z_s");
            var longer = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Quantity("L", l * 1.5, "m"))).OutputSi("Z_s");
            return longer > shorter;
        }, iter: 40);
    }

    [Fact]
    public void A_Long_Circuit_Fails_Disconnection()
    {
        var r = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Quantity("L", 60, "m")));
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Loop impedance", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void Curve_Points_That_Do_Not_Bracket_The_Required_Time_Block()
    {
        var r = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Choice("device", FaultProtectionCalculator.Curve)).With(FaultProtectionCalculator.Points((5, 120), (1, 180))));
        Assert.True(r.IsBlocked);
        Assert.Equal("curve", r.Blocks[0].RuleId);
    }

    [Fact]
    public void Log_Log_Interpolation_Returns_The_Points_And_Nothing_Outside()
    {
        var points = new List<(double T, double I)> { (0.1, 320), (0.4, 220), (5, 120) }.OrderBy(p => p.I).ToList();
        Assert.Equal(220, FaultProtectionCalculator.Interpolate(points, 0.4, byTime: true)!.Value, 9);
        Assert.Equal(0.4, FaultProtectionCalculator.Interpolate(points, 220, byTime: false)!.Value, 9);
        Assert.Null(FaultProtectionCalculator.Interpolate(points, 10, byTime: true));
    }

    [Fact]
    public void A_Let_Through_Energy_Replaces_The_Conservative_Estimate()
    {
        var conservative = CalcRunner.Run(Fault, Defaults());
        Assert.Contains(conservative.Warnings, w => w.RuleId == "let-through");
        var let = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Quantity("I2t_let", 2000, "A^2*s")));
        Assert.True(let.OutputSi("S_min") < conservative.OutputSi("S_min"));
        Assert.DoesNotContain(let.Warnings, w => w.RuleId == "let-through");
    }

    [Fact]
    public void Derived_K_Rises_With_The_Final_Temperature()
    {
        var pvc = CalcRunner.Run(Fault, Defaults()).OutputSi("k");
        var hotter = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Quantity("theta_f", 250, "degC"))).OutputSi("k");
        Assert.True(hotter > pvc);
    }
}
