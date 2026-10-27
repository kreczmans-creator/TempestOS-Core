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
        Id, "1.4.0", CalcDiscipline.Electrical, "Conductor sizing and overload coordination",
        "Calculates the design current, the smallest preferred device rating and the smallest conductor whose tabulated capacity carries that rating. It checks I_b ≤ I_n ≤ I_z and I₂ ≤ 1.45 I_z, using your own capacity and rating records.",
        "Design current: I_b = P/(√3·U·cosφ·η) three-phase, P/(U·cosφ·η) single-phase, P/U DC; or the linked I_b\n" +
        "Protective device rating: I_n = smallest preferred rating in the ratings record with I_n ≥ I_b\n" +
        "Fuse factor: C_c = 0.725 for a semi-enclosed fuse (1.45/2, derived), else 1\n" +
        "Required tabulated capacity: I_t,req = I_n/(C_a·C_g·C_i·C_c); optional derived C_a = √[(θ_p − θ_a)/(θ_p − θ_ref)] (derived approximation — compare with the licensed table)\n" +
        "Conductor cross-section: CSA = smallest cross-section in the ampacity record with I_t ≥ I_t,req; AWG gauge n: d = 0.127·92^((36 − n)/39) mm, area = π d²/4 (derived)\n" +
        "Carrying capacity: I_z = I_t·C_a·C_g·C_i\n" +
        "Device and conductor coordination: I_b ≤ I_n ≤ I_z and I₂ ≤ 1.45·I_z, I₂ = 1.45·I_n inherent for MCB types B, C, D; I₂ = 2·I_n by default for a semi-enclosed fuse; an entered I₂ (datasheet) is always checked; in the BS 7671 installation context a gG fuse to BS 88 (and an MCCB or MPCB entered without I₂, taken as I₂ ≤ 1.45·I_n) has the I₂ condition deemed satisfied when I_n ≤ I_z; in the machine-wiring context (IEC 60269-1 gG fuses have I₂ = 1.6·I_n, not 1.45·I_n) I₂ must be entered\n" +
        "Device code: 1 = MCB type B, 2 = type C, 3 = type D, 4 = fuse, MCCB or MPCB (carried to fault-protection so the fault check uses the device chosen here)",
        [
            new CalcInputSpec("context", "Design context (changes clause citations and the scope note only)", CalcInputKind.Choice, Choices: [ContextMachine, ContextInstallation], DefaultChoice: ContextMachine, Group: "Setup"),
            new CalcInputSpec("supply", "Supply", CalcInputKind.Choice, Choices: [ThreePhase, SinglePhase, Dc], DefaultChoice: ThreePhase, Group: "Setup"),
            new CalcInputSpec("device", "Protective device", CalcInputKind.Choice, Choices: [McbB, McbC, McbD, GgFuse, SemiEnclosed, Mccb, Mpcb], DefaultChoice: McbC, Group: "Setup"),
            new CalcInputSpec("insulation", "Insulation", CalcInputKind.Choice, Choices: [Pvc, Xlpe, UserInsulation], DefaultChoice: Pvc, Group: "Setup"),
            new CalcInputSpec("system", "Conductor system (AWG: geometry only, no AWG capacities are supplied)", CalcInputKind.Choice, Choices: [Metric, Awg], DefaultChoice: Metric, Group: "Setup"),
            new CalcInputSpec("method", "Installation method key (descriptive only, e.g. B2, C, E)", CalcInputKind.Text, IsOptional: true, Group: "Setup"),
            new CalcInputSpec("ratings", "Preferred device ratings", CalcInputKind.Table, Columns: RatingColumns, MaxRows: 60, IsOptional: true, Group: "Setup"),
            new CalcInputSpec("ampacity", "Current-carrying capacities for the installation method", CalcInputKind.Table, Columns: AmpacityColumns, MaxRows: 60, IsOptional: true, Group: "Setup"),
            new CalcInputSpec("L", "Circuit length (carried to the voltage-drop calculation)", CalcInputKind.Quantity, "m", Default: 20, Min: 1, Max: 300, HardMin: 0.1, HardMax: 500, Group: "Geometry"),
            new CalcInputSpec("T_user", "Conductor operating temperature for 'user' insulation", CalcInputKind.Quantity, "degC", Min: 60, Max: 180, HardMin: 50, HardMax: 250, IsOptional: true, Group: "Material", Aliases: ["theta_p"]),
            new CalcInputSpec("P", "Rated output (shaft) power; η converts it to input (DC: electrical power drawn, η not applied)", CalcInputKind.Quantity, "kW", Default: 7.5, Min: 0.1, Max: 200, HardMin: 0.01, HardMax: 500, Group: "Loads"),
            new CalcInputSpec("U", "Supply voltage (line voltage for three-phase)", CalcInputKind.Quantity, "V", Default: 400, Min: 12, Max: 690, HardMin: 12, HardMax: 1000, Group: "Loads"),
            new CalcInputSpec("cos_phi", "Power factor (not used for DC)", CalcInputKind.Number, Default: 0.85, Min: 0.6, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Loads"),
            new CalcInputSpec("eta", "Efficiency (not used for DC)", CalcInputKind.Number, Default: 0.9, Min: 0.6, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Loads"),
            new CalcInputSpec("I_b_in", "Design current from another calculation (replaces the computed I_b when given)", CalcInputKind.Quantity, "A", Min: 0.1, Max: 10000, HardMin: 0.01, HardMax: 50000, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("T_amb", "Ambient temperature", CalcInputKind.Quantity, "degC", Default: 30, Min: 0, Max: 60, HardMin: -10, HardMax: 70, Group: "Conditions", Aliases: ["theta_a"]),
            new CalcInputSpec("T_ref", "Reference ambient temperature for the derived C_a", CalcInputKind.Quantity, "degC", Default: 30, Min: 20, Max: 40, HardMin: 0, HardMax: 60, Group: "Conditions", Aliases: ["theta_ref"]),
            new CalcInputSpec("N_g", "Grouped circuits", CalcInputKind.Number, Default: 1, Min: 1, Max: 10, HardMin: 1, HardMax: 20, Group: "Factors"),
            new CalcInputSpec("C_a", "Ambient correction factor (1 does not apply the ambient: enter the factor for T_amb from your licensed table)", CalcInputKind.Number, Default: 1, Min: 0.5, Max: 1.3, HardMin: 0.1, HardMax: 1.5, Group: "Factors"),
            new CalcInputSpec("C_g", "Grouping correction factor (at most 1)", CalcInputKind.Number, Default: 1, Min: 0.3, Max: 1, HardMin: 0.1, HardMax: 1, Group: "Factors"),
            new CalcInputSpec("C_i", "Thermal insulation correction factor (at most 1)", CalcInputKind.Number, Default: 1, Min: 0.3, Max: 1, HardMin: 0.1, HardMax: 1, Group: "Factors"),
            new CalcInputSpec("derive_Ca", "Derive C_a from the temperatures instead of the entered factor", CalcInputKind.Boolean, Default: 0, Group: "Factors"),
            new CalcInputSpec("I2", "Conventional operating current I₂ (gG fuse, MCCB, MPCB: required in the machine-wiring context; a semi-enclosed fuse defaults to 2 I_n)", CalcInputKind.Quantity, "A", Min: 1, Max: 10000, HardMin: 0.1, HardMax: 100000, IsOptional: true, Group: "Device"),
        ],
        [
            new CalcAssumption("Every tabulated value is entered from the user's own licensed copy or a manufacturer datasheet.", "No table from a standard or a manufacturer is shipped."),
            new CalcAssumption("No table from a standard or a manufacturer ships with the calculator.", "No table from a standard or a manufacturer is shipped."),
            new CalcAssumption("The capacities in the table are for the installation method stated.", "Records carry their source and edition."),
            new CalcAssumption("The method key is descriptive and is not interpreted.", "Records carry their source and edition."),
            new CalcAssumption("A gG fuse is deemed to meet I₂ ≤ 1.45·I_z when I_n ≤ I_z only under BS 7671.", "BS 7671 reg. 433.1.201; IEC 60269-1."),
            new CalcAssumption("An IEC 60269-1 gG fuse has I₂ = 1.6·I_n, so in the machine-wiring context I₂ is entered from the datasheet.", "BS 7671 reg. 433.1.201; IEC 60269-1."),
            new CalcAssumption("The ambient, grouping and insulation factors are applied as a product.", "The count is carried to the sheet and warns when C_g = 1 with more than one circuit."),
            new CalcAssumption("C_g is not looked up from the number of grouped circuits.", "The count is carried to the sheet and warns when C_g = 1 with more than one circuit."),
            new CalcAssumption("The optional derived C_a is an approximation for the temperature difference only.", "Derived, not tabulated."),
            new CalcAssumption("The derived C_a is labelled as an approximation, so compare it with the licensed table.", "Derived, not tabulated."),
            new CalcAssumption("C_c = 0.725 applies to a semi-enclosed fuse only: 1.45 divided by an assumed fusing factor of 2.", "Derived."),
            new CalcAssumption("AWG areas follow the geometry d = 0.127·92^((36 − n)/39) mm.", "Derived."),
            new CalcAssumption("No AWG capacity is supplied.", "Derived."),
            new CalcAssumption("DC circuits use I_b = P/U.", "Method."),
            new CalcAssumption("The power factor and the efficiency are not applied to a DC circuit.", "Method."),
        ],
        [
            new CalcClauseRef("BS 7671:2018", "reg. 433.1.1", "Co-ordination of conductors and overload protective devices"),
            new CalcClauseRef("BS 7671:2018", "reg. 523.1", "Current-carrying capacity of a cable"),
            new CalcClauseRef("BS EN 60204-1:2018", "cl. 12.4", "Current-carrying capacity of conductors and cables in normal service"),
            new CalcClauseRef("BS EN 60204-1:2018", "cl. 7.2", "Overcurrent protection"),
            new CalcClauseRef("BS EN 60898-1:2019", "cl. (number to be checked)", "Circuit-breakers for overcurrent protection: conventional tripping current"),
            new CalcClauseRef("BS 7671:2018", "reg. 433.1.201, 433.1.202", "Overload protection by gG fuses and semi-enclosed fuses: deemed conditions"),
        ],
        [
            new CalcApplicabilityRule("ampacity", "The ampacity record must hold at least one usable row.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("ampacity-ignored", "A row without the cross-section or gauge of the conductor system is ignored.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("ratings", "The ratings record must hold at least one rating.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("theta_p", "The conductor operating temperature must be entered for 'user' insulation to derive C_a.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("derive-Ca", "The ambient and the reference ambient must both be below the conductor temperature to derive C_a.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("C_a-range", "The derived C_a must lie between 0.1 and 1.5.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("C_a", "The ambient factor is derived from the temperatures: an approximation to compare with the licensed table.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("I2-deemed", "The conventional operating current of the device is not entered, and the condition is deemed satisfied when I_n ≤ I_z.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("I_n", "A preferred rating of at least I_b must be in the ratings record.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("CSA", "A conductor in the ampacity record must carry I_t,req.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("I2", "For a gG fuse, MCCB or MPCB in the machine-wiring context, I₂ must be entered (blocked without it); in the BS 7671 installation context a gG fuse to BS 88 is deemed to meet I₂ ≤ 1.45 I_z when I_n ≤ I_z, and an MCCB or MPCB without I₂ is treated the same with a warning.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("C_g", "More than one grouped circuit with C_g = 1 is probably a missing grouping factor.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("theta_a", "An ambient temperature above the reference with C_a left at 1: the ambient has not reached the sizing.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("ampacity-sense", "A cross-section entered twice (the lower capacity is used) or a capacity that falls as the cross-section grows is probably a typing slip or a mix of installation methods.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("theta_a-limit", "An ambient temperature at or above the conductor operating temperature leaves no current-carrying capacity.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-three-phase-mcb-c", "Three-phase 7.5 kW (output) load on a type C MCB, θ_a 35 °C with C_a 0.91, two circuits with C_g 0.77",
                new CalcReferenceSource("Analytic (synthetic test values invented for these tests, not taken from any table)", null, "I_b = 7500/(√3·400·0.85·0.9) = 14.15074 A; I_n = 16 A; I_t,req = 16/(0.91·0.77) = 22.83431 A; the synthetic table (1.5→12.3, 2.5→17.7, 4→24.1, 6→31.9, 10→44.6 A) gives 4 mm² (24.1 A); I_z = 24.1·0.91·0.77 = 16.88687 A"),
                [Choice("supply", ThreePhase), Q("P", 7.5, "kW"), Q("U", 400, "V"), N("cos_phi", 0.85), N("eta", 0.9), Q("T_amb", 35, "degC"), N("N_g", 2), Choice("device", McbC), N("C_a", 0.91), N("C_g", 0.77), N("C_i", 1), TypicalRatings(), SyntheticAmpacity()],
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
                [Choice("supply", ThreePhase), Q("P", 7.5, "kW"), Q("U", 400, "V"), N("cos_phi", 0.85), N("eta", 0.9), Choice("device", McbC), Choice("insulation", Pvc), Q("T_amb", 40, "degC"), CalcInputValue.Switch("derive_Ca", true), Q("T_ref", 30, "degC"), N("N_g", 2), N("C_g", 0.77), TypicalRatings(), SyntheticAmpacity()],
                [new CalcExpectedValue("C_a", 0.8660254038, "", 1e-9), new CalcExpectedValue("I_t_req", 23.99377742, "A", 1e-6), new CalcExpectedValue("CSA", 4, "mm^2", 1e-9), new CalcExpectedValue("I_z", 16.07083342, "A", 1e-6)]),
            new CalcTestCase("ka-awg-10", "AWG geometry: AWG 10 is 5.2612 mm²",
                new CalcReferenceSource("Analytic (the AWG geometry formula)", null, "d = 0.127·92^((36 − 10)/39) = 2.588187 mm; area = π d²/4 = 5.261155 mm². Linked I_b 25 A, type C MCB, AWG rows 14, 12, 10, 8 with synthetic capacities 11.2, 16.9, 26.3, 37.4 A (invented for these tests, not taken from any table): I_n = 25 A, AWG 10 (26.3 A) is the smallest that carries it"),
                [Q("I_b_in", 25, "A"), Choice("device", McbC), Choice("system", Awg), TypicalRatings(), AwgAmpacity((14, 11.2), (12, 16.9), (10, 26.3), (8, 37.4))],
                [new CalcExpectedValue("CSA", 5.261154955, "mm^2", 1e-9), new CalcExpectedValue("I_n", 25, "A", 1e-9), new CalcExpectedValue("I_z", 26.3, "A", 1e-9)]),
        ],
        Category: "Power",
        OutputPorts:
        [
            new CalcOutputPort("I_b", "A", "Design current", "conductor.current_design"),
            new CalcOutputPort("I_n", "A", "Rated current of the protective device", "conductor.rating_device"),
            new CalcOutputPort("CSA", "mm^2", "Selected conductor cross-section", "conductor.area_selected"),
            new CalcOutputPort("I_z", "A", "Current-carrying capacity of the selected conductor", "conductor.capacity_current"),
            new CalcOutputPort("L", "m", "Circuit length", "conductor.length_circuit"),
            new CalcOutputPort("T_op", "degC", "Conductor operating temperature for the insulation (70 PVC, 90 XLPE, the entered T_user for 'user'; → fault-protection T_op, voltage-drop T_c)", "conductor.temperature_operating", ["theta_op"]),
            new CalcOutputPort("device_code", "", "Protective device type code: 1 MCB B, 2 MCB C, 3 MCB D, 4 fuse, MCCB or MPCB (→ fault-protection device_code)", "conductor.code_device"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("cable.ampacity", "Cable ampacity (current-carrying capacities)", ["ampacity"], "From your own licensed table for the installation method and insulation (cross-section against I_t); none ships with the calculator."),
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
        c.MarkUtilisation("u_I2", "u_Ib", "u_In");
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
        c.Input("T_amb");
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
                c.Block("ampacity", "ampacity: the rows hold cross-sections but the conductor system is AWG. Enter AWG gauge numbers, or switch the system to metric.");
            else if (!awgMode && hasGauge && !hasCsa)
                c.Block("ampacity", "ampacity: the rows hold AWG gauge numbers but the conductor system is metric. Enter cross-sections, or switch the system to AWG.");
            else
                c.Block("ampacity", "ampacity: enter the current-carrying capacities for the installation method from your own licensed table (record kind cable.ampacity).");

            return;
        }

        if (ignored > 0)
            c.Warn("ampacity-ignored", $"ampacity: {ignored} row(s) without the {(awgMode ? "AWG gauge" : "cross-section")} for the {(awgMode ? "AWG" : "metric")} system were ignored.");

        table = CheckTable(c, table, awgMode);

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
            c.Input("T_ref");
            var insulation = c.InputText("insulation");
            double thetaP;

            if (insulation == UserInsulation)
            {
                if (!c.Has("T_user"))
                {
                    c.Block("theta_p", "T_user: enter the conductor operating temperature for 'user' insulation to derive C_a.");
                    return;
                }

                thetaP = c.InputSi("T_user") - 273.15;
            }
            else
            {
                thetaP = insulation == Pvc ? 70 : 90;
            }

            var ta = c.Si("T_amb") - 273.15;
            var tref = c.Si("T_ref") - 273.15;

            if (!(thetaP > ta) || !(thetaP > tref))
            {
                c.Block("derive-Ca", $"T_amb: the ambient ({F(ta)} °C) and the reference ({F(tref)} °C) must be below the conductor temperature ({F(thetaP)} °C).");
                return;
            }

            var ca = Math.Sqrt((thetaP - ta) / (thetaP - tref));

            if (ca is < 0.1 or > 1.5)
            {
                c.Block("C_a-range", $"C_a: the derived factor {F(ca)} is outside 0.1 to 1.5. Enter the factor from your licensed table instead.");
                return;
            }

            c.Record("C_a", "√[(θ_p − θ_a)/(θ_p − θ_ref)] (derived approximation — compare with the licensed table)", $"√[({F(thetaP)} − {F(ta)})/({F(thetaP)} − {F(tref)})]", new ScalarValue(ca), "Ambient factor, derived approximation — compare with the licensed table", sizing);
            c.Warn("C_a", $"C_a: derived approximation, so compare it with the licensed table. It uses the reference ambient T_ref of {F(tref)} °C, which must be the reference ambient of your capacity table.");
        }
        else
        {
            c.Input("C_a");

            // The ambient temperature reaches the sizing only through C_a (elec.conductor-sizing-01): an ambient above the table's reference with
            // C_a left at 1 sizes the conductor as if the ambient were the reference, so say so; an ambient at or above the conductor's own
            // operating temperature leaves it no capacity at all.
            var thetaPLimit = c.InputText("insulation") switch
            {
                Pvc => 70.0,
                Xlpe => 90.0,
                _ => c.Has("T_user") ? c.InputSi("T_user") - 273.15 : double.NaN,
            };
            var ambient = c.Si("T_amb") - 273.15;
            var reference = c.InputSi("T_ref") - 273.15;

            if (!double.IsNaN(thetaPLimit) && ambient >= thetaPLimit)
            {
                c.Block("theta_a-limit", $"T_amb: the ambient ({F(ambient)} °C) is at or above the conductor operating temperature ({F(thetaPLimit)} °C). The cable has no capacity left, so the sizing is refused.");
                return;
            }

            if (Math.Abs(c.Si("C_a") - 1) < 1e-12 && ambient > reference + 0.5)
                c.Warn("theta_a", $"T_amb: the ambient {F(ambient)} °C is above the reference {F(reference)} °C, but C_a is 1. The conductor is sized as if the ambient were the reference, so enter C_a or tick derive_Ca.");
        }

        // The conductor's operating temperature for its insulation (chain.motor-circuit-R1): the loop-impedance and voltage-drop steps take their resistance at it.
        var operating = c.InputText("insulation") switch
        {
            Pvc => 70.0,
            Xlpe => 90.0,
            _ => c.Has("T_user") ? c.InputSi("T_user") - 273.15 : double.NaN,
        };

        if (!double.IsNaN(operating))
            c.Assign("T_op", c.InputText("insulation") switch { Pvc => "70 degC", Xlpe => "90 degC", _ => "T_user" }, "degC", "Conductor operating temperature for the insulation");

        c.Input("C_g");
        c.Input("C_i");

        if (c.Si("N_g") > 1 && c.Si("C_g") == 1)
            c.Warn("C_g", $"C_g: {F(c.Si("N_g"))} circuits are grouped but C_g = 1. Enter the grouping factor from your licensed table.");

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

        if (!double.IsNaN(operating))
            outputs.Add(("T_op", "Conductor operating temperature for the insulation"));

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
            c.Block("I2", $"I2: enter the conventional operating current of the {device} from its datasheet. In the machine-wiring context the I₂ ≤ 1.45·I_z condition cannot be deemed satisfied.");
            return;
        }
        else
        {
            deemed = true;

            if (!gg)
                c.Warn("I2-deemed", $"I2: the conventional operating current of the {device} is not entered. The I₂ ≤ 1.45·I_z condition holds only if the device's I₂ is at most 1.45·I_n, so enter it.");
        }

        if (haveI2)
        {
            c.Assign("I2_lim", "1.45 * I_z", "A", "Largest conventional operating current the conductor allows, 1.45 I_z", coordination);
            c.Assign("u_I2", "I2 / I2_lim", null, "I₂ utilisation of 1.45 I_z", coordination);
            outputs.AddRange([("I2", "Conventional operating current"), ("u_I2", "I₂ / (1.45 I_z)")]);
        }

        outputs.Add(("device_code", "Protective device type code"));

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        if (awgMode)
            c.Output("gauge", "Selected AWG gauge", new TextValue("AWG " + AwgName(pick.Gauge)));

        c.Check("Design current against the device rating", "I_b", "I_n", coordination);
        c.Check("Device rating against the conductor capacity", "I_n", "I_z", coordination);

        if (haveI2)
            c.Check("Conventional operating current against 1.45 times the conductor capacity", "I2", "I2_lim", device == SemiEnclosed ? "BS 7671 reg. 433.1.202" : coordination);
        else if (deemed)
            c.Check(gg ? "Device rating against the conductor capacity, I₂ deemed satisfied for a gG fuse to BS 88" : "Device rating against the conductor capacity, I₂ deemed satisfied (taken as at most 1.45·I_n)", "I_n", "I_z", gg ? "BS 7671 reg. 433.1.201" : coordination);
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

    /// <summary>
    /// elec.conductor-sizing-05: the ampacity record is checked for sense. A cross-section entered twice keeps its lower capacity (the unconservative reading
    /// is never taken) and warns; a larger cross-section with a lower capacity than a smaller one warns, because a record that falls with size mixes
    /// installation methods or has a typing slip.
    /// </summary>
    private static List<(double Area, double It, double Gauge)> CheckTable(CalcRunContext c, List<(double Area, double It, double Gauge)> table, bool awgMode)
    {
        var merged = table.GroupBy(t => Math.Round(t.Area, 9)).Select(g => g.OrderBy(t => t.It).First()).ToList();
        var duplicates = table.Count - merged.Count;

        if (duplicates > 0)
            c.Warn("ampacity-sense", $"ampacity: {duplicates} {(awgMode ? "gauge" : "cross-section")}(s) appear more than once in the record. The lower capacity of each is used, so remove the wrong row.");

        var bySize = merged.OrderBy(t => t.Area).ToList();

        for (var i = 1; i < bySize.Count; i++)
        {
            if (bySize[i].It < bySize[i - 1].It * (1 - 1e-9))
            {
                c.Warn("ampacity-sense", $"ampacity: the capacity falls from {F(bySize[i - 1].It)} A ({F(bySize[i - 1].Area)} mm²) to {F(bySize[i].It)} A ({F(bySize[i].Area)} mm²) as the cross-section grows. The record probably mixes installation methods or has a typing slip.");
                break;
            }
        }

        return merged;
    }

    /// <summary>The SI value of a table cell (0 when absent); a cell without a unit is read in <paramref name="unit"/>.</summary>
    private static double Cell(CalcRow row, string key, Unit unit) => row.Get(key) is { Number: { } n } cell ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : unit).ToSi(n) : 0;

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
