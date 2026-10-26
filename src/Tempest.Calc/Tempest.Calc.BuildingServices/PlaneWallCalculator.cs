using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.BuildingServices;

/// <summary>
/// Steady one-dimensional heat flow through a composite plane wall by the
/// thermal-circuit method: convection films and conducting layers as
/// resistances in series. Ported from the legacy
/// <c>calc.plane-wall-heat-transfer</c> module (ADR-0167); its three
/// hand-worked examples (two after Çengel) run as known-answer cases.
/// </summary>
public sealed class PlaneWallCalculator : ICalcCalculator
{
    public const string Id = "bs.plane-wall";
    public const int MaxLayers = 50;

    private static readonly IReadOnlyList<CalcColumnSpec> Columns =
    [
        new("name", "Layer", CalcInputKind.Text, IsOptional: true),
        new("t", "Thickness", CalcInputKind.Quantity, "mm", HardMin: 1e-6, HardMax: 1e5),
        new("k", "Thermal conductivity", CalcInputKind.Quantity, "W/(m*K)", HardMin: 1e-6, HardMax: 1e4),
    ];

    /// <summary>A layer table: (name, thickness in mm, conductivity in W/(m·K)), hot side first.</summary>
    public static CalcInputValue Layers(params (string Name, double T, double K)[] layers) =>
        CalcInputValue.Table("layers", [.. layers.Select(l => CalcRow.Of(("name", new CalcCell(Text: l.Name)), ("t", new CalcCell(l.T, "mm")), ("k", new CalcCell(l.K, "W/(m*K)"))))]);

    private static CalcReferenceSource Legacy(int n, string? after = null) =>
        new($"TempestOS calc.plane-wall-heat-transfer (v0.21.0) hand calculation{after}", null, $"docs/engineering/calculations/calc.plane-wall-heat-transfer.md, Example {n}");

