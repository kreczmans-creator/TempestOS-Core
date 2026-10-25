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
            [Choice("starter", MotorStartCalculator.StarterSoft), N("k_soft", 3), Q("L_cab", 120, "m"), Q("A_c", 50, "mm^2"), N("cos_phi_st", 0.35)],
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
        Assert.Contains(Run(Choice("starter", MotorStartCalculator.StarterVfd), N("cos_phi_vfd", 0.85)).Warnings, w => w.RuleId == "pfc-starter");
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
        Assert.Equal(["start_dip", "kvar", "I_start", "I_rated"], Start.Template.OutputPorts!.Select(p => p.Symbol));
        Assert.DoesNotContain("∠ -", sheet, StringComparison.Ordinal);
        Assert.Contains("∠ −", sheet, StringComparison.Ordinal);
        Assert.All(Start.Template.OutputPorts!, p => Assert.NotNull(result.FindOutput(p.Symbol)));
        Assert.Contains(Start.Template.RecordKinds!, k => k.Kind == "cable.impedance" && k.Symbols.Contains("R_km") && k.Symbols.Contains("X_km"));
    }

    /// <summary>The held-current solution by fixed-point iteration of the phasors, independent of the closed form the calculator uses.</summary>
    private static (System.Numerics.Complex V, System.Numerics.Complex I) HeldCurrent(double e, System.Numerics.Complex zl, double held, double cos)
    {
        var phi = Math.Acos(cos);
        var v = new System.Numerics.Complex(e, 0);

        for (var i = 0; i < 20000; i++)
        {
            var current = System.Numerics.Complex.FromPolarCoordinates(held, v.Phase - phi);
            var next = e - current * zl;

            if ((next - v).Magnitude < 1e-13)
            {
                v = next;
                break;
            }

            v = next;
        }

        return (v, System.Numerics.Complex.FromPolarCoordinates(held, v.Phase - phi));
    }

    [Fact]
    public void A_Soft_Starter_In_Current_Limit_And_A_Drive_Hold_The_Current_And_Match_An_Independent_Phasor_Solution()
    {
        // S_k 8 MVA, X/R 6, 80 m of 95 mm² at 70 °C (R_km explicit so the test owns the numbers), cos φ_st 0.3.
        var zQ = 400.0 * 400 / 8e6;
        var r = zQ / Math.Sqrt(1 + 36);
        var zl = new System.Numerics.Complex(r, 6 * r) + new System.Numerics.Complex(0.01724 * (1 + 0.00393 * 50) * 80 / 95, 0.08 * 80 / 1000);
        var e = 400 / Math.Sqrt(3);

        var soft = Run(Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisSk), Q("S_k", 8, "MVA"), N("c_src", 1), N("XR", 6), Q("P", 132, "kW"), N("eta", 0.955), N("cos_phi", 0.89), N("I_LR_r", 6.5),
            Choice("starter", MotorStartCalculator.StarterSoft), N("k_soft", 3), Q("L_cab", 80, "m"), Q("A_c", 95, "mm^2"), N("cos_phi_st", 0.3));
        var held = 3 * soft.OutputSi("I_rated");
        var (v, i) = HeldCurrent(e, zl, held, 0.3);

        Assert.Equal(held, soft.OutputSi("I_dip"), 9);
        Assert.Equal(v.Magnitude * Math.Sqrt(3), soft.OutputSi("V_m"), 6);
        Assert.Equal(100 * (1 - v.Magnitude / e), soft.OutputSi("start_dip") * 100, 8);

        var vfd = Run(Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisSk), Q("S_k", 8, "MVA"), N("c_src", 1), N("XR", 6), Q("P", 132, "kW"), N("eta", 0.955), N("cos_phi", 0.89),
            Choice("starter", MotorStartCalculator.StarterVfd), N("k_vfd", 1.2), N("cos_phi_vfd", 0.9), Q("L_cab", 80, "m"), Q("A_c", 95, "mm^2"));
        var (vv, _) = HeldCurrent(e, zl, 1.2 * vfd.OutputSi("I_rated"), 0.9);
        Assert.Equal(1.2 * vfd.OutputSi("I_rated"), vfd.OutputSi("I_dip"), 9);
        Assert.Equal(100 * (1 - vv.Magnitude / e), vfd.OutputSi("start_dip") * 100, 8);
        Assert.Equal(vfd.OutputSi("V_m") / Math.Sqrt(3) / vfd.OutputSi("I_dip"), vfd.OutputSi("Z_m"), 9);
    }

    [Fact]
    public void A_Held_Current_Dips_More_Than_A_Passive_Impedance_Drawing_The_Same_Nominal_Current()
    {
        // The regression behind the review: a fixed impedance lets the current fall with the voltage, so it understates the dip of a current-limited start.
        var passive = Run(N("I_LR_r", 3), Choice("starter", MotorStartCalculator.StarterDol));
        var held = Run(N("I_LR_r", 7), Choice("starter", MotorStartCalculator.StarterSoft), N("k_soft", 3));
        Assert.Equal(passive.OutputSi("I_start"), held.OutputSi("I_start"), 9);
        Assert.True(held.OutputSi("start_dip") > passive.OutputSi("start_dip"));
        Assert.True(held.OutputSi("dip_pcc") > passive.OutputSi("dip_pcc"));
        Assert.True(held.OutputSi("I_dip") > passive.OutputSi("I_dip"));
        Assert.Contains("starter or drive input", held.FindOutput("V_m")!.Description, StringComparison.Ordinal);
        Assert.NotNull(passive.FindOutput("k_T"));
        Assert.Null(held.FindOutput("k_T"));
        Assert.Null(Run(Choice("starter", MotorStartCalculator.StarterVfd)).FindOutput("k_T"));
    }

    [Fact]
    public void A_Supply_That_Cannot_Hold_The_Regulated_Current_Blocks_Naming_The_Limit_To_Lower()
    {
        var weak = Run(Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisSk), Q("S_k", 0.01, "MVA"), Choice("starter", MotorStartCalculator.StarterVfd), N("k_vfd", 5));
        var block = Assert.Single(weak.Blocks, b => b.RuleId == "current-limit");
        Assert.StartsWith("k_vfd:", block.Message, StringComparison.Ordinal);
        var soft = Run(Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisSk), Q("S_k", 0.01, "MVA"), Choice("starter", MotorStartCalculator.StarterSoft), N("k_soft", 5));
        Assert.StartsWith("k_soft:", Assert.Single(soft.Blocks, b => b.RuleId == "current-limit").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Regulated_Dips_Deepen_With_A_Weaker_Source_A_Longer_Cable_Or_A_Higher_Current_Limit()
    {
        Gen.Select(Gen.Double[2, 40], Gen.Double[1, 12], Gen.Double[0, 300], Gen.Double[0.2, 0.95]).Sample((sk, xr, length, cos) =>
        {
            CalcRunResult Go(double skMva, double l, double k) => Run(Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisSk), Q("S_k", skMva, "MVA"), N("XR", xr), Q("L_cab", l, "m"), Choice("starter", MotorStartCalculator.StarterSoft), N("k_soft", k), N("cos_phi_st", cos));
            var baseline = Go(sk, length, 3);
            var dip = baseline.OutputSi("start_dip");
            return dip >= 0 && dip < 1 && baseline.OutputSi("dip_pcc") <= dip + 1e-12
                && Go(sk * 0.8, length, 3).OutputSi("start_dip") > dip && Go(sk, length + 20, 3).OutputSi("start_dip") > dip && Go(sk, length, 3.5).OutputSi("start_dip") > dip;
        }, iter: 60);
    }

    [Fact]
    public void The_I_k_Basis_Defaults_C_To_1_1_And_A_Linked_I_k_With_Another_C_Warns()
    {
        var common = new[] { Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisIk), Q("I_k", 12, "kA") };
        var blank = Run(common);
        Assert.Equal(1.1 * 400 / (Math.Sqrt(3) * 12e3), blank.OutputSi("Z_src") * 1.0, 9);
        var explicitOne = Run([.. common, N("c_src", 1)]);
        Assert.Equal(400 / (Math.Sqrt(3) * 12e3), explicitOne.OutputSi("Z_src"), 9);
        Assert.DoesNotContain(blank.Warnings, w => w.RuleId == "c_src");
        Assert.DoesNotContain(explicitOne.Warnings, w => w.RuleId == "c_src");

        // The S_k basis still defaults to 1.
        var level = Run(Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisSk), Q("S_k", 10, "MVA"));
        Assert.Equal(400.0 * 400 / 10e6, level.OutputSi("Z_src"), 9);

        // A linked I_k: c other than 1.1 warns, 1.1 and the blank default do not.
        var link = CalcProvenance.Link(Guid.NewGuid(), "I_k3", "1");
        var linked = new[] { Choice("source", MotorStartCalculator.SourceFault), Choice("fault_basis", MotorStartCalculator.BasisIk), Q("I_k", 12, "kA") with { Provenance = link } };
        var wrong = Assert.Single(Run([.. linked, N("c_src", 1)]).Warnings, w => w.RuleId == "c_src");
        Assert.StartsWith("c_src:", wrong.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Run([.. linked, N("c_src", 1.1)]).Warnings, w => w.RuleId == "c_src");
        Assert.DoesNotContain(Run(linked).Warnings, w => w.RuleId == "c_src");
    }

    [Fact]
    public void A_Drive_Corrects_From_Its_Own_Displacement_Factor_Not_The_Motors()
    {
        var motor = Run(Choice("starter", MotorStartCalculator.StarterVfd), N("cos_phi", 0.8), N("cos_phi_vfd", 0.95), N("cos_phi2", 0.95));
        Assert.Equal(0, motor.OutputSi("kvar"));
        Assert.Contains(motor.Warnings, w => w.RuleId == "pfc" && w.Message.Contains("drive's displacement", StringComparison.Ordinal));

        var lower = Run(Choice("starter", MotorStartCalculator.StarterVfd), N("cos_phi", 0.8), N("cos_phi_vfd", 0.85), N("cos_phi2", 0.95), Q("P", 40, "kW"), N("eta", 0.92));
        var expected = 40 / 0.92 * (Math.Tan(Math.Acos(0.85)) - Math.Tan(Math.Acos(0.95)));
        Assert.Equal(expected, lower.OutputSi("kvar") / 1000, 9);
        Assert.Equal(40 / 0.92 * 1000 / (Math.Sqrt(3) * 400 * 0.85), lower.OutputSi("I_run1"), 9);
        Assert.Contains(lower.Warnings, w => w.RuleId == "harmonics" && w.Message.Contains("drive is itself a harmonic load", StringComparison.Ordinal));
        Assert.DoesNotContain(Run(Choice("starter", MotorStartCalculator.StarterVfd), CalcInputValue.Switch("harmonics", true), N("cos_phi_vfd", 0.85)).Warnings, w => w.Message.Contains("drive is itself", StringComparison.Ordinal));
    }

    [Fact]
    public void An_Entered_Rated_Current_Sets_The_Correction_Power_And_A_Mismatch_Warns()
    {
        var fromCurrent = Run(Q("I_n", 100, "A"), Q("P", 55, "kW"), N("eta", 0.93), N("cos_phi", 0.88), N("cos_phi2", 0.95));
        var pIn = Math.Sqrt(3) * 400 * 100 * 0.88;
        Assert.Equal(pIn, fromCurrent.OutputSi("P_in"), 6);
        Assert.Equal(pIn / 1000 * (Math.Tan(Math.Acos(0.88)) - Math.Tan(Math.Acos(0.95))), fromCurrent.OutputSi("kvar") / 1000, 9);
        Assert.Equal(100, fromCurrent.OutputSi("I_run1"), 9);
        Assert.DoesNotContain(fromCurrent.Warnings, w => w.RuleId == "rated");

        var mismatch = Run(Q("I_n", 160, "A"));
        var warning = Assert.Single(mismatch.Warnings, w => w.RuleId == "rated");
        Assert.StartsWith("I_n:", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "rated");
    }

    [Fact]
    public void An_Upstream_Network_Adds_In_Series_To_The_Transformer_And_Blank_Means_An_Infinite_Bus()
    {
        var bare = Run(Choice("source", MotorStartCalculator.SourceTransformer), Q("S_n", 315, "kVA"), Q("u_k", 4, "%"), N("XR", 4.2));
        var with = Run(Choice("source", MotorStartCalculator.SourceTransformer), Q("S_n", 315, "kVA"), Q("u_k", 4, "%"), N("XR", 4.2), Q("S_kQ", 20, "MVA"), N("XR_Q", 10));
        var zt = 0.04 * 400 * 400 / 315e3;
        var rt = zt / Math.Sqrt(1 + 4.2 * 4.2);
        var zq = 400.0 * 400 / 20e6;
        var rq = zq / Math.Sqrt(1 + 100);
        var sum = new System.Numerics.Complex(rt, 4.2 * rt) + new System.Numerics.Complex(rq, 10 * rq);

        Assert.Equal(zt, bare.OutputSi("Z_src"), 12);
        Assert.Equal(sum.Magnitude, with.OutputSi("Z_src"), 12);
        Assert.True(with.OutputSi("start_dip") > bare.OutputSi("start_dip"));
        Assert.True(with.OutputSi("h_res") < bare.OutputSi("h_res"));
        Assert.Equal(400.0 * 400 / sum.Magnitude, with.OutputSi("S_sc"), 3);
    }
}
