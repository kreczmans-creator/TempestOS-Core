using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// Static strength margins of a material against an applied stress: the
/// factors on yield and on ultimate strength, against a required factor,
/// with the strengths fillable from a released material record. Ported from
/// the legacy <c>calc.material-selection-margin</c> (ADR-0167).
/// </summary>
public sealed class MaterialMarginCalculator : ICalcCalculator
{
    public const string Id = "mech.material-margin";

    public CalcTemplate Template { get; } = new(
        Id, "1.0.1", CalcDiscipline.Mechanical, "Material strength margin",
        "The factors of safety of a material against an applied (equivalent) stress on its yield and, when given, its ultimate strength, against a required factor; the strengths can be filled from a released material record so the margin cites its source.",
        "Factor of safety against yield and against ultimate: n_y = f_y/σ; n_u = f_u/σ.\nGoverning factor, the ultimate margin scaled to the yield basis by k_u ≥ 1: n_gov = min(n_y, n_u/k_u).\nUtilisation against the required factor: u = n_req/n_gov.",
        [
            new CalcInputSpec("sigma", "Applied (equivalent) stress", CalcInputKind.Quantity, "MPa", Default: 120, Min: 0.01, Max: 5000, HardMin: 1e-6, HardMax: 1e5),
            new CalcInputSpec("f_y", "Yield strength", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials", ReferenceProperty: "YieldStrength", ReferenceDimension: "Pressure", Default: 300, Min: 1, Max: 5000, HardMin: 1e-6, HardMax: 1e5),
            new CalcInputSpec("f_u", "Ultimate tensile strength (optional)", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials", ReferenceProperty: "UltimateTensileStrength", ReferenceDimension: "Pressure", Min: 1, Max: 5000, HardMin: 1e-6, HardMax: 1e5, IsOptional: true),
            new CalcInputSpec("k_u", "Ratio of the ultimate to the yield safety factor required", CalcInputKind.Number, Default: 1.5, Min: 1, Max: 3, HardMin: 1, HardMax: 10),
            new CalcInputSpec("n_req", "Required factor of safety on yield", CalcInputKind.Number, Default: 1.5, Min: 1, Max: 10, HardMin: 1, HardMax: 100),
        ],
        [
            new CalcAssumption("Static loading; the applied stress is the equivalent stress at the critical point.", "Fatigue and stability are separate checks."),
        ],
        [
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "§1-10", "Design factor and factor of safety"),
        ],
        [],
        [
            new CalcTestCase("ka-margin-hand", "Hand calculation: σ = 120 MPa on f_y 300, f_u 450, k_u 1.5, n_req 1.5",
                new CalcReferenceSource("Hand calculation", null, "n_y = 300/120 = 2.5; n_u = 450/120 = 3.75; n_u/k_u = 2.5; n_gov = 2.5; u = 1.5/2.5 = 0.6"),
                [CalcInputValue.Quantity("sigma", 120, "MPa"), CalcInputValue.Quantity("f_y", 300, "MPa"), CalcInputValue.Quantity("f_u", 450, "MPa"), CalcInputValue.Of("k_u", 1.5), CalcInputValue.Of("n_req", 1.5)],
                [new CalcExpectedValue("n_y", 2.5, "", 1e-12), new CalcExpectedValue("n_u", 3.75, "", 1e-12), new CalcExpectedValue("n_gov", 2.5, "", 1e-12), new CalcExpectedValue("u", 0.6, "", 1e-12)]),
            new CalcTestCase("ka-margin-yield-only", "Hand calculation: S355 at 250 MPa, yield only, n_req 1.5 (fails)",
                new CalcReferenceSource("Hand calculation", null, "n_y = 355/250 = 1.42; u = 1.5/1.42 = 1.0563"),
                [CalcInputValue.Quantity("sigma", 250, "MPa"), CalcInputValue.Quantity("f_y", 355, "MPa"), CalcInputValue.Of("k_u", 1.5), CalcInputValue.Of("n_req", 1.5)],
                [new CalcExpectedValue("n_y", 1.42, "", 1e-12), new CalcExpectedValue("n_gov", 1.42, "", 1e-12), new CalcExpectedValue("u", 1.0563, "", 5e-5)]),
        ],
        Category: "Materials");

    public void Run(CalcRunContext c)
    {
        foreach (var s in new[] { "sigma", "f_y", "k_u", "n_req" })
            c.Input(s);

        c.Assign("n_y", "f_y / sigma", null, "Factor of safety on yield", "Shigley §1-10");

        if (c.Has("f_u"))
        {
            c.Input("f_u");
            c.Assign("n_u", "f_u / sigma", null, "Factor of safety on ultimate strength");
            c.Assign("n_gov", "min(n_y, n_u / k_u)", null, "Governing factor (ultimate margin scaled to the yield basis)");
            c.Output("n_u", "Factor on ultimate");
        }
        else
        {
            c.Assign("n_gov", "n_y", null, "Governing factor (yield only)");
        }

        c.Assign("u", "n_req / n_gov", null, "Required over governing factor of safety");
        c.Output("n_y", "Factor on yield");
        c.Output("n_gov", "Governing factor");
        c.Output("u", "Utilisation");
        c.CheckUtilisation("Factor of safety against the required factor", "u", "Shigley §1-10");
    }
}
