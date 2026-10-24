using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Electrical;

/// <summary>E6 — power and load budget: demand per bus, DC power supply, transformer with an inrush check, battery capacity, and the heat dissipated (→ E2).</summary>
public sealed class LoadBudgetCalculator : ICalcCalculator
{
    public const string Id = "elec.load-budget";
    public const string Ac3 = "AC three-phase";
    public const string Ac1 = "AC single-phase";
    public const string Dc = "DC";

    private static readonly IReadOnlyList<CalcColumnSpec> LoadColumns =
    [
        new("name", "Load", CalcInputKind.Text),
        new("supply", "Supply", CalcInputKind.Choice, Choices: [Ac3, Ac1, Dc]),
        new("P", "Electrical input power", CalcInputKind.Quantity, "kW", HardMin: 0, HardMax: 200),
        new("cosphi", "Power factor", CalcInputKind.Number, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, IsOptional: true),
        new("eta", "Efficiency", CalcInputKind.Number, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, IsOptional: true),
        new("d", "Duty (share of time on)", CalcInputKind.Number, HardMin: 0, HardMax: 1, IsOptional: true),
        new("k", "Diversity", CalcInputKind.Number, HardMin: 0, HardMax: 1, IsOptional: true),
        new("inrush", "Inrush multiple", CalcInputKind.Number, HardMin: 1, HardMax: 20, IsOptional: true),
        new("backed", "Battery-backed", CalcInputKind.Boolean, IsOptional: true),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Electrical, "Load budget, PSU, transformer and battery",
        "The demand of a set of loads per supply (three-phase AC, single-phase AC, DC) and the minimum DC power supply, transformer and battery for it, each with a margin; the heat the loads dissipate for the enclosure calculation.",
        "Demand per load: P_i·d_i·k_i; apparent power P_i·d_i·k_i/cosφ_i (arithmetic sum, conservative)\n" +
        "Bus currents: I_3φ = S_3φ/(√3·U), I_1φ = S_1φ/U, I_DC = P_DC/U_DC\n" +
        "Heat dissipated: Φ = Σ(1 − η_i)·P_i·d_i·k_i\n" +
        "PSU: P_PSU ≥ margin·P_DC/f_derate (f_derate from the manufacturer's curve at θ and altitude)\n" +
        "Transformer: S_T ≥ margin·S; inrush S_peak = S + max((m_i − 1)·S_i) ≤ k_T·S_T\n" +
        "Battery: C = I_b·t/(DoD·η_b·k_temp·k_age), I_b = Σ backed P_i·d_i·k_i/(U_b·η_conv); Peukert: I_b·H·(t/H)^(1/k_p) in place of I_b·t",
        [
            new CalcInputSpec("loads", "Loads", CalcInputKind.Table, Columns: LoadColumns, MaxRows: 100),
            new CalcInputSpec("U_3ph", "Three-phase line voltage", CalcInputKind.Quantity, "V", Default: 400, Min: 12, Max: 1000, HardMin: 12, HardMax: 1000, Group: "Supplies"),
            new CalcInputSpec("U_1ph", "Single-phase voltage", CalcInputKind.Quantity, "V", Default: 230, Min: 12, Max: 1000, HardMin: 12, HardMax: 1000, Group: "Supplies"),
            new CalcInputSpec("U_DC", "DC bus voltage", CalcInputKind.Quantity, "V", Default: 24, Min: 12, Max: 1000, HardMin: 12, HardMax: 1000, Group: "Supplies"),
            new CalcInputSpec("margin", "Sizing margin", CalcInputKind.Number, Default: 1.25, Min: 1, Max: 1.5, HardMin: 1, HardMax: 1.5, Group: "Supplies"),
            new CalcInputSpec("theta", "Ambient temperature", CalcInputKind.Quantity, "degC", Default: 40, Min: -20, Max: 70, HardMin: -20, HardMax: 70, Group: "PSU"),
            new CalcInputSpec("f_derate", "PSU derating factor at the ambient and altitude", CalcInputKind.Number, Default: 1, Min: 0.1, Max: 1, HardMin: 0.05, HardMax: 1, Group: "PSU"),
            new CalcInputSpec("P_PSU", "Selected PSU rating", CalcInputKind.Quantity, "W", Min: 1, Max: 1e5, HardMin: 1e-3, HardMax: 1e6, IsOptional: true, Group: "PSU"),
            new CalcInputSpec("S_T", "Selected transformer rating", CalcInputKind.Quantity, "kVA", Min: 0.01, Max: 1e4, HardMin: 1e-3, HardMax: 1e5, IsOptional: true, Group: "Transformer"),
            new CalcInputSpec("k_T", "Transformer short-time overload allowed for inrush", CalcInputKind.Number, Default: 3, Min: 1, Max: 20, HardMin: 1, HardMax: 50, Group: "Transformer"),
            new CalcInputSpec("t_auto", "Battery autonomy", CalcInputKind.Quantity, "h", Default: 0, Min: 0, Max: 72, HardMin: 0, HardMax: 72, Group: "Battery"),
            new CalcInputSpec("U_b", "Battery nominal voltage", CalcInputKind.Quantity, "V", Default: 24, Min: 2, Max: 1000, HardMin: 1, HardMax: 2000, Group: "Battery"),
            new CalcInputSpec("eta_conv", "Conversion efficiency battery → loads", CalcInputKind.Number, Default: 1, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Battery"),
            new CalcInputSpec("DoD", "Allowed depth of discharge", CalcInputKind.Number, Default: 0.8, Min: 0.1, Max: 1, HardMin: 0.05, HardMax: 1, Group: "Battery"),
            new CalcInputSpec("eta_b", "Battery discharge efficiency", CalcInputKind.Number, Default: 0.95, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Battery"),
            new CalcInputSpec("k_temp", "Temperature capacity factor", CalcInputKind.Number, Default: 1, Min: 0.3, Max: 1.2, HardMin: 0.1, HardMax: 1.5, Group: "Battery"),
            new CalcInputSpec("k_age", "Ageing (end-of-life) factor", CalcInputKind.Number, Default: 0.8, Min: 0.5, Max: 1, HardMin: 0.3, HardMax: 1, Group: "Battery"),
            new CalcInputSpec("k_p", "Peukert exponent", CalcInputKind.Number, Min: 1, Max: 1.5, HardMin: 1, HardMax: 2, IsOptional: true, Group: "Battery"),
            new CalcInputSpec("H_rate", "Hour rate of the rated capacity (Peukert)", CalcInputKind.Quantity, "h", Min: 1, Max: 100, HardMin: 0.1, HardMax: 1000, IsOptional: true, Group: "Battery"),
            new CalcInputSpec("C_batt", "Selected battery capacity", CalcInputKind.Quantity, "A*h", Min: 0.1, Max: 1e5, HardMin: 1e-3, HardMax: 1e6, IsOptional: true, Group: "Battery"),
        ],
        [
            new CalcAssumption("Each load's P is its electrical input; DC loads count at power factor 1 in the transformer demand (the PSU's own loss is a load row if wanted).", "Load budget convention."),
            new CalcAssumption("Apparent powers are added arithmetically, which is conservative when power factors differ.", "Upper bound on S."),
            new CalcAssumption("PSU derating, transformer overload and battery factors are the manufacturer's values, entered as records.", "§A3 reference records."),
        ],
        [
            new CalcClauseRef("Linden's Handbook of Batteries", "5th ed., Ch. 3–4", "Battery sizing and the Peukert relation"),
            new CalcClauseRef("BS EN 60204-1:2018", "cl. 4.3", "Electrical supply (supply voltage tolerance; cited by number only)"),
        ],
        [
            new CalcApplicabilityRule("no-loads", "No loads: nothing to budget.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("derate", "Ambient above 40 °C with no PSU derating entered.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("peukert", "Peukert needs both the exponent and the hour rate.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-mixed-buses", "Pump, fan, lighting and controls on three buses",
                new CalcReferenceSource("Analytic (load budget arithmetic)", null, "P_dem = 7.5 + 2.2·0.5 + 0.5·0.8 + 0.12 = 9.12 kW; S = 7.5/0.85 + 1.1/0.8 + 0.4/0.95 + 0.12 = 10.7396 kVA; I_3φ = 10.1985 kVA/(√3·400 V) = 14.720 A; Φ = 0.927 kW; PSU ≥ 1.25·120 = 150 W; S_peak = 10.7396 + 5·8.8235 = 54.857 kVA; battery 5 A·4 h/(0.8·0.95·0.8) = 32.895 A·h"),
                [
                    Loads(("Pump motor", Ac3, 7.5, 0.85, 0.9, 1, 1, 6, false), ("Supply fan", Ac3, 2.2, 0.8, 0.85, 0.5, 1, 6, false), ("Lighting", Ac1, 0.5, 0.95, 1, 1, 0.8, 1, false), ("Controls", Dc, 0.12, 1, 0.9, 1, 1, 1, true)),
                    CalcInputValue.Quantity("t_auto", 4, "h"),
                ],
                [new CalcExpectedValue("P_dem", 9.12, "kW", 1e-9), new CalcExpectedValue("S_dem", 10.73958, "kVA", 1e-6), new CalcExpectedValue("I_3ph", 14.72031, "A", 1e-6), new CalcExpectedValue("I_1ph", 1.830664, "A", 1e-6), new CalcExpectedValue("I_DC", 5, "A", 1e-9), new CalcExpectedValue("Phi", 0.927, "kW", 1e-9), new CalcExpectedValue("P_PSU_req", 150, "W", 1e-9), new CalcExpectedValue("S_T_req", 13.42448, "kVA", 1e-6), new CalcExpectedValue("S_peak", 54.85723, "kVA", 1e-6), new CalcExpectedValue("C_req", 32.89474, "A*h", 1e-6)]),
            new CalcTestCase("ka-battery", "24 V DC battery for controls and comms, all derates",
                new CalcReferenceSource("Analytic (capacity with derates)", null, "I_b = 120/24 + 48/24 = 7 A; 7 A × 4 h = 28 A·h; C = 28/(0.8·0.95·0.9·0.8) = 51.170 A·h"),
                [Loads(("Controls", Dc, 0.12, 1, 1, 1, 1, 1, true), ("Comms", Dc, 0.048, 1, 1, 1, 1, 1, true)), CalcInputValue.Quantity("t_auto", 4, "h"), CalcInputValue.Of("k_temp", 0.9), CalcInputValue.Quantity("C_batt", 55, "A*h")],
                [new CalcExpectedValue("I_b", 7, "A", 1e-9), new CalcExpectedValue("C_req", 51.16959, "A*h", 1e-6), new CalcExpectedValue("u_batt", 0.9303562, "", 1e-6)]),
            new CalcTestCase("ka-peukert", "Peukert-adjusted capacity (Linden's Handbook relation)",
                new CalcReferenceSource("Linden's Handbook of Batteries", "5th ed.", "Peukert relation t = H·(C/(I·H))^k as given there; numbers analytic: 5 A for 4 h on a 20 h rated battery, k = 1.2: 5·20·0.2^(1/1.2) = 26.153 A·h; /0.608 = 43.015 A·h. Relation form to be checked against the owner's copy"),
                [Loads(("Controls", Dc, 0.12, 1, 1, 1, 1, 1, true)), CalcInputValue.Quantity("t_auto", 4, "h"), CalcInputValue.Of("k_p", 1.2), CalcInputValue.Quantity("H_rate", 20, "h")],
                [new CalcExpectedValue("Ah", 26.15321, "A*h", 1e-6), new CalcExpectedValue("C_req", 43.01515, "A*h", 1e-6)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-psu-derating", "24 V PSU at 70 °C with the datasheet derating (manufacturer pattern)",
                new CalcReferenceSource("Phoenix Contact QUINT4-PS/1AC/24DC/10 datasheet", "current", "Derating above +60 °C read from the datasheet curve as f = 0.75 at 70 °C (value to be confirmed against the datasheet); 200 W DC load: P_PSU ≥ 1.25·200/0.75 = 333.33 W, so a 240 W unit fails"),
                [Loads(("PLC and I/O", Dc, 0.2, 1, 1, 1, 1, 1, false)), CalcInputValue.Quantity("theta", 70, "degC"), CalcInputValue.Of("f_derate", 0.75), CalcInputValue.Quantity("P_PSU", 240, "W")],
                [new CalcExpectedValue("P_PSU_req", 333.3333, "W", 1e-6), new CalcExpectedValue("u_PSU", 1.388889, "", 1e-6)],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Power",
        OutputPorts:
        [
            new CalcOutputPort("P_dem", "kW", "Demand active power"),
            new CalcOutputPort("S_dem", "kVA", "Demand apparent power"),
            new CalcOutputPort("I_3ph", "A", "Three-phase bus current (→ E4 I_b)"),
            new CalcOutputPort("I_1ph", "A", "Single-phase bus current"),
            new CalcOutputPort("I_DC", "A", "DC bus current"),
            new CalcOutputPort("Phi", "kW", "Heat dissipated (→ E2)"),
            new CalcOutputPort("C_req", "A*h", "Minimum battery capacity"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("psu.derating", "PSU derating", ["f_derate"], "From the PSU manufacturer's derating curve at the ambient and altitude."),
            new CalcRecordKind("battery.factors", "Battery factors", ["DoD", "eta_b", "k_temp", "k_age", "k_p", "H_rate"], "From the battery manufacturer's data for the chemistry and the temperature."),
        ]);

    /// <summary>A loads table from (name, supply, P kW, cosφ, η, d, k, inrush, backed) rows.</summary>
    public static CalcInputValue Loads(params (string Name, string Supply, double P, double CosPhi, double Eta, double D, double K, double Inrush, bool Backed)[] rows) =>
        CalcInputValue.Table("loads", rows.Select(r => CalcRow.Of(
            ("name", new CalcCell(Text: r.Name)), ("supply", new CalcCell(Text: r.Supply)), ("P", new CalcCell(r.P, "kW")), ("cosphi", new CalcCell(r.CosPhi)), ("eta", new CalcCell(r.Eta)),
            ("d", new CalcCell(r.D)), ("k", new CalcCell(r.K)), ("inrush", new CalcCell(r.Inrush)), ("backed", new CalcCell(Flag: r.Backed)))).ToList());

    private sealed record Load(int Index, string Name, string Supply, double P, double CosPhi, double Eta, double D, double K, double Inrush, bool Backed)
    {
        public double Demand => P * D * K;

        public double Apparent => Supply == Dc ? Demand : Demand / CosPhi;
    }

    public void Run(CalcRunContext c)
    {
        var rows = c.Rows("loads");

        foreach (var s in new[] { "U_3ph", "U_1ph", "U_DC", "margin", "theta", "f_derate", "k_T", "t_auto", "U_b", "eta_conv", "DoD", "eta_b", "k_temp", "k_age" })
            c.Input(s);

        if (rows.Count == 0)
        {
            c.Block("no-loads", "loads: enter at least one load.");
            return;
        }

        var loads = rows.Select((r, i) => new Load(
            i + 1,
            r.Get("name")?.Text is { Length: > 0 } n ? CalcSheetTextRenderer.Clean(n) : $"Load {i + 1}",
            r.Get("supply")?.Text ?? Ac3,
            r.Get("P") is { Number: { } p } cell ? UnitCatalog.Parse(cell.Unit ?? "kW").ToSi(p) : 0,
            r.Get("cosphi")?.Number ?? 1, r.Get("eta")?.Number ?? 1, r.Get("d")?.Number ?? 1, r.Get("k")?.Number ?? 1, r.Get("inrush")?.Number ?? 1, r.Get("backed")?.Flag ?? false)).ToList();

        var kW = UnitCatalog.Parse("kW");
        var kVA = UnitCatalog.Parse("kVA");
        var amp = UnitCatalog.Parse("A");
        var ah = UnitCatalog.Parse("A*h");

        foreach (var l in loads)
            c.Record($"L{l.Index}", "P·d·k; P·d·k/cosφ", $"{F(l.P / 1e3)} × {F(l.D)} × {F(l.K)} = {F(l.Demand / 1e3)} kW; {F(l.Apparent / 1e3)} kVA", ScalarValue.Of(l.Demand / 1e3, kW), $"{l.Name} ({l.Supply})");

        c.Record("P_conn", "Σ P_i", string.Join(" + ", loads.Select(l => F(l.P / 1e3))), ScalarValue.Of(loads.Sum(l => l.P) / 1e3, kW), "Connected load");
        c.Record("P_dem", "Σ P_i·d_i·k_i", string.Join(" + ", loads.Select(l => F(l.Demand / 1e3))), ScalarValue.Of(loads.Sum(l => l.Demand) / 1e3, kW), "Demand active power");
        var sDem = loads.Sum(l => l.Apparent);
        c.Record("S_dem", "Σ P_i·d_i·k_i/cosφ_i", string.Join(" + ", loads.Select(l => F(l.Apparent / 1e3))), ScalarValue.Of(sDem / 1e3, kVA), "Demand apparent power (arithmetic)");

        var s3 = loads.Where(l => l.Supply == Ac3).Sum(l => l.Apparent);
        var s1 = loads.Where(l => l.Supply == Ac1).Sum(l => l.Apparent);
        var pdc = loads.Where(l => l.Supply == Dc).Sum(l => l.Demand);
        c.Record("I_3ph", "S_3φ/(√3·U_3ph)", $"{F(s3)} / (√3 × {F(c.Si("U_3ph"))})", ScalarValue.Of(s3 / (Math.Sqrt(3) * c.Si("U_3ph")), amp), "Three-phase bus current");
        c.Record("I_1ph", "S_1φ/U_1ph", $"{F(s1)} / {F(c.Si("U_1ph"))}", ScalarValue.Of(s1 / c.Si("U_1ph"), amp), "Single-phase bus current");
        c.Record("I_DC", "P_DC/U_DC", $"{F(pdc)} / {F(c.Si("U_DC"))}", ScalarValue.Of(pdc / c.Si("U_DC"), amp), "DC bus current");
        c.Record("Phi", "Σ (1 − η_i)·P_i·d_i·k_i", string.Join(" + ", loads.Select(l => $"{F(1 - l.Eta)}×{F(l.Demand / 1e3)}")), ScalarValue.Of(loads.Sum(l => (1 - l.Eta) * l.Demand) / 1e3, kW), "Heat dissipated (→ E2)");

        if (c.Si("theta") > 273.15 + 40 && c.InputSi("f_derate") >= 1)
            c.Warn("derate", "f_derate: the ambient is above 40 °C but no PSU derating is entered; read the factor from the manufacturer's curve.");

        c.Record("P_DC", "Σ DC P_i·d_i·k_i", $"{F(pdc)} W", ScalarValue.Of(pdc, "W"), "DC demand");
        c.Assign("P_PSU_req", "margin * P_DC / f_derate", "W", "Minimum PSU rating");
        c.Assign("S_T_req", "margin * S_dem", "kVA", "Minimum transformer rating");
        var surge = loads.Where(l => l.Supply != Dc).Select(l => (l.Inrush - 1) * l.Apparent).DefaultIfEmpty(0).Max();
        var surgeLoad = loads.Where(l => l.Supply != Dc).OrderByDescending(l => (l.Inrush - 1) * l.Apparent).FirstOrDefault();
        c.Record("S_peak", "S_dem + max((m_i − 1)·S_i)", $"{F(sDem / 1e3)} + {F(surge / 1e3)}{(surgeLoad is null ? string.Empty : $" ({surgeLoad.Name} starting)")}", ScalarValue.Of((sDem + surge) / 1e3, kVA), "Peak apparent power while the largest load starts");

        var outputs = new List<(string, string)> { ("P_conn", "Connected load"), ("P_dem", "Demand active power"), ("S_dem", "Demand apparent power"), ("I_3ph", "Three-phase bus current"), ("I_1ph", "Single-phase bus current"), ("I_DC", "DC bus current"), ("Phi", "Heat dissipated"), ("P_PSU_req", "Minimum PSU rating"), ("S_T_req", "Minimum transformer rating"), ("S_peak", "Peak apparent power at start") };

        if (c.Si("t_auto") > 0)
        {
            var ib = loads.Where(l => l.Backed).Sum(l => l.Demand) / (c.Si("U_b") * c.InputSi("eta_conv"));
            c.Record("I_b", "Σ backed P_i·d_i·k_i/(U_b·η_conv)", $"{F(loads.Where(l => l.Backed).Sum(l => l.Demand))} / ({F(c.Si("U_b"))} × {F(c.InputSi("eta_conv"))})", ScalarValue.Of(ib, amp), "Battery discharge current");

            if (c.Has("k_p") != c.Has("H_rate"))
                c.Warn("peukert", "k_p/H_rate: Peukert needs both the exponent and the hour rate; it is not applied.");

            if (c.Has("k_p") && c.Has("H_rate"))
            {
                c.Input("k_p");
                c.Input("H_rate");
                c.Assign("Ah", "I_b * H_rate * (t_auto / H_rate)^(1 / k_p)", "A*h", "Discharge capacity, Peukert-adjusted", "Linden 5th ed.");
            }
            else
            {
                c.Assign("Ah", "I_b * t_auto", "A*h", "Discharge capacity");
            }

            c.Assign("C_req", "Ah / (DoD * eta_b * k_temp * k_age)", "A*h", "Minimum battery capacity");
            outputs.AddRange([("I_b", "Battery discharge current"), ("Ah", "Discharge capacity"), ("C_req", "Minimum battery capacity")]);
        }

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        if (c.Has("P_PSU"))
        {
            c.Input("P_PSU");
            c.Assign("u_PSU", "P_PSU_req / P_PSU", null, "PSU utilisation");
            c.Output("u_PSU", "PSU utilisation");
            c.CheckUtilisation("PSU rating against required", "u_PSU");
        }

        if (c.Has("S_T"))
        {
            c.Input("S_T");
            c.Assign("u_T", "S_T_req / S_T", null, "Transformer utilisation");
            c.Assign("u_inrush", "S_peak / (k_T * S_T)", null, "Inrush utilisation");
            c.Output("u_T", "Transformer utilisation");
            c.Output("u_inrush", "Inrush utilisation");
            c.CheckUtilisation("Transformer rating against required", "u_T");
            c.CheckUtilisation("Starting peak against transformer overload", "u_inrush");
        }

        if (c.Has("C_batt") && c.Si("t_auto") > 0)
        {
            c.Input("C_batt");
            c.Assign("u_batt", "C_req / C_batt", null, "Battery utilisation");
            c.Output("u_batt", "Battery utilisation");
            c.CheckUtilisation("Battery capacity against required", "u_batt");
        }
    }

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
