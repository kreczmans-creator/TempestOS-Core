using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.BuildingServices;

/// <summary>
/// B8 — pipe thermal expansion, expansion legs and loops, and anchor forces: the growth ΔL = α·L·ΔT between anchors, the fully
/// restrained stress and force, the leg a guided cantilever needs to absorb the growth within a user-supplied allowable range
/// (an offset leg takes all of it; a U-loop is two legs that take half each), the force and moment the available leg puts on its
/// anchors, a screening ratio D·Y/(L − U)² against a user-supplied K1, and the pressure thrust of a bellows. A screening
/// calculation only: it is not a piping stress analysis and ships no allowable stress and no code constant.
/// </summary>
public sealed class PipeExpansionCalculator : ICalcCalculator
{
    public const string Id = "bs.pipe-expansion";

    public const string TwoTemperatures = "install and operating temperatures";
    public const string ChangeGiven = "temperature change entered";

    public const string OffsetLeg = "offset leg (L- or Z-bend: the leg takes the whole movement)";
    public const string UpLoop = "U-loop (two legs, each takes half the movement)";

    /// <summary>The record kind of a W3 material (<c>MaterialLibrary.Kind</c>; written out here because this assembly does not reference the library, and a test keeps the two equal).</summary>
    public const string MaterialKind = "material.properties";

    /// <summary>Legs shorter than this many pipe diameters are outside the beam model.</summary>
    public const double ShortLegDiameters = 10;

