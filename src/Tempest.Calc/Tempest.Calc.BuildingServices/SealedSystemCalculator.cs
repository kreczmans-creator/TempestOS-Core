using Tempest.Calc.BuildingServices.Fluids;
using Tempest.Calc.Engine.Physics;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.BuildingServices;

/// <summary>B6 — sealed LTHW system: emitter outputs at the design temperatures and the expansion vessel.</summary>
public sealed class SealedSystemCalculator : ICalcCalculator
{
    public const string Id = "bs.sealed-system";
    public const string Amtd = "arithmetic mean (catalogue rating)";
    public const string Lmtd = "log mean";
    public const double MinSystemVolume = 10;
    public const double MaxSystemVolume = 10_000;

    private static readonly IReadOnlyList<CalcColumnSpec> RoomColumns =
    [
        new("name", "Room", CalcInputKind.Text),
        new("Phi", "Heat required", CalcInputKind.Quantity, "W", HardMin: 0, HardMax: 1e6),
        new("Phi50", "Selected emitter output at ΔT 50 K", CalcInputKind.Quantity, "W", HardMin: 0, HardMax: 1e6, IsOptional: true),
        new("V_em", "Emitter water content", CalcInputKind.Quantity, "L", HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("t_i", "Room design temperature (blank = the sheet's room temperature t_i)", CalcInputKind.Quantity, "degC", HardMin: 10, HardMax: 30, IsOptional: true),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.4.0", CalcDiscipline.BuildingServices, "Sealed heating system",
        "A closed LTHW system below 100 °C: the catalogue output (at ΔT 50 K) each room's emitter needs at the design flow, return and room temperatures, checked against the selected emitters; and the minimum expansion vessel for the system volume, fill and maximum temperatures, static head and pressures.",
        "Emitter temperature difference: ΔT = (t_f + t_r)/2 − t_i, or the log mean (t_f − t_r)/ln((t_f − t_i)/(t_r − t_i))\n" +
        "Output at design: Φ = Φ50·(ΔT/50)^n; required catalogue output Φ50 = Φ_req/(ΔT/50)^n\n" +
        "System volume: V_s = Σ V_em + V_pipe + V_plant\n" +
        "Water expansion: e_x = ρ(t_fill)/ρ(t_max) − 1 (water density by Kell 1975), or a glycol record\n" +
        "Vessel pre-charge: p_i = ρ·g·h_static + margin (≥ static head)\n" +
        "Vessel size: V_e = e_x·V_s/[1 − (p_i + 1)/(p_f + 1)] (pressures in bar gauge, + 1 bar for absolute); the expansion volume only, sized for the higher of t_max and t_f; a water reserve and a safety-valve margin are not included",
        [
            new CalcInputSpec("rooms", "Rooms and emitters", CalcInputKind.Table, Columns: RoomColumns, MaxRows: 200, IsOptional: true, Group: "Emitters"),
            new CalcInputSpec("t_f", "Flow temperature", CalcInputKind.Quantity, "degC", Default: 75, Min: 30, Max: 90, HardMin: 30, HardMax: 90, Group: "Emitters"),
            new CalcInputSpec("t_r", "Return temperature", CalcInputKind.Quantity, "degC", Default: 65, Min: 30, Max: 90, HardMin: 30, HardMax: 90, Group: "Emitters"),
            new CalcInputSpec("t_i", "Room temperature", CalcInputKind.Quantity, "degC", Default: 20, Min: 16, Max: 24, HardMin: 16, HardMax: 24, Group: "Emitters"),
            new CalcInputSpec("n", "Emitter exponent", CalcInputKind.Number, Default: 1.3, Min: 1.1, Max: 1.5, HardMin: 1.1, HardMax: 1.5, Group: "Emitters"),
            new CalcInputSpec("dT_method", "Mean temperature difference", CalcInputKind.Choice, Choices: [Amtd, Lmtd], DefaultChoice: Amtd, Group: "Emitters"),
            new CalcInputSpec("V_pipe", "Pipework water content", CalcInputKind.Quantity, "L", Default: 20, Min: 0, Max: 10000, HardMin: 0, HardMax: 10000, Group: "Volume"),
            new CalcInputSpec("V_plant", "Plant water content (boiler, cylinder coil, buffer)", CalcInputKind.Quantity, "L", Default: 10, Min: 0, Max: 10000, HardMin: 0, HardMax: 10000, Group: "Volume"),
            new CalcInputSpec("t_fill", "Fill temperature", CalcInputKind.Quantity, "degC", Default: 10, Min: 4, Max: 30, HardMin: 1, HardMax: 40, Group: "Expansion"),
            new CalcInputSpec("t_max", "Maximum system temperature", CalcInputKind.Quantity, "degC", Default: 85, Min: 30, Max: 99, HardMin: 30, HardMax: 99, Group: "Expansion"),
            new CalcInputSpec("e_fluid", "Expansion coefficient from a glycol record (replaces water)", CalcInputKind.Number, Min: 0.001, Max: 0.2, HardMin: 0, HardMax: 0.5, IsOptional: true, Group: "Expansion"),
            new CalcInputSpec("h_static", "Static height above the vessel", CalcInputKind.Quantity, "m", Default: 5, Min: 0, Max: 50, HardMin: 0, HardMax: 50, Group: "Pressures"),
            new CalcInputSpec("p_margin", "Pre-charge margin over static head", CalcInputKind.Quantity, "bar", Default: 0.3, Min: 0, Max: 1, HardMin: 0, HardMax: 2, Group: "Pressures"),
            new CalcInputSpec("p_f", "Final (maximum working) pressure, gauge", CalcInputKind.Quantity, "bar", Default: 2.5, Min: 0.5, Max: 6, HardMin: 0.1, HardMax: 6, Group: "Pressures"),
            new CalcInputSpec("p_prv", "Safety valve setting, gauge", CalcInputKind.Quantity, "bar", Default: 3, Min: 1.5, Max: 6, HardMin: 1.5, HardMax: 6, Group: "Pressures"),
            new CalcInputSpec("V_vessel", "Selected vessel volume", CalcInputKind.Quantity, "L", Min: 1, Max: 5000, HardMin: 0.01, HardMax: 1e5, IsOptional: true, Group: "Pressures"),
        ],
        [
            new CalcAssumption("Closed LTHW system below 100 °C; emitter output follows Φ50·(ΔT/50)^n with the manufacturer's n.", "Catalogue ratings at 75/65/20 °C."),
            new CalcAssumption("Water density from the Kell (1975) correlation, within about 0.01 % of IAPWS-95 at atmospheric pressure, 0–100 °C; glycol mixtures take e from a record.", "Formula, not a table."),
            new CalcAssumption("Gauge pressures are made absolute by adding 1 bar, as the vessel relation is usually written.", "Within 1.3 % of the standard atmosphere."),
            new CalcAssumption("V_e is the expansion volume by CIBSE Guide B1's relation only. It does not include a water reserve for system losses, nor a margin between the final pressure and the safety valve setting beyond p_f below p_prv; add both when the vessel is selected, from the heating-system standard in use.", "Stated omission."),
        ],
        [
            new CalcClauseRef("BS EN 442-2:2014", "cl. 5", "Radiators and convectors — test methods and rating (characteristic equation)"),
            new CalcClauseRef("CIBSE Guide B1", "2016, §1.4", "Heating — sealed systems and expansion"),
        ],
        [
            new CalcApplicabilityRule("temperatures", "Flow above return above room temperature.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("pressures", "Pre-charge below final pressure below the safety valve setting.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("volume", "System volume between 10 and 10 000 L.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("v-pipe-default", "The pipework water content V_pipe is the template default of 20 L: it is not linked to the pipework, so a larger system gets an undersized expansion vessel.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("t-fill", "The fill temperature must be below the maximum system temperature.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("emitter", "A selected emitter output of 0 W is refused: leave the cell empty for none selected.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("t-max", "The maximum temperature is below the flow temperature: the vessel is sized for the flow temperature.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-dt50", "Catalogue conditions 75/65/20: correction 1",
                new CalcReferenceSource("Analytic (characteristic equation)", null, "ΔT = 70 − 20 = 50 K: (50/50)^1.3 = 1; a 1500 W room needs a 1500 W catalogue emitter"),
                [Rooms(("Lounge", 1500, 1600, 8)), Q("V_pipe", 20, "L"), Q("V_plant", 10, "L")],
                [new CalcExpectedValue("dT", 50, "K", 1e-9), new CalcExpectedValue("f_dT", 1, "", 1e-9), new CalcExpectedValue("R1", 1500, "W", 1e-9), new CalcExpectedValue("u_emit", 0.9375, "", 1e-9), new CalcExpectedValue("V_s", 38, "L", 1e-9)]),
            new CalcTestCase("ka-dt30", "Low-temperature 55/45/20, arithmetic mean",
                new CalcReferenceSource("Analytic (characteristic equation)", null, "ΔT = 30 K: (30/50)^1.3 = 0.514750; 1500 W needs 2914.03 W at ΔT 50"),
                [Rooms(("Lounge", 1500, 0, 0)), Q("t_f", 55, "degC"), Q("t_r", 45, "degC")],
                [new CalcExpectedValue("f_dT", 0.5147503, "", 1e-6), new CalcExpectedValue("R1", 2914.034, "W", 1e-6)]),
            new CalcTestCase("ka-lmtd", "Low-temperature 55/45/20, log mean",
                new CalcReferenceSource("Analytic (log-mean form)", null, "ΔT = 10/ln(35/25) = 29.7201 K; (29.7201/50)^1.3 = 0.508516"),
                [Rooms(("Lounge", 1500, 0, 0)), Q("t_f", 55, "degC"), Q("t_r", 45, "degC"), CalcInputValue.Choice("dT_method", Lmtd)],
                [new CalcExpectedValue("dT", 29.72013, "K", 1e-6), new CalcExpectedValue("f_dT", 0.5085164, "", 1e-6)]),
            new CalcTestCase("ka-vessel", "Expansion vessel for 100 L, 10 → 85 °C, 5 m static",
                new CalcReferenceSource("Analytic (Kell 1975 density; vessel relation)", null, "ρ(10) = 999.700, ρ(85) = 968.620 kg/m³: e = 0.0320862; p_i = 999.70·9.80665·5/10⁵ + 0.3 = 0.790185 bar; V_e = 3.20862/(1 − 1.790185/3.5) = 6.56806 L"),
                [Q("V_pipe", 90, "L"), Q("V_plant", 10, "L"), Q("V_vessel", 8, "L")],
                [new CalcExpectedValue("e_x", 0.03208619, "", 1e-6), new CalcExpectedValue("p_i", 0.7901852, "bar", 1e-6), new CalcExpectedValue("V_e", 6.568060, "L", 1e-6), new CalcExpectedValue("u_vessel", 0.8210075, "", 1e-6)]),
        ],
        Category: "Hydraulics",
        OutputPorts:
        [
            new CalcOutputPort("Phi_tot", "W", "Total heat required by the rooms (→ pipe-pump Phi_load)", "heating.heat_required"),
            new CalcOutputPort("V_s", "L", "System water volume", "heating.volume_system"),
            new CalcOutputPort("V_e", "L", "Minimum expansion vessel volume", "heating.volume_expansion_min"),
            new CalcOutputPort("dT", "K", "Emitter mean temperature difference", "heating.temperature_difference_emitter"),
            new CalcOutputPort("dT_fr", "K", "Flow-to-return temperature difference of the loop (→ pipe-pump dT_load)", "heating.temperature_difference_loop"),
            new CalcOutputPort("t_m", "degC", "Mean water temperature of the loop, (t_f + t_r)/2 (→ pipe-pump T)", "heating.temperature_mean"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("emitter.rating", "Emitter exponent", ["n"], "From the emitter manufacturer's catalogue (BS EN 442 rating)."),
            new CalcRecordKind("fluid.expansion", "Heat-transfer fluid expansion", ["e_fluid"], "From the glycol supplier's data between the fill and maximum temperatures."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    /// <summary>A rooms table from (name, Φ required W, Φ50 selected W (0 = none), emitter content L) rows.</summary>
    public static CalcInputValue Rooms(params (string Name, double Phi, double Phi50, double Volume)[] rows) =>
        CalcInputValue.Table("rooms", rows.Select(r =>
        {
            var cells = new List<(string, CalcCell)> { ("name", new CalcCell(Text: r.Name)), ("Phi", new CalcCell(r.Phi, "W")) };

            if (r.Phi50 > 0)
                cells.Add(("Phi50", new CalcCell(r.Phi50, "W")));

            cells.Add(("V_em", new CalcCell(r.Volume, "L")));
            return CalcRow.Of([.. cells]);
        }).ToList());

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_emit", "u_vessel");
        foreach (var s in new[] { "t_f", "t_r", "t_i", "n", "V_pipe", "V_plant", "t_fill", "t_max", "h_static", "p_margin", "p_f", "p_prv" })
            c.Input(s);

        if (!(c.Si("t_f") >= c.Si("t_r") && c.Si("t_r") > c.Si("t_i")))
        {
            c.Block("temperatures", "t_r: the flow must be at least the return, and the return above the room temperature.");
            return;
        }

        if (!(c.Si("t_fill") < c.Si("t_max")))
        {
            c.Block("t-fill", $"t_fill: the fill temperature ({F(c.Si("t_fill") - 273.15)} °C) must be below the maximum system temperature ({F(c.Si("t_max") - 273.15)} °C); the water expands between them.");
            return;
        }

        // The water reaches the flow temperature, so the vessel is sized for the higher of the two (Black B-A-08).
        var tExpansion = Math.Max(c.Si("t_max"), c.Si("t_f"));

        if (c.Si("t_max") < c.Si("t_f"))
            c.Warn("t-max", $"t_max: the maximum system temperature ({F(c.Si("t_max") - 273.15)} °C) is below the flow temperature ({F(c.Si("t_f") - 273.15)} °C); the vessel is sized for the flow temperature.");

        var log = c.InputText("dT_method") == Lmtd;

        if (log && c.Si("t_f") > c.Si("t_r"))
            c.Assign("dT", "(t_f - t_r) / ln((t_f - t_i) / (t_r - t_i))", "K", "Log-mean temperature difference, emitter to room", "BS EN 442-2 cl. 5");
        else
            c.Assign("dT", "(t_f + t_r) / 2 - t_i", "K", "Mean water-to-room temperature difference", "BS EN 442-2 cl. 5");

        c.Assign("dT_fr", "t_f - t_r", "K", "Flow-to-return temperature difference (what the water gives up across the emitters)");
        c.Assign("t_m", "(t_f + t_r) / 2", "degC", "Mean water temperature, (flow + return)/2");
        c.Assign("f_dT", "(dT / 50 K)^n", null, "Output correction from ΔT 50 K");
        var f = c.Si("f_dT");
        var rows = c.Rows("rooms");
        var w = UnitCatalog.Parse("W");
        var litre = UnitCatalog.Parse("L");
        var outputs = new List<(string, string)> { ("dT", "Mean temperature difference"), ("dT_fr", "Flow-to-return temperature difference"), ("t_m", "Mean water temperature"), ("f_dT", "Output correction factor") };
        var worst = 0.0;
        var anySelected = false;
        var total = 0.0;
        var emitterVolume = 0.0;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var name = row.Get("name")?.Text is { Length: > 0 } text ? CalcSheetTextRenderer.Clean(text) : $"Room {i + 1}";
            var phi = Si(row, "Phi", w);
            var rowF = f;
            var rowNote = string.Empty;

            if (row.Get("t_i")?.Number is not null)
            {
                // The room's own design temperature (B4's θ_int) sets its emitter's temperature difference.
                var tiRoom = Si(row, "t_i", UnitCatalog.Parse("degC"));

                if (!(c.Si("t_r") > tiRoom))
                {
                    c.Block("temperatures", $"rooms: {name}: the room temperature ({F(tiRoom - 273.15)} °C) must be below the return temperature ({F(c.Si("t_r") - 273.15)} °C).");
                    return;
                }

                var dTRoom = log && c.Si("t_f") > c.Si("t_r")
                    ? (c.Si("t_f") - c.Si("t_r")) / Math.Log((c.Si("t_f") - tiRoom) / (c.Si("t_r") - tiRoom))
                    : (c.Si("t_f") + c.Si("t_r")) / 2 - tiRoom;
                rowF = Math.Pow(dTRoom / 50, c.Si("n"));
                rowNote = $" at its room temperature {F(tiRoom - 273.15)} °C (ΔT {F(dTRoom)} K)";
            }

            var needed = phi / rowF;
            total += phi;
            emitterVolume += Si(row, "V_em", litre);
            var symbol = $"R{i + 1}";

            if (row.Get("Phi50") is { Number: <= 0 })
            {
                c.Block("emitter", $"rooms: {name}: the selected emitter output is 0 W, which cannot heat the room; leave Phi50 empty when no emitter is selected, or enter its catalogue output.");
                return;
            }

            if (Si(row, "Phi50", w) is var selected && selected > 0)
            {
                var u = phi / (selected * rowF);
                worst = Math.Max(worst, u);
                anySelected = true;
                c.Record(symbol, "Φ_req/f_dT", $"{F(phi)} / {F(rowF)}", ScalarValue.Of(needed, w), $"{name}: catalogue output needed{rowNote}; selected {F(selected)} W gives {F(selected * rowF)} W (utilisation {CalcValue.FormatNumber(u, 3)})");
            }
            else
            {
                c.Record(symbol, "Φ_req/f_dT", $"{F(phi)} / {F(rowF)}", ScalarValue.Of(needed, w), $"{name}: catalogue output needed at ΔT 50 K{rowNote}");
            }

            outputs.Add((symbol, $"{name}: Φ50 needed"));
        }

        if (rows.Count > 0)
        {
            c.Record("Phi_tot", "Σ Φ_req", $"{F(total)} W", ScalarValue.Of(total, w), "Total heat required by the rooms");
            outputs.Add(("Phi_tot", "Total heat required"));
        }

        c.Record("V_em", "Σ V_em", $"{F(litre.FromSi(emitterVolume))} L", ScalarValue.Of(litre.FromSi(emitterVolume), litre), "Emitter water content");
        c.Assign("V_s", "V_em + V_pipe + V_plant", "L", "System water volume");

        if (Math.Abs(litre.FromSi(c.Si("V_pipe")) - 20) < 1e-9)
            c.Warn("v-pipe-default", "V_pipe: 20 L is the template default, not the water content of this system's pipework (π D² L/4 over the pipe segments; B1 gives it as V_pipe). A larger system holds more water, so the vessel here would be undersized (DN28 × 40 m holds 24.63 L: V_e 2.496 L against 2.800 L).");

        var vs = litre.FromSi(c.Si("V_s"));

        if (vs < MinSystemVolume || vs > MaxSystemVolume)
        {
            c.Block("volume", $"V_pipe: the system volume {CalcValue.FormatNumber(vs, 4)} L is outside 10–10 000 L.");
            return;
        }

        var fill = Water.Density(c.Si("t_fill") - 273.15);
        c.Record("rho_fill", "ρ(t_fill), Kell 1975", $"t = {F(c.Si("t_fill") - 273.15)} °C", ScalarValue.Of(fill, "kg/m^3"), "Water density at fill");

        if (c.Has("e_fluid"))
        {
            c.Input("e_fluid");
            c.Assign("e_x", "e_fluid", null, "Expansion coefficient (fluid record)");
        }
        else
        {
            var hot = Water.Density(tExpansion - 273.15);
            c.Record("rho_max", tExpansion > c.Si("t_max") + 1e-9 ? "ρ(t_f), Kell 1975 (t_f is above t_max)" : "ρ(t_max), Kell 1975", $"t = {F(tExpansion - 273.15)} °C", ScalarValue.Of(hot, "kg/m^3"), "Water density at maximum temperature");
            c.Assign("e_x", "rho_fill / rho_max - 1", null, "Expansion coefficient of water");
        }

        c.Assign("p_st", "rho_fill * 9.80665 m/s^2 * h_static", "bar", "Static head at the vessel");
        c.Assign("p_i", "p_st + p_margin", "bar", "Pre-charge (initial) pressure, gauge");

        if (!(c.Si("p_i") < c.Si("p_f") && c.Si("p_f") < c.Si("p_prv")))
        {
            c.Block("pressures", $"p_f: the pre-charge ({F(c.Si("p_i") / 1e5)} bar) must be below the final pressure ({F(c.Si("p_f") / 1e5)} bar), and that below the safety valve setting ({F(c.Si("p_prv") / 1e5)} bar).");
            return;
        }

        RecordVesselVolume(c, litre);
        outputs.AddRange([("V_em", "Emitter water content"), ("V_s", "System water volume"), ("e_x", "Expansion coefficient"), ("p_i", "Pre-charge pressure"), ("V_e", "Minimum vessel volume")]);

        if (anySelected)
        {
            c.Record("u_emit", "max over rooms Φ_req/(Φ50·f_dT)", "worst room", new ScalarValue(worst), "Emitter utilisation (governing room)");
            outputs.Add(("u_emit", "Emitter utilisation"));
        }

        if (c.Has("V_vessel"))
        {
            c.Input("V_vessel");
            c.Assign("u_vessel", "V_e / V_vessel", null, "Vessel utilisation");
            outputs.Add(("u_vessel", "Vessel utilisation"));
        }

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        if (anySelected)
            c.CheckUtilisation("Selected emitters against room heat (governing room)", "u_emit");

        if (c.Has("V_vessel"))
            c.CheckUtilisation("Selected vessel against minimum volume", "u_vessel", "CIBSE Guide B1 §1.4");
    }

    /// <summary>
    /// V_e through the shared gas-law kernel (<see cref="GasLaw"/>): isothermal (n = 1) on the gas side, absolute pressures as gauge + 1 bar,
    /// the water expansion e_x·V_s as the volume the vessel must accept. Recorded with the formula and substitution the engine would have
    /// written for the inline expression, so the sheet reads exactly as before.
    /// </summary>
    private static void RecordVesselVolume(CalcRunContext c, Unit litre)
    {
        var sf = c.Session.SignificantFigures;
        var expansion = c.Scalar("e_x");
        var system = c.Scalar("V_s");
        var preCharge = c.Scalar("p_i");
        var final = c.Scalar("p_f");
        var low = GasLaw.ToAbsolute(preCharge.Si, GasLaw.OneBar);
        var high = GasLaw.ToAbsolute(final.Si, GasLaw.OneBar);
        var volume = GasLaw.GasVolumeForDisplacement(expansion.Si * system.Si, low, high, GasLaw.Isothermal);

        c.Record(
            "V_e",
            "e_x * V_s / (1 - (p_i + 1 bar) / (p_f + 1 bar))",
            $"{expansion.Format(sf)} × {system.Format(sf)} / (1 − ({preCharge.Format(sf)} + 1 bar) / ({final.Format(sf)} + 1 bar))",
            new ScalarValue(volume, system.Dimension, litre),
            "Minimum expansion vessel volume",
            "CIBSE Guide B1 §1.4");
    }

    private static double Si(CalcRow row, string key, Unit unit) => row.Get(key) is { Number: { } n } cell ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : unit).ToSi(n) : 0;

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
