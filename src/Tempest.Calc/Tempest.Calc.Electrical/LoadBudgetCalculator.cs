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

    public const string PsuNone = "not counted on an AC bus";

    private static readonly IReadOnlyList<CalcColumnSpec> LoadColumns =
    [
        new("name", "Load", CalcInputKind.Text),
        new("supply", "Supply", CalcInputKind.Choice, Choices: [Ac3, Ac1, Dc]),
        new("P", "Electrical input power", CalcInputKind.Quantity, "kW", HardMin: 0, HardMax: 200),
        new("cosphi", "Power factor", CalcInputKind.Number, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, IsOptional: true),
        new("eta", "Efficiency", CalcInputKind.Number, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, IsOptional: true),
        new("heat", "Share of the input power released as heat inside the enclosure (blank: the loss 1 − η; electronics 1; a machine outside the enclosure 0)", CalcInputKind.Number, Min: 0, Max: 1, HardMin: 0, HardMax: 1, IsOptional: true),
        new("d", "Duty (share of time on)", CalcInputKind.Number, HardMin: 0, HardMax: 1, IsOptional: true),
        new("k", "Diversity", CalcInputKind.Number, HardMin: 0, HardMax: 1, IsOptional: true),
        new("inrush", "Inrush multiple", CalcInputKind.Number, HardMin: 1, HardMax: 20, IsOptional: true),
        new("backed", "Battery-backed", CalcInputKind.Boolean, IsOptional: true),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.4.0", CalcDiscipline.Electrical, "Load budget, PSU, transformer and battery",
        "The demand of a set of loads per supply (three-phase AC, single-phase AC, DC) and the minimum DC power supply, transformer and battery for it, each with a margin; the heat the loads dissipate for the enclosure calculation.",
        "Demand per load: P_i·d_i·k_i; apparent power P_i·d_i·k_i/cosφ_i (arithmetic sum, conservative); this duty-weighted demand sizes the transformer and the enclosure heat\n" +
        "Running load of each load while it is on: P_i·k_i, apparent P_i·k_i/cosφ_i (the duty d is the share of time on, so it is left out: a cable, a bus and a power supply carry the load while it is on)\n" +
        "Bus currents: I_3φ = S_3φ,run/(√3·U), I_1φ = S_1φ,run/U, I_DC = P_DC,run/U_DC, from the running loads; when a bus is chosen for the DC power supply its input P_DC,run/η_PSU (power factor 1) is added to that bus\n" +
        "Heat dissipated inside the enclosure: Φ = Σ h_i·P_i·d_i·k_i + P_DC·(1/η_PSU − 1), h_i = the share of the input power released inside (blank: the loss 1 − η_i; electronics 1; a machine outside the enclosure 0); the second term is the power supply's own loss, present when an AC bus is chosen for it\n" +
        "Power supply sizing: P_PSU ≥ margin·P_DC,run/f_derate (the running DC load, duty not applied; f_derate from the manufacturer's curve at θ and altitude)\n" +
        "Transformer sizing: S_T ≥ margin·S for the running load; starting peak S_peak = S + max(m_i·P_i/cosφ_i − S_i) ≤ k_T·S_T (the starting load undiversified), so S_T ≥ S_peak/k_T for the start; the rating to buy is the larger of the two\n" +
        "Battery sizing: C = I_b·t/(DoD·η_b·k_temp·k_age), I_b = Σ backed P_i·d_i·k_i/(U_b·η_conv); Peukert: I_b·H·(t/H)^(1/k_p) in place of I_b·t, never below I_b·t (the exponent is not valid beyond the rated hour rate H)",
        [
            new CalcInputSpec("loads", "Loads", CalcInputKind.Table, Columns: LoadColumns, MaxRows: 100),
            new CalcInputSpec("P_add", "Additional three-phase load (linked from E1's RMS power)", CalcInputKind.Quantity, "kW", Default: 0, Min: 0, Max: 200, HardMin: 0, HardMax: 200, Group: "Loads"),
            new CalcInputSpec("P_rated_add", "Rated (nameplate) shaft power of the additional machine (linked from E1's P_rated): the supply carries the larger of this and the duty power P_add; blank: the machine is sized on its duty only", CalcInputKind.Quantity, "kW", Min: 0, Max: 200, HardMin: 0, HardMax: 200, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("cosphi_add", "Power factor of the additional load", CalcInputKind.Number, Default: 0.85, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Loads"),
            new CalcInputSpec("eta_add", "Efficiency of the additional load", CalcInputKind.Number, Default: 0.9, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Loads"),
            new CalcInputSpec("inrush_add", "Starting multiple of the additional load", CalcInputKind.Number, Default: 1, Min: 1, Max: 20, HardMin: 1, HardMax: 20, Group: "Loads"),
            new CalcInputSpec("heat_add", "Share of the additional load's input power released inside the enclosure (a linked machine is outside it: 0)", CalcInputKind.Number, Default: 0, Min: 0, Max: 1, HardMin: 0, HardMax: 1, Group: "Loads"),
            new CalcInputSpec("U_3ph", "Three-phase line voltage", CalcInputKind.Quantity, "V", Default: 400, Min: 12, Max: 1000, HardMin: 12, HardMax: 1000, Group: "Supplies"),
            new CalcInputSpec("U_1ph", "Single-phase voltage", CalcInputKind.Quantity, "V", Default: 230, Min: 12, Max: 1000, HardMin: 12, HardMax: 1000, Group: "Supplies"),
            new CalcInputSpec("U_DC", "DC bus voltage", CalcInputKind.Quantity, "V", Default: 24, Min: 12, Max: 1000, HardMin: 12, HardMax: 1000, Group: "Supplies"),
            new CalcInputSpec("psu_bus", "AC bus that feeds the DC power supply (its input P_DC/η_PSU is added to that bus)", CalcInputKind.Choice, Choices: [PsuNone, Ac1, Ac3], DefaultChoice: PsuNone, Group: "Supplies"),
            new CalcInputSpec("eta_psu", "DC power supply efficiency (power factor taken as 1)", CalcInputKind.Number, Default: 0.9, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Supplies"),
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
            new CalcAssumption("Each load's P is its electrical input; DC loads count at power factor 1 in the transformer demand; with a PSU bus chosen they count at P/η_PSU, the PSU's own loss being a load row if its heat is wanted.", "Load budget convention."),
            new CalcAssumption("The heat released inside the enclosure is h·P·d·k per load; the default h = 1 − η is the loss of a drive or supply, electronics release all their input (h = 1) and a machine outside the panel releases none (h = 0).", "Stated convention for the heat a load releases inside the panel; change h for a load that differs."),
            new CalcAssumption("Apparent powers are added arithmetically, which is conservative when power factors differ.", "Upper bound on S."),
            new CalcAssumption("PSU derating, transformer overload and battery factors are the manufacturer's values, entered as records.", "Entered as reference records, which carry their source, edition and licence."),
        ],
        [
            new CalcClauseRef("Linden's Handbook of Batteries", "5th ed., Ch. 3–4", "Battery sizing and the Peukert relation"),
            new CalcClauseRef("BS EN 60204-1:2018", "cl. 4.3", "Electrical supply (supply voltage tolerance; cited by number only)"),
        ],
        [
            new CalcApplicabilityRule("no-loads", "No loads: nothing to budget.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("cosphi", "An AC load row without its power factor: the current, the apparent power and the transformer demand cannot be stated (a blank is not taken as 1).", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("load-defaults", "A load row with a blank optional cell (η, d, k or inrush): the blank is taken as 1, which understates the heat (η) and the starting peak (inrush) and overstates the demand (d, k).", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("psu_bus", "DC loads with no AC bus chosen for their power supply: the AC bus currents and the transformer demand omit its input.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("derate", "Ambient above 40 °C with no PSU derating entered.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("rated-add", "A linked machine with no rated power: the supply is sized on its duty power only, and the machine's rating can be higher.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("battery-charge", "A battery-backed load with an autonomy: the power supply rating omits the battery's charging power.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("peukert-range", "An autonomy beyond the Peukert rated hour rate: the capacity is not taken below the plain product I_b·t.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("peukert", "Peukert needs both the exponent and the hour rate.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-mixed-buses", "Pump, fan, lighting and controls on three buses",
                new CalcReferenceSource("Analytic (load budget arithmetic)", null, "P_dem = 7.5 + 2.2·0.5 + 0.5·0.8 + 0.12 = 9.12 kW; S = 7.5/0.85 + 1.1/0.8 + 0.4/0.95 + 0.12 = 10.7396 kVA; I_3φ = (7.5/0.85 + 2.2/0.8) = 11.5735 kVA (running loads, duty not applied)/(√3·400 V) = 16.705 A; Φ = 0.927 kW; PSU ≥ 1.25·120 = 150 W; S_peak = 10.7396 + 5·8.8235 = 54.857 kVA; battery 5 A·4 h/(0.8·0.95·0.8) = 32.895 A·h"),
                [
                    Loads(("Pump motor", Ac3, 7.5, 0.85, 0.9, 1, 1, 6, false), ("Supply fan", Ac3, 2.2, 0.8, 0.85, 0.5, 1, 6, false), ("Lighting", Ac1, 0.5, 0.95, 1, 1, 0.8, 1, false), ("Controls", Dc, 0.12, 1, 0.9, 1, 1, 1, true)),
                    CalcInputValue.Quantity("t_auto", 4, "h"),
                ],
                [new CalcExpectedValue("P_dem", 9.12, "kW", 1e-9), new CalcExpectedValue("S_dem", 10.73958, "kVA", 1e-6), new CalcExpectedValue("I_3ph", 16.70495, "A", 1e-6), new CalcExpectedValue("I_1ph", 1.830664, "A", 1e-6), new CalcExpectedValue("I_DC", 5, "A", 1e-9), new CalcExpectedValue("Phi", 0.927, "kW", 1e-9), new CalcExpectedValue("P_PSU_req", 150, "W", 1e-9), new CalcExpectedValue("S_T_req", 13.42448, "kVA", 1e-6), new CalcExpectedValue("S_peak", 54.85723, "kVA", 1e-6), new CalcExpectedValue("C_req", 32.89474, "A*h", 1e-6)]),
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
            new CalcOutputPort("I_3ph", "A", "Three-phase bus current of the loads while they are on, duty not applied (→ E4 I_b)"),
            new CalcOutputPort("I_1ph", "A", "Single-phase bus current"),
            new CalcOutputPort("I_DC", "A", "DC bus current"),
            new CalcOutputPort("Phi", "kW", "Heat dissipated inside the enclosure (→ E2)"),
            new CalcOutputPort("U_L", "V", "Three-phase line voltage of the bus (→ E3 V_n)"),
            new CalcOutputPort("pf_link", "", "Power factor of the linked machine (→ E3 cos φ)"),
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

    private sealed record Load(int Index, string Name, string Supply, double P, double CosPhi, double Eta, double D, double K, double Inrush, bool Backed, double Heat, double PsuEta)
    {
        /// <summary>The duty-weighted demand P·d·k: for the transformer, the enclosure heat and the battery energy.</summary>
        public double Demand => P * D * K;

        public double Apparent => Supply == Dc ? Demand / PsuEta : Demand / CosPhi;

        /// <summary>The load while it is on, P·k (the duty d is the share of time on and is not applied): for the bus currents and the power supply.</summary>
        public double Running => P * K;

        public double RunningApparent => Supply == Dc ? Running / PsuEta : Running / CosPhi;
    }

    public void Run(CalcRunContext c)
    {
        var rows = c.Rows("loads");

        foreach (var s in new[] { "P_add", "cosphi_add", "eta_add", "inrush_add", "heat_add", "U_3ph", "U_1ph", "U_DC", "margin", "theta", "f_derate", "k_T", "t_auto", "U_b", "eta_conv", "DoD", "eta_b", "k_temp", "k_age" })
            c.Input(s);

        if (rows.Count == 0 && !(c.Si("P_add") > 0))
        {
            c.Block("no-loads", c.EnterRows("loads").TrimEnd('.') + ", or link an additional load (P_add).");
            return;
        }

        var psuBus = c.InputText("psu_bus");
        var psuEta = psuBus == PsuNone ? 1 : c.InputSi("eta_psu");

        // A blank optional cell is read as 1 (Load below); say which, row by row (elec.load-budget-03). A blank power factor on an AC row is not read as 1.
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var name = row.Get("name")?.Text is { Length: > 0 } n ? CalcSheetTextRenderer.Clean(n) : $"Load {i + 1}";
            var isDc = (row.Get("supply")?.Text ?? Ac3) == Dc;

            if (!isDc && row.Get("cosphi")?.Number is null)
            {
                c.Block("cosphi", $"loads[{i + 1}].cosphi: enter the power factor of {name}; an AC load row without it is not taken as 1, which would understate its current and apparent power.");
                return;
            }

            var blank = new List<string>();

            if (row.Get("eta")?.Number is null && row.Get("heat")?.Number is null)
                blank.Add("η (the heat released inside the enclosure is then 0)");

            if (row.Get("d")?.Number is null)
                blank.Add("d (duty 1: always on)");

            if (row.Get("k")?.Number is null)
                blank.Add("k (diversity 1)");

            if (!isDc && row.Get("inrush")?.Number is null)
                blank.Add("inrush (starting multiple 1: the load is taken to start at its running current)");

            if (blank.Count > 0)
                c.Warn("load-defaults", $"loads[{i + 1}]: {name} has blank cells taken as 1: {string.Join("; ", blank)}. Enter them, or enter 1 where 1 is meant.");
        }

        var loads = rows.Select((r, i) => new Load(
            i + 1,
            r.Get("name")?.Text is { Length: > 0 } n ? CalcSheetTextRenderer.Clean(n) : $"Load {i + 1}",
            r.Get("supply")?.Text ?? Ac3,
            r.Get("P") is { Number: { } p } cell ? UnitCatalog.Parse(cell.Unit ?? "kW").ToSi(p) : 0,
            r.Get("cosphi")?.Number ?? 1, r.Get("eta")?.Number ?? 1, r.Get("d")?.Number ?? 1, r.Get("k")?.Number ?? 1, r.Get("inrush")?.Number ?? 1, r.Get("backed")?.Flag ?? false,
            r.Get("heat")?.Number ?? 1 - (r.Get("eta")?.Number ?? 1), psuEta)).ToList();

        // A linked machine (E1's continuous power) enters as a three-phase load drawing P_add/η_add (Addendum A §A7 motor circuit). The machine is outside the
        // enclosure, so by default none of its loss is panel heat (heat_add = 0).
        // The supply carries the machine the circuit is for, not only the duty it is asked to do (chain.motor-circuit-04): the larger of the duty power and the rated shaft power.
        var pLinked = c.Si("P_add");

        if (c.Has("P_rated_add"))
        {
            c.Input("P_rated_add");
            pLinked = Math.Max(pLinked, c.Si("P_rated_add"));
        }
        else if (pLinked > 0)
        {
            c.Warn("rated-add", "P_rated_add: the linked machine is sized on its duty power only; its rated (nameplate) power, which the drive and the supply must carry, can be higher (a larger motor on the same duty draws more current). Enter P_rated_add, or link E1's P_rated, to size the cable and the device for the machine.");
        }

        if (pLinked > 0)
            loads.Add(new Load(loads.Count + 1, "Linked load", Ac3, pLinked / c.InputSi("eta_add"), c.InputSi("cosphi_add"), c.InputSi("eta_add"), 1, 1, c.InputSi("inrush_add"), false, c.InputSi("heat_add"), psuEta));

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

        // The bus currents and the power supply are for the loads while they are on: P·k, not P·d·k (elec.load-budget-R1). The duty d is the share of
        // time on and belongs to the energy (battery), the heat and the transformer, not to what a cable, a bus or a supply must carry.
        var pdc = loads.Where(l => l.Supply == Dc).Sum(l => l.Demand);
        var pdcRun = loads.Where(l => l.Supply == Dc).Sum(l => l.Running);
        var psuInput = pdcRun / psuEta;
        var s3 = loads.Where(l => l.Supply == Ac3).Sum(l => l.RunningApparent) + (psuBus == Ac3 ? psuInput : 0);
        var s1 = loads.Where(l => l.Supply == Ac1).Sum(l => l.RunningApparent) + (psuBus == Ac1 ? psuInput : 0);
        c.Record("I_3ph", "S_3φ,run/(√3·U_3ph), S_run = Σ P_i·k_i/cosφ_i", $"{F(s3)} / (√3 × {F(c.Si("U_3ph"))})", ScalarValue.Of(s3 / (Math.Sqrt(3) * c.Si("U_3ph")), amp), "Three-phase bus current, loads while on (duty not applied)");
        c.Record("I_1ph", "S_1φ,run/U_1ph, S_run = Σ P_i·k_i/cosφ_i", $"{F(s1)} / {F(c.Si("U_1ph"))}", ScalarValue.Of(s1 / c.Si("U_1ph"), amp), "Single-phase bus current, loads while on (duty not applied)");
        c.Record("I_DC", "P_DC,run/U_DC, P_DC,run = Σ DC P_i·k_i", $"{F(pdcRun)} / {F(c.Si("U_DC"))}", ScalarValue.Of(pdcRun / c.Si("U_DC"), amp), "DC bus current, loads while on (duty not applied)");
        // The power supply's own loss is heat inside the panel too (elec.load-budget-05): P_DC·(1/η_PSU − 1), only when a bus is chosen for it (its η is then known).
        var pdcDemand = loads.Where(l => l.Supply == Dc).Sum(l => l.Demand);
        var psuLoss = psuBus == PsuNone ? 0 : pdcDemand * (1 / psuEta - 1);
        var heatTerms = string.Join(" + ", loads.Select(l => $"{F(l.Heat)}×{F(l.Demand / 1e3)}"));
        c.Record("Phi", psuBus == PsuNone ? "Σ h_i·P_i·d_i·k_i" : "Σ h_i·P_i·d_i·k_i + P_DC·(1/η_PSU − 1)", psuBus == PsuNone ? heatTerms : $"{heatTerms} + PSU loss {F(psuLoss / 1e3)}", ScalarValue.Of((loads.Sum(l => l.Heat * l.Demand) + psuLoss) / 1e3, kW), "Heat dissipated inside the enclosure (→ E2)");

        if (c.Si("theta") > 273.15 + 40 && c.InputSi("f_derate") >= 1)
            c.Warn("derate", "f_derate: the ambient is above 40 °C but no PSU derating is entered; read the factor from the manufacturer's curve.");

        if (pdc > 0 && psuBus == PsuNone)
            c.Warn("psu_bus", "psu_bus: DC loads are present but no AC bus is chosen for the power supply that feeds them, so the AC bus currents and the transformer demand omit its input (P_DC/η_PSU); choose the bus.");

        c.Record("P_DC", "Σ DC P_i·d_i·k_i", $"{F(pdc)} W", ScalarValue.Of(pdc, "W"), "DC demand (duty-weighted)");
        c.Record("P_DC_run", "Σ DC P_i·k_i", $"{F(pdcRun)} W", ScalarValue.Of(pdcRun, "W"), "DC load while on (duty not applied)");
        c.Assign("P_PSU_req", "margin * P_DC_run / f_derate", "W", "Minimum PSU rating (the supply carries the load while it is on)");
        c.Assign("S_T_req", "margin * S_dem", "kVA", "Minimum transformer rating for the running load");
        // A start draws m·P/cosφ whatever the duty or diversity; it replaces that load's diversified share (Opus review,
        // finding 6). Loads with no demand (standby at diversity 0) do not start with the rest running.
        double Increment(Load l) => l.Inrush * l.P / (l.Supply == Dc ? 1 : l.CosPhi) - l.Apparent;
        var starters = loads.Where(l => l.Supply != Dc && l.Demand > 0).ToList();
        var surge = starters.Select(Increment).DefaultIfEmpty(0).Max();
        var surgeLoad = starters.OrderByDescending(Increment).FirstOrDefault();
        c.Record("S_peak", "S_dem + max(m_i·P_i/cosφ_i − S_i)", $"{F(sDem / 1e3)} + {F(surge / 1e3)}{(surgeLoad is null ? string.Empty : $" ({surgeLoad.Name} starting)")}", ScalarValue.Of((sDem + surge) / 1e3, kVA), "Peak apparent power while the largest load starts");
        c.Assign("S_T_req_start", "S_peak / k_T", "kVA", "Minimum transformer rating for the starting peak");
        c.Assign("S_T_min", "max(S_T_req, S_T_req_start)", "kVA", "Minimum transformer rating (running load and starting peak)");

        var outputs = new List<(string, string)> { ("P_conn", "Connected load"), ("P_dem", "Demand active power"), ("S_dem", "Demand apparent power"), ("I_3ph", "Three-phase bus current"), ("I_1ph", "Single-phase bus current"), ("I_DC", "DC bus current"), ("Phi", "Heat dissipated inside the enclosure"), ("P_PSU_req", "Minimum PSU rating"), ("S_T_req", "Minimum transformer rating, running load"), ("S_peak", "Peak apparent power at start"), ("S_T_req_start", "Minimum transformer rating, starting peak"), ("S_T_min", "Minimum transformer rating, running and starting") };

        if (c.Si("t_auto") > 0)
        {
            var ib = loads.Where(l => l.Backed).Sum(l => l.Demand) / (c.Si("U_b") * c.InputSi("eta_conv"));
            c.Record("I_b", "Σ backed P_i·d_i·k_i/(U_b·η_conv)", $"{F(loads.Where(l => l.Backed).Sum(l => l.Demand))} / ({F(c.Si("U_b"))} × {F(c.InputSi("eta_conv"))})", ScalarValue.Of(ib, amp), "Battery discharge current");

            if (c.Has("k_p") != c.Has("H_rate"))
                c.Warn("peukert", "k_p/H_rate: Peukert needs both the exponent and the hour rate; it is not applied.");

            if (loads.Any(l => l.Backed))
                c.Warn("battery-charge", "t_auto: the power supply rating covers the DC loads only. A battery-backed load needs the battery's charging power on top (a 0.1C charge at the battery voltage is a common allowance); check that the supply can charge the battery as well as carry the load.");

            if (c.Has("k_p") && c.Has("H_rate"))
            {
                c.Input("k_p");
                c.Input("H_rate");

                // Peukert's exponent is valid inside the rated hour rate (elec.load-budget-06): beyond it the relation gives a capacity credit that real batteries do not, so the plain product is the floor.
                if (c.Si("t_auto") > c.Si("H_rate") * (1 + 1e-9))
                {
                    c.Warn("peukert-range", $"t_auto: the autonomy {F(c.Si("t_auto") / 3600)} h is beyond the rated hour rate {F(c.Si("H_rate") / 3600)} h, outside the range the Peukert exponent is valid for; the capacity is not taken below I_b·t.");
                    c.Assign("Ah", "max(I_b * H_rate * (t_auto / H_rate)^(1 / k_p), I_b * t_auto)", "A*h", "Discharge capacity, Peukert-adjusted (not below I_b·t beyond the rated hour rate)", "Linden 5th ed.");
                }
                else
                {
                    c.Assign("Ah", "I_b * H_rate * (t_auto / H_rate)^(1 / k_p)", "A*h", "Discharge capacity, Peukert-adjusted", "Linden 5th ed.");
                }
            }
            else
            {
                c.Assign("Ah", "I_b * t_auto", "A*h", "Discharge capacity");
            }

            c.Assign("C_req", "Ah / (DoD * eta_b * k_temp * k_age)", "A*h", "Minimum battery capacity");
            outputs.AddRange([("I_b", "Battery discharge current"), ("Ah", "Discharge capacity"), ("C_req", "Minimum battery capacity")]);
        }

        // The supply voltage and the linked machine's power factor travel on to the voltage-drop step (chain.motor-circuit-07).
        c.Record("U_L", "U_3ph", $"{F(c.Si("U_3ph"))} V", ScalarValue.Of(c.Si("U_3ph"), "V"), "Three-phase line voltage of the bus");
        c.Record("pf_link", "cosφ of the linked machine", F(c.InputSi("cosphi_add")), new ScalarValue(c.InputSi("cosphi_add")), "Power factor of the linked machine");
        outputs.AddRange([("U_L", "Three-phase line voltage"), ("pf_link", "Power factor of the linked machine")]);

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
