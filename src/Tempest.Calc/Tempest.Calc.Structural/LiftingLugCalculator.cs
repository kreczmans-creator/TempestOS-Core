using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Structural;

/// <summary>
/// S8 — lifting lugs (pin-loaded plates) and the lug loads of spreader and lifting beams (Addendum B, ADR-0173). GATED: the
/// template names the <see cref="CalcFeatures.Lifting"/> feature, so the catalogue withholds it until the installation's configuration
/// switches the feature on, and its Issue needs a competent-person attestation and a second independent check. The sheet states, in
/// its scope and as an output, that it is a design calculation and not a thorough examination.
/// </summary>
/// <remarks>
/// <para>
/// Two modes and both. <b>BTH-1 mode</b> (clause 3-3.3 cited by number only) is an allowable-stress approach: the design load is
/// N<sub>d</sub>·k<sub>svc</sub> times the sling force; the resistances are the pin-hole checks — tension beyond the hole, fracture of
/// the net section across the hole (single plane), shear-out on two planes at φ = 55·D<sub>p</sub>/D<sub>h</sub> degrees, and pin bearing —
/// with the pin-clearance factor C<sub>r</sub> = 1 − 0.275·√(1 − D<sub>p</sub>²/D<sub>h</sub>²) on the two tension modes, and a dishing
/// ratio D<sub>h</sub>/t against a limit. <b>EN 13155 mode</b> asks for no permanent set at 2× the rated load and no release at 3× it
/// (at most 16,000 cycles), applied to the same four sections with the stresses at f<sub>y</sub> and f<sub>u</sub>; the stress
/// calculation route of EN 13001-3-1 is cited, not implemented. A horizontal beam adds the automatic 6° tilt case.
/// </para>
/// <para>
/// Nothing from either standard is shipped: the design factor N<sub>d</sub> by Design Category, the service-class factor, the divisors
/// on f<sub>y</sub> and f<sub>u</sub>, the bearing multiple, the dishing limit and the proof-load factor are user records (inputs with
/// no default). The equations are written out in the spec document; the owner's review checklist (§B10 item 6) compares them with the
/// standards before the flag is ever enabled.
/// </para>
/// </remarks>
public sealed class LiftingLugCalculator : ICalcCalculator
{
    public const string Id = "struct.lifting";

    public const string ModeBth1 = "BTH-1";
    public const string ModeEn = "EN 13155";
    public const string ModeBoth = "both";

    /// <summary>The record kind a material from the W3 library fills (equal to <c>MaterialLibrary.Kind</c>).</summary>
    public const string MaterialKind = "material.properties";

    /// <summary>The tilt, in degrees, of the automatic load case for a horizontal beam (Addendum B §B6 S8).</summary>
    public const double TiltDegrees = 6.0;

    /// <summary>The most load cycles the 2×/3× proof route covers (Addendum B §B6 S8); above it the sheet does not run.</summary>
    public const double MaxCycles = 16000;

    public const string DesignNotice = "This is a design calculation, not a thorough examination.";

