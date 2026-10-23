using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// Cumulative fatigue damage by the Palmgren–Miner rule over a stress-range
/// spectrum, with an S–N curve of one slope through a reference point and an
/// optional endurance limit. Ported from the legacy <c>calc.fatigue-miner</c>
/// module (ADR-0167); its two hand-worked examples run as known-answer cases.
/// </summary>
public sealed class FatigueMinerCalculator : ICalcCalculator
{
    public const string Id = "mech.fatigue-miner";
    public const int MaxBlocks = 200;

    /// <summary>The high-cycle boundary below which a stress–life curve is not used.</summary>
    public const double MinimumLife = 1e4;

    private static readonly IReadOnlyList<CalcColumnSpec> Columns =
    [
        new("S", "Stress range", CalcInputKind.Quantity, "MPa", HardMin: 1e-6, HardMax: 1e5),
        new("n", "Cycles", CalcInputKind.Number, HardMin: 0, HardMax: 1e15),
    ];

    /// <summary>A block table: (stress range in MPa, cycles).</summary>
    public static CalcInputValue Blocks(params (double S, double N)[] blocks) =>
        CalcInputValue.Table("blocks", [.. blocks.Select(b => CalcRow.Of(("S", new CalcCell(b.S, "MPa")), ("n", new CalcCell(b.N))))]);

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.fatigue-miner (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.fatigue-miner.md, Example {n}");

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Mechanical, "Fatigue damage (Palmgren–Miner)",
        "Cumulative fatigue damage of a stress-range spectrum against an S–N curve of one slope through a reference point, with an optional endurance limit below which a range does no damage; the spectrum repetitions to failure.",
        "N_i = N_ref·(S_ref/S_i)^m, unbounded when S_i ≤ S_e. d_i = n_i/N_i. D = Σ d_i; repetitions to failure = 1/D. The damage passes when D ≤ 1.",
        [
            new CalcInputSpec("S_ref", "Reference stress range (detail category or material fatigue strength)", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials", ReferenceProperty: "FatigueStrength", ReferenceDimension: "Pressure", Default: 71, Min: 1, Max: 2000, HardMin: 1e-6, HardMax: 1e5),
            new CalcInputSpec("N_ref", "Cycles at the reference stress range", CalcInputKind.Number, Default: 2e6, Min: 1e4, Max: 1e9, HardMin: 1, HardMax: 1e15),
            new CalcInputSpec("m", "S–N slope", CalcInputKind.Number, Default: 3, Min: 2, Max: 15, HardMin: 0.1, HardMax: 50),
            new CalcInputSpec("S_e", "Endurance limit (ranges at or below it do no damage)", CalcInputKind.Quantity, "MPa", Min: 0.1, Max: 2000, HardMin: 1e-6, HardMax: 1e5, IsOptional: true),
            new CalcInputSpec("blocks", "Stress-range blocks", CalcInputKind.Table, Columns: Columns, MaxRows: MaxBlocks),
        ],
        [
            new CalcAssumption("Damage accumulates linearly and independently of the order of the blocks.", "Palmgren–Miner rule."),
            new CalcAssumption("One S–N slope over the whole range; no second slope below a knee.", "Simplified curve; a two-slope curve is entered as two sheets or a lower slope."),
        ],
        [
            new CalcClauseRef("BS EN 1993-1-9", "§8", "Fatigue resistance (S–N curves by detail category)"),
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "§6-15", "Cumulative fatigue damage"),
        ],
        [
            new CalcApplicabilityRule("no-blocks", "At least one stress-range block is needed.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("low-cycle", "A block with a predicted life below 10⁴ cycles is in the low-cycle (strain-life) regime.", CalcNoticeSeverity.Block),
        ],
        [
            new CalcTestCase("ka-legacy-cat71", "Legacy example 1: detail category 71, no cut-off", Legacy(1),
                [CalcInputValue.Quantity("S_ref", 71, "MPa"), CalcInputValue.Of("N_ref", 2e6), CalcInputValue.Of("m", 3), Blocks((100, 1e5), (60, 5e5), (40, 2e6))],
                [new CalcExpectedValue("N_1", 715_822, "", 5e-5), new CalcExpectedValue("d_1", 0.13970, "", 5e-5), new CalcExpectedValue("N_2", 3_313_991, "", 5e-5), new CalcExpectedValue("d_2", 0.15088, "", 5e-5), new CalcExpectedValue("N_3", 11_184_719, "", 5e-5), new CalcExpectedValue("d_3", 0.17882, "", 5e-5), new CalcExpectedValue("D", 0.46939, "", 5e-5), new CalcExpectedValue("R_f", 2.1304, "", 5e-5)]),
            new CalcTestCase("ka-legacy-slope5-limit", "Legacy example 2: slope 5 with an endurance limit, damage above one", Legacy(2),
                [CalcInputValue.Quantity("S_ref", 150, "MPa"), CalcInputValue.Of("N_ref", 1e6), CalcInputValue.Of("m", 5), CalcInputValue.Quantity("S_e", 60, "MPa"), Blocks((120, 2e6), (90, 1e7), (50, 1e7))],
                [new CalcExpectedValue("N_1", 3_051_758, "", 5e-5), new CalcExpectedValue("d_1", 0.65536, "", 5e-5), new CalcExpectedValue("N_2", 12_860_082, "", 5e-5), new CalcExpectedValue("d_2", 0.77760, "", 5e-5), new CalcExpectedValue("d_3", 0, "", 1e-12), new CalcExpectedValue("D", 1.4330, "", 5e-5), new CalcExpectedValue("R_f", 0.69786, "", 5e-5)]),
        ],
        Category: "Fatigue");

    public void Run(CalcRunContext c)
    {
        c.Input("S_ref");
        c.Input("N_ref");
        c.Input("m");
        var limited = c.Has("S_e");

        if (limited)
            c.Input("S_e");

        var rows = c.Rows("blocks");

        if (rows.Count == 0)
        {
            c.Block("no-blocks", "blocks: at least one stress-range block is needed.");
            return;
        }

        var sRef = c.Si("S_ref");
        var nRef = c.Si("N_ref");
        var m = c.Si("m");
        var sE = limited ? c.Si("S_e") : 0;
        var mpa = UnitCatalog.Parse("MPa");
        string F(double v) => CalcValue.FormatNumber(v, 5);
        var damages = new List<double>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            c.CancellationToken.ThrowIfCancellationRequested();
            var k = i + 1;
            var sCell = rows[i].Get("S") ?? throw new CalcEvaluationException($"blocks row {k}: the stress range is missing.");
            var s = UnitCatalog.Parse(sCell.Unit ?? "MPa").ToSi(sCell.Number ?? throw new CalcEvaluationException($"blocks row {k}: the stress range needs a number."));
            var n = rows[i].Get("n")?.Number ?? throw new CalcEvaluationException($"blocks row {k}: the cycle count needs a number.");

            if (limited && s <= sE)
            {
                c.Record($"N_{k}", $"S_{k} ≤ S_e", $"{F(mpa.FromSi(s))} MPa ≤ {F(mpa.FromSi(sE))} MPa", new TextValue("unbounded"), $"Life at block {k}: below the endurance limit");
                c.Record($"d_{k}", $"n_{k} / N_{k}", $"{F(n)} / ∞", new ScalarValue(0), $"Damage of block {k}");
                damages.Add(0);
                continue;
            }

            var life = nRef * Math.Pow(sRef / s, m);
            c.Record($"N_{k}", $"N_ref · (S_ref / S_{k})^m", $"{F(nRef)} × ({F(mpa.FromSi(sRef))} MPa / {F(mpa.FromSi(s))} MPa)^{F(m)}", new ScalarValue(life), $"Life at block {k}", "BS EN 1993-1-9 §8");

            if (life < MinimumLife)
            {
                c.Block("low-cycle", $"blocks row {k}: the predicted life {F(life)} cycles is below 10⁴, the low-cycle regime where a stress–life curve does not apply.");
                return;
            }

            var d = n / life;
            c.Record($"d_{k}", $"n_{k} / N_{k}", $"{F(n)} / {F(life)}", new ScalarValue(d), $"Damage of block {k}");
            damages.Add(d);
        }

        var total = damages.Sum();
        c.Record("D", "Σ d_i", string.Join(" + ", damages.Select(F)), new ScalarValue(total), "Total damage (Palmgren–Miner)", "Shigley §6-15");

        if (total > 0)
            c.Record("R_f", "1 / D", $"1 / {F(total)}", new ScalarValue(1 / total), "Spectrum repetitions to failure");
        else
            c.Record("R_f", "1 / D", "1 / 0", new TextValue("unbounded"), "Spectrum repetitions to failure");

        c.Output("D", "Total damage");
        c.Output("R_f", "Repetitions to failure");

        for (var k = 1; k <= rows.Count; k++)
        {
            c.Output($"N_{k}", $"Life at block {k}");
            c.Output($"d_{k}", $"Damage of block {k}");
        }

        c.CheckUtilisation("Cumulative damage D ≤ 1", "D", "Shigley §6-15");
    }
}
