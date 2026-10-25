using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.BuildingServices;

/// <summary>
/// B8 — pipe thermal expansion, expansion legs and loops, and anchor forces: the growth ΔL = α·L·ΔT between anchors, the fully
/// restrained stress and force, the leg a guided cantilever needs to absorb the growth within a user-supplied allowable range
/// (an offset leg takes all of it and its run, or both runs of a Z-bend, bend for the leg's own growth; a U-loop is two legs that take
/// half each), the force on the anchors (the resultant of the two perpendicular components) and the largest root moment, a screening ratio D·Y/(L − U)² against a user-supplied K1, and an axial bellows layout whose anchor
/// force is the pressure thrust plus its spring force. A screening calculation only: it is not a piping stress analysis and ships
/// no allowable stress and no code constant.
/// </summary>
public sealed class PipeExpansionCalculator : ICalcCalculator
{
    public const string Id = "bs.pipe-expansion";

    public const string TwoTemperatures = "install and operating temperatures";
    public const string ChangeGiven = "temperature change entered";

    public const string OffsetLeg = "offset leg (L- or Z-bend: the leg takes the whole movement)";
    public const string UpLoop = "U-loop (two legs, each takes half the movement)";
    public const string AxialBellows = "axial bellows (no leg or loop: the bellows absorbs the movement)";

    /// <summary>The record kind of a W3 material (<c>MaterialLibrary.Kind</c>; written out here because this assembly does not reference the library, and a test keeps the two equal).</summary>
    public const string MaterialKind = "material.properties";

    /// <summary>Legs shorter than this many pipe diameters are outside the beam model.</summary>
    public const double ShortLegDiameters = 10;

    /// <summary>
    /// The smallest modulus (GPa) a pipe material is expected to have: PE, PP and PEX are about 0.5 to 1.5 GPa short-term. Below it the
    /// sheet warns that E may have been typed in MPa (steel 210 GPa is 210 000 MPa, so a metal typed in MPa lands at 0.07 to 0.21 GPa and
    /// turns a failing leg into a passing one with an anchor force 1000 times low, Black B-B-03).
    /// </summary>
    public const double PlausibleMinModulusGPa = 0.5;

    /// <summary>The smallest modulus (GPa) accepted at all: long-term or hot values of the softest plastic pipe are about 0.1 to 0.3 GPa.</summary>
    public const double HardMinModulusGPa = 0.1;

    private const string ScreeningClause = "ASME B31.3 para. 319.4.1 (screening form; number to be checked against the owner's copy)";

