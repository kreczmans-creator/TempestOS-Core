using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public static class MechanicalWorkedExamples
{
    private static CalcProvenance Book(string where) => CalcProvenance.Specification(where);

    public static IReadOnlyList<WorkedExample> All { get; } =
    [
        new(ToleranceStackCalculator.Id, "shouldered-screw-gap", "Shouldered-screw assembly gap (Shigley)",
            new CalcReferenceSource("Shigley's Mechanical Engineering Design", "9th ed.", "Ch. 1 tolerance stack-up example"),
            new CalcInputSet(
            [
                ToleranceStackCalculator.Table("in",
                    ("a shoulder length", "adds", 1.750, 0.003, -0.003),
                    ("b sleeve", "subtracts", 0.750, 0.001, -0.001),
                    ("c washer", "subtracts", 0.120, 0.005, -0.005),
                    ("d sleeve", "subtracts", 0.875, 0.001, -0.001)) with { Provenance = Book("Shigley 9e Ch. 1 example, part dimensions") },
                CalcInputValue.Quantity("minimum", 0.003, "in", Book("Shigley 9e Ch. 1 example, required gap")),
                CalcInputValue.Of("k", 3, CalcProvenance.Assumed("Each part at ±3σ within its band.")),
                CalcInputValue.Of("f_rss", 1.5, CalcProvenance.Assumed("Bender factor 1.5.")),
                CalcInputValue.Of("cpk_target", 1.33, CalcProvenance.Assumed("Usual capability target.")),
                CalcInputValue.Of("n_mc", 10_000, CalcProvenance.Assumed("Default sample count.")),
                CalcInputValue.Of("seed", 1, CalcProvenance.Assumed("Fixed seed for reproducibility.")),
                CalcInputValue.Choice("notation", "ASME Y14.5", CalcProvenance.Assumed("Inch drawing.")),
            ]),
            [new CalcExpectedValue("x_nom", 0.005, "in", 1e-9), new CalcExpectedValue("t_wc", 0.010, "in", 1e-9), new CalcExpectedValue("t_rss", 0.006, "in", 1e-6), new CalcExpectedValue("x_wc_min", -0.005, "in", 1e-6)],
            "Four inch-dimensioned parts on a shouldered screw. The worst-case gap can go negative (−0.005 in), so the stack fails the 0.003 in minimum; the RSS band ±0.006 in predicts most assemblies within 0.005 ± 0.006 in.",
            CalcReferenceStatus.Unconfirmed),

        new(ToleranceStackCalculator.Id, "bearing-housing-gap", "Bearing housing axial clearance (analytic)",
            new CalcReferenceSource("Analytic check", null, "RSS = √(0.05² + 0.06² + 0.02²) mm; C_pk from the nearer limit"),
            new CalcInputSet(
            [
                ToleranceStackCalculator.Table("mm",
                    ("Housing bore depth", "adds", 40, 0.10, 0),
                    ("Bearing width", "subtracts", 20, 0, -0.12),
                    ("Spacer", "subtracts", 19.5, 0.02, -0.02)) with { Provenance = CalcProvenance.Drawing("DRG-HSG-001", "B") },
                CalcInputValue.Quantity("minimum", 0.40, "mm", CalcProvenance.Specification("SPEC-BRG-7 §4.2")),
                CalcInputValue.Quantity("maximum", 0.80, "mm", CalcProvenance.Specification("SPEC-BRG-7 §4.2")),
                CalcInputValue.Of("k", 3, CalcProvenance.Assumed("Each part at ±3σ within its band.")),
                CalcInputValue.Of("f_rss", 1.5, CalcProvenance.Assumed("Bender factor 1.5.")),
                CalcInputValue.Of("cpk_target", 1.33, CalcProvenance.Assumed("Usual capability target.")),
                CalcInputValue.Of("n_mc", 10_000, CalcProvenance.Assumed("Default sample count.")),
                CalcInputValue.Of("seed", 1, CalcProvenance.Assumed("Fixed seed for reproducibility.")),
                CalcInputValue.Choice("notation", "BS 8888", CalcProvenance.Assumed("Metric drawing.")),
            ]),
            [new CalcExpectedValue("x_mean", 0.61, "mm", 1e-9), new CalcExpectedValue("t_wc", 0.13, "mm", 1e-9), new CalcExpectedValue("t_rss", 0.080622577, "mm", 1e-6), new CalcExpectedValue("cpk", 2.35666, "", 1e-5)],
            "Three metric parts with asymmetric deviations. Worst case 0.48–0.74 mm sits inside the 0.40–0.80 mm requirement with margins 0.08 and 0.06 mm; C_pk 2.36 against the 1.33 target."),
    ];
}
