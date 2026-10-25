using System.Numerics;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Electrical;

/// <summary>
/// E9 — motor starting voltage dip and power-factor correction. The source impedance (from a fault level or a
/// transformer), the cable and the motor's locked-rotor impedance form a three-element series circuit; the dip at the
/// motor terminals and at the point of common coupling comes from the complex potential divider (phasors, shown on the
/// sheet). The capacitor kvar to lift the running power factor to a target is Q_c = P·(tan φ₁ − tan φ₂), with the
/// parallel-resonance order √(S_k/Q_c) and a warning when harmonic loads are present. No cable table ships: per-km R and
/// X are user or record values, or R follows from resistivity and area as in E3.
/// </summary>
public sealed class MotorStartCalculator : ICalcCalculator
{
    public const string Id = "elec.motor-start";

    public const string SourceFault = "Fault level at the supply";
    public const string SourceTransformer = "Transformer";

    public const string BasisSk = "Fault level S_k (MVA)";
    public const string BasisIk = "Fault current I_k (kA)";

    public const string StarterDol = "Direct on line (DOL)";
    public const string StarterStarDelta = "Star-delta";
    public const string StarterSoft = "Soft starter (current limit)";
    public const string StarterVfd = "Variable-frequency drive (current limit)";

    public const string DataResistivity = "resistivity and area";
    public const string DataPerKm = "R and X per km";

