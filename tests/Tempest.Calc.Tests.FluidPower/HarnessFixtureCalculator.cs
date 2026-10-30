using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.FluidPower;

/// <summary>
/// A test-only Fluid Power calculator (Pascal's law, F = p·A) with the full
/// worksheet shape. It is not shipped — <c>FluidPowerCalculators.All</c> is
/// empty until F1–F3 land — but it lets the common checks, the golden-sheet
/// path and the discipline plumbing (template hash, sheet header, catalogue
/// ordering) run end to end today.
/// </summary>
public sealed class HarnessFixtureCalculator : ICalcCalculator
{
    public const string Id = "test.fluid-power-pascal";

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.FluidPower, "Cylinder force from pressure (harness fixture)",
        "The force a hydraulic cylinder develops from a gauge pressure acting on a piston area, losses ignored.",
        "Force: F = p·A (Pascal's law, gauge pressure on the full bore area; no seal friction, no back pressure).",
        [
            new CalcInputSpec("p", "Gauge pressure", CalcInputKind.Quantity, "bar", Default: 100, Min: 1, Max: 400, HardMin: 0, HardMax: 1e4),
            new CalcInputSpec("A", "Piston area", CalcInputKind.Quantity, "mm^2", Default: 1000, Min: 1, Max: 1e6, HardMin: 0, HardMax: 1e9),
        ],
        [new CalcAssumption("No seal friction, back pressure or acceleration: the static force.", "Pascal's law on a confined incompressible fluid.")],
        [new CalcClauseRef("Pascal's law", "—", "Pressure acts equally on a confined fluid")],
        [],
        [
            new CalcTestCase("ka-100-bar", "100 bar on 1000 mm²",
                new CalcReferenceSource("Analytic", null, "F = 10 MPa × 1000 mm² = 10 kN"),
                [CalcInputValue.Quantity("p", 100, "bar"), CalcInputValue.Quantity("A", 1000, "mm^2")],
                [new CalcExpectedValue("F", 10, "kN", 1e-9)]),
            new CalcTestCase("ka-250-bar", "250 bar on 2000 mm²",
                new CalcReferenceSource("Analytic", null, "F = 25 MPa × 2000 mm² = 50 kN"),
                [CalcInputValue.Quantity("p", 250, "bar"), CalcInputValue.Quantity("A", 2000, "mm^2")],
                [new CalcExpectedValue("F", 50, "kN", 1e-9)]),
        ],
        Category: "Cylinders");

    public void Run(CalcRunContext c)
    {
        c.Input("p");
        c.Input("A");
        c.Assign("F", "p * A", "kN", "Cylinder force");
        c.Output("F", "Cylinder force");
    }
}
