using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// Pinned lifting lug between two cheek plates: net-section tension, pin
/// bearing, tear-out, pin double shear and pin bending, each against its
/// allowable, the largest governing. Ported from the legacy
/// <c>calc.lifting-lug-pin-joint</c> module (ADR-0167); its two hand-worked
/// examples run as known-answer cases. A lifting design calculation like S8
/// (<c>struct.lifting</c>), it is gated the same way (ADR-0173): the template
/// names the <see cref="CalcFeatures.Lifting"/> feature, so the catalogue
/// withholds it until the installation's administrator switches the feature
/// on, and its issue needs a competent-person attestation and a second
/// independent check.
/// </summary>
public sealed class LiftingLugCalculator : ICalcCalculator
{
    public const string Id = "mech.lifting-lug";

    /// <summary>Printed on the sheet (ADR-0173): this is a design calculation, not a thorough examination.</summary>
    public const string DesignNotice = "This is a design calculation, not a thorough examination.";

    /// <summary>Printed on the sheet (ADR-0173): the two checks and the attestation are made by whoever is signed in; TempestOS does not check who they are.</summary>
    public const string SignatureNotice = "Signatures are not authenticated by TempestOS: the signed-in names of the author, the checkers and the competent person are as entered into this installation.";

    private const string GroupAttest = "Competent-person attestation (required to check and issue)";

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.lifting-lug-pin-joint (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.lifting-lug-pin-joint.md, Example {n}");

    private static CalcInputValue[] Example(double p, double t, double w, double dh, double dp, double a, double ts, double g) =>
    [
        CalcInputValue.Quantity("F_lug", p, "kN"), CalcInputValue.Quantity("t_lug", t, "mm"), CalcInputValue.Quantity("W", w, "mm"),
        CalcInputValue.Quantity("d_h", dh, "mm"), CalcInputValue.Quantity("d_p", dp, "mm"), CalcInputValue.Quantity("a", a, "mm"),
        CalcInputValue.Quantity("t_s", ts, "mm"), CalcInputValue.Quantity("g", g, "mm"),
        CalcInputValue.Quantity("f_t", 150, "MPa"), CalcInputValue.Quantity("f_br", 200, "MPa"), CalcInputValue.Quantity("f_v", 90, "MPa"),
        CalcInputValue.Quantity("f_b_pin", 250, "MPa"), CalcInputValue.Quantity("f_v_pin", 150, "MPa"),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.2.0", CalcDiscipline.Mechanical, "Lifting lug and pin joint",
        "Calculates the net-section tension, bearing, tear-out, pin shear and pin bending of a pinned lifting lug between two cheek plates under a load along the lug. It is a design calculation, not a thorough examination.",
        "Net-section tension, beside the hole: σ_t = F_lug/((W − d_h)·t_lug)\n" +
        "Bearing of the pin on the lug: σ_br = F_lug/(d_p·t_lug)\n" +
        "Tear-out shear beyond the hole, on two planes: τ_to = F_lug/(2·a·t_lug)\n" +
        "Pin shear, in double shear: τ_p = F_lug/(2·π·d_p²/4)\n" +
        "Pin bending moment, from the lever arm across the lug, the gap and the cheek plate: M = (F_lug/2)·(t_lug/4 + g + t_s/2)\n" +
        "Pin bending stress: σ_b = 32·M/(π·d_p³)\n" +
        "Utilisation of each mode, its stress over the allowable entered: u = σ/σ_allow",
        [
            new CalcInputSpec("F_lug", "Load on the lug (along the lug)", CalcInputKind.Quantity, "kN", Default: 50, Min: 0.01, Max: 1e5, HardMin: 1e-9, HardMax: 1e8, Group: "Loads", Aliases: ["P"]),
            new CalcInputSpec("t_lug", "Lug thickness", CalcInputKind.Quantity, "mm", Default: 20, Min: 1, Max: 500, HardMin: 1e-6, HardMax: 1e5, Group: "Lug", Aliases: ["t"]),
            new CalcInputSpec("W", "Lug width at the hole", CalcInputKind.Quantity, "mm", Default: 100, Min: 5, Max: 2000, HardMin: 1e-6, HardMax: 1e5, Group: "Lug"),
            new CalcInputSpec("d_h", "Hole diameter", CalcInputKind.Quantity, "mm", Default: 32, Min: 2, Max: 1000, HardMin: 1e-6, HardMax: 1e5, Group: "Lug"),
            new CalcInputSpec("a", "Edge distance, hole edge to lug end (load direction)", CalcInputKind.Quantity, "mm", Default: 40, Min: 1, Max: 2000, HardMin: 1e-6, HardMax: 1e5, Group: "Lug"),
            new CalcInputSpec("d_p", "Pin diameter", CalcInputKind.Quantity, "mm", Default: 30, Min: 2, Max: 1000, HardMin: 1e-6, HardMax: 1e5, Group: "Pin"),
            new CalcInputSpec("t_s", "Cheek plate thickness (each)", CalcInputKind.Quantity, "mm", Default: 12, Min: 1, Max: 500, HardMin: 1e-6, HardMax: 1e5, Group: "Pin"),
            new CalcInputSpec("g", "Clearance, lug face to cheek plate (each side)", CalcInputKind.Quantity, "mm", Default: 2, Min: 0, Max: 100, HardMin: 0, HardMax: 1e4, Group: "Pin"),
            new CalcInputSpec("f_t", "Allowable tensile stress, lug", CalcInputKind.Quantity, "MPa", Default: 150, Min: 1, Max: 2000, HardMin: 1e-6, HardMax: 1e5, Group: "Allowables"),
            new CalcInputSpec("f_br", "Allowable bearing stress, lug", CalcInputKind.Quantity, "MPa", Default: 200, Min: 1, Max: 3000, HardMin: 1e-6, HardMax: 1e5, Group: "Allowables"),
            new CalcInputSpec("f_v", "Allowable shear stress, lug", CalcInputKind.Quantity, "MPa", Default: 90, Min: 1, Max: 2000, HardMin: 1e-6, HardMax: 1e5, Group: "Allowables"),
            new CalcInputSpec("f_b_pin", "Allowable bending stress, pin", CalcInputKind.Quantity, "MPa", Default: 250, Min: 1, Max: 3000, HardMin: 1e-6, HardMax: 1e5, Group: "Allowables"),
            new CalcInputSpec("f_v_pin", "Allowable shear stress, pin", CalcInputKind.Quantity, "MPa", Default: 150, Min: 1, Max: 2000, HardMin: 1e-6, HardMax: 1e5, Group: "Allowables"),
            new CalcInputSpec(CalcAttestation.NameSymbol, "Competent person attesting the design (not the author; required to check and issue)", CalcInputKind.Text, IsOptional: true, Group: GroupAttest),
            new CalcInputSpec(CalcAttestation.QualificationSymbol, "Their qualification and standing", CalcInputKind.Text, IsOptional: true, Group: GroupAttest),
            new CalcInputSpec(CalcAttestation.StatementSymbol, "Attestation: what the competent person has reviewed (at least 20 characters)", CalcInputKind.Text, IsOptional: true, Group: GroupAttest),
        ],
        [
            new CalcAssumption("The load acts along the lug's axis through the hole centre.", "No out-of-plane or side load."),
            new CalcAssumption("The pin bears uniformly over the lug thickness and over each cheek plate.", "Pin-bending idealisation for a lug between two cheek plates."),
            new CalcAssumption("Each cheek plate reacts half the load.", "Pin-bending idealisation for a lug between two cheek plates."),
            new CalcAssumption("The pin is a close fit, with d_p at least 0.9 d_h.", "Uniform-bearing idealisation."),
            new CalcAssumption(DesignNotice, "The sheet checks the lug and pin at the stated loads against the allowables entered. LOLER reg 9 and PUWER duties stay with the owner and the competent person."),
        ],
        [
            new CalcClauseRef("ASME BTH-1", "Ch. 3", "Pinned connections"),
            new CalcClauseRef("Budynas and Nisbett, Shigley's Mechanical Engineering Design", "Ch. 3, 11th ed.", "Shear, bearing and bending stresses"),
        ],
        [
            new CalcApplicabilityRule("geometry", "The lug must be wider than the hole and the pin must fit the hole.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("loose-pin", "A pin smaller than 0.9 of the hole concentrates bearing; the uniform-bearing method does not apply.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-legacy-pass", "Legacy example 1: a lug that passes (pin bending governs)", Legacy(1),
                Example(50, 20, 100, 32, 30, 40, 12, 2),
                [new CalcExpectedValue("sigma_t", 36.765, "MPa", 5e-5), new CalcExpectedValue("sigma_br", 83.333, "MPa", 5e-5), new CalcExpectedValue("tau_to", 31.250, "MPa", 5e-5), new CalcExpectedValue("tau_p", 35.368, "MPa", 5e-5), new CalcExpectedValue("sigma_b", 122.61, "MPa", 5e-5), new CalcExpectedValue("u_b", 0.49043, "", 5e-5)]),
            new CalcTestCase("ka-legacy-fail", "Legacy example 2: a lug that fails three ways (bearing governs)", Legacy(2),
                Example(120, 15, 90, 26, 25, 30, 10, 1),
                [new CalcExpectedValue("sigma_t", 125.00, "MPa", 5e-5), new CalcExpectedValue("sigma_br", 320.00, "MPa", 5e-5), new CalcExpectedValue("tau_to", 133.33, "MPa", 5e-5), new CalcExpectedValue("tau_p", 122.23, "MPa", 5e-5), new CalcExpectedValue("sigma_b", 381.36, "MPa", 5e-5), new CalcExpectedValue("u_br", 1.6000, "", 5e-5)]),
        ],
        Category: "Connections",
        IndependentCheck: true,
        FeatureFlag: CalcFeatures.Lifting,
        RequiresAttestation: true,
        RequiresSecondCheck: true,
        OutputPorts:
        [
            new CalcOutputPort("u_t", "", "Net-section tension utilisation", "lug.utilisation_tension"),
            new CalcOutputPort("u_br", "", "Bearing utilisation", "lug.utilisation_bearing"),
            new CalcOutputPort("u_to", "", "Tear-out utilisation", "lug.utilisation_tear_out"),
            new CalcOutputPort("u_p", "", "Pin shear utilisation", "lug.utilisation_pin_shear"),
            new CalcOutputPort("u_b", "", "Pin bending utilisation", "lug.utilisation_pin_bending"),
            new CalcOutputPort("M_p", "kN*m", "Pin bending moment", "lug.moment_pin"),
        ]);

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_b", "u_br", "u_p", "u_t", "u_to");
        foreach (var s in new[] { "F_lug", "t_lug", "W", "d_h", "a", "d_p", "t_s", "g", "f_t", "f_br", "f_v", "f_b_pin", "f_v_pin" })
            c.Input(s);

        if (!(c.Si("W") > c.Si("d_h")))
        {
            c.Block("geometry", "W: the lug must be wider than the hole. Otherwise it has no net section.");
            return;
        }

        if (c.Si("d_p") > c.Si("d_h"))
        {
            c.Block("geometry", "d_p: the pin is larger than the hole and does not fit.");
            return;
        }

        c.Assign("k_fit", "d_p / d_h", null, "Pin-to-hole ratio");

        if (c.Si("k_fit") < 0.9)
        {
            c.Block("loose-pin", $"d_p: the pin is {CalcValue.FormatNumber(c.Si("k_fit"), 3)} of the hole, below 0.9. Bearing concentrates, so the uniform-bearing method does not apply.");
            return;
        }

        c.Assign("A_net", "(W - d_h) * t_lug", "mm^2", "Net section through the hole");
        c.Assign("sigma_t", "F_lug / A_net", "MPa", "Net-section tension", "ASME BTH-1 Ch. 3");
        c.Assign("A_br", "d_p * t_lug", "mm^2", "Bearing area");
        c.Assign("sigma_br", "F_lug / A_br", "MPa", "Bearing of the pin on the hole");
        c.Assign("A_to", "2 * a * t_lug", "mm^2", "Tear-out area (two planes beyond the hole)");
        c.Assign("tau_to", "F_lug / A_to", "MPa", "Tear-out shear");
        c.Assign("A_p", "2 * pi * d_p^2 / 4", "mm^2", "Pin area in double shear");
        c.Assign("tau_p", "F_lug / A_p", "MPa", "Pin shear");
        c.Assign("M_p", "F_lug / 2 * (t_lug / 4 + g + t_s / 2)", "kN*m", "Pin bending moment (uniform bearing on lug and cheek plates)");
        c.Assign("Z_p", "pi * d_p^3 / 32", "mm^3", "Pin section modulus");
        c.Assign("sigma_b", "M_p / Z_p", "MPa", "Pin bending stress");

        c.Assign("u_t", "sigma_t / f_t", null, "Net-section utilisation");
        c.Assign("u_br", "sigma_br / f_br", null, "Bearing utilisation");
        c.Assign("u_to", "tau_to / f_v", null, "Tear-out utilisation");
        c.Assign("u_p", "tau_p / f_v_pin", null, "Pin shear utilisation");
        c.Assign("u_b", "sigma_b / f_b_pin", null, "Pin bending utilisation");

        foreach (var (symbol, text) in new[] { ("sigma_t", "Net-section tension"), ("sigma_br", "Bearing stress"), ("tau_to", "Tear-out shear"), ("tau_p", "Pin shear"), ("M_p", "Pin bending moment"), ("sigma_b", "Pin bending stress"), ("u_t", "Net-section utilisation"), ("u_br", "Bearing utilisation"), ("u_to", "Tear-out utilisation"), ("u_p", "Pin shear utilisation"), ("u_b", "Pin bending utilisation") })
            c.Output(symbol, text);

        c.Output("sheet_notice", "Status of this sheet", new TextValue(DesignNotice + " It does not replace the thorough examination by a competent person (LOLER reg 9) or the marking, testing and certification that the regulations require. " + SignatureNotice));

        c.Check("Net-section tension against the allowable tensile stress", "sigma_t", "f_t", "ASME BTH-1 Ch. 3", resultSymbol: "u_t");
        c.Check("Pin bearing against the allowable bearing stress", "sigma_br", "f_br", "ASME BTH-1 Ch. 3", resultSymbol: "u_br");
        c.Check("Tear-out shear against the allowable shear stress", "tau_to", "f_v", "ASME BTH-1 Ch. 3", resultSymbol: "u_to");
        c.Check("Pin double shear against the allowable pin shear stress", "tau_p", "f_v_pin", resultSymbol: "u_p");
        c.Check("Pin bending stress against the allowable pin bending stress", "sigma_b", "f_b_pin", resultSymbol: "u_b");
    }
}
