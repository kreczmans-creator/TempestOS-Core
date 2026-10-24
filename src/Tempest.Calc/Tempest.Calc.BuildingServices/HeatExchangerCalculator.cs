using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.BuildingServices;

/// <summary>B5 — heat exchanger rating and sizing by ε-NTU, with the LMTD and its correction factor F shown as a cross-check.</summary>
public sealed class HeatExchangerCalculator : ICalcCalculator
{
    public const string Id = "bs.heat-exchanger";
    public const string Counterflow = "counterflow";
    public const string Parallel = "parallel flow";
    public const string ShellTube = "shell and tube, 1 shell pass, 2n tube passes";
    public const string Crossflow = "crossflow, both fluids unmixed (approximate)";
    public const string Isothermal = "one fluid at constant temperature (C_r = 0)";
    public const string HotConstant = "hot side at constant temperature (e.g. condensing vapour)";
    public const string ColdConstant = "cold side at constant temperature (e.g. boiling liquid)";
    public const string Rating = "rating (given area)";
    public const string Sizing = "sizing (given duty)";

    public CalcTemplate Template { get; } = new(
        Id, "1.2.0", CalcDiscipline.BuildingServices, "Heat exchanger (LMTD and ε-NTU)",
        "Single-phase heat exchanger with constant properties: in rating mode the duty and outlet temperatures for a given area; in sizing mode the area for a given duty. Counterflow, parallel flow, 1-shell-pass shell and tube, crossflow with both fluids unmixed (approximate), and one fluid at constant temperature.",
        "Capacity rates: C_h = ṁ_h·c_p,h, C_c = ṁ_c·c_p,c, C_r = C_min/C_max; with one fluid at constant temperature (C_r = 0) that fluid's capacity rate is infinite, C_min is the other stream's and the constant stream leaves at its inlet temperature\n" +
        "Overall coefficient with fouling: U_f = 1/(1/U + R_f)\n" +
        "Transfer units: NTU = U_f·A/C_min; effectiveness ε(NTU, C_r) for the arrangement\n" +
        "Duty: Q = ε·C_min·(T_h,in − T_c,in); outlets from the energy balance\n" +
        "Sizing: ε = Q/(C_min·ΔT_max), NTU from the inverse relation, A = NTU·C_min/U_f\n" +
        "Cross-check: Q = U_f·A·F·ΔT_lm, F from R and P (1 shell pass) or back-calculated",
        [
            new CalcInputSpec("arrangement", "Flow arrangement", CalcInputKind.Choice, Choices: [Counterflow, Parallel, ShellTube, Crossflow, Isothermal], DefaultChoice: Counterflow),
            new CalcInputSpec("const_side", "Which side is at constant temperature (used only by the C_r = 0 arrangement)", CalcInputKind.Choice, Choices: [HotConstant, ColdConstant], DefaultChoice: HotConstant),
            new CalcInputSpec("mode", "Mode", CalcInputKind.Choice, Choices: [Rating, Sizing], DefaultChoice: Rating),
            new CalcInputSpec("m_h", "Hot-side mass flow", CalcInputKind.Quantity, "kg/s", Default: 1, Min: 0.001, Max: 100, HardMin: 0.001, HardMax: 100, Group: "Hot side"),
            new CalcInputSpec("cp_h", "Hot-side specific heat", CalcInputKind.Quantity, "J/(kg*K)", Default: 4180, Min: 500, Max: 10000, HardMin: 1, HardMax: 1e5, Group: "Hot side"),
            new CalcInputSpec("T_h_in", "Hot-side inlet temperature", CalcInputKind.Quantity, "degC", Default: 80, Min: -40, Max: 200, HardMin: -40, HardMax: 200, Group: "Hot side"),
            new CalcInputSpec("m_c", "Cold-side mass flow", CalcInputKind.Quantity, "kg/s", Default: 1, Min: 0.001, Max: 100, HardMin: 0.001, HardMax: 100, Group: "Cold side"),
            new CalcInputSpec("cp_c", "Cold-side specific heat", CalcInputKind.Quantity, "J/(kg*K)", Default: 4180, Min: 500, Max: 10000, HardMin: 1, HardMax: 1e5, Group: "Cold side"),
            new CalcInputSpec("T_c_in", "Cold-side inlet temperature", CalcInputKind.Quantity, "degC", Default: 20, Min: -40, Max: 200, HardMin: -40, HardMax: 200, Group: "Cold side"),
            new CalcInputSpec("U", "Overall heat transfer coefficient (clean)", CalcInputKind.Quantity, "W/(m^2*K)", Default: 1000, Min: 10, Max: 10000, HardMin: 10, HardMax: 10000, Group: "Exchanger"),
            new CalcInputSpec("R_f", "Total fouling resistance", CalcInputKind.Quantity, "m^2*K/W", Default: 0, Min: 0, Max: 0.01, HardMin: 0, HardMax: 0.1, Group: "Exchanger"),
            new CalcInputSpec("A", "Heat transfer area (rating)", CalcInputKind.Quantity, "m^2", Default: 4.18, Min: 0.01, Max: 1000, HardMin: 0.01, HardMax: 1000, IsOptional: true, Group: "Exchanger"),
            new CalcInputSpec("Q_req", "Required duty", CalcInputKind.Quantity, "kW", Min: 0.001, Max: 1e5, HardMin: 1e-6, HardMax: 1e6, IsOptional: true, Group: "Requirements"),
            new CalcInputSpec("dT_min", "Minimum approach temperature difference", CalcInputKind.Quantity, "K", Default: 2, Min: 0, Max: 50, HardMin: 0, HardMax: 200, Group: "Requirements"),
        ],
        [
            new CalcAssumption("Single phase on both sides (except the C_r = 0 arrangement), constant properties, no heat loss to surroundings.", "Incropera 7th ed. §11.3–11.4."),
            new CalcAssumption("The crossflow both-unmixed effectiveness is the published approximate correlation, flagged on the sheet.", "Incropera 7th ed. Table 11.3."),
            new CalcAssumption("Specific heats are user records (or B2 for air) at the mean temperature of each stream.", "§A3 reference records."),
        ],
        [
            new CalcClauseRef("Incropera, Fundamentals of Heat and Mass Transfer", "7th ed., §11.3–11.5, Tables 11.3–11.4", "LMTD and effectiveness-NTU methods"),
            new CalcClauseRef("Çengel & Ghajar, Heat and Mass Transfer", "5th ed., Ch. 11", "Heat exchangers"),
        ],
        [
            new CalcApplicabilityRule("temperatures", "The hot inlet must be hotter than the cold inlet.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("area-missing", "Rating needs an area; sizing needs a required duty.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("duty-unattainable", "The required duty exceeds what any area of this arrangement can transfer.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("lmtd-mismatch", "The ε-NTU duty and the LMTD duty differ by more than 2 %: the arrangement or the temperatures are inconsistent.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("crossflow-approx", "Crossflow both-unmixed effectiveness is an approximate correlation.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("pinch", "An end temperature difference is zero: the area is effectively infinite.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("low-F", "F below 0.75: the 1-shell-pass arrangement is used inefficiently; consider more shell passes.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-counter-balanced", "Balanced counterflow, NTU = 1",
                new CalcReferenceSource("Analytic (ε = NTU/(1 + NTU) for C_r = 1)", null, "C_h = C_c = 4180 W/K, UA = 4180 W/K: ε = 0.5, Q = 0.5·4180·60 = 125.4 kW, both outlets 50 °C, ΔT_lm = 30 K"),
                [Choice("arrangement", Counterflow), Q("m_h", 1, "kg/s"), Q("m_c", 1, "kg/s"), Q("T_h_in", 80, "degC"), Q("T_c_in", 20, "degC"), Q("U", 1000, "W/(m^2*K)"), Q("A", 4.18, "m^2"), Q("Q_req", 120, "kW")],
                [new CalcExpectedValue("NTU", 1, "", 1e-9), new CalcExpectedValue("eps", 0.5, "", 1e-9), new CalcExpectedValue("Q", 125.4, "kW", 1e-9), new CalcExpectedValue("T_h_out", 50, "degC", 1e-9), new CalcExpectedValue("dT_lm", 30, "K", 1e-9), new CalcExpectedValue("F", 1, "", 1e-9)]),
            new CalcTestCase("ka-parallel-balanced", "Balanced parallel flow, NTU = 1",
                new CalcReferenceSource("Analytic (ε = (1 − e^(−2·NTU))/2 for C_r = 1)", null, "ε = 0.4323324 (ε = (1 − e^(−2))/2); Q = 0.4323324·4180·60 = 108.42896 kW"),
                [Choice("arrangement", Parallel), Q("m_h", 1, "kg/s"), Q("m_c", 1, "kg/s"), Q("T_h_in", 80, "degC"), Q("T_c_in", 20, "degC"), Q("U", 1000, "W/(m^2*K)"), Q("A", 4.18, "m^2")],
                [new CalcExpectedValue("eps", 0.4323324, "", 1e-6), new CalcExpectedValue("Q", 108.42896, "kW", 1e-6)]),
            new CalcTestCase("ka-shell-tube", "1-2 shell and tube, C_r = 0.5, NTU = 1: ε-NTU and F agree",
                new CalcReferenceSource("Analytic (Incropera Table 11.3 shell-and-tube relation; F from R and P)", "7th ed.", "ε = 0.539940; Q = 135.417 kW; T_h,out = 47.604 °C; T_c,out = 36.198 °C; ΔT_lm,cf = 35.082 K; F = 0.923456 (R = 2, P = 0.270)"),
                [Choice("arrangement", ShellTube), Q("m_h", 1, "kg/s"), Q("m_c", 2, "kg/s"), Q("T_h_in", 80, "degC"), Q("T_c_in", 20, "degC"), Q("U", 1000, "W/(m^2*K)"), Q("A", 4.18, "m^2")],
                [new CalcExpectedValue("eps", 0.5399396, "", 1e-6), new CalcExpectedValue("Q", 135.4168, "kW", 1e-5), new CalcExpectedValue("T_c_out", 36.19819, "degC", 1e-6), new CalcExpectedValue("F", 0.9234561, "", 1e-6)]),
            new CalcTestCase("ka-counter-sizing", "Counterflow sizing, C_r = 0.5, ε = 0.774600",
                new CalcReferenceSource("Analytic (inverse counterflow relation)", null, "C_min = 4180 W/K, C_r = 0.5, Q = 0.7746003·4180·60 = 194.27 kW requires NTU = 2, A = 8.36 m² at U = 1000"),
                [Choice("arrangement", Counterflow), Choice("mode", Sizing), Q("m_h", 1, "kg/s"), Q("m_c", 2, "kg/s"), Q("T_h_in", 80, "degC"), Q("T_c_in", 20, "degC"), Q("U", 1000, "W/(m^2*K)"), Q("Q_req", 194.2697, "kW")],
                [new CalcExpectedValue("NTU", 2, "", 1e-5), new CalcExpectedValue("A_req", 8.36, "m^2", 1e-5)]),
            new CalcTestCase("ka-isothermal-hot", "Hot side at constant temperature (condensing vapour), cold water 2 kg/s, NTU = 0.5 on the finite stream",
                new CalcReferenceSource("Analytic (ε = 1 − e^(−NTU), C_r = 0, C_min = the cold stream)", null, "C_c = 8360 W/K, UA = 4180 W/K: NTU = 0.5, ε = 0.393469, Q = 0.393469·8360·60 = 197.36 kW; the hot side leaves at 80 °C; the cold side rises 23.61 K to 43.61 °C (the hot stream's own m·c_p of 2090 W/K must not set C_min)"),
                [Choice("arrangement", Isothermal), Choice("const_side", HotConstant), Q("m_h", 0.5, "kg/s"), Q("m_c", 2, "kg/s"), Q("T_h_in", 80, "degC"), Q("T_c_in", 20, "degC"), Q("U", 1000, "W/(m^2*K)"), Q("A", 4.18, "m^2")],
                [new CalcExpectedValue("NTU", 0.5, "", 1e-9), new CalcExpectedValue("eps", 0.3934693, "", 1e-6), new CalcExpectedValue("Q", 197.36422, "kW", 1e-6), new CalcExpectedValue("T_h_out", 80, "degC", 1e-9), new CalcExpectedValue("T_c_out", 43.60816, "degC", 1e-6), new CalcExpectedValue("Q_lmtd", 197.36422, "kW", 1e-6)]),
            new CalcTestCase("ka-isothermal-cold", "Cold side at constant temperature (boiling liquid), hot water 1 kg/s, NTU = 1 on the finite stream",
                new CalcReferenceSource("Analytic (ε = 1 − e^(−NTU), C_r = 0, C_min = the hot stream)", null, "C_h = 4180 W/K, UA = 4180 W/K: NTU = 1, ε = 0.632121, Q = 0.632121·4180·60 = 158.54 kW; the cold side leaves at 20 °C; the hot side falls 37.93 K to 42.07 °C"),
                [Choice("arrangement", Isothermal), Choice("const_side", ColdConstant), Q("m_h", 1, "kg/s"), Q("m_c", 1, "kg/s"), Q("T_h_in", 80, "degC"), Q("T_c_in", 20, "degC"), Q("U", 1000, "W/(m^2*K)"), Q("A", 4.18, "m^2")],
                [new CalcExpectedValue("eps", 0.6321206, "", 1e-6), new CalcExpectedValue("Q", 158.53584, "kW", 1e-6), new CalcExpectedValue("T_c_out", 20, "degC", 1e-9), new CalcExpectedValue("T_h_out", 42.07277, "degC", 1e-6), new CalcExpectedValue("Q_lmtd", 158.53584, "kW", 1e-6)]),
            new CalcTestCase("ka-incropera-11-1", "Oil cooler, counterflow concentric tube (Incropera Example 11.1)",
                new CalcReferenceSource("Incropera et al., Fundamentals of Heat and Mass Transfer", "7th ed.", "Example 11.1: oil 0.1 kg/s, 100 → 60 °C, c_p 2131; water 0.2 kg/s from 30 °C, c_p 4178: q = 8524 W, T_c,o = 40.2 °C, ΔT_lm = 43.2 °C. Numbers as recalled; check against the book"),
                [Choice("arrangement", Counterflow), Choice("mode", Sizing), Q("m_h", 0.1, "kg/s"), Q("cp_h", 2131, "J/(kg*K)"), Q("T_h_in", 100, "degC"), Q("m_c", 0.2, "kg/s"), Q("cp_c", 4178, "J/(kg*K)"), Q("T_c_in", 30, "degC"), Q("U", 38.1, "W/(m^2*K)"), Q("Q_req", 8.524, "kW")],
                [new CalcExpectedValue("T_h_out", 60, "degC", 1e-6), new CalcExpectedValue("T_c_out", 40.20, "degC", 1e-3), new CalcExpectedValue("dT_lm", 43.2, "K", 1e-3), new CalcExpectedValue("UA", 197.31, "W/K", 1e-3)],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Thermal",
        OutputPorts:
        [
            new CalcOutputPort("Q", "kW", "Heat duty"),
            new CalcOutputPort("T_h_out", "degC", "Hot-side outlet temperature"),
            new CalcOutputPort("T_c_out", "degC", "Cold-side outlet temperature"),
            new CalcOutputPort("dT_c", "K", "Cold-side temperature rise, T_c,out − T_c,in (→ B1 dT_load)"),
            new CalcOutputPort("T_c_m", "degC", "Mean cold-side temperature (→ B1 fluid temperature T)"),
            new CalcOutputPort("cp_c", "J/(kg*K)", "Cold-side specific heat as entered (→ B1 cp_load, so a glycol mixture reaches the pump flow)"),
            new CalcOutputPort("A_req", "m^2", "Required area (sizing)"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("fluid.properties", "Fluid specific heats", ["cp_h", "cp_c"], "From a property table or B2 at each stream's mean temperature, with its source."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue Choice(string symbol, string choice) => CalcInputValue.Choice(symbol, choice);

    public void Run(CalcRunContext c)
    {
        var arrangement = c.InputText("arrangement");
        var sizing = c.InputText("mode") == Sizing;

        foreach (var s in new[] { "m_h", "cp_h", "T_h_in", "m_c", "cp_c", "T_c_in", "U", "R_f", "dT_min" })
            c.Input(s);

        var thi = c.InputSi("T_h_in");
        var tci = c.InputSi("T_c_in");

        if (!(thi > tci))
        {
            c.Block("temperatures", "T_h_in: the hot inlet must be hotter than the cold inlet.");
            return;
        }

        if (sizing ? !c.Has("Q_req") : !c.Has("A"))
        {
            c.Block("area-missing", sizing ? "Q_req: sizing needs the required duty." : "A: rating needs the heat transfer area.");
            return;
        }

        if (arrangement == Crossflow)
            c.Warn("crossflow-approx", "arrangement: the crossflow both-unmixed effectiveness is an approximate correlation (within a few percent).");

        // With one fluid at constant temperature its capacity rate is infinite: the other stream is C_min and the constant stream leaves at its
        // inlet temperature (Black B-A-02, Green G-B-07). Its flow and specific heat are not used.
        var isothermal = arrangement == Isothermal;
        var hotConstant = isothermal && c.InputText("const_side") == HotConstant;
        var coldConstant = isothermal && !hotConstant;

        if (isothermal)
            c.Assume($"The {(hotConstant ? "hot" : "cold")} side is held at constant temperature (its capacity rate is infinite), so the {(hotConstant ? "cold" : "hot")} stream sets C_min and the constant stream leaves at its inlet temperature; the {(hotConstant ? "hot" : "cold")}-side flow and specific heat are not used.", "Incropera 7th ed. Table 11.3, C_r = 0.");

        c.Assign("C_h", "m_h * cp_h", "W/K", coldConstant ? "Hot-side capacity rate" : hotConstant ? "Hot-side capacity rate (not used: the hot side is at constant temperature)" : "Hot-side capacity rate");
        c.Assign("C_c", "m_c * cp_c", "W/K", hotConstant ? "Cold-side capacity rate" : coldConstant ? "Cold-side capacity rate (not used: the cold side is at constant temperature)" : "Cold-side capacity rate");
        c.Assign("C_min", hotConstant ? "C_c" : coldConstant ? "C_h" : "min(C_h, C_c)", "W/K", isothermal ? "Capacity rate of the stream whose temperature changes" : "Minimum capacity rate");
        c.Assign("C_r", arrangement == Isothermal ? "0" : "min(C_h, C_c) / max(C_h, C_c)", null, arrangement == Isothermal ? "Capacity ratio (one fluid at constant temperature)" : "Capacity ratio");
        c.Assign("U_f", "1 / (1 / U + R_f)", "W/(m^2*K)", "Overall coefficient with fouling");
        var cmin = c.Si("C_min");
        var cr = c.Si("C_r");
        var uf = c.Si("U_f");
        var dtMax = thi - tci;
        var kelvin = UnitCatalog.Parse("K");
        var kW = UnitCatalog.Parse("kW");
        var m2 = UnitCatalog.Parse("m^2");
        double ntu, eps, area;

        if (sizing)
        {
            c.Input("Q_req");
            eps = c.Si("Q_req") / (cmin * dtMax);
            var limit = Effectiveness(arrangement, 1e3, cr);
            c.Record("eps", "Q_req / (C_min·ΔT_max)", $"{F(c.Si("Q_req"))} / ({F(cmin)} × {F(dtMax)})", new ScalarValue(eps), "Effectiveness required");

            if (!(eps < limit - 1e-12))
            {
                c.Block("duty-unattainable", $"Q_req: the duty needs ε = {CalcValue.FormatNumber(eps, 4)}; a {arrangement} exchanger cannot exceed ε = {CalcValue.FormatNumber(limit, 4)} with these flows.");
                return;
            }

            ntu = InverseNtu(arrangement, eps, cr);
            area = ntu * cmin / uf;
            c.Record("NTU", "NTU(ε, C_r) inverse relation", $"ε = {F(eps)}, C_r = {F(cr)}", new ScalarValue(ntu), "Transfer units required", "Incropera 7th ed. Table 11.4");
            c.Record("A_req", "NTU·C_min / U_f", $"{F(ntu)} × {F(cmin)} / {F(uf)}", ScalarValue.Of(area, m2), "Required heat transfer area");
            c.Record("UA", "NTU·C_min", $"{F(ntu)} × {F(cmin)}", ScalarValue.Of(ntu * cmin, "W/K"), "Required UA");
        }
        else
        {
            c.Input("A");
            area = c.Si("A");
            ntu = uf * area / cmin;
            c.Record("NTU", "U_f·A / C_min", $"{F(uf)} × {F(area)} / {F(cmin)}", new ScalarValue(ntu), "Number of transfer units");
            eps = Effectiveness(arrangement, ntu, cr);
            c.Record("eps", Relation(arrangement), $"NTU = {F(ntu)}, C_r = {F(cr)}", new ScalarValue(eps), "Effectiveness", "Incropera 7th ed. Table 11.3");
            c.Record("UA", "U_f·A", $"{F(uf)} × {F(area)}", ScalarValue.Of(uf * area, "W/K"), "UA");
        }

        var q = eps * cmin * dtMax;
        var tho = hotConstant ? thi : thi - q / c.Si("C_h");
        var tco = coldConstant ? tci : tci + q / c.Si("C_c");
        c.Record("Q", "ε·C_min·(T_h,in − T_c,in)", $"{F(eps)} × {F(cmin)} × {F(dtMax)}", ScalarValue.Of(q / 1e3, kW), "Heat duty");
        c.Record("T_h_out", hotConstant ? "T_h,in (constant temperature)" : "T_h,in − Q/C_h", hotConstant ? $"{F(thi - 273.15)} °C" : $"{F(thi - 273.15)} °C − {F(q)} / {F(c.Si("C_h"))}", ScalarValue.Of(tho - 273.15, "degC"), "Hot-side outlet temperature");
        c.Record("T_c_out", coldConstant ? "T_c,in (constant temperature)" : "T_c,in + Q/C_c", coldConstant ? $"{F(tci - 273.15)} °C" : $"{F(tci - 273.15)} °C + {F(q)} / {F(c.Si("C_c"))}", ScalarValue.Of(tco - 273.15, "degC"), "Cold-side outlet temperature");

        // The cold stream's rise and mean temperature: what a pump on the cold circuit (B1) is sized for.
        c.Record("dT_c", "T_c,out − T_c,in", $"{F(tco - 273.15)} °C − {F(tci - 273.15)} °C", ScalarValue.Of(tco - tci, kelvin), "Cold-side temperature rise");
        c.Record("T_c_m", "(T_c,in + T_c,out)/2", $"({F(tci - 273.15)} °C + {F(tco - 273.15)} °C)/2", ScalarValue.Of((tci + tco) / 2 - 273.15, "degC"), "Mean cold-side temperature");

        var parallel = arrangement == Parallel;
        var dt1 = parallel ? thi - tci : thi - tco;
        var dt2 = parallel ? tho - tco : tho - tci;
        var approach = parallel ? tho - tco : Math.Min(thi - tco, tho - tci);
        c.Record("dT_app", parallel ? "T_h,out − T_c,out" : "min(T_h,in − T_c,out, T_h,out − T_c,in)", $"{F(approach)} K", ScalarValue.Of(approach, kelvin), "Approach temperature difference");

        if (!(dt1 > 1e-6 && dt2 > 1e-6))
        {
            c.Warn("pinch", "A/Q_req: the temperatures pinch (an end difference is zero); the area is effectively infinite, so the LMTD cross-check is not shown.");
            c.CheckCondition("Approach against minimum", c.Si("dT_min") <= 0, "approach 0 K", $"minimum {F(c.Si("dT_min"))} K");
            Outputs(c, sizing);
            return;
        }

        var lm = Math.Abs(dt1 - dt2) < 1e-9 * Math.Max(1, Math.Abs(dt1)) ? dt1 : (dt1 - dt2) / Math.Log(dt1 / dt2);
        c.Record("dT_lm", parallel ? "(ΔT_1 − ΔT_2)/ln(ΔT_1/ΔT_2), parallel-flow ends" : "(ΔT_1 − ΔT_2)/ln(ΔT_1/ΔT_2), counterflow ends", $"ΔT_1 = {F(dt1)} K, ΔT_2 = {F(dt2)} K", ScalarValue.Of(lm, kelvin), "Log-mean temperature difference");

        var f = arrangement switch
        {
            ShellTube => ShellTubeF(thi, tho, tci, tco),
            Crossflow => q / (uf * area * lm),
            _ => 1.0,
        };

        c.Record("F", arrangement switch { ShellTube => "F(R, P), 1 shell pass", Crossflow => "Q / (U_f·A·ΔT_lm) (back-calculated)", _ => "1 (pure counterflow, parallel flow or C_r = 0)" }, $"R = {(tco - tci > 1e-12 ? F((thi - tho) / (tco - tci)) : "—")}, P = {F((tco - tci) / dtMax)}", new ScalarValue(f), "LMTD correction factor", "Incropera 7th ed. §11.3");

        if (arrangement == ShellTube && f < 0.75)
            c.Warn("low-F", $"arrangement: F = {CalcValue.FormatNumber(f, 3)} is below 0.75; the single shell pass is used inefficiently.");

        c.Record("Q_lmtd", "U_f·A·F·ΔT_lm", $"{F(uf)} × {F(area)} × {F(f)} × {F(lm)}", ScalarValue.Of(uf * area * f * lm / 1e3, kW), "Duty by the LMTD method (cross-check)");

        // The two methods describe one exchanger, so they must agree; a gap means the arrangement and the temperatures contradict each other.
        if (Math.Abs(uf * area * f * lm - q) > 0.02 * q)
            c.Warn("lmtd-mismatch", $"arrangement: the ε-NTU duty ({F(q / 1e3)} kW) and the LMTD duty ({F(uf * area * f * lm / 1e3)} kW) differ by more than 2 %; check the arrangement against the temperatures.");
        c.Assign("u_app", "dT_min / dT_app", null, "Approach utilisation (minimum / actual)");

        Outputs(c, sizing);
        c.Output("dT_lm", "Log-mean temperature difference");
        c.Output("Q_lmtd", "Duty by the LMTD method");
        c.Output("F", "LMTD correction factor");
        c.CheckUtilisation("Approach against minimum", "u_app");

        if (!sizing && c.Has("Q_req"))
        {
            c.Input("Q_req");
            c.Assign("u_duty", "Q_req / Q", null, "Duty utilisation (required / achieved)");
            c.Output("u_duty", "Duty utilisation");
            c.CheckUtilisation("Duty against required", "u_duty");
        }
    }

    private static void Outputs(CalcRunContext c, bool sizing)
    {
        foreach (var (symbol, text) in new[] { ("C_r", "Capacity ratio"), ("NTU", "Transfer units"), ("eps", "Effectiveness"), ("Q", "Heat duty"), ("T_h_out", "Hot outlet"), ("T_c_out", "Cold outlet"), ("dT_c", "Cold-side rise"), ("T_c_m", "Cold-side mean"), ("cp_c", "Cold-side specific heat"), ("dT_app", "Approach"), ("UA", "UA") })
            c.Output(symbol, text);

        if (sizing)
            c.Output("A_req", "Required area");
    }

    /// <summary>ε(NTU, C_r) for the arrangement (Incropera 7th ed. Table 11.3).</summary>
    public static double Effectiveness(string arrangement, double ntu, double cr)
    {
        switch (arrangement)
        {
            case Parallel:
                return (1 - Math.Exp(-ntu * (1 + cr))) / (1 + cr);
            case ShellTube:
            {
                var s = Math.Sqrt(1 + cr * cr);
                var x = Math.Exp(-ntu * s);
                return 2 / (1 + cr + s * (1 + x) / (1 - x));
            }
            case Crossflow:
                return cr < 1e-9 ? 1 - Math.Exp(-ntu) : 1 - Math.Exp(Math.Pow(ntu, 0.22) / cr * (Math.Exp(-cr * Math.Pow(ntu, 0.78)) - 1));
            case Isothermal:
                return 1 - Math.Exp(-ntu);
            default:
                if (Math.Abs(1 - cr) < 1e-9)
                    return ntu / (1 + ntu);

                var e = Math.Exp(-ntu * (1 - cr));
                return (1 - e) / (1 - cr * e);
        }
    }

    /// <summary>NTU(ε, C_r) (Incropera 7th ed. Table 11.4); crossflow by bisection on the forward relation.</summary>
    public static double InverseNtu(string arrangement, double eps, double cr)
    {
        switch (arrangement)
        {
            case Parallel:
                return -Math.Log(1 - eps * (1 + cr)) / (1 + cr);
            case ShellTube:
            {
                var s = Math.Sqrt(1 + cr * cr);
                var e = (2 / eps - (1 + cr)) / s;
                return -Math.Log((e - 1) / (e + 1)) / s;
            }
            case Crossflow:
            {
                double lo = 0, hi = 1e3;

                for (var i = 0; i < 200; i++)
                {
                    var mid = (lo + hi) / 2;

                    if (Effectiveness(arrangement, mid, cr) < eps)
                        lo = mid;
                    else
                        hi = mid;
                }

                return (lo + hi) / 2;
            }
            case Isothermal:
                return -Math.Log(1 - eps);
            default:
                return Math.Abs(1 - cr) < 1e-9 ? eps / (1 - eps) : Math.Log((eps - 1) / (eps * cr - 1)) / (cr - 1);
        }
    }

    private static string Relation(string arrangement) => arrangement switch
    {
        Parallel => "ε = (1 − exp(−NTU(1 + C_r)))/(1 + C_r)",
        ShellTube => "ε = 2/(1 + C_r + √(1 + C_r²)·(1 + e^(−NTU√(1+C_r²)))/(1 − e^(−NTU√(1+C_r²))))",
        Crossflow => "ε ≈ 1 − exp[(NTU^0.22/C_r)(exp(−C_r·NTU^0.78) − 1)]",
        Isothermal => "ε = 1 − exp(−NTU)",
        _ => "ε = (1 − exp(−NTU(1 − C_r)))/(1 − C_r·exp(−NTU(1 − C_r))); NTU/(1 + NTU) at C_r = 1",
    };

    /// <summary>F for one shell pass and an even number of tube passes, from R and P (temperatures in K).</summary>
    private static double ShellTubeF(double thi, double tho, double tci, double tco)
    {
        var p = (tco - tci) / (thi - tci);
        var r = (thi - tho) / (tco - tci);
        var s = Math.Sqrt(r * r + 1);

        if (Math.Abs(r - 1) < 1e-9)
            return p * Math.Sqrt(2) / (1 - p) / Math.Log((2 - p * (2 - Math.Sqrt(2))) / (2 - p * (2 + Math.Sqrt(2))));

        var f = s / (r - 1) * Math.Log((1 - p) / (1 - p * r)) / Math.Log((2 - p * (r + 1 - s)) / (2 - p * (r + 1 + s)));
        return double.IsFinite(f) ? f : 0;
    }

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