    public CalcTemplate Template { get; } = new(
        Id, "1.2.2", CalcDiscipline.BuildingServices, "Pipe expansion, loops and anchor forces (screening)",
        "Screening only — not a piping stress analysis. Thermal growth of a straight run between anchors, the fully restrained stress and force, the leg an offset leg or a U-loop (two guided cantilevers) needs to absorb the growth within the owner's allowable displacement-stress range Sa and whether the available leg is long enough (an offset leg or Z-bend also loads its run or runs with the leg's own growth), the force on the anchors and the largest root moment of the guided-cantilever model, the screening ratio D·Y/(L − U)² against the owner's K1 (PASS, or the line needs a formal stress analysis), and, for an axial bellows, the anchor force P·A_eff plus its spring force. No allowable stress, K1 or code table ships.",
        "Pipe section: d_i = OD − 2·t; A_m = π/4·(OD² − d_i²); I = π/64·(OD⁴ − d_i⁴); Z = I/(OD/2)\n" +
        "Temperature change: ΔT = T_operate − T_install, or as entered; the movement uses |ΔT| and its sign gives the direction\n" +
        "Run that grows: L_tot = L, or L + L2 for a Z-bend; thermal growth ΔL = α·L_tot·|ΔT|\n" +
        "Fully restrained pipe: σ_r = E·α·|ΔT| (E at the installation temperature); F_r = E_r·α·|ΔT|·A_m (E_r = E_hot, else E)\n" +
        "Movement one leg must absorb: Δ = ΔL for an offset leg or Z-bend; Δ = ΔL/2 for a U-loop (two identical legs in series, the cross leg a rigid guide)\n" +
        "Guided cantilever, required leg: L_req = √(3·E·OD·Δ/Sa), the leg at which the bending stress M/Z reaches Sa; PASS when L_req ≤ the available leg H\n" +
        "Force and moment of the available leg: F_leg = 12·E_r·I·Δ/H³ (acts along the run); M_leg = 6·E_r·I·Δ/H² (= F_leg·H/2); σ_leg = 3·E·OD·Δ/H² = Sa·(L_req/H)²\n" +
        "Offset leg or Z-bend, the runs bend for the leg's growth Δ_leg = α·|ΔT|·H: the runs act in series under one force F_run = 12·E_r·I·Δ_leg/(L³ + L2³) (L2 = 0 for an L-bend) acting along the leg; run i takes the share L_i³/(L³ + L2³) of Δ_leg, with root stress σ_i = 3·E·OD·Δ_i/L_i² and moment M_i = F_run·L_i/2; each run is checked as its own cantilever, PASS when √(σ_i/Sa) ≤ 1 (for an L-bend the run needed is L_req,run = √(3·E·OD·Δ_leg/Sa))\n" +
        "Force on the anchors: the two components are perpendicular, F_leg along the run and F_run along the leg, so the force is √(F_leg² + F_run²); the moment reported is the largest root moment of the legs and runs in the guided-cantilever model\n" +
        "Flexibility screening: OD·Y/(L_dev − U)² ≤ K1 with Y = α·|ΔT|·U, the free growth between the anchors; offset leg or Z-bend L_dev = L_tot + H, U = √(L_tot² + H²); U-loop L_dev = L + 2·H, U = L (so Y = ΔL); PASS, or the line needs a formal stress analysis\n" +
        "Axial bellows (no leg or loop, no leg check, no screening): anchor force = P·A_eff + k·ΔL; the movement is checked against the bellows' rated axial movement when given",
        [
            new CalcInputSpec("E", "Young's modulus of the pipe material at the installation (reference) temperature, for the stress check, in GPa (steel 210, copper 120, PE and PP about 1; not MPa)", CalcInputKind.Quantity, "GPa", Default: 210, Min: PlausibleMinModulusGPa, Max: 500, HardMin: HardMinModulusGPa, HardMax: 1000, Group: "Material", MaterialProperty: "E"),
            new CalcInputSpec("E_hot", "Young's modulus at the operating temperature, for the reactions on the anchors (blank = E), in GPa; do not use it for the stress check", CalcInputKind.Quantity, "GPa", Min: PlausibleMinModulusGPa, Max: 500, HardMin: HardMinModulusGPa, HardMax: 1000, IsOptional: true, Group: "Material"),
            new CalcInputSpec("alpha", "Mean coefficient of thermal expansion between the install and operating temperatures", CalcInputKind.Quantity, "um/(m*K)", Default: 12, Min: 1, Max: 250, HardMin: 0.1, HardMax: 1000, Group: "Material", MaterialProperty: "alpha"),
            new CalcInputSpec("OD", "Pipe outside diameter", CalcInputKind.Quantity, "mm", Default: 88.9, Min: 10, Max: 1500, HardMin: 1, HardMax: 10000, Group: "Pipe"),
            new CalcInputSpec("t_wall", "Pipe wall thickness", CalcInputKind.Quantity, "mm", Default: 3.2, Min: 0.5, Max: 60, HardMin: 0.1, HardMax: 1000, Group: "Pipe"),
            new CalcInputSpec("L", "Anchor spacing: the straight length that grows (an offset leg or Z-bend: the first run, which also bends for the leg's growth)", CalcInputKind.Quantity, "m", Default: 50, Min: 1, Max: 500, HardMin: 0.1, HardMax: 10000, Group: "Movement"),
            new CalcInputSpec("L2", "Second run of a Z-bend, on the other side of the leg (blank = L-bend; offset-leg layout only)", CalcInputKind.Quantity, "m", Min: 1, Max: 500, HardMin: 0.1, HardMax: 10000, IsOptional: true, Group: "Movement"),
            new CalcInputSpec("temp_mode", "Temperature change from", CalcInputKind.Choice, Choices: [TwoTemperatures, ChangeGiven], DefaultChoice: TwoTemperatures, Group: "Movement"),
            new CalcInputSpec("T_install", "Temperature at installation (when the run is anchored)", CalcInputKind.Quantity, "degC", Default: 10, Min: -50, Max: 50, HardMin: -100, HardMax: 500, Group: "Movement"),
            new CalcInputSpec("T_operate", "Operating temperature (hot or cold)", CalcInputKind.Quantity, "degC", Default: 80, Min: -50, Max: 500, HardMin: -200, HardMax: 1000, Group: "Movement"),
            new CalcInputSpec("dT_in", "Temperature change ΔT (negative for a cold line), when entered directly", CalcInputKind.Quantity, "K", Min: -300, Max: 500, HardMin: -300, HardMax: 1000, IsOptional: true, Group: "Movement"),
            new CalcInputSpec("layout", "What absorbs the movement", CalcInputKind.Choice, Choices: [OffsetLeg, UpLoop, AxialBellows], DefaultChoice: OffsetLeg, Group: "Layout"),
            new CalcInputSpec("H", "Available leg length (each leg of a U-loop; an offset leg: the leg off the run, which may be the longer of the two); no default, not needed for an axial bellows", CalcInputKind.Quantity, "m", Min: 0.5, Max: 50, HardMin: 0.01, HardMax: 1000, IsOptional: true, Group: "Layout"),
            new CalcInputSpec("Sa", "Allowable displacement-stress range Sa for the legs and runs (your piping code and its formula; no default; not needed for an axial bellows)", CalcInputKind.Quantity, "MPa", Min: 5, Max: 500, HardMin: 0.1, HardMax: 5000, IsOptional: true, Group: "Criteria"),
            new CalcInputSpec("K_scr", "Screening limit K1 for OD·Y/(L − U)², in mm²/m² (or in²/ft², …) from your piping code (no default; blank = screening not evaluated and the sheet does not pass)", CalcInputKind.Quantity, "mm^2/m^2", Min: 1, Max: 1000, HardMin: 1e-6, HardMax: 1e9, IsOptional: true, Group: "Criteria"),
            new CalcInputSpec("P_b", "Axial bellows governing pressure, gauge, at or above zero: the design or test pressure that governs the anchor sizing (a vacuum bellows is outside this sheet; compression and extension ratings can differ, enter the governing one)", CalcInputKind.Quantity, "bar", Min: 0.1, Max: 40, HardMin: 0, HardMax: 1000, IsOptional: true, Group: "Bellows"),
            new CalcInputSpec("A_eff", "Axial bellows effective area A_eff (manufacturer's data)", CalcInputKind.Quantity, "cm^2", Min: 1, Max: 5000, HardMin: 0.01, HardMax: 1e6, IsOptional: true, Group: "Bellows"),
            new CalcInputSpec("k_b", "Axial bellows spring rate (manufacturer's data; blank = no spring force)", CalcInputKind.Quantity, "N/mm", Min: 1, Max: 5000, HardMin: 0, HardMax: 1e6, IsOptional: true, Group: "Bellows"),
            new CalcInputSpec("dL_rated", "Rated axial movement of the bellows (manufacturer's data; blank = movement not checked)", CalcInputKind.Quantity, "mm", Min: 1, Max: 500, HardMin: 0.01, HardMax: 10000, IsOptional: true, Group: "Bellows"),
        ],
        [
            new CalcAssumption("Screening only — not a piping stress analysis. The result tells whether a simple leg or loop is plausible and what it puts on its anchors; it does not replace a flexibility analysis of the real routing (elbow flexibility, intensification, torsion, branches, restraint friction, supports, sustained and occasional loads).", "Stated scope."),
            new CalcAssumption("A guided cantilever: the available leg is straight, uniform, fixed against rotation at both ends and loaded only by the movement it absorbs (stiffness 12·E·I/H³, end moment F·H/2). Elbows are taken as rigid and the leg as slender: the beam model is not valid for legs shorter than about ten diameters.", "Beam theory."),
            new CalcAssumption("No stress-intensification factor and no elbow flexibility are applied: the stress at the elbow root of a real bend is higher than the beam stress M/Z, by the intensification factor of the code, so a leg that passes here can still exceed Sa at the elbow. This is the main reason the result is a screen and not a stress analysis.", "Stated limit."),
            new CalcAssumption("A U-loop is two identical legs in series, each a guided cantilever taking half the growth, the cross leg a rigid guide; the force along the line is the same F in both legs. The moment M is the moment at the leg roots (the corners of the loop); it reaches an anchor only where the leg ends at the anchor (an offset leg). The growth of the loop's own legs, which the cross leg has to absorb, is not checked.", "Loop model."),
            new CalcAssumption("An offset leg is anchored at both ends: the run L bends for the growth of the leg (α·|ΔT|·H) as a second guided cantilever and is checked against the same Sa. A Z-bend (second run L2) has the two runs in series under one force, each taking its share of the leg's growth in proportion to its flexibility, L_i³, and each checked as its own cantilever; the leg takes the growth of both runs. The force on the anchors is the resultant of the force along the run (the leg's) and the force along the leg (the runs'), which are perpendicular.", "Offset-leg and Z-bend model."),
            new CalcAssumption("anchor_moment is the largest root moment of the legs and runs in the guided-cantilever simplification (M = F·L/2 with each end fixed against rotation). It is not the moment on a frame anchor, which depends on the whole frame (the stiffness of the other legs, the elbow rotations, the anchor's own fixity); a formal analysis is needed before an anchor is sized for moment.", "Stated limit."),
            new CalcAssumption("The material properties are single values: α the mean coefficient between the install and operating temperatures; E the modulus at the installation (reference) temperature, used for the stress check, as the piping codes do (ASME B31.3 para. 319.3.2 and 319.5, numbers to be checked against the owner's copy); E_hot, when given, is used only for the reactions on the anchors. Take them from a W3 material applied at the right temperature.", "Material library; code convention to be checked."),
            new CalcAssumption("Sa and K1 are the owner's records from the owner's own copy of the piping code (or the design basis), with their provenance; no allowable stress, K1 or code constant ships. The sheet applies the relation to the numbers entered and does not know which code, material or load case they belong to.", "No table from a standard is shipped."),
            new CalcAssumption("No friction, weight, spring-hanger or cold-spring effect is included in the leg force. The axial bellows anchor force is the pressure thrust P·A_eff plus the spring force k·ΔL at the governing pressure entered (the design or the test pressure, whichever governs; a bellows' compression and extension ratings can differ): guide friction and any other bellows load come from the manufacturer's data.", "Stated limit."),
        ],
        [
            new CalcClauseRef("ASME B31.3", "para. 319.4.1 (screening relation; number to be checked against the owner's copy)", "Process piping — flexibility analysis, exemption from formal analysis"),
            new CalcClauseRef("ASME B31.3", "para. 319.3.2 and 319.5 (reference and hot moduli; numbers to be checked against the owner's copy)", "Process piping — displacement stress range and reactions"),
            new CalcClauseRef("EN 13480-3", "cl. 12 (number to be checked against the owner's copy)", "Metallic industrial piping — flexibility analysis"),
        ],
        [
            new CalcApplicabilityRule("wall", "The wall must be less than half the outside diameter.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("Sa", "Enter the allowable displacement-stress range Sa for a leg or a loop; it has no default.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("dT_in", "Enter the temperature change when it is the source of the movement; it must not be zero.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("dT-zero", "Install and operating temperatures are equal: there is no movement to absorb.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("bellows", "An axial bellows needs both the pressure P_b and the effective area A_eff.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("modulus-low", "E (or E_hot) is below 0.5 GPa, lower than any usual pipe material: it may have been typed in MPa.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("short-leg", "The available leg (or, for an offset leg or Z-bend, a run) is shorter than ten pipe diameters: the guided-cantilever beam model is not valid.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("bellows-unused", "Bellows inputs are given with a leg or loop layout and are not used; choose the axial bellows layout.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("k1-unused", "K1 is given with the axial bellows layout, which has no screening; it is not used.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("l2-unused", "L2 is given with a layout other than the offset leg; it is not used.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("screening-k1", "No K1 was entered: the screening ratio is shown and not judged, and the sheet does not pass.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-offset-leg-steel-50m", "Steel pipe 50 m between anchors, 70 K: growth 42 mm, an offset leg of 6 m against 4.850 m required",
                new CalcReferenceSource("Analytic (ΔL = α·L·ΔT; guided cantilever L = √(3·E·D·Δ/Sa), F = 12·E·I·Δ/L³, M = 6·E·I·Δ/L²; screening Y = α·ΔT·U; recomputed in python3)", null, "88.9 × 3.2 mm steel, E 210 GPa, α 12 µm/(m·K), 10 → 80 °C over 50 m: ΔL = 12e-6·50·70 = 42 mm; fully restrained σ_r = 176.4 MPa and F_r = 151.98 kN on A_m = 861.55 mm²; an invented Sa of 100 MPa gives L_req = 4.85005 m, so the 6 m offset leg passes (u 0.80834) with F = 388.11 N and M = 1164.33 N·m (σ_leg 65.34 MPa); the run bends for the leg's 5.04 mm and needs 1.68010 m (u 0.0336) with 0.080 N along the leg, so the anchors take the resultant 388.10882 N; the anchors are 50.3587 m apart (L_dev 56 m), Y = 12e-6·70·50.3587 = 42.301 mm, so the screening ratio is 118.168 mm²/m² against an invented K1 of 100 mm²/m² and the line needs a formal stress analysis. Sa and K1 are illustrative inputs, not code values"),
                [Q("E", 210, "GPa"), Q("alpha", 12, "um/(m*K)"), Q("OD", 88.9, "mm"), Q("t_wall", 3.2, "mm"), Q("L", 50, "m"), Choice("temp_mode", TwoTemperatures), Q("T_install", 10, "degC"), Q("T_operate", 80, "degC"), Choice("layout", OffsetLeg), Q("H", 6, "m"), Q("Sa", 100, "MPa"), Q("K_scr", 100, "mm^2/m^2")],
                [new CalcExpectedValue("dL", 42, "mm", 1e-9), new CalcExpectedValue("dL_signed", 42, "mm", 1e-9), new CalcExpectedValue("dT", 70, "K", 1e-9), new CalcExpectedValue("sigma_res", 176.4, "MPa", 1e-9), new CalcExpectedValue("A_m", 861.5503693204649, "mm^2", 1e-9), new CalcExpectedValue("F_res", 151.97748514813, "kN", 1e-9), new CalcExpectedValue("L_req", 4.85004536061262, "m", 1e-9), new CalcExpectedValue("u_leg", 0.8083408934354367, "", 1e-9), new CalcExpectedValue("F_leg", 388.1088087998288, "N", 1e-9), new CalcExpectedValue("M_leg", 1164.3264263994865, "N*m", 1e-9), new CalcExpectedValue("L_req_run", 1.6801049967189552, "m", 1e-9), new CalcExpectedValue("u_run", 0.033602099934379105, "", 1e-9), new CalcExpectedValue("F_run", 0.0804782425927325, "N", 1e-9), new CalcExpectedValue("F_along_run", 388.1088087998288, "N", 1e-9), new CalcExpectedValue("F_along_leg", 0.0804782425927325, "N", 1e-9), new CalcExpectedValue("anchor_force", 388.1088171438129, "N", 1e-9), new CalcExpectedValue("anchor_moment", 1164.3264263994865, "N*m", 1e-9), new CalcExpectedValue("sigma_leg", 65.3415, "MPa", 1e-9), new CalcExpectedValue("L_dev", 56, "m", 1e-9), new CalcExpectedValue("U_a", 50.35871324805669, "m", 1e-9), new CalcExpectedValue("Y_scr", 42.30131912836762, "mm", 1e-9), new CalcExpectedValue("scr", 118.16784619539838, "mm^2/m^2", 1e-9), new CalcExpectedValue("u_scr", 1.1816784619539837, "", 1e-9), new CalcExpectedValue("pass", 0, "", 1e-9)]),
            new CalcTestCase("ka-u-loop-too-short", "The same line with a U-loop of two 3 m legs: each takes 21 mm and needs 3.429 m, so the loop is too short",
                new CalcReferenceSource("Analytic (U-loop as two guided cantilevers, Δ = ΔL/2; recomputed in python3)", null, "As the previous case with a U-loop of legs H = 3 m: Δ = 21 mm, L_req = √(3·210e9·0.0889·0.021/100e6) = 3.42950 m, u 1.14317 (FAIL), F = 1552.44 N, M = 2328.65 N·m, σ_leg 130.68 MPa = Sa·u²; L_dev = 56 m, U = L = 50 m, Y = ΔL = 42 mm, so the screening ratio is 103.717 mm²/m²; an invented K1 of 120 mm²/m² is met (u 0.86431)"),
                [Q("E", 210, "GPa"), Q("alpha", 12, "um/(m*K)"), Q("OD", 88.9, "mm"), Q("t_wall", 3.2, "mm"), Q("L", 50, "m"), Choice("temp_mode", TwoTemperatures), Q("T_install", 10, "degC"), Q("T_operate", 80, "degC"), Choice("layout", UpLoop), Q("H", 3, "m"), Q("Sa", 100, "MPa"), Q("K_scr", 120, "mm^2/m^2")],
                [new CalcExpectedValue("dL", 42, "mm", 1e-9), new CalcExpectedValue("Delta", 21, "mm", 1e-9), new CalcExpectedValue("L_req", 3.4294999635515384, "m", 1e-9), new CalcExpectedValue("u_leg", 1.1431666545171795, "", 1e-9), new CalcExpectedValue("anchor_force", 1552.4352351993152, "N", 1e-9), new CalcExpectedValue("anchor_moment", 2328.652852798973, "N*m", 1e-9), new CalcExpectedValue("sigma_leg", 130.683, "MPa", 1e-9), new CalcExpectedValue("L_dev", 56, "m", 1e-9), new CalcExpectedValue("U_a", 50, "m", 1e-9), new CalcExpectedValue("Y_scr", 42, "mm", 1e-9), new CalcExpectedValue("scr", 103.71666666666668, "mm^2/m^2", 1e-9), new CalcExpectedValue("u_scr", 0.8643055555555558, "", 1e-9), new CalcExpectedValue("pass", 0, "", 1e-9)]),
            new CalcTestCase("ka-cold-copper-loop", "A cold copper line (ΔT −40 K) with a 1.5 m U-loop leg: contraction of 19.92 mm, both checks met",
                new CalcReferenceSource("Analytic (ΔL = α·L·|ΔT|; U-loop Δ = ΔL/2; recomputed in python3)", null, "28 × 1.2 mm copper, E 120 GPa, α 16.6 µm/(m·K), 30 m, ΔT = −40 K entered: ΔL = 16.6e-6·30·40 = 19.92 mm (a contraction, dL_signed −19.92 mm), fully restrained σ_r = 79.68 MPa and F_r = 8.0504 kN on A_m = 101.03 mm²; U-loop legs of 1.5 m, an invented Sa of 60 MPa: Δ = 9.96 mm, L_req = 1.29355 m, u 0.86237, F = 38.62 N, M = 28.97 N·m; Y = ΔL, the screening ratio is 61.973 mm²/m² against an invented K1 of 150 mm²/m². Sa and K1 are illustrative inputs, not code values"),
                [Q("E", 120, "GPa"), Q("alpha", 16.6, "um/(m*K)"), Q("OD", 28, "mm"), Q("t_wall", 1.2, "mm"), Q("L", 30, "m"), Choice("temp_mode", ChangeGiven), Q("dT_in", -40, "K"), Choice("layout", UpLoop), Q("H", 1.5, "m"), Q("Sa", 60, "MPa"), Q("K_scr", 150, "mm^2/m^2")],
                [new CalcExpectedValue("dT", -40, "K", 1e-9), new CalcExpectedValue("dL", 19.92, "mm", 1e-9), new CalcExpectedValue("dL_signed", -19.92, "mm", 1e-9), new CalcExpectedValue("sigma_res", 79.68, "MPa", 1e-9), new CalcExpectedValue("F_res", 8.0503588208392, "kN", 1e-9), new CalcExpectedValue("Delta", 9.96, "mm", 1e-9), new CalcExpectedValue("L_req", 1.2935532459083392, "m", 1e-9), new CalcExpectedValue("u_leg", 0.8623688306055595, "", 1e-9), new CalcExpectedValue("anchor_force", 38.62454824121034, "N", 1e-9), new CalcExpectedValue("anchor_moment", 28.968411180907758, "N*m", 1e-9), new CalcExpectedValue("scr", 61.97333333333333, "mm^2/m^2", 1e-9), new CalcExpectedValue("u_scr", 0.41315555555555555, "", 1e-9), new CalcExpectedValue("pass", 1, "", 1e-9)]),
            new CalcTestCase("ka-z-bend-steel", "A Z-bend of two runs (30 m and 20 m) and a 6 m leg: the leg takes the 42 mm of both runs, the runs share the leg's growth",
                new CalcReferenceSource("Analytic (Z-bend: leg a guided cantilever for α·(L + L2)·ΔT; runs in series under F_run = 12·E·I·Δ_leg/(L³ + L2³), run i taking Δ_leg·L_i³/(L³ + L2³); resultant anchor force; recomputed in python3)", null, "88.9 × 3.2 mm steel as the first case, runs L = 30 m and L2 = 20 m either side of a 6 m leg, invented Sa 100 MPa and K1 100 mm²/m²: ΔL = 12e-6·50·70 = 42 mm, leg as in the first case (L_req 4.85005 m, F_leg 388.109 N, M_leg 1164.33 N·m); the leg grows 5.04 mm, F_run = 12·210e9·7.9206e-7·0.00504/(30³ + 20³) = 0.287422 N, run 1 stress 241.95 kPa (u 0.049188, L_req 1.47565 m, M 4.3113 N·m), run 2 161.30 kPa (u 0.040162, M 2.8742 N·m); anchors √(388.1088² + 0.2874²) = 388.10892 N; screening as for a 50 m × 6 m L-bend: 118.168 mm²/m² against K1 100"),
                [Q("E", 210, "GPa"), Q("alpha", 12, "um/(m*K)"), Q("OD", 88.9, "mm"), Q("t_wall", 3.2, "mm"), Q("L", 30, "m"), Q("L2", 20, "m"), Choice("temp_mode", TwoTemperatures), Q("T_install", 10, "degC"), Q("T_operate", 80, "degC"), Choice("layout", OffsetLeg), Q("H", 6, "m"), Q("Sa", 100, "MPa"), Q("K_scr", 100, "mm^2/m^2")],
                [new CalcExpectedValue("L_tot", 50, "m", 1e-9), new CalcExpectedValue("dL", 42, "mm", 1e-9), new CalcExpectedValue("L_req", 4.85004536061262, "m", 1e-9), new CalcExpectedValue("Delta_run", 5.04, "mm", 1e-9), new CalcExpectedValue("F_run", 0.28742229497404465, "N", 1e-9), new CalcExpectedValue("sigma_run", 0.24195024, "MPa", 1e-9), new CalcExpectedValue("u_run", 0.049188437665776706, "", 1e-9), new CalcExpectedValue("L_req_run", 1.4756531299733011, "m", 1e-9), new CalcExpectedValue("M_run", 4.31133442461067, "N*m", 1e-9), new CalcExpectedValue("sigma_run2", 0.16130016, "MPa", 1e-9), new CalcExpectedValue("u_run2", 0.04016219117528326, "", 1e-9), new CalcExpectedValue("M_run2", 2.8742229497404463, "N*m", 1e-9), new CalcExpectedValue("anchor_force", 388.108915228184, "N", 1e-9), new CalcExpectedValue("anchor_moment", 1164.3264263994865, "N*m", 1e-9), new CalcExpectedValue("L_dev", 56, "m", 1e-9), new CalcExpectedValue("U_a", 50.35871324805669, "m", 1e-9), new CalcExpectedValue("scr", 118.16784619539838, "mm^2/m^2", 1e-9), new CalcExpectedValue("pass", 0, "", 1e-9)]),
            new CalcTestCase("ka-axial-bellows", "The 50 m steel line on an axial bellows: 6 bar on 80 cm² and 120 N/mm over 42 mm anchor at 9.84 kN",
                new CalcReferenceSource("Analytic (ΔL = α·L·ΔT; anchor force = P·A_eff + k·ΔL; recomputed in python3)", null, "88.9 × 3.2 mm steel, 12 µm/(m·K), 10 → 80 °C over 50 m: ΔL = 42 mm; a bellows at 6 bar gauge with A_eff 80 cm² thrusts 6e5·80e-4 = 4800 N and its spring of 120 N/mm gives 120e3·0.042 = 5040 N, so the anchors take 9840 N; against a rated movement of 50 mm the utilisation is 0.84 (met). No leg, no Sa, no screening. The bellows data are illustrative inputs, not a datasheet"),
                [Q("E", 210, "GPa"), Q("alpha", 12, "um/(m*K)"), Q("OD", 88.9, "mm"), Q("t_wall", 3.2, "mm"), Q("L", 50, "m"), Choice("temp_mode", TwoTemperatures), Q("T_install", 10, "degC"), Q("T_operate", 80, "degC"), Choice("layout", AxialBellows), Q("P_b", 6, "bar"), Q("A_eff", 80, "cm^2"), Q("k_b", 120, "N/mm"), Q("dL_rated", 50, "mm")],
                [new CalcExpectedValue("dL", 42, "mm", 1e-9), new CalcExpectedValue("F_thrust", 4.8, "kN", 1e-9), new CalcExpectedValue("F_spring", 5040, "N", 1e-9), new CalcExpectedValue("anchor_force", 9840, "N", 1e-9), new CalcExpectedValue("anchor_moment", 0, "N*m", 1e-9), new CalcExpectedValue("u_bellows", 0.84, "", 1e-9), new CalcExpectedValue("pass", 1, "", 1e-9)]),
        ],
        Category: "Thermal",
        OutputPorts:
        [
            new CalcOutputPort("anchor_force", "N", "Force on the anchors: the resultant of the force along the run (the leg's) and along the leg (the runs') for an offset leg or Z-bend, the leg's for a U-loop, or P·A_eff plus the spring force for an axial bellows"),
            new CalcOutputPort("anchor_moment", "N*m", "Largest root moment of the legs and runs in the guided-cantilever model (not a frame anchor moment); zero for an axial bellows"),
            new CalcOutputPort("dL", "mm", "Thermal growth (or contraction) of the run between anchors, as a magnitude"),
        ],
        RecordKinds:
        [
            new CalcRecordKind(MaterialKind, "Material properties", ["E", "alpha"], "A material from the library: its properties fill these inputs, each citing the material, its revision and the page or table of the value. Apply the material at the installation (reference) temperature, since E is the modulus for the stress check; α must be the mean coefficient between the install and operating temperatures. E_hot, the modulus for the reactions, is typed."),
            new CalcRecordKind("pipe.section", "Pipe outside diameter and wall", ["OD", "t_wall"], "The outside diameter and wall of the pipe from the manufacturer's datasheet or your copy of the pipe standard (minus any mill or corrosion allowance you apply); no pipe table is shipped. The symbols accept a link to an upstream calculation that declares them."),
            new CalcRecordKind("pipe.allowable-range", "Allowable displacement-stress range", ["Sa"], "Sa from your piping code and its formula (stress range reduction factor, allowable at the cold and hot temperatures), with the code, edition and clause. No allowable stress is shipped and there is no default."),
            new CalcRecordKind("pipe.screening-k1", "Screening limit K1", ["K_scr"], "K1 for the relation OD·Y/(L − U)² ≤ K1 from your piping code and its formula, in the units of the code (mm²/m², in²/ft², …) with the code, edition and clause. K1 is not shipped and there is no default; without it the screening ratio is shown and not judged, and the sheet does not pass."),
            new CalcRecordKind("bellows.data", "Axial bellows data", ["A_eff", "k_b", "dL_rated"], "The effective area, spring rate and rated axial movement of the bellows from the manufacturer's datasheet."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue Choice(string symbol, string choice) => CalcInputValue.Choice(symbol, choice);

    public void Run(CalcRunContext c)
    {
        foreach (var symbol in new[] { "E", "alpha", "OD", "t_wall", "L" })
            c.Input(symbol);

        if (!(2 * c.Si("t_wall") < c.Si("OD")))
        {
            c.Block("wall", $"t_wall: the wall ({F(c.Si("t_wall") * 1000)} mm) must be less than half the outside diameter ({F(c.Si("OD") * 1000)} mm); enter the wall and diameter of the pipe.");
            return;
        }

        var givenDirectly = c.InputText("temp_mode") == ChangeGiven;

        if (givenDirectly)
        {
            if (!c.Has("dT_in"))
            {
                c.Block("dT_in", $"dT_in: {c.Template.FindInput("dT_in")!.NextStepText()}");
                return;
            }

            c.Input("dT_in");
            c.Assign("dT", "dT_in", "K", "Temperature change (entered)");
        }
        else
        {
            c.Input("T_install");
            c.Input("T_operate");
            c.Assign("dT", "T_operate - T_install", "K", "Temperature change, operating − install");
        }

        if (!(Math.Abs(c.Si("dT")) > 1e-9))
        {
            c.Block(givenDirectly ? "dT_in" : "T_operate", givenDirectly ? "dT_in: the temperature change is zero, so there is no movement; enter a temperature change other than zero." : "T_operate: the operating temperature equals the install temperature, so there is no movement; enter an operating temperature different from the install temperature.");
            return;
        }

        var layout = c.InputText("layout");
        var bellows = layout == AxialBellows;
        var offset = layout == OffsetLeg;
        var grows = c.Si("dT") >= 0;
        var z = offset && c.Has("L2");

        if (z)
            c.Input("L2");
        else if (c.Has("L2"))
            c.Warn("l2-unused", "L2: the second run is only used with the offset-leg layout (a Z-bend); it is ignored here.");

        if (c.Has("E_hot"))
        {
            c.Input("E_hot");
            c.Assign("E_r", "E_hot", "GPa", "Modulus for the reactions (E_hot)");
        }
        else
        {
            c.Assign("E_r", "E", "GPa", "Modulus for the reactions (E, no E_hot given)");
        }

        foreach (var symbol in new[] { "E", "E_hot" })
        {
            if (symbol == "E" || c.Has("E_hot"))
            {
                var gpa = c.Si(symbol) / 1e9;

                if (gpa < PlausibleMinModulusGPa)
                    c.Warn("modulus-low", $"{symbol}: {F(gpa)} GPa is below any usual pipe material (steel 210, copper 120, PE and PP about 1 GPa); is E in GPa? A metal's modulus typed in MPa (steel 210 000 MPa) lands near 0.1 to 0.2 GPa, which makes a leg look 1000 times more flexible, passes a leg that fails and gives an anchor force 1000 times low. Enter {symbol} in GPa, or take it from a material.");
            }
        }

        // Section, movement and the restrained case: none of it needs Sa.
        c.Assign("D_i", "OD - 2 * t_wall", "mm", "Inside diameter");
        c.Assign("A_m", "pi / 4 * (OD^2 - D_i^2)", "mm^2", "Metal cross-section");
        c.Assign("I_x", "pi / 64 * (OD^4 - D_i^4)", "cm^4", "Second moment of area of the pipe section");
        c.Assign("Z_x", "I_x / (OD / 2)", "cm^3", "Section modulus");
        c.Assign("L_tot", z ? "L + L2" : "L", "m", z ? "Length that grows: the two runs of the Z-bend" : "Length that grows between the anchors");
        c.Assign("dL", "alpha * L_tot * abs(dT)", "mm", $"Thermal {(grows ? "growth" : "contraction (cold line)")} between the anchors, as a magnitude");
        c.Assign("dL_signed", "alpha * L_tot * dT", "mm", "Thermal movement with its sign: positive lengthens, negative shortens");
        c.Assign("sigma_res", "E * alpha * abs(dT)", "MPa", "Stress in the fully restrained pipe (E at the installation temperature)");
        c.Assign("F_res", "E_r * alpha * abs(dT) * A_m", "kN", "Force on the anchors of the fully restrained pipe");

        var direction = grows
            ? "growth: the pipe lengthens; fully restrained it presses on its anchors (compression)"
            : "contraction: the pipe shortens; fully restrained it pulls on its anchors (tension)";

        foreach (var (symbol, text) in new[] { ("D_i", "Inside diameter"), ("A_m", "Metal cross-section"), ("I_x", "Second moment of area"), ("Z_x", "Section modulus"), ("dT", "Temperature change"), ("L_tot", "Length that grows"), ("dL", "Thermal movement between the anchors (magnitude)"), ("dL_signed", "Thermal movement, signed"), ("sigma_res", "Fully restrained stress"), ("F_res", "Fully restrained force") })
            c.Output(symbol, text);

        c.Output("direction", "Direction of the movement", new TextValue(direction));

        if (bellows)
        {
            RunBellows(c);
            return;
        }

        if (!c.Has("Sa"))
        {
            c.Block("Sa", $"Sa: {c.Template.FindInput("Sa")!.NextStepText()}");
            return;
        }

        if (!c.Has("H"))
        {
            c.Block("H", $"H: {c.Template.FindInput("H")!.NextStepText()}");
            return;
        }

        c.Input("Sa");
        c.Input("H");
        var od = c.Si("OD");
        var leg = c.Si("H");
        var run = c.Si("L");

        if (new[] { "P_b", "A_eff", "k_b", "dL_rated" }.FirstOrDefault(c.Has) is { } unusedBellows)
            c.Warn("bellows-unused", $"{unusedBellows}: bellows inputs are given with a leg or loop layout and are not used; choose the axial bellows layout to size the anchors for a bellows.");

        c.Assign("u_res", "sigma_res / Sa", null, "Fully restrained stress as a fraction of Sa (above 1: the movement has to be absorbed)");

        // The leg.
        c.Assign("Delta", offset ? "dL" : "dL / 2", "mm", offset ? "Movement the offset leg absorbs (all of it)" : "Movement each leg of the U-loop absorbs (half of it)");
        c.Assign("L_req", "sqrt(3 * E * OD * Delta / Sa)", "m", "Leg length at which the guided-cantilever bending stress reaches Sa");
        c.Assign("u_leg", "L_req / H", null, "Required leg over available leg");
        c.Assign("F_leg", "12 * E_r * I_x * Delta / H^3", "N", "Force of the available leg on its anchors or guides");
        c.Assign("M_leg", "6 * E_r * I_x * Delta / H^2", "N*m", "Moment at the root of the available leg");
        c.Assign("sigma_leg", "3 * E * OD * Delta / H^2", "MPa", "Bending stress at the root of the available leg (E at the installation temperature)");
        c.Assign("u_sigma", "sigma_leg / Sa", null, "Bending stress over Sa (= u_leg²)");

        if (offset)
        {
            // An offset leg or Z-bend is anchored at both ends: the runs bend, in series under one force, for the growth of the leg.
            c.Assign("Delta_run", "alpha * abs(dT) * H", "mm", "Growth of the leg, which the run or runs have to absorb");
            c.Assign("S_run", z ? "L^3 + L2^3" : "L^3", "m^3", z ? "Sum of the cubes of the two runs (their flexibility in series)" : "Cube of the run (its flexibility)");
            c.Assign("F_run", "12 * E_r * I_x * Delta_run / S_run", "N", "Force of the run or runs, along the leg");
            c.Assign("sigma_run", "3 * E * OD * Delta_run * L / S_run", "MPa", z ? "Bending stress at the root of the first run" : "Bending stress at the root of the run");
            c.Assign("u_run", "sqrt(sigma_run / Sa)", null, z ? "First run: required length over its length (= √(σ/Sa))" : "Required run over available run (= √(σ/Sa))");
            c.Assign("L_req_run", "L * u_run", "m", "Cantilever length for this run's share of the leg's growth");
            c.Assign("M_run", "F_run * L / 2", "N*m", z ? "Moment at the root of the first run" : "Moment at the root of the run");

            if (z)
            {
                c.Assign("sigma_run2", "3 * E * OD * Delta_run * L2 / S_run", "MPa", "Bending stress at the root of the second run");
                c.Assign("u_run2", "sqrt(sigma_run2 / Sa)", null, "Second run: required length over its length (= √(σ/Sa))");
                c.Assign("L_req_run2", "L2 * u_run2", "m", "Cantilever length for this run's share of the leg's growth");
                c.Assign("M_run2", "F_run * L2 / 2", "N*m", "Moment at the root of the second run");
            }

            c.Assign("F_along_run", "F_leg", "N", "Component of the anchor force along the run: the leg's force");
            c.Assign("F_along_leg", "F_run", "N", "Component of the anchor force along the leg: the runs' force");
            c.Assign("anchor_force", "sqrt(F_leg^2 + F_run^2)", "N", "Force on the anchors: resultant of the two perpendicular components");
            c.Assign("anchor_moment", z ? "max(max(M_leg, M_run), M_run2)" : "max(M_leg, M_run)", "N*m", "Largest root moment of the leg and the run or runs (guided-cantilever model)");
        }
        else
        {
            c.Assign("F_along_run", "F_leg", "N", "Component of the anchor force along the run: the loop's force");
            c.Record("F_along_leg", "no component along the leg", "a U-loop is in line", ScalarValue.Of(0, "N"), "Component of the anchor force along the leg: none for a U-loop");
            c.Assign("anchor_force", "F_leg", "N", "Force on the anchors (the force along the loop)");
            c.Assign("anchor_moment", "M_leg", "N*m", "Moment at the root of the loop's legs");
        }

        if (leg < ShortLegDiameters * od)
            c.Warn("short-leg", $"H: the available leg ({F(leg)} m) is shorter than ten pipe diameters ({F(ShortLegDiameters * od)} m); the guided-cantilever beam model is not valid for such short legs. Use a flexibility analysis.");

        if (offset && run < ShortLegDiameters * od)
            c.Warn("short-leg", $"L: the run ({F(run)} m) is shorter than ten pipe diameters ({F(ShortLegDiameters * od)} m); the guided-cantilever beam model is not valid for such a short run. Use a flexibility analysis.");

        if (z && c.Si("L2") < ShortLegDiameters * od)
            c.Warn("short-leg", $"L2: the second run ({F(c.Si("L2"))} m) is shorter than ten pipe diameters ({F(ShortLegDiameters * od)} m); the guided-cantilever beam model is not valid for such a short run. Use a flexibility analysis.");

        // Screening: Y is the free growth between the anchors, alpha·|dT|·U.
        var hasK1 = c.Has("K_scr");
        c.Assign("L_dev", offset ? "L_tot + H" : "L + 2 * H", "m", offset ? "Developed length between the anchors, run or runs plus leg" : "Developed length between the anchors, run plus two legs");
        c.Assign("U_a", offset ? "sqrt(L_tot^2 + H^2)" : "L", "m", offset ? "Straight distance between the anchors" : "Straight distance between the anchors (the loop is in line)");
        c.Assign("Y_scr", "alpha * abs(dT) * U_a", "mm", "Resultant displacement Y: the free growth between the anchors");
        c.Assign("scr", "OD * Y_scr / (L_dev - U_a)^2", "mm^2/m^2", "Screening ratio OD·Y/(L − U)²");

        if (hasK1)
        {
            c.Input("K_scr");
            c.Assign("u_scr", "scr / K_scr", null, "Screening ratio over the K1 record");
        }
        else
        {
            c.Warn("screening-k1", "K_scr: no K1 was entered, so the screening ratio is shown and not judged and the sheet does not pass; enter K1 from your piping code (it has no default), or treat the line as needing a formal stress analysis.");
        }

        c.CheckUtilisation($"Available leg against the required leg ({F(c.Si("L_req"))} m)", "u_leg");

        if (offset)
            c.CheckUtilisation($"{(z ? "First run" : "Run")} against the length it needs to absorb the leg's growth ({F(c.Si("L_req_run"))} m)", "u_run");

        if (z)
            c.CheckUtilisation($"Second run against the length it needs to absorb the leg's growth ({F(c.Si("L_req_run2"))} m)", "u_run2");

        if (hasK1)
            c.CheckUtilisation("Screening only: OD·Y/(L − U)² ≤ K1 (not met: the line needs a formal stress analysis)", "u_scr", ScreeningClause);

        var screenText = !hasK1 ? "not evaluated: K1 not entered (treat as: needs formal stress analysis)" : c.Si("u_scr") <= 1 + 1e-9 ? "PASS" : "needs formal stress analysis";
        var passed = hasK1 && c.Checks.All(k => k.Passed);
        c.Record("pass", "K1 given and every check passes", hasK1 ? $"{c.Checks.Count} check(s)" : "no K1: the screening is not evaluated", new ScalarValue(passed ? 1 : 0), "1 when the leg, the run (offset leg) and the screening checks all pass; 0 when one fails or when no K1 was entered");

        c.Assume($"Layout: {(offset ? "the offset leg of " + F(leg) + " m takes the whole movement of " + F(c.Si("dL") * 1000) + " mm as a guided cantilever fixed against rotation at both ends, and the " + (z ? "two runs of " + F(run) + " m and " + F(c.Si("L2")) + " m bend" : "run of " + F(run) + " m bends") + " for the leg's growth of " + F(c.Si("Delta_run") * 1000) + " mm." : "the U-loop is two legs of " + F(leg) + " m in series, each a guided cantilever taking half the movement (" + F(c.Si("Delta") * 1000) + " mm); the cross leg is a rigid guide and its width does not enter.")} Screening only — not a piping stress analysis.", "Guided-cantilever model.");

        if (c.Si("u_leg") > 1 + 1e-9)
            c.Assume($"The available leg ({F(leg)} m) is shorter than the {F(c.Si("L_req"))} m required: the bending stress at its root is {F(c.Si("sigma_leg") / 1e6)} MPa against Sa = {F(c.Si("Sa") / 1e6)} MPa. Lengthen the leg or loop, add a loop or bellows, or analyse the line formally.", "Result of the check.");

        foreach (var (symbol, text) in new[] { ("u_res", "Fully restrained stress over Sa"), ("Delta", "Movement absorbed by each leg"), ("L_req", "Required leg length"), ("u_leg", "Required leg over available leg"), ("F_leg", "Force of the leg"), ("M_leg", "Moment at the leg root"), ("sigma_leg", "Bending stress at the leg root"), ("u_sigma", "Bending stress over Sa") })
            c.Output(symbol, text);

        if (offset)
        {
            foreach (var (symbol, text) in new[] { ("Delta_run", "Leg growth absorbed by the run or runs"), ("S_run", "Flexibility of the runs"), ("F_run", "Force of the runs"), ("sigma_run", "Bending stress at the first run root"), ("L_req_run", "Required length of the first run"), ("u_run", "First run: required over available"), ("M_run", "Moment at the first run root") })
                c.Output(symbol, text);

            if (z)
            {
                foreach (var (symbol, text) in new[] { ("sigma_run2", "Bending stress at the second run root"), ("L_req_run2", "Required length of the second run"), ("u_run2", "Second run: required over available"), ("M_run2", "Moment at the second run root") })
                    c.Output(symbol, text);
            }
        }

        foreach (var (symbol, text) in new[] { ("F_along_run", "Anchor force component along the run"), ("F_along_leg", "Anchor force component along the leg"), ("anchor_force", "Force on the anchors (resultant)"), ("anchor_moment", "Largest root moment (guided-cantilever model)"), ("L_dev", "Developed length"), ("U_a", "Anchor distance"), ("Y_scr", "Resultant displacement Y"), ("scr", "Screening ratio OD·Y/(L − U)²") })
            c.Output(symbol, text);

        if (hasK1)
            c.Output("u_scr", "Screening ratio over K1");

        c.Output("screen", "Screening result", new TextValue(screenText));
        c.Output("pass", "All checks pass");
    }

    /// <summary>The axial bellows layout: no leg, no Sa and no screening; the anchors take the pressure thrust plus the spring force.</summary>
    private static void RunBellows(CalcRunContext c)
    {
        foreach (var symbol in new[] { "P_b", "A_eff" })
        {
            if (!c.Has(symbol))
            {
                c.Block("bellows", $"{symbol}: {c.Template.FindInput(symbol)!.NextStepText()} The axial bellows layout needs both the pressure P_b and the effective area A_eff.");
                return;
            }

            c.Input(symbol);
        }

        c.Assign("F_thrust", "P_b * A_eff", "kN", "Bellows pressure thrust on the anchors, at the governing (design or test) pressure");

        if (c.Has("k_b"))
        {
            c.Input("k_b");
            c.Assign("F_spring", "k_b * dL", "N", "Bellows spring force at the full movement");
            c.Assign("anchor_force", "F_thrust + F_spring", "N", "Force on the anchors: pressure thrust plus spring force");
        }
        else
        {
            c.Assign("anchor_force", "F_thrust", "N", "Force on the anchors: pressure thrust (no spring rate given)");
        }

        c.Record("anchor_moment", "axial bellows: no bending", "the movement is axial", ScalarValue.Of(0, "N*m"), "Moment at the anchors: none from an axial bellows");

        if (c.Has("Sa"))
        {
            c.Input("Sa");
            c.Assign("u_res", "sigma_res / Sa", null, "Fully restrained stress as a fraction of Sa (above 1: the movement has to be absorbed)");
            c.Output("u_res", "Fully restrained stress over Sa");
        }

        if (c.Has("K_scr"))
            c.Warn("k1-unused", "K_scr: the axial bellows layout has no screening; K1 is not used.");

        var rated = c.Has("dL_rated");

        if (rated)
        {
            c.Input("dL_rated");
            c.Assign("u_bellows", "dL / dL_rated", null, "Movement over the bellows' rated axial movement");
            c.CheckUtilisation($"Movement against the rated axial movement of the bellows ({F(c.Si("dL_rated") * 1000)} mm)", "u_bellows");
        }

        c.Assume($"Layout: an axial bellows absorbs the whole movement of {F(c.Si("dL") * 1000)} mm; there is no leg check and no screening. The anchors take the pressure thrust P·A_eff plus the spring force k·ΔL; guide friction, lateral and angular movement and the bellows' own limits are the manufacturer's. Screening only — not a piping stress analysis.", "Bellows layout.");

        foreach (var (symbol, text) in new[] { ("F_thrust", "Bellows pressure thrust"), ("anchor_force", "Force on the anchors"), ("anchor_moment", "Moment at the anchors") })
            c.Output(symbol, text);

        if (c.Has("k_b"))
            c.Output("F_spring", "Bellows spring force");

        if (rated)
            c.Output("u_bellows", "Movement over the rated movement");

        var passed = rated && c.Checks.All(k => k.Passed);
        c.Record("pass", "the rated movement is given and met", rated ? $"{c.Checks.Count} check(s)" : "no rated movement: nothing is checked", new ScalarValue(passed ? 1 : 0), "1 when the movement is within the bellows' rated axial movement; 0 when it is not or when no rating was entered");
        c.Output("screen", "Screening result", new TextValue("not applicable: the bellows absorbs the movement"));
        c.Output("pass", "All checks pass");
    }

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
