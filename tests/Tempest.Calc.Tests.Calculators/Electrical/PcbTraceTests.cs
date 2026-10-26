using CsCheck;
using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>E7 — PCB trace width from the legacy IPC-2221 curve-fit: behaviour beyond the known-answer cases.</summary>
public sealed class PcbTraceTests
{
    private static readonly PcbTraceCalculator Pcb = new();

    private const double MilSquared = 25.4e-6 * 25.4e-6;

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Pcb.Template);

    private static CalcInputSet With(params CalcInputValue[] overrides) => overrides.Aggregate(Defaults(), (set, value) => set.With(value));

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Pcb, With(overrides));

    [Fact]
    public void The_Three_Amp_Ten_Kelvin_One_Ounce_Case_Gives_The_Widely_Quoted_Widths()
    {
        var external = Run(CalcInputValue.Choice("layer", PcbTraceCalculator.External));
        var inner = Run(CalcInputValue.Choice("layer", PcbTraceCalculator.Internal));
        Assert.Equal(1.37, external.OutputSi("w_min") * 1000, 2);
        Assert.Equal(3.56, inner.OutputSi("w_min") * 1000, 2);
    }

    [Fact]
    public void The_Internal_Layer_Needs_2_To_The_Power_One_Over_n_A_More_Area_Than_The_External()
    {
        var external = Run(CalcInputValue.Choice("layer", PcbTraceCalculator.External)).OutputSi("A_req");
        var inner = Run(CalcInputValue.Choice("layer", PcbTraceCalculator.Internal)).OutputSi("A_req");
        Assert.Equal(Math.Pow(2, 1 / 0.725), inner / external, 9);
    }

    [Fact]
    public void Doubling_The_Copper_Weight_Halves_The_Width_And_Leaves_The_Area()
    {
        var one = Run(CalcInputValue.Of("Cu_oz", 1));
        var two = Run(CalcInputValue.Of("Cu_oz", 2));
        Assert.Equal(1.0, two.OutputSi("A_req") / one.OutputSi("A_req"), 12);
        Assert.Equal(one.OutputSi("w_min") / 2, two.OutputSi("w_min"), 12);
    }

    [Fact]
    public void The_Area_Reproduces_The_Current_Through_The_Curve_Fit()
    {
        // Property: I = k·ΔT^0.44·A^0.725 recovers the input current from the computed area, for any current, rise and layer.
        // The curve-fit is refused above 35 A, under 10 K and for a minimum width above 10 mm (F2): 3 oz copper keeps a 12 A internal trace under 10 mm.
        Gen.Select(Gen.Double[0.05, 12], Gen.Double[10, 100], Gen.Bool).Sample((i, dt, inner) =>
        {
            var r = Run(CalcInputValue.Quantity("I", i, "A"), CalcInputValue.Quantity("dT", dt, "K"), CalcInputValue.Of("Cu_oz", 3), CalcInputValue.Choice("layer", inner ? PcbTraceCalculator.Internal : PcbTraceCalculator.External));
            var k = inner ? 0.024 : 0.048;
            var back = k * Math.Pow(dt, 0.44) * Math.Pow(r.OutputSi("A_req") / MilSquared, 0.725);
            return Math.Abs(back - i) <= 1e-9 * i;
        }, iter: 80);
    }

    [Fact]
    public void A_Larger_Current_Or_A_Smaller_Rise_Needs_A_Wider_Trace()
    {
        // 3 oz copper, up to 12 A and a rise of at least 12.5 K, so that every run stays inside the range of the fit (F2).
        Gen.Select(Gen.Double[0.1, 12], Gen.Double[12.5, 90]).Sample((i, dt) =>
        {
            var baseline = Run(CalcInputValue.Quantity("I", i, "A"), CalcInputValue.Quantity("dT", dt, "K"), CalcInputValue.Of("Cu_oz", 3)).OutputSi("w_min");
            var moreCurrent = Run(CalcInputValue.Quantity("I", i * 1.2, "A"), CalcInputValue.Quantity("dT", dt, "K"), CalcInputValue.Of("Cu_oz", 3)).OutputSi("w_min");
            var smallerRise = Run(CalcInputValue.Quantity("I", i, "A"), CalcInputValue.Quantity("dT", dt * 0.8, "K"), CalcInputValue.Of("Cu_oz", 3)).OutputSi("w_min");
            return moreCurrent > baseline && smallerRise > baseline;
        }, iter: 60);
    }

    [Fact]
    public void Resistance_Drop_And_Loss_Follow_Ohms_Law_And_Scale_With_Length()
    {
        var r = Run(CalcInputValue.Quantity("L", 80, "mm"));
        var longer = Run(CalcInputValue.Quantity("L", 160, "mm"));
        const double current = 3.0;
        Assert.Equal(current * r.OutputSi("trace_R"), r.OutputSi("vdrop"), 12);
        Assert.Equal(current * current * r.OutputSi("trace_R"), r.OutputSi("trace_Ploss"), 12);
        Assert.Equal(2 * r.OutputSi("trace_R"), longer.OutputSi("trace_R"), 12);
    }

    [Fact]
    public void A_Hotter_Board_Raises_The_Resistance_By_The_Copper_Temperature_Coefficient()
    {
        var cool = Run(CalcInputValue.Quantity("T_amb", 20, "degC"), CalcInputValue.Quantity("dT", 10, "K")).OutputSi("trace_R");
        var hot = Run(CalcInputValue.Quantity("T_amb", 70, "degC"), CalcInputValue.Quantity("dT", 10, "K")).OutputSi("trace_R");
        Assert.Equal((1 + 0.00393 * 60) / (1 + 0.00393 * 10), hot / cool, 9);
    }

    [Fact]
    public void A_Trace_Narrower_Than_The_Minimum_Fails_And_A_Wider_One_Passes()
    {
        var narrow = Run(CalcInputValue.Quantity("w_act", 1, "mm"));
        var wide = Run(CalcInputValue.Quantity("w_act", 1.5, "mm"));
        Assert.False(narrow.Passed);
        Assert.True(wide.Passed);
        Assert.True(narrow.OutputSi("u_w") > 1);
        Assert.True(wide.OutputSi("u_w") < 1);
    }

    [Fact]
    public void A_Current_Above_35_Amps_And_A_Width_Above_10_Millimetres_Are_Both_Refused()
    {
        // F2: both are beyond the range of the curve-fit and the sheet warned and passed; they are now blocks.
        var notices = CalcInputValidator.Validate(Pcb.Template, With(CalcInputValue.Quantity("I", 40, "A")));
        var current = Assert.Single(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.StartsWith("I:", StringComparison.Ordinal));
        Assert.Equal("input.I", current.RuleId);
        Assert.True(Run(CalcInputValue.Quantity("I", 40, "A")).IsBlocked);
        Assert.DoesNotContain(CalcInputValidator.Validate(Pcb.Template, With(CalcInputValue.Quantity("I", 35, "A"))), n => n.Severity == CalcNoticeSeverity.Block);
        Assert.Contains(Pcb.Template.ApplicabilityRules, r => r.Id == "input.I" && r.Severity == CalcNoticeSeverity.Block && r.Effect == CalcRuleEffect.Unconservative);

        // 15 A at a 10 K rise on 1 oz external copper needs w_min = [15/(0.048 x 10^0.44)]^(1/0.725)/1.378 mil = 12.585 mm (python3), beyond the 10 mm the fit was drawn for.
        var wide = Run(CalcInputValue.Quantity("I", 15, "A"), CalcInputValue.Quantity("dT", 10, "K"), CalcInputValue.Of("Cu_oz", 1), CalcInputValue.Quantity("w_act", 10, "mm"));
        Assert.True(wide.IsBlocked);
        var block = Assert.Single(wide.Blocks);
        Assert.Equal("width", block.RuleId);
        Assert.StartsWith("w_min:", block.Message, StringComparison.Ordinal);
        Assert.Empty(CalcMessageShape.Problems(block.Message));
        Assert.Null(wide.FindOutput("u_w"));
        Assert.DoesNotContain(Run().Notices, n => n.RuleId == "width");
    }

    [Fact]
    public void Copper_Outside_1_To_3_Ounces_Fails_A_Check_And_A_Rise_Below_10_Kelvin_Is_Refused()
    {
        // Inside the range the fit was drawn for (the ends included): no notice.
        foreach (var inside in new[] { Run(), Run(CalcInputValue.Of("Cu_oz", 3)), Run(CalcInputValue.Of("Cu_oz", 2), CalcInputValue.Quantity("dT", 100, "K")), Run(CalcInputValue.Quantity("dT", 10, "K")) })
        {
            Assert.DoesNotContain(inside.Notices, n => n.RuleId is "copper" or "rise");
            Assert.DoesNotContain(inside.Checks, k => k.Name.StartsWith("Copper weight against", StringComparison.Ordinal));
        }

        // F2: a copper weight outside the fit warned and the sheet passed an extrapolated width. It is a failing check (a block would refuse the half-ounce known-answer case).
        foreach (var oz in new[] { 0.5, 3.5, 6.0 })
        {
            var r = Run(CalcInputValue.Of("Cu_oz", oz), CalcInputValue.Quantity("w_act", 5, "mm"));
            Assert.Contains(r.Warnings, w => w.RuleId == "copper" && w.Message.StartsWith("Cu_oz:", StringComparison.Ordinal));
            var check = Assert.Single(r.Checks, k => k.Name.StartsWith("Copper weight against the range", StringComparison.Ordinal));
            Assert.False(check.Passed);
            Assert.False(r.Passed);
            Assert.True(r.OutputSi("u_w") < 1, "the width alone would have passed");
        }

        // A rise below 10 K is refused (it warned and passed an extrapolated width).
        foreach (var rise in new[] { 1.0, 5.0, 9.9 })
        {
            var r = Run(CalcInputValue.Quantity("dT", rise, "K"), CalcInputValue.Quantity("w_act", 20, "mm"));
            Assert.True(r.IsBlocked);
            var block = Assert.Single(r.Blocks);
            Assert.Equal("rise", block.RuleId);
            Assert.StartsWith("dT:", block.Message, StringComparison.Ordinal);
            Assert.Empty(CalcMessageShape.Problems(block.Message));
        }

        Assert.Contains(Pcb.Template.ApplicabilityRules, r => r.Id == "copper" && r.Severity == CalcNoticeSeverity.Warning && r.Effect == CalcRuleEffect.Unconservative);
        Assert.Contains(Pcb.Template.ApplicabilityRules, r => r.Id == "rise" && r.Severity == CalcNoticeSeverity.Block && r.Effect == CalcRuleEffect.Unconservative);
    }

    [Theory]
    [InlineData(17.5)]
    [InlineData(18)]
    [InlineData(35)]
    [InlineData(70)]
    [InlineData(105)]
    public void Black_B_B_02_A_Copper_Thickness_In_Micrometres_Typed_Into_The_Ounce_Field_Is_Blocked(double micrometres)
    {
        // 18 µm typed as "18 oz" gave w_min 0.10 mm and a PASS where the real half ounce needs 3.6 mm and fails.
        var result = Run(CalcInputValue.Of("Cu_oz", micrometres));
        var block = Assert.Single(result.Blocks, b => b.Message.StartsWith("Cu_oz", StringComparison.Ordinal));
        Assert.Contains("enter at most 6", block.Message, StringComparison.Ordinal);
        Assert.Contains("not a thickness in µm", block.Message, StringComparison.Ordinal);
        Assert.True(result.IsBlocked);

        // The ends of the valid range (Addendum B 0.5 to 6 oz/ft²) still run.
        foreach (var oz in new[] { 0.5, 6.0 })
            Assert.False(Run(CalcInputValue.Of("Cu_oz", oz), CalcInputValue.Quantity("w_act", 20, "mm")).IsBlocked);

        // What 18 µm really is: a half ounce, which this trace fails.
        var half = Run(CalcInputValue.Of("Cu_oz", 0.5), CalcInputValue.Quantity("w_act", 0.1, "mm"));
        Assert.True(half.OutputSi("u_w") > 1);
    }

    [Fact]
    public void Inputs_Outside_Their_Hard_Limits_Block_And_The_Method_Constants_Stay_Editable()
    {
        foreach (var (symbol, value, unit) in new[] { ("I", 0.0, "A"), ("dT", 0.0, "K"), ("dT", -5.0, "K"), ("w_act", 0.0, "mm"), ("L", -1.0, "mm") })
            Assert.Contains(CalcInputValidator.Validate(Pcb.Template, With(CalcInputValue.Quantity(symbol, value, unit))), n => n.Severity == CalcNoticeSeverity.Block);

        // The curve-fit constants are record values but stay ordinary inputs: a different k changes the answer.
        var standard = Run().OutputSi("w_min");
        var edited = Run(CalcInputValue.Of("k_ext", 0.096)).OutputSi("w_min");
        Assert.True(edited < standard);
        Assert.Equal(Math.Pow(2, -1 / 0.725), edited / standard, 9);
    }

    [Fact]
    public void The_Sheet_Carries_The_Legacy_Label_The_Ports_And_The_Method_Records()
    {
        var inputs = Defaults();
        var sheet = CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(Pcb.Template, inputs, CalcRunner.Run(Pcb, inputs), Verification.GoldenFiles.FixedHeader(Pcb.Template.Id, Pcb.Template.Title), Verification.GoldenFiles.FixedFooter(Pcb.Template, 0, 0)));
        Assert.Contains("legacy IPC-2221 curve-fit, conservative for multilayer boards", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("IPC-2152 chart", sheet, StringComparison.Ordinal);
        Assert.Equal(["trace_R", "trace_Ploss", "vdrop"], Pcb.Template.OutputPorts!.Select(p => p.Symbol));
        Assert.All(Pcb.Template.OutputPorts!, p => Assert.NotNull(CalcRunner.Run(Pcb, inputs).FindOutput(p.Symbol)));
        Assert.Contains(Pcb.Template.RecordKinds!, k => k.Kind == "pcb.method" && k.Symbols.Contains("k_ext") && k.Symbols.Contains("k_int"));
    }
}
