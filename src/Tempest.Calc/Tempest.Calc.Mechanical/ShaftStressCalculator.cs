using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// Solid round shaft under combined bending and torsion: bending and torsional
/// stresses with concentration factors, maximum shear (Tresca) and von Mises
/// stresses, and the factors of safety against yield. Ported from the legacy
/// <c>calc.shaft-combined-stress</c> module (ADR-0167); its three hand-worked
/// examples run as known-answer cases.
/// </summary>
public sealed class ShaftStressCalculator : ICalcCalculator
{
    public const string Id = "mech.shaft-stress";

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.shaft-combined-stress (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.shaft-combined-stress.md, Example {n}");

    private static CalcInputValue[] Example(double d, double m, double t, double sy, double kt, double kts, double nreq) =>
    [
        CalcInputValue.Quantity("d", d, "mm"), CalcInputValue.Quantity("M", m, "N*m"), CalcInputValue.Quantity("T", t, "N*m"),
        CalcInputValue.Quantity("S_y", sy, "MPa"), CalcInputValue.Of("K_t", kt), CalcInputValue.Of("K_ts", kts), CalcInputValue.Of("n_req", nreq),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.0.1", CalcDiscipline.Mechanical, "Shaft under combined bending and torsion",
        "Bending and torsional stresses in a solid round shaft with stress-concentration factors, the maximum shear (Tresca) and von Mises equivalent stresses, and the factors of safety against yield, against a required factor.",
        "Bending stress, with its stress concentration: σ = K_t·32M/(πd³).\nTorsional shear stress, with its stress concentration: τ = K_ts·16T/(πd³).\nMaximum shear stress (Tresca): τ_max = √((σ/2)² + τ²).\nvon Mises equivalent stress: σ' = √(σ² + 3τ²).\nFactors of safety against yield, the lower governing: n_T = (S_y/2)/τ_max; n_VM = S_y/σ'.",
        [
            new CalcInputSpec("d", "Shaft diameter", CalcInputKind.Quantity, "mm", Default: 40, Min: 1, Max: 2000, HardMin: 1e-6, HardMax: 1e5),
            new CalcInputSpec("M", "Bending moment (magnitude)", CalcInputKind.Quantity, "N*m", Default: 500, Min: 0, Max: 1e7, HardMin: 0, HardMax: 1e10),
            new CalcInputSpec("T", "Torque (magnitude)", CalcInputKind.Quantity, "N*m", Default: 800, Min: 0, Max: 1e7, HardMin: 0, HardMax: 1e10),
            new CalcInputSpec("S_y", "Yield strength", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials", ReferenceProperty: "YieldStrength", ReferenceDimension: "Pressure", Default: 350, Min: 50, Max: 3000, HardMin: 1e-3, HardMax: 1e5),
            new CalcInputSpec("K_t", "Bending stress-concentration factor (1 for none)", CalcInputKind.Number, Default: 1, Min: 1, Max: 5, HardMin: 1, HardMax: 20),
            new CalcInputSpec("K_ts", "Torsional stress-concentration factor (1 for none)", CalcInputKind.Number, Default: 1, Min: 1, Max: 5, HardMin: 1, HardMax: 20),
            new CalcInputSpec("n_req", "Required factor of safety", CalcInputKind.Number, Default: 2, Min: 1, Max: 10, HardMin: 1, HardMax: 100),
        ],
        [
            new CalcAssumption("Solid round section, linear elastic; static loading.", "Fatigue is mech.fatigue-miner's job; hollow shafts are not covered."),
        ],
        [
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "§5-4, §5-5", "Maximum-shear-stress and distortion-energy theories"),
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "§7-4", "Shaft design for stress"),
        ],
        [
            new CalcApplicabilityRule("no-load", "Bending moment and torque cannot both be zero.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-legacy-1", "Legacy example 1", Legacy(1), Example(40, 500, 800, 350, 1, 1, 2),
                [new CalcExpectedValue("sigma", 79.577, "MPa", 5e-5), new CalcExpectedValue("tau", 63.662, "MPa", 5e-5), new CalcExpectedValue("tau_max", 75.073, "MPa", 5e-5), new CalcExpectedValue("sigma_vm", 135.98, "MPa", 5e-5), new CalcExpectedValue("n_T", 2.3311, "", 5e-5), new CalcExpectedValue("n_VM", 2.5739, "", 5e-5)]),
            new CalcTestCase("ka-legacy-2", "Legacy example 2 (fails the required factor)", Legacy(2), Example(25, 120, 60, 250, 1, 1, 3),
                [new CalcExpectedValue("sigma", 78.228, "MPa", 5e-5), new CalcExpectedValue("tau", 19.557, "MPa", 5e-5), new CalcExpectedValue("tau_max", 43.731, "MPa", 5e-5), new CalcExpectedValue("sigma_vm", 85.247, "MPa", 5e-5), new CalcExpectedValue("n_T", 2.8584, "", 5e-5), new CalcExpectedValue("n_VM", 2.9327, "", 5e-5)]),
            new CalcTestCase("ka-legacy-3", "Legacy example 3: example 2 with a shoulder fillet", Legacy(3), Example(25, 120, 60, 250, 1.6, 1.3, 1.5),
                [new CalcExpectedValue("sigma", 125.16, "MPa", 5e-5), new CalcExpectedValue("tau", 25.424, "MPa", 5e-5), new CalcExpectedValue("tau_max", 67.549, "MPa", 5e-5), new CalcExpectedValue("sigma_vm", 132.69, "MPa", 5e-5), new CalcExpectedValue("n_T", 1.8505, "", 5e-5), new CalcExpectedValue("n_VM", 1.8842, "", 5e-5)]),
        ],
        Category: "Shafts");

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u");
        c.MarkAtLeast("n_T", "n_VM", "n_min");
        foreach (var s in new[] { "d", "M", "T", "S_y", "K_t", "K_ts", "n_req" })
            c.Input(s);

        if (c.Si("M") == 0 && c.Si("T") == 0)
        {
            c.Block("no-load", "M/T: the bending moment and the torque are both zero; there is nothing to check.");
            return;
        }

        c.Assign("sigma", "K_t * 32 * M / (pi * d^3)", "MPa", "Bending stress", "Shigley §7-4");
        c.Assign("tau", "K_ts * 16 * T / (pi * d^3)", "MPa", "Torsional shear stress", "Shigley §7-4");
        c.Assign("tau_max", "sqrt((sigma / 2)^2 + tau^2)", "MPa", "Maximum shear stress (Tresca)", "Shigley §5-4");
        c.Assign("sigma_vm", "sqrt(sigma^2 + 3 * tau^2)", "MPa", "von Mises stress", "Shigley §5-5");
        c.Assign("n_T", "(S_y / 2) / tau_max", null, "Factor of safety, maximum shear stress");
        c.Assign("n_VM", "S_y / sigma_vm", null, "Factor of safety, distortion energy");
        c.Assign("n_min", "min(n_T, n_VM)", null, "Governing factor of safety");
        c.Assign("u", "n_req / n_min", null, "Required over governing factor of safety");

        foreach (var (symbol, text) in new[] { ("sigma", "Bending stress"), ("tau", "Torsional shear"), ("tau_max", "Maximum shear stress"), ("sigma_vm", "von Mises stress"), ("n_T", "Factor of safety (Tresca)"), ("n_VM", "Factor of safety (von Mises)"), ("n_min", "Governing factor of safety") })
            c.Output(symbol, text);

        c.CheckUtilisation("Factor of safety against the required factor", "u", "Shigley §7-4");
    }
}
