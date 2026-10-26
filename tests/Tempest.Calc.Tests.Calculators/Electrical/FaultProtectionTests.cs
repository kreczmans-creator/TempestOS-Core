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
    public void A_Let_Through_Energy_Replaces_The_End_Of_Line_Estimate()
    {
        var estimate = CalcRunner.Run(Fault, Defaults());
        Assert.Contains(estimate.Warnings, w => w.RuleId == "let-through");
        var let = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Quantity("I2t_let", 2000, "A^2*s")));
        Assert.True(let.OutputSi("S_min") < estimate.OutputSi("S_min"));
        Assert.DoesNotContain(let.Warnings, w => w.RuleId == "let-through");
    }

    [Fact]
    public void The_Line_Conductor_Is_Not_Checked_Against_The_End_Of_Line_Fault_Energy()
    {
        // Green G-B-03: with 1.5 mm2 line and CPC and no let-through value, I_f^2 * 0.1 s at the end-of-line (minimum) fault gave a
        // line check of 0.45-0.5 "PASS". The line conductor (reg. 434.5.2) needs the let-through at the maximum prospective fault.
        var withoutLet = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Quantity("S_line", 1.5, "mm^2")));
        Assert.Null(withoutLet.FindOutput("u_line"));
        Assert.DoesNotContain(withoutLet.Checks, c => c.Name.StartsWith("Line conductor", StringComparison.Ordinal));
        Assert.Contains(withoutLet.Checks, c => c.Name.StartsWith("Protective conductor", StringComparison.Ordinal));
        var warning = Assert.Single(withoutLet.Warnings, w => w.RuleId == "let-through");
        Assert.Contains("line conductor check", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("conservative", warning.Message, StringComparison.OrdinalIgnoreCase);

        // A let-through of 3e4 A2s at the maximum fault needs sqrt(3e4)/k = 1.5083 mm2: 1.5 mm2 fails, 2.5 mm2 passes.
        var k = 114.83611;
        var thin = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Quantity("S_line", 1.5, "mm^2")).With(CalcInputValue.Quantity("I2t_let", 3e4, "A^2*s")));
        Assert.Equal(Math.Sqrt(3e4) / k / 1.5, thin.OutputSi("u_line"), 4);
        Assert.Contains(thin.Checks, c => c.Name.StartsWith("Line conductor", StringComparison.Ordinal) && !c.Passed);
        var thick = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Quantity("S_line", 2.5, "mm^2")).With(CalcInputValue.Quantity("I2t_let", 3e4, "A^2*s")));
        Assert.Contains(thick.Checks, c => c.Name.StartsWith("Line conductor", StringComparison.Ordinal) && c.Passed);
    }

    [Fact]
    public void The_Disconnection_Time_Check_Carries_A_Real_Ratio()
    {
        // Black B-A-14: the time check printed utilisation 0 whenever it passed. t_d = 0.1 s against t_req = 0.4 s is 0.25.
        var r = CalcRunner.Run(Fault, Defaults());
        var check = Assert.Single(r.Checks, c => c.Name == "Disconnection time within required");
        Assert.Equal(0.25, check.Utilisation, 9);
        Assert.True(check.Passed);
        Assert.Equal("0.1000 s", check.DemandText);
        Assert.Equal("0.4000 s", check.CapacityText);
    }

    [Theory]
    [InlineData(1, FaultProtectionCalculator.McbB)]
    [InlineData(2, FaultProtectionCalculator.McbC)]
    [InlineData(3, FaultProtectionCalculator.McbD)]
    [InlineData(4, FaultProtectionCalculator.Curve)]
    public void A_Device_Carried_From_The_Cable_Step_Must_Be_The_Device_Chosen(int code, string device)
    {
        // Green G-B-01 / Black B-A-03: E4 sized a type C circuit, E5 silently checked type B and passed loop impedances up to twice the limit.
        var inputs = Defaults().With(CalcInputValue.Of("device_code", code)).With(FaultProtectionCalculator.Points((5, 120), (0.4, 220), (0.1, 320), (0.01, 700)));
        Assert.False(CalcRunner.Run(Fault, inputs.With(CalcInputValue.Choice("device", device))).IsBlocked);

        foreach (var other in new[] { FaultProtectionCalculator.McbB, FaultProtectionCalculator.McbC, FaultProtectionCalculator.McbD, FaultProtectionCalculator.Curve }.Where(d => d != device))
        {
            var r = CalcRunner.Run(Fault, inputs.With(CalcInputValue.Choice("device", other)));
            var block = Assert.Single(r.Blocks);
            Assert.Equal("device", block.RuleId);
            Assert.StartsWith("device:", block.Message, StringComparison.Ordinal);
            Assert.Contains(device, block.Message, StringComparison.Ordinal);
            Assert.Empty(r.Outputs);
        }
    }

    [Fact]
    public void A_Type_C_Device_Halves_The_Allowed_Loop_Impedance_Of_The_Type_B_Default()
    {
        // Same circuit, 150 m: the type B default passes with u_Zs 0.6145, the type C device E4 chose fails with 1.2290.
        var circuit = Defaults().With(CalcInputValue.Quantity("L", 150, "m")).With(CalcInputValue.Quantity("I_n", 6, "A")).With(CalcInputValue.Quantity("S_line", 1.5, "mm^2")).With(CalcInputValue.Quantity("S_cpc", 1.5, "mm^2"));
        var typeB = CalcRunner.Run(Fault, circuit.With(CalcInputValue.Choice("device", FaultProtectionCalculator.McbB)));
        var typeC = CalcRunner.Run(Fault, circuit.With(CalcInputValue.Choice("device", FaultProtectionCalculator.McbC)));
        Assert.Equal(2 * typeB.OutputSi("u_Zs"), typeC.OutputSi("u_Zs"), 9);
        Assert.Contains(typeC.Checks, c => c.Name.StartsWith("Loop impedance", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(typeB.Checks, c => c.Name.StartsWith("Loop impedance", StringComparison.Ordinal) && c.Passed);
    }

    [Fact]
    public void A_Fractional_Or_Out_Of_Range_Device_Code_Is_Refused()
    {
        Assert.Contains(CalcInputValidator.Validate(Fault.Template, Defaults().With(CalcInputValue.Of("device_code", 5))), n => n.Severity == CalcNoticeSeverity.Block);
        Assert.Contains(CalcInputValidator.Validate(Fault.Template, Defaults().With(CalcInputValue.Of("device_code", 0))), n => n.Severity == CalcNoticeSeverity.Block);
        var r = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Of("device_code", 1.5)));
        Assert.Equal("device_code", Assert.Single(r.Blocks).RuleId);
    }

    [Fact]
    public void Derived_K_Rises_With_The_Final_Temperature()
    {
        var pvc = CalcRunner.Run(Fault, Defaults()).OutputSi("k");
        var hotter = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Quantity("theta_f", 250, "degC"))).OutputSi("k");
        Assert.True(hotter > pvc);
    }

    [Fact]
    public void No_Disconnection_Means_No_Adiabatic_Claim()
    {
        // Opus review finding 12: a 200 m circuit on a 32 A type B MCB never reaches 5 I_n.
        var r = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Quantity("L", 200, "m")));
        Assert.Null(r.FindOutput("S_min"));
        Assert.Contains(r.Warnings, w => w.RuleId == "adiabatic");
        Assert.Contains(r.Checks, c => c.Name == "Disconnection achieved" && !c.Passed);
    }

    [Fact]
    public void A_Fault_Current_Above_The_Curve_Uses_The_Fastest_Point_As_An_Upper_Bound()
    {
        // Opus verification: a low-impedance circuit faster than the fastest point entered still disconnects.
        var r = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Choice("device", FaultProtectionCalculator.Curve))
            .With(FaultProtectionCalculator.Points((5, 60), (0.4, 120), (0.1, 180))).With(CalcInputValue.Quantity("L", 2, "m")));
        Assert.Equal(0.1, r.OutputSi("t_d"), 12);
        Assert.NotNull(r.FindOutput("S_min"));
        Assert.Contains(r.Warnings, w => w.RuleId == "curve-range" && w.Message.Contains("upper bound", StringComparison.Ordinal));
        Assert.DoesNotContain(r.Checks, c => c.Name == "Disconnection achieved");
    }

    [Fact]
    public void The_Breaking_Capacity_Is_Checked_Even_When_Disconnection_Fails()
    {
        var r = CalcRunner.Run(Fault, Defaults().With(CalcInputValue.Quantity("L", 200, "m"))
            .With(CalcInputValue.Quantity("S_r", 500, "kVA")).With(CalcInputValue.Quantity("u_k", 5, "%")).With(CalcInputValue.Quantity("I_cn", 10, "kA")));
        Assert.Contains(r.Checks, c => c.Name == "Breaking capacity against prospective fault" && !c.Passed);
    }

    [Fact]
    public void The_Voltage_Factor_The_Fault_Current_Was_Computed_With_Is_A_Port()
    {
        var withTransformer = new[] { CalcInputValue.Quantity("S_r", 500, "kVA"), CalcInputValue.Quantity("u_k", 5, "%") };
        var standard = CalcRunner.Run(Fault, withTransformer.Aggregate(Defaults(), (set, v) => set.With(v)));
        Assert.Equal(1.1, standard.OutputSi("c_k3"), 12);

        var lower = CalcRunner.Run(Fault, withTransformer.Aggregate(Defaults(), (set, v) => set.With(v)).With(CalcInputValue.Of("c_max", 1.05)));
        Assert.Equal(1.05, lower.OutputSi("c_k3"), 12);
        Assert.Equal(standard.OutputSi("I_k3") * 1.05 / 1.1, lower.OutputSi("I_k3"), 6);
        Assert.Contains(Fault.Template.OutputPorts!, p => p.Symbol == "c_k3");
    }
}
