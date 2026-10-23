using Tempest.Calc.Sheets;

namespace Tempest.Calc.Structural;

/// <summary>
/// S2b — the beam formula-case library: closed-form reactions, moments,
/// shears and deflections for the standard single-span cases (Roark
/// Table 8.1), as quick checks on the frame solver and on hand sheets.
/// </summary>
public sealed class BeamFormulasCalculator : ICalcCalculator
{
    public const string Id = "struct.beam-formulas";

    public const string SsUdl = "simply supported, UDL";
    public const string SsPoint = "simply supported, point load at a";
    public const string SsMomentEnd = "simply supported, couple at the left end";
    public const string CantUdl = "cantilever, UDL";
    public const string CantPoint = "cantilever, point load at a";
    public const string FixedUdl = "fixed both ends, UDL";
    public const string FixedPointCentre = "fixed both ends, point load at midspan";
    public const string ProppedUdl = "propped cantilever (fixed left, pinned right), UDL";

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Structural, "Beam formulas (single span)",
        "Closed-form reactions, maximum moment and its position, maximum shear and maximum deflection for the standard single-span beam cases, as a quick check. Left end at x = 0; loads downward; sagging moments positive; deflection downward positive.",
        "Roark Table 8.1 formulas in own notation. For the simply supported point load at a (b = L − a): R_A = Pb/L, M_max = Pab/L, δ_max = Pb(L² − b²)^(3/2)/(9√3 L EI) at x = √((L² − b²)/3) when a ≥ b. For the propped cantilever: R_B = 3wL/8, M_A = −wL²/8, sag M_max = 9wL²/128 at x = 5L/8.",
        [
            new CalcInputSpec("case", "Case", CalcInputKind.Choice, Choices: [SsUdl, SsPoint, SsMomentEnd, CantUdl, CantPoint, FixedUdl, FixedPointCentre, ProppedUdl], DefaultChoice: SsUdl),
            new CalcInputSpec("L", "Span", CalcInputKind.Quantity, "m", Default: 6, Min: 0.1, Max: 100, HardMin: 1e-3, HardMax: 1e4),
            new CalcInputSpec("E", "Modulus of elasticity", CalcInputKind.Quantity, "GPa", Default: 210, Min: 1, Max: 500, HardMin: 1e-3, HardMax: 1e4),
            new CalcInputSpec("I", "Second moment of area", CalcInputKind.Quantity, "cm^4", Default: 8000, Min: 1, Max: 1e8, HardMin: 1e-6, HardMax: 1e12),
            new CalcInputSpec("w", "Distributed load (downward)", CalcInputKind.Quantity, "kN/m", Default: 10, Min: 0, Max: 1e4, HardMin: 0, HardMax: 1e7, IsOptional: true),
            new CalcInputSpec("P", "Point load (downward)", CalcInputKind.Quantity, "kN", Default: 50, Min: 0, Max: 1e5, HardMin: 0, HardMax: 1e8, IsOptional: true),
            new CalcInputSpec("M_0", "Applied couple", CalcInputKind.Quantity, "kN*m", Default: 20, Min: 0, Max: 1e6, HardMin: 0, HardMax: 1e9, IsOptional: true),
            new CalcInputSpec("a", "Load position from the left end", CalcInputKind.Quantity, "m", Default: 3, Min: 0, Max: 100, HardMin: 0, HardMax: 1e4, IsOptional: true),
        ],
        [
            new CalcAssumption("Prismatic, linear elastic, Euler–Bernoulli beam; small deflections.", "Roark Table 8.1 conditions."),
            new CalcAssumption("A cantilever is fixed at the left end (x = 0) and free at the right.", "Case convention."),
        ],
        [
            new CalcClauseRef("Roark's Formulas for Stress and Strain", "Ch. 8, Table 8.1", "Shear, moment, slope and deflection formulas for elastic straight beams"),
        ],
        [
            new CalcApplicabilityRule("position", "The load position must lie on the span.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("deflection-position", "For a simply supported point load the deflection formula assumes the load is at or right of midspan; the beam is mirrored when it is not.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-ss-udl", "Simply supported UDL", new CalcReferenceSource("Roark's Formulas for Stress and Strain", "8th ed.", "Table 8.1 case 2e: R = wL/2, M = wL²/8, δ = 5wL⁴/384EI"),
                [CalcInputValue.Choice("case", SsUdl), CalcInputValue.Quantity("L", 6, "m"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Quantity("I", 8000, "cm^4"), CalcInputValue.Quantity("w", 10, "kN/m")],
                [new CalcExpectedValue("R_A", 30, "kN", 1e-9), new CalcExpectedValue("M_max", 45, "kN*m", 1e-9), new CalcExpectedValue("x_M", 3, "m", 1e-9), new CalcExpectedValue("delta_max", 10.0446, "mm", 1e-4)]),
            new CalcTestCase("ka-ss-point", "Simply supported point load off-centre", new CalcReferenceSource("Roark's Formulas for Stress and Strain", "8th ed.", "Table 8.1 case 2a: L = 6, a = 4, P = 50: R_A = 16.667, R_B = 33.333 kN, M_max = 66.667 kN·m at x = 4; δ_max = Pb(L²−b²)^1.5/(9√3 L EI) at x = √((L²−b²)/3) = 3.266 m; δ_max = 11.520 mm with EI = 16,800 kN·m²"),
                [CalcInputValue.Choice("case", SsPoint), CalcInputValue.Quantity("L", 6, "m"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Quantity("I", 8000, "cm^4"), CalcInputValue.Quantity("P", 50, "kN"), CalcInputValue.Quantity("a", 4, "m")],
                [new CalcExpectedValue("R_A", 16.6667, "kN", 1e-4), new CalcExpectedValue("R_B", 33.3333, "kN", 1e-4), new CalcExpectedValue("M_max", 66.6667, "kN*m", 1e-4), new CalcExpectedValue("x_delta", 3.2660, "m", 1e-4), new CalcExpectedValue("delta_max", 11.5202, "mm", 1e-4)]),
            new CalcTestCase("ka-cantilever-point", "Cantilever tip load", new CalcReferenceSource("Roark's Formulas for Stress and Strain", "8th ed.", "Table 8.1 case 1a: M = −PL, δ = PL³/3EI"),
                [CalcInputValue.Choice("case", CantPoint), CalcInputValue.Quantity("L", 3, "m"), CalcInputValue.Quantity("E", 200, "GPa"), CalcInputValue.Quantity("I", 2000, "cm^4"), CalcInputValue.Quantity("P", 20, "kN"), CalcInputValue.Quantity("a", 3, "m")],
                [new CalcExpectedValue("R_A", 20, "kN", 1e-9), new CalcExpectedValue("M_min", -60, "kN*m", 1e-9), new CalcExpectedValue("delta_max", 45, "mm", 1e-9)]),
            new CalcTestCase("ka-fixed-udl", "Fixed-ended UDL", new CalcReferenceSource("Roark's Formulas for Stress and Strain", "8th ed.", "Table 8.1 case 2d: M_ends = −wL²/12, M_mid = wL²/24, δ = wL⁴/384EI"),
                [CalcInputValue.Choice("case", FixedUdl), CalcInputValue.Quantity("L", 6, "m"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Quantity("I", 8000, "cm^4"), CalcInputValue.Quantity("w", 10, "kN/m")],
                [new CalcExpectedValue("M_min", -30, "kN*m", 1e-9), new CalcExpectedValue("M_max", 15, "kN*m", 1e-9), new CalcExpectedValue("delta_max", 2.00893, "mm", 1e-4)]),
        ],
        DiagramKey: "beam-formulas",
        Category: "Analysis");

    public void Run(CalcRunContext c)
    {
        var kase = c.InputText("case");
        c.Input("L");
        c.Input("E");
        c.Input("I");
        c.Assign("EI", "E * I", "kN*m^2", "Flexural rigidity");
        var l = c.Si("L");

        switch (kase)
        {
            case SsUdl:
                c.Input("w");
                c.Assign("R_A", "w * L / 2", "kN", "Left reaction", "Roark 8.1 2e");
                c.Assign("R_B", "w * L / 2", "kN", "Right reaction");
                c.Assign("M_max", "w * L^2 / 8", "kN*m", "Maximum (sagging) moment");
                c.Assign("x_M", "L / 2", "m", "Position of the maximum moment");
                c.Assign("M_min", "0 kN*m", "kN*m", "Minimum moment");
                c.Assign("V_max", "w * L / 2", "kN", "Maximum shear");
                c.Assign("delta_max", "5 * w * L^4 / (384 * EI)", "mm", "Maximum deflection");
                c.Assign("x_delta", "L / 2", "m", "Position of the maximum deflection");
                break;

            case SsPoint:
            {
                c.Input("P");
                c.Input("a");
                var a = c.Si("a");
                if (a < 0 || a > l) { c.Block("position", "a: the load must lie on the span (0 … L)."); return; }
                if (a < l / 2) c.Warn("deflection-position", "a: the load is left of midspan; the beam is mirrored for the deflection formula (b and a swapped).");
                c.Assign("b", "L - a", "m", "Distance from the load to the right end");
                c.Assign("R_A", "P * b / L", "kN", "Left reaction", "Roark 8.1 2a");
                c.Assign("R_B", "P * a / L", "kN", "Right reaction");
                c.Assign("M_max", "P * a * b / L", "kN*m", "Maximum moment, under the load");
                c.Assign("x_M", "a", "m", "Position of the maximum moment");
                c.Assign("M_min", "0 kN*m", "kN*m", "Minimum moment");
                c.Assign("V_max", "max(R_A, R_B)", "kN", "Maximum shear");
                c.Assign("c_far", a >= l / 2 ? "b" : "a", "m", "Shorter segment (the deflection formula's b)");
                c.Assign("x_delta", a >= l / 2 ? "sqrt((L^2 - c_far^2) / 3)" : "L - sqrt((L^2 - c_far^2) / 3)", "m", "Position of the maximum deflection");
                c.Assign("delta_max", "P * c_far * sqrt((L^2 - c_far^2)^3) / (9 * sqrt(3) * L * EI)", "mm", "Maximum deflection");
                break;
            }

            case SsMomentEnd:
                c.Input("M_0");
                c.Assign("R_A", "-M_0 / L", "kN", "Left reaction (upward positive, so negative: the couple lifts the left support)", "Roark 8.1 3a");
                c.Assign("R_B", "M_0 / L", "kN", "Right reaction");
                c.Assign("M_max", "M_0", "kN*m", "Maximum moment, at the loaded end");
                c.Assign("x_M", "0 m", "m", "Position of the maximum moment");
                c.Assign("M_min", "0 kN*m", "kN*m", "Minimum moment");
                c.Assign("V_max", "M_0 / L", "kN", "Shear (constant)");
                c.Assign("x_delta", "L * (1 - 1 / sqrt(3))", "m", "Position of the maximum deflection");
                c.Assign("delta_max", "M_0 * L^2 / (9 * sqrt(3) * EI)", "mm", "Maximum deflection");
                break;

            case CantUdl:
                c.Input("w");
                c.Assign("R_A", "w * L", "kN", "Fixed-end reaction", "Roark 8.1 1e");
                c.Assign("R_B", "0 kN", "kN", "Free end");
                c.Assign("M_min", "-w * L^2 / 2", "kN*m", "Fixed-end (hogging) moment");
                c.Assign("M_max", "0 kN*m", "kN*m", "Maximum sagging moment");
                c.Assign("x_M", "0 m", "m", "Position of the extreme moment");
                c.Assign("V_max", "w * L", "kN", "Maximum shear");
                c.Assign("delta_max", "w * L^4 / (8 * EI)", "mm", "Tip deflection");
                c.Assign("x_delta", "L", "m", "Position of the maximum deflection");
                break;

            case CantPoint:
            {
                c.Input("P");
                c.Input("a");
                var a = c.Si("a");
                if (a < 0 || a > l) { c.Block("position", "a: the load must lie on the span (0 … L)."); return; }
                c.Assign("R_A", "P", "kN", "Fixed-end reaction", "Roark 8.1 1a");
                c.Assign("R_B", "0 kN", "kN", "Free end");
                c.Assign("M_min", "-P * a", "kN*m", "Fixed-end (hogging) moment");
                c.Assign("M_max", "0 kN*m", "kN*m", "Maximum sagging moment");
                c.Assign("x_M", "0 m", "m", "Position of the extreme moment");
                c.Assign("V_max", "P", "kN", "Maximum shear");
                c.Assign("delta_max", "P * a^2 * (3 * L - a) / (6 * EI)", "mm", "Tip deflection");
                c.Assign("x_delta", "L", "m", "Position of the maximum deflection");
                break;
            }

            case FixedUdl:
                c.Input("w");
                c.Assign("R_A", "w * L / 2", "kN", "Left reaction", "Roark 8.1 2d");
                c.Assign("R_B", "w * L / 2", "kN", "Right reaction");
                c.Assign("M_min", "-w * L^2 / 12", "kN*m", "End (hogging) moments");
                c.Assign("M_max", "w * L^2 / 24", "kN*m", "Midspan moment");
                c.Assign("x_M", "L / 2", "m", "Position of the maximum sagging moment");
                c.Assign("V_max", "w * L / 2", "kN", "Maximum shear");
                c.Assign("delta_max", "w * L^4 / (384 * EI)", "mm", "Midspan deflection");
                c.Assign("x_delta", "L / 2", "m", "Position of the maximum deflection");
                break;

            case FixedPointCentre:
                c.Input("P");
                c.Assign("R_A", "P / 2", "kN", "Left reaction", "Roark 8.1 1d");
                c.Assign("R_B", "P / 2", "kN", "Right reaction");
                c.Assign("M_min", "-P * L / 8", "kN*m", "End (hogging) moments");
                c.Assign("M_max", "P * L / 8", "kN*m", "Midspan moment");
                c.Assign("x_M", "L / 2", "m", "Position of the maximum sagging moment");
                c.Assign("V_max", "P / 2", "kN", "Maximum shear");
                c.Assign("delta_max", "P * L^3 / (192 * EI)", "mm", "Midspan deflection");
                c.Assign("x_delta", "L / 2", "m", "Position of the maximum deflection");
                break;

            case ProppedUdl:
                c.Input("w");
                c.Assign("R_A", "5 * w * L / 8", "kN", "Fixed-end reaction", "Roark 8.1 2c");
                c.Assign("R_B", "3 * w * L / 8", "kN", "Propped-end reaction");
                c.Assign("M_min", "-w * L^2 / 8", "kN*m", "Fixed-end (hogging) moment");
                c.Assign("M_max", "9 * w * L^2 / 128", "kN*m", "Maximum sagging moment");
                c.Assign("x_M", "5 * L / 8", "m", "Position of the maximum sagging moment");
                c.Assign("V_max", "5 * w * L / 8", "kN", "Maximum shear");
                c.Assign("x_delta", "L * (1 + sqrt(33)) / 16", "m", "Position of the maximum deflection");
                c.Assign("delta_max", "w * L^4 / (185 * EI)", "mm", "Maximum deflection");
                break;

            default:
                c.Block("position", $"case: '{kase}' is not a case this sheet knows.");
                return;
        }

        foreach (var (symbol, text) in new[] { ("R_A", "Left reaction"), ("R_B", "Right reaction"), ("M_max", "Maximum sagging moment"), ("x_M", "Position of M_max"), ("M_min", "Minimum (hogging) moment"), ("V_max", "Maximum shear"), ("delta_max", "Maximum deflection"), ("x_delta", "Position of δ_max") })
            c.Output(symbol, text);
    }
}