    private static CalcInputValue[] Window(CalcInputValue layers) =>
    [
        CalcInputValue.Quantity("T_1", 20, "degC"), CalcInputValue.Quantity("h_1", 10, "W/(m^2*K)"), layers,
        CalcInputValue.Quantity("h_2", 40, "W/(m^2*K)"), CalcInputValue.Quantity("T_2", -10, "degC"), CalcInputValue.Quantity("A", 1.2, "m^2"),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.0.1", CalcDiscipline.BuildingServices, "Plane wall heat transfer (layers and films)",
        "Steady heat flow through a composite plane wall between two fluids, or between two known surface temperatures: each layer's conduction resistance, each film's convection resistance, the overall U-value, the heat flow and flux, and every surface and interface temperature.",
        "Resistance of each surface film: R = 1/(h·A).\nResistance of each conducting layer: R_i = t_i/(k_i·A).\nTotal resistance, hot side first: R_tot = Σ R.\nHeat flow through the wall: Q = (T_1 − T_2)/R_tot.\nHeat flux and overall U-value: q = Q/A; U = 1/(R_tot·A).\nSurface and interface temperatures, stepping down from T_1 across each resistance in turn: T_next = T_previous − Q·R.",
        [
            new CalcInputSpec("T_1", "Side 1 temperature (the fluid's, or the surface's when no film is given)", CalcInputKind.Quantity, "degC", Default: 20, Min: -60, Max: 400, HardMin: -273.15, HardMax: 5000, Group: "Side 1"),
            new CalcInputSpec("h_1", "Side 1 film coefficient (omit when T_1 is the surface temperature)", CalcInputKind.Quantity, "W/(m^2*K)", Default: 7.7, Min: 2, Max: 1e4, HardMin: 1e-6, HardMax: 1e6, IsOptional: true, Group: "Side 1"),
            new CalcInputSpec("layers", "Conducting layers, side 1 first", CalcInputKind.Table, Columns: Columns, MaxRows: MaxLayers, Group: "Wall"),
            new CalcInputSpec("h_2", "Side 2 film coefficient (omit when T_2 is the surface temperature)", CalcInputKind.Quantity, "W/(m^2*K)", Default: 25, Min: 2, Max: 1e4, HardMin: 1e-6, HardMax: 1e6, IsOptional: true, Group: "Side 2"),
            new CalcInputSpec("T_2", "Side 2 temperature (the fluid's, or the surface's when no film is given)", CalcInputKind.Quantity, "degC", Default: 0, Min: -60, Max: 400, HardMin: -273.15, HardMax: 5000, Group: "Side 2"),
            new CalcInputSpec("A", "Wall area normal to the heat flow", CalcInputKind.Quantity, "m^2", Default: 1, Min: 1e-3, Max: 1e4, HardMin: 1e-9, HardMax: 1e7, Group: "Wall"),
        ],
        [
            new CalcAssumption("Steady, one-dimensional conduction through plane layers of constant conductivity, with no internal heat generation.", "Thermal-circuit idealisation: equal layer areas, no edge losses."),
            new CalcAssumption("Layers are in perfect thermal contact; each film coefficient is constant over its surface.", "A contact resistance can be entered as a thin layer of equivalent thickness and conductivity."),
        ],
        [
            new CalcClauseRef("Incropera & DeWitt, Fundamentals of Heat and Mass Transfer", "§3.1", "The plane wall: thermal resistance and the composite wall"),
            new CalcClauseRef("Çengel & Ghajar, Heat and Mass Transfer", "§3-3", "Generalized thermal resistance networks (multilayer plane walls)"),
            new CalcClauseRef("BS EN ISO 6946", "§6", "Thermal resistance of building components (method)"),
        ],
        [
            new CalcApplicabilityRule("no-layers", "At least one conducting layer is needed.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("reversed", "Side 2 is warmer than side 1: the heat flows from side 2 to side 1 and Q is negative.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-legacy-single-pane", "Legacy example 1: single-pane window", Legacy(1, ", after Çengel & Ghajar"), Window(Layers(("glass", 8, 0.78))),
                [new CalcExpectedValue("R_h1", 0.083333, "K/W", 5e-5), new CalcExpectedValue("R_1", 0.0085470, "K/W", 5e-5), new CalcExpectedValue("R_h2", 0.020833, "K/W", 5e-5), new CalcExpectedValue("R_tot", 0.11271, "K/W", 5e-5), new CalcExpectedValue("Q", 266.16, "W", 5e-5), new CalcExpectedValue("q", 221.80, "W/m^2", 5e-5), new CalcExpectedValue("U", 7.3934, "W/(m^2*K)", 5e-5), new CalcExpectedValue("T_s0", -2.1801, "degC", 5e-5), new CalcExpectedValue("T_s1", -4.4550, "degC", 5e-5)]),
            new CalcTestCase("ka-legacy-double-pane", "Legacy example 2: double-pane window with a 10 mm air gap", Legacy(2, ", after Çengel & Ghajar"), Window(Layers(("glass", 4, 0.78), ("air gap", 10, 0.026), ("glass", 4, 0.78))),
                [new CalcExpectedValue("R_2", 0.32051, "K/W", 5e-5), new CalcExpectedValue("R_tot", 0.43323, "K/W", 5e-5), new CalcExpectedValue("Q", 69.248, "W", 5e-5), new CalcExpectedValue("q", 57.707, "W/m^2", 5e-5), new CalcExpectedValue("U", 1.9236, "W/(m^2*K)", 5e-5), new CalcExpectedValue("T_s0", 14.229, "degC", 5e-5), new CalcExpectedValue("T_s1", 13.933, "degC", 5e-5), new CalcExpectedValue("T_s2", -8.2614, "degC", 5e-5), new CalcExpectedValue("T_s3", -8.5573, "degC", 5e-5)]),
            new CalcTestCase("ka-legacy-surfaces", "Legacy example 3: surface temperatures given, no films", Legacy(3),
                [CalcInputValue.Quantity("T_1", 25, "degC"), Layers(("brick", 200, 0.72), ("insulation", 50, 0.04)), CalcInputValue.Quantity("T_2", 5, "degC"), CalcInputValue.Quantity("A", 3, "m^2")],
                [new CalcExpectedValue("R_1", 0.092593, "K/W", 5e-5), new CalcExpectedValue("R_2", 0.41667, "K/W", 5e-5), new CalcExpectedValue("R_tot", 0.50926, "K/W", 5e-5), new CalcExpectedValue("Q", 39.273, "W", 5e-5), new CalcExpectedValue("q", 13.091, "W/m^2", 5e-5), new CalcExpectedValue("U", 0.65455, "W/(m^2*K)", 5e-5), new CalcExpectedValue("T_s0", 25, "degC", 5e-5), new CalcExpectedValue("T_s1", 21.364, "degC", 5e-5), new CalcExpectedValue("T_s2", 5, "degC", 5e-5)]),
        ],
        Category: "Thermal");

