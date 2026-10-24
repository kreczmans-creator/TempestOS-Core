using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Electrical;

/// <summary>
/// E4 — conductor sizing and overload coordination for one circuit: the design current, the preferred device rating,
/// the smallest conductor whose tabulated capacity carries the rating once the correction factors are applied, and
/// the checks I_b ≤ I_n ≤ I_z and I₂ ≤ 1.45·I_z. No ampacity, rating factor or standard table is embedded: the
/// capacities, the device ratings and the factors are records the user enters from their own licensed copy. The only
/// built-in constants are formulas: the AWG geometry, the optional derived ambient factor and C_c = 0.725.
/// </summary>
public sealed class ConductorSizingCalculator : ICalcCalculator
{
    public const string Id = "elec.conductor-sizing";

    public const string ContextMachine = "BS EN 60204-1 machine wiring";
    public const string ContextInstallation = "BS 7671 installation";

    public const string ThreePhase = "three-phase";
    public const string SinglePhase = "single-phase";
    public const string Dc = "DC";

    public const string Pvc = "PVC 70 °C";
    public const string Xlpe = "XLPE 90 °C";
    public const string UserInsulation = "user";

    public const string McbB = "MCB type B";
    public const string McbC = "MCB type C";
    public const string McbD = "MCB type D";
    public const string GgFuse = "gG fuse";
    public const string SemiEnclosed = "semi-enclosed fuse";
    public const string Mccb = "MCCB";
    public const string Mpcb = "MPCB";

    public const string Metric = "metric";
    public const string Awg = "AWG";

    /// <summary>C_c for a semi-enclosed (rewirable) fuse: 1.45 divided by an assumed fusing factor of 2.</summary>
    public const double SemiEnclosedFactor = 0.725;

