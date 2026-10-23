using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

public static class StructuralWorkedExamples
{
    public static IReadOnlyList<WorkedExample> All { get; } =
    [
        new(SectionPropertiesCalculator.Id, "ub-457x191x98", "UB 457×191×98 section properties (Blue Book)",
            new CalcReferenceSource("SCI / Steel for Life Blue Book (P363 data)", "EC3 UK NA", "UB 457×191×98 dimensions and properties"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapeI, CalcProvenance.Specification("BS EN 10365 UB 457×191×98")),
                CalcInputValue.Quantity("h", 467.2, "mm", CalcProvenance.Specification("BS EN 10365 UB 457×191×98")),
                CalcInputValue.Quantity("b", 192.8, "mm", CalcProvenance.Specification("BS EN 10365 UB 457×191×98")),
                CalcInputValue.Quantity("t", 11.4, "mm", CalcProvenance.Specification("BS EN 10365 UB 457×191×98")),
                CalcInputValue.Quantity("t_f", 19.6, "mm", CalcProvenance.Specification("BS EN 10365 UB 457×191×98")),
                CalcInputValue.Quantity("r", 10.2, "mm", CalcProvenance.Specification("BS EN 10365 UB 457×191×98")),
            ]),
            [new CalcExpectedValue("A", 125, "cm^2", 0.005), new CalcExpectedValue("I_xx", 45730, "cm^4", 0.005), new CalcExpectedValue("W_pl_x", 2232, "cm^3", 0.005), new CalcExpectedValue("J", 121, "cm^4", 0.01), new CalcExpectedValue("I_w", 1.18, "dm^6", 0.01)],
            "A rolled universal beam with its root fillets. Every tabulated property is reproduced within 0.5 % (torsion and warping within 1 %) from the dimensions alone, so the sheet can stand in for the table when a section is not listed."),

        new(SectionPropertiesCalculator.Id, "fabricated-box-girder", "Fabricated box girder with a hole, as a polygon",
            new CalcReferenceSource("Analytic (outer 400 × 600 rectangle minus 360 × 560 opening)", null, "A = 384 cm², Ixx = (400·600³ − 360·560³)/12 = 193,152 cm⁴, Iyy = 102,272 cm⁴, Wel,x = 6,438.4 cm³, Wpl,x = (400·600² − 360·560²)/4 = 7,776 cm³"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapePolygon, CalcProvenance.Drawing("DRG-BG-100", "C")),
                SectionPropertiesCalculator.Polygon(("mm", [(-200, -300), (200, -300), (200, 300), (-200, 300)]), ("mm", [(-180, -280), (180, -280), (180, 280), (-180, 280)])) with { Provenance = CalcProvenance.Drawing("DRG-BG-100", "C") },
            ]),
            [new CalcExpectedValue("A", 384, "cm^2", 1e-9), new CalcExpectedValue("I_xx", 193152, "cm^4", 1e-9), new CalcExpectedValue("I_yy", 102272, "cm^4", 1e-9), new CalcExpectedValue("W_pl_x", 7776, "cm^3", 1e-9), new CalcExpectedValue("W_el_x", 6438.4, "cm^3", 1e-9)],
            "A welded box drawn as an outer loop and a hole loop. The polygon route gives the same answers as the closed form for a rectangle minus a rectangle; no torsion constant is reported for an arbitrary polygon (warning), so Bredt's formula on the hollow-rectangle shape is the route for J."),
    ];
}
