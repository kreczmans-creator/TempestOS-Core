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
        Gen.Select(Gen.Double[0.05, 35], Gen.Double[1, 100], Gen.Bool).Sample((i, dt, inner) =>
        {
            var r = Run(CalcInputValue.Quantity("I", i, "A"), CalcInputValue.Quantity("dT", dt, "K"), CalcInputValue.Choice("layer", inner ? PcbTraceCalculator.Internal : PcbTraceCalculator.External));
            var k = inner ? 0.024 : 0.048;
            var back = k * Math.Pow(dt, 0.44) * Math.Pow(r.OutputSi("A_req") / MilSquared, 0.725);
            return Math.Abs(back - i) <= 1e-9 * i;
        }, iter: 80);
    }

    [Fact]
    public void A_Larger_Current_Or_A_Smaller_Rise_Needs_A_Wider_Trace()
    {
        Gen.Select(Gen.Double[0.1, 30], Gen.Double[2, 90]).Sample((i, dt) =>
        {
            var baseline = Run(CalcInputValue.Quantity("I", i, "A"), CalcInputValue.Quantity("dT", dt, "K")).OutputSi("w_min");
            var moreCurrent = Run(CalcInputValue.Quantity("I", i * 1.2, "A"), CalcInputValue.Quantity("dT", dt, "K")).OutputSi("w_min");
            var smallerRise = Run(CalcInputValue.Quantity("I", i, "A"), CalcInputValue.Quantity("dT", dt * 0.8, "K")).OutputSi("w_min");
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
    public void A_Current_Above_35_Amps_And_A_Width_Above_10_Millimetres_Both_Warn()
    {
        var heavy = Run(CalcInputValue.Quantity("I", 40, "A"), CalcInputValue.Quantity("dT", 20, "K"), CalcInputValue.Of("Cu_oz", 1), CalcInputValue.Quantity("w_act", 10, "mm"));
        var notices = CalcInputValidator.Validate(Pcb.Template, With(CalcInputValue.Quantity("I", 40, "A")));
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Warning && n.Message.StartsWith("I ", StringComparison.Ordinal));
        Assert.Contains(heavy.Warnings, w => w.RuleId == "width");
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "width");
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
