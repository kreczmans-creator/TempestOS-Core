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

    /// <summary>Printed on the sheet (ADR-0173): the two checks and the attestation are made by whoever is signed in; TempestOS does not check who they are.</summary>
    public const string SignatureNotice = "Signatures are not authenticated by TempestOS: the signed-in names of the author, the checkers and the competent person are as entered into this installation.";

    private const string GroupLoads = "Mode and loading";
    private const string GroupLug = "Lug geometry";
    private const string GroupMaterial = "Material";
    private const string GroupFactors = "Design factors (user records, no defaults)";
    private const string GroupBeam = "Spreader or lifting beam";
    private const string GroupAttest = "Competent-person attestation (required to check and issue)";

    public CalcTemplate Template { get; } = new(
        Id, "1.1.1", CalcDiscipline.Structural, "Lifting lugs and spreader beams",
        DesignNotice + " Pin-loaded lifting lugs and the lug loads of spreader or lifting beams, checked by the BTH-1 allowable-stress approach (clause 3-3.3), by the EN 13155 proof route (2× the rated load without permanent set, 3× without release) or both: " +
        "sling force from the rated load, sling angle and out-of-plane angle (with the automatic 6° tilt cases for a horizontal beam); tension beyond the hole, single-plane fracture beyond the hole, shear-out on two planes, pin bearing and dishing; capacity and utilisation per mode, the governing mode, the proof load and a marking and certification checklist. " +
        "Scope notes (LOLER and PUWER): this sheet does not replace the thorough examination and inspection of lifting equipment by a competent person (LOLER reg 9), the marking, test and certification that the regulations and the standards require, the planning of the lifting operation, the weld, bolt and beam checks of the linked sheets, or the pin, shackle and sling ratings. " +
        "It is available only while the lifting feature is enabled; its issue needs a competent-person attestation and a second independent check. " + SignatureNotice,
        "Sling force: F_v = WLL/n_eff (n_eff = n_lugs, at most 3); F_s = F_v/(cos θ·cos β); F_ip = F_s·cos β; F_op = F_s·sin β; N_beam = F_v·tan θ; M_op = F_op·h\n" +
        "Horizontal beam, automatic 6° tilt: pitch (θ ± 6°, load redistributed, the sling at θ − 6° takes 2·F_v·sin(θ + 6°)/sin 2θ, and the force along the tilted beam is F_v·sin(θ + 6°)/cos θ); roll (β + 6°); the larger F_s governs, F_ip, F_op and N_beam are the envelopes\n" +
        "Lug section geometry: side = min(b_e, R − D_h/2); b_e,eff = min(side, 4t, 0.6·(f_u/f_y)·√(D_h/side)·side); a_eff = a (default R − D_h/2); A_t = 2·t·b_e,eff; A_fr = [1.13·a_eff + 0.92·side/(1 + side/D_h)]·t; φ = 55·D_p/D_h (degrees); A_v = 2·t·[a_eff + (D_p/2)(1 − cos φ)]; A_b = D_p·t\n" +
        "Pin clearance: C_r = 1 − 0.275·√(1 − D_p²/D_h²)\n" +
        "BTH-1 (3-3.3): F_a = min(f_y/n_y, f_u/n_u); design load D = N_d·k_svc·F_s; P_t = C_r·F_a·A_t; P_sp = C_r·F_a·A_fr; P_v = F_a·A_v/√3; P_b = k_b·F_a·A_b; u = D/P for each; dishing D_h/t ≤ dish_max\n" +
        "EN 13155 (2× elastic, 3× no release): P_EN = min(Y/2, U/3) for each section, Y with f_y and U with f_u (shear with f/√3; fracture area without C_r); u = F_s/P_EN\n" +
        "Lug capacity and proof load: WLL_cap = WLL/u_lug for the governing section of each mode (smaller of the modes used); beam: u_beam from N_beam on A_beam; proof load = k_proof·WLL",
        [
            new CalcInputSpec("mode", "Design mode", CalcInputKind.Choice, Choices: [ModeBth1, ModeEn, ModeBoth], DefaultChoice: ModeBoth, Group: GroupLoads),
            new CalcInputSpec("WLL", "Rated load (working load limit) of the appliance, carried by all its lugs", CalcInputKind.Quantity, "kN", Default: 150, Min: 0.5, Max: 10000, HardMin: 0.5, HardMax: 10000, Group: GroupLoads),
            new CalcInputSpec("n_lugs", "Number of lugs sharing the load (a fourth is not relied on: three share)", CalcInputKind.Choice, Choices: ["1", "2", "3", "4"], DefaultChoice: "2", Group: GroupLoads),
            new CalcInputSpec("theta", "Sling angle θ from the vertical, measured in the plane of the lug (the out-of-plane angle β is applied on top: F_s = F_v/(cos θ·cos β))", CalcInputKind.Quantity, "deg", Default: 30, Min: 0, Max: 60, HardMin: 0, HardMax: 60, Group: GroupLoads),
            new CalcInputSpec("beta", "Out-of-plane angle β of the sling out of the lug plane", CalcInputKind.Quantity, "deg", Default: 0, Min: 0, Max: 30, HardMin: 0, HardMax: 30, Group: GroupLoads),
            new CalcInputSpec("beam", "The lugs are on a horizontal spreader or lifting beam (adds the automatic 6° pitch and roll tilt cases)", CalcInputKind.Boolean, Default: 0, Group: GroupLoads),
            new CalcInputSpec("n_cycles", "Load cycles in the life of the lug (the 2×/3× route covers up to 16,000; beyond that a fatigue assessment is needed and the sheet does not run)", CalcInputKind.Number, Default: 1000, Min: 1, Max: MaxCycles, HardMin: 1, HardMax: MaxCycles, Group: GroupLoads),

            new CalcInputSpec("t", "Lug plate thickness t", CalcInputKind.Quantity, "mm", Default: 20, Min: 5, Max: 150, HardMin: 5, HardMax: 150, Group: GroupLug),
            new CalcInputSpec("Dh", "Hole diameter D_h", CalcInputKind.Quantity, "mm", Default: 50, Min: 10, Max: 500, HardMin: 10, HardMax: 500, Group: GroupLug),
            new CalcInputSpec("Dp", "Pin diameter D_p (not larger than the hole)", CalcInputKind.Quantity, "mm", Default: 45, Min: 8, Max: 500, HardMin: 8, HardMax: 500, Group: GroupLug),
            new CalcInputSpec("R", "Lug end radius R about the hole centre (the plate is 2R wide at the hole)", CalcInputKind.Quantity, "mm", Default: 65, Min: 15, Max: 600, HardMin: 15, HardMax: 600, Group: GroupLug),
            new CalcInputSpec("be", "Edge distance b_e from the hole edge to the plate edge on each side, across the load line (limited to R − D_h/2; the effective width for tension is reduced further by 4t and f_u/f_y)", CalcInputKind.Quantity, "mm", Default: 35, Min: 5, Max: 400, HardMin: 5, HardMax: 400, Group: GroupLug),
            new CalcInputSpec("a", "End distance a from the hole edge to the plate edge along the load line (blank: R − D_h/2, a round-ended lug)", CalcInputKind.Quantity, "mm", Min: 5, Max: 600, HardMin: 5, HardMax: 600, IsOptional: true, Group: GroupLug),
            new CalcInputSpec("h", "Distance h from the hole centre to the weld or base, for the out-of-plane moment (blank: no moment given)", CalcInputKind.Quantity, "mm", Min: 1, Max: 3000, HardMin: 1, HardMax: 3000, IsOptional: true, Group: GroupLug),

            new CalcInputSpec("f_y", "Yield strength f_y of the lug plate for its thickness", CalcInputKind.Quantity, "MPa", Default: 355, Min: 100, Max: 1100, HardMin: 100, HardMax: 1100, Group: GroupMaterial, MaterialProperty: "f_y"),
            new CalcInputSpec("f_u", "Ultimate strength f_u of the lug plate", CalcInputKind.Quantity, "MPa", Default: 470, Min: 150, Max: 1400, HardMin: 150, HardMax: 1400, Group: GroupMaterial, MaterialProperty: "f_u"),

            new CalcInputSpec("design_category", "Design Category the design factor belongs to (a label, printed on the sheet)", CalcInputKind.Text, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("service_class", "Service Class the service factor belongs to (a label, printed on the sheet)", CalcInputKind.Text, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("Nd", "Design factor N_d of the Design Category (BTH-1 mode; user record)", CalcInputKind.Number, Min: 1, Max: 5, HardMin: 1, HardMax: 5, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("k_svc", "Service-class factor on the load (blank: 1.0; user record)", CalcInputKind.Number, Min: 1, Max: 3, HardMin: 1, HardMax: 3, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("n_y", "Divisor n_y on f_y; the allowable stress is F_a = min(f_y/n_y, f_u/n_u) (BTH-1 mode; user record)", CalcInputKind.Number, Min: 1, Max: 4, HardMin: 1, HardMax: 4, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("n_u", "Divisor n_u on f_u; the allowable stress is F_a = min(f_y/n_y, f_u/n_u) (BTH-1 mode; user record)", CalcInputKind.Number, Min: 1, Max: 6, HardMin: 1, HardMax: 6, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("k_b", "Bearing multiple k_b of the allowable stress on the projected pin area (BTH-1 mode; user record)", CalcInputKind.Number, Min: 0.5, Max: 3, HardMin: 0.5, HardMax: 3, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("dish_max", "Dishing-ratio limit on D_h/t (blank: the dishing check is not made; user record)", CalcInputKind.Number, Min: 1, Max: 100, HardMin: 1, HardMax: 100, IsOptional: true, Group: GroupFactors),
            new CalcInputSpec("k_proof", "Proof-load factor on the rated load (blank: no proof load given; user record)", CalcInputKind.Number, Min: 1, Max: 3, HardMin: 1, HardMax: 3, IsOptional: true, Group: GroupFactors),

            new CalcInputSpec("A_beam", "Gross cross-section area of the spreader or lifting beam, for its axial utilisation (blank: lug utilisation only)", CalcInputKind.Quantity, "cm^2", Min: 1, Max: 50000, HardMin: 1, HardMax: 50000, IsOptional: true, Group: GroupBeam),

            new CalcInputSpec(CalcAttestation.NameSymbol, "Competent person attesting the design (not the author; required to check and issue)", CalcInputKind.Text, IsOptional: true, Group: GroupAttest),
            new CalcInputSpec(CalcAttestation.QualificationSymbol, "Their qualification and standing", CalcInputKind.Text, IsOptional: true, Group: GroupAttest),
            new CalcInputSpec(CalcAttestation.StatementSymbol, "Attestation: what the competent person has reviewed (at least 20 characters)", CalcInputKind.Text, IsOptional: true, Group: GroupAttest),
        ],
        [
            new CalcAssumption("A design calculation, not a thorough examination: it checks the lug plate at the pin hole against the user's chosen criteria and nothing else.", "LOLER reg 9 and PUWER duties are the owner's and the competent person's."),
            new CalcAssumption("Nominal-stress screening of four sections (tension beyond the hole, single-plane fracture beyond the hole, shear-out, bearing) on a plate loaded through a pin; no stress-concentration factor, no fatigue (the cycle count is limited to 16,000), no weld, bolt or beam check, which are made on the linked sheets.", "The notch-stress route of EN 13001-3-1 is cited, not implemented."),
            new CalcAssumption("The load on the hole is the resultant sling force F_s whatever the out-of-plane angle, which is conservative; the out-of-plane component and its moment are outputs for the weld and base checks.", "F_op = F_s·sin β."),
            new CalcAssumption("Equal sharing between the lugs, with a fourth lug not relied on (three share). The share of a statically indeterminate set is not computed.", "Conservative convention; replace with the actual share if it is known."),
            new CalcAssumption("A horizontal beam adds the automatic 6° tilt as two cases: pitch (sling angles θ ± 6° with the load redistributed between the lugs by statics, the slings meeting at the hook) and roll (out-of-plane angle + 6°), the larger sling force governing. N_beam is the force along the beam axis: in the pitch case each sling stays at θ to the beam's normal, so the more loaded lug sends F_v·sin(θ + 6°)/cos θ along the beam. With θ ≤ 6° the pitch case puts the whole load on one lug, with W·sin 6° along the beam; with more than two lugs the two-lug redistribution is applied to each lug's share.", "Owner's specification (6° tilt of a horizontal beam); EN 13155."),
            new CalcAssumption("The allowable stress is the one lesser value min(f_y/n_y, f_u/n_u) for every BTH-1 section, as in the published worked example the sheet is checked against; the pin-clearance factor C_r is applied as stated from D_p/D_h < 1 with no cut-off.", "Conservative; owner checklist."),
            new CalcAssumption("Signatures are not authenticated by TempestOS: the platform asks for no password, so the author, the two checkers and the competent person are as entered into this installation; any person can switch to another name.", "ADR-0173; the feature stays off until this is addressed."),
            new CalcAssumption("The design factor, service-class factor, allowable-stress divisors, bearing multiple, dishing limit and proof-load factor are the user's records from their own licensed copy of the code; none is shipped and none has a default.", "ASME BTH-1 and EN 13155 text and tables are not shipped."),
            new CalcAssumption("The equations are written out in the spec document in the author's notation (the single-plane fracture and effective-width relations and the 1.13 and 0.92 constants follow the published course example); the owner compares them with BTH-1 (2023 or later) and EN 13155+A1 before the feature is ever enabled.", "Owner test pack, S8 review checklist."),
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
                new CalcReferenceSource("Analytic: the equations of the spec document (docs/calcs/structural/struct.lifting.md) evaluated independently in python (s8_ref2); the design factors are illustrative user-record values, not values from any standard", "struct.lifting spec 1.1.0",
                    "F_v = 75 kN, F_s = 75/(cos 30° · cos 5°) = 86.9333 kN; C_r = 1 − 0.275·√(1 − 0.9²) = 0.880130, φ = 55 × 0.9 = 49.5°; side distance 35 mm, end distance a = 40 mm; b_e,eff = min(35, 4·20, 0.6·(470/355)·√(50/35)·35) = 33.2307 mm, A_t = 2·20·33.2307 = 1329.23 mm², A_fr = (1.13·40 + 0.92·35/(1 + 35/50))·20 = 1282.82 mm², A_v = 2·20·[40 + 22.5·(1 − cos 49.5°)] = 1915.50 mm², A_b = 45·20 = 900 mm²; " +
                    "F_a = min(355/1.6, 470/2.2) = 213.636 MPa; D = 1.8 × 86.9333 = 156.48 kN; capacities and utilisations as listed (bearing governs); capacity 150/u kN; dishing 50/20 = 2.5; proof 1.5 × 150 = 225 kN"),
                [.. LugA()],
                [
                    Ex("F_s", 86.93334801353069, "kN"), Ex("F_ip", 86.60254037844385, "kN"), Ex("F_op", 7.576740515659921, "kN"), Ex("N_beam", 43.301270189221924, "kN"),
                    Ex("M_op", 1.136511077348988, "kN*m"), Ex("Cr", 0.8801302790526315, ""), Ex("phi", 49.5, "deg"), Ex("be_eff", 33.230722180649195, "mm"),
                    Ex("A_t", 1329.2288872259678, "mm^2"), Ex("A_fr", 1282.8235294117646, "mm^2"), Ex("A_v", 1915.4967565028346, "mm^2"), Ex("A_b", 900, "mm^2"),
                    Ex("Fa", 213.63636363636363, "MPa"), Ex("D_bth", 156.48002642435526, "kN"), Ex("P_t", 249.93202635287938, "kN"), Ex("P_sp", 241.2065275139706, "kN"),
                    Ex("P_v", 236.26313952700923, "kN"), Ex("P_b", 230.7272727272727, "kN"), Ex("u_t", 0.6260903362717545, ""), Ex("u_sp", 0.6487387718613543, ""),
                    Ex("u_v", 0.6623124823348363, ""), Ex("u_b", 0.6782034242190339, ""), Ex("u_bth", 0.6782034242190339, ""), Ex("cap_bth", 221.17257837901408, "kN"),
                    Ex("dish", 2.5, ""), Ex("u_dish", 0.0625, ""), Ex("lug_util", 0.6782034242190339, ""), Ex("capacity", 221.17257837901408, "kN"),
                    Ex("util", 0.6782034242190339, ""), Ex("proof_load", 225.0, "kN"),
                ]),
            new CalcTestCase("ka-en13155-hand", "EN 13155 mode, two lugs at 20°, 100 kN, f_y 235 / f_u 360: the 2×WLL elastic route governs the shear-out",
                new CalcReferenceSource("Analytic: 2× the rated load without permanent set (capacity at f_y) and 3× without release (capacity at f_u) on the four sections of the spec, evaluated independently in python (s8_ref2)", "struct.lifting spec 1.1.0, EN 13155 mode",
                    "F_s = 50/cos 20° = 53.2089 kN; side = end distance = 20 mm; b_e,eff = min(20, 120, 0.6·(360/235)·√(40/20)·20 = 26.0) = 20 mm, A_t = 2·30·20 = 1200 mm²; A_fr = (1.13·20 + 0.92·20/(1 + 20/40))·30 = 1046 mm²; φ = 55 × 0.95 = 52.25°, A_v = 2·30·[20 + 19·(1 − cos 52.25°)] = 1642.07 mm²; A_b = 38·30 = 1140 mm²; P_EN = min(Y/2, U/3) per section"),
                [.. LugB()],
                [
                    Ex("F_s", 53.2088886237956, "kN"), Ex("F_ip", 53.2088886237956, "kN"), Ex("F_op", 0.0, "kN"), Ex("N_beam", 18.198511713310115, "kN"),
                    Ex("be_eff", 20.0, "mm"), Ex("A_t", 1200.0, "mm^2"), Ex("A_fr", 1046.0, "mm^2"), Ex("A_v", 1642.0723007607278, "mm^2"),
                    Ex("A_b", 1140, "mm^2"), Ex("P_en_t", 141.0, "kN"), Ex("u_en_t", 0.3773680044240823, ""), Ex("P_en_sp", 122.905, "kN"),
                    Ex("u_en_sp", 0.43292696492246535, ""), Ex("P_en_v", 111.39597897258153, "kN"), Ex("u_en_v", 0.4776553796155621, ""), Ex("P_en_b", 133.95, "kN"),
                    Ex("u_en_b", 0.39722947834113925, ""), Ex("u_en", 0.4776553796155621, ""), Ex("cap_en", 209.35595885151417, "kN"), Ex("lug_util", 0.4776553796155621, ""),
                    Ex("capacity", 209.35595885151417, "kN"), Ex("util", 0.4776553796155621, ""),
                ]),
            new CalcTestCase("ka-both-beam-tilt", "Both modes on a horizontal beam: four lugs (three share), 400 kN at 25° and 3°, with the automatic 6° tilt (pitch redistribution and roll), 120 cm² beam",
                new CalcReferenceSource("Analytic: the spec equations with F_v = 400/3 = 133.333 kN; nominal, pitch (sling angles 31° and 19°, F_s = 2·F_v·sin 31°/sin 50°/cos 3°) and roll (β + 6° = 9°) cases evaluated independently in python (s8_ref2); the pitch case governs", "struct.lifting spec 1.1.0, both modes",
                    "F_s,nom = 147.319 kN, F_s,pitch = 179.535 kN, F_s,roll = 148.951 kN; the pitch case governs the lug checks; F_ip, F_op and N_beam are the envelopes over the three cases; BTH-1 bearing and EN 13155 bearing (3× route) govern; beam axial utilisation on 120 cm²"),
                [.. LugC()],
                [
                    Ex("F_s", 179.5352354061199, "kN"), Ex("F_ip", 179.2891886056664, "kN"), Ex("F_op", 23.301052554843444, "kN"), Ex("N_beam", 75.77088523742714, "kN"),
                    Ex("M_op", 4.660210510968689, "kN*m"), Ex("Cr", 0.9083707559381453, ""), Ex("phi", 51.857142857142854, "deg"), Ex("be_eff", 46.99533799532652, "mm"),
                    Ex("A_t", 2819.7202797195914, "mm^2"), Ex("A_fr", 2839.0, "mm^2"), Ex("A_v", 4357.103829590557, "mm^2"), Ex("A_b", 1980, "mm^2"),
                    Ex("Fa", 213.63636363636363, "MPa"), Ex("D_bth", 323.1634237310158, "kN"), Ex("P_t", 547.1978080685508, "kN"), Ex("P_sp", 550.9392503504298, "kN"),
                    Ex("P_v", 537.4183101743596, "kN"), Ex("P_b", 507.5999999999999, "kN"), Ex("u_t", 0.5905787979518573, ""), Ex("u_sp", 0.586568162506964, ""),
                    Ex("u_v", 0.6013256668277062, ""), Ex("u_b", 0.6366497709436877, ""), Ex("u_bth", 0.6366497709436877, ""), Ex("cap_bth", 628.2889247051663, "kN"),
                    Ex("P_en_t", 441.7561771560693, "kN"), Ex("u_en_t", 0.40641250692164377, ""), Ex("P_en_sp", 444.7766666666667, "kN"), Ex("u_en_sp", 0.4036525493831059, ""),
                    Ex("P_en_v", 394.1067607945304, "kN"), Ex("u_en_v", 0.4555497475967471, ""), Ex("P_en_b", 310.2, "kN"), Ex("u_en_b", 0.5787725190397159, ""),
                    Ex("u_en", 0.5787725190397159, ""), Ex("cap_en", 691.1178171756832, "kN"), Ex("u_beam", 0.053200834315640336, ""), Ex("lug_util", 0.6366497709436877, ""),
                    Ex("capacity", 628.2889247051663, "kN"), Ex("util", 0.6366497709436877, ""), Ex("proof_load", 600.0, "kN"),
                ]),
            new CalcTestCase("ka-both-shear-governs", "Both modes, shear-out governs: the geometry of the EN 13155 case with the BTH-1 user-record factors",
                new CalcReferenceSource("Analytic: the spec equations, evaluated independently in python (s8_ref2)", "struct.lifting spec 1.1.0, both modes",
                    "F_s = 53.2089 kN; D = 1.8 × 53.2089 = 95.776 kN; F_a = min(235/1.6, 360/2.2) = 146.875 MPa; C_r = 0.914131; capacities and utilisations as listed"),
                [.. LugE()],
                [
                    Ex("Cr", 0.9141312775220221, ""), Ex("phi", 52.25, "deg"), Ex("be_eff", 20.0, "mm"), Ex("A_t", 1200.0, "mm^2"),
                    Ex("A_fr", 1046.0, "mm^2"), Ex("A_v", 1642.0723007607278, "mm^2"), Ex("A_b", 1140, "mm^2"), Ex("Fa", 146.875, "MPa"),
                    Ex("D_bth", 95.7759995228321, "kN"), Ex("P_t", 161.11563766325636, "kN"), Ex("P_sp", 140.43913082980512, "kN"), Ex("P_v", 139.24497371572693, "kN"),
                    Ex("P_b", 200.925, "kN"), Ex("u_t", 0.5944550194625492, ""), Ex("u_sp", 0.681975165731414, ""), Ex("u_v", 0.6878237466464093, ""),
                    Ex("u_b", 0.47667537400936716, ""), Ex("u_bth", 0.6878237466464093, ""), Ex("cap_bth", 145.38608253577374, "kN"), Ex("P_en_t", 141.0, "kN"),
                    Ex("u_en_t", 0.3773680044240823, ""), Ex("P_en_sp", 122.905, "kN"), Ex("u_en_sp", 0.43292696492246535, ""), Ex("P_en_v", 111.39597897258153, "kN"),
                    Ex("u_en_v", 0.4776553796155621, ""), Ex("P_en_b", 133.95, "kN"), Ex("u_en_b", 0.39722947834113925, ""), Ex("u_en", 0.4776553796155621, ""),
                    Ex("cap_en", 209.35595885151417, "kN"), Ex("lug_util", 0.6878237466464093, ""), Ex("capacity", 145.38608253577374, "kN"), Ex("util", 0.6878237466464093, ""),
                    Ex("proof_load", 150.0, "kN"),
                ]),
            new CalcTestCase("ka-pdhonline-s106", "PDHonline S106 worked example: 1.25 in plate, 1.25 in hole, 0.75 in pin, F_u 58 ksi, F_y 36 ksi, allowable stress min(F_u/5, F_y/3)",
                new CalcReferenceSource("PDHonline course S106 (Design/Evaluation of Overhead Lifting Lugs), worked example, Failure Modes 1.1, 2, 3.1 and 4.1 (course notes: a secondary source)", null,
                    "Inputs as published: t = 1.25 in, hole d = 1.25 in, pin 0.75 in, plate 3.5 in wide (side distance a = 1.125 in), end distance e = 1.125 in, F_u 58 ksi, F_y 36 ksi, harness factors n_y = 3, n_u = 5, N_d = k_b = 1 (so F_a = min(F_u/5, F_y/3) = 11.6 ksi), unit load. " +
                    "Published values: C_r = 0.78, φ = 33°, A_v = 2.964 in², tension 25.448 k, bearing 10.875 k, double-plane shear 19.849 k, single-plane fracture 20.539 k; all reproduced by the equations of the spec recomputed in python (s8_ref2) to the rounding of the published three decimals. To be checked against the owner's copy of the course", Secondary: true),
                [.. S106()],
                [
                    new CalcExpectedValue("Cr", 0.78, "", 1e-9), new CalcExpectedValue("phi", 33.0, "deg", 1e-9), new CalcExpectedValue("A_v", 2.964, "in^2", 3e-4), new CalcExpectedValue("P_t", 25.448, "kip", 3e-5),
                    new CalcExpectedValue("P_b", 10.875, "kip", 1e-9), new CalcExpectedValue("P_v", 19.849, "kip", 3e-5), new CalcExpectedValue("P_sp", 20.539, "kip", 3e-5),
                ]),
        ],
        Category: "Lifting",
        OutputPorts:
        [
            new CalcOutputPort("capacity", "kN", "Rated load the lug carries in the governing mode (lug.capacity)"),
            new CalcOutputPort("governing_mode", "", "The governing mode and section (lug.governing_mode)"),
            new CalcOutputPort("util", "", "Governing utilisation of the lug and the spreader beam (spreader.util)"),
            new CalcOutputPort("F_s", "kN", "Sling force on each lug (resultant), for the pin, shackle and sling"),
            new CalcOutputPort("F_ip", "kN", "In-plane component of the sling force (→ M6 weld group, S4)"),
            new CalcOutputPort("F_op", "kN", "Out-of-plane component of the sling force (→ M6 weld group, M3 bolt group)"),
            new CalcOutputPort("N_beam", "kN", "Axial force along the spreader beam at the more loaded lug, the larger of the level and the 6° pitched case (→ S7 member check)"),
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
        var fV = wll / nEff;
        var tau = TiltDegrees;
        var cases = new List<(string Name, double Fs, double Fip, double Fop, double N)>();

        // The sling leaves the lug at θ in the lug plane and β out of it: F_v = F_s·cos θ·cos β, F_ip = F_s·cos β, F_op = F_s·sin β, N = F_v·tan θ.
        (double Fs, double Fip, double Fop, double N) Plain(double th, double be2)
        {
            var fs = fV / (Math.Cos(th * Math.PI / 180) * Math.Cos(be2 * Math.PI / 180));
            return (fs, fs * Math.Cos(be2 * Math.PI / 180), fs * Math.Sin(be2 * Math.PI / 180), fV * Math.Tan(th * Math.PI / 180));
        }

        var nominal = Plain(theta, beta);
        cases.Add(("nominal", nominal.Fs, nominal.Fip, nominal.Fop, nominal.N));

        if (nLugs == 4)
            c.Warn("sharing", "n_lugs: four lugs — three are taken to share the load; replace the sharing with the actual share if it is known (owner checklist).");

        c.Record("n_eff", "min(n_lugs, 3)", $"min({nLugs}, 3)", new ScalarValue(nEff), "Lugs sharing the load (a fourth is not relied on)", "spec");
        c.Record("F_v", "WLL/n_eff", $"{F(wll / 1e3)} kN / {nEff}", ScalarValue.Of(fV / 1e3, "kN"), "Vertical load on each lug", "spec");
        c.Record("F_s_nom", "F_v/(cos θ·cos β)", $"{F(fV / 1e3)} kN / (cos {F(theta)}° × cos {F(beta)}°)", ScalarValue.Of(nominal.Fs / 1e3, "kN"), "Sling force on each lug, untilted", "spec");

        if (beam)
        {
            // Pitch: the beam tilts about a horizontal axis across it, so the slings leave at θ + 6° and θ − 6°. The slings meet at the hook, so their
            // horizontal components are equal and opposite and the vertical components carry the load: the sling at θ − 6° takes F = W·sin(θ + 6°)/sin 2θ.
            // The triangle is rigid with the beam, so each sling stays at θ to the beam's normal and sends F·sin θ along the beam: F_v·sin(θ + 6°)/cos θ.
            double pitchPlane, pitchN;

            if (nEff >= 2 && theta > tau)
            {
                pitchPlane = fV * 2 * Math.Sin((theta + tau) * Math.PI / 180) / Math.Sin(2 * theta * Math.PI / 180);
                pitchN = fV * Math.Sin((theta + tau) * Math.PI / 180) / Math.Cos(theta * Math.PI / 180);
                c.Record("F_s_pitch", "2·F_v·sin(θ + 6°)/sin 2θ / cos β", $"2 × {F(fV / 1e3)} kN × sin({F(theta + tau)}°) / sin({F(2 * theta)}°) / cos({F(beta)}°)",
                    ScalarValue.Of(pitchPlane / Math.Cos(beta * Math.PI / 180) / 1e3, "kN"), "Sling force on the more loaded lug when the beam pitches 6°", "EN 13155");
            }
            else if (nEff >= 2)
            {
                pitchPlane = wll;
                pitchN = wll * Math.Sin(tau * Math.PI / 180);
                c.Warn("tilt", $"theta: a sling angle of {F(theta)}° is no more than the 6° tilt, so the tilted beam can put the whole load on one vertical sling; F_s is taken as the whole rated load.");
                c.Record("F_s_pitch", "WLL / cos β (θ ≤ 6°: the whole load on one sling)", $"{F(wll / 1e3)} kN / cos({F(beta)}°)", ScalarValue.Of(pitchPlane / Math.Cos(beta * Math.PI / 180) / 1e3, "kN"), "Sling force on the more loaded lug when the beam pitches 6°", "EN 13155");
            }
            else
            {
                pitchPlane = fV / Math.Cos(theta * Math.PI / 180);
                pitchN = fV * Math.Tan(theta * Math.PI / 180);
                c.Warn("tilt", "n_lugs: one lug on a beam has no other lug to take load off it, so the pitch tilt changes nothing here; check the lug it hangs from.");
                c.Record("F_s_pitch", "F_v/(cos θ·cos β)", $"{F(fV / 1e3)} kN / (cos {F(theta)}° × cos {F(beta)}°)", ScalarValue.Of(pitchPlane / Math.Cos(beta * Math.PI / 180) / 1e3, "kN"), "Sling force when the beam pitches 6° (a single lug)", "EN 13155");
            }

            var pitchFs = pitchPlane / Math.Cos(beta * Math.PI / 180);
            cases.Add(("pitch", pitchFs, pitchPlane, pitchFs * Math.Sin(beta * Math.PI / 180), pitchN));

            // Roll: the beam tilts about its own axis, so the out-of-plane angle grows by 6°.
            var roll = Plain(theta, beta + tau);
            cases.Add(("roll", roll.Fs, roll.Fip, roll.Fop, roll.N));
            c.Record("F_s_roll", "F_v/(cos θ·cos(β + 6°))", $"{F(fV / 1e3)} kN / (cos {F(theta)}° × cos {F(beta + tau)}°)", ScalarValue.Of(roll.Fs / 1e3, "kN"), "Sling force when the beam rolls 6°", "EN 13155");
            c.Warn("tilt", "beam: the automatic 6° tilt cases are applied (pitch: sling angles θ ± 6° with the load redistributed between the lugs; roll: out-of-plane angle + 6°) and the larger sling force governs.");
        }

        var worst = cases.MaxBy(k => k.Fs);
        var fS = worst.Fs;
        var fIp = cases.Max(k => k.Fip);
        var fOp = cases.Max(k => k.Fop);
        var nBeam = cases.Max(k => k.N);

        if (theta + (beam ? tau : 0) > 60)
            c.Warn("angle", $"theta: the sling angle with the tilt is {F(theta + (beam ? tau : 0))}° from the vertical, above the usual 60°; check the sling and the lug for the lower angle to the horizontal.");

        c.Record("F_s", beam ? "max(F_s,nom, F_s,pitch, F_s,roll)" : "F_s,nom", beam ? $"governing case: {worst.Name}" : "no tilt case", ScalarValue.Of(fS / 1e3, "kN"), "Sling force on each lug (resultant), governing case", "spec");
        c.Record("F_ip", beam ? "max over the cases of F_s·cos β" : "F_s·cos β", $"{F(fS / 1e3)} kN × cos β", ScalarValue.Of(fIp / 1e3, "kN"), "In-plane component of the sling force (envelope)", "spec");
        c.Record("F_op", beam ? "max over the cases of F_s·sin β" : "F_s·sin β", $"{F(fS / 1e3)} kN × sin β", ScalarValue.Of(fOp / 1e3, "kN"), "Out-of-plane component of the sling force (envelope)", "spec");
        c.Record("N_beam", beam ? "max(F_v·tan θ, F_v·sin(θ + 6°)/cos θ)" : "F_v·tan θ", beam ? $"{F(nBeam / 1e3)} kN, the larger of the level and the pitched case" : $"{F(fV / 1e3)} kN × tan({F(theta)}°)", ScalarValue.Of(nBeam / 1e3, "kN"), "Axial force along the spreader beam at the more loaded lug (envelope over the cases)", "spec");

        if (c.Has("h"))
        {
            var h = c.InputSi("h") * 1e3;
            c.Record("M_op", "F_op·h", $"{F(fOp / 1e3)} kN × {F(h)} mm", ScalarValue.Of(fOp * h / 1e6, "kN*m"), "Out-of-plane moment at the weld or base", "spec");
        }

        // ---------------------------------------------------------------- geometry
        // In this sheet's names: b_e is the distance from the hole edge to the plate edge on each side across the load line (S106's a), a the end
        // distance along the load line (S106's e).
        var side = Math.Min(be, radius - dh / 2);

        if (be > radius - dh / 2)
            c.Warn("be", $"be: b_e ({F(be)} mm) exceeds R − D_h/2 ({F(radius - dh / 2)} mm); the smaller is used.");

        var aEff = c.Has("a") ? c.InputSi("a") * 1e3 : radius - dh / 2;
        var ratio = dp / dh;
        var phi = 55.0 * ratio;
        var beEff = Math.Min(side, Math.Min(4 * t, 0.6 * (fu / fy) * Math.Sqrt(dh / side) * side));
        var aT = 2 * t * beEff;
        var aFr = (1.13 * aEff + 0.92 * side / (1 + side / dh)) * t;
        var aV = 2 * t * (aEff + dp / 2 * (1 - Math.Cos(phi * Math.PI / 180)));
        var aB = dp * t;

        if (beEff < side)
            c.Warn("be-eff", $"be: the effective width {F(beEff)} mm is below the edge distance {F(side)} mm (limited to 4t and by f_u/f_y), so the tension area uses the smaller.");

        if (ratio < 0.9)
            c.Warn("clearance", $"Dp: the pin is {F(ratio * 100)} % of the hole diameter; the pin-clearance factor C_r falls as the pin gets smaller, and a close-fitting pin is the usual practice.");

        c.Record("side", "min(b_e, R − D_h/2)", $"min({F(be)}, {F(radius)} − {F(dh)}/2) mm", ScalarValue.Of(side, "mm"), "Edge distance on each side of the hole", "spec");
        c.Record("a_eff", "a, or R − D_h/2", c.Has("a") ? $"a = {F(aEff)} mm" : $"{F(radius)} − {F(dh)}/2 mm", ScalarValue.Of(aEff, "mm"), "End distance along the load line", "spec");
        c.Record("phi", "55·D_p/D_h (degrees)", $"55 × {F(dp)} / {F(dh)}", ScalarValue.Of(phi, "deg"), "Angle of the shear planes", "BTH-1 3-3.3");
        c.Record("be_eff", "min(b_e,eff, 4t, 0.6·(f_u/f_y)·√(D_h/b_e,eff)·b_e,eff)", $"min({F(side)}, {F(4 * t)}, {F(0.6 * (fu / fy) * Math.Sqrt(dh / side) * side)}) mm", ScalarValue.Of(beEff, "mm"), "Effective width beyond the hole in tension", "BTH-1 3-3.3");
        c.Record("A_t", "2·t·b_e,eff", $"2 × {F(t)} × {F(beEff)} mm", ScalarValue.Of(aT, "mm^2"), "Area beyond the hole in tension", "BTH-1 3-3.3");
        c.Record("A_fr", "(1.13·a + 0.92·b_e/(1 + b_e/D_h))·t", $"(1.13 × {F(aEff)} + 0.92 × {F(side)} / (1 + {F(side)}/{F(dh)})) × {F(t)} mm", ScalarValue.Of(aFr, "mm^2"), "Single-plane fracture area beyond the hole (before C_r)", "BTH-1 3-3.3");
        c.Record("A_v", "2·t·[a + (D_p/2)(1 − cos φ)]", $"2 × {F(t)} × [{F(aEff)} + ({F(dp)}/2) × (1 − cos {F(phi)}°)] mm", ScalarValue.Of(aV, "mm^2"), "Shear area of the two shear-out planes", "BTH-1 3-3.3");
        c.Record("A_b", "D_p·t", $"{F(dp)} × {F(t)} mm", ScalarValue.Of(aB, "mm^2"), "Projected bearing area of the pin", "spec");

        var modes = new List<(string Label, string Symbol, double U)>();
        string? dishFailure = null;
        double uDish = 0;
        double uBth = 0;
        double uEn = 0;

        // ---------------------------------------------------------------- BTH-1
        if (bth)
        {
            var cr = 1 - 0.275 * Math.Sqrt(Math.Max(0, 1 - ratio * ratio));
            var fa = Math.Min(fy / ny, fu / nu);
            var design = nd * kSvc * fS;
            var pT = cr * fa * aT;
            var pSp = cr * fa * aFr;
            var pV = fa * aV / Math.Sqrt(3);
            var pB = kb * fa * aB;

            c.Record("Cr", "1 − 0.275·√(1 − D_p²/D_h²)", $"1 − 0.275 × √(1 − ({F(dp)}/{F(dh)})²)", new ScalarValue(cr), "Pin-clearance factor", "BTH-1 3-3.3");
            c.Record("Fa", "min(f_y/n_y, f_u/n_u)", $"min({F(fy)} / {F(ny)}, {F(fu)} / {F(nu)}) MPa", ScalarValue.Of(fa, "MPa"), "Allowable stress", "spec");
            c.Record("D_bth", "N_d·k_svc·F_s", $"{F(nd)} × {F(kSvc)} × {F(fS / 1e3)} kN", ScalarValue.Of(design / 1e3, "kN"), "Design load on each lug", "BTH-1 3-3.3");
            c.Record("P_t", "C_r·F_a·A_t", $"{F(cr)} × {F(fa)} MPa × {F(aT)} mm²", ScalarValue.Of(pT / 1e3, "kN"), "Tension beyond the hole", "BTH-1 3-3.3");
            c.Record("P_sp", "C_r·F_a·A_fr", $"{F(cr)} × {F(fa)} MPa × {F(aFr)} mm²", ScalarValue.Of(pSp / 1e3, "kN"), "Single-plane fracture beyond the hole", "BTH-1 3-3.3");
            c.Record("P_v", "F_a·A_v/√3", $"{F(fa)} MPa × {F(aV)} mm² / √3", ScalarValue.Of(pV / 1e3, "kN"), "Double-plane shear-out", "BTH-1 3-3.3");
            c.Record("P_b", "k_b·F_a·A_b", $"{F(kb)} × {F(fa)} MPa × {F(aB)} mm²", ScalarValue.Of(pB / 1e3, "kN"), "Pin bearing", "BTH-1 3-3.3");

            var parts = new[] { ("tension beyond the hole", "u_t", pT), ("single-plane fracture", "u_sp", pSp), ("double-plane shear", "u_v", pV), ("bearing", "u_b", pB) };

            foreach (var (label, symbol, capacity) in parts)
            {
                var u = design / capacity;
                c.Record(symbol, "D/P", $"{F(design / 1e3)} kN / {F(capacity / 1e3)} kN", new ScalarValue(u), $"Utilisation, {label}", "BTH-1 3-3.3");
                modes.Add(($"BTH-1: {label}", symbol, u));
                uBth = Math.Max(uBth, u);
            }

            var capBth = wll / 1e3 / uBth;
            c.Record("u_bth", "max(u_t, u_sp, u_v, u_b)", "governing BTH-1 section", new ScalarValue(uBth), "BTH-1 utilisation", "BTH-1 3-3.3");
            c.Record("cap_bth", "WLL/u_bth", $"{F(wll / 1e3)} kN / {F(uBth)}", ScalarValue.Of(capBth, "kN"), "Rated load the lug carries by the BTH-1 approach", "BTH-1 3-3.3");

            if (c.Has("dish_max"))
            {
                var dishMax = c.InputSi("dish_max");
                var dish = dh / t;
                uDish = dish / dishMax;
                c.Record("dish", "D_h/t", $"{F(dh)} / {F(t)}", new ScalarValue(dish), "Dishing ratio", "spec");
                c.Record("u_dish", "(D_h/t)/dish_max", $"{F(dish)} / {F(dishMax)}", new ScalarValue(uDish), "Dishing-ratio utilisation", "spec");

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
                ("single-plane fracture", "sp", fy * aFr, fu * aFr),
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

            var capEn = wll / 1e3 / uEn;
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
                var fa = bth ? Math.Min(fy / ny, fu / nu) : 0.0;
                var uBb = bth ? nd * kSvc * nBeam / (area * fa) : 0.0;
                var uBe = en ? Math.Max(2 * nBeam / (area * fy), 3 * nBeam / (area * fu)) : 0.0;
                uBeam = Math.Max(uBb, uBe);
                c.Record("u_beam", bth && en ? "max(N_d·k_svc·N_beam/(A·F_a), max(2N/(A·f_y), 3N/(A·f_u)))" : bth ? "N_d·k_svc·N_beam/(A·F_a)" : "max(2N_beam/(A·f_y), 3N_beam/(A·f_u))",
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

        foreach (var (symbol, text) in new[] { ("F_s", "Sling force on each lug"), ("F_ip", "In-plane component"), ("F_op", "Out-of-plane component"), ("N_beam", "Horizontal component per lug"), ("M_op", "Out-of-plane moment"), ("F_s_pitch", "Sling force, beam pitched 6°"), ("F_s_roll", "Sling force, beam rolled 6°"), ("side", "Edge distance on each side of the hole"), ("be_eff", "Effective width beyond the hole"), ("a_eff", "End distance along the load line"), ("Cr", "Pin-clearance factor C_r"), ("phi", "Shear-plane angle φ"), ("A_v", "Shear area"), ("A_t", "Tension area beyond the hole"), ("A_fr", "Fracture area beyond the hole"), ("A_b", "Bearing area") })
        {
            if (c.Session.Find(symbol) is not null)
                c.Output(symbol, text);
        }

        if (bth)
        {
            foreach (var (symbol, text) in new[] { ("Fa", "Allowable stress"), ("D_bth", "Design load on each lug"), ("P_t", "Tension beyond the hole"), ("P_sp", "Single-plane fracture"), ("P_v", "Double-plane shear"), ("P_b", "Bearing"), ("u_t", "Utilisation, tension"), ("u_sp", "Utilisation, fracture"), ("u_v", "Utilisation, shear"), ("u_b", "Utilisation, bearing"), ("dish", "Dishing ratio"), ("u_dish", "Dishing utilisation") })
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

        c.Output("sheet_notice", "Status of this sheet", new TextValue(DesignNotice + " It does not replace the thorough examination by a competent person (LOLER reg 9) or the marking, testing and certification that the regulations require. " + SignatureNotice));
        c.Output("checklist", "Marking and certification checklist", new TextValue(
            "(1) mark the appliance with its rated load and a unique identification (LOLER reg 7); " +
            "(2) thorough examination by a competent person before first use and at the intervals required (LOLER reg 9) — this sheet is not one; " +
            $"(3) {proofText}, test certificate where the route requires it; " +
            "(4) material certificates for the lug plate and the pin, and weld procedure and inspection records for the lug welds; " +
            $"(5) pin, shackle and sling ratings at least {F(fS / 1e3)} kN (PUWER reg 4); " +
            "(6) record the design basis: mode, Design Category, service class and the user records used; " +
            "(7) keep the report of the thorough examination with the appliance."));

        // ---------------------------------------------------------------- checks
        foreach (var (label, symbol, _) in modes)
            c.CheckUtilisation(label, symbol, label.StartsWith("BTH-1", StringComparison.Ordinal) ? "BTH-1 3-3.3" : "EN 13155");

        if (bth && c.Session.Find("u_dish") is not null)
            c.CheckUtilisation("BTH-1: dishing ratio", "u_dish", "spec");

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

    /// <summary>The PDHonline S106 worked example in its own units (in, ksi): one lug, a unit load, the harness factors of its allowable-stress basis min(F_u/5, F_y/3).</summary>
    private static CalcInputValue[] S106() =>
    [
        Choice("mode", ModeBth1), Q("WLL", 5, "kip"), Choice("n_lugs", "1"), Q("theta", 0, "deg"), Q("beta", 0, "deg"), CalcInputValue.Switch("beam", false), N("n_cycles", 1000),
        Q("t", 1.25, "in"), Q("Dh", 1.25, "in"), Q("Dp", 0.75, "in"), Q("R", 1.75, "in"), Q("be", 1.125, "in"), Q("a", 1.125, "in"), Q("f_y", 36, "ksi"), Q("f_u", 58, "ksi"),
        N("Nd", 1), N("n_y", 3), N("n_u", 5), N("k_b", 1),
    ];

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