    public CalcTemplate Template { get; } = new(
        Id, "1.1.0", CalcDiscipline.Electrical, "Motor start voltage dip and PF correction",
        "The voltage dip when a three-phase induction motor starts, at the motor terminals and at the point of common coupling, from the supply impedance (a fault level or a transformer), the cable and the motor's locked-rotor impedance for a direct-on-line or star-delta start (a passive motor impedance) or a soft-start or drive start (a regulated current), by the complex potential divider; the dip is checked against a user limit (default 10 %). Also the capacitor kvar to raise the running power factor to a target, the capacitor current and the parallel-resonance order, with a warning when harmonic loads are present. Steady-state phasor model: no motor run-up dynamics, no flicker assessment, no starting-torque adequacy check.",
        "Rated current: I_n = P/(√3·U·cos φ·η), or entered\n" +
        "Starting current at rated voltage: DOL I_LR·I_n; star-delta I_LR·I_n/3; soft starter min(k_soft·I_n, I_LR·I_n); drive k_vfd·I_n\n" +
        "Motor starting impedance (DOL, star-delta, and a soft starter not in current limit): |Z_m| = (U/√3)/I_start at the starting power factor, Z_m = |Z_m|·(cos φ_st + j sin φ_st)\n" +
        "Regulated current (soft starter in current limit, drive): |I| = I_start held, lagging V_m by φ; with R + jX = Z_s + Z_c, a = |I|(R cos φ + X sin φ), b = |I|(X cos φ − R sin φ): |V_m| = √(E² − b²) − a, V_pcc = V_m + I·Z_c, and Z_m = V_m/I is the equivalent impedance\n" +
        "Source impedance: transformer |Z_T| = u_k·U²/S_n, plus an optional upstream network |Z_Q| = U²/S_kQ in series; fault level |Z_s| = c·U²/S_k or c·U/(√3·I_k); each element Z = |Z|·(1 + j·X/R)/√(1 + (X/R)²)\n" +
        "Cable: R = ρ_T·L/(A·n) with ρ_T = ρ_20·[1 + α(T_c − 20)], or R′·L/n; X = X′·L/n (as E3)\n" +
        "Phasor divider for a passive motor (phase voltage E = U/√3 at 0°): I = E/(Z_s + Z_c + Z_m); V_pcc = E·(Z_c + Z_m)/(Z_s + Z_c + Z_m); V_m = E·Z_m/(Z_s + Z_c + Z_m)\n" +
        "Dip: 100·(1 − |V|/E) at the motor terminals and at the point of common coupling; checked against the limits\n" +
        "Cable drop along V_m: dV_cab = √3·Re[I·Z_c·V̄_m/|V_m|] = √3·I·(R cos φ_st + X sin φ_st), the E3 three-phase form at the dipped current\n" +
        "Power-factor correction: P_in = P/η (or √3·U·I_n·cos φ when I_n is entered), Q_c = P_in·(tan φ₁ − tan φ₂) with φ₁ the motor's (a drive's displacement factor with a drive), I_c = Q_c/(√3·U), I after = P_in/(√3·U·cos φ₂)\n" +
        "Resonance: S_sc = U²/|Z_s|, parallel resonance order h ≈ √(S_sc/Q_c) (|Z_s| used for X_s, so h reads 1 to 1.4 % low)",
        [
            new CalcInputSpec("U", "Supply and motor rated voltage (line-to-line)", CalcInputKind.Quantity, "V", Default: 400, Min: 50, Max: 1000, HardMin: 50, HardMax: 1000, Group: "Supply"),
            new CalcInputSpec("source", "Source impedance from", CalcInputKind.Choice, Choices: [SourceFault, SourceTransformer], DefaultChoice: SourceTransformer, Group: "Supply"),
            new CalcInputSpec("fault_basis", "Fault level given as", CalcInputKind.Choice, Choices: [BasisSk, BasisIk], DefaultChoice: BasisSk, Group: "Supply"),
            new CalcInputSpec("S_k", "Three-phase fault level at the point of supply (use the minimum fault level, which gives the deepest dip)", CalcInputKind.Quantity, "MVA", Default: 10, Min: 0.5, Max: 500, HardMin: 0.01, HardMax: 10000, Group: "Supply"),
            new CalcInputSpec("I_k", "Three-phase fault current at the point of supply (links E5 I_k3)", CalcInputKind.Quantity, "kA", Default: 15, Min: 1, Max: 150, HardMin: 0.05, HardMax: 1000, Group: "Supply"),
            new CalcInputSpec("c_src", "Voltage factor c for the fault-level route (blank: 1.1 for the I_k basis, as E5 I_k3 is computed with; 1.0 for a declared S_k)", CalcInputKind.Number, Min: 0.95, Max: 1.1, HardMin: 0.95, HardMax: 1.1, IsOptional: true, Group: "Supply"),
            new CalcInputSpec("S_n", "Transformer rating", CalcInputKind.Quantity, "kVA", Default: 500, Min: 25, Max: 5000, HardMin: 1, HardMax: 10000, Group: "Supply"),
            new CalcInputSpec("u_k", "Transformer impedance voltage", CalcInputKind.Quantity, "%", Default: 4, Min: 1, Max: 12, HardMin: 0.5, HardMax: 25, Group: "Supply"),
            new CalcInputSpec("S_kQ", "Upstream network fault level at the transformer primary, in series with the transformer (minimum fault level; blank: infinite bus)", CalcInputKind.Quantity, "MVA", Min: 1, Max: 5000, HardMin: 0.1, HardMax: 100000, IsOptional: true, Group: "Supply"),
            new CalcInputSpec("XR_Q", "Upstream network X/R ratio", CalcInputKind.Number, Default: 10, Min: 1, Max: 50, HardMin: 0.1, HardMax: 1000, Group: "Supply"),
            new CalcInputSpec("XR", "Source X/R ratio (of the transformer, or of the fault level)", CalcInputKind.Number, Default: 4.2, Min: 0.5, Max: 40, HardMin: 0, HardMax: 100, Group: "Supply"),
            new CalcInputSpec("P", "Motor rated output (shaft) power, from the nameplate (E1 P_cont is only a lower bound)", CalcInputKind.Quantity, "kW", Default: 55, Min: 0.1, Max: 2000, HardMin: 0.01, HardMax: 10000, Group: "Motor"),
            new CalcInputSpec("eta", "Motor efficiency at rated load", CalcInputKind.Number, Default: 0.93, Min: 0.5, Max: 0.99, HardMin: 0.3, HardMax: 1, Group: "Motor"),
            new CalcInputSpec("cos_phi", "Motor power factor at rated load", CalcInputKind.Number, Default: 0.88, Min: 0.5, Max: 1, HardMin: 0.1, HardMax: 1, Group: "Motor"),
            new CalcInputSpec("I_n", "Motor rated current (when given it replaces the value from P, η and cos φ, for the starting current and the correction power)", CalcInputKind.Quantity, "A", Min: 0.1, Max: 5000, HardMin: 0.01, HardMax: 100000, IsOptional: true, Group: "Motor"),
            new CalcInputSpec("I_LR_r", "Locked-rotor current ratio I_LR/I_n", CalcInputKind.Number, Default: 7, Min: 3, Max: 10, HardMin: 1, HardMax: 15, Group: "Motor"),
            new CalcInputSpec("cos_phi_st", "Power factor at locked rotor (starting)", CalcInputKind.Number, Default: 0.3, Min: 0.1, Max: 0.8, HardMin: 0.05, HardMax: 1, Group: "Motor"),
            new CalcInputSpec("starter", "Starter type", CalcInputKind.Choice, Choices: [StarterDol, StarterStarDelta, StarterSoft, StarterVfd], DefaultChoice: StarterDol, Group: "Starter"),
            new CalcInputSpec("k_soft", "Soft-starter current limit as a multiple of the rated current", CalcInputKind.Number, Default: 3.5, Min: 1, Max: 6, HardMin: 0.5, HardMax: 15, Group: "Starter"),
            new CalcInputSpec("k_vfd", "Drive line-current limit as a multiple of the rated current", CalcInputKind.Number, Default: 1.5, Min: 0.5, Max: 2.5, HardMin: 0.1, HardMax: 5, Group: "Starter"),
            new CalcInputSpec("cos_phi_vfd", "Drive line displacement power factor while starting", CalcInputKind.Number, Default: 0.95, Min: 0.5, Max: 1, HardMin: 0.1, HardMax: 1, Group: "Starter"),
            new CalcInputSpec("cable_data", "Cable resistance from", CalcInputKind.Choice, Choices: [DataResistivity, DataPerKm], DefaultChoice: DataResistivity, Group: "Cable"),
            new CalcInputSpec("L_cab", "Cable length, source to motor (links E4 L)", CalcInputKind.Quantity, "m", Default: 25, Min: 0, Max: 1000, HardMin: 0, HardMax: 10000, Group: "Cable"),
            new CalcInputSpec("n_par", "Parallel conductors per phase", CalcInputKind.Number, Default: 1, Min: 1, Max: 6, HardMin: 1, HardMax: 20, Group: "Cable"),
            new CalcInputSpec("A_c", "Conductor cross-section (links E4 CSA)", CalcInputKind.Quantity, "mm^2", Default: 35, Min: 1, Max: 1000, HardMin: 0.01, HardMax: 100000, Group: "Cable"),
            new CalcInputSpec("rho_20", "Resistivity at 20 °C (copper 0.01724, aluminium 0.0282 Ω·mm²/m)", CalcInputKind.Quantity, "ohm*mm^2/m", Default: 0.01724, Min: 0.01, Max: 0.05, HardMin: 1e-4, HardMax: 10, Group: "Cable"),
            new CalcInputSpec("alpha", "Temperature coefficient of resistance", CalcInputKind.Quantity, "1/K", Default: 0.00393, Min: 0.003, Max: 0.0045, HardMin: 0, HardMax: 0.02, Group: "Cable"),
            new CalcInputSpec("T_c", "Conductor temperature for the resistance", CalcInputKind.Quantity, "degC", Default: 70, Min: 20, Max: 90, HardMin: -50, HardMax: 300, Group: "Cable"),
            new CalcInputSpec("R_km", "Resistance per km per conductor (cable datasheet or your own table)", CalcInputKind.Quantity, "ohm/km", Min: 0.01, Max: 50, HardMin: 1e-6, HardMax: 1e4, IsOptional: true, Group: "Cable"),
            new CalcInputSpec("X_km", "Reactance per km per conductor", CalcInputKind.Quantity, "ohm/km", Default: 0.08, Min: 0, Max: 1, HardMin: 0, HardMax: 100, Group: "Cable"),
            new CalcInputSpec("dip_limit", "Dip limit at the motor terminals", CalcInputKind.Quantity, "%", Default: 10, Min: 1, Max: 30, HardMin: 0.1, HardMax: 100, Group: "Limits"),
            new CalcInputSpec("dip_limit_pcc", "Dip limit at the point of common coupling (when the network operator sets one)", CalcInputKind.Quantity, "%", Min: 0.5, Max: 20, HardMin: 0.1, HardMax: 100, IsOptional: true, Group: "Limits"),
            new CalcInputSpec("cos_phi2", "Target power factor after correction", CalcInputKind.Number, Default: 0.95, Min: 0.8, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Power-factor correction"),
            new CalcInputSpec("harmonics", "Harmonic loads present on the supply", CalcInputKind.Boolean, Default: 0, Group: "Power-factor correction"),
        ],
        [
            new CalcAssumption("The supply is a stiff source behind the stated impedance (voltage E = U/√3 before the start). A direct-on-line or star-delta start, and a soft starter not in current limit, draw current through a passive winding: a constant impedance equal to the locked-rotor impedance at rated voltage and the starting power factor, so the current falls with the dipped voltage. A soft starter in current limit and a drive regulate the current: they hold |I| = I_start whatever the voltage, so the dip is deeper than the passive model gives; the current phasor lags the voltage at the starter or drive input by the stated power factor.", "Standard steady-state starting-dip screening model; the regulated case is solved in closed form."),
            new CalcAssumption("The dip is the steady-state phasor value at the instant of locked rotor; the current falls as the motor accelerates, so the dip is the worst case. No other load is running on the supply and no run-up dynamics, flicker (Pst) or tap-changer action is assessed.", "Stated limitation."),
            new CalcAssumption("A star-delta start draws one third of the direct-on-line current at rated voltage (and one third of the starting torque, which is not checked here); the current spike at the star-to-delta changeover is not modelled.", "Star-delta connection ratio."),
            new CalcAssumption("The cable resistance follows E3: from the resistivity, area and temperature, or from R′ per kilometre; X′ per kilometre is a user or record value. No cable table is embedded.", "User or record values."),
            new CalcAssumption("The capacitor kvar corrects the running power factor of the motor input power P/η at rated load; the capacitor sits at the point of common coupling, and the resonance order uses the short-circuit apparent power U²/|Z_s| there (|Z_s| stands in for X_s, so h reads 1 to 1.4 % low). Capacitor switching, detuning and self-excitation on disconnection are not assessed.", "Standard power-factor-correction relation."),
            new CalcAssumption("Source: E5 'Z_s' and 'I_f' are earth-fault-loop quantities (line to earth through the protective conductor), not the positive-sequence source impedance a motor start sees, so they are not linked; the fault level links E5 I_k3. E5 computes I_k3 with c = 1.1 and U_n = √3·U_0 (398.4 V at 230 V), so the I_k basis defaults c to 1.1 and U should be √3·U_0 for an exact match with E5's Z_T. E6 declares no impedance port, so the transformer rating and impedance are entered.", "Linking decision."),
            new CalcAssumption("The source impedance should be taken at the minimum fault level (the deepest dip); a transformer alone is an infinite bus behind it, which understates the dip by a few per cent and overstates the resonance order unless the upstream network fault level S_kQ is entered.", "Stated limitation."),
            new CalcAssumption("With a drive, the power-factor correction starts from the drive's displacement factor, not the motor's, the drive is itself a harmonic load, and the 'torque factor' and the voltage at the starter or drive input (not the motor terminals) are reported only for what they are.", "Regulated-current starters."),
        ],
        [
            new CalcClauseRef("IEC 60909-0", "cl. 5.3", "Voltage factor c and the equivalent voltage source"),
            new CalcClauseRef("IEC 61000-3-11", "scope", "Limitation of voltage changes and flicker in public low-voltage systems (limits are user inputs)"),
            new CalcClauseRef("Hughes, Electrical and Electronic Technology", "Ch. on AC circuits", "Complex impedance and the potential divider"),
            new CalcClauseRef("Hughes, Electrical and Electronic Technology", "Ch. on power factor", "Power-factor correction: Q = P (tan φ₁ − tan φ₂)"),
        ],
        [
            new CalcApplicabilityRule("cable-data", "The chosen cable-data route needs values that were not given.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("pfc", "The target power factor is not above the motor's own, so no capacitor is needed.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("pfc-starter", "Power-factor capacitors must not be connected on the output of a drive, or across a soft starter during the start.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("harmonics", "Harmonic loads are present (or a drive is used): the capacitor bank forms a parallel resonance with the supply at the stated order.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("current-limit", "The supply cannot hold the regulated starting current: the source and cable drop alone exceeds the supply voltage.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("rated", "The entered rated current and P/η disagree by more than 5 % in input power.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("c_src", "A linked fault current needs c = 1.1, the factor E5 computes it with.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-pump-dol-transformer", "75 kW pump motor, direct on line, on a 500 kVA 4 % transformer, 40 m of 50 mm² copper (analytic)",
                new CalcReferenceSource("Analytic (complex divider, recomputed with Python complex arithmetic)", null, "I_n = 75000/(√3·400·0.87·0.945) = 131.671 A; I_start = 7·I_n = 921.696 A; |Z_m| = 230.940/921.696 = 0.250560 Ω → 0.075168 + j0.239019; |Z_s| = 0.04·400²/500 kVA = 0.0128 Ω at X/R 4.2 → 0.0029647 + j0.0124519; cable ρ_70 = 0.01724·(1 + 0.00393·50) = 0.020628 → R = 0.0165021 Ω, X = 0.0032 Ω; |V_m| = 212.9 V per phase → 368.897 V line, dip 7.7757 %; V_pcc dip 4.6746 %; |I| = 850.027 A; dV_cab = 11.7831 V"),
                [CalcInputValue.Quantity("U", 400, "V"), CalcInputValue.Choice("source", SourceTransformer), CalcInputValue.Quantity("S_n", 500, "kVA"), CalcInputValue.Quantity("u_k", 4, "%"), CalcInputValue.Of("XR", 4.2), CalcInputValue.Quantity("P", 75, "kW"), CalcInputValue.Of("eta", 0.945), CalcInputValue.Of("cos_phi", 0.87), CalcInputValue.Of("I_LR_r", 7), CalcInputValue.Of("cos_phi_st", 0.3), CalcInputValue.Choice("starter", StarterDol), CalcInputValue.Choice("cable_data", DataResistivity), CalcInputValue.Quantity("L_cab", 40, "m"), CalcInputValue.Quantity("A_c", 50, "mm^2"), CalcInputValue.Quantity("T_c", 70, "degC"), CalcInputValue.Quantity("X_km", 0.08, "ohm/km"), CalcInputValue.Quantity("dip_limit", 10, "%"), CalcInputValue.Of("cos_phi2", 0.95)],
                [new CalcExpectedValue("I_rated", 131.6708331, "A", 1e-9), new CalcExpectedValue("I_start", 921.6958320, "A", 1e-9), new CalcExpectedValue("Z_src", 0.0128, "ohm", 1e-9), new CalcExpectedValue("Z_m", 0.2505600, "ohm", 1e-7), new CalcExpectedValue("I_dip", 850.0271945, "A", 1e-9), new CalcExpectedValue("V_m", 368.8970548, "V", 1e-9), new CalcExpectedValue("start_dip", 7.775736312, "%", 1e-9), new CalcExpectedValue("dip_pcc", 4.674562050, "%", 1e-9), new CalcExpectedValue("dV_cab", 11.78309819, "V", 1e-9), new CalcExpectedValue("kvar", 18.89222313, "kvar", 1e-9), new CalcExpectedValue("I_cap", 27.26857527, "A", 1e-9), new CalcExpectedValue("h_res", 25.72251762, "", 1e-9)]),
            new CalcTestCase("ka-hand-phasor", "Hand phasor case: Z_s = Z_c = 0.03 + j0.04 Ω, Z_m = 0.24 + j0.32 Ω, all at 53.13° (analytic)",
                new CalcReferenceSource("Analytic (complex divider; all three impedances share one angle)", null, "S_k = 3.2 MVA at 400 V gives |Z_s| = 0.05 Ω, X/R = 4/3 gives 0.03 + j0.04; cable 100 m of 0.3 + j0.4 Ω/km gives the same; I_n = 100 A and I_LR/I_n = 10/√3 give I_start = 577.35 A, so |Z_m| = (400/√3)/577.35 = 0.4 Ω at cos φ = 0.6 (0.24 + j0.32). Z_t = 0.3 + j0.4 = 0.5∠53.13°, so V_m = 0.8·E (dip 20 %), V_pcc = 0.9·E (dip 10 %), |I| = 230.94/0.5 = 461.88 A; I·Z_c lies along V_m, so dV_cab = √3·461.88·0.05 = 40 V"),
                [CalcInputValue.Quantity("U", 400, "V"), CalcInputValue.Choice("source", SourceFault), CalcInputValue.Choice("fault_basis", BasisSk), CalcInputValue.Quantity("S_k", 3.2, "MVA"), CalcInputValue.Of("c_src", 1), CalcInputValue.Of("XR", 4.0 / 3.0), CalcInputValue.Quantity("I_n", 100, "A"), CalcInputValue.Of("I_LR_r", 10 / Math.Sqrt(3)), CalcInputValue.Of("cos_phi_st", 0.6), CalcInputValue.Choice("starter", StarterDol), CalcInputValue.Choice("cable_data", DataPerKm), CalcInputValue.Quantity("L_cab", 100, "m"), CalcInputValue.Quantity("R_km", 0.3, "ohm/km"), CalcInputValue.Quantity("X_km", 0.4, "ohm/km"), CalcInputValue.Quantity("dip_limit", 10, "%")],
                [new CalcExpectedValue("Z_src", 0.05, "ohm", 1e-9), new CalcExpectedValue("Z_m", 0.4, "ohm", 1e-9), new CalcExpectedValue("I_start", 577.3502692, "A", 1e-9), new CalcExpectedValue("I_dip", 461.8802154, "A", 1e-9), new CalcExpectedValue("V_m", 320, "V", 1e-9), new CalcExpectedValue("V_pcc", 360, "V", 1e-9), new CalcExpectedValue("start_dip", 20, "%", 1e-9), new CalcExpectedValue("dip_pcc", 10, "%", 1e-9), new CalcExpectedValue("dV_cab", 40, "V", 1e-9), new CalcExpectedValue("u_dip", 2, "", 1e-9)]),
            new CalcTestCase("ka-star-delta-fault-current", "30 kW star-delta start from a 12 kA fault current (c = 1.1), 60 m of 25 mm² (analytic)",
                new CalcReferenceSource("Analytic (complex divider, recomputed with Python complex arithmetic)", null, "I_n = 55.3725 A, I_start = 7.5·I_n/3 = 138.431 A at cos φ_st 0.35; |Z_s| = 1.1·400/(√3·12 kA) = 0.0211695 Ω at X/R 3; cable R = 0.0495064 Ω, X = 0.0048 Ω; dip at the motor 2.5437 %, at the point of common coupling 1.2343 %; Q_c to 0.96 = 10.698 kvar"),
                [CalcInputValue.Quantity("U", 400, "V"), CalcInputValue.Choice("source", SourceFault), CalcInputValue.Choice("fault_basis", BasisIk), CalcInputValue.Quantity("I_k", 12, "kA"), CalcInputValue.Of("c_src", 1.1), CalcInputValue.Of("XR", 3), CalcInputValue.Quantity("P", 30, "kW"), CalcInputValue.Of("eta", 0.92), CalcInputValue.Of("cos_phi", 0.85), CalcInputValue.Of("I_LR_r", 7.5), CalcInputValue.Of("cos_phi_st", 0.35), CalcInputValue.Choice("starter", StarterStarDelta), CalcInputValue.Choice("cable_data", DataResistivity), CalcInputValue.Quantity("L_cab", 60, "m"), CalcInputValue.Quantity("A_c", 25, "mm^2"), CalcInputValue.Quantity("T_c", 70, "degC"), CalcInputValue.Quantity("X_km", 0.08, "ohm/km"), CalcInputValue.Of("cos_phi2", 0.96)],
                [new CalcExpectedValue("I_rated", 55.37246827, "A", 1e-9), new CalcExpectedValue("I_start", 138.4311707, "A", 1e-9), new CalcExpectedValue("Z_src", 0.02116950987, "ohm", 1e-9), new CalcExpectedValue("Z_m", 1.668266667, "ohm", 1e-7), new CalcExpectedValue("start_dip", 2.543711907, "%", 1e-9), new CalcExpectedValue("dip_pcc", 1.234307435, "%", 1e-9), new CalcExpectedValue("kvar", 10.69818495, "kvar", 1e-9)]),
            new CalcTestCase("ka-soft-start-current-limit", "132 kW soft start holding 3 × I_n on an 8 MVA supply, 80 m of 95 mm² (analytic, current source)",
                new CalcReferenceSource("Analytic (regulated current: E = V_m + I·(Z_s + Z_c), solved by fixed-point iteration of the phasors in Python and checked against the closed form)", null, "I_n = 224.161 A; 3·I_n = 672.483 A is below I_LR = 6.5·I_n so the current is held at 672.483 A at cos φ 0.3; |Z_s| = 400²/8 MVA = 0.02 Ω at X/R 6; cable R = 0.0173707 Ω, X = 0.0064 Ω; |V_m| = 363.511 V line, dip 9.1223 % at the starter input and 5.7304 % at the point of common coupling (the passive-impedance model would give 8.3555 % and 5.2548 %); Z_m,eq = 0.312087 Ω; Q_c = 25.381 kvar"),
                [CalcInputValue.Quantity("U", 400, "V"), CalcInputValue.Choice("source", SourceFault), CalcInputValue.Choice("fault_basis", BasisSk), CalcInputValue.Quantity("S_k", 8, "MVA"), CalcInputValue.Of("c_src", 1), CalcInputValue.Of("XR", 6), CalcInputValue.Quantity("P", 132, "kW"), CalcInputValue.Of("eta", 0.955), CalcInputValue.Of("cos_phi", 0.89), CalcInputValue.Of("I_LR_r", 6.5), CalcInputValue.Of("cos_phi_st", 0.3), CalcInputValue.Choice("starter", StarterSoft), CalcInputValue.Of("k_soft", 3), CalcInputValue.Choice("cable_data", DataResistivity), CalcInputValue.Quantity("L_cab", 80, "m"), CalcInputValue.Quantity("A_c", 95, "mm^2"), CalcInputValue.Quantity("T_c", 70, "degC"), CalcInputValue.Quantity("X_km", 0.08, "ohm/km"), CalcInputValue.Of("cos_phi2", 0.95)],
                [new CalcExpectedValue("I_rated", 224.1609375, "A", 1e-9), new CalcExpectedValue("I_start", 672.4828125, "A", 1e-9), new CalcExpectedValue("I_dip", 672.4828125, "A", 1e-9), new CalcExpectedValue("Z_m", 0.3120869831, "ohm", 1e-7), new CalcExpectedValue("V_m", 363.510928, "V", 1e-8), new CalcExpectedValue("start_dip", 9.122267996, "%", 1e-8), new CalcExpectedValue("dip_pcc", 5.730420833, "%", 1e-8), new CalcExpectedValue("dV_cab", 13.18106372, "V", 1e-8), new CalcExpectedValue("kvar", 25.38147017, "kvar", 1e-9)]),
            new CalcTestCase("ka-vfd-current-limit", "37 kW drive holding 1.5 × I_n at 0.92 displacement power factor on a 5 MVA supply, 30 m of 16 mm² (analytic, current source)",
                new CalcReferenceSource("Analytic (regulated current: E = V_m + I·(Z_s + Z_c), solved by fixed-point iteration of the phasors in Python and checked against the closed form)", null, "I_n = 66.0625 A; the line current is held at 1.5·I_n = 99.0937 A at cos φ 0.92; |Z_s| = 400²/5 MVA = 0.032 Ω at X/R 3; cable R = 0.0386769 Ω, X = 0.0024 Ω; |V_m| = 390.087 V line, dip 2.4783 % at the drive input and 0.90955 % at the point of common coupling; Z_m,eq = 2.27277 Ω; Q_c from the drive's 0.92 to 0.95 on 37/0.94 kW = 3.83045 kvar"),
                [CalcInputValue.Quantity("U", 400, "V"), CalcInputValue.Choice("source", SourceFault), CalcInputValue.Choice("fault_basis", BasisSk), CalcInputValue.Quantity("S_k", 5, "MVA"), CalcInputValue.Of("c_src", 1), CalcInputValue.Of("XR", 3), CalcInputValue.Quantity("P", 37, "kW"), CalcInputValue.Of("eta", 0.94), CalcInputValue.Of("cos_phi", 0.86), CalcInputValue.Choice("starter", StarterVfd), CalcInputValue.Of("k_vfd", 1.5), CalcInputValue.Of("cos_phi_vfd", 0.92), CalcInputValue.Choice("cable_data", DataResistivity), CalcInputValue.Quantity("L_cab", 30, "m"), CalcInputValue.Quantity("A_c", 16, "mm^2"), CalcInputValue.Quantity("T_c", 70, "degC"), CalcInputValue.Quantity("X_km", 0.08, "ohm/km")],
                [new CalcExpectedValue("I_rated", 66.06246895, "A", 1e-9), new CalcExpectedValue("I_start", 99.09370343, "A", 1e-9), new CalcExpectedValue("I_dip", 99.09370343, "A", 1e-9), new CalcExpectedValue("Z_m", 2.272765633, "ohm", 1e-7), new CalcExpectedValue("V_m", 390.0868772, "V", 1e-8), new CalcExpectedValue("start_dip", 2.478280697, "%", 1e-8), new CalcExpectedValue("dip_pcc", 0.9095497237, "%", 1e-8), new CalcExpectedValue("dV_cab", 6.268691627, "V", 1e-8), new CalcExpectedValue("kvar", 3.830449048, "kvar", 1e-8), new CalcExpectedValue("h_res", 36.12935075, "", 1e-8)]),
            new CalcTestCase("ka-transformer-upstream-network", "55 kW pump direct on line on a 315 kVA 4 % transformer behind a 20 MVA upstream network, 30 m of 35 mm² (analytic)",
                new CalcReferenceSource("Analytic (complex divider, recomputed with Python complex arithmetic)", null, "|Z_T| = 0.04·400²/315 kVA = 0.0203175 Ω at X/R 4.2; |Z_Q| = 400²/20 MVA = 0.008 Ω at X/R 10; the series sum has |Z_s| = 0.0282659 Ω; I_n = 97.0011 A, I_start = 679.007 A; dip 9.5523 % at the motor and 7.4333 % at the point of common coupling; S_sc = 5.66053 MVA, h = 21.295"),
                [CalcInputValue.Quantity("U", 400, "V"), CalcInputValue.Choice("source", SourceTransformer), CalcInputValue.Quantity("S_n", 315, "kVA"), CalcInputValue.Quantity("u_k", 4, "%"), CalcInputValue.Of("XR", 4.2), CalcInputValue.Quantity("S_kQ", 20, "MVA"), CalcInputValue.Of("XR_Q", 10), CalcInputValue.Quantity("P", 55, "kW"), CalcInputValue.Of("eta", 0.93), CalcInputValue.Of("cos_phi", 0.88), CalcInputValue.Of("I_LR_r", 7), CalcInputValue.Of("cos_phi_st", 0.3), CalcInputValue.Choice("starter", StarterDol), CalcInputValue.Choice("cable_data", DataResistivity), CalcInputValue.Quantity("L_cab", 30, "m"), CalcInputValue.Quantity("A_c", 35, "mm^2"), CalcInputValue.Quantity("T_c", 70, "degC"), CalcInputValue.Quantity("X_km", 0.08, "ohm/km"), CalcInputValue.Of("cos_phi2", 0.95)],
                [new CalcExpectedValue("I_rated", 97.00105329, "A", 1e-9), new CalcExpectedValue("I_start", 679.007373, "A", 1e-8), new CalcExpectedValue("Z_src", 0.02826590036, "ohm", 1e-8), new CalcExpectedValue("I_dip", 614.146462, "A", 1e-8), new CalcExpectedValue("start_dip", 9.552313216, "%", 1e-8), new CalcExpectedValue("dip_pcc", 7.433250216, "%", 1e-8), new CalcExpectedValue("S_sc", 5.660530815, "MVA", 1e-8), new CalcExpectedValue("h_res", 21.29546179, "", 1e-8)]),
            new CalcTestCase("ka-pfc-resonance", "22 kW motor, 0.83 to 0.95, on a 6 MVA bus: capacitor kvar, current and resonance order (analytic)",
                new CalcReferenceSource("Analytic (Q_c = P_in (tan φ₁ − tan φ₂))", null, "P_in = 22/0.91 = 24.17582 kW; tan φ₁ = 0.671965, tan φ₂ = 0.328685 (cos φ = 0.83, 0.95); Q_c = 24.17582·0.343280 = 8.30004 kvar; I_c = 8300.04/(√3·400) = 11.9801 A; S_sc = 400²/|Z_s| = 6 MVA; h = √(6000/8.30004) = 26.8866"),
                [CalcInputValue.Quantity("U", 400, "V"), CalcInputValue.Choice("source", SourceFault), CalcInputValue.Choice("fault_basis", BasisSk), CalcInputValue.Quantity("S_k", 6, "MVA"), CalcInputValue.Of("c_src", 1), CalcInputValue.Of("XR", 4), CalcInputValue.Quantity("P", 22, "kW"), CalcInputValue.Of("eta", 0.91), CalcInputValue.Of("cos_phi", 0.83), CalcInputValue.Quantity("L_cab", 0, "m"), CalcInputValue.Of("cos_phi2", 0.95)],
                [new CalcExpectedValue("P_in", 24.17582418, "kW", 1e-9), new CalcExpectedValue("kvar", 8.300043472, "kvar", 1e-9), new CalcExpectedValue("I_cap", 11.98008083, "A", 1e-9), new CalcExpectedValue("S_sc", 6, "MVA", 1e-9), new CalcExpectedValue("h_res", 26.88657249, "", 1e-9), new CalcExpectedValue("start_dip", 3.281045746, "%", 1e-9), new CalcExpectedValue("dip_pcc", 3.281045746, "%", 1e-9)]),
        ],
        DiagramKey: null,
        Category: "Power",
        OutputPorts:
        [
            new CalcOutputPort("start_dip", "%", "Voltage dip at the motor terminals while starting (motor.start_dip; at the starter or drive input for a soft starter in current limit or a drive)"),
            new CalcOutputPort("kvar", "kvar", "Capacitor kvar to reach the target power factor (pfc.kvar)"),
            new CalcOutputPort("I_start", "A", "Starting current at rated voltage for the starter chosen, for the starting-current coordination check (not a design current)"),
            new CalcOutputPort("I_rated", "A", "Motor rated current, the running design current (→ E4 I_b)"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("cable.impedance", "Cable resistance and reactance per kilometre", ["R_km", "X_km"], "From the cable manufacturer's datasheet or your own licensed table for the cross-section and temperature; none ships with the calculator."),
            new CalcRecordKind("conductor.material", "Conductor material constants", ["rho_20", "alpha"], "Resistivity at 20 °C and the temperature coefficient of the conductor material, with their source."),
            new CalcRecordKind("motor.start", "Motor starting data", ["I_LR_r", "cos_phi_st"], "Locked-rotor current ratio and starting power factor from the motor datasheet."),
            new CalcRecordKind("starter.limits", "Soft-starter and drive limits", ["k_soft", "k_vfd"], "Current limits from the soft-starter or drive datasheet and its parameter setting."),
            new CalcRecordKind("transformer.data", "Transformer impedance data", ["u_k", "XR"], "Impedance voltage and X/R from the transformer's rating plate or test certificate."),
        ]);

    public void Run(CalcRunContext c)
    {
        foreach (var s in new[] { "U", "P", "eta", "cos_phi", "dip_limit", "cos_phi2" })
            c.Input(s);

        var starter = c.InputText("starter");
        var source = c.InputText("source");
        var data = c.InputText("cable_data");

        // A soft starter in current limit and a drive hold the current; every other start draws it through a passive winding.
        if (starter == StarterSoft)
        {
            c.Input("k_soft");
            c.Input("I_LR_r");
        }

        var regulated = starter == StarterVfd || (starter == StarterSoft && c.Si("k_soft") < c.Si("I_LR_r"));
        var kSymbol = starter == StarterVfd ? "k_vfd" : "k_soft";

        // Rated current: entered, or from the rated output.
        if (c.Has("I_n"))
        {
            c.Input("I_n");
            c.Assign("I_rated", "I_n", "A", "Motor rated current (entered)");
        }
        else
        {
            c.Assign("I_rated", "P / (sqrt(3) * U * cos_phi * eta)", "A", "Motor rated current from the rated output");
        }

        // Starting current at rated voltage, and the power factor it flows at.
        var cosSt = "cos_phi_st";

        switch (starter)
        {
            case StarterStarDelta:
                c.Input("I_LR_r"); c.Input("cos_phi_st");
                c.Assign("I_start", "I_LR_r * I_rated / 3", "A", "Starting current at rated voltage (star connection: one third of direct on line)");
                break;
            case StarterSoft:
                c.Input("cos_phi_st");
                c.Assign("I_start", "min(k_soft * I_rated, I_LR_r * I_rated)", "A", "Starting current at rated voltage (soft-starter limit, never above locked rotor)");
                break;
            case StarterVfd:
                c.Input("k_vfd"); c.Input("cos_phi_vfd");
                c.Assign("I_start", "k_vfd * I_rated", "A", "Line current while starting (drive current limit, held)");
                cosSt = "cos_phi_vfd";
                break;
            default:
                c.Input("I_LR_r"); c.Input("cos_phi_st");
                c.Assign("I_start", "I_LR_r * I_rated", "A", "Starting current at rated voltage (direct on line, locked rotor)");
                break;
        }

        c.Assign("S_start", "sqrt(3) * U * I_start", "kVA", "Starting apparent power");

        if (!regulated)
        {
            c.Assign("Z_m", "U / (sqrt(3) * I_start)", "ohm", "Motor starting impedance magnitude");
            c.Assign("R_m", $"Z_m * {cosSt}", "ohm", "Motor starting resistance");
            c.Assign("X_m", $"Z_m * sqrt(1 - {cosSt}^2)", "ohm", "Motor starting reactance");
        }

        // Source impedance.
        c.Input("XR");

        if (source == SourceTransformer)
        {
            c.Input("S_n"); c.Input("u_k");

            if (!c.Has("S_kQ"))
            {
                c.Assign("Z_T", "u_k * U^2 / S_n", "ohm", "Source impedance magnitude from the transformer");
                c.Assign("R_src", "Z_T / sqrt(1 + XR^2)", "ohm", "Source resistance");
                c.Assign("X_src", "R_src * XR", "ohm", "Source reactance");
            }
            else
            {
                c.Input("S_kQ"); c.Input("XR_Q");
                c.Assign("Z_T", "u_k * U^2 / S_n", "ohm", "Transformer impedance magnitude");
                c.Assign("R_T", "Z_T / sqrt(1 + XR^2)", "ohm", "Transformer resistance");
                c.Assign("X_T", "R_T * XR", "ohm", "Transformer reactance");
                c.Assign("Z_Q", "U^2 / S_kQ", "ohm", "Upstream network impedance magnitude (referred to the secondary)");
                c.Assign("R_Q", "Z_Q / sqrt(1 + XR_Q^2)", "ohm", "Upstream network resistance");
                c.Assign("X_Q", "R_Q * XR_Q", "ohm", "Upstream network reactance");
                c.Assign("R_src", "R_T + R_Q", "ohm", "Source resistance (transformer plus upstream network)");
                c.Assign("X_src", "X_T + X_Q", "ohm", "Source reactance (transformer plus upstream network)");
            }
        }
        else
        {
            var basisSk = c.InputText("fault_basis") == BasisSk;
            string cName;

            if (c.Has("c_src"))
            {
                c.Input("c_src");
                cName = "c_src";
            }
            else
            {
                c.Assign("c_f", basisSk ? "1" : "1.1", null, basisSk ? "Voltage factor c (1.0 for a declared fault level)" : "Voltage factor c (1.1, as E5 computes I_k3)", "IEC 60909-0 cl. 5.3");
                cName = "c_f";
            }

            if (basisSk)
            {
                c.Input("S_k");
                c.Assign("Z_fl", $"{cName} * U^2 / S_k", "ohm", "Source impedance magnitude from the fault level", "IEC 60909-0 cl. 5.3");
            }
            else
            {
                c.Input("I_k");
                c.Assign("Z_fl", $"{cName} * U / (sqrt(3) * I_k)", "ohm", "Source impedance magnitude from the fault current", "IEC 60909-0 cl. 5.3");

                if (c.Inputs.Find("I_k")?.Provenance?.Kind == CalcProvenanceKind.Link && Math.Abs(c.Si(cName) - 1.1) > 1e-9)
                    c.Warn("c_src", $"c_src: I_k is linked from E5 I_k3, which is computed with c = 1.1, but c is {F(c.Si(cName))}; the source impedance is then {F(c.Si(cName) / 1.1 * 100)} % of E5's Z_T. Set c_src to 1.1 (and U to √3·U_0 for an exact match).");
            }

            c.Assign("R_src", "Z_fl / sqrt(1 + XR^2)", "ohm", "Source resistance");
            c.Assign("X_src", "R_src * XR", "ohm", "Source reactance");
        }

        c.Assign("Z_src", "hypot(R_src, X_src)", "ohm", "Source impedance magnitude");

        // Cable, as E3.
        c.Input("L_cab"); c.Input("n_par"); c.Input("X_km");

        if (data == DataResistivity)
        {
            c.Input("rho_20"); c.Input("alpha"); c.Input("A_c"); c.Input("T_c");
            c.Assign("rho_T", "rho_20 * (1 + alpha * (T_c - 20 degC))", "ohm*mm^2/m", "Resistivity at the operating temperature");
            c.Assign("R_cab", "rho_T * L_cab / (A_c * n_par)", "ohm", "Cable resistance (parallel paths combined)");
        }
        else
        {
            if (!c.Has("R_km"))
            {
                c.Block("cable-data", "R_km: the cable resistance per km is needed; enter it, or choose the resistivity and area route.");
                return;
            }

            c.Input("R_km");
            c.Assign("R_cab", "R_km * L_cab / n_par", "ohm", "Cable resistance");
        }

        c.Assign("X_cab", "X_km * L_cab / n_par", "ohm", "Cable reactance");

        // Phasor working.
        var root3 = Math.Sqrt(3);
        var e = c.Si("U") / root3;
        var zs = new Complex(c.Si("R_src"), c.Si("X_src"));
        var zc = new Complex(c.Si("R_cab"), c.Si("X_cab"));

        c.Record("E_ph", "E = U/√3 (reference phasor, angle 0°)", $"{F(c.Si("U"))} V / √3", ScalarValue.Of(e, "V"), "Source phase voltage before the start");
        c.Record("Z_s_ph", "Z_s = R_s + jX_s", $"R_s = {F(zs.Real)} Ω, X_s = {F(zs.Imaginary)} Ω", new TextValue(Ph(zs, "Ω")), "Source impedance phasor");
        c.Record("Z_c_ph", "Z_c = R_c + jX_c", $"R_c = {F(zc.Real)} Ω, X_c = {F(zc.Imaginary)} Ω", new TextValue(Ph(zc, "Ω")), "Cable impedance phasor");

        Complex zm, vm, current;

        if (!regulated)
        {
            zm = new Complex(c.Si("R_m"), c.Si("X_m"));
            var zt0 = zs + zc + zm;
            current = e / zt0;
            vm = e * zm / zt0;
            c.Record("Z_m_ph", "Z_m = |Z_m|·(cos φ_st + j sin φ_st)", $"{F(c.Si("Z_m"))} Ω · ({F(c.Si(cosSt))} + j{F(Math.Sqrt(Math.Max(0, 1 - c.Si(cosSt) * c.Si(cosSt))))})", new TextValue(Ph(zm, "Ω")), "Motor starting impedance phasor");
        }
        else
        {
            // The current is held: |I| lags V_m by φ. With Z_l = Z_s + Z_c = R + jX, E = V_m + I·Z_l gives |V_m| = √(E² − b²) − a.
            var held = c.Si("I_start");
            var cos = c.Si(cosSt);
            var sin = Math.Sqrt(Math.Max(0, 1 - cos * cos));
            var phi = Math.Acos(Math.Min(1, cos));
            var zl = zs + zc;
            var a = held * (zl.Real * cos + zl.Imaginary * sin);
            var b = held * (zl.Imaginary * cos - zl.Real * sin);
            var disc = e * e - b * b;
            var vmMag = disc >= 0 ? Math.Sqrt(disc) - a : -1;

            if (!(vmMag > 0))
            {
                c.Block("current-limit", $"{kSymbol}: the supply cannot hold the regulated starting current of {F(held)} A (the source and cable drop alone reaches the supply voltage); lower {kSymbol} or strengthen the supply.");
                return;
            }

            c.Record("a_reg", "a = I·(R cos φ + X sin φ), R + jX = Z_s + Z_c", $"{F(held)} A · ({F(zl.Real)} · {F(cos)} + {F(zl.Imaginary)} · {F(sin)}) Ω", ScalarValue.Of(a, "V"), "In-phase drop of the held current in the source and cable (phase)");
            c.Record("b_reg", "b = I·(X cos φ − R sin φ)", $"{F(held)} A · ({F(zl.Imaginary)} · {F(cos)} − {F(zl.Real)} · {F(sin)}) Ω", ScalarValue.Of(b, "V"), "Quadrature drop of the held current in the source and cable (phase)");
            var theta = -(vmMag + Complex.FromPolarCoordinates(held, -phi) * zl).Phase;
            vm = Complex.FromPolarCoordinates(vmMag, theta);
            current = Complex.FromPolarCoordinates(held, theta - phi);
            zm = vm / current;
            c.Record("Z_m", "|V_m|/|I|", $"{F(vmMag)} V / {F(held)} A", ScalarValue.Of(zm.Magnitude, "ohm"), "Equivalent motor-side impedance at the dipped voltage (current held)");
            c.Record("Z_m_ph", "Z_m = V_m/I (equivalent at the dipped voltage)", $"|V_m| = √(E² − b²) − a = √({F(e)}² − {F(b)}²) − {F(a)} = {F(vmMag)} V", new TextValue(Ph(zm, "Ω")), "Equivalent motor-side impedance phasor");
        }

        var zt = zs + zc + zm;
        var vpcc = vm + current * zc;
        var where = regulated ? "starter or drive input" : "motor terminals";

        c.Record("Z_t_ph", "Z_t = Z_s + Z_c + Z_m", $"({F(zs.Real)} + j{F(zs.Imaginary)}) + ({F(zc.Real)} + j{F(zc.Imaginary)}) + ({F(zm.Real)} + j{F(zm.Imaginary)})", new TextValue(Ph(zt, "Ω")), "Total series impedance phasor");

        if (!regulated)
        {
            c.Record("I_ph", "I = E/Z_t", $"{F(e)} V / ({F(zt.Magnitude)} Ω ∠ {F(Deg(zt))}°)", new TextValue(Ph(current, "A")), "Starting current phasor at the dipped voltage");
            c.Record("V_m_ph", "V_m = E·Z_m/Z_t", $"{F(e)} V · ({F(zm.Magnitude)} Ω ∠ {F(Deg(zm))}°) / ({F(zt.Magnitude)} Ω ∠ {F(Deg(zt))}°)", new TextValue(Ph(vm, "V")), "Motor terminal voltage phasor (phase)");
        }
        else
        {
            c.Record("I_ph", "I = |I|∠(θ − φ), E = V_m + I·(Z_s + Z_c)", $"{F(c.Si("I_start"))} A held, φ = {F(Math.Acos(Math.Min(1, c.Si(cosSt))) * 180 / Math.PI)}°", new TextValue(Ph(current, "A")), "Held starting current phasor");
            c.Record("V_m_ph", "V_m = |V_m|∠θ, θ = −arg(|V_m| + I·e^(−jφ)·(Z_s + Z_c))", $"|V_m| = {F(vm.Magnitude)} V", new TextValue(Ph(vm, "V")), "Starter or drive input voltage phasor (phase)");
        }

        c.Record("V_pcc_ph", regulated ? "V_pcc = V_m + I·Z_c" : "V_pcc = E·(Z_c + Z_m)/Z_t", regulated ? $"({F(vm.Real)} + j{F(vm.Imaginary)}) V + I·Z_c" : $"{F(e)} V · ({F((zc + zm).Magnitude)} Ω ∠ {F(Deg(zc + zm))}°) / ({F(zt.Magnitude)} Ω ∠ {F(Deg(zt))}°)", new TextValue(Ph(vpcc, "V")), "Point of common coupling voltage phasor (phase)");

        c.Record("I_dip", "|I|", regulated ? $"{F(current.Magnitude)} A (held)" : $"{F(e)} V / {F(zt.Magnitude)} Ω", ScalarValue.Of(current.Magnitude, "A"), "Starting current at the dipped voltage");
        c.Record("V_m", "|V_m|·√3", $"{F(vm.Magnitude)} V · √3", ScalarValue.Of(vm.Magnitude * root3, "V"), $"Voltage at the {where} while starting (line)");
        c.Record("V_pcc", "|V_pcc|·√3", $"{F(vpcc.Magnitude)} V · √3", ScalarValue.Of(vpcc.Magnitude * root3, "V"), "Point of common coupling voltage while starting (line)");
        c.Record("start_dip", "100·(1 − |V_m|/E)", $"100 · (1 − {F(vm.Magnitude)} / {F(e)})", ScalarValue.Of(100 * (1 - vm.Magnitude / e), "%"), $"Voltage dip at the {where}");
        c.Record("dip_pcc", "100·(1 − |V_pcc|/E)", $"100 · (1 − {F(vpcc.Magnitude)} / {F(e)})", ScalarValue.Of(100 * (1 - vpcc.Magnitude / e), "%"), "Voltage dip at the point of common coupling");

        var along = (current * zc * Complex.Conjugate(vm) / vm.Magnitude).Real * root3;
        c.Record("dV_cab", "√3·Re[I·Z_c·V̄_m/|V_m|]", $"√3 · Re[({F(current.Magnitude)} A ∠ {F(Deg(current))}°) · ({F(zc.Magnitude)} Ω ∠ {F(Deg(zc))}°) · e^(−j{F(Deg(vm))}°)]", ScalarValue.Of(along, "V"), "Cable drop along the starting voltage: √3·I·(R cos φ + X sin φ), the E3 three-phase form at this current");

        if (!regulated)
            c.Record("k_T", "(|V_m|/E)²", $"({F(vm.Magnitude)} / {F(e)})²", new ScalarValue(vm.Magnitude * vm.Magnitude / (e * e)), "Torque factor at the dipped voltage relative to rated voltage (torque varies as V²)");

        c.Assign("u_dip", "start_dip / dip_limit", null, $"Dip at the {where} against the limit");

        // Power-factor correction. A drive's line displacement factor, not the motor's, is the power factor before correction.
        var cosRun = starter == StarterVfd ? "cos_phi_vfd" : "cos_phi";

        if (c.Has("I_n"))
        {
            c.Assign("P_in", "sqrt(3) * U * I_n * cos_phi", "kW", "Motor input power from the entered rated current");
            c.Assign("P_in_pl", "P / eta", "kW", "Motor input power from the rated output and efficiency");

            if (Math.Abs(c.Si("P_in") - c.Si("P_in_pl")) > 0.05 * c.Si("P_in_pl"))
                c.Warn("rated", $"I_n: the entered rated current implies an input power of {F(c.Si("P_in") / 1000)} kW, but P/η is {F(c.Si("P_in_pl") / 1000)} kW ({F(Math.Abs(c.Si("P_in") / c.Si("P_in_pl") - 1) * 100)} % apart); check that P, η, cos φ and I_n are for the same motor.");
        }
        else
        {
            c.Assign("P_in", "P / eta", "kW", "Motor input power at rated load");
        }

        c.Assign("tan_1", $"sqrt(1 - {cosRun}^2) / {cosRun}", null, starter == StarterVfd ? "tan φ₁ at the drive's displacement power factor" : "tan φ₁ at the running power factor");
        c.Assign("tan_2", "sqrt(1 - cos_phi2^2) / cos_phi2", null, "tan φ₂ at the target power factor");
        c.Assign("Q_c", "max(0 kW, P_in * (tan_1 - tan_2))", "kvar", "Capacitor kvar to reach the target power factor");
        c.Assign("I_cap", "Q_c / (sqrt(3) * U)", "A", "Capacitor line current at rated voltage");
        c.Assign("I_run1", $"P_in / (sqrt(3) * U * {cosRun})", "A", "Running line current before correction");
        c.Assign("I_run2", "P_in / (sqrt(3) * U * cos_phi2)", "A", "Running line current after correction (when the target is reached)");
        c.Assign("S_sc", "U^2 / Z_src", "MVA", "Short-circuit apparent power at the point of supply");

        var hasBank = c.Si("Q_c") > 0;

        if (hasBank)
            c.Assign("h_res", "sqrt(S_sc / Q_c)", null, "Parallel resonance order of the capacitor bank with the supply");

        c.Output("Z_src", "Source impedance (magnitude)");
        c.Output("Z_m", regulated ? "Equivalent motor-side impedance at the dipped voltage" : "Motor starting impedance (magnitude)");
        c.Output("I_rated", "Motor rated current");
        c.Output("I_start", regulated ? "Starting current held (soft-starter or drive limit)" : "Starting current at rated voltage");
        c.Output("S_start", "Starting apparent power");
        c.Output("I_dip", "Starting current at the dipped voltage");
        c.Output("V_m", $"Voltage at the {where} while starting");
        c.Output("V_pcc", "Point of common coupling voltage while starting");
        c.Output("start_dip", $"Voltage dip at the {where}");
        c.Output("dip_pcc", "Voltage dip at the point of common coupling");
        c.Output("dV_cab", "Cable drop along the starting voltage");

        if (!regulated)
            c.Output("k_T", "Torque factor at the dipped voltage");

        c.Output("u_dip", $"Dip utilisation at the {where}");
        c.Output("P_in", "Motor input power at rated load");
        c.Output("kvar", "Capacitor kvar to the target power factor", c.Scalar("Q_c"));
        c.Output("I_cap", "Capacitor current");
        c.Output("I_run1", "Running current before correction");
        c.Output("I_run2", "Running current after correction");
        c.Output("S_sc", "Short-circuit apparent power at the bank");

        if (hasBank)
            c.Output("h_res", "Parallel resonance order");

        c.CheckUtilisation(regulated ? "Starter or drive input dip within the limit" : "Motor-terminal dip within the limit", "u_dip");

        if (c.Has("dip_limit_pcc"))
        {
            c.Input("dip_limit_pcc");
            c.Assign("u_dip_pcc", "dip_pcc / dip_limit_pcc", null, "Point-of-common-coupling dip against the limit");
            c.Output("u_dip_pcc", "Point-of-common-coupling dip utilisation");
            c.CheckUtilisation("Point-of-common-coupling dip within the limit", "u_dip_pcc", "IEC 61000-3-11 scope");
        }

        if (!(c.Si("cos_phi2") > c.Si(cosRun)))
            c.Warn("pfc", $"cos_phi2: the target power factor is not above the {(starter == StarterVfd ? "drive's displacement" : "motor's own")} power factor, so no capacitor is needed (kvar = 0).");

        if (hasBank && starter is StarterVfd or StarterSoft)
            c.Warn("pfc-starter", starter == StarterVfd
                ? "starter: power-factor capacitors must not be connected on the output of a drive; correct at the supply side of the drive, or not at all."
                : "starter: connect the capacitors only after the soft starter's bypass contactor has closed, never across the soft starter during the start.");

        if (starter == StarterVfd && !c.InputFlag("harmonics"))
            c.Warn("harmonics", "harmonics: a drive is itself a harmonic load; set the harmonic-loads flag so the capacitor resonance is assessed, and check the supply distortion.");
        else if (hasBank && c.InputFlag("harmonics"))
            c.Warn("harmonics", $"harmonics: harmonic loads are present and the {F(c.Si("Q_c") / 1000)} kvar bank forms a parallel resonance with the supply at about harmonic order {F(c.Si("h_res"))} (h ≈ √(S_sc/Q_c)); a resonance near a harmonic the loads inject (typically the 5th, 7th, 11th, 13th) overloads the capacitors and amplifies the distortion. Check by a harmonic study, or use a detuned (reactor) bank.");
    }

    /// <summary>A phasor as "a + jb unit [|z| unit ∠ angle°]"; components below rounding noise (1e-12 of the magnitude) are shown as zero.</summary>
    private static string Ph(Complex z, string unit)
    {
        z = Clean(z);
        return $"{F(z.Real)} {(z.Imaginary < 0 ? "−" : "+")} j{F(Math.Abs(z.Imaginary))} {unit}  [{F(z.Magnitude)} {unit} ∠ {F(Deg(z))}°]";
    }

    private static Complex Clean(Complex z)
    {
        var floor = 1e-12 * z.Magnitude;
        return new Complex(Math.Abs(z.Real) < floor ? 0 : z.Real, Math.Abs(z.Imaginary) < floor ? 0 : z.Imaginary);
    }

    private static double Deg(Complex z) => Clean(z).Phase * 180 / Math.PI;

    private static string F(double v)
    {
        if (!double.IsFinite(v))
            return "—";

        var text = CalcValue.FormatNumber(v, 5);
        return text.StartsWith('-') ? "−" + text[1..] : text;
    }
}