    public void Run(CalcRunContext c)
    {
        c.Input("T_1");
        var film1 = c.Has("h_1");

        if (film1)
            c.Input("h_1");

        var rows = c.Rows("layers");
        var film2 = c.Has("h_2");

        if (film2)
            c.Input("h_2");

        c.Input("T_2");
        c.Input("A");

        if (rows.Count == 0)
        {
            c.Block("no-layers", "layers: at least one conducting layer is needed.");
            return;
        }

        var area = c.Si("A");
        var kpw = UnitCatalog.Parse("K/W");
        var degC = UnitCatalog.Parse("degC");
        string F(double v) => CalcValue.FormatNumber(v, 5);
        var circuit = new List<(string Symbol, double R)>(rows.Count + 2);

        if (film1)
        {
            var h = c.Si("h_1");
            var r = 1 / (h * area);
            c.Record("R_h1", "1 / (h_1 · A)", $"1 / ({F(h)} W/(m²·K) × {F(area)} m²)", ScalarValue.Of(r, kpw), "Side 1 film resistance", "Incropera §3.1");
            circuit.Add(("R_h1", r));
        }

        for (var i = 0; i < rows.Count; i++)
        {
            c.CancellationToken.ThrowIfCancellationRequested();
            var n = i + 1;
            var name = rows[i].Get("name")?.Text is { Length: > 0 } text ? text : $"layer {n}";
            var tCell = rows[i].Get("t") ?? throw new CalcEvaluationException($"layers row {n}: the thickness is missing.");
            var kCell = rows[i].Get("k") ?? throw new CalcEvaluationException($"layers row {n}: the conductivity is missing.");
            var t = UnitCatalog.Parse(tCell.Unit ?? "mm").ToSi(tCell.Number ?? throw new CalcEvaluationException($"layers row {n}: the thickness needs a number."));
            var k = UnitCatalog.Parse(kCell.Unit ?? "W/(m*K)").ToSi(kCell.Number ?? throw new CalcEvaluationException($"layers row {n}: the conductivity needs a number."));
            var r = t / (k * area);
            c.Record($"R_{n}", $"t_{n} / (k_{n} · A)", $"{F(t * 1000)} mm / ({F(k)} W/(m·K) × {F(area)} m²)", ScalarValue.Of(r, kpw), $"Conduction resistance of {name}", "Incropera §3.1");
            circuit.Add(($"R_{n}", r));
        }

        if (film2)
        {
            var h = c.Si("h_2");
            var r = 1 / (h * area);
            c.Record("R_h2", "1 / (h_2 · A)", $"1 / ({F(h)} W/(m²·K) × {F(area)} m²)", ScalarValue.Of(r, kpw), "Side 2 film resistance", "Incropera §3.1");
            circuit.Add(("R_h2", r));
        }

        var total = circuit.Sum(x => x.R);
        c.Record("R_tot", "Σ R", string.Join(" + ", circuit.Select(x => F(x.R))) + " K/W", ScalarValue.Of(total, kpw), "Total thermal resistance", "Çengel §3-3");
        c.Assign("Q", "(T_1 - T_2) / R_tot", "W", "Heat flow from side 1 to side 2");
        c.Assign("q", "Q / A", "W/m^2", "Heat flux");
        c.Assign("U", "1 / (R_tot * A)", "W/(m^2*K)", "Overall heat transfer coefficient");

        var q = c.Si("Q");

        if (q < 0)
            c.Warn("reversed", "T_2: side 2 is warmer than side 1, so the heat flows from side 2 to side 1 and Q is negative.");

        // Walk the circuit from side 1: a film's far side is a surface, a layer's an interface
        // (or the side 2 surface after the last layer). The side 2 fluid is not a surface.
        var node = c.Si("T_1");
        var surface = 0;

        if (!film1)
            RecordSurface(surface++, "T_1", "Side 1 surface (given)");

        for (var i = 0; i < circuit.Count; i++)
        {
            var (symbol, r) = circuit[i];
            node -= q * r;

            if (film2 && i == circuit.Count - 1)
                break;

            var previous = surface == 0 ? "T_1" : $"T_s{surface - 1}";
            var what = surface == 0 ? "Side 1 surface" : surface == rows.Count ? "Side 2 surface" : $"Interface {surface}";
            c.Record($"T_s{surface}", $"{previous} − Q · {symbol}", $"{F(degC.FromSi(node + q * r))} °C − {F(q)} W × {F(r)} K/W", new ScalarValue(node, degC.Dimension, degC), what);
            surface++;
        }

        void RecordSurface(int index, string from, string what) =>
            c.Record($"T_s{index}", from, $"{F(degC.FromSi(node))} °C", new ScalarValue(node, degC.Dimension, degC), what);

        c.Output("Q", "Heat flow");
        c.Output("q", "Heat flux");
        c.Output("U", "Overall heat transfer coefficient");
        c.Output("R_tot", "Total thermal resistance");

        foreach (var (symbol, _) in circuit)
            c.Output(symbol, symbol switch { "R_h1" => "Side 1 film resistance", "R_h2" => "Side 2 film resistance", _ => $"Layer {symbol[2..]} resistance" });

        for (var s = 0; s < surface; s++)
            c.Output($"T_s{s}", s == 0 ? "Side 1 surface temperature" : s == rows.Count ? "Side 2 surface temperature" : $"Interface {s} temperature");
    }
}
