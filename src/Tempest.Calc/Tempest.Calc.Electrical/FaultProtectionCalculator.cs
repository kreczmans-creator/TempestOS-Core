using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Electrical;

/// <summary>
/// E5 — fault protection in a TN system: earth fault loop impedance and automatic disconnection, the adiabatic check of
/// the protective conductor with k derived from material constants, and a simplified prospective fault level. No
/// BS 7671 or IEC 60364 table is embedded: Zs,max comes from C_min·U₀/I_a, k from its formula, fuse and MCCB
/// characteristics from points the user reads off the manufacturer's curve.
/// </summary>
public sealed class FaultProtectionCalculator : ICalcCalculator
{
    public const string Id = "elec.fault-protection";
    public const string McbB = "MCB type B (I_a = 5 I_n)";
    public const string McbC = "MCB type C (I_a = 10 I_n)";
    public const string McbD = "MCB type D (I_a = 20 I_n)";
    public const string Curve = "fuse or MCCB (time–current points)";
    public const double CopperRho20 = 17.241e-6;

    private static readonly IReadOnlyList<CalcColumnSpec> CurveColumns =
    [
        new("t", "Time", CalcInputKind.Quantity, "s", HardMin: 1e-4, HardMax: 1e5),
        new("I", "Current", CalcInputKind.Quantity, "A", HardMin: 1e-3, HardMax: 1e6),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.5.0", CalcDiscipline.Electrical, "Fault protection (BS 7671)",
        "Calculates automatic disconnection of supply in a TN system for one circuit: loop impedance, disconnection time and the adiabatic conductor check. It also gives the prospective three-phase fault at the origin against the breaking capacity.",
        "Loop impedance: Z_s = Z_e + (R_1 + R_2)·[1 + α(θ − 20)], R = k_R·ρ_20·L/S (k_R = 1.125 takes the pure-copper ρ_20 to the cable standard's maximum conductor resistance; k enters with the pure ρ_20)\n" +
        "Fault current: I_f = C_min·U_0/Z_s, C_min = 0.95\n" +
        "Maximum loop impedance: Z_s,max = C_min·U_0/I_a; I_a = 5, 10 or 20·I_n for MCB types B, C, D, or read from the time–current points at t_req (log-log)\n" +
        "Adiabatic conductor check: S_min = √(I²t)/k, k = √[Q_c(β + 20)/ρ_20 · ln(1 + (θ_f − θ_i)/(β + θ_i))] (derived); I²t = I_f²·t_d from 0.1 s; below 0.1 s the device's let-through I²t at the maximum prospective fault bounds both conductors at the origin (reg. 434.5.2, 543.1.3), and without it the sheet fails 'Conductors against let-through at the origin'\n" +
        "Protective device check: the device chosen here must be the one carried from the cable step (device_code 1 = MCB B, 2 = C, 3 = D, 4 = fuse or MCCB), else the sheet is blocked\n" +
        "Fault level (simplified): Z_T = u_k·U_n²/S_r, I_k3 = c·U_n/(√3·Z_T), U_n = √3·U_0",
        [
            new CalcInputSpec("device", "Protective device", CalcInputKind.Choice, Choices: [McbB, McbC, McbD, Curve], DefaultChoice: McbB, Group: "Setup"),
            new CalcInputSpec("I_n", "Rated current", CalcInputKind.Quantity, "A", Default: 32, Min: 6, Max: 630, HardMin: 1, HardMax: 1600, Group: "Setup"),
            new CalcInputSpec("curve", "Time–current points (fuse or MCCB)", CalcInputKind.Table, Columns: CurveColumns, MaxRows: 40, IsOptional: true, Group: "Setup"),
            new CalcInputSpec("device_code", "Device type code carried from the cable step (1 MCB type B, 2 type C, 3 type D, 4 fuse, MCCB or MPCB); the device chosen here must match it", CalcInputKind.Number, Min: 1, Max: 4, HardMin: 0, HardMax: 9, IsOptional: true, Group: "Setup"),
            new CalcInputSpec("L", "Circuit length", CalcInputKind.Quantity, "m", Default: 20, Min: 1, Max: 500, HardMin: 0.1, HardMax: 1000, Group: "Geometry"),
            new CalcInputSpec("S_line", "Line conductor cross-section", CalcInputKind.Quantity, "mm^2", Default: 2.5, Min: 1, Max: 300, HardMin: 0.5, HardMax: 630, Group: "Geometry"),
            new CalcInputSpec("S_cpc", "Protective conductor cross-section", CalcInputKind.Quantity, "mm^2", Default: 1.5, Min: 1, Max: 300, HardMin: 0.5, HardMax: 630, Group: "Geometry"),
            new CalcInputSpec("rho_20", "Resistivity at 20 °C (used for k, and for R₁ + R₂ through k_R)", CalcInputKind.Quantity, "ohm*mm^2/m", Default: CopperRho20 * 1e3, Min: 0.015, Max: 0.05, HardMin: 1e-3, HardMax: 0.1, Group: "Material"),
            new CalcInputSpec("k_R", "Conductor resistance factor on ρ₂₀ for R₁ + R₂ (1 = the resistivity itself; the default of 1.125 takes pure copper to the cable standard's maximum conductor resistances, so Z_s is not understated; k is not changed by it)", CalcInputKind.Number, Default: 1.125, Min: 1, Max: 1.25, HardMin: 1, HardMax: 1.5, Group: "Material"),
            new CalcInputSpec("alpha", "Temperature coefficient of resistance", CalcInputKind.Quantity, "1/K", Default: 0.00393, Min: 0.0035, Max: 0.0045, HardMin: 0.001, HardMax: 0.01, Group: "Material"),
            new CalcInputSpec("Q_c", "Volumetric heat capacity", CalcInputKind.Quantity, "J/(K*mm^3)", Default: 3.45e-3, Min: 0.002, Max: 0.005, HardMin: 1e-3, HardMax: 1e-2, Group: "Material"),
            new CalcInputSpec("beta", "Reciprocal temperature coefficient β", CalcInputKind.Quantity, "K", Default: 234.5, Min: 200, Max: 260, HardMin: 100, HardMax: 400, Group: "Material"),
            new CalcInputSpec("theta_i", "Initial temperature for the adiabatic check", CalcInputKind.Quantity, "degC", Default: 70, Min: 40, Max: 120, HardMin: 20, HardMax: 250, Group: "Material"),
            new CalcInputSpec("theta_f", "Final temperature for the adiabatic check", CalcInputKind.Quantity, "degC", Default: 160, Min: 140, Max: 300, HardMin: 100, HardMax: 400, Group: "Material"),
            new CalcInputSpec("U_0", "Nominal voltage to earth", CalcInputKind.Quantity, "V", Default: 230, Min: 100, Max: 600, HardMin: 50, HardMax: 1000, Group: "Conditions"),
            new CalcInputSpec("Z_e", "External earth fault loop impedance", CalcInputKind.Quantity, "ohm", Default: 0.35, Min: 0.05, Max: 2, HardMin: 0.01, HardMax: 3, Group: "Conditions"),
            new CalcInputSpec("T_op", "Conductor temperature for Z_s", CalcInputKind.Quantity, "degC", Default: 70, Min: 30, Max: 90, HardMin: 20, HardMax: 90, Group: "Conditions", Aliases: ["theta"]),
            new CalcInputSpec("S_r", "Supply transformer rating (for the fault level)", CalcInputKind.Quantity, "kVA", Min: 10, Max: 2500, HardMin: 1, HardMax: 5000, IsOptional: true, Group: "Conditions"),
            new CalcInputSpec("u_k", "Transformer impedance voltage", CalcInputKind.Quantity, "%", Min: 2, Max: 10, HardMin: 1, HardMax: 20, IsOptional: true, Group: "Conditions"),
            new CalcInputSpec("C_min", "Voltage factor for the minimum fault", CalcInputKind.Number, Default: 0.95, Min: 0.93, Max: 1, HardMin: 0.9, HardMax: 1, Group: "Factors"),
            new CalcInputSpec("c_max", "Voltage factor c for the maximum fault", CalcInputKind.Number, Default: 1.1, Min: 1.05, Max: 1.1, HardMin: 1, HardMax: 1.1, Group: "Factors"),
            new CalcInputSpec("t_req", "Required disconnection time", CalcInputKind.Quantity, "s", Default: 0.4, Min: 0.2, Max: 5, HardMin: 0.1, HardMax: 5, Group: "Requirements"),
            new CalcInputSpec("I2t_let", "Device let-through energy I²t at the maximum prospective fault (disconnection below 0.1 s)", CalcInputKind.Quantity, "A^2*s", Min: 100, Max: 50000000, HardMin: 1, HardMax: 1e9, IsOptional: true, Group: "Device"),
            new CalcInputSpec("I_cn", "Rated breaking capacity", CalcInputKind.Quantity, "kA", Min: 3, Max: 100, HardMin: 1, HardMax: 150, IsOptional: true, Group: "Device"),
        ],
        [
            new CalcAssumption("The system is TN with a single earth fault of negligible impedance.", "Scope of the check."),
            new CalcAssumption("There is no motor contribution, no arc fault and no IT system.", "Scope of the check."),
            new CalcAssumption("The line and protective conductors are the same material at the same temperature.", "Conductor data from a material record."),
            new CalcAssumption("The resistance is ρ₂₀·L/S corrected by α.", "Conductor data from a material record."),
            new CalcAssumption("The factor k is derived from the material constants, not read from a licensed table.", "Derived constants, not read from a table."),
            new CalcAssumption("Fuse and MCCB characteristics are points the user reads off the manufacturer's curve.", "No time–current table ships."),
            new CalcAssumption("The points are interpolated log-log, with no extrapolation.", "No time–current table ships."),
            new CalcAssumption("The optional field-test value 0.8 × Z_s,max is a rule of thumb for measurements at ambient temperature.", "Labelled on the sheet."),
            new CalcAssumption("The line conductor check (BS 7671 clause 434.5.2) needs the device's let-through energy at the maximum prospective fault.", "Stated limitation: the end-of-line earth fault is the minimum fault, not the maximum."),
            new CalcAssumption("The end-of-line earth fault is the minimum fault and does not bound that energy.", "Stated limitation: the end-of-line earth fault is the minimum fault, not the maximum."),
            new CalcAssumption("The line conductor check is made only when I2t_let is entered or the disconnection time is 0.1 s or more.", "Stated limitation: the end-of-line earth fault is the minimum fault, not the maximum."),
        ],
        [
            new CalcClauseRef("BS 7671:2018", "reg. 411.3.2.2, 411.3.2.3, 411.4.4", "Automatic disconnection in TN systems: times and the loop impedance condition"),
            new CalcClauseRef("BS 7671:2018", "reg. 543.1.3, 434.5.2", "Protective conductor and fault current adiabatic checks"),
            new CalcClauseRef("BS EN 60204-1:2018", "cl. 6.3.3", "Protection by automatic disconnection of supply"),
            new CalcClauseRef("IEC 60909-0:2016", "cl. 5.3", "Voltage factor c"),
        ],
        [
            new CalcApplicabilityRule("curve", "The time–current points must bracket the required disconnection time; at least two points are needed.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("curve-range", "The fault current is outside the points entered: above the highest the fastest time entered bounds the disconnection time, below the lowest no time is read. The loop impedance verdict is still given.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("resistance", "R₁ + R₂ and Z_s may be understated: the conductor resistance factor k_R is below 1.05, or the protective conductor is entered equal to a line conductor above 16 mm².", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("theta", "Z_s is taken at a conductor temperature below the adiabatic check's initial temperature theta_i: R₁ + R₂ and Z_s are understated.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("t_req", "The required disconnection time is above 0.4 s at U_0 up to 230 V, or is the 230 V time at a higher voltage to earth: check it against the standard's table of times.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("let-through", "Disconnection below 0.1 s without a let-through I²t: the protective conductor check uses I_f²·0.1 s at the end-of-line fault only, the line conductor check is not made and neither conductor has been shown to withstand a fault near the origin, so the sheet fails 'Conductors against let-through at the origin' until I2t_let is entered.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("device", "The device chosen must be the one carried from the cable step (device_code).", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("device_code", "The device type code carried from the cable step must be a whole number from 1 to 4.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("adiabatic", "No disconnection time: the adiabatic check is not made.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("temperatures", "The adiabatic final temperature must exceed the initial temperature.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-mcb-b32", "32 A type B MCB, 20 m of 2.5/1.5 mm² copper at 70 °C",
                new CalcReferenceSource("Analytic (C_min relation and derived k; k_R 1, the pure resistivity)", null, "R_1 + R_2 = 17.241e-3·20·(1/2.5 + 1/1.5) = 0.367808 Ω at 20 °C, × 1.1965 = 0.440082 Ω; Z_s = 0.790082 Ω; I_f = 218.5/0.790082 = 276.553 A; Z_s,max = 218.5/160 = 1.365625 Ω; S_min = √(276.553²·0.1)/114.836 = 0.76155 mm²"),
                [Q("Z_e", 0.35, "ohm"), Q("L", 20, "m"), Q("S_line", 2.5, "mm^2"), Q("S_cpc", 1.5, "mm^2"), CalcInputValue.Of("k_R", 1), CalcInputValue.Choice("device", McbB), Q("I_n", 32, "A")],
                [new CalcExpectedValue("Z_s", 0.7900823, "ohm", 1e-6), new CalcExpectedValue("I_f", 276.5535, "A", 1e-6), new CalcExpectedValue("Z_s_max", 1.365625, "ohm", 1e-9), new CalcExpectedValue("u_Zs", 0.5785499, "", 1e-6), new CalcExpectedValue("k", 114.8361, "", 1e-6), new CalcExpectedValue("S_min", 0.7615538, "mm^2", 1e-6)]),
            new CalcTestCase("ka-mcb-b32-maximum-resistance", "The same circuit at the cable standard's maximum conductor resistance (k_R 1.125, the default)",
                new CalcReferenceSource("Analytic (C_min relation and derived k; k_R 1.125 takes the pure resistivity to the cable standard's maximum conductor resistance, recomputed in python3)", null, "R_1 + R_2 = 17.241e-3·1.125·20·(1/2.5 + 1/1.5) = 0.413784 Ω at 20 °C, × 1.1965 = 0.495093 Ω; Z_s = 0.845093 Ω; I_f = 218.5/0.845093 = 258.552 A; Z_s,max = 218.5/160 = 1.365625 Ω; u_Zs = 0.618832; S_min = √(258.552²·0.1)/114.836 = 0.711981 mm²"),
                [Q("Z_e", 0.35, "ohm"), Q("L", 20, "m"), Q("S_line", 2.5, "mm^2"), Q("S_cpc", 1.5, "mm^2"), CalcInputValue.Choice("device", McbB), Q("I_n", 32, "A")],
                [new CalcExpectedValue("R_12", 0.4950926, "ohm", 1e-6), new CalcExpectedValue("Z_s", 0.8450926, "ohm", 1e-6), new CalcExpectedValue("I_f", 258.5516, "A", 1e-6), new CalcExpectedValue("u_Zs", 0.6188321, "", 1e-6), new CalcExpectedValue("S_min", 0.7119815, "mm^2", 1e-6)]),
            new CalcTestCase("ka-fuse-curve", "Fuse from user-read curve points, 0.4 s",
                new CalcReferenceSource("Analytic (log-log interpolation of illustrative points)", null, "Points (5 s, 120 A), (0.4 s, 220 A), (0.1 s, 320 A), (0.01 s, 700 A): I_a(0.4 s) = 220 A, Z_s,max = 0.993182 Ω; I_f = 276.553 A gives t_d = 0.171577 s; S_min = √(276.553²·0.171577)/114.836 = 0.99754 mm²"),
                [Q("Z_e", 0.35, "ohm"), Q("L", 20, "m"), Q("S_line", 2.5, "mm^2"), Q("S_cpc", 1.5, "mm^2"), CalcInputValue.Of("k_R", 1), CalcInputValue.Choice("device", Curve), Q("I_n", 32, "A"), Points((5, 120), (0.4, 220), (0.1, 320), (0.01, 700))],
                [new CalcExpectedValue("I_a", 220, "A", 1e-9), new CalcExpectedValue("Z_s_max", 0.9931818, "ohm", 1e-6), new CalcExpectedValue("t_d", 0.1715774, "s", 1e-6), new CalcExpectedValue("S_min", 0.9975407, "mm^2", 1e-6)]),
            new CalcTestCase("ka-fault-level", "Prospective fault from a 500 kVA, 5 % transformer",
                new CalcReferenceSource("Analytic (simplified IEC 60909 form)", null, "U_n = √3·230 = 398.37 V; Z_T = 0.05·398.37²/500 kVA = 0.01587 Ω; I_k3 = 1.1·398.37/(√3·0.01587) = 15.942 kA; against I_cn 25 kA: 0.6377"),
                [Q("S_r", 500, "kVA"), Q("u_k", 5, "%"), Q("I_cn", 25, "kA")],
                [new CalcExpectedValue("Z_T", 0.01587, "ohm", 1e-6), new CalcExpectedValue("I_k3", 15.94203, "kA", 1e-6), new CalcExpectedValue("u_Icn", 0.6376812, "", 1e-6)]),
            new CalcTestCase("ka-wiring-matters-b32", "Maximum Z_s for a 32 A type B MCB (IET Wiring Matters)",
                new CalcReferenceSource("IET Wiring Matters, \"Determining the maximum earth fault loop impedance\"", "March 2023", "Z_s,max = 0.95 × 230/(5 × 32) = 1.37 Ω as quoted in the addendum; to be checked against the article"),
                [CalcInputValue.Choice("device", McbB), Q("I_n", 32, "A")],
                [new CalcExpectedValue("Z_s_max", 1.37, "ohm", 0.005)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-schneider-k", "Derived k for copper, PVC 70 → 160 °C and XLPE 90 → 250 °C (Schneider guide)",
                new CalcReferenceSource("Schneider Electric, Electrical Installation Guide, \"Sizing of protective earthing conductor\"", "current web edition", "k ≈ 115 (PVC) and 143 (XLPE) with Q_c 3.45e-3 J/(K·mm³), β 234.5 K, ρ₂₀ 17.241e-6 Ω·mm, as quoted in the addendum; this case checks the 90 → 250 °C value"),
                [Q("theta_i", 90, "degC"), Q("theta_f", 250, "degC"), Q("T_op", 90, "degC")],
                [new CalcExpectedValue("k", 143, "", 0.005)],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Power",
        OutputPorts:
        [
            new CalcOutputPort("Z_s", "ohm", "Earth fault loop impedance", "fault.impedance_loop"),
            new CalcOutputPort("I_f", "A", "Earth fault current", "fault.current_earth"),
            new CalcOutputPort("S_min", "mm^2", "Minimum protective conductor", "conductor.area_adiabatic_min"),
            new CalcOutputPort("I_k3", "kA", "Prospective three-phase fault", "fault.current_three_phase"),
            new CalcOutputPort("c_k3", "", "Voltage factor c that I_k3 was computed with, c_max (→ motor-start c_src, so the motor start uses the same factor)", "fault.factor_voltage"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("conductor.material", "Conductor material constants", ["rho_20", "k_R", "alpha", "Q_c", "beta", "theta_i", "theta_f"], "Material constants (IEC 60228 / IEC 60949 values) and the insulation's initial and final temperatures, with their source."),
            new CalcRecordKind("device.characteristic", "Device characteristic (time–current points)", ["curve", "I2t_let", "I_cn"], "Read from the manufacturer's curve and datasheet for the exact device."),
        ],
        IndependentCheck: true);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    /// <summary>A time–current table from (t s, I A) points.</summary>
    public static CalcInputValue Points(params (double T, double I)[] points) =>
        CalcInputValue.Table("curve", points.Select(p => CalcRow.Of(("t", new CalcCell(p.T, "s")), ("I", new CalcCell(p.I, "A")))).ToList());

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_Icn", "u_Zs", "u_cpc", "u_line");
        foreach (var s in new[] { "U_0", "Z_e", "C_min", "c_max", "L", "S_line", "S_cpc", "T_op", "rho_20", "k_R", "alpha", "Q_c", "beta", "theta_i", "theta_f", "I_n", "t_req" })
            c.Input(s);

        var device = c.InputText("device");

        if (c.Has("device_code"))
        {
            var code = c.InputSi("device_code");

            if (code != Math.Round(code) || code < 1 || code > 4)
            {
                c.Block("device_code", "device_code: the device type code is a whole number 1 to 4.");
                return;
            }

            var carried = (int)code switch { 1 => McbB, 2 => McbC, 3 => McbD, _ => Curve };

            if (device != carried)
            {
                c.Block("device", $"device: the cable step sized the circuit for {carried}, but the device chosen here is {device}. Choose {carried}, or change the cable step.");
                return;
            }
        }

        if (!(c.Si("theta_f") > c.Si("theta_i")))
        {
            c.Block("temperatures", "theta_f: the final temperature must exceed the initial temperature.");
            return;
        }

        c.Assign("R_12", "rho_20 * k_R * L * (1 / S_line + 1 / S_cpc) * (1 + alpha * (T_op - 20 degC))", "ohm", "R₁ + R₂ at the conductor temperature");

        if (c.Si("k_R") < 1.05)
            c.Warn("resistance", $"k_R: the factor {F(c.Si("k_R"))} leaves R₁ + R₂ near the pure resistivity, so Z_s is understated. Use 1.125 unless the conductor resistance is measured or taken from the maker's data.");
        // elec.fault-protection-06: Z_s is taken at T_op, the adiabatic check starts from theta_i; a loop temperature below the conductor's own initial temperature understates Z_s.
        if (c.Si("T_op") < c.Si("theta_i") - 0.5)
            c.Warn("theta", $"T_op: Z_s is taken at {F(c.Si("T_op") - 273.15)} °C, below the initial temperature theta_i of {F(c.Si("theta_i") - 273.15)} °C. R₁ + R₂ and Z_s are understated, so set T_op to theta_i unless the circuit runs cooler.");

        // elec.fault-protection-05: t_req is an input, not read from U_0 and the circuit type, so say where it needs checking.
        if (c.Si("U_0") <= 230 * (1 + 1e-9) && c.Si("t_req") > 0.4 * (1 + 1e-9))
            c.Warn("t_req", $"t_req: {F(c.Si("t_req"))} s is longer than 0.4 s, the time for a final circuit at U_0 up to 230 V. A longer time is allowed only for a distribution circuit, so check it against Table 41.1 of your copy.");
        else if (c.Si("U_0") > 230 * (1 + 1e-9) && c.Si("t_req") >= 0.4 * (1 - 1e-9))
            c.Warn("t_req", $"t_req: {F(c.Si("t_req"))} s is the time for U_0 up to 230 V. At U_0 = {F(c.Si("U_0"))} V the time in Table 41.1 of your copy is shorter, so check t_req against it.");

        if (c.Si("S_line") > 16e-6 * (1 + 1e-9) && Math.Abs(c.Si("S_cpc") - c.Si("S_line")) <= 1e-9 * c.Si("S_line"))
            c.Warn("resistance", $"S_cpc: the protective conductor equals the line conductor ({F(c.Si("S_line") * 1e6)} mm²), as the pack link from the cable step sets it. Above 16 mm² it is normally smaller, which raises Z_s and the adiabatic utilisation, so enter the real S_cpc.");
        c.Assign("Z_s", "Z_e + R_12", "ohm", "Earth fault loop impedance", "BS 7671 reg. 411.4.4");
        c.Assign("I_f", "C_min * U_0 / Z_s", "A", "Earth fault current");

        var amp = UnitCatalog.Parse("A");
        var second = UnitCatalog.Parse("s");
        double ia;
        double? td;

        if (device == Curve)
        {
            var points = c.Rows("curve").Select(r => (T: Cell(r, "t", second), I: Cell(r, "I", amp))).Where(p => p.T > 0 && p.I > 0).OrderBy(p => p.I).ToList();

            if (points.Count < 2 || Interpolate(points, c.Si("t_req"), byTime: true) is not { } current)
            {
                c.Block("curve", "curve: enter at least two time–current points that bracket the required disconnection time.");
                return;
            }

            ia = current;
            c.Record("I_a", "I(t_req), log-log between the bracketing points", $"t_req = {F(c.Si("t_req"))} s", ScalarValue.Of(ia, amp), "Current for disconnection in the required time", "BS 7671 reg. 411.3.2.2");
            td = Interpolate(points, c.Si("I_f"), byTime: false);

            // Above the highest current entered the device is faster than its fastest point: that point's time is a safe
            // upper bound (Opus verification). Below the lowest current, no disconnection time can be stated.
            if (td is null && c.Si("I_f") > points[^1].I)
            {
                td = points.MinBy(p => p.T).T;
                c.Warn("curve-range", $"curve: the fault current {F(c.Si("I_f"))} A is above the highest point entered. The fastest time entered ({F(td.Value)} s) is used as an upper bound.");
            }
            else if (td is null)
            {
                c.Warn("curve-range", $"curve: the fault current {F(c.Si("I_f"))} A is below the lowest point entered. The disconnection time is not read.");
            }
        }
        else
        {
            var multiple = device == McbB ? 5 : device == McbC ? 10 : 20;
            c.Assign("I_a", $"{multiple} * I_n", "A", $"Instantaneous tripping current ({multiple} I_n)", "BS EN 60898-1");
            ia = c.Si("I_a");
            td = c.Si("I_f") >= ia ? 0.1 : null;
        }

        c.Assign("Z_s_max", "C_min * U_0 / I_a", "ohm", "Maximum earth fault loop impedance", "BS 7671 reg. 411.4.4");
        c.Assign("u_Zs", "Z_s / Z_s_max", null, "Loop impedance utilisation");
        c.Assign("Z_test", "0.8 * Z_s_max", "ohm", "Field-test maximum at ambient (0.8 × Z_s,max, rule of thumb)");

        if (td is { } t)
            c.Record("t_d", device == Curve ? "t(I_f), log-log between the bracketing points" : "instantaneous region (I_f ≥ I_a): t ≤ 0.1 s", $"I_f = {F(c.Si("I_f"))} A", ScalarValue.Of(t, second), "Disconnection time");

        // Adiabatic: I²t from the let-through energy below 0.1 s, else I_f²·t. With no disconnection time (an MCB below
        // its instantaneous current, or a fault current below the curve) the energy is unknown and the check is not made
        // (Opus review, finding 12); the fault level is still checked.
        double i2t;

        if (td is null)
        {
            c.Warn("adiabatic", "I_f: the device does not disconnect within the data given, so the adiabatic check cannot be made. Reduce the loop impedance or choose a faster device.");
            var outputsWithout = new List<(string, string)> { ("R_12", "R₁ + R₂"), ("Z_s", "Loop impedance"), ("I_f", "Earth fault current"), ("I_a", "Disconnection current"), ("Z_s_max", "Maximum loop impedance"), ("u_Zs", "Loop impedance utilisation"), ("Z_test", "Field-test maximum (rule of thumb)") };
            FaultLevel(c, outputsWithout);

            foreach (var (symbol, text) in outputsWithout)
                c.Output(symbol, text);

            c.Check("Loop impedance against the maximum", "Z_s", "Z_s_max", "BS 7671 reg. 411.4.4");
            c.CheckCondition("Fault current against the disconnection current", false, device == Curve ? "fault current below the curve points" : "I_f below the instantaneous tripping current", "disconnection within t_req", null, "BS 7671 reg. 411.3.2.2");

            if (c.Has("I_cn") && c.Has("S_r") && c.Has("u_k"))
                c.Check("Prospective fault against the breaking capacity", "I_k3", "I_cn");

            return;
        }

        // The line conductor (reg. 434.5.2) is checked against the energy the device lets through at the maximum prospective fault: from the
        // curve above 0.1 s, or the entered let-through. The end-of-line earth fault is the minimum fault, so I_f²·0.1 s is not a bound for the
        // line conductor and that check is not made without a let-through value (Green G-B-03).
        var lineChecked = true;

        if (td is { } time && time >= 0.1 && device == Curve)
        {
            i2t = c.Si("I_f") * c.Si("I_f") * time;
            c.Record("I2t", "I_f²·t_d", $"{F(c.Si("I_f"))}² × {F(time)}", ScalarValue.Of(i2t, "A^2*s"), "Fault energy");
        }
        else if (c.Has("I2t_let"))
        {
            c.Input("I2t_let");
            i2t = c.Si("I2t_let");
            c.Record("I2t", "device let-through I²t at the maximum prospective fault", $"{F(i2t)} A²s", ScalarValue.Of(i2t, "A^2*s"), "Fault energy (let-through below 0.1 s)");
        }
        else
        {
            c.Warn("let-through", "I2t_let: disconnection is below 0.1 s and no let-through I²t is entered. The check 'Conductors against let-through at the origin' fails until I2t_let is entered.");
            i2t = c.Si("I_f") * c.Si("I_f") * 0.1;
            c.Record("I2t", "I_f²·0.1 s (no let-through entered)", $"{F(c.Si("I_f"))}² × 0.1", ScalarValue.Of(i2t, "A^2*s"), "Fault energy at the end-of-line fault, clearing time taken as 0.1 s");
            lineChecked = false;
        }

        var qc = c.Si("Q_c") * 1e-9;   // J/(K·mm³) from SI J/(K·m³)
        var beta = c.Si("beta");
        var rho = c.Si("rho_20") * 1e3; // Ω·mm from SI Ω·m
        var ti = c.Si("theta_i") - 273.15;
        var tf = c.Si("theta_f") - 273.15;
        var k = Math.Sqrt(qc * (beta + 20) / rho * Math.Log(1 + (tf - ti) / (beta + ti)));
        c.Record("k", "√[Q_c(β + 20)/ρ₂₀ · ln(1 + (θ_f − θ_i)/(β + θ_i))] (derived)", $"√[{F(qc)} × {F(beta + 20)} / {F(rho)} × ln(1 + {F(tf - ti)}/{F(beta + ti)})]", new ScalarValue(k), "Adiabatic factor k, A·√s/mm² (derived, compare with the licensed table)", "BS 7671 reg. 543.1.3");
        var smin = Math.Sqrt(i2t) / k;
        c.Record("S_min", "√(I²t)/k", $"√{F(i2t)} / {F(k)}", ScalarValue.Of(smin, "mm^2"), "Minimum conductor cross-section (adiabatic)", "BS 7671 reg. 543.1.3, 434.5.2");
        c.Assign("u_cpc", "S_min / S_cpc", null, "Protective conductor utilisation");

        var outputs = new List<(string, string)> { ("R_12", "R₁ + R₂"), ("Z_s", "Loop impedance"), ("I_f", "Earth fault current"), ("I_a", "Disconnection current"), ("Z_s_max", "Maximum loop impedance"), ("u_Zs", "Loop impedance utilisation"), ("Z_test", "Field-test maximum (rule of thumb)"), ("k", "Adiabatic factor (derived)"), ("S_min", "Minimum cross-section"), ("u_cpc", "Protective conductor utilisation") };

        if (lineChecked)
        {
            c.Assign("u_line", "S_min / S_line", null, "Line conductor utilisation (same fault energy)");
            outputs.Add(("u_line", "Line conductor utilisation"));
        }

        if (td is not null)
            outputs.Insert(4, ("t_d", "Disconnection time"));

        FaultLevel(c, outputs);

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        c.Check("Loop impedance against the maximum", "Z_s", "Z_s_max", "BS 7671 reg. 411.4.4");

        if (td is not null)
            c.Check("Disconnection time against the required time", "t_d", "t_req", "BS 7671 reg. 411.3.2.2");

        c.Check("Adiabatic minimum against the protective conductor", "S_min", "S_cpc", "BS 7671 reg. 543.1.3");

        if (lineChecked)
            c.Check("Adiabatic minimum against the line conductor", "S_min", "S_line", "BS 7671 reg. 434.5.2");
        else
            c.CheckCondition("Conductors against let-through at the origin", false, "I2t_let not entered", "I²t the device lets through at the maximum prospective fault", null, "BS 7671 reg. 434.5.2, 543.1.3");

        if (c.Has("I_cn") && c.Has("S_r") && c.Has("u_k"))
            c.Check("Prospective fault against the breaking capacity", "I_k3", "I_cn");
    }

    /// <summary>The simplified prospective fault level and the breaking-capacity utilisation, when the transformer data are given.</summary>
    private static void FaultLevel(CalcRunContext c, List<(string, string)> outputs)
    {
        if (c.Has("S_r") && c.Has("u_k"))
        {
            c.Input("S_r");
            c.Input("u_k");
            c.Assign("U_n", "sqrt(3) * U_0", "V", "Line voltage");
            c.Assign("Z_T", "u_k * U_n^2 / S_r", "ohm", "Transformer impedance");
            c.Assign("I_k3", "c_max * U_n / (sqrt(3) * Z_T)", "kA", "Prospective three-phase fault at the origin (simplified)", "IEC 60909-0 cl. 5.3");
            c.Assign("c_k3", "c_max", null, "Voltage factor c of the prospective fault current", "IEC 60909-0 cl. 5.3");
            outputs.AddRange([("Z_T", "Transformer impedance"), ("I_k3", "Prospective fault current"), ("c_k3", "Voltage factor of I_k3")]);

            if (c.Has("I_cn"))
            {
                c.Input("I_cn");
                c.Assign("u_Icn", "I_k3 / I_cn", null, "Breaking capacity utilisation");
                outputs.Add(("u_Icn", "Breaking capacity utilisation"));
            }
        }
    }

    /// <summary>Log-log interpolation on time–current points sorted by current: I at a time (<paramref name="byTime"/>) or t at a current; null outside the points.</summary>
    public static double? Interpolate(IReadOnlyList<(double T, double I)> points, double x, bool byTime)
    {
        for (var i = 0; i < points.Count - 1; i++)
        {
            var (a, b) = (points[i], points[i + 1]);
            var (x0, x1, y0, y1) = byTime ? (a.T, b.T, a.I, b.I) : (a.I, b.I, a.T, b.T);
            var lo = Math.Min(x0, x1);
            var hi = Math.Max(x0, x1);

            if (x < lo || x > hi || x0 == x1)
                continue;

            var f = (Math.Log(x) - Math.Log(x0)) / (Math.Log(x1) - Math.Log(x0));
            return Math.Exp(Math.Log(y0) + f * (Math.Log(y1) - Math.Log(y0)));
        }

        return null;
    }

    private static double Cell(CalcRow row, string key, Unit unit) => row.Get(key) is { Number: { } n } cell ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : unit).ToSi(n) : 0;

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
