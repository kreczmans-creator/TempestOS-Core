using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Electrical;

/// <summary>
/// Steady-state junction (source) temperature from a series chain of thermal
/// resistances to ambient: the heat sink sizing calculation. Ported from the
/// legacy <c>calc.thermal-resistance-chain</c> module (ADR-0167, D-40); its
/// four hand-worked examples run as known-answer cases.
/// </summary>
public sealed class ThermalChainCalculator : ICalcCalculator
{
    public const string Id = "elec.thermal-chain";
    public const int MaxStages = 50;

    private static readonly IReadOnlyList<CalcColumnSpec> Columns =
    [
        new("name", "Stage", CalcInputKind.Text, IsOptional: true),
        new("R", "Thermal resistance", CalcInputKind.Quantity, "K/W", HardMin: 0, HardMax: 1e6),
    ];

    /// <summary>A stage table: (name, resistance in K/W), hottest first.</summary>
    public static CalcInputValue Stages(params (string Name, double R)[] stages) =>
        CalcInputValue.Table("stages", [.. stages.Select(s => CalcRow.Of(("name", new CalcCell(Text: s.Name)), ("R", new CalcCell(s.R, "K/W"))))]);

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.thermal-resistance-chain (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.thermal-resistance-chain.md, Example {n}");

    private static CalcInputValue[] ThreeStage(double power) =>
    [
        CalcInputValue.Quantity("P", power, "W"), CalcInputValue.Quantity("T_a", 40, "degC"),
        Stages(("junction to case", 0.5), ("case to sink", 0.2), ("sink to ambient", 1.8)), CalcInputValue.Quantity("T_max", 125, "degC"),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.0.1", CalcDiscipline.Electrical, "Heat sink thermal resistance chain",
        "Steady-state source (junction) temperature from a series chain of thermal resistances to ambient (junction to case, case to heat sink, heat sink to ambient), with each stage's temperature rise, the margin against the permitted maximum and the largest total resistance the limit allows: the heat sink budget.",
        "Temperature rise across each stage: ΔT_i = P·R_i.\nTotal resistance of the chain and the total rise: R_tot = Σ R_i; ΔT_tot = P·R_tot.\nSource (hottest node) temperature, each node below it being the one above less that stage's rise: T_1 = T_a + ΔT_tot.\nMargin against the permitted maximum: margin = T_max − T_1.\nLargest total resistance the limit allows, the heat sink budget: R_max = (T_max − T_a)/P.\nUtilisation of the permitted rise: u = ΔT_tot/(T_max − T_a).",
        [
            new CalcInputSpec("P", "Steady power dissipated (the heat flow through every stage)", CalcInputKind.Quantity, "W", Default: 25, Min: 0, Max: 1e4, HardMin: 0, HardMax: 1e7, Group: "Source"),
            new CalcInputSpec("T_a", "Ambient the last stage rejects to", CalcInputKind.Quantity, "degC", Default: 40, Min: -40, Max: 85, HardMin: -273.15, HardMax: 1000, Group: "Conditions"),
            new CalcInputSpec("stages", "Thermal resistances in series, hottest first", CalcInputKind.Table, Columns: Columns, MaxRows: MaxStages, Group: "Chain"),
            new CalcInputSpec("T_max", "Maximum permitted temperature of the hottest node (junction or case)", CalcInputKind.Quantity, "degC", Default: 125, Min: 40, Max: 200, HardMin: -273.15, HardMax: 2000, Group: "Source"),
        ],
        [
            new CalcAssumption("Steady state: the whole dissipated power flows through the one chain, and each resistance is constant.", "No thermal capacitance (nothing about warm-up or a transient overload); no parallel path through the board."),
            new CalcAssumption("The stated ambient is the temperature the last stage actually rejects to.", "Air preheated by neighbouring parts can be well above room temperature."),
        ],
        [
            new CalcClauseRef("JEDEC JESD51-1", "—", "Integrated circuit thermal measurement method: electrical test method (junction-to-reference resistances)"),
            new CalcClauseRef("Çengel & Ghajar, Heat and Mass Transfer", "§15-2", "Thermal resistance networks in the cooling of electronic equipment"),
            new CalcClauseRef("Incropera & DeWitt, Fundamentals of Heat and Mass Transfer", "§3.1", "Thermal resistance in series"),
        ],
        [
            new CalcApplicabilityRule("no-stages", "At least one thermal resistance stage is needed.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("zero-chain", "The total thermal resistance must be positive.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("limit", "The maximum source temperature must be above the ambient.", CalcNoticeSeverity.Block),
        ],
        [
            new CalcTestCase("ka-legacy-three-stage", "Legacy example 1: three-stage chain at 25 W", Legacy(1), ThreeStage(25),
                [new CalcExpectedValue("R_tot", 2.5, "K/W", 5e-5), new CalcExpectedValue("dT_1", 12.5, "K", 5e-5), new CalcExpectedValue("dT_2", 5, "K", 5e-5), new CalcExpectedValue("dT_3", 45, "K", 5e-5), new CalcExpectedValue("T_1", 102.5, "degC", 5e-5), new CalcExpectedValue("T_2", 90, "degC", 5e-5), new CalcExpectedValue("T_3", 85, "degC", 5e-5), new CalcExpectedValue("dT_m", 22.5, "K", 5e-5), new CalcExpectedValue("u", 0.73529, "", 5e-5), new CalcExpectedValue("R_max", 3.4, "K/W", 5e-5)]),
            new CalcTestCase("ka-legacy-sink-selection", "Legacy example 2: heat sink selection for a 60 W transistor (after Çengel & Ghajar)", Legacy(2),
                [CalcInputValue.Quantity("P", 60, "W"), CalcInputValue.Quantity("T_a", 30, "degC"), Stages(("case to ambient (sink)", 0.9)), CalcInputValue.Quantity("T_max", 90, "degC")],
                [new CalcExpectedValue("R_max", 1.0, "K/W", 5e-5), new CalcExpectedValue("dT_tot", 54, "K", 5e-5), new CalcExpectedValue("T_1", 84, "degC", 5e-5), new CalcExpectedValue("dT_m", 6, "K", 5e-5), new CalcExpectedValue("u", 0.9, "", 5e-5)]),
            new CalcTestCase("ka-legacy-over-limit", "Legacy example 3: the same chain at 40 W exceeds the limit", Legacy(3), ThreeStage(40),
                [new CalcExpectedValue("dT_1", 20, "K", 5e-5), new CalcExpectedValue("dT_2", 8, "K", 5e-5), new CalcExpectedValue("dT_3", 72, "K", 5e-5), new CalcExpectedValue("T_1", 140, "degC", 5e-5), new CalcExpectedValue("T_2", 120, "degC", 5e-5), new CalcExpectedValue("T_3", 112, "degC", 5e-5), new CalcExpectedValue("dT_m", -15, "K", 5e-5), new CalcExpectedValue("u", 1.1765, "", 5e-5), new CalcExpectedValue("R_max", 2.125, "K/W", 5e-5)]),
            new CalcTestCase("ka-legacy-on-limit", "Legacy example 4: exactly on the limit at 34 W", Legacy(4), ThreeStage(34),
                [new CalcExpectedValue("dT_tot", 85, "K", 5e-5), new CalcExpectedValue("T_1", 125, "degC", 5e-5), new CalcExpectedValue("dT_m", 0, "K", 1e-9), new CalcExpectedValue("u", 1, "", 5e-5)]),
        ],
        Category: "Thermal");

    public void Run(CalcRunContext c)
    {
        c.Input("P");
        c.Input("T_a");
        var rows = c.Rows("stages");
        c.Input("T_max");

        if (rows.Count == 0)
        {
            c.Block("no-stages", "stages: at least one thermal resistance stage is needed.");
            return;
        }

        if (!(c.Si("T_max") > c.Si("T_a")))
        {
            c.Block("limit", "T_max: the maximum source temperature must be above the ambient.");
            return;
        }

        var p = c.Si("P");
        var kpw = UnitCatalog.Parse("K/W");
        var kelvin = UnitCatalog.Parse("K");
        var degC = UnitCatalog.Parse("degC");
        string F(double v) => CalcValue.FormatNumber(v, 5);
        var resistances = new List<double>(rows.Count);
        var names = new List<string>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            var n = i + 1;
            var cell = rows[i].Get("R") ?? throw new CalcEvaluationException($"stages row {n}: the resistance is missing.");
            resistances.Add(UnitCatalog.Parse(cell.Unit ?? "K/W").ToSi(cell.Number ?? throw new CalcEvaluationException($"stages row {n}: the resistance needs a number.")));
            names.Add(rows[i].Get("name")?.Text is { Length: > 0 } text ? text : $"stage {n}");
        }

        var total = resistances.Sum();

        if (!(total > 0))
        {
            c.Block("zero-chain", "stages: the total thermal resistance is zero; enter at least one positive resistance.");
            return;
        }

        c.Record("R_tot", "Σ R_i", string.Join(" + ", resistances.Select(F)) + " K/W", ScalarValue.Of(total, kpw), "Total thermal resistance", "Incropera §3.1");
        c.Assign("dT_tot", "P * R_tot", "K", "Total rise from ambient to the source");
        c.Assign("T_1", "T_a + dT_tot", "degC", $"Source temperature (hot side of {names[0]})", "Çengel §15-2");

        // Built from the source down: each node is the one above it less that stage's rise,
        // so the last stage's cold side is the ambient. Rises are temperature differences in K.
        var node = c.Si("T_1");

        for (var i = 0; i < resistances.Count; i++)
        {
            c.CancellationToken.ThrowIfCancellationRequested();
            var n = i + 1;
            var rise = p * resistances[i];
            c.Record($"dT_{n}", $"P · R_{n}", $"{F(p)} W × {F(resistances[i])} K/W", ScalarValue.Of(rise, kelvin), $"Rise across {names[i]}");

            if (n > 1)
                c.Record($"T_{n}", $"T_{n - 1} − dT_{n - 1}", $"{F(degC.FromSi(node + p * resistances[i - 1]))} °C − {F(p * resistances[i - 1])} K", new ScalarValue(node, degC.Dimension, degC), $"Hot side of {names[i]}");

            node -= rise;
        }

        c.Assign("dT_m", "T_max - T_1", "K", "Thermal margin (negative when over the limit)");
        c.Assign("u", "dT_tot / (T_max - T_a)", null, "Utilisation: rise over permitted rise");

        if (p > 0)
            c.Assign("R_max", "(T_max - T_a) / P", "K/W", "Largest total resistance the limit allows (heat sink budget)");
        else
            c.Record("R_max", "(T_max − T_a) / P", "P = 0", new TextValue("unbounded"), "Largest total resistance the limit allows (heat sink budget)");

        c.Output("T_1", "Source temperature");
        c.Output("dT_m", "Thermal margin");
        c.Output("u", "Utilisation");
        c.Output("R_max", "Heat sink budget");
        c.Output("R_tot", "Total thermal resistance");
        c.Output("dT_tot", "Total rise");

        for (var n = 1; n <= resistances.Count; n++)
        {
            c.Output($"dT_{n}", $"Rise across stage {n}");

            if (n > 1)
                c.Output($"T_{n}", $"Hot side of stage {n}");
        }

        c.CheckUtilisation("Source temperature T_1 ≤ T_max", "u", "Çengel §15-2");
    }
}
