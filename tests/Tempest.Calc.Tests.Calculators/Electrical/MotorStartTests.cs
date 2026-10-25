using CsCheck;
using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>E9 — motor start voltage dip and PF correction: behaviour beyond the known-answer cases.</summary>
public sealed class MotorStartTests
{
    private static readonly MotorStartCalculator Start = new();
    private static readonly VoltageDropCalculator VoltageDrop = new();
    private static readonly FaultProtectionCalculator Fault = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Start.Template);

    private static CalcInputSet With(params CalcInputValue[] overrides) => overrides.Aggregate(Defaults(), (set, value) => set.With(value));

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Start, With(overrides));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue N(string symbol, double value) => CalcInputValue.Of(symbol, value);

    private static CalcInputValue Choice(string symbol, string choice) => CalcInputValue.Choice(symbol, choice);

    /// <summary>The hand phasor case: Z_s = Z_c = 0.03 + j0.04 Ω and Z_m = 0.24 + j0.32 Ω, all at 53.13°.</summary>
    private static CalcRunResult HandCase(params CalcInputValue[] more) => Run(
    [
        Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisSk), Q("S_k", 3.2, "MVA"), N("c_src", 1), N("XR", 4.0 / 3.0),
        Q("I_n", 100, "A"), N("I_LR_r", 10 / Math.Sqrt(3)), N("cos_phi_st", 0.6),
        Choice("cable_data", MotorStartCalculator.DataPerKm), Q("L_cab", 100, "m"), Q("R_km", 0.3, "ohm/km"), Q("X_km", 0.4, "ohm/km"),
        .. more,
    ]);

    [Fact]
    public void When_All_Three_Impedances_Share_An_Angle_The_Dip_Is_The_Plain_Ratio_Of_Magnitudes()
    {
        var r = HandCase();
        Assert.Equal(20.0, r.OutputSi("start_dip") * 100, 9);
        Assert.Equal(10.0, r.OutputSi("dip_pcc") * 100, 9);
        Assert.Equal(0.4 / 0.5, r.OutputSi("V_m") / 400, 12);
        Assert.Equal(0.9, r.OutputSi("V_pcc") / 400, 12);
    }

    [Fact]
    public void A_Purely_Resistive_Circuit_Divides_Like_Scalars()
    {
        // X/R = 0, no cable reactance, unity starting power factor: Z_s = 0.05, Z_c = 0.05 (100 m of 0.5 Ω/km), Z_m = 0.4 → dip 20 %, PCC 10 %.
        var r = HandCase(N("XR", 0), Q("X_km", 0, "ohm/km"), Q("R_km", 0.5, "ohm/km"), N("cos_phi_st", 1));
        Assert.Equal(20.0, r.OutputSi("start_dip") * 100, 9);
        Assert.Equal(10.0, r.OutputSi("dip_pcc") * 100, 9);
        Assert.Equal(1 - 0.4 / 0.5, r.OutputSi("start_dip"), 12);
    }

    [Fact]
    public void The_Cable_Drop_Along_The_Motor_Voltage_Equals_E3_At_The_Same_Current_And_Power_Factor()
    {
        // Cross-check against E3 for the same cable, the dipped current and the starting power factor (three-phase, resistivity route).
        foreach (var overrides in new CalcInputValue[][]
        {
            [],
            [Choice("starter", MotorStartCalculator.StarterStarDelta), Q("L_cab", 90, "m"), Q("A_c", 25, "mm^2"), N("cos_phi_st", 0.4)],
            [Choice("source", MotorStartCalculator.SourceFault), Q("S_k", 6, "MVA"), N("XR", 6), Q("L_cab", 150, "m"), Q("A_c", 70, "mm^2"), Q("X_km", 0.1, "ohm/km"), N("n_par", 2), N("cos_phi_st", 0.25)],
        })
        {
            var inputs = With(overrides);
            var r = CalcRunner.Run(Start, inputs);
            var e3 = CalcRunner.Run(VoltageDrop, CalcTemplateHasher.DefaultInputs(VoltageDrop.Template)
                .With(Choice("system", VoltageDropCalculator.SystemThree))
                .With(Q("V_n", 400, "V"))
                .With(Q("I", r.OutputSi("I_dip"), "A"))
                .With(N("cos_phi", inputs.Find("cos_phi_st")!.Number!.Value))
                .With(Q("L", inputs.Find("L_cab")!.Number!.Value, "m"))
                .With(N("n_par", inputs.Find("n_par")!.Number!.Value))
                .With(Choice("data", VoltageDropCalculator.DataResistivity))
                .With(Q("A_c", inputs.Find("A_c")!.Number!.Value, "mm^2"))
                .With(Q("T_c", 70, "degC"))
                .With(Q("X_km", inputs.Find("X_km")!.Number!.Value, "ohm/km")));
            Assert.False(e3.IsBlocked, string.Join("; ", e3.Blocks.Select(b => b.Message)));
            Assert.Equal(e3.OutputSi("dV"), r.OutputSi("dV_cab"), 9);
        }
    }

    [Fact]
    public void The_Hand_Case_Cable_Drop_Is_Forty_Volts_In_Both_Calculators()
    {
        var r = HandCase();
        Assert.Equal(40.0, r.OutputSi("dV_cab"), 9);

        var e3 = CalcRunner.Run(VoltageDrop, CalcTemplateHasher.DefaultInputs(VoltageDrop.Template)
            .With(Choice("system", VoltageDropCalculator.SystemThree)).With(Q("V_n", 400, "V")).With(Q("I", r.OutputSi("I_dip"), "A")).With(N("cos_phi", 0.6))
            .With(Q("L", 100, "m")).With(Choice("data", VoltageDropCalculator.DataPerKm)).With(Q("R_km", 0.3, "ohm/km")).With(Q("X_km", 0.4, "ohm/km")));
        Assert.Equal(40.0, e3.OutputSi("dV"), 9);
    }

    [Fact]
    public void Starter_Types_Set_The_Starting_Current_And_Soft_Start_Never_Exceeds_Locked_Rotor()
    {
        var dol = Run(Choice("starter", MotorStartCalculator.StarterDol));
        var star = Run(Choice("starter", MotorStartCalculator.StarterStarDelta));
        var soft = Run(Choice("starter", MotorStartCalculator.StarterSoft), N("k_soft", 3));
        var softAboveLr = Run(Choice("starter", MotorStartCalculator.StarterSoft), N("k_soft", 12));
        var vfd = Run(Choice("starter", MotorStartCalculator.StarterVfd), N("k_vfd", 1.2));
        var inRated = dol.OutputSi("I_rated");

        Assert.Equal(7 * inRated, dol.OutputSi("I_start"), 9);
        Assert.Equal(7 * inRated / 3, star.OutputSi("I_start"), 9);
        Assert.Equal(3 * inRated, soft.OutputSi("I_start"), 9);
        Assert.Equal(7 * inRated, softAboveLr.OutputSi("I_start"), 9);
        Assert.Equal(1.2 * inRated, vfd.OutputSi("I_start"), 9);

        // A lower starting current gives a smaller dip.
        Assert.True(star.OutputSi("start_dip") < dol.OutputSi("start_dip"));
        Assert.True(soft.OutputSi("start_dip") < dol.OutputSi("start_dip"));
        Assert.True(vfd.OutputSi("start_dip") < soft.OutputSi("start_dip"));
    }

    [Fact]
    public void A_Transformer_A_Fault_Level_And_A_Fault_Current_Describe_The_Same_Source_Identically()
    {
        // 500 kVA, 4 % at 400 V is 12.5 MVA, which at c = 1 is I_k = 12.5 MVA/(√3·400 V) = 18.0422 kA.
        var transformer = Run(Choice("source", MotorStartCalculator.SourceTransformer), Q("S_n", 500, "kVA"), Q("u_k", 4, "%"));
        var level = Run(Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisSk), Q("S_k", 12.5, "MVA"), N("c_src", 1));
        var current = Run(Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisIk), Q("I_k", 12.5e3 / (Math.Sqrt(3) * 400), "kA"), N("c_src", 1));

        Assert.Equal(0.0128, transformer.OutputSi("Z_src"), 12);
        Assert.Equal(transformer.OutputSi("start_dip"), level.OutputSi("start_dip"), 12);
        Assert.Equal(transformer.OutputSi("start_dip"), current.OutputSi("start_dip"), 9);
    }

    [Fact]
    public void The_Source_Impedance_From_E5_Fault_Current_Reproduces_E5_Transformer_Impedance()
    {
        // Link-ready: E5 I_k3 (kA, computed with c = 1.1) into I_k with c_src = 1.1 recovers E5's own Z_T.
        var e5 = CalcRunner.Run(Fault, CalcTemplateHasher.DefaultInputs(Fault.Template).With(Q("S_r", 800, "kVA")).With(Q("u_k", 6, "%")));
        var uLine = Math.Sqrt(3) * 230;
        var r = Run(Q("U", uLine, "V"), Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisIk), Q("I_k", e5.OutputSi("I_k3") / 1000, "kA"), N("c_src", 1.1));
        Assert.Equal(e5.OutputSi("Z_T"), r.OutputSi("Z_src"), 9);
        Assert.Equal(Start.Template.Inputs.Single(i => i.Symbol == "I_k").DefaultUnit, Fault.Template.OutputPorts!.Single(p => p.Symbol == "I_k3").Unit);
    }

    [Fact]
    public void A_Heavier_Start_A_Weaker_Source_Or_A_Longer_Cable_Deepens_The_Dip_And_The_Point_Of_Coupling_Never_Dips_More_Than_The_Motor()
    {
        Gen.Select(Gen.Double[0.5, 40], Gen.Double[1, 12], Gen.Double[0, 400], Gen.Double[0.1, 0.9]).Sample((sk, xr, length, cos) =>
        {
            CalcRunResult Go(double skMva, double l, double ratio) => Run(Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisSk), Q("S_k", skMva, "MVA"), N("XR", xr), Q("L_cab", l, "m"), N("cos_phi_st", cos), N("I_LR_r", ratio));
            var baseline = Go(sk, length, 6);
            var weaker = Go(sk * 0.8, length, 6);
            var longer = Go(sk, length + 20, 6);
            var heavier = Go(sk, length, 7);
            var dip = baseline.OutputSi("start_dip");
            return dip >= 0 && dip < 1 && baseline.OutputSi("dip_pcc") <= dip + 1e-12
                && weaker.OutputSi("start_dip") > dip && longer.OutputSi("start_dip") > dip && heavier.OutputSi("start_dip") > dip;
        }, iter: 80);
    }

    [Fact]
    public void The_Dip_Is_Checked_Against_The_Limit_And_An_Optional_Limit_At_The_Point_Of_Common_Coupling()
    {
        var pass = Run(Q("dip_limit", 10, "%"));
        var fail = Run(Q("dip_limit", 5, "%"));
        Assert.True(pass.Passed);
        Assert.False(fail.Passed);
        Assert.Equal(pass.OutputSi("start_dip") / 0.10, pass.OutputSi("u_dip"), 12);
        Assert.Equal(pass.OutputSi("start_dip") / 0.05, fail.OutputSi("u_dip"), 12);

        var tight = Run(Q("dip_limit", 10, "%"), Q("dip_limit_pcc", 3, "%"));
        Assert.False(tight.Passed);
        Assert.Contains(tight.Checks, c => c.Name.Contains("coupling", StringComparison.Ordinal) && !c.Passed);
        Assert.DoesNotContain(pass.Checks, c => c.Name.Contains("coupling", StringComparison.Ordinal));
    }

    [Fact]
    public void Capacitor_Kvar_Follows_P_Times_The_Difference_Of_Tangents_And_Warns_When_No_Capacitor_Is_Needed()
    {
        var r = Run(Q("P", 22, "kW"), N("eta", 0.91), N("cos_phi", 0.83), N("cos_phi2", 0.95));
        var pIn = 22 / 0.91;
        var expected = pIn * (Math.Tan(Math.Acos(0.83)) - Math.Tan(Math.Acos(0.95)));
        Assert.Equal(expected, r.OutputSi("kvar") / 1000, 9);
        Assert.Equal(expected * 1000 / (Math.Sqrt(3) * 400), r.OutputSi("I_cap"), 9);
        Assert.True(r.OutputSi("I_run2") < r.OutputSi("I_run1"));
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "pfc");

        var none = Run(N("cos_phi", 0.95), N("cos_phi2", 0.9));
        Assert.Equal(0, none.OutputSi("kvar"));
        Assert.Contains(none.Warnings, w => w.RuleId == "pfc");
        Assert.Null(none.FindOutput("h_res"));
    }

    [Fact]
    public void The_Resonance_Order_Is_Shown_And_Only_Warns_When_Harmonic_Loads_Are_Flagged()
    {
        // A weak 0.5 MVA bus with about 20 kvar: h ≈ √(500/20) ≈ 5.
        var weak = new[] { Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisSk), Q("S_k", 0.5, "MVA"), N("cos_phi", 0.8), N("cos_phi2", 0.95), Q("P", 40, "kW"), N("eta", 0.92) };
        var quiet = Run(weak);
        var flagged = Run([.. weak, CalcInputValue.Switch("harmonics", true)]);
        var kvar = quiet.OutputSi("kvar") / 1000;

        Assert.Equal(Math.Sqrt(500.0 / kvar), quiet.OutputSi("h_res"), 9);
        Assert.InRange(quiet.OutputSi("h_res"), 3.5, 5.5);
        Assert.DoesNotContain(quiet.Warnings, w => w.RuleId == "harmonics");
        var warning = Assert.Single(flagged.Warnings, w => w.RuleId == "harmonics");
        Assert.StartsWith("harmonics:", warning.Message, StringComparison.Ordinal);
        Assert.Contains("harmonic order", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Capacitors_Are_Flagged_On_A_Drive_Or_A_Soft_Starter_But_Not_Direct_On_Line()
    {
        Assert.Contains(Run(Choice("starter", MotorStartCalculator.StarterVfd)).Warnings, w => w.RuleId == "pfc-starter");
        Assert.Contains(Run(Choice("starter", MotorStartCalculator.StarterSoft)).Warnings, w => w.RuleId == "pfc-starter");
        Assert.DoesNotContain(Run(Choice("starter", MotorStartCalculator.StarterDol)).Warnings, w => w.RuleId == "pfc-starter");
        Assert.DoesNotContain(Run(Choice("starter", MotorStartCalculator.StarterStarDelta)).Warnings, w => w.RuleId == "pfc-starter");
    }

    [Fact]
    public void Inputs_Outside_Their_Hard_Ranges_Block_And_A_Missing_R_Per_Km_Says_What_To_Enter()
    {
        foreach (var (symbol, value, unit) in new[] { ("cos_phi_st", 0.0, ""), ("cos_phi_st", 1.2, ""), ("cos_phi2", 0.4, ""), ("cos_phi", 0.0, ""), ("eta", 1.2, ""), ("I_LR_r", 0.5, ""), ("I_LR_r", 30.0, ""), ("XR", -1.0, ""), ("c_src", 0.5, ""), ("U", 20.0, "V"), ("L_cab", -1.0, "m"), ("u_k", 40.0, "%"), ("dip_limit", 0.0, "%") })
        {
            var spec = Start.Template.Inputs.Single(i => i.Symbol == symbol);
            var changed = With(spec.Kind == CalcInputKind.Quantity ? Q(symbol, value, unit) : N(symbol, value));
            Assert.Contains(CalcInputValidator.Validate(Start.Template, changed), n => n.Severity == CalcNoticeSeverity.Block);
        }

        var missing = Run(Choice("cable_data", MotorStartCalculator.DataPerKm));
        var block = Assert.Single(missing.Blocks, b => b.RuleId == "cable-data");
        Assert.StartsWith("R_km:", block.Message, StringComparison.Ordinal);
        Assert.Contains("enter", block.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_Sheet_Shows_The_Phasor_Working_And_The_Ports_And_Record_Kinds_Are_Declared()
    {
        var inputs = Defaults();
        var result = CalcRunner.Run(Start, inputs);
        var sheet = CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(Start.Template, inputs, result, Verification.GoldenFiles.FixedHeader(Start.Template.Id, Start.Template.Title), Verification.GoldenFiles.FixedFooter(Start.Template, 0, 0)));

        foreach (var phasor in new[] { "Z_s_ph", "Z_c_ph", "Z_m_ph", "Z_t_ph", "I_ph", "V_m_ph", "V_pcc_ph" })
            Assert.Contains(phasor, sheet, StringComparison.Ordinal);

        Assert.Contains("∠", sheet, StringComparison.Ordinal);
        Assert.Equal(["start_dip", "kvar", "I_start"], Start.Template.OutputPorts!.Select(p => p.Symbol));
        Assert.All(Start.Template.OutputPorts!, p => Assert.NotNull(result.FindOutput(p.Symbol)));
        Assert.Contains(Start.Template.RecordKinds!, k => k.Kind == "cable.impedance" && k.Symbols.Contains("R_km") && k.Symbols.Contains("X_km"));
    }
}
