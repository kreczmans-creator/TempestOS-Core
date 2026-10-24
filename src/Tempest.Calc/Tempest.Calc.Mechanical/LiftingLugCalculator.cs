using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// Pinned lifting lug between two cheek plates: net-section tension, pin
/// bearing, tear-out, pin double shear and pin bending, each against its
/// allowable, the largest governing. Ported from the legacy
/// <c>calc.lifting-lug-pin-joint</c> module (ADR-0167); its two hand-worked
/// examples run as known-answer cases.
/// </summary>
public sealed class LiftingLugCalculator : ICalcCalculator
{
    public const string Id = "mech.lifting-lug";

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.lifting-lug-pin-joint (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.lifting-lug-pin-joint.md, Example {n}");

    private static CalcInputValue[] Example(double p, double t, double w, double dh, double dp, double a, double ts, double g) =>
    [
        CalcInputValue.Quantity("P", p, "kN"), CalcInputValue.Quantity("t", t, "mm"), CalcInputValue.Quantity("W", w, "mm"),
        CalcInputValue.Quantity("d_h", dh, "mm"), CalcInputValue.Quantity("d_p", dp, "mm"), CalcInputValue.Quantity("a", a, "mm"),
        CalcInputValue.Quantity("t_s", ts, "mm"), CalcInputValue.Quantity("g", g, "mm"),
        CalcInputValue.Quantity("f_t", 150, "MPa"), CalcInputValue.Quantity("f_br", 200, "MPa"), CalcInputValue.Quantity("f_v", 90, "MPa"),
        CalcInputValue.Quantity("f_b_pin", 250, "MPa"), CalcInputValue.Quantity("f_v_pin", 150, "MPa"),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.0.1", CalcDiscipline.Mechanical, "Lifting lug and pin joint",
        "A pinned lug between two cheek plates under a load along the lug: tension on the net section through the hole, bearing of the pin on the hole, shear tear-out beyond the hole, and the pin in double shear and in bending, each against an allowable stress; the largest utilisation governs.",
        "Tension across the net section beside the hole: σ_t = P/((W − d_h)·t).\nBearing of the pin on the lug: σ_br = P/(d_p·t).\nTear-out shear beyond the hole, on two planes: τ_to = P/(2·a·t).\nPin shear, in double shear: τ_p = P/(2·π·d_p²/4).\nPin bending, from the lever arm across the lug, the gap and the shackle: M = (P/2)·(t/4 + g + t_s/2); σ_b = 32·M/(π·d_p³).\nUtilisation of each mode, its stress over the allowable from the material and design category in force (the sheet adds no design factor of its own): u = σ/σ_allow.",
        [
            new CalcInputSpec("P", "Load on the lug (along the lug)", CalcInputKind.Quantity, "kN", Default: 50, Min: 0.01, Max: 1e5, HardMin: 1e-9, HardMax: 1e8, Group: "Load"),
            new CalcInputSpec("t", "Lug thickness", CalcInputKind.Quantity, "mm", Default: 20, Min: 1, Max: 500, HardMin: 1e-6, HardMax: 1e5, Group: "Lug"),
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
        ],
        [
            new CalcAssumption("The load acts along the lug's axis through the hole centre.", "No out-of-plane or side load."),
            new CalcAssumption("The pin bears uniformly over the lug thickness and over each cheek plate; each cheek plate reacts half the load.", "Pin-bending idealisation for a lug between two cheek plates."),
            new CalcAssumption("A close-fitting pin (d_p ≥ 0.9·d_h).", "Uniform-bearing idealisation."),
        ],
        [
            new CalcClauseRef("ASME BTH-1", "Ch. 3", "Pinned connections"),
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "Ch. 3", "Shear, bearing and bending stresses"),
        ],
        [
            new CalcApplicabilityRule("geometry", "The lug must be wider than the hole and the pin must fit the hole.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("loose-pin", "A pin smaller than 0.9 of the hole concentrates bearing; the uniform-bearing method does not apply.", CalcNoticeSeverity.Block),
        ],
        [
            new CalcTestCase("ka-legacy-pass", "Legacy example 1: a lug that passes (pin bending governs)", Legacy(1),
                Example(50, 20, 100, 32, 30, 40, 12, 2),
                [new CalcExpectedValue("sigma_t", 36.765, "MPa", 5e-5), new CalcExpectedValue("sigma_br", 83.333, "MPa", 5e-5), new CalcExpectedValue("tau_to", 31.250, "MPa", 5e-5), new CalcExpectedValue("tau_p", 35.368, "MPa", 5e-5), new CalcExpectedValue("sigma_b", 122.61, "MPa", 5e-5), new CalcExpectedValue("u_b", 0.49043, "", 5e-5)]),
            new CalcTestCase("ka-legacy-fail", "Legacy example 2: a lug that fails three ways (bearing governs)", Legacy(2),
                Example(120, 15, 90, 26, 25, 30, 10, 1),
                [new CalcExpectedValue("sigma_t", 125.00, "MPa", 5e-5), new CalcExpectedValue("sigma_br", 320.00, "MPa", 5e-5), new CalcExpectedValue("tau_to", 133.33, "MPa", 5e-5), new CalcExpectedValue("tau_p", 122.23, "MPa", 5e-5), new CalcExpectedValue("sigma_b", 381.36, "MPa", 5e-5), new CalcExpectedValue("u_br", 1.6000, "", 5e-5)]),
        ],
        Category: "Connections");

    public void Run(CalcRunContext c)
    {
        foreach (var s in new[] { "P", "t", "W", "d_h", "a", "d_p", "t_s", "g", "f_t", "f_br", "f_v", "f_b_pin", "f_v_pin" })
            c.Input(s);

        if (!(c.Si("W") > c.Si("d_h")))
        {
            c.Block("geometry", "W: the lug must be wider than the hole; there is no net section.");
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
            c.Block("loose-pin", $"d_p: the pin is {CalcValue.FormatNumber(c.Si("k_fit"), 3)} of the hole, below 0.9; bearing concentrates and the uniform-bearing method does not apply.");
            return;
        }

        c.Assign("A_net", "(W - d_h) * t", "mm^2", "Net section through the hole");
        c.Assign("sigma_t", "P / A_net", "MPa", "Net-section tension", "ASME BTH-1 Ch. 3");
        c.Assign("A_br", "d_p * t", "mm^2", "Bearing area");
        c.Assign("sigma_br", "P / A_br", "MPa", "Bearing of the pin on the hole");
        c.Assign("A_to", "2 * a * t", "mm^2", "Tear-out area (two planes beyond the hole)");
        c.Assign("tau_to", "P / A_to", "MPa", "Tear-out shear");
        c.Assign("A_p", "2 * pi * d_p^2 / 4", "mm^2", "Pin area in double shear");
        c.Assign("tau_p", "P / A_p", "MPa", "Pin shear");
        c.Assign("M_p", "P / 2 * (t / 4 + g + t_s / 2)", "N*m", "Pin bending moment (uniform bearing on lug and cheek plates)");
        c.Assign("Z_p", "pi * d_p^3 / 32", "mm^3", "Pin section modulus");
        c.Assign("sigma_b", "M_p / Z_p", "MPa", "Pin bending stress");

        c.Assign("u_t", "sigma_t / f_t", null, "Net-section utilisation");
        c.Assign("u_br", "sigma_br / f_br", null, "Bearing utilisation");
        c.Assign("u_to", "tau_to / f_v", null, "Tear-out utilisation");
        c.Assign("u_p", "tau_p / f_v_pin", null, "Pin shear utilisation");
        c.Assign("u_b", "sigma_b / f_b_pin", null, "Pin bending utilisation");

        foreach (var (symbol, text) in new[] { ("sigma_t", "Net-section tension"), ("sigma_br", "Bearing stress"), ("tau_to", "Tear-out shear"), ("tau_p", "Pin shear"), ("M_p", "Pin bending moment"), ("sigma_b", "Pin bending stress"), ("u_t", "Net-section utilisation"), ("u_br", "Bearing utilisation"), ("u_to", "Tear-out utilisation"), ("u_p", "Pin shear utilisation"), ("u_b", "Pin bending utilisation") })
            c.Output(symbol, text);

        c.CheckUtilisation("Net-section tension", "u_t", "ASME BTH-1 Ch. 3");
        c.CheckUtilisation("Bearing", "u_br", "ASME BTH-1 Ch. 3");
        c.CheckUtilisation("Tear-out", "u_to", "ASME BTH-1 Ch. 3");
        c.CheckUtilisation("Pin double shear", "u_p");
        c.CheckUtilisation("Pin bending", "u_b");
    }
}