    private static readonly IReadOnlyList<CalcColumnSpec> RatingColumns =
    [
        new("I_n", "Rated current", CalcInputKind.Quantity, "A", Min: 0.1, Max: 10000, HardMin: 0.1, HardMax: 10000),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> AmpacityColumns =
    [
        new("CSA", "Cross-section (metric)", CalcInputKind.Quantity, "mm^2", Min: 0.1, Max: 1000, HardMin: 0.1, HardMax: 1000, IsOptional: true),
        new("AWG", "AWG gauge number n (0 = 1/0, −1 = 2/0, −2 = 3/0, −3 = 4/0)", CalcInputKind.Number, Min: -3, Max: 40, HardMin: -3, HardMax: 40, IsOptional: true),
        new("I_t", "Tabulated current-carrying capacity", CalcInputKind.Quantity, "A", Min: 0.1, Max: 5000, HardMin: 0.1, HardMax: 5000),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.1.0", CalcDiscipline.Electrical, "Conductor sizing and overload coordination",
        "One circuit: the design current, the smallest preferred device rating that carries it, the smallest conductor whose tabulated current-carrying capacity (your own record) carries that rating once the ambient, grouping, installation and semi-enclosed-fuse factors are applied, and the overload coordination checks I_b ≤ I_n ≤ I_z and I₂ ≤ 1.45 I_z. The context (machine wiring or installation) changes the clause citations and, for a gG fuse, MCCB or MPCB entered without I₂, whether the I₂ condition may be deemed satisfied (BS 7671 installation) or I₂ must be entered (machine wiring); every other number is the same in both contexts.",
        "Design current: I_b = P/(√3·U·cosφ·η) three-phase, P/(U·cosφ·η) single-phase, P/U DC; or the linked I_b\n" +
        "Device: I_n = smallest preferred rating in the ratings record with I_n ≥ I_b\n" +
        "Fuse factor: C_c = 0.725 for a semi-enclosed fuse (1.45/2, derived), else 1\n" +
        "Required tabulated capacity: I_t,req = I_n/(C_a·C_g·C_i·C_c); optional derived C_a = √[(θ_p − θ_a)/(θ_p − θ_ref)] (derived approximation — compare with the licensed table)\n" +
        "Conductor: CSA = smallest cross-section in the ampacity record with I_t ≥ I_t,req; AWG gauge n: d = 0.127·92^((36 − n)/39) mm, area = π d²/4 (derived)\n" +
        "Carrying capacity: I_z = I_t·C_a·C_g·C_i\n" +
        "Coordination: I_b ≤ I_n ≤ I_z and I₂ ≤ 1.45·I_z, I₂ = 1.45·I_n inherent for MCB types B, C, D; I₂ = 2·I_n by default for a semi-enclosed fuse; an entered I₂ (datasheet) is always checked; in the BS 7671 installation context a gG fuse to BS 88 (and an MCCB or MPCB entered without I₂, taken as I₂ ≤ 1.45·I_n) has the I₂ condition deemed satisfied when I_n ≤ I_z; in the machine-wiring context (IEC 60269-1 gG fuses have I₂ = 1.6·I_n, not 1.45·I_n) I₂ must be entered\n" +
        "Device code: 1 = MCB type B, 2 = type C, 3 = type D, 4 = fuse, MCCB or MPCB (carried to E5 so the fault check uses the device chosen here)",
        [
            new CalcInputSpec("context", "Design context (changes clause citations and the scope note only)", CalcInputKind.Choice, Choices: [ContextMachine, ContextInstallation], DefaultChoice: ContextMachine, Group: "Context"),
            new CalcInputSpec("supply", "Supply", CalcInputKind.Choice, Choices: [ThreePhase, SinglePhase, Dc], DefaultChoice: ThreePhase, Group: "Load"),
            new CalcInputSpec("P", "Rated output (shaft) power; η converts it to input (DC: electrical power drawn, η not applied)", CalcInputKind.Quantity, "kW", Default: 7.5, Min: 0.01, Max: 500, HardMin: 0.01, HardMax: 500, Group: "Load"),
            new CalcInputSpec("U", "Supply voltage (line voltage for three-phase)", CalcInputKind.Quantity, "V", Default: 400, Min: 12, Max: 1000, HardMin: 12, HardMax: 1000, Group: "Load"),
            new CalcInputSpec("cos_phi", "Power factor (not used for DC)", CalcInputKind.Number, Default: 0.85, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Load"),
            new CalcInputSpec("eta", "Efficiency (not used for DC)", CalcInputKind.Number, Default: 0.9, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Load"),
            new CalcInputSpec("I_b_in", "Design current from another calculation (replaces the computed I_b when given)", CalcInputKind.Quantity, "A", Min: 0.01, Max: 50000, HardMin: 0.01, HardMax: 50000, IsOptional: true, Group: "Load"),
            new CalcInputSpec("L", "Circuit length (carried to the voltage-drop calculation)", CalcInputKind.Quantity, "m", Default: 20, Min: 0.1, Max: 500, HardMin: 0.1, HardMax: 500, Group: "Cable"),
            new CalcInputSpec("theta_a", "Ambient temperature", CalcInputKind.Quantity, "degC", Default: 30, Min: -10, Max: 70, HardMin: -10, HardMax: 70, Group: "Cable"),
            new CalcInputSpec("insulation", "Insulation", CalcInputKind.Choice, Choices: [Pvc, Xlpe, UserInsulation], DefaultChoice: Pvc, Group: "Cable"),
            new CalcInputSpec("theta_p", "Conductor operating temperature for 'user' insulation", CalcInputKind.Quantity, "degC", Min: 50, Max: 250, HardMin: 50, HardMax: 250, IsOptional: true, Group: "Cable"),
            new CalcInputSpec("method", "Installation method key (descriptive only, e.g. B2, C, E)", CalcInputKind.Text, IsOptional: true, Group: "Cable"),
            new CalcInputSpec("N_g", "Grouped circuits", CalcInputKind.Number, Default: 1, Min: 1, Max: 20, HardMin: 1, HardMax: 20, Group: "Cable"),
            new CalcInputSpec("system", "Conductor system (AWG: geometry only, no AWG capacities are supplied)", CalcInputKind.Choice, Choices: [Metric, Awg], DefaultChoice: Metric, Group: "Cable"),
            new CalcInputSpec("device", "Protective device", CalcInputKind.Choice, Choices: [McbB, McbC, McbD, GgFuse, SemiEnclosed, Mccb, Mpcb], DefaultChoice: McbC, Group: "Device"),
            new CalcInputSpec("I2", "Conventional operating current I₂ (gG fuse, MCCB, MPCB: required in the machine-wiring context; a semi-enclosed fuse defaults to 2 I_n)", CalcInputKind.Quantity, "A", Min: 0.1, Max: 100000, HardMin: 0.1, HardMax: 100000, IsOptional: true, Group: "Device"),
            new CalcInputSpec("C_a", "Ambient correction factor", CalcInputKind.Number, Default: 1, Min: 0.1, Max: 1.5, HardMin: 0.1, HardMax: 1.5, Group: "Correction factors"),
            new CalcInputSpec("C_g", "Grouping correction factor", CalcInputKind.Number, Default: 1, Min: 0.1, Max: 1.5, HardMin: 0.1, HardMax: 1.5, Group: "Correction factors"),
            new CalcInputSpec("C_i", "Thermal insulation correction factor", CalcInputKind.Number, Default: 1, Min: 0.1, Max: 1.5, HardMin: 0.1, HardMax: 1.5, Group: "Correction factors"),
            new CalcInputSpec("derive_Ca", "Derive C_a from the temperatures instead of the entered factor", CalcInputKind.Boolean, Default: 0, Group: "Correction factors"),
            new CalcInputSpec("theta_ref", "Reference ambient temperature for the derived C_a", CalcInputKind.Quantity, "degC", Default: 30, Min: 0, Max: 60, HardMin: 0, HardMax: 60, Group: "Correction factors"),
            new CalcInputSpec("ratings", "Preferred device ratings", CalcInputKind.Table, Columns: RatingColumns, MaxRows: 60, IsOptional: true, Group: "Records"),
            new CalcInputSpec("ampacity", "Current-carrying capacities for the installation method", CalcInputKind.Table, Columns: AmpacityColumns, MaxRows: 60, IsOptional: true, Group: "Records"),
        ],
        [
            new CalcAssumption("Every tabulated value (the capacities, the device ratings, the correction factors) is entered from the user's own licensed copy or a manufacturer datasheet; none ships with the calculator.", "§A5."),
            new CalcAssumption("The capacities in the table are for the installation method stated; the method key is descriptive and is not interpreted.", "Records carry their source and edition."),
            new CalcAssumption("A gG fuse to BS 88 is deemed to meet I₂ ≤ 1.45·I_z when I_n ≤ I_z only under BS 7671 (installation context); an IEC 60269-1 gG fuse has I₂ = 1.6·I_n, so in the machine-wiring context I₂ is entered from the datasheet.", "BS 7671 reg. 433.1.201; IEC 60269-1."),
            new CalcAssumption("The ambient, grouping and insulation factors are applied as a product; C_g is not looked up from the number of grouped circuits.", "The count is carried to the sheet and warns when C_g = 1 with more than one circuit."),
            new CalcAssumption("The optional derived C_a is an approximation for the temperature difference only and is labelled as such; compare it with the licensed table.", "Derived, not tabulated."),
            new CalcAssumption("C_c = 0.725 applies to a semi-enclosed fuse only: 1.45 divided by an assumed fusing factor of 2.", "Derived."),
            new CalcAssumption("AWG areas follow the geometry d = 0.127·92^((36 − n)/39) mm; no AWG capacity is supplied.", "Derived."),
            new CalcAssumption("DC circuits use I_b = P/U; the power factor and efficiency are not applied.", "Method."),
        ],
        [
            new CalcClauseRef("BS 7671:2018 (as amended)", "reg. 433.1.1", "Co-ordination of conductors and overload protective devices"),
            new CalcClauseRef("BS 7671:2018 (as amended)", "reg. 523.1", "Current-carrying capacity of a cable"),
            new CalcClauseRef("BS EN 60204-1:2018", "cl. 12.4", "Current-carrying capacity of conductors and cables in normal service"),
            new CalcClauseRef("BS EN 60204-1:2018", "cl. 7.2", "Overcurrent protection"),
            new CalcClauseRef("BS EN 60898-1", "clause number to be checked", "Circuit-breakers for overcurrent protection: conventional tripping current"),
            new CalcClauseRef("BS 7671:2018 (as amended)", "reg. 433.1.201, 433.1.202", "Overload protection by gG fuses and semi-enclosed fuses: deemed conditions"),
        ],
        [
            new CalcApplicabilityRule("ampacity", "The ampacity record must hold at least one usable row.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("I_n", "A preferred rating of at least I_b must be in the ratings record.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("CSA", "A conductor in the ampacity record must carry I_t,req.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("I2", "For a gG fuse, MCCB or MPCB in the machine-wiring context, I₂ must be entered (blocked without it); in the BS 7671 installation context a gG fuse to BS 88 is deemed to meet I₂ ≤ 1.45 I_z when I_n ≤ I_z, and an MCCB or MPCB without I₂ is treated the same with a warning.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("C_g", "More than one grouped circuit with C_g = 1 is probably a missing grouping factor.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-three-phase-mcb-c", "Three-phase 7.5 kW (output) load on a type C MCB, θ_a 35 °C with C_a 0.91, two circuits with C_g 0.77",
                new CalcReferenceSource("Analytic (synthetic test values invented for these tests, not taken from any table)", null, "I_b = 7500/(√3·400·0.85·0.9) = 14.15074 A; I_n = 16 A; I_t,req = 16/(0.91·0.77) = 22.83431 A; the synthetic table (1.5→12.3, 2.5→17.7, 4→24.1, 6→31.9, 10→44.6 A) gives 4 mm² (24.1 A); I_z = 24.1·0.91·0.77 = 16.88687 A"),
                [Choice("supply", ThreePhase), Q("P", 7.5, "kW"), Q("U", 400, "V"), N("cos_phi", 0.85), N("eta", 0.9), Q("theta_a", 35, "degC"), N("N_g", 2), Choice("device", McbC), N("C_a", 0.91), N("C_g", 0.77), N("C_i", 1), TypicalRatings(), SyntheticAmpacity()],
                [new CalcExpectedValue("I_b", 14.15074189, "A", 1e-6), new CalcExpectedValue("I_n", 16, "A", 1e-9), new CalcExpectedValue("I_t_req", 22.83430855, "A", 1e-6), new CalcExpectedValue("CSA", 4, "mm^2", 1e-9), new CalcExpectedValue("I_z", 16.88687, "A", 1e-6), new CalcExpectedValue("u_Ib", 0.8844213682, "", 1e-6), new CalcExpectedValue("u_In", 0.9474816825, "", 1e-6)]),
            new CalcTestCase("ka-single-phase", "Single-phase 3 kW (output) load on a type B MCB",
                new CalcReferenceSource("Analytic (synthetic test values invented for these tests, not taken from any table)", null, "I_b = 3000/(230·0.95·0.9) = 15.25553 A; I_n = 16 A; factors 1 so I_t,req = 16 A; the synthetic table gives 2.5 mm² (17.7 A); I_z = 17.7 A"),
                [Choice("supply", SinglePhase), Q("P", 3, "kW"), Q("U", 230, "V"), N("cos_phi", 0.95), N("eta", 0.9), Choice("device", McbB), TypicalRatings(), SyntheticAmpacity()],
                [new CalcExpectedValue("I_b", 15.25553013, "A", 1e-6), new CalcExpectedValue("I_n", 16, "A", 1e-9), new CalcExpectedValue("I_t_req", 16, "A", 1e-9), new CalcExpectedValue("CSA", 2.5, "mm^2", 1e-9), new CalcExpectedValue("I_z", 17.7, "A", 1e-9), new CalcExpectedValue("u_In", 0.9039548023, "", 1e-6)]),
            new CalcTestCase("ka-dc", "DC 600 W load at 24 V on a gG fuse, C_a 0.91",
                new CalcReferenceSource("Analytic (synthetic test values invented for these tests, not taken from any table)", null, "I_b = 600/24 = 25 A; I_n = 25 A; I_t,req = 25/0.91 = 27.47253 A; the synthetic table gives 6 mm² (31.9 A); I_z = 31.9·0.91 = 29.029 A; machine-wiring context, so I₂ is entered from the (invented) datasheet value 40 A = 1.6·I_n: u_I2 = 40/(1.45·29.029) = 0.95030"),
                [Choice("supply", Dc), Q("P", 0.6, "kW"), Q("U", 24, "V"), Choice("device", GgFuse), Q("I2", 40, "A"), N("C_a", 0.91), TypicalRatings(), SyntheticAmpacity()],
                [new CalcExpectedValue("I_b", 25, "A", 1e-9), new CalcExpectedValue("I_n", 25, "A", 1e-9), new CalcExpectedValue("I_t_req", 27.47252747, "A", 1e-6), new CalcExpectedValue("CSA", 6, "mm^2", 1e-9), new CalcExpectedValue("I_z", 29.029, "A", 1e-9), new CalcExpectedValue("u_In", 0.8612077578, "", 1e-6), new CalcExpectedValue("u_I2", 0.9502982155, "", 1e-6)]),
            new CalcTestCase("ka-semi-enclosed", "Semi-enclosed fuse on a linked 18 A design current: C_c = 0.725",
                new CalcReferenceSource("Analytic (synthetic test values invented for these tests, not taken from any table)", null, "I_b = 18 A (linked, replaces P); I_n = 20 A; C_c = 0.725; I_t,req = 20/0.725 = 27.58621 A; the synthetic table gives 6 mm² (31.9 A); I_z = 31.9 A; I₂ = 2·I_n = 40 A, u_I2 = 40/(1.45·31.9) = 0.86477 (a gG fuse of the same rating would need only 4 mm²)"),
                [Q("I_b_in", 18, "A"), Choice("device", SemiEnclosed), TypicalRatings(), SyntheticAmpacity()],
                [new CalcExpectedValue("I_b", 18, "A", 1e-9), new CalcExpectedValue("I_n", 20, "A", 1e-9), new CalcExpectedValue("C_c", 0.725, "", 1e-9), new CalcExpectedValue("I_t_req", 27.5862069, "A", 1e-6), new CalcExpectedValue("CSA", 6, "mm^2", 1e-9), new CalcExpectedValue("I_z", 31.9, "A", 1e-9), new CalcExpectedValue("I2", 40, "A", 1e-9), new CalcExpectedValue("u_I2", 0.8647713761, "", 1e-6)]),
            new CalcTestCase("ka-derived-ca", "Derived ambient factor: PVC 70 °C, θ_ref 30 °C, θ_a 40 °C",
                new CalcReferenceSource("Analytic (the derived-approximation formula)", null, "C_a = √[(70 − 40)/(70 − 30)] = 0.866025; with C_g 0.77 and the 7.5 kW load: I_t,req = 16/(0.866025·0.77) = 23.99378 A, 4 mm² (24.1 A synthetic), I_z = 24.1·0.866025·0.77 = 16.07083 A"),
                [Choice("supply", ThreePhase), Q("P", 7.5, "kW"), Q("U", 400, "V"), N("cos_phi", 0.85), N("eta", 0.9), Choice("device", McbC), Choice("insulation", Pvc), Q("theta_a", 40, "degC"), CalcInputValue.Switch("derive_Ca", true), Q("theta_ref", 30, "degC"), N("N_g", 2), N("C_g", 0.77), TypicalRatings(), SyntheticAmpacity()],
                [new CalcExpectedValue("C_a", 0.8660254038, "", 1e-9), new CalcExpectedValue("I_t_req", 23.99377742, "A", 1e-6), new CalcExpectedValue("CSA", 4, "mm^2", 1e-9), new CalcExpectedValue("I_z", 16.07083342, "A", 1e-6)]),
            new CalcTestCase("ka-awg-10", "AWG geometry: AWG 10 is 5.2612 mm²",
                new CalcReferenceSource("Analytic (the AWG geometry formula)", null, "d = 0.127·92^((36 − 10)/39) = 2.588187 mm; area = π d²/4 = 5.261155 mm². Linked I_b 25 A, type C MCB, AWG rows 14, 12, 10, 8 with synthetic capacities 11.2, 16.9, 26.3, 37.4 A (invented for these tests, not taken from any table): I_n = 25 A, AWG 10 (26.3 A) is the smallest that carries it"),
                [Q("I_b_in", 25, "A"), Choice("device", McbC), Choice("system", Awg), TypicalRatings(), AwgAmpacity((14, 11.2), (12, 16.9), (10, 26.3), (8, 37.4))],
                [new CalcExpectedValue("CSA", 5.261154955, "mm^2", 1e-9), new CalcExpectedValue("I_n", 25, "A", 1e-9), new CalcExpectedValue("I_z", 26.3, "A", 1e-9)]),
        ],
        Category: "Power",
        OutputPorts:
        [
            new CalcOutputPort("I_b", "A", "Design current"),
            new CalcOutputPort("I_n", "A", "Rated current of the protective device"),
            new CalcOutputPort("CSA", "mm^2", "Selected conductor cross-section"),
            new CalcOutputPort("I_z", "A", "Current-carrying capacity of the selected conductor"),
            new CalcOutputPort("L", "m", "Circuit length"),
            new CalcOutputPort("device_code", "", "Protective device type code: 1 MCB B, 2 MCB C, 3 MCB D, 4 fuse, MCCB or MPCB (→ E5 device_code)"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("cable.ampacity", "Cable current-carrying capacities", ["ampacity"], "From your own licensed table for the installation method and insulation (cross-section against I_t); none ships with the calculator."),
            new CalcRecordKind("cable.correction-factors", "Cable correction factors", ["C_a", "C_g", "C_i"], "Ambient, grouping and thermal-insulation factors from your own licensed table for the installation method."),
            new CalcRecordKind("device.ratings", "Preferred device ratings", ["ratings"], "The preferred ratings of the device range (manufacturer datasheet or product standard)."),
        ],
        IndependentCheck: true);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue N(string symbol, double value) => CalcInputValue.Of(symbol, value);

    private static CalcInputValue Choice(string symbol, string choice) => CalcInputValue.Choice(symbol, choice);

    /// <summary>A ratings table from rated currents in A.</summary>
    public static CalcInputValue Ratings(params double[] amps) =>
        CalcInputValue.Table("ratings", amps.Select(a => CalcRow.Of(("I_n", new CalcCell(a, "A")))).ToList());

    /// <summary>A metric ampacity table from (CSA mm², I_t A) rows.</summary>
    public static CalcInputValue Ampacity(params (double Csa, double It)[] rows) =>
        CalcInputValue.Table("ampacity", rows.Select(r => CalcRow.Of(("CSA", new CalcCell(r.Csa, "mm^2")), ("I_t", new CalcCell(r.It, "A")))).ToList());

    /// <summary>An AWG ampacity table from (gauge n, I_t A) rows.</summary>
    public static CalcInputValue AwgAmpacity(params (double Gauge, double It)[] rows) =>
        CalcInputValue.Table("ampacity", rows.Select(r => CalcRow.Of(("AWG", new CalcCell(r.Gauge)), ("I_t", new CalcCell(r.It, "A")))).ToList());

    /// <summary>The typical preferred series 6 … 63 A, for the known-answer cases.</summary>
    public static CalcInputValue TypicalRatings() => Ratings(6, 10, 16, 20, 25, 32, 40, 50, 63);

    /// <summary>Capacities 1.5 → 12.3, 2.5 → 17.7, 4 → 24.1, 6 → 31.9, 10 → 44.6 A: synthetic test values invented for these tests, not taken from any table.</summary>
    public static CalcInputValue SyntheticAmpacity() => Ampacity((1.5, 12.3), (2.5, 17.7), (4, 24.1), (6, 31.9), (10, 44.6));

    /// <summary>The AWG area in mm²: d = 0.127·92^((36 − n)/39) mm, area = π d²/4 (derived).</summary>
    public static double AwgArea(double gauge)
    {
        var d = 0.127 * Math.Pow(92, (36 - gauge) / 39);
        return Math.PI * d * d / 4;
    }

    /// <summary>The conventional gauge name: 10 → "10", 0 → "1/0", −3 → "4/0".</summary>
    public static string AwgName(double gauge) => gauge > 0 ? CalcValue.FormatNumber(gauge, 4) : $"{CalcValue.FormatNumber(1 - gauge, 4)}/0";

    public void Run(CalcRunContext c)
    {
        var machine = c.InputText("context") == ContextMachine;
        var sizing = machine ? "BS EN 60204-1 cl. 12.4" : "BS 7671 reg. 523.1";
        var protection = machine ? "BS EN 60204-1 cl. 7.2" : "BS 7671 reg. 433.1.1";
        var coordination = machine ? "BS EN 60204-1 cl. 7.2; BS 7671 reg. 433.1.1" : "BS 7671 reg. 433.1.1";

        c.Assume(
            machine
                ? "Machine wiring context (BS EN 60204-1): the conductor is sized for the protective device rating; the capacities and factors are the user's records for the installation method stated."
                : "Installation context (BS 7671): the conductor is sized for the protective device rating; the capacities and factors are the user's records for the installation method stated.",
            "The context changes citations, and whether a gG fuse, MCCB or MPCB without I₂ may be deemed to meet I₂ ≤ 1.45·I_z (installation) or must have I₂ entered (machine wiring); no other number differs.");

        var supply = c.InputText("supply");
        var device = c.InputText("device");
        var awgMode = c.InputText("system") == Awg;
        c.Input("L");
        c.Input("theta_a");
        c.InputText("insulation");
        c.Input("N_g");

        if (c.Has("method"))
            c.Input("method");

        // Design current.
        if (c.Has("I_b_in"))
        {
            c.Input("I_b_in");
            c.Assign("I_b", "I_b_in", "A", "Design current (linked, replaces the computed value)");
        }
        else
        {
            c.Input("P");
            c.Input("U");

            if (supply == Dc)
            {
                c.Assign("I_b", "P / U", "A", "Design current (DC)");
            }
            else
            {
                c.Input("cos_phi");
                c.Input("eta");
                c.Assign("I_b", supply == ThreePhase ? "P / (sqrt(3) * U * cos_phi * eta)" : "P / (U * cos_phi * eta)", "A", supply == ThreePhase ? "Design current (three-phase)" : "Design current (single-phase)");
            }
        }

        var amp = UnitCatalog.Parse("A");
        var mm2 = UnitCatalog.Parse("mm^2");

        var table = ReadAmpacity(c, awgMode, amp, mm2, out var ignored, out var fractional);

        if (fractional)
        {
            c.Block("ampacity", "ampacity: AWG gauge must be a whole number.");
            return;
        }

        if (table.Count == 0)
        {
            // Rows that are all in the other conductor system: say so rather than that the table is empty.
            var given = c.Rows("ampacity");
            var hasCsa = given.Any(r => r.Get("CSA") is { Number: > 0 });
            var hasGauge = given.Any(r => r.Get("AWG") is { Number: not null });

            if (awgMode && hasCsa && !hasGauge)
                c.Block("ampacity", "ampacity: the rows give cross-sections (metric) but the conductor system is AWG; enter AWG gauge numbers or switch the system to metric.");
            else if (!awgMode && hasGauge && !hasCsa)
                c.Block("ampacity", "ampacity: the rows give AWG gauge numbers but the conductor system is metric; enter cross-sections or switch the system to AWG.");
            else
                c.Block("ampacity", "ampacity: enter the current-carrying capacities for the installation method from your own licensed table (record kind cable.ampacity).");

            return;
        }

        if (ignored > 0)
            c.Warn("ampacity", $"ampacity: {ignored} row(s) without the {(awgMode ? "AWG gauge" : "cross-section")} for the {(awgMode ? "AWG" : "metric")} system were ignored.");

        var ratings = c.Rows("ratings").Select(r => Cell(r, "I_n", amp)).Where(x => x > 0).Distinct().Order().ToList();

        if (ratings.Count == 0)
        {
            c.Block("ratings", "ratings: enter the preferred device ratings (record kind device.ratings).");
            return;
        }

        var ib = c.Si("I_b");
        var rated = ratings.Where(r => r >= ib * (1 - 1e-9)).ToList();

        if (rated.Count == 0)
        {
            c.Block("I_n", $"I_n: no preferred rating of at least I_b = {F(ib)} A is in the ratings record (largest entered: {F(ratings[^1])} A).");
            return;
        }

        c.Record("I_n", "smallest preferred rating with I_n ≥ I_b", $"smallest rating ≥ {F(ib)} A in the ratings record", ScalarValue.Of(rated[0], amp), "Rated current of the protective device (ratings record)", protection);
        c.Record("device_code", "1 = MCB type B, 2 = type C, 3 = type D, 4 = fuse, MCCB or MPCB", device, new ScalarValue(DeviceCode(device)), "Protective device type code (carried to E5 so the fault check uses this device)", protection);

        var semi = device == SemiEnclosed;
        c.Record("C_c", semi ? "0.725 for a semi-enclosed fuse (1.45/2, derived)" : "1 (not a semi-enclosed fuse)", device, new ScalarValue(semi ? SemiEnclosedFactor : 1), "Semi-enclosed fuse factor", sizing);

        // Ambient factor: entered, or derived from the temperatures.
        if (c.InputFlag("derive_Ca"))
        {
            c.Input("theta_ref");
            var insulation = c.InputText("insulation");
            double thetaP;

            if (insulation == UserInsulation)
            {
                if (!c.Has("theta_p"))
                {
                    c.Block("theta_p", "theta_p: enter the conductor operating temperature for 'user' insulation to derive C_a.");
                    return;
                }

                thetaP = c.InputSi("theta_p") - 273.15;
            }
            else
            {
                thetaP = insulation == Pvc ? 70 : 90;
            }

            var ta = c.Si("theta_a") - 273.15;
            var tref = c.Si("theta_ref") - 273.15;

            if (!(thetaP > ta) || !(thetaP > tref))
            {
                c.Block("theta_a", $"theta_a: the ambient ({F(ta)} °C) and the reference ({F(tref)} °C) must both be below the conductor temperature ({F(thetaP)} °C) to derive C_a.");
                return;
            }

            var ca = Math.Sqrt((thetaP - ta) / (thetaP - tref));

            if (ca is < 0.1 or > 1.5)
            {
                c.Block("C_a", $"C_a: the derived factor {F(ca)} is outside 0.1 … 1.5; enter the factor from your licensed table instead.");
                return;
            }

            c.Record("C_a", "√[(θ_p − θ_a)/(θ_p − θ_ref)] (derived approximation — compare with the licensed table)", $"√[({F(thetaP)} − {F(ta)})/({F(thetaP)} − {F(tref)})]", new ScalarValue(ca), "Ambient factor, derived approximation — compare with the licensed table", sizing);
            c.Warn("C_a", "C_a: derived approximation — compare with the licensed table.");
        }
        else
        {
            c.Input("C_a");
        }

        c.Input("C_g");
        c.Input("C_i");

        if (c.Si("N_g") > 1 && c.Si("C_g") == 1)
            c.Warn("C_g", $"C_g: {F(c.Si("N_g"))} circuits are grouped but C_g = 1; enter the grouping factor from your licensed table.");

        c.Assign("I_t_req", "I_n / (C_a * C_g * C_i * C_c)", "A", "Required tabulated capacity", sizing);

        var req = c.Si("I_t_req");
        var carrying = table.Where(t => t.It >= req * (1 - 1e-9)).OrderBy(t => t.Area).ThenByDescending(t => t.It).ToList();

        if (carrying.Count == 0)
        {
            c.Block("CSA", $"CSA: no conductor in the ampacity record carries I_t,req = {F(req)} A (largest entered: {F(table.Max(t => t.It))} A).");
            return;
        }

        var pick = carrying[0];
        var formula = awgMode ? "AWG gauge n: area = π/4·[0.127·92^((36 − n)/39)]² (derived); smallest with I_t ≥ I_t,req" : "smallest cross-section in the ampacity record with I_t ≥ I_t,req";
        c.Record("I_t", "I_t of the selected row of the ampacity record", $"I_t,req = {F(req)} A", ScalarValue.Of(pick.It, amp), "Tabulated current-carrying capacity of the selected conductor", sizing);
        c.Record("CSA", formula, awgMode ? $"n = {AwgName(pick.Gauge)}" : $"I_t,req = {F(req)} A", ScalarValue.Of(pick.Area, mm2), "Selected conductor cross-section", sizing);
        c.Assign("I_z", "I_t * C_a * C_g * C_i", "A", "Current-carrying capacity in the installed conditions", sizing);
        c.Assign("u_Ib", "I_b / I_n", null, "Design current utilisation of the device", coordination);
        c.Assign("u_In", "I_n / I_z", null, "Device rating utilisation of the conductor", coordination);

        var outputs = new List<(string, string)> { ("I_b", "Design current"), ("I_n", "Rated current of the device"), ("C_c", "Semi-enclosed fuse factor"), ("C_a", "Ambient factor"), ("I_t_req", "Required tabulated capacity"), ("CSA", "Selected cross-section"), ("I_z", "Current-carrying capacity"), ("L", "Circuit length"), ("u_Ib", "I_b / I_n"), ("u_In", "I_n / I_z") };

        // I₂: inherent 1.45·I_n for MCBs; 2·I_n by default for a semi-enclosed fuse; an entered I₂ is always checked. A gG fuse (to BS 88),
        // an MCCB or an MPCB without I₂ is deemed to meet I₂ ≤ 1.45·I_z when I_n ≤ I_z only in the BS 7671 installation context: an
        // IEC 60269-1 gG fuse has I₂ = 1.6·I_n, so in the machine-wiring context I₂ must be entered (Green G-B-02, G-B-13).
        var mcb = device is McbB or McbC or McbD;
        var gg = device == GgFuse;
        var haveI2 = false;
        var deemed = false;

        if (mcb)
        {
            c.Assign("I2", "1.45 * I_n", "A", "Conventional operating current (inherent 1.45 I_n)", coordination);
            haveI2 = true;
        }
        else if (c.Has("I2"))
        {
            c.Input("I2");
            haveI2 = true;
        }
        else if (device == SemiEnclosed)
        {
            c.Assign("I2", "2 * I_n", "A", "Conventional operating current of a semi-enclosed fuse (default 2 I_n)", "BS 7671 reg. 433.1.202");
            haveI2 = true;
        }
        else if (machine)
        {
            c.Block("I2", $"I2: enter the conventional operating current of the {device} from its datasheet; outside the BS 7671 installation context the I₂ ≤ 1.45·I_z condition cannot be deemed satisfied (an IEC 60269-1 gG fuse has I₂ = 1.6·I_n).");
            return;
        }
        else
        {
            deemed = true;

            if (!gg)
                c.Warn("I2", $"I2: the conventional operating current of the {device} is not entered; I₂ ≤ 1.45·I_z is deemed satisfied when I_n ≤ I_z only if the device's I₂ is not above 1.45·I_n. Enter I₂ from the datasheet to check it.");
        }

        if (haveI2)
        {
            c.Assign("u_I2", "I2 / (1.45 * I_z)", null, "I₂ utilisation of 1.45 I_z", coordination);
            outputs.AddRange([("I2", "Conventional operating current"), ("u_I2", "I₂ / (1.45 I_z)")]);
        }

        outputs.Add(("device_code", "Protective device type code"));

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        if (awgMode)
            c.Output("gauge", "Selected AWG gauge", new TextValue("AWG " + AwgName(pick.Gauge)));

        c.CheckUtilisation("Design current within device rating (I_b ≤ I_n)", "u_Ib", coordination);
        c.CheckUtilisation("Device rating within conductor capacity (I_n ≤ I_z)", "u_In", coordination);

        if (haveI2)
            c.CheckUtilisation("Conventional operating current within 1.45·I_z (I₂ ≤ 1.45·I_z)", "u_I2", device == SemiEnclosed ? "BS 7671 reg. 433.1.202" : coordination);
        else if (deemed)
            c.Check(gg ? "I₂ ≤ 1.45·I_z deemed satisfied for a gG fuse to BS 88 when I_n ≤ I_z" : "I₂ ≤ 1.45·I_z deemed satisfied when I_n ≤ I_z (I₂ taken as not above 1.45·I_n)", "I_n", "I_z", gg ? "BS 7671 reg. 433.1.201" : coordination);
    }

    /// <summary>The device type code carried to E5: 1 = MCB type B, 2 = type C, 3 = type D, 4 = any other device (fuse, MCCB, MPCB: time–current points).</summary>
    public static int DeviceCode(string device) => device switch { McbB => 1, McbC => 2, McbD => 3, _ => 4 };

    private static List<(double Area, double It, double Gauge)> ReadAmpacity(CalcRunContext c, bool awgMode, Unit amp, Unit mm2, out int ignored, out bool fractional)
    {
        var list = new List<(double, double, double)>();
        ignored = 0;
        fractional = false;

        foreach (var row in c.Rows("ampacity"))
        {
            var it = Cell(row, "I_t", amp);

            if (awgMode)
            {
                if (row.Get("AWG") is { Number: { } n } && it > 0)
                {
                    fractional |= n != Math.Round(n);
                    list.Add((AwgArea(n), it, n));
                }
                else
                    ignored++;
            }
            else
            {
                var area = mm2.FromSi(Cell(row, "CSA", mm2));

                if (area > 0 && it > 0)
                    list.Add((area, it, double.NaN));
                else
                    ignored++;
            }
        }

        return list;
    }

    /// <summary>The SI value of a table cell (0 when absent); a cell without a unit is read in <paramref name="unit"/>.</summary>
    private static double Cell(CalcRow row, string key, Unit unit) => row.Get(key) is { Number: { } n } cell ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : unit).ToSi(n) : 0;

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