    private const string ScreeningClause = "ASME B31.3 para. 319.4.1 (screening form; number to be checked against the owner's copy)";

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.BuildingServices, "Pipe expansion, loops and anchor forces (screening)",
        "Screening only — not a piping stress analysis. Thermal growth of a straight run between anchors, the fully restrained stress and force, the leg length an offset leg or a U-loop (two guided cantilevers) needs to absorb the growth within the owner's allowable displacement-stress range Sa and whether the available leg is long enough, the force and moment that leg puts on its anchors, the screening ratio D·Y/(L − U)² against the owner's K1 (PASS, or the line needs a formal stress analysis), and the pressure thrust P·A_eff of a bellows. No allowable stress, K1 or code table ships.",
        "Pipe section: d_i = OD − 2·t; A_m = π/4·(OD² − d_i²); I = π/64·(OD⁴ − d_i⁴); Z = I/(OD/2)\n" +
        "Temperature change: ΔT = T_operate − T_install, or as entered; the movement is taken for |ΔT|\n" +
        "Thermal growth between anchors: ΔL = α·L·|ΔT|\n" +
        "Fully restrained pipe: σ_r = E·α·|ΔT|; F_r = σ_r·A_m\n" +
        "Movement one leg must absorb: Δ = ΔL for an offset leg; Δ = ΔL/2 for a U-loop (two identical legs in series, the cross leg a rigid guide)\n" +
        "Guided cantilever, required leg: L_req = √(3·E·OD·Δ/Sa), the leg at which the bending stress M/Z reaches Sa; PASS when L_req ≤ the available leg H\n" +
        "Force and moment of the available leg: F = 12·E·I·Δ/H³; M = 6·E·I·Δ/H²; σ_leg = M/Z = 3·E·OD·Δ/H² = Sa·(L_req/H)²\n" +
        "Screening: OD·Y/(L_dev − U)² ≤ K1 with Y = ΔL; offset leg L_dev = L + H, U = √(L² + H²); U-loop L_dev = L + 2·H, U = L; PASS, or the line needs a formal stress analysis\n" +
        "Bellows pressure thrust: F_p = P·A_eff",
        [
            new CalcInputSpec("E", "Young's modulus of the pipe material at the operating temperature", CalcInputKind.Quantity, "GPa", Default: 210, Min: 0.1, Max: 500, HardMin: 0.01, HardMax: 1000, Group: "Material", MaterialProperty: "E"),
            new CalcInputSpec("alpha", "Mean coefficient of thermal expansion between the install and operating temperatures", CalcInputKind.Quantity, "um/(m*K)", Default: 12, Min: 1, Max: 250, HardMin: 0.1, HardMax: 1000, Group: "Material", MaterialProperty: "alpha"),
            new CalcInputSpec("OD", "Pipe outside diameter", CalcInputKind.Quantity, "mm", Default: 88.9, Min: 10, Max: 1500, HardMin: 1, HardMax: 10000, Group: "Pipe"),
            new CalcInputSpec("t_wall", "Pipe wall thickness", CalcInputKind.Quantity, "mm", Default: 3.2, Min: 0.5, Max: 60, HardMin: 0.1, HardMax: 1000, Group: "Pipe"),
            new CalcInputSpec("L", "Anchor spacing: the straight length that grows", CalcInputKind.Quantity, "m", Default: 50, Min: 1, Max: 500, HardMin: 0.1, HardMax: 10000, Group: "Movement"),
            new CalcInputSpec("temp_mode", "Temperature change from", CalcInputKind.Choice, Choices: [TwoTemperatures, ChangeGiven], DefaultChoice: TwoTemperatures, Group: "Movement"),
            new CalcInputSpec("T_install", "Temperature at installation (when the run is anchored)", CalcInputKind.Quantity, "degC", Default: 10, Min: -50, Max: 50, HardMin: -100, HardMax: 500, Group: "Movement"),
            new CalcInputSpec("T_operate", "Operating temperature (hot or cold)", CalcInputKind.Quantity, "degC", Default: 80, Min: -50, Max: 500, HardMin: -200, HardMax: 1000, Group: "Movement"),
            new CalcInputSpec("dT_in", "Temperature change ΔT (negative for a cold line), when entered directly", CalcInputKind.Quantity, "K", Min: -300, Max: 500, HardMin: -1000, HardMax: 1000, IsOptional: true, Group: "Movement"),
            new CalcInputSpec("layout", "What absorbs the movement", CalcInputKind.Choice, Choices: [OffsetLeg, UpLoop], DefaultChoice: OffsetLeg, Group: "Layout"),
            new CalcInputSpec("H", "Available leg length (each leg of a U-loop)", CalcInputKind.Quantity, "m", Default: 5, Min: 0.5, Max: 50, HardMin: 0.01, HardMax: 1000, Group: "Layout"),
            new CalcInputSpec("Sa", "Allowable displacement-stress range Sa for the leg (your piping code and its formula; no default)", CalcInputKind.Quantity, "MPa", Min: 5, Max: 500, HardMin: 0.1, HardMax: 5000, IsOptional: true, Group: "Criteria"),
            new CalcInputSpec("K_scr", "Screening limit K1 for OD·Y/(L − U)², in mm²/m² (or in²/ft², …) from your piping code (no default; blank = screening not evaluated)", CalcInputKind.Quantity, "mm^2/m^2", Min: 1, Max: 1000, HardMin: 1e-6, HardMax: 1e9, IsOptional: true, Group: "Criteria"),
            new CalcInputSpec("P_b", "Bellows design pressure, gauge (blank = no bellows thrust)", CalcInputKind.Quantity, "bar", Min: 0.1, Max: 40, HardMin: 0, HardMax: 1000, IsOptional: true, Group: "Bellows"),
            new CalcInputSpec("A_eff", "Bellows effective area A_eff (manufacturer's data)", CalcInputKind.Quantity, "cm^2", Min: 1, Max: 5000, HardMin: 0.01, HardMax: 1e6, IsOptional: true, Group: "Bellows"),
        ],
        [
            new CalcAssumption("Screening only — not a piping stress analysis. The result tells whether a simple leg or loop is plausible and what it puts on its anchors; it does not replace a flexibility analysis of the real routing (elbow flexibility, intensification, torsion, branches, restraint friction, supports, sustained and occasional loads).", "Stated scope."),
            new CalcAssumption("A guided cantilever: the available leg is straight, uniform, fixed against rotation at both ends and loaded only by the movement it absorbs (stiffness 12·E·I/H³, end moment F·H/2). Elbows are taken as rigid and the leg as slender: the beam model is not valid for legs shorter than about ten diameters.", "Beam theory."),
            new CalcAssumption("A U-loop is two identical legs in series, each a guided cantilever taking half the growth, the cross leg a rigid guide; the force along the line is the same F in both legs. The moment M is the moment at the leg roots (the corners of the loop); it reaches an anchor only where the leg ends at the anchor (an offset leg).", "Loop model."),
            new CalcAssumption("The material properties are single values: α the mean coefficient between the install and operating temperatures, E at the operating temperature. Take them from a W3 material applied at that temperature.", "Material library."),
            new CalcAssumption("Sa and K1 are the owner's records from the owner's own copy of the piping code (or the design basis), with their provenance; no allowable stress, K1 or code constant ships. The sheet applies the relation to the numbers entered and does not know which code, material or load case they belong to.", "No table from a standard is shipped."),
            new CalcAssumption("No pressure thrust, friction, weight, spring-hanger or cold-spring effect is included in the leg force. The bellows thrust is the pressure thrust P·A_eff alone: the spring force of the bellows and the friction of the guides are added from the manufacturer's data.", "Stated limit."),
        ],
        [
            new CalcClauseRef("ASME B31.3", "para. 319.4.1 (screening relation; number to be checked against the owner's copy)", "Process piping — flexibility analysis, exemption from formal analysis"),
            new CalcClauseRef("EN 13480-3", "cl. 12 (number to be checked against the owner's copy)", "Metallic industrial piping — flexibility analysis"),
        ],
        [
            new CalcApplicabilityRule("wall", "The wall must be less than half the outside diameter.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("Sa", "Enter the allowable displacement-stress range Sa; it has no default.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("dT_in", "Enter the temperature change when it is the source of the movement; it must not be zero.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("dT-zero", "Install and operating temperatures are equal: there is no movement to absorb.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("short-leg", "The required or the available leg is shorter than ten pipe diameters: the guided-cantilever beam model is not valid.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("bellows-incomplete", "A bellows thrust needs both the pressure and the effective area.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("screening-k1", "No K1 was entered: the screening ratio is shown and not judged.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-offset-leg-steel-50m", "Steel pipe 50 m between anchors, 70 K: growth 42 mm, an offset leg of 6 m against 4.850 m required",
                new CalcReferenceSource("Analytic (ΔL = α·L·ΔT; guided cantilever L = √(3·E·D·Δ/Sa), F = 12·E·I·Δ/L³, M = 6·E·I·Δ/L²; recomputed in python3)", null, "88.9 × 3.2 mm steel, E 210 GPa, α 12 µm/(m·K), 10 → 80 °C over 50 m: ΔL = 12e-6·50·70 = 42 mm; fully restrained σ_r = 176.4 MPa and F_r = 151.98 kN on A_m = 861.55 mm²; an invented Sa of 100 MPa gives L_req = 4.85005 m, so the 6 m offset leg passes (u 0.80834) with F = 388.11 N and M = 1164.33 N·m (σ_leg 65.34 MPa); the invented K1 of 100 mm²/m² is exceeded by 117.33 mm²/m² (L_dev 56 m, U 50.3587 m), so the line needs a formal stress analysis. Sa and K1 are illustrative inputs, not code values"),
                [Q("E", 210, "GPa"), Q("alpha", 12, "um/(m*K)"), Q("OD", 88.9, "mm"), Q("t_wall", 3.2, "mm"), Q("L", 50, "m"), Choice("temp_mode", TwoTemperatures), Q("T_install", 10, "degC"), Q("T_operate", 80, "degC"), Choice("layout", OffsetLeg), Q("H", 6, "m"), Q("Sa", 100, "MPa"), Q("K_scr", 100, "mm^2/m^2")],
                [new CalcExpectedValue("dL", 42, "mm", 1e-9), new CalcExpectedValue("dT", 70, "K", 1e-9), new CalcExpectedValue("sigma_res", 176.4, "MPa", 1e-9), new CalcExpectedValue("A_m", 861.5503693204649, "mm^2", 1e-9), new CalcExpectedValue("F_res", 151.97748514813, "kN", 1e-9), new CalcExpectedValue("L_req", 4.85004536061262, "m", 1e-9), new CalcExpectedValue("u_leg", 0.8083408934354367, "", 1e-9), new CalcExpectedValue("anchor_force", 388.1088087998288, "N", 1e-9), new CalcExpectedValue("anchor_moment", 1164.3264263994865, "N*m", 1e-9), new CalcExpectedValue("sigma_leg", 65.3415, "MPa", 1e-9), new CalcExpectedValue("L_dev", 56, "m", 1e-9), new CalcExpectedValue("U_a", 50.35871324805669, "m", 1e-9), new CalcExpectedValue("scr", 117.32611754129601, "mm^2/m^2", 1e-9), new CalcExpectedValue("u_scr", 1.17326117541296, "", 1e-9), new CalcExpectedValue("pass", 0, "", 1e-9)]),
            new CalcTestCase("ka-u-loop-too-short", "The same line with a U-loop of two 3 m legs: each takes 21 mm and needs 3.429 m, so the loop is too short",
                new CalcReferenceSource("Analytic (U-loop as two guided cantilevers, Δ = ΔL/2; recomputed in python3)", null, "As the previous case with a U-loop of legs H = 3 m: Δ = 21 mm, L_req = √(3·210e9·0.0889·0.021/100e6) = 3.42950 m, u 1.14317 (FAIL), F = 1552.44 N, M = 2328.65 N·m, σ_leg 130.68 MPa = Sa·u²; L_dev = 56 m, U = L = 50 m, so the screening ratio is 103.717 mm²/m²; an invented K1 of 120 mm²/m² is met (u 0.86431)"),
                [Q("E", 210, "GPa"), Q("alpha", 12, "um/(m*K)"), Q("OD", 88.9, "mm"), Q("t_wall", 3.2, "mm"), Q("L", 50, "m"), Choice("temp_mode", TwoTemperatures), Q("T_install", 10, "degC"), Q("T_operate", 80, "degC"), Choice("layout", UpLoop), Q("H", 3, "m"), Q("Sa", 100, "MPa"), Q("K_scr", 120, "mm^2/m^2")],
                [new CalcExpectedValue("dL", 42, "mm", 1e-9), new CalcExpectedValue("Delta", 21, "mm", 1e-9), new CalcExpectedValue("L_req", 3.4294999635515384, "m", 1e-9), new CalcExpectedValue("u_leg", 1.1431666545171795, "", 1e-9), new CalcExpectedValue("anchor_force", 1552.4352351993152, "N", 1e-9), new CalcExpectedValue("anchor_moment", 2328.652852798973, "N*m", 1e-9), new CalcExpectedValue("sigma_leg", 130.683, "MPa", 1e-9), new CalcExpectedValue("L_dev", 56, "m", 1e-9), new CalcExpectedValue("U_a", 50, "m", 1e-9), new CalcExpectedValue("scr", 103.71666666666668, "mm^2/m^2", 1e-9), new CalcExpectedValue("u_scr", 0.8643055555555557, "", 1e-9), new CalcExpectedValue("pass", 0, "", 1e-9)]),
            new CalcTestCase("ka-cold-copper-bellows", "A cold copper line (ΔT −40 K) with a 1.5 m U-loop leg and a bellows thrust of 5 kN",
                new CalcReferenceSource("Analytic (ΔL = α·L·|ΔT|; U-loop Δ = ΔL/2; P·A_eff; recomputed in python3)", null, "28 × 1.2 mm copper, E 120 GPa, α 16.6 µm/(m·K), 30 m, ΔT = −40 K entered: ΔL = 16.6e-6·30·40 = 19.92 mm (a contraction), fully restrained σ_r = 79.68 MPa and F_r = 8.0504 kN on A_m = 101.03 mm²; U-loop legs of 1.5 m, an invented Sa of 60 MPa: Δ = 9.96 mm, L_req = 1.29355 m, u 0.86237; a bellows at 10 bar gauge on A_eff 50 cm² gives F_p = 10e5·50e-4 = 5000 N. Sa and K1 are illustrative inputs, not code values"),
                [Q("E", 120, "GPa"), Q("alpha", 16.6, "um/(m*K)"), Q("OD", 28, "mm"), Q("t_wall", 1.2, "mm"), Q("L", 30, "m"), Choice("temp_mode", ChangeGiven), Q("dT_in", -40, "K"), Choice("layout", UpLoop), Q("H", 1.5, "m"), Q("Sa", 60, "MPa"), Q("K_scr", 150, "mm^2/m^2"), Q("P_b", 10, "bar"), Q("A_eff", 50, "cm^2")],
                [new CalcExpectedValue("dT", -40, "K", 1e-9), new CalcExpectedValue("dL", 19.92, "mm", 1e-9), new CalcExpectedValue("sigma_res", 79.68, "MPa", 1e-9), new CalcExpectedValue("F_res", 8.0503588208392, "kN", 1e-9), new CalcExpectedValue("Delta", 9.96, "mm", 1e-9), new CalcExpectedValue("L_req", 1.2935532459083392, "m", 1e-9), new CalcExpectedValue("u_leg", 0.8623688306055595, "", 1e-9), new CalcExpectedValue("anchor_force", 38.62454824121034, "N", 1e-9), new CalcExpectedValue("anchor_moment", 28.968411180907758, "N*m", 1e-9), new CalcExpectedValue("scr", 61.97333333333333, "mm^2/m^2", 1e-9), new CalcExpectedValue("u_scr", 0.41315555555555555, "", 1e-9), new CalcExpectedValue("F_thrust", 5, "kN", 1e-9), new CalcExpectedValue("pass", 1, "", 1e-9)]),
        ],
        Category: "Thermal",
        OutputPorts:
        [
            new CalcOutputPort("anchor_force", "N", "Force the available leg puts on its anchors or guides, F = 12·E·I·Δ/H³"),
            new CalcOutputPort("anchor_moment", "N*m", "Moment at the root of the available leg, M = 6·E·I·Δ/H²"),
            new CalcOutputPort("dL", "mm", "Thermal growth of the run between anchors"),
        ],
        RecordKinds:
        [
            new CalcRecordKind(MaterialKind, "Material properties", ["E", "alpha"], "A material from the library: its properties fill these inputs, each citing the material, its revision and the page or table of the value. Apply the material at the operating temperature so a temperature table gives E there; α must be the mean coefficient between the install and operating temperatures."),
            new CalcRecordKind("pipe.section", "Pipe outside diameter and wall", ["OD", "t_wall"], "The outside diameter and wall of the pipe from the manufacturer's datasheet or your copy of the pipe standard (minus any mill or corrosion allowance you apply); no pipe table is shipped. The symbols accept a link to an upstream calculation that declares them."),
            new CalcRecordKind("pipe.allowable-range", "Allowable displacement-stress range", ["Sa"], "Sa from your piping code and its formula (stress range reduction factor, allowable at the cold and hot temperatures), with the code, edition and clause. No allowable stress is shipped and there is no default."),
            new CalcRecordKind("pipe.screening-k1", "Screening limit K1", ["K_scr"], "K1 for the relation OD·Y/(L − U)² ≤ K1 from your piping code and its formula, in the units of the code (mm²/m², in²/ft², …) with the code, edition and clause. K1 is not shipped and there is no default; without it the screening ratio is shown and not judged."),
            new CalcRecordKind("bellows.effective-area", "Bellows effective area", ["A_eff"], "The effective area of the bellows (and its spring rate, which this sheet does not use) from the manufacturer's datasheet."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue Choice(string symbol, string choice) => CalcInputValue.Choice(symbol, choice);

    public void Run(CalcRunContext c)
    {
        foreach (var symbol in new[] { "E", "alpha", "OD", "t_wall", "L", "H" })
            c.Input(symbol);

        if (!(2 * c.Si("t_wall") < c.Si("OD")))
        {
            c.Block("wall", $"t_wall: the wall ({F(c.Si("t_wall") * 1000)} mm) must be less than half the outside diameter ({F(c.Si("OD") * 1000)} mm); enter the wall and diameter of the pipe.");
            return;
        }

        if (!c.Has("Sa"))
        {
            c.Block("Sa", $"Sa: {c.Template.FindInput("Sa")!.NextStepText()}");
            return;
        }

        c.Input("Sa");
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

        var offset = c.InputText("layout") == OffsetLeg;
        var dTSi = c.Si("dT");
        var kind = dTSi >= 0 ? "growth" : "contraction (cold line)";

        // Section.
        var od = c.Si("OD");
        var leg = c.Si("H");
        c.Assign("D_i", "OD - 2 * t_wall", "mm", "Inside diameter");
        c.Assign("A_m", "pi / 4 * (OD^2 - D_i^2)", "mm^2", "Metal cross-section");
        c.Assign("I_x", "pi / 64 * (OD^4 - D_i^4)", "cm^4", "Second moment of area of the pipe section");
        c.Assign("Z_x", "I_x / (OD / 2)", "cm^3", "Section modulus");

        // Movement and the restrained case.
        c.Assign("dL", "alpha * L * abs(dT)", "mm", $"Thermal {kind} between the anchors");
        c.Assign("sigma_res", "E * alpha * abs(dT)", "MPa", "Stress in the fully restrained pipe");
        c.Assign("F_res", "sigma_res * A_m", "kN", "Force on the anchors of the fully restrained pipe");
        c.Assign("u_res", "sigma_res / Sa", null, "Fully restrained stress as a fraction of Sa (above 1: the movement has to be absorbed)");

        // Leg.
        c.Assign("Delta", offset ? "dL" : "dL / 2", "mm", offset ? "Movement the offset leg absorbs (all of it)" : "Movement each leg of the U-loop absorbs (half of it)");
        c.Assign("L_req", "sqrt(3 * E * OD * Delta / Sa)", "m", "Leg length at which the guided-cantilever bending stress reaches Sa");
        c.Assign("u_leg", "L_req / H", null, "Required leg over available leg");
        c.Assign("anchor_force", "12 * E * I_x * Delta / H^3", "N", "Force of the available leg on its anchors or guides");
        c.Assign("anchor_moment", "6 * E * I_x * Delta / H^2", "N*m", "Moment at the root of the available leg");
        c.Assign("sigma_leg", "anchor_moment / Z_x", "MPa", "Bending stress at the root of the available leg");
        c.Assign("u_sigma", "sigma_leg / Sa", null, "Bending stress over Sa (= u_leg²)");

        if (c.Si("L_req") < ShortLegDiameters * od || leg < ShortLegDiameters * od)
            c.Warn("short-leg", $"H: the required leg ({F(c.Si("L_req"))} m) or the available leg ({F(leg)} m) is shorter than ten pipe diameters ({F(ShortLegDiameters * od)} m); the guided-cantilever beam model is not valid for such short legs. Use a flexibility analysis.");

        // Screening.
        var hasK1 = c.Has("K_scr");
        c.Assign("L_dev", offset ? "L + H" : "L + 2 * H", "m", offset ? "Developed length between the anchors, run plus leg" : "Developed length between the anchors, run plus two legs");
        c.Assign("U_a", offset ? "sqrt(L^2 + H^2)" : "L", "m", offset ? "Straight distance between the anchors" : "Straight distance between the anchors (the loop is in line)");
        c.Assign("scr", "OD * dL / (L_dev - U_a)^2", "mm^2/m^2", "Screening ratio OD·Y/(L − U)² with Y = ΔL");

        if (hasK1)
        {
            c.Input("K_scr");
            c.Assign("u_scr", "scr / K_scr", null, "Screening ratio over the K1 record");
        }
        else
        {
            c.Warn("screening-k1", "K_scr: no K1 was entered, so the screening ratio is shown and not judged; enter K1 from your piping code (it has no default), or treat the line as needing a formal stress analysis.");
        }

        // Bellows.
        var thrust = false;

        if (c.Has("P_b") || c.Has("A_eff"))
        {
            if (c.Has("P_b") && c.Has("A_eff"))
            {
                c.Input("P_b");
                c.Input("A_eff");
                c.Assign("F_thrust", "P_b * A_eff", "kN", "Bellows pressure thrust on the anchors");
                thrust = true;
            }
            else
            {
                c.Warn("bellows-incomplete", "A_eff: a bellows thrust needs both the pressure P_b and the effective area A_eff; enter both or neither.");
            }
        }

        // Checks.
        var uLeg = c.Si("u_leg");
        c.CheckUtilisation($"Available leg against the required leg ({F(c.Si("L_req"))} m)", "u_leg");

        if (hasK1)
            c.CheckUtilisation("Screening only: OD·Y/(L − U)² ≤ K1 (not met: the line needs a formal stress analysis)", "u_scr", ScreeningClause);

        var screenText = !hasK1 ? "not evaluated: K1 not entered (treat as: needs formal stress analysis)" : c.Si("u_scr") <= 1 + 1e-9 ? "PASS" : "needs formal stress analysis";
        var passed = c.Checks.All(k => k.Passed);
        c.Record("pass", "every check passes", $"{c.Checks.Count} check(s)", new ScalarValue(passed ? 1 : 0), "1 when the leg and the screening checks pass, else 0");

        c.Assume($"Layout: {(offset ? "the offset leg of " + F(leg) + " m takes the whole movement of " + F(c.Si("dL") * 1000) + " mm, as a guided cantilever fixed against rotation at both ends." : "the U-loop is two legs of " + F(leg) + " m in series, each a guided cantilever taking half the movement (" + F(c.Si("Delta") * 1000) + " mm); the cross leg is a rigid guide and its width does not enter.")} Screening only — not a piping stress analysis.", "Guided-cantilever model.");

        if (uLeg > 1 + 1e-9)
            c.Assume($"The available leg ({F(leg)} m) is shorter than the {F(c.Si("L_req"))} m required: the bending stress at its root is {F(c.Si("sigma_leg") / 1e6)} MPa against Sa = {F(c.Si("Sa") / 1e6)} MPa. Lengthen the leg or loop, add a loop or bellows, or analyse the line formally.", "Result of the check.");

        foreach (var (symbol, text) in new[] { ("D_i", "Inside diameter"), ("A_m", "Metal cross-section"), ("I_x", "Second moment of area"), ("Z_x", "Section modulus"), ("dT", "Temperature change"), ("dL", "Thermal growth between the anchors"), ("sigma_res", "Fully restrained stress"), ("F_res", "Fully restrained force"), ("u_res", "Fully restrained stress over Sa"), ("Delta", "Movement absorbed by each leg"), ("L_req", "Required leg length"), ("u_leg", "Required leg over available leg"), ("anchor_force", "Force on the anchors"), ("anchor_moment", "Moment at the leg root"), ("sigma_leg", "Bending stress at the leg root"), ("u_sigma", "Bending stress over Sa"), ("L_dev", "Developed length"), ("U_a", "Anchor distance"), ("scr", "Screening ratio OD·Y/(L − U)²") })
            c.Output(symbol, text);

        if (hasK1)
            c.Output("u_scr", "Screening ratio over K1");

        c.Output("screen", "Screening result", new TextValue(screenText));

        if (thrust)
            c.Output("F_thrust", "Bellows pressure thrust");

        c.Output("pass", "All checks pass");
    }

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
