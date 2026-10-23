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

        new(FrameCalculator.Id, "two-span-continuous-beam", "Two-span continuous beam, ULS and SLS combinations",
            new CalcReferenceSource("Roark's Formulas for Stress and Strain", "8th ed.", "Table 8.1 case 2c applied to each span of a symmetric two-span beam under UDL: interior support moment −wL²/8, end reactions 3wL/8, interior reaction 10wL/8"),
            new CalcInputSet(
            [
                CalcInputValue.Quantity("E", 210, "GPa", CalcProvenance.Specification("BS EN 1993-1-1 3.2.6: E = 210 GPa")),
                FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 6, 0, FrameCalculator.SupportRollerY), (3, 12, 0, FrameCalculator.SupportRollerY)) with { Provenance = CalcProvenance.Drawing("DRG-FL-201", "A") },
                FrameCalculator.Elements((1, 1, 2, 51.3, 8503), (2, 2, 3, 51.3, 8503)) with { Provenance = CalcProvenance.FromReference("UB 305×165×40 (Blue Book)") },
                FrameCalculator.Loads((1, "udl", 1, 0, 6, -8, 0, 0, 0, 0), (1, "udl", 2, 0, 6, -8, 0, 0, 0, 0), (2, "udl", 1, 0, 6, -12, 0, 0, 0, 0), (2, "udl", 2, 0, 6, -12, 0, 0, 0, 0)) with { Provenance = CalcProvenance.Specification("Load schedule LS-01: G = 8 kN/m, Q = 12 kN/m") },
                FrameCalculator.Combinations(("ULS", 1, 1.35), ("ULS", 2, 1.5), ("SLS", 1, 1.0), ("SLS", 2, 1.0)) with { Provenance = CalcProvenance.Specification("BS EN 1990 6.4.3.2 eq. 6.10 (γ_G 1.35, γ_Q 1.5); SLS characteristic") },
                CalcInputValue.Choice("report", "ULS", CalcProvenance.Assumed("ULS reported in detail; SLS in the envelope.")),
                CalcInputValue.Of("n_div", 24, CalcProvenance.Assumed("24 stations per span.")),
            ]),
            [new CalcExpectedValue("R2_y", 216, "kN", 1e-6), new CalcExpectedValue("R1_y", 64.8, "kN", 1e-6), new CalcExpectedValue("M_min", -129.6, "kN*m", 1e-6), new CalcExpectedValue("M_max", 72.9, "kN*m", 1e-6), new CalcExpectedValue("d_max_env", 11.322, "mm", 1e-3)],
            "Two equal 6 m spans on UB 305×165×40 under permanent 8 kN/m and imposed 12 kN/m. ULS (1.35G + 1.5Q = 28.8 kN/m): hogging −wL²/8 = −129.6 kN·m over the middle support, sagging 9wL²/128 = 72.9 kN·m, interior reaction 1.25wL = 216 kN; deflection wL⁴ (39 + 55√33)/(65536 EI) = 11.32 mm. The SLS combination sits in the envelope."),

        new(FrameCalculator.Id, "pinned-portal-gravity-wind", "Pinned-base portal frame, gravity and wind",
            new CalcReferenceSource("Hibbeler, Structural Analysis", "9th ed.", "Portal frame with pinned supports: lateral load shared equally by equal columns (exact for an axially stiff beam); gravity case by symmetry"),
            new CalcInputSet(
            [
                CalcInputValue.Quantity("E", 210, "GPa", CalcProvenance.Specification("BS EN 1993-1-1 3.2.6")),
                FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 0, 5, FrameCalculator.SupportFree), (3, 8, 5, FrameCalculator.SupportFree), (4, 8, 0, FrameCalculator.SupportPin)) with { Provenance = CalcProvenance.Drawing("DRG-PF-010", "B") },
                FrameCalculator.Elements((1, 1, 2, 58.7, 4568), (2, 2, 3, 51.3, 8503), (3, 3, 4, 58.7, 4568)) with { Provenance = CalcProvenance.FromReference("UC 203×203×46 columns, UB 305×165×40 rafter (Blue Book)") },
                FrameCalculator.Loads((1, "udl", 2, 0, 8, -15, 0, 0, 0, 0), (2, "nodal", 2, 0, 0, 0, 0, 24, 0, 0)) with { Provenance = CalcProvenance.Specification("Load schedule LS-02: roof 15 kN/m; wind 24 kN at eaves") },
                FrameCalculator.Combinations(("G+W", 1, 1.0), ("G+W", 2, 1.0), ("G", 1, 1.0)) with { Provenance = CalcProvenance.Assumed("Characteristic combinations for the worked example.") },
                CalcInputValue.Choice("report", "G+W", CalcProvenance.Assumed("Gravity plus wind reported.")),
                CalcInputValue.Of("n_div", 16, CalcProvenance.Assumed("16 stations per member.")),
            ]),
            [new CalcExpectedValue("sum_R_y", 120, "kN", 1e-9), new CalcExpectedValue("sum_R_x", -24, "kN", 1e-9)],
            "A 8 m × 5 m pinned portal under 15 kN/m roof load and a 24 kN eaves wind load. Equilibrium: ΣR_y = 120 kN, ΣR_x = −24 kN. The column-top moments combine the gravity frame action with the wind sway; the sheet reports every member's extremes and the envelope over G+W and G."),

        new(BeamFormulasCalculator.Id, "ss-point-offcentre", "Simply supported beam, off-centre point load",
            new CalcReferenceSource("Roark's Formulas for Stress and Strain", "8th ed.", "Table 8.1 case 2a"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("case", BeamFormulasCalculator.SsPoint, CalcProvenance.Assumed("Single span, pin–roller.")),
                CalcInputValue.Quantity("L", 6, "m", CalcProvenance.Drawing("DRG-FL-201", "A")),
                CalcInputValue.Quantity("E", 210, "GPa", CalcProvenance.Specification("BS EN 1993-1-1 3.2.6")),
                CalcInputValue.Quantity("I", 8503, "cm^4", CalcProvenance.FromReference("UB 305×165×40 (Blue Book)")),
                CalcInputValue.Quantity("P", 50, "kN", CalcProvenance.Specification("Load schedule LS-01: plant load")),
                CalcInputValue.Quantity("a", 4, "m", CalcProvenance.Drawing("DRG-FL-201", "A")),
            ]),
            [new CalcExpectedValue("R_A", 16.6667, "kN", 1e-4), new CalcExpectedValue("M_max", 66.6667, "kN*m", 1e-4), new CalcExpectedValue("delta_max", 10.839, "mm", 1e-3)],
            "A 50 kN plant load 4 m along a 6 m span: R_A = Pb/L = 16.67 kN, M under the load = Pab/L = 66.67 kN·m, maximum deflection at x = √((L² − b²)/3) = 3.27 m."),

        new(BeamFormulasCalculator.Id, "propped-cantilever-udl", "Propped cantilever, UDL",
            new CalcReferenceSource("Roark's Formulas for Stress and Strain", "8th ed.", "Table 8.1 case 2c: R_prop = 3wL/8, M_fixed = −wL²/8, M_sag = 9wL²/128 at 5L/8, δ_max = wL⁴/(185EI)"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("case", BeamFormulasCalculator.ProppedUdl, CalcProvenance.Assumed("Fixed at the wall, propped at the free end.")),
                CalcInputValue.Quantity("L", 8, "m", CalcProvenance.Drawing("DRG-CA-033", "A")),
                CalcInputValue.Quantity("E", 210, "GPa", CalcProvenance.Specification("BS EN 1993-1-1 3.2.6")),
                CalcInputValue.Quantity("I", 20000, "cm^4", CalcProvenance.FromReference("Fabricated section, S1 sheet")),
                CalcInputValue.Quantity("w", 12, "kN/m", CalcProvenance.Specification("Load schedule LS-03")),
            ]),
            [new CalcExpectedValue("R_B", 36, "kN", 1e-9), new CalcExpectedValue("M_min", -96, "kN*m", 1e-9), new CalcExpectedValue("M_max", 54, "kN*m", 1e-9), new CalcExpectedValue("x_M", 5, "m", 1e-9)],
            "The same propped cantilever as the frame solver's known-answer case ka-propped-cantilever: the closed forms and the stiffness solution agree (R_prop 36 kN, M_fixed −96 kN·m, sag 54 kN·m at 5 m)."),
    ];
}