    private const string GroupLoads = "Mode and loading";
    private const string GroupLug = "Lug geometry";
    private const string GroupMaterial = "Material";
    private const string GroupFactors = "Design factors (user records, no defaults)";
    private const string GroupBeam = "Spreader or lifting beam";
    private const string GroupAttest = "Competent-person attestation (required to check and issue)";

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Structural, "Lifting lugs and spreader beams",
        DesignNotice + " Pin-loaded lifting lugs and the lug loads of spreader or lifting beams, checked by the BTH-1 allowable-stress approach (clause 3-3.3), by the EN 13155 proof route (2× the rated load without permanent set, 3× without release) or both: " +
        "sling force from the rated load, sling angle and out-of-plane angle; tension beyond the hole, net-section fracture, shear-out on two planes, pin bearing and dishing; capacity and utilisation per mode, the governing mode, the proof load and a marking and certification checklist. " +
        "Scope notes (LOLER and PUWER): this sheet does not replace the thorough examination and inspection of lifting equipment by a competent person (LOLER reg 9), the marking, test and certification that the regulations and the standards require, the planning of the lifting operation, the weld, bolt and beam checks of the linked sheets, or the pin, shackle and sling ratings. " +
        "It is available only while the lifting feature is enabled; its issue needs a competent-person attestation and a second independent check.",
        "Sling force: F_v = WLL/n_eff (n_eff = n_lugs, at most 3); θ_d = θ (+6° for a horizontal beam); β_d = β (+6° for a horizontal beam); F_s = F_v/cos θ_d; in-plane F_ip = F_s·cos β_d; out-of-plane F_op = F_s·sin β_d; horizontal N_beam = F_v·tan θ_d; M_op = F_op·h\n" +
        "Geometry: b_e,eff = min(b_e, R − D_h/2); a_eff = a (default R − D_h/2); A_t = 2·t·b_e,eff; A_net = t·(2R − D_h); φ = 55·D_p/D_h (degrees); A_v = 2·t·[a_eff + (D_h/2)(1 − cos φ)]; A_b = D_p·t\n" +
        "Pin clearance: C_r = 1 − 0.275·√(1 − D_p²/D_h²)\n" +
        "BTH-1 (3-3.3): F_y,a = f_y/n_y; F_u,a = f_u/n_u; design load D = N_d·k_svc·F_s; P_t = C_r·F_y,a·A_t; P_sp = C_r·F_u,a·A_net; P_v = (F_y,a/√3)·A_v; P_b = k_b·F_y,a·A_b; u = D/P for each; dishing D_h/t ≤ dish_max\n" +
        "EN 13155 (2× elastic, 3× no release): P_EN = min(Y/2, U/3) for each section, Y with f_y and U with f_u (shear with f/√3); u = F_s/P_EN\n" +
        "Capacity: WLL_cap = WLL/u_lug for the governing section of each mode (smaller of the modes used); beam: u_beam from N_beam on A_beam; proof load = k_proof·WLL",
        [
            new CalcInputSpec("mode", "Design mode", CalcInputKind.Choice, Choices: [ModeBth1, ModeEn, ModeBoth], DefaultChoice: ModeBoth, Group: GroupLoads),
            new CalcInputSpec("WLL", "Rated load (working load limit) of the appliance, carried by all its lugs", CalcInputKind.Quantity, "kN", Default: 150, Min: 0.5, Max: 10000, HardMin: 0.5, HardMax: 10000, Group: GroupLoads),
            new CalcInputSpec("n_lugs", "Number of lugs sharing the load (a fourth is not relied on: three share)", CalcInputKind.Choice, Choices: ["1", "2", "3", "4"], DefaultChoice: "2", Group: GroupLoads),
            new CalcInputSpec("theta", "Sling angle from the vertical, in the plane of the lug", CalcInputKind.Quantity, "deg", Default: 30, Min: 0, Max: 60, HardMin: 0, HardMax: 60, Group: GroupLoads),
            new CalcInputSpec("beta", "Out-of-plane angle of the sling", CalcInputKind.Quantity, "deg", Default: 0, Min: 0, Max: 30, HardMin: 0, HardMax: 30, Group: GroupLoads),
            new CalcInputSpec("beam", "The lugs are on a horizontal spreader or lifting beam (adds the automatic 6° tilt case)", CalcInputKind.Boolean, Default: 0, Group: GroupLoads),
            new CalcInputSpec("n_cycles", "Load cycles in the life of the lug (the 2×/3× route covers up to 16,000; beyond that a fatigue assessment is needed and the sheet does not run)", CalcInputKind.Number, Default: 1000, Min: 1, Max: MaxCycles, HardMin: 1, HardMax: MaxCycles, Group: GroupLoads),

            new CalcInputSpec("t", "Lug plate thickness t", CalcInputKind.Quantity, "mm", Default: 20, Min: 5, Max: 150, HardMin: 5, HardMax: 150, Group: GroupLug),
            new CalcInputSpec("Dh", "Hole diameter D_h", CalcInputKind.Quantity, "mm", Default: 50, Min: 10, Max: 500, HardMin: 10, HardMax: 500, Group: GroupLug),
            new CalcInputSpec("Dp", "Pin diameter D_p (not larger than the hole)", CalcInputKind.Quantity, "mm", Default: 45, Min: 8, Max: 500, HardMin: 8, HardMax: 500, Group: GroupLug),
            new CalcInputSpec("R", "Lug end radius R about the hole centre (the plate is 2R wide at the hole)", CalcInputKind.Quantity, "mm", Default: 65, Min: 15, Max: 600, HardMin: 15, HardMax: 600, Group: GroupLug),
            new CalcInputSpec("be", "Effective edge distance b_e on each side of the hole for tension beyond the hole (limited to R − D_h/2)", CalcInputKind.Quantity, "mm", Default: 35, Min: 5, Max: 400, HardMin: 5, HardMax: 400, Group: GroupLug),
            new CalcInputSpec("a", "Distance a from the hole edge to the plate edge along the load line (blank: R − D_h/2, a round-ended lug)", CalcInputKind.Quantity, "mm", Min: 5, Max: 600, HardMin: 5, HardMax: 600, IsOptional: true, Group: GroupLug),
            new CalcInputSpec("h", "Distance h from the hole centre to the weld or base, for the out-of-plane moment (blank: no moment given)", CalcInputKind.Quantity, "mm", Min: 1, Max: 3000, HardMin: 1, HardMax: 3000, IsOptional: true, Group: GroupLug),

            new CalcInputSpec("f_y", "Yield strength f_y of the lug plate for its thickness", CalcInputKind.Quantity, "MPa", Default: 355, Min: 100, Max: 1100, HardMin: 100, HardMax: 1100, Group: GroupMaterial, MaterialProperty: "f_y"),
            new CalcInputSpec("f_u", "Ultimate strength f_u of the lug plate", CalcInputKind.Quantity, "MPa", Default: 470, Min: 150, Max: 1400, HardMin: 150, HardMax: 1400, Group: GroupMaterial, MaterialProperty: "f_u"),

            new CalcInputSpec("design_category", "Design Category the design factor belongs to (a label, printed on the sheet)", CalcInputKind.Text, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("service_class", "Service Class the service factor belongs to (a label, printed on the sheet)", CalcInputKind.Text, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("Nd", "Design factor N_d of the Design Category (BTH-1 mode; user record)", CalcInputKind.Number, Min: 1, Max: 5, HardMin: 1, HardMax: 5, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("k_svc", "Service-class factor on the load (blank: 1.0; user record)", CalcInputKind.Number, Min: 1, Max: 3, HardMin: 1, HardMax: 3, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("n_y", "Divisor n_y on f_y for the allowable stress, F_y,a = f_y/n_y (BTH-1 mode; user record)", CalcInputKind.Number, Min: 1, Max: 3, HardMin: 1, HardMax: 3, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("n_u", "Divisor n_u on f_u for the allowable stress, F_u,a = f_u/n_u (BTH-1 mode; user record)", CalcInputKind.Number, Min: 1, Max: 4, HardMin: 1, HardMax: 4, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("k_b", "Bearing multiple k_b of the allowable yield stress on the projected pin area (BTH-1 mode; user record)", CalcInputKind.Number, Min: 0.5, Max: 3, HardMin: 0.5, HardMax: 3, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("dish_max", "Dishing-ratio limit on D_h/t (blank: the dishing check is not made; user record)", CalcInputKind.Number, Min: 1, Max: 100, HardMin: 1, HardMax: 100, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("k_proof", "Proof-load factor on the rated load (blank: no proof load given; user record)", CalcInputKind.Number, Min: 1, Max: 3, HardMin: 1, HardMax: 3, IsOptional: true, Group: GroupFactors),

            new CalcInputSpec("A_beam", "Gross cross-section area of the spreader or lifting beam, for its axial utilisation (blank: lug utilisation only)", CalcInputKind.Quantity, "cm^2", Min: 1, Max: 50000, HardMin: 1, HardMax: 50000, IsOptional: true, Group: GroupBeam),

            new CalcInputSpec(CalcAttestation.NameSymbol, "Competent person attesting the design (not the author; required to check and issue)", CalcInputKind.Text, IsOptional: true, Group: GroupAttest),
            new CalcInputSpec(CalcAttestation.QualificationSymbol, "Their qualification and standing", CalcInputKind.Text, IsOptional: true, Group: GroupAttest),
            new CalcInputSpec(CalcAttestation.StatementSymbol, "Attestation: what the competent person has reviewed (at least 20 characters)", CalcInputKind.Text, IsOptional: true, Group: GroupAttest),
        ],
        [
            new CalcAssumption("A design calculation, not a thorough examination: it checks the lug plate at the pin hole against the user's chosen criteria and nothing else.", "LOLER reg 9 and PUWER duties are the owner's and the competent person's."),
            new CalcAssumption("Nominal-stress screening of four sections (tension beyond the hole, net section across the hole, shear-out, bearing) on a plate loaded through a pin; no stress-concentration factor, no fatigue (the cycle count is limited to 16,000), no weld, bolt or beam check, which are made on the linked sheets.", "The notch-stress route of EN 13001-3-1 is cited, not implemented."),
            new CalcAssumption("The load on the hole is the resultant sling force F_s whatever the out-of-plane angle, which is conservative; the out-of-plane component and its moment are outputs for the weld and base checks.", "F_op = F_s·sin β_d."),
            new CalcAssumption("Equal sharing between the lugs, with a fourth lug not relied on (three share). The share of a statically indeterminate set is not computed.", "Conservative convention; replace with the actual share if it is known."),
            new CalcAssumption("A horizontal beam adds the automatic tilt case: sling angle and out-of-plane angle each increased by 6° together, which dominates the untilted case.", "Addendum B §B6 S8."),
            new CalcAssumption("The design factor, service-class factor, allowable-stress divisors, bearing multiple, dishing limit and proof-load factor are the user's records from their own licensed copy of the code; none is shipped and none has a default.", "ASME BTH-1 and EN 13155 text and tables are not shipped."),
            new CalcAssumption("The equations are written out in the spec document in the author's notation; the owner compares them with BTH-1 (2023 or later) and EN 13155+A1 before the feature is ever enabled.", "Owner test pack, S8 review checklist."),
        ],
        [
            new CalcClauseRef("ASME BTH-1", "3-3.3", "Pin-connected plates (design of lifting lugs)"),
            new CalcClauseRef("BS EN 13155", "design requirements (clause to be confirmed)", "Non-fixed load lifting attachments: 2× elastic, 3× no release"),
            new CalcClauseRef("BS EN 13001-3-1", "5", "Proof of static strength: the calculation route cited, not implemented"),
            new CalcClauseRef("LOLER 1998", "reg 7, reg 9", "Marking of lifting equipment; thorough examination and inspection"),
            new CalcClauseRef("PUWER 1998", "reg 4, reg 5", "Suitability and maintenance of work equipment"),
        ],
        [
            new CalcApplicabilityRule("pin", "The pin cannot be larger than the hole.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("radius", "The lug end radius must exceed the hole radius.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("strength", "f_u cannot be below f_y.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("factors", "BTH-1 mode needs the user-record factors N_d, n_y, n_u and k_b.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("clearance", "A pin much smaller than the hole reduces C_r.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("be", "b_e is limited to R − D_h/2.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("tilt", "A horizontal beam adds the 6° tilt case.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("sharing", "Four lugs: three are taken to share the load.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("angle", "A sling angle above 60° from the vertical, with the tilt, is outside the usual range.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("k-svc", "No service-class factor entered: 1.0 used.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("dishing", "No dishing limit entered: the dishing check is not made.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("proof", "No proof-load factor entered: no proof load given.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("beam-area", "No beam area entered: the spreader utilisation is the lug utilisation only.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("attestation", "The competent-person attestation and the second check are required to check and issue.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-bth1-hand", "BTH-1 mode, two lugs at 30° and 5° out of plane, 150 kN: every section hand-recomputed",
                new CalcReferenceSource("Analytic: the equations of the spec document (docs/calcs/structural/struct.lifting.md) evaluated independently in python (s8_ref); the design factors are illustrative user-record values, not values from any standard", "struct.lifting spec",
                    "F_v = 75 kN, F_s = 75/cos 30° = 86.6025 kN, F_op = F_s·sin 5° = 7.5479 kN; C_r = 1 − 0.275·√(1 − 0.9²) = 0.880130, φ = 55 × 0.9 = 49.5°; A_t = 2·20·35 = 1400 mm², A_net = 20·(130 − 50) = 1600 mm², A_v = 2·20·[40 + 25·(1 − cos 49.5°)] = 1950.55 mm², A_b = 45·20 = 900 mm²; " +
                    "F_y,a = 355/1.6 = 221.875 MPa, F_u,a = 470/2.2 = 213.636 MPa; D = 1.8 × 86.6025 = 155.885 kN; P_t = 273.390, P_sp = 300.845, P_v = 249.865, P_b = 1.2 × 221.875 × 900 = 239.625 kN; u = 0.5702, 0.5182, 0.6239, 0.6505 (bearing governs); capacity 150/0.6505 = 230.58 kN; dishing 50/20 = 2.5, u = 0.0625; proof 1.5 × 150 = 225 kN"),
                [.. LugA()],
                [
                    Ex("F_s", 86.60254037844385, "kN"), Ex("F_ip", 86.27299156628207, "kN"), Ex("F_op", 7.547908730517332, "kN"), Ex("N_beam", 43.301270189221924, "kN"), Ex("M_op", 1.1321863095775997, "kN*m"),
                    Ex("Cr", 0.8801302790526315, ""), Ex("phi", 49.5, "deg"), Ex("A_t", 1400, "mm^2"), Ex("A_net", 1600, "mm^2"), Ex("A_v", 1950.5519516698164, "mm^2"), Ex("A_b", 900, "mm^2"),
                    Ex("Fy_a", 221.875, "MPa"), Ex("Fu_a", 213.63636363636363, "MPa"), Ex("D_bth", 155.88457268119893, "kN"),
                    Ex("P_t", 273.3904679307236, "kN"), Ex("P_sp", 300.8445317488995, "kN"), Ex("P_v", 249.86490718721626, "kN"), Ex("P_b", 239.625, "kN"),
                    Ex("u_t", 0.5701902259470862, ""), Ex("u_sp", 0.5181565766709973, ""), Ex("u_v", 0.6238754150633858, ""), Ex("u_b", 0.6505355145798599, ""), Ex("u_bth", 0.6505355145798599, ""),
                    Ex("cap_bth", 230.5792637576068, "kN"), Ex("dish", 2.5, ""), Ex("u_dish", 0.0625, ""), Ex("lug_util", 0.6505355145798599, ""), Ex("capacity", 230.5792637576068, "kN"), Ex("util", 0.6505355145798599, ""), Ex("proof_load", 225.0, "kN"),
                ]),
            new CalcTestCase("ka-en13155-hand", "EN 13155 mode, two lugs at 20°, 100 kN, f_y 235 / f_u 360: the 2×WLL elastic route governs the shear-out",
                new CalcReferenceSource("Analytic: 2× the rated load without permanent set (capacity at f_y) and 3× without release (capacity at f_u) on the four sections of the spec, evaluated independently in python (s8_ref)", "struct.lifting spec, EN 13155 mode",
                    "F_s = 50/cos 20° = 53.2089 kN; A_t = 2·30·20 = 1200 mm² (b_e limited to R − D_h/2 = 20), A_net = 30·(80 − 40) = 1200 mm², φ = 55 × 0.95 = 52.25°, A_v = 2·30·[20 + 20·(1 − cos 52.25°)] = 1665.34 mm², A_b = 38·30 = 1140 mm²; " +
                    "P_EN = min(Y/2, U/3): tension 141.0 kN (Y/2 = 141.0, U/3 = 144.0), shear-out 112.974 kN (Y/2 = 112.974 < U/3 = 115.378: the 2× route governs), bearing 133.95 kN; u = 0.3774, 0.3774, 0.4710, 0.3972; capacity 100/0.4710 = 212.32 kN"),
                [.. LugB()],
                [
                    Ex("F_s", 53.2088886237956, "kN"), Ex("A_t", 1200.0, "mm^2"), Ex("A_net", 1200, "mm^2"), Ex("A_v", 1665.3392639586607, "mm^2"), Ex("A_b", 1140, "mm^2"),
                    Ex("P_en_t", 141.0, "kN"), Ex("u_en_t", 0.3773680044240823, ""), Ex("P_en_sp", 141.0, "kN"), Ex("u_en_sp", 0.3773680044240823, ""),
                    Ex("P_en_v", 112.97437849978385, "kN"), Ex("u_en_v", 0.4709819105036936, ""), Ex("P_en_b", 133.95, "kN"), Ex("u_en_b", 0.39722947834113925, ""),
                    Ex("u_en", 0.4709819105036936, ""), Ex("cap_en", 212.32237962824215, "kN"), Ex("lug_util", 0.4709819105036936, ""), Ex("capacity", 212.32237962824215, "kN"), Ex("util", 0.4709819105036936, ""),
                ]),
            new CalcTestCase("ka-both-beam-tilt", "Both modes on a horizontal beam: four lugs (three share), 400 kN at 25° and 3° plus the automatic 6° tilt, 120 cm² beam",
                new CalcReferenceSource("Analytic: the spec equations with θ_d = 25° + 6° = 31°, β_d = 3° + 6° = 9°, n_eff = 3, evaluated independently in python (s8_ref)", "struct.lifting spec, both modes",
                    "F_v = 400/3 = 133.333 kN, F_s = 133.333/cos 31° = 155.551 kN, F_op = F_s·sin 9° = 24.3336 kN, N_beam = 133.333·tan 31° = 80.1147 kN, M_op = 24.3336 × 0.2 = 4.86671 kN·m; C_r = 0.908371, φ = 51.857°; BTH-1 u = 0.4631, 0.4372, 0.4964, 0.5311 (bearing), EN 13155 u = 0.3310, 0.3009, 0.3906, 0.5015 (bearing, 3× route); " +
                    "capacity min(753.13, 797.68) = 753.13 kN; beam axial u = max(0.0542, 0.0426) = 0.0542 on 120 cm²; util = 0.5311 (lug governs)"),
                [.. LugC()],
                [
                    Ex("F_s", 155.55111962871072, "kN"), Ex("F_ip", 153.63602722379707, "kN"), Ex("F_op", 24.33355618552632, "kN"), Ex("N_beam", 80.11474920367473, "kN"), Ex("M_op", 4.866711237105263, "kN*m"),
                    Ex("Cr", 0.9083707559381453, ""), Ex("phi", 51.857142857142854, "deg"), Ex("A_v", 4402.988910171803, "mm^2"),
                    Ex("P_t", 604.634284421328, "kN"), Ex("P_sp", 640.4013829363925, "kN"), Ex("P_v", 564.0210784668454, "kN"), Ex("P_b", 527.175, "kN"),
                    Ex("u_t", 0.4630766440901525, ""), Ex("u_sp", 0.4372133208829897, ""), Ex("u_v", 0.49642119066324564, ""), Ex("u_b", 0.5311177793553931, ""), Ex("u_bth", 0.5311177793553931, ""), Ex("cap_bth", 753.1286195793933, "kN"),
                    Ex("P_en_t", 470.0, "kN"), Ex("P_en_sp", 517.0, "kN"), Ex("P_en_v", 398.2571370958383, "kN"), Ex("P_en_b", 310.2, "kN"),
                    Ex("u_en_t", 0.3309598289972569, ""), Ex("u_en_sp", 0.30087257181568805, ""), Ex("u_en_v", 0.39057961588087814, ""), Ex("u_en_b", 0.5014542863594801, ""), Ex("u_en", 0.5014542863594801, ""), Ex("cap_en", 797.6798900333857, "kN"),
                    Ex("u_beam", 0.0541620839686815, ""), Ex("lug_util", 0.5311177793553931, ""), Ex("capacity", 753.1286195793933, "kN"), Ex("util", 0.5311177793553931, ""), Ex("proof_load", 600.0, "kN"),
                ]),
            new CalcTestCase("ka-both-shear-governs", "Both modes, shear-out governs: the geometry of the EN 13155 case with the BTH-1 user-record factors",
                new CalcReferenceSource("Analytic: the spec equations, evaluated independently in python (s8_ref)", "struct.lifting spec, both modes",
                    "F_s = 53.2089 kN; D = 1.8 × 53.2089 = 95.776 kN; F_y,a = 235/1.6 = 146.875 MPa, F_u,a = 360/2.2 = 163.636 MPa; C_r = 0.914131; P_t = 161.116, P_sp = 179.502, P_v = (146.875/√3) × 1665.34 = 141.218, P_b = 1.2 × 146.875 × 1140 = 200.925 kN; u = 0.5945, 0.5336, 0.6782 (shear-out governs), 0.4767; capacity BTH-1 147.446 kN, EN 13155 212.322 kN, so 147.446 kN"),
                [.. LugE()],
                [
                    Ex("Cr", 0.9141312775220221, ""), Ex("D_bth", 95.7759995228321, "kN"), Ex("P_t", 161.11563766325636, "kN"), Ex("P_sp", 179.50214176796072, "kN"), Ex("P_v", 141.2179731247298, "kN"), Ex("P_b", 200.925, "kN"),
                    Ex("u_t", 0.5944550194625492, ""), Ex("u_sp", 0.5335646615662116, ""), Ex("u_v", 0.6782139511253188, ""), Ex("u_b", 0.47667537400936716, ""), Ex("u_bth", 0.6782139511253188, ""), Ex("cap_bth", 147.44609696405703, "kN"),
                    Ex("u_en", 0.4709819105036936, ""), Ex("cap_en", 212.32237962824215, "kN"), Ex("lug_util", 0.6782139511253188, ""), Ex("capacity", 147.44609696405703, "kN"),
                ]),
            new CalcTestCase("ka-pdhonline-s106-ratio", "PDHonline S106 pin ratio D_p/D_h = 0.6: C_r = 0.78 and φ = 33° (the other S106 values are not pinned)",
                new CalcReferenceSource("PDHonline course S106, lifting lug design example (course notes, as quoted in the Addendum B brief)", null,
                    "C_r = 0.78 and φ = 33° are the quoted values; D_p/D_h = 0.6 is inferred from them (both follow exactly from it): C_r = 1 − 0.275·√(1 − 0.36) = 1 − 0.275 × 0.8 = 0.78 and φ = 55 × 0.6 = 33°, recomputed in python. " +
                    "The example's own plate, hole, edge distance, material and factor inputs are not available, so its A_v = 2.964 in², double-plane shear 19.849 k, single-plane 20.539 k and bearing 10.875 k are NOT pinned here; the geometry around the ratio is this sheet's own (50 mm hole, 30 mm pin). To be checked against the owner's copy of the course", Secondary: true),
                [.. LugD()],
                [Ex("Cr", 0.78, ""), Ex("phi", 33.0, "deg")],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Design",
        OutputPorts:
        [
            new CalcOutputPort("capacity", "kN", "Rated load the lug carries in the governing mode (lug.capacity)"),
            new CalcOutputPort("governing_mode", "", "The governing mode and section (lug.governing_mode)"),
            new CalcOutputPort("util", "", "Governing utilisation of the lug and the spreader beam (spreader.util)"),
            new CalcOutputPort("F_s", "kN", "Sling force on each lug (resultant), for the pin, shackle and sling"),
            new CalcOutputPort("F_ip", "kN", "In-plane component of the sling force (→ M6 weld group, S4)"),
            new CalcOutputPort("F_op", "kN", "Out-of-plane component of the sling force (→ M6 weld group, M3 bolt group)"),
            new CalcOutputPort("N_beam", "kN", "Horizontal sling component per lug: the axial force in a spreader beam (→ S7 member check)"),
        ],
        RecordKinds:
        [
            new CalcRecordKind(MaterialKind, "Material properties", ["f_y", "f_u"], "A material from the library: its strengths fill these inputs, each citing the material, its revision and the page or table of the value."),
            new CalcRecordKind("lifting.design-factors", "Design factor and service-class factor", ["design_category", "service_class", "Nd", "k_svc"], "N_d for the Design Category and the service-class factor, from the user's own licensed copy of the code, with its edition and table. Never shipped."),
            new CalcRecordKind("lifting.allowable-stress", "Allowable-stress divisors and bearing multiple", ["n_y", "n_u", "k_b"], "The divisors on f_y and f_u that give the allowable stresses and the bearing multiple of the approach in use, from the user's own copy of the code. Never shipped."),
            new CalcRecordKind("lifting.limits", "Dishing limit and proof-load factor", ["dish_max", "k_proof"], "The dishing-ratio limit D_h/t and the proof-load factor from the code or the owner's procedure. Never shipped."),
        ],
        IndependentCheck: true,
        FeatureFlag: CalcFeatures.Lifting,
        RequiresAttestation: true,
        RequiresSecondCheck: true);

    public void Run(CalcRunContext c)
    {
        var mode = c.InputText("mode");
        var bth = mode is ModeBth1 or ModeBoth;
        var en = mode is ModeEn or ModeBoth;

        foreach (var s in new[] { "WLL", "theta", "beta", "n_cycles", "t", "Dh", "Dp", "R", "be", "f_y", "f_u" })
            c.Input(s);

        // Units in this method: N, mm, MPa, degrees at the interface.
        var wll = c.InputSi("WLL");
        var theta = c.InputSi("theta") * 180 / Math.PI;
        var beta = c.InputSi("beta") * 180 / Math.PI;
        var t = c.InputSi("t") * 1e3;
        var dh = c.InputSi("Dh") * 1e3;
        var dp = c.InputSi("Dp") * 1e3;
        var radius = c.InputSi("R") * 1e3;
        var be = c.InputSi("be") * 1e3;
        var fy = c.InputSi("f_y") / 1e6;
        var fu = c.InputSi("f_u") / 1e6;
        var beam = c.InputFlag("beam");
        var nLugs = int.Parse(c.InputText("n_lugs"), System.Globalization.CultureInfo.InvariantCulture);

        if (dp > dh)
        {
            c.Block("pin", "Dp: the pin cannot be larger than the hole; enter D_p ≤ D_h.");
            return;
        }

        if (radius <= dh / 2)
        {
            c.Block("radius", "R: the lug end radius must exceed the hole radius D_h/2; enter a larger R or a smaller D_h.");
            return;
        }

        if (fu < fy)
        {
            c.Block("strength", "f_u: the ultimate strength cannot be below the yield strength; enter f_u ≥ f_y.");
            return;
        }

        double nd = 0, ny = 0, nu = 0, kb = 0;

        if (bth)
        {
            foreach (var (symbol, text) in new[] { ("Nd", "the design factor N_d of the Design Category"), ("n_y", "the divisor n_y on f_y"), ("n_u", "the divisor n_u on f_u"), ("k_b", "the bearing multiple k_b") })
            {
                if (!c.Has(symbol))
                {
                    c.Block("factors", $"{symbol}: BTH-1 mode needs {text} from your own copy of the code (a user record, no default); enter it, or choose the EN 13155 mode.");
                    return;
                }
            }

            nd = c.InputSi("Nd");
            ny = c.InputSi("n_y");
            nu = c.InputSi("n_u");
            kb = c.InputSi("k_b");
        }

        var kSvc = 1.0;

        if (bth && c.Has("k_svc"))
            kSvc = c.InputSi("k_svc");
        else if (bth)
            c.Warn("k-svc", "k_svc: no service-class factor entered, 1.0 used; enter it from your own copy of the code if it applies.");

        // ---------------------------------------------------------------- loads
        var nEff = Math.Min(nLugs, 3);
        var tilt = beam ? TiltDegrees : 0.0;
        var thetaD = theta + tilt;
        var betaD = beta + tilt;

        if (beam)
            c.Warn("tilt", "beam: the automatic 6° tilt case is applied — the sling angle and the out-of-plane angle are each increased by 6°.");

        if (nLugs == 4)
            c.Warn("sharing", "n_lugs: four lugs — three are taken to share the load; replace the sharing with the actual share if it is known.");

        if (thetaD > 60)
            c.Warn("angle", $"theta: the sling angle with the tilt is {F(thetaD)}° from the vertical, above the usual 60°; check the sling and the lug for the lower angle to the horizontal.");

        var fV = wll / nEff;
        var cosTheta = Math.Cos(thetaD * Math.PI / 180);
        var fS = fV / cosTheta;
        var fIp = fS * Math.Cos(betaD * Math.PI / 180);
        var fOp = fS * Math.Sin(betaD * Math.PI / 180);
        var nBeam = fV * Math.Tan(thetaD * Math.PI / 180);

        c.Record("n_eff", "min(n_lugs, 3)", $"min({nLugs}, 3)", new ScalarValue(nEff), "Lugs sharing the load (a fourth is not relied on)", "spec");
        c.Record("theta_d", beam ? "θ + 6° (horizontal beam)" : "θ", beam ? $"{F(theta)}° + 6°" : $"{F(theta)}°", ScalarValue.Of(thetaD, "deg"), "Sling angle used", "spec");
        c.Record("beta_d", beam ? "β + 6° (horizontal beam)" : "β", beam ? $"{F(beta)}° + 6°" : $"{F(beta)}°", ScalarValue.Of(betaD, "deg"), "Out-of-plane angle used", "spec");
        c.Record("F_v", "WLL/n_eff", $"{F(wll / 1e3)} kN / {nEff}", ScalarValue.Of(fV / 1e3, "kN"), "Vertical load on each lug", "spec");
        c.Record("F_s", "F_v/cos θ_d", $"{F(fV / 1e3)} kN / cos({F(thetaD)}°)", ScalarValue.Of(fS / 1e3, "kN"), "Sling force on each lug (resultant)", "spec");
        c.Record("F_ip", "F_s·cos β_d", $"{F(fS / 1e3)} kN × cos({F(betaD)}°)", ScalarValue.Of(fIp / 1e3, "kN"), "In-plane component of the sling force", "spec");
        c.Record("F_op", "F_s·sin β_d", $"{F(fS / 1e3)} kN × sin({F(betaD)}°)", ScalarValue.Of(fOp / 1e3, "kN"), "Out-of-plane component of the sling force", "spec");
        c.Record("N_beam", "F_v·tan θ_d", $"{F(fV / 1e3)} kN × tan({F(thetaD)}°)", ScalarValue.Of(nBeam / 1e3, "kN"), "Horizontal sling component per lug (axial force in a spreader beam)", "spec");

        if (c.Has("h"))
        {
            var h = c.InputSi("h") * 1e3;
            c.Record("M_op", "F_op·h", $"{F(fOp / 1e3)} kN × {F(h)} mm", ScalarValue.Of(fOp * h / 1e6, "kN*m"), "Out-of-plane moment at the weld or base", "spec");
        }

        // ---------------------------------------------------------------- geometry
        var beEff = Math.Min(be, radius - dh / 2);

        if (be > radius - dh / 2)
            c.Warn("be", $"be: b_e ({F(be)} mm) exceeds R − D_h/2 ({F(radius - dh / 2)} mm); the smaller is used.");

        var aEff = c.Has("a") ? c.InputSi("a") * 1e3 : radius - dh / 2;
        var ratio = dp / dh;
        var phi = 55.0 * ratio;
        var aT = 2 * t * beEff;
        var aNet = t * (2 * radius - dh);
        var aV = 2 * t * (aEff + dh / 2 * (1 - Math.Cos(phi * Math.PI / 180)));
        var aB = dp * t;

        if (ratio < 0.9)
            c.Warn("clearance", $"Dp: the pin is {F(ratio * 100)} % of the hole diameter; the pin-clearance factor C_r falls as the pin gets smaller, and a close-fitting pin is the usual practice.");

        c.Record("be_eff", "min(b_e, R − D_h/2)", $"min({F(be)}, {F(radius)} − {F(dh)}/2) mm", ScalarValue.Of(beEff, "mm"), "Effective edge distance used", "spec");
        c.Record("a_eff", "a, or R − D_h/2", c.Has("a") ? $"a = {F(aEff)} mm" : $"{F(radius)} − {F(dh)}/2 mm", ScalarValue.Of(aEff, "mm"), "Edge distance along the load line", "spec");
        c.Record("phi", "55·D_p/D_h (degrees)", $"55 × {F(dp)} / {F(dh)}", ScalarValue.Of(phi, "deg"), "Angle of the shear planes", "BTH-1 3-3.3");
        c.Record("A_t", "2·t·b_e,eff", $"2 × {F(t)} × {F(beEff)} mm", ScalarValue.Of(aT, "mm^2"), "Area beyond the hole in tension", "spec");
        c.Record("A_net", "t·(2R − D_h)", $"{F(t)} × (2 × {F(radius)} − {F(dh)}) mm", ScalarValue.Of(aNet, "mm^2"), "Net section across the hole (single plane)", "spec");
        c.Record("A_v", "2·t·[a + (D_h/2)(1 − cos φ)]", $"2 × {F(t)} × [{F(aEff)} + ({F(dh)}/2) × (1 − cos {F(phi)}°)] mm", ScalarValue.Of(aV, "mm^2"), "Shear area of the two shear-out planes", "BTH-1 3-3.3");
        c.Record("A_b", "D_p·t", $"{F(dp)} × {F(t)} mm", ScalarValue.Of(aB, "mm^2"), "Projected bearing area of the pin", "spec");

        var modes = new List<(string Label, string Symbol, double U)>();
        string? dishFailure = null;
        double uDish = 0;
        double capBth = double.NaN;
        double uBth = 0;
        double capEn = double.NaN;
        double uEn = 0;

        // ---------------------------------------------------------------- BTH-1
        if (bth)
        {
            var cr = 1 - 0.275 * Math.Sqrt(Math.Max(0, 1 - ratio * ratio));
            var fya = fy / ny;
            var fua = fu / nu;
            var design = nd * kSvc * fS;
            var pT = cr * fya * aT;
            var pSp = cr * fua * aNet;
            var pV = fya / Math.Sqrt(3) * aV;
            var pB = kb * fya * aB;

            c.Record("Cr", "1 − 0.275·√(1 − D_p²/D_h²)", $"1 − 0.275 × √(1 − ({F(dp)}/{F(dh)})²)", new ScalarValue(cr), "Pin-clearance factor", "BTH-1 3-3.3");
            c.Record("Fy_a", "f_y/n_y", $"{F(fy)} MPa / {F(ny)}", ScalarValue.Of(fya, "MPa"), "Allowable stress on yield", "spec");
            c.Record("Fu_a", "f_u/n_u", $"{F(fu)} MPa / {F(nu)}", ScalarValue.Of(fua, "MPa"), "Allowable stress on ultimate", "spec");
            c.Record("D_bth", "N_d·k_svc·F_s", $"{F(nd)} × {F(kSvc)} × {F(fS / 1e3)} kN", ScalarValue.Of(design / 1e3, "kN"), "Design load on each lug", "BTH-1 3-3.3");
            c.Record("P_t", "C_r·F_y,a·A_t", $"{F(cr)} × {F(fya)} MPa × {F(aT)} mm²", ScalarValue.Of(pT / 1e3, "kN"), "Tension beyond the hole", "BTH-1 3-3.3");
            c.Record("P_sp", "C_r·F_u,a·A_net", $"{F(cr)} × {F(fua)} MPa × {F(aNet)} mm²", ScalarValue.Of(pSp / 1e3, "kN"), "Single-plane fracture of the net section", "BTH-1 3-3.3");
            c.Record("P_v", "(F_y,a/√3)·A_v", $"({F(fya)} MPa / √3) × {F(aV)} mm²", ScalarValue.Of(pV / 1e3, "kN"), "Double-plane shear-out", "BTH-1 3-3.3");
            c.Record("P_b", "k_b·F_y,a·A_b", $"{F(kb)} × {F(fya)} MPa × {F(aB)} mm²", ScalarValue.Of(pB / 1e3, "kN"), "Pin bearing", "BTH-1 3-3.3");

            var parts = new[] { ("tension beyond the hole", "u_t", pT), ("single-plane fracture", "u_sp", pSp), ("double-plane shear", "u_v", pV), ("bearing", "u_b", pB) };

            foreach (var (label, symbol, capacity) in parts)
            {
                var u = design / capacity;
                c.Record(symbol, "D/P", $"{F(design / 1e3)} kN / {F(capacity / 1e3)} kN", new ScalarValue(u), $"Utilisation, {label}", "BTH-1 3-3.3");
                modes.Add(($"BTH-1: {label}", symbol, u));
                uBth = Math.Max(uBth, u);
            }

            capBth = wll / 1e3 / uBth;
            c.Record("u_bth", "max(u_t, u_sp, u_v, u_b)", "governing BTH-1 section", new ScalarValue(uBth), "BTH-1 utilisation", "BTH-1 3-3.3");
            c.Record("cap_bth", "WLL/u_bth", $"{F(wll / 1e3)} kN / {F(uBth)}", ScalarValue.Of(capBth, "kN"), "Rated load the lug carries by the BTH-1 approach", "BTH-1 3-3.3");

            if (c.Has("dish_max"))
            {
                var dishMax = c.InputSi("dish_max");
                var dish = dh / t;
                uDish = dish / dishMax;
                c.Record("dish", "D_h/t", $"{F(dh)} / {F(t)}", new ScalarValue(dish), "Dishing ratio", "BTH-1 3-3.3");
                c.Record("u_dish", "(D_h/t)/dish_max", $"{F(dish)} / {F(dishMax)}", new ScalarValue(uDish), "Dishing-ratio utilisation", "BTH-1 3-3.3");

                if (uDish > 1)
                    dishFailure = $"dishing ratio {F(dish)} above the limit {F(dishMax)}";
            }
            else
            {
                c.Warn("dishing", "dish_max: no dishing-ratio limit entered, so the dishing check is not made; enter it from your own copy of the code.");
            }
        }

        // ---------------------------------------------------------------- EN 13155
        if (en)
        {
            var sections = new (string Label, string Symbol, double Y, double U)[]
            {
                ("tension beyond the hole", "t", fy * aT, fu * aT),
                ("single-plane fracture", "sp", fy * aNet, fu * aNet),
                ("double-plane shear", "v", fy / Math.Sqrt(3) * aV, fu / Math.Sqrt(3) * aV),
                ("bearing", "b", fy * aB, fu * aB),
            };

            foreach (var (label, symbol, y, u3) in sections)
            {
                var elastic = y / 2;
                var release = u3 / 3;
                var p = Math.Min(elastic, release);
                var u = fS / p;
                c.Record($"P_en_{symbol}", "min(Y/2, U/3)", $"min({F(y / 1e3)} kN / 2, {F(u3 / 1e3)} kN / 3)", ScalarValue.Of(p / 1e3, "kN"), $"EN 13155 capacity, {label}", "EN 13155");
                c.Record($"u_en_{symbol}", "F_s/P_EN", $"{F(fS / 1e3)} kN / {F(p / 1e3)} kN", new ScalarValue(u), $"EN 13155 utilisation, {label}", "EN 13155");
                var route = elastic <= release ? "2×WLL elastic" : "3×WLL no release";
                modes.Add(($"EN 13155 ({route}): {label}", $"u_en_{symbol}", u));

                uEn = Math.Max(uEn, u);
            }

            capEn = wll / 1e3 / uEn;
            c.Record("u_en", "max over the four sections", "governing EN 13155 section", new ScalarValue(uEn), "EN 13155 utilisation", "EN 13155");
            c.Record("cap_en", "WLL/u_en", $"{F(wll / 1e3)} kN / {F(uEn)}", ScalarValue.Of(capEn, "kN"), "Rated load the lug carries by the EN 13155 route", "EN 13155");
        }

        // ---------------------------------------------------------------- governing, spreader, proof load
        var lugUtil = modes.Max(m => m.U);
        var governing = modes.First(m => m.U >= lugUtil);
        var lugCapacity = wll / 1e3 / lugUtil;
        var uBeam = 0.0;

        c.Record("lug_util", "max over the modes used", "governing lug section", new ScalarValue(lugUtil), "Governing lug utilisation", "spec");
        c.Record("capacity", "WLL/lug_util", $"{F(wll / 1e3)} kN / {F(lugUtil)}", ScalarValue.Of(lugCapacity, "kN"), "Rated load the lug carries in the governing mode", "spec");

        if (beam)
        {
            if (c.Has("A_beam"))
            {
                var area = c.InputSi("A_beam") * 1e6;
                var uBb = bth ? nd * kSvc * nBeam / (area * (fy / ny)) : 0.0;
                var uBe = en ? Math.Max(2 * nBeam / (area * fy), 3 * nBeam / (area * fu)) : 0.0;
                uBeam = Math.Max(uBb, uBe);
                c.Record("u_beam", bth && en ? "max(N_d·k_svc·N_beam/(A·F_y,a), max(2N/(A·f_y), 3N/(A·f_u)))" : bth ? "N_d·k_svc·N_beam/(A·F_y,a)" : "max(2N_beam/(A·f_y), 3N_beam/(A·f_u))",
                    $"N_beam = {F(nBeam / 1e3)} kN on {F(area / 100)} cm²", new ScalarValue(uBeam), "Axial yield utilisation of the beam (buckling: S7)", "spec");
            }
            else
            {
                c.Warn("beam-area", "A_beam: no beam area entered, so the spreader utilisation is the lug utilisation only; link N_beam to the S7 member check for the beam.");
            }
        }

        var util = Math.Max(Math.Max(lugUtil, uBeam), uDish);
        c.Record("util", "max(lug_util, u_beam, u_dish)", $"max({F(lugUtil)}, {F(uBeam)}, {F(uDish)})", new ScalarValue(util), "Governing utilisation of the lug and the spreader", "spec");

        var governingText = dishFailure is not null
            ? $"dishing ratio limit exceeded ({dishFailure}); force-governing section: {governing.Label}"
            : uBeam > lugUtil ? "spreader beam axial yield" : governing.Label;

        c.Output("governing_mode", "Governing mode and section", new TextValue(governingText));
        c.Output("capacity", "Rated load the lug carries in the governing mode");
        c.Output("util", "Governing utilisation of the lug and the spreader");
        c.Output("lug_util", "Governing lug utilisation");

        if (bth)
        {
            c.Output("cap_bth", "Capacity, BTH-1 mode");
            c.Output("u_bth", "Utilisation, BTH-1 mode");
        }

        if (en)
        {
            c.Output("cap_en", "Capacity, EN 13155 mode");
            c.Output("u_en", "Utilisation, EN 13155 mode");
        }

        if (beam && c.Session.Find("u_beam") is not null)
            c.Output("u_beam", "Beam axial utilisation");

        foreach (var (symbol, text) in new[] { ("F_s", "Sling force on each lug"), ("F_ip", "In-plane component"), ("F_op", "Out-of-plane component"), ("N_beam", "Horizontal component per lug"), ("M_op", "Out-of-plane moment"), ("be_eff", "Effective edge distance used"), ("a_eff", "Edge distance along the load line"), ("Cr", "Pin-clearance factor C_r"), ("phi", "Shear-plane angle φ"), ("A_v", "Shear area"), ("A_t", "Tension area beyond the hole"), ("A_net", "Net section"), ("A_b", "Bearing area") })
        {
            if (c.Session.Find(symbol) is not null)
                c.Output(symbol, text);
        }

        if (bth)
        {
            foreach (var (symbol, text) in new[] { ("Fy_a", "Allowable stress on yield"), ("Fu_a", "Allowable stress on ultimate"), ("D_bth", "Design load on each lug"), ("P_t", "Tension beyond the hole"), ("P_sp", "Single-plane fracture"), ("P_v", "Double-plane shear"), ("P_b", "Bearing"), ("u_t", "Utilisation, tension"), ("u_sp", "Utilisation, fracture"), ("u_v", "Utilisation, shear"), ("u_b", "Utilisation, bearing"), ("dish", "Dishing ratio"), ("u_dish", "Dishing utilisation") })
            {
                if (c.Session.Find(symbol) is not null)
                    c.Output(symbol, text);
            }
        }

        if (en)
        {
            foreach (var s in new[] { "t", "sp", "v", "b" })
            {
                c.Output($"P_en_{s}", $"EN 13155 capacity, section {s}");
                c.Output($"u_en_{s}", $"EN 13155 utilisation, section {s}");
            }
        }

        // The proof load and the checklist.
        string proofText;

        if (c.Has("k_proof"))
        {
            var proof = c.InputSi("k_proof") * wll / 1e3;
            c.Record("proof_load", "k_proof·WLL", $"{F(c.InputSi("k_proof"))} × {F(wll / 1e3)} kN", ScalarValue.Of(proof, "kN"), "Proof-load value", "spec");
            c.Output("proof_load", "Proof-load value");
            proofText = $"proof load {F(proof)} kN";
        }
        else
        {
            c.Warn("proof", "k_proof: no proof-load factor entered, so no proof load is given; enter it from your own copy of the code or procedure.");
            proofText = "proof load not set (enter k_proof)";
        }

        c.Output("sheet_notice", "Status of this sheet", new TextValue(DesignNotice + " It does not replace the thorough examination by a competent person (LOLER reg 9) or the marking, testing and certification that the regulations require."));
        c.Output("checklist", "Marking and certification checklist", new TextValue(
            "(1) mark the appliance with its rated load and a unique identification (LOLER reg 7); " +
            "(2) thorough examination by a competent person before first use and at the intervals required (LOLER reg 9) — this sheet is not one; " +
            $"(3) {proofText}, test certificate where the route requires it; " +
            "(4) material certificates for the lug plate and the pin, and weld procedure and inspection records for the lug welds; " +
            $"(5) pin, shackle and sling ratings at least {F(fS / 1e3)} kN at {F(thetaD)}° (PUWER reg 4); " +
            "(6) record the design basis: mode, Design Category, service class and the user records used; " +
            "(7) keep the report of the thorough examination with the appliance."));

        // ---------------------------------------------------------------- checks
        foreach (var (label, symbol, _) in modes)
            c.CheckUtilisation(label, symbol, label.StartsWith("BTH-1", StringComparison.Ordinal) ? "BTH-1 3-3.3" : "EN 13155");

        if (bth && c.Session.Find("u_dish") is not null)
            c.CheckUtilisation("BTH-1: dishing ratio", "u_dish", "BTH-1 3-3.3");

        if (beam && c.Session.Find("u_beam") is not null)
            c.CheckUtilisation("Spreader beam axial yield", "u_beam", "spec");

    }

    private static CalcExpectedValue Ex(string symbol, double value, string unit) => new(symbol, value, unit, 1e-9);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue N(string symbol, double value) => CalcInputValue.Of(symbol, value);

    private static CalcInputValue Choice(string symbol, string value) => CalcInputValue.Choice(symbol, value);

    /// <summary>The illustrative user-record factors of the known-answer cases: arbitrary round values, not values from any standard.</summary>
    private static CalcInputValue[] Factors() => [N("Nd", 1.8), N("n_y", 1.6), N("n_u", 2.2), N("k_b", 1.2), N("dish_max", 40), N("k_proof", 1.5)];

    private static CalcInputValue[] LugA() =>
    [
        Choice("mode", ModeBth1), Q("WLL", 150, "kN"), Choice("n_lugs", "2"), Q("theta", 30, "deg"), Q("beta", 5, "deg"), CalcInputValue.Switch("beam", false), N("n_cycles", 1000),
        Q("t", 20, "mm"), Q("Dh", 50, "mm"), Q("Dp", 45, "mm"), Q("R", 65, "mm"), Q("be", 35, "mm"), Q("h", 150, "mm"), Q("f_y", 355, "MPa"), Q("f_u", 470, "MPa"), .. Factors(),
    ];

    private static CalcInputValue[] LugB() =>
    [
        Choice("mode", ModeEn), Q("WLL", 100, "kN"), Choice("n_lugs", "2"), Q("theta", 20, "deg"), Q("beta", 0, "deg"), CalcInputValue.Switch("beam", false), N("n_cycles", 1000),
        Q("t", 30, "mm"), Q("Dh", 40, "mm"), Q("Dp", 38, "mm"), Q("R", 40, "mm"), Q("be", 30, "mm"), Q("f_y", 235, "MPa"), Q("f_u", 360, "MPa"),
    ];

    private static CalcInputValue[] LugC() =>
    [
        Choice("mode", ModeBoth), Q("WLL", 400, "kN"), Choice("n_lugs", "4"), Q("theta", 25, "deg"), Q("beta", 3, "deg"), CalcInputValue.Switch("beam", true), N("n_cycles", 1000),
        Q("t", 30, "mm"), Q("Dh", 70, "mm"), Q("Dp", 66, "mm"), Q("R", 90, "mm"), Q("be", 50, "mm"), Q("a", 60, "mm"), Q("h", 200, "mm"), Q("f_y", 355, "MPa"), Q("f_u", 470, "MPa"), Q("A_beam", 120, "cm^2"), .. Factors(),
    ];

    private static CalcInputValue[] LugE() => [.. LugB().Select(v => v.Symbol == "mode" ? Choice("mode", ModeBoth) : v), .. Factors()];

    private static CalcInputValue[] LugD() => [.. LugA().Select(v => v.Symbol == "Dp" ? Q("Dp", 30, "mm") : v)];

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
