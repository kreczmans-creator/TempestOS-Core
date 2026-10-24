using System.Globalization;
using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Mechanical.ToleranceStack;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// M1 — one-dimensional tolerance stack, version 2. Worst case, root-sum-
/// square, modified (Bender-factor) RSS, per-contributor distributions and
/// mean shifts, predicted σ, C_p, C_pk, DPMO and yield, a contribution
/// Pareto, a seeded Monte Carlo with histogram, GD&T contributors
/// (position, profile) with an explicit MMC bonus, and a BS 8888 / ASME
/// Y14.5 notation toggle for the sheet. Supersedes the `v0.16`
/// <c>calc.tolerance-stack</c> definition on the worksheet engine; the
/// old definition and its tests are untouched. The arithmetic lives in the
/// public <see cref="ToleranceStackEngine"/>; this class reads the table,
/// writes the steps and checks.
/// </summary>
public sealed class ToleranceStackCalculator : ICalcCalculator
{
    public const string Id = "mech.tolerance-stack";

    public const string KindSize = "size";
    public const string KindPosition = "position";
    public const string KindProfile = "profile";
    public const string DistributionNormal = "normal";
    public const string DistributionUniform = "uniform";
    public const string DistributionTriangular = "triangular";

    private static readonly IReadOnlyList<CalcColumnSpec> Columns =
    [
        new("name", "Name", CalcInputKind.Text),
        new("direction", "Direction", CalcInputKind.Choice, Choices: ["adds", "subtracts"]),
        new("nominal", "Nominal", CalcInputKind.Quantity, "mm", HardMin: -1e6, HardMax: 1e6),
        new("upper", "Upper deviation (or total zone for position/profile)", CalcInputKind.Quantity, "mm", HardMin: -1e3, HardMax: 1e3),
        new("lower", "Lower deviation", CalcInputKind.Quantity, "mm", HardMin: -1e3, HardMax: 1e3, IsOptional: true),
        new("kind", "Kind", CalcInputKind.Choice, Choices: [KindSize, KindPosition, KindProfile], IsOptional: true),
        new("bonus", "MMC bonus (position/profile)", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 1e3, IsOptional: true),
        new("distribution", "Distribution", CalcInputKind.Choice, Choices: [DistributionNormal, DistributionUniform, DistributionTriangular], IsOptional: true),
        new("shift", "Mean shift", CalcInputKind.Quantity, "mm", HardMin: -1e3, HardMax: 1e3, IsOptional: true),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "2.0.0", CalcDiscipline.Mechanical, "Tolerance stack-up (1D) v2",
        "A linear tolerance loop: worst-case limits, root-sum-square and modified RSS limits, predicted spread and capability against a stated requirement, contribution Pareto and a Monte Carlo prediction.",
        "Worst case: Σ|half-bands|. RSS: √Σ half². Modified RSS: factor × RSS (Bender-style). Predicted σ: √Σ σ_i² with σ_i from each contributor's distribution. C_pk from the nearer limit. Monte Carlo: seeded sampling of every contributor, summed with its sign.",
        [
            new CalcInputSpec("contributors", "Contributors in the loop", CalcInputKind.Table, Columns: Columns, MaxRows: 200),
            new CalcInputSpec("minimum", "Minimum acceptable result", CalcInputKind.Quantity, "mm", IsOptional: true, HardMin: -1e6, HardMax: 1e6),
            new CalcInputSpec("maximum", "Maximum acceptable result", CalcInputKind.Quantity, "mm", IsOptional: true, HardMin: -1e6, HardMax: 1e6),
            new CalcInputSpec("k", "Standard deviations per half-band (normal contributors)", CalcInputKind.Number, Default: 3, Min: 1, Max: 6, HardMin: 0.1, HardMax: 100),
            new CalcInputSpec("f_rss", "Modified-RSS factor (Bender-style)", CalcInputKind.Number, Default: 1.5, Min: 1, Max: 2, HardMin: 0.1, HardMax: 10),
            new CalcInputSpec("cpk_target", "Required C_pk", CalcInputKind.Number, Default: 1.33, Min: 1, Max: 2, HardMin: 0.01, HardMax: 10),
            new CalcInputSpec("n_mc", "Monte Carlo samples", CalcInputKind.Number, Default: 10_000, Min: 1_000, Max: 100_000, HardMin: 1, HardMax: CalcRunner.MaxCalculatorSamples),
            new CalcInputSpec("seed", "Monte Carlo seed", CalcInputKind.Number, Default: 1, HardMin: 0, HardMax: 4_294_967_295),
            new CalcInputSpec("notation", "Drawing notation", CalcInputKind.Choice, Choices: ["BS 8888", "ASME Y14.5"], DefaultChoice: "BS 8888"),
        ],
        [
            new CalcAssumption("Every contributor acts along the one axis of the stack.", "A linear stack: no angular or datum-shift effects beyond what the user enters as a contributor."),
            new CalcAssumption("Statistical figures assume contributors vary independently about their mid-limit (plus any stated mean shift), each with the distribution stated for it.", "The worst case is a guarantee; every statistical figure is a prediction about a population."),
            new CalcAssumption("A position or profile contributor contributes ± half its tolerance zone, plus half the stated MMC bonus.", "Diametral zones are bilateral about the true position."),
        ],
        [
            new CalcClauseRef("BS 8888", "—", "Technical product documentation and specification"),
            new CalcClauseRef("ASME Y14.5", "—", "Dimensioning and tolerancing"),
            new CalcClauseRef("ISO 2768-1", "—", "General tolerances (where a contributor's band is a general tolerance)"),
        ],
        [
            new CalcApplicabilityRule("few-contributors", "Fewer than four contributors: RSS and modified RSS are optimistic.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("no-requirement", "No requirement stated: capability and yield are not computed.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("min-above-max", "The stated minimum is above the stated maximum.", CalcNoticeSeverity.Block),
        ],
        [
            new CalcTestCase(
                "ka-shigley-screw", "Shouldered-screw gap, worst case and RSS",
                new CalcReferenceSource("Shigley's Mechanical Engineering Design", "9th ed.", "Ch. 1, tolerance stack-up example: gap 0.005 ± 0.010 in worst case; RSS ± 0.006 in"),
                [
                    Table(
                        ("a shoulder length", "adds", 1.750, 0.003, -0.003),
                        ("b sleeve", "subtracts", 0.750, 0.001, -0.001),
                        ("c washer", "subtracts", 0.120, 0.005, -0.005),
                        ("d sleeve", "subtracts", 0.875, 0.001, -0.001)),
                    CalcInputValue.Quantity("minimum", 0.003, "in"),
                    CalcInputValue.Of("k", 3),
                ],
                [
                    new CalcExpectedValue("x_nom", 0.005, "in", 1e-9),
                    new CalcExpectedValue("t_wc", 0.010, "in", 1e-9),
                    new CalcExpectedValue("x_wc_min", -0.005, "in", 1e-6),
                    new CalcExpectedValue("t_rss", 0.006, "in", 1e-6),
                    new CalcExpectedValue("sigma", 0.002, "in", 1e-6),
                    new CalcExpectedValue("cpk", 1.0 / 3.0, "", 1e-6),
                ],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase(
                "ka-rss-analytic", "Housing stack, analytic RSS and capability",
                new CalcReferenceSource("Analytic check", null, "RSS = √(0.05² + 0.06² + 0.02²) = 0.080623 mm; σ = RSS/3; C_pk = min((0.61−0.40), (0.80−0.61)) / (3σ) = 2.35666"),
                [
                    TableMm(
                        ("Housing bore depth", "adds", 40, 0.10, 0),
                        ("Bearing width", "subtracts", 20, 0, -0.12),
                        ("Spacer", "subtracts", 19.5, 0.02, -0.02)),
                    CalcInputValue.Quantity("minimum", 0.40, "mm"),
                    CalcInputValue.Quantity("maximum", 0.80, "mm"),
                    CalcInputValue.Of("k", 3),
                ],
                [
                    new CalcExpectedValue("x_mean", 0.61, "mm", 1e-9),
                    new CalcExpectedValue("t_wc", 0.13, "mm", 1e-9),
                    new CalcExpectedValue("t_rss", 0.080622577, "mm", 1e-6),
                    new CalcExpectedValue("sigma", 0.026874192, "mm", 1e-6),
                    new CalcExpectedValue("cpk", 2.3566600, "", 1e-6),
                    new CalcExpectedValue("dpmo", 7.7755e-7, "", 0.05),
                ]),
        ],
        DiagramKey: "tolerance-stack",
        Category: "Tolerancing");

    private static CalcInputValue Table(params (string Name, string Direction, double Nominal, double Upper, double Lower)[] rows) => Table("in", rows);

    private static CalcInputValue TableMm(params (string Name, string Direction, double Nominal, double Upper, double Lower)[] rows) => Table("mm", rows);

    /// <summary>A contributors table in one unit, for test cases and worked examples.</summary>
    public static CalcInputValue Table(string unit, params (string Name, string Direction, double Nominal, double Upper, double Lower)[] rows) =>
        CalcInputValue.Table("contributors", rows.Select(r => CalcRow.Of(
            ("name", new CalcCell(Text: r.Name)),
            ("direction", new CalcCell(Text: r.Direction)),
            ("nominal", new CalcCell(r.Nominal, unit)),
            ("upper", new CalcCell(r.Upper, unit)),
            ("lower", new CalcCell(r.Lower, unit)))).ToList());

    /// <summary>A table row as the sheet shows it: the engine contributor plus its label.</summary>
    private sealed record Line(Contributor Source, string Label)
    {
        public double Sign => Source.Sign;

        public double NominalSi => Source.Nominal;

        public double HalfSi => Source.Half;

        public double MidSi => Source.Mid;

        public double SigmaSi => Source.Sigma;
    }

    public void Run(CalcRunContext c)
    {
        var rows = c.Rows("contributors");
        var k = c.InputSi("k");
        var fRss = c.InputSi("f_rss");
        var cpkTarget = c.InputSi("cpk_target");
        var notation = c.InputText("notation");
        var unitSymbol = rows.Count > 0 && rows[0].Get("nominal")?.Unit is { } u ? u : "mm";
        var unit = UnitCatalog.Parse(unitSymbol);
        var hasMin = c.Has("minimum");
        var hasMax = c.Has("maximum");
        var minSi = hasMin ? c.InputSi("minimum") : (double?)null;
        var maxSi = hasMax ? c.InputSi("maximum") : (double?)null;

        if (rows.Count == 0)
        {
            c.Block("input.contributors", c.EnterRows("contributors"));
            return;
        }

        if (minSi is { } lo && maxSi is { } hi && lo > hi)
        {
            c.Block("min-above-max", "minimum: the stated minimum is above the stated maximum.");
            return;
        }

        var contributors = new List<Line>();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var name = row.Get("name")?.Text is { Length: > 0 } rowName ? CalcSheetTextRenderer.Clean(rowName) : $"Contributor {i + 1}";
            var sign = string.Equals(row.Get("direction")?.Text, "subtracts", StringComparison.Ordinal) ? -1.0 : 1.0;
            var kind = row.Get("kind")?.Text ?? KindSize;
            var distribution = row.Get("distribution")?.Text switch
            {
                DistributionUniform => ContributorDistribution.Uniform,
                DistributionTriangular => ContributorDistribution.Triangular,
                _ => ContributorDistribution.Normal,
            };
            var distributionName = row.Get("distribution")?.Text ?? DistributionNormal;
            var nominal = Si(row, "nominal");
            var upper = Si(row, "upper");
            var lower = row.Get("lower")?.Number is null ? 0.0 : Si(row, "lower");
            var bonus = row.Get("bonus")?.Number is null ? 0.0 : Si(row, "bonus");
            var shift = row.Get("shift")?.Number is null ? 0.0 : Si(row, "shift");
            Contributor source;
            string label;

            if (kind == KindSize)
            {
                if (lower > upper)
                {
                    c.Block("input.contributors", $"contributors row {i + 1} ({name}): the lower deviation is above the upper deviation.");
                    return;
                }

                source = Contributor.Size(name, sign, nominal, upper, lower, shift, distribution, k);
                label = notation == "ASME Y14.5"
                    ? $"{F(nominal, unit)} +{F(upper, unit)}/{F(lower, unit)}"
                    : $"{F(nominal, unit)} {(Math.Abs(upper + lower) < 1e-15 ? "±" + F(upper, unit) : "+" + F(upper, unit) + "/" + F(lower, unit))}";
            }
            else
            {
                if (upper < 0)
                {
                    c.Block("input.contributors", $"contributors row {i + 1} ({name}): a {kind} tolerance zone cannot be negative.");
                    return;
                }

                source = Contributor.Zone(name, sign, nominal, upper, bonus, shift, distribution, k);
                var mmc = bonus > 0 ? (notation == "ASME Y14.5" ? " (M)" : " Ⓜ") : string.Empty;
                label = $"{kind} zone {F(upper, unit)}{mmc}{(bonus > 0 ? $" + bonus {F(bonus, unit)}" : string.Empty)}";
            }

            var mid = source.Mid;
            var half = source.Half;
            var sigma = source.Sigma;

            contributors.Add(new Line(source, label));
            var symbol = $"c{i + 1}";
            c.Record(symbol, $"{(sign > 0 ? "+" : "−")} {label}", $"mid {F(mid, unit)} {unitSymbol}, ± {F(half, unit)} {unitSymbol}, σ {F(sigma, unit)} {unitSymbol} ({distributionName})", ScalarValue.Of(unit.FromSi(sign * mid), unit), name);
        }

        if (contributors.Count < 4)
            c.Warn("few-contributors", "contributors: fewer than four contributors; RSS and modified RSS are optimistic for so few.");

        var stack = ToleranceStackEngine.Analyse(
            [.. contributors.Select(x => x.Source)],
            hasMin || hasMax ? new StackRequirement(minSi, maxSi) : null,
            fRss);
        var meanSi = stack.Mean;
        var wcSi = stack.WorstCase;
        var sigmaSi = stack.Sigma;

        // Nominal, mean and worst case.
        c.Record("x_nom", "Σ (sign × nominal)", Join(contributors, x => $"{SignText(x)}{F(x.NominalSi, unit)}"), ScalarValue.Of(unit.FromSi(stack.Nominal), unit), "Nominal result");
        c.Record("x_mean", "Σ (sign × mid-limit)", Join(contributors, x => $"{SignText(x)}{F(x.MidSi, unit)}"), ScalarValue.Of(unit.FromSi(meanSi), unit), "Mean result");
        c.Record("t_wc", "Σ half-bands", Join(contributors, x => $"+ {F(x.HalfSi, unit)}"), ScalarValue.Of(unit.FromSi(wcSi), unit), "Worst-case tolerance (±)");
        c.Assign("x_wc_min", "x_mean - t_wc", unitSymbol, "Worst-case minimum");
        c.Assign("x_wc_max", "x_mean + t_wc", unitSymbol, "Worst-case maximum");

        // RSS and modified RSS.
        c.Record("t_rss", "√Σ half²", "√(" + Join(contributors, x => $"+ {F(x.HalfSi, unit)}²") + ")", ScalarValue.Of(unit.FromSi(stack.Rss), unit), "Root-sum-square tolerance (±)");
        c.Assign("x_rss_min", "x_mean - t_rss", unitSymbol, "RSS minimum");
        c.Assign("x_rss_max", "x_mean + t_rss", unitSymbol, "RSS maximum");
        c.Assign("t_mrss", "f_rss * t_rss", unitSymbol, "Modified RSS tolerance (±)");

        // Predicted spread from each contributor's own distribution.
        c.Record("sigma", "√Σ σ_i²", "√(" + Join(contributors, x => $"+ {F(x.SigmaSi, unit)}²") + ")", ScalarValue.Of(unit.FromSi(sigmaSi), unit), "Predicted standard deviation of the result");

        c.Output("x_nom", "Nominal result");
        c.Output("x_mean", "Mean result");
        c.Output("t_wc", "Worst-case tolerance (±)");
        c.Output("x_wc_min", "Worst-case minimum");
        c.Output("x_wc_max", "Worst-case maximum");
        c.Output("t_rss", "RSS tolerance (±)");
        c.Output("t_mrss", "Modified RSS tolerance (±)");
        c.Output("sigma", "Predicted σ");

        // Requirement checks and capability.
        if (hasMin || hasMax)
        {
            if (stack.MarginToMinimum is { } marginMin && minSi is { } lo2)
            {
                c.Record("m_min", "x_wc_min − minimum", $"{F(meanSi - wcSi, unit)} − {F(lo2, unit)}", ScalarValue.Of(unit.FromSi(marginMin), unit), "Worst-case margin to the minimum");
                c.Output("m_min", "Worst-case margin to the minimum");
            }

            if (stack.MarginToMaximum is { } marginMax && maxSi is { } hi2)
            {
                c.Record("m_max", "maximum − x_wc_max", $"{F(hi2, unit)} − {F(meanSi + wcSi, unit)}", ScalarValue.Of(unit.FromSi(marginMax), unit), "Worst-case margin to the maximum");
                c.Output("m_max", "Worst-case margin to the maximum");
            }

            c.CheckCondition("Worst case within requirement", stack.WorstCaseWithinRequirement,
                $"{F(meanSi - wcSi, unit)} … {F(meanSi + wcSi, unit)} {unitSymbol}",
                $"{(minSi is { } a ? F(a, unit) : "open")} … {(maxSi is { } b ? F(b, unit) : "open")} {unitSymbol}", "x_wc_min");

            if (stack.Capability is { } capability)
            {
                var cpk = capability.Cpk;
                var dpmo = capability.DefectsPerMillion;
                c.Record("cpk", "min(x_mean − LSL, USL − x_mean) / (3 σ)", $"min({(minSi is { } l3 ? F(meanSi - l3, unit) : "∞")}, {(maxSi is { } h3 ? F(h3 - meanSi, unit) : "∞")}) / (3 × {F(sigmaSi, unit)})", new ScalarValue(cpk), "Process capability index C_pk");

                if (capability.Cp is { } cp && minSi is { } l4 && maxSi is { } h4)
                {
                    c.Record("cp", "(USL − LSL) / (6 σ)", $"({F(h4, unit)} − {F(l4, unit)}) / (6 × {F(sigmaSi, unit)})", new ScalarValue(cp), "Process capability C_p");
                    c.Output("cp", "C_p");
                }

                c.Record("dpmo", "10⁶ × P(outside)", $"10⁶ × Φ-tails at σ = {F(sigmaSi, unit)}", new ScalarValue(dpmo), "Predicted defects per million");
                c.Record("yield", "1 − dpmo / 10⁶", $"1 − {CalcValue.FormatNumber(dpmo)} / 10⁶", ScalarValue.Of(capability.Yield * 100, "%"), "Predicted yield");
                c.Output("cpk", "C_pk");
                c.Output("dpmo", "DPMO (predicted)");
                c.Output("yield", "Yield (predicted)");
                if (cpk > 0)
                {
                    c.Record("u_cpk", "cpk_target / cpk", $"{CalcValue.FormatNumber(cpkTarget)} / {CalcValue.FormatNumber(cpk)}", new ScalarValue(cpkTarget / cpk), "Capability utilisation");
                    c.CheckUtilisation("C_pk against target", "u_cpk");
                }
                else
                {
                    c.CheckCondition("C_pk against target", false, $"C_pk {CalcValue.FormatNumber(cpk)}", $"≥ {CalcValue.FormatNumber(cpkTarget)}", "cpk");
                }
            }
        }
        else
        {
            c.Warn("no-requirement", "minimum/maximum: no requirement stated, so capability and yield are not computed.");
        }

        // Contribution Pareto.
        var paretoWc = stack.WorstCaseShare.Select((y, i) => (X: (double)(i + 1), Y: y)).ToList();
        var paretoSt = stack.StatisticalShare.Select((y, i) => (X: (double)(i + 1), Y: y)).ToList();
        c.Series(new CalcSeries("Contribution, worst case", "contributor", "", "share", "%", paretoWc));
        c.Series(new CalcSeries("Contribution, statistical", "contributor", "", "share", "%", paretoSt));
        c.Record("pareto", "largest contributor (statistical)", string.Join("; ", contributors.Select((x, i) => $"{x.Source.Name} {CalcValue.FormatNumber(paretoSt[i].Y, 3)} %")), new TextValue(contributors[stack.LargestStatisticalIndex].Source.Name), "Contribution Pareto");

        // Monte Carlo.
        var n = (int)Math.Round(c.InputSi("n_mc"));
        var seed = (ulong)Math.Max(0, Math.Round(c.InputSi("seed")));
        var mc = ToleranceStackEngine.Simulate([.. contributors.Select(x => x.Source)], n, seed, new StackRequirement(minSi, maxSi), cancellationToken: c.CancellationToken);

        c.Record("mc_mean", $"mean of {n} samples (seed {seed})", "Monte Carlo", ScalarValue.Of(unit.FromSi(mc.Mean), unit), "Monte Carlo mean");
        c.Record("mc_sigma", "sample standard deviation", "Monte Carlo", ScalarValue.Of(unit.FromSi(mc.StandardDeviation), unit), "Monte Carlo σ");
        c.Record("mc_p05", "5th percentile", "Monte Carlo", ScalarValue.Of(unit.FromSi(mc.Percentile05), unit), "Monte Carlo 5 %");
        c.Record("mc_p95", "95th percentile", "Monte Carlo", ScalarValue.Of(unit.FromSi(mc.Percentile95), unit), "Monte Carlo 95 %");
        c.Output("mc_mean", "Monte Carlo mean");
        c.Output("mc_sigma", "Monte Carlo σ");
        c.Output("mc_p05", "Monte Carlo 5th percentile");
        c.Output("mc_p95", "Monte Carlo 95th percentile");

        if (mc.Yield is { } y)
        {
            c.Record("mc_yield", "samples inside the requirement", $"{n - (int)Math.Round((mc.DefectsPerMillion ?? 0) / 1e6 * n)} of {n}", ScalarValue.Of(y * 100, "%"), "Monte Carlo yield");
            c.Output("mc_yield", "Monte Carlo yield");
        }

        c.Series(new CalcSeries("Monte Carlo histogram", "result", unitSymbol, "count", "", mc.Histogram.Select(b => (unit.FromSi((b.Lower + b.Upper) / 2), (double)b.Count)).ToList()));
    }

    private static double Si(CalcRow row, string key)
    {
        var cell = row.Get(key) ?? throw new CalcEvaluationException($"contributors: column '{key}' is missing.");
        return UnitCatalog.Parse(cell.Unit ?? "mm").ToSi(cell.Number ?? throw new CalcEvaluationException($"contributors: column '{key}' needs a number."));
    }

    private static string F(double si, Unit unit) => CalcValue.FormatNumber(unit.FromSi(si), 6);

    private static string SignText(Line x) => x.Sign > 0 ? "+ " : "− ";

    private static string Join(IEnumerable<Line> xs, Func<Line, string> f) => string.Join(" ", xs.Select(f)).TrimStart('+', ' ');
}
