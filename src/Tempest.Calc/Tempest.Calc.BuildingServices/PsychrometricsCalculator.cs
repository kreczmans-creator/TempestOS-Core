using Tempest.Calc.BuildingServices.Fluids;
using Tempest.Calc.Engine.Numerics;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.BuildingServices;

/// <summary>
/// B2 — psychrometrics: a moist-air state from dry-bulb plus one of
/// wet-bulb, relative humidity, humidity ratio, dew point or enthalpy at a
/// given pressure (or altitude); the processes sensible heating/cooling,
/// cooling with dehumidification, steam humidification, adiabatic
/// (evaporative) humidification and adiabatic mixing, with heat and water
/// balances; chart series for the saturation curve, RH lines and the
/// process line. Hyland–Wexler saturation pressure; ASHRAE ideal-gas
/// relations in own notation.
/// </summary>
public sealed class PsychrometricsCalculator : ICalcCalculator
{
    public const string Id = "bs.psychrometrics";

    public const string KnownWetBulb = "wet-bulb temperature";
    public const string KnownRh = "relative humidity";
    public const string KnownW = "humidity ratio";
    public const string KnownDewPoint = "dew point";
    public const string KnownEnthalpy = "enthalpy";

    public const string ProcessNone = "none (state only)";
    public const string ProcessSensible = "sensible heating / cooling";
    public const string ProcessCoolingDehum = "cooling with dehumidification";
    public const string ProcessSteam = "steam humidification";
    public const string ProcessEvaporative = "adiabatic (evaporative) humidification";
    public const string ProcessMixing = "adiabatic mixing";

    public CalcTemplate Template { get; } = new(
        Id, "1.3.0", CalcDiscipline.BuildingServices, "Psychrometrics",
        "The state of moist air (humidity ratio, relative humidity, dew point, wet-bulb, enthalpy, specific volume, density) from dry-bulb and one other property at a stated pressure or altitude, and one air-handling process from that state with its heat and moisture balance: sensible heating or cooling, cooling with dehumidification, steam humidification, adiabatic humidification, or mixing with a second stream.",
        "Saturation vapour pressure at the dry-bulb temperature, over water at or above 0 °C and over ice below: p_ws(t) by Hyland–Wexler.\nMoisture content (humidity ratio) of the air: W = 0.621945 p_w/(p − p_w).\nRelative humidity: φ = p_w/p_ws.\nSpecific enthalpy per kilogram of dry air: h = 1.006 t + W(2501 + 1.86 t) kJ/kg_da.\nSpecific volume: v = 0.287042 (t + 273.15)(1 + 1.607858 W)/p.\nDew point and wet-bulb temperatures, by bisection (wet-bulb from the adiabatic-saturation relation): p_ws(t_dp) = p_w.\nSensible heating or cooling at constant moisture content: Q = ṁ (h₂ − h₁).\nCooling with dehumidification, less the heat leaving in the condensate: Q_total = ṁ(h₁ − h₂) − ṁ_w h_w, with ṁ_w = ṁ(W₁ − W₂).\nSensible and latent split of the coil load, along the path t₁ → t₂ at W₁ then W₁ → W₂ at t₂: Q_sensible = ṁ(h₁ − h(t₂, W₁)); Q_latent = ṁ(h(t₂, W₁) − h₂); SHR = Q_sensible/(Q_sensible + Q_latent). With RH_2 blank and t₂ above the entering dew point the coil is dry: W₂ = W₁, no condensate, sensible only.\nSteam humidification: h₂ = h₁ + (W₂ − W₁) h_steam.\nAdiabatic (spray) humidification, at constant enthalpy: h₂ = h₁.\nMixing two airstreams, weighted by dry-air mass flow: W_mix = Σṁ W/Σṁ; h_mix = Σṁ h/Σṁ.",
        [
            new CalcInputSpec("altitude", "Altitude (used when pressure is blank)", CalcInputKind.Quantity, "m", Default: 0, Min: -500, Max: 5000, HardMin: -1000, HardMax: 10000, Group: "Pressure"),
            new CalcInputSpec("p", "Barometric pressure (blank = standard atmosphere at the altitude)", CalcInputKind.Quantity, "kPa", Min: 50, Max: 110, HardMin: 10, HardMax: 1000, IsOptional: true, Group: "Pressure"),
            new CalcInputSpec("t_db", "Dry-bulb temperature", CalcInputKind.Quantity, "degC", Default: 25, Min: -40, Max: 60, HardMin: MoistAir.MinT, HardMax: MoistAir.MaxT, Group: "State 1"),
            new CalcInputSpec("known", "Second known property", CalcInputKind.Choice, Choices: [KnownRh, KnownWetBulb, KnownW, KnownDewPoint, KnownEnthalpy], DefaultChoice: KnownRh, Group: "State 1"),
            new CalcInputSpec("RH", "Relative humidity", CalcInputKind.Number, Default: 50, Min: 0, Max: 100, HardMin: 0, HardMax: 100, IsOptional: true, Group: "State 1"),
            new CalcInputSpec("t_wb", "Wet-bulb temperature", CalcInputKind.Quantity, "degC", Min: -40, Max: 60, HardMin: MoistAir.MinT, HardMax: MoistAir.MaxT, IsOptional: true, Group: "State 1"),
            new CalcInputSpec("W", "Humidity ratio (g per kg dry air)", CalcInputKind.Number, Min: 0, Max: 60, HardMin: 0, HardMax: 1000, IsOptional: true, Group: "State 1"),
            new CalcInputSpec("t_dp", "Dew-point temperature", CalcInputKind.Quantity, "degC", Min: -40, Max: 60, HardMin: MoistAir.MinT, HardMax: MoistAir.MaxT, IsOptional: true, Group: "State 1"),
            new CalcInputSpec("h", "Specific enthalpy (kJ per kg dry air)", CalcInputKind.Number, Min: -50, Max: 400, HardMin: -200, HardMax: 2000, IsOptional: true, Group: "State 1"),
            new CalcInputSpec("m_da", "Dry-air mass flow", CalcInputKind.Quantity, "kg/s", Default: 1, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e5, Group: "Process"),
            new CalcInputSpec("process", "Process", CalcInputKind.Choice, Choices: [ProcessNone, ProcessSensible, ProcessCoolingDehum, ProcessSteam, ProcessEvaporative, ProcessMixing], DefaultChoice: ProcessNone, Group: "Process"),
            new CalcInputSpec("t_2", "Leaving dry-bulb (sensible, cooling)", CalcInputKind.Quantity, "degC", Min: -40, Max: 80, HardMin: MoistAir.MinT, HardMax: MoistAir.MaxT, IsOptional: true, Group: "Process"),
            new CalcInputSpec("RH_2", "Leaving relative humidity (cooling, evaporative)", CalcInputKind.Number, Min: 0, Max: 100, HardMin: 0, HardMax: 100, IsOptional: true, Group: "Process"),
            new CalcInputSpec("W_2", "Leaving humidity ratio, g/kg (steam)", CalcInputKind.Number, Min: 0, Max: 60, HardMin: 0, HardMax: 1000, IsOptional: true, Group: "Process"),
            new CalcInputSpec("h_steam", "Steam enthalpy (steam humidification)", CalcInputKind.Number, Default: 2676, Min: 2500, Max: 3000, HardMin: 0, HardMax: 5000, Group: "Process"),
            new CalcInputSpec("eff", "Saturation efficiency (evaporative, blank = use RH_2)", CalcInputKind.Number, Min: 0, Max: 1, HardMin: 0, HardMax: 1, IsOptional: true, Group: "Process"),
            new CalcInputSpec("t_db_b", "Second stream dry-bulb (mixing)", CalcInputKind.Quantity, "degC", Min: -40, Max: 60, HardMin: MoistAir.MinT, HardMax: MoistAir.MaxT, IsOptional: true, Group: "Mixing"),
            new CalcInputSpec("RH_b", "Second stream relative humidity (mixing)", CalcInputKind.Number, Min: 0, Max: 100, HardMin: 0, HardMax: 100, IsOptional: true, Group: "Mixing"),
            new CalcInputSpec("m_da_b", "Second stream dry-air mass flow (mixing)", CalcInputKind.Quantity, "kg/s", Min: 0, Max: 1000, HardMin: 0, HardMax: 1e5, IsOptional: true, Group: "Mixing"),
        ],
        [
            new CalcAssumption("Moist air is an ideal mixture of ideal gases; the enhancement factor is taken as 1.", "ASHRAE Fundamentals Ch. 1 ideal-gas relations."),
            new CalcAssumption("Condensate leaves at the coil leaving dry-bulb; steam adds enthalpy h_steam at negligible mass of dry air.", "Process conventions."),
            new CalcAssumption("Adiabatic humidification follows a line of constant enthalpy (close to constant wet-bulb).", "Evaporative process idealisation."),
        ],
        [
            new CalcClauseRef("ASHRAE Handbook — Fundamentals", "Ch. 1", "Psychrometrics (ideal-gas relations, processes)"),
            new CalcClauseRef("Hyland & Wexler, ASHRAE Trans. 89(2A), 1983", "—", "Saturation pressure of water and ice"),
            new CalcClauseRef("CIBSE Guide C", "Section 1", "Properties of humid air (principles)"),
        ],
        [
            new CalcApplicabilityRule("second-property", "The chosen second property has no value.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("supersaturated", "The state lies above saturation (RH > 100 %).", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("process-input", "The process needs a leaving-state value that was not given, or it is not reachable.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("no-dehumidification", "The mixture is supersaturated: fog forms and the state shown is nominal.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("dry-coil", "Cooling with dehumidification and RH_2 blank, but t_2 is above the entering dew point: the coil is dry, the leaving moisture is held at the entering value and the load is sensible only.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-saturation-20", "Saturated air at 20 °C, 101.325 kPa",
                new CalcReferenceSource("ASHRAE Handbook — Fundamentals", "2017", "Ch. 1 Table 3 (thermodynamic properties of moist air at standard pressure), 20 °C: p_ws = 2.3392 kPa, W_s = 14.758 g/kg, h_s = 57.555 kJ/kg, v_s = 0.8498 m³/kg. The table includes the enhancement factor (≈ 1.004 at 20 °C); this sheet takes it as 1 (assumption A1), so W and h read ≈ 0.4 % lower"),
                [CalcInputValue.Quantity("t_db", 20, "degC"), CalcInputValue.Choice("known", KnownRh), CalcInputValue.Of("RH", 100), CalcInputValue.Quantity("altitude", 0, "m"), CalcInputValue.Choice("process", ProcessNone)],
                [new CalcExpectedValue("p_ws_1", 2.3392, "kPa", 1e-3), new CalcExpectedValue("W_1", 14.758, "", 5e-3), new CalcExpectedValue("h_1", 57.555, "", 5e-3), new CalcExpectedValue("v_1", 0.8498, "m^3/kg", 2e-3), new CalcExpectedValue("t_dp_1", 20, "degC", 1e-4), new CalcExpectedValue("t_wb_1", 20, "degC", 1e-4)]),
            new CalcTestCase("ka-ashrae-example-1", "40 °C dry-bulb, 20 °C wet-bulb, sea level (ASHRAE Ch. 1 Example 1 style)",
                new CalcReferenceSource("ASHRAE Handbook — Fundamentals", "2017", "Ch. 1 Example 1: t = 40 °C, t* = 20 °C, p = 101.325 kPa → W ≈ 6.5 g/kg, h ≈ 56.7 kJ/kg, v ≈ 0.896 m³/kg, t_d ≈ 7 °C, φ ≈ 14 % (values here from the same relations, to be confirmed against the printed example)"),
                [CalcInputValue.Quantity("t_db", 40, "degC"), CalcInputValue.Choice("known", KnownWetBulb), CalcInputValue.Quantity("t_wb", 20, "degC"), CalcInputValue.Quantity("altitude", 0, "m"), CalcInputValue.Choice("process", ProcessNone)],
                [new CalcExpectedValue("W_1", 6.40, "", 0.02), new CalcExpectedValue("h_1", 56.6, "", 0.01), new CalcExpectedValue("v_1", 0.897, "m^3/kg", 0.005), new CalcExpectedValue("RH_1", 13.9, "", 0.03), new CalcExpectedValue("t_dp_1", 7.4, "degC", 0.05)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-sensible-heating", "Sensible heating 10 → 30 °C at W = 5 g/kg, 1 kg/s (analytic)",
                new CalcReferenceSource("Analytic from h = 1.006 t + W(2501 + 1.86 t)", null, "Q = ṁ [1.006 (30 − 10) + 0.005 × 1.86 × 20] = 20.306 kW"),
                [CalcInputValue.Quantity("t_db", 10, "degC"), CalcInputValue.Choice("known", KnownW), CalcInputValue.Of("W", 5), CalcInputValue.Quantity("altitude", 0, "m"), CalcInputValue.Quantity("m_da", 1, "kg/s"), CalcInputValue.Choice("process", ProcessSensible), CalcInputValue.Quantity("t_2", 30, "degC")],
                [new CalcExpectedValue("Q", 20.306, "kW", 1e-6), new CalcExpectedValue("W_2", 5, "", 1e-9), new CalcExpectedValue("t_2_out", 30, "degC", 1e-9)]),
            new CalcTestCase("ka-mixing", "Adiabatic mixing of 2 kg/s at 30 °C, 10 g/kg with 1 kg/s at 15 °C, 5 g/kg (analytic)",
                new CalcReferenceSource("Analytic mass and energy balance", null, "W₃ = (2×10 + 1×5)/3 = 8.333 g/kg; h₃ = (2 h_a + h_b)/3 with h_a = 55.74, h_b = 27.74 → 46.41 kJ/kg; t₃ = (h₃ − 2501 W₃)/(1.006 + 1.86 W₃) = 25.07 °C"),
                [CalcInputValue.Quantity("t_db", 30, "degC"), CalcInputValue.Choice("known", KnownW), CalcInputValue.Of("W", 10), CalcInputValue.Quantity("altitude", 0, "m"), CalcInputValue.Quantity("m_da", 2, "kg/s"), CalcInputValue.Choice("process", ProcessMixing), CalcInputValue.Quantity("t_db_b", 15, "degC"), CalcInputValue.Of("RH_b", 46.93), CalcInputValue.Quantity("m_da_b", 1, "kg/s")],
                [new CalcExpectedValue("W_2", 8.333, "", 2e-3), new CalcExpectedValue("h_2", 46.41, "", 2e-3), new CalcExpectedValue("t_2_out", 25.07, "degC", 2e-3)]),
        ],
        DiagramKey: "psychrometric-chart",
        Category: "Air");

    public void Run(CalcRunContext c)
    {
        c.Input("altitude");
        double p;

        if (c.Has("p"))
        {
            c.Input("p");
            p = c.Si("p") / 1e3;
        }
        else
        {
            c.Record("p", "101.325 (1 − 2.25577e-5 z)^5.2559", $"z = {F(c.Si("altitude"))} m", ScalarValue.Of(MoistAir.PressureAtAltitude(c.Si("altitude")), "kPa"), "Barometric pressure (standard atmosphere)", "ASHRAE Ch. 1 eq. 3");
            p = c.Si("p") / 1e3;
        }

        c.Input("t_db");
        var t1 = c.Si("t_db") - 273.15;
        var known = c.InputText("known");
        MoistAirState s1;

        switch (known)
        {
            case KnownRh:
                if (!c.Has("RH") && c.Template.FindInput("RH")!.Default is null) { c.Block("second-property", "RH: give the relative humidity."); return; }
                c.Input("RH");
                s1 = MoistAir.FromRelativeHumidity(t1, c.Si("RH") / 100, p);
                break;
            case KnownWetBulb:
                if (!c.Has("t_wb")) { c.Block("second-property", "t_wb: give the wet-bulb temperature."); return; }
                c.Input("t_wb");
                if (c.Si("t_wb") - 273.15 > t1 + 1e-9) { c.Block("supersaturated", "t_wb: the wet-bulb cannot exceed the dry-bulb."); return; }
                s1 = MoistAir.FromWetBulb(t1, c.Si("t_wb") - 273.15, p);
                break;
            case KnownW:
                if (!c.Has("W")) { c.Block("second-property", "W: give the humidity ratio."); return; }
                c.Input("W");
                s1 = new MoistAirState(t1, c.Si("W") / 1000, p);
                break;
            case KnownDewPoint:
                if (!c.Has("t_dp")) { c.Block("second-property", "t_dp: give the dew point."); return; }
                c.Input("t_dp");
                if (c.Si("t_dp") - 273.15 > t1 + 1e-9) { c.Block("supersaturated", "t_dp: the dew point cannot exceed the dry-bulb."); return; }
                s1 = MoistAir.FromDewPoint(t1, c.Si("t_dp") - 273.15, p);
                break;
            default:
                if (!c.Has("h")) { c.Block("second-property", "h: give the enthalpy."); return; }
                c.Input("h");
                s1 = MoistAir.FromEnthalpy(t1, c.Si("h"), p);
                break;
        }

        if (s1.W < 0) { c.Block("second-property", $"{SymbolFor(known)}: the value gives a negative humidity ratio."); return; }
        if (s1.RelativeHumidity > 1 + 1e-6) { c.Block("supersaturated", $"{SymbolFor(known)}: the state is above saturation (RH = {F(s1.RelativeHumidity * 100)} %)."); return; }

        WriteState(c, "1", s1, "entering");

        c.Input("m_da");
        var mda = c.Si("m_da");
        var process = c.InputText("process");
        MoistAirState? s2 = null;

        switch (process)
        {
            case ProcessSensible:
            {
                if (!c.Has("t_2")) { c.Block("process-input", "t_2: give the leaving dry-bulb."); return; }
                c.Input("t_2");
                s2 = new MoistAirState(c.Si("t_2") - 273.15, s1.W, p);
                if (s2.RelativeHumidity > 1 + 1e-6) { c.Block("process-input", "t_2: cooling below the dew point at constant moisture is not sensible cooling; use cooling with dehumidification."); return; }
                WriteState(c, "2", s2, "leaving");
                c.Record("Q", "ṁ (h₂ − h₁)", $"{F(mda)} × ({F(s2.Enthalpy)} − {F(s1.Enthalpy)})", ScalarValue.Of(mda * (s2.Enthalpy - s1.Enthalpy), "kW"), "Heat added (+) or removed (−)");
                c.Output("Q", "Heat");
                break;
            }

            case ProcessCoolingDehum:
            {
                if (!c.Has("t_2")) { c.Block("process-input", "t_2: give the coil leaving dry-bulb."); return; }
                c.Input("t_2");
                var rh2Given = c.Has("RH_2");
                var rh2 = rh2Given ? c.InputSi("RH_2") / 100 : 1.0;
                s2 = MoistAir.FromRelativeHumidity(c.Si("t_2") - 273.15, rh2, p);

                if (s2.W > s1.W + 1e-12)
                {
                    if (rh2Given)
                    {
                        c.Block("process-input", $"RH_2: a coil cannot add moisture; the leaving state ({F(s2.W * 1000)} g/kg) is wetter than the entering state ({F(s1.W * 1000)} g/kg). Lower RH_2 or t_2, or use sensible cooling.");
                        return;
                    }

                    // RH_2 blank: the coil surface is above the entering dew point, so the coil is dry and the air leaves at the entering moisture.
                    s2 = new MoistAirState(s2.T, s1.W, p);
                    c.Warn("dry-coil", "t_2: the leaving temperature is above the entering dew point, so the coil is dry; no condensate forms, the leaving moisture is held at the entering value and the load is sensible only.");
                }

                WriteState(c, "2", s2, "leaving");
                var mw = Math.Max(0, mda * (s1.W - s2.W));
                var hMid = MoistAir.Enthalpy(s2.T, s1.W);
                c.Record("m_w", "ṁ (W₁ − W₂)", $"{F(mda)} × ({F(s1.W * 1000)} − {F(s2.W * 1000)}) g/kg", ScalarValue.Of(mw, "kg/s"), "Condensate");
                c.Record("h_w", "4.186 t₂ (condensate enthalpy)", $"4.186 × {F(s2.T)}", new ScalarValue(4.186 * s2.T), "Condensate enthalpy, kJ/kg");
                c.Record("Q_total", "ṁ (h₁ − h₂) − ṁ_w h_w", $"{F(mda)} × ({F(s1.Enthalpy)} − {F(s2.Enthalpy)}) − {F(mw)} × {F(4.186 * s2.T)}", ScalarValue.Of(mda * (s1.Enthalpy - s2.Enthalpy) - mw * 4.186 * s2.T, "kW"), "Total cooling load");
                c.Record("Q_sensible", "ṁ (h₁ − h(t₂, W₁))", $"{F(mda)} × ({F(s1.Enthalpy)} − {F(hMid)})", ScalarValue.Of(mda * (s1.Enthalpy - hMid), "kW"), "Sensible part: the air cooled from t₁ to t₂ at the entering humidity ratio W₁");
                c.Record("Q_latent", "ṁ (h(t₂, W₁) − h₂)", $"{F(mda)} × ({F(hMid)} − {F(s2.Enthalpy)})", ScalarValue.Of(mda * (hMid - s2.Enthalpy), "kW"), "Latent part: the moisture removed from W₁ to W₂ at t₂");
                c.Record("SHR", "Q_sensible / (Q_sensible + Q_latent)", "", new ScalarValue(mda * (s1.Enthalpy - hMid) / Math.Max(1e-12, mda * (s1.Enthalpy - s2.Enthalpy))), "Sensible heat ratio");
                foreach (var o in new[] { ("m_w", "Condensate"), ("Q_total", "Total cooling"), ("Q_sensible", "Sensible cooling"), ("Q_latent", "Latent cooling"), ("SHR", "Sensible heat ratio") }) c.Output(o.Item1, o.Item2);
                break;
            }

            case ProcessSteam:
            {
                if (!c.Has("W_2")) { c.Block("process-input", "W_2: give the leaving humidity ratio."); return; }
                c.Input("W_2");
                c.Input("h_steam");
                var w2 = c.Si("W_2") / 1000;
                if (w2 < s1.W) { c.Block("process-input", "W_2: steam humidification cannot lower the humidity ratio."); return; }
                var h2 = s1.Enthalpy + (w2 - s1.W) * c.Si("h_steam");
                s2 = new MoistAirState(MoistAir.TemperatureFromEnthalpy(h2, w2), w2, p);
                if (s2.RelativeHumidity > 1 + 1e-6) { c.Block("process-input", "W_2: the leaving state is above saturation."); return; }
                WriteState(c, "2", s2, "leaving");
                c.Record("m_s", "ṁ (W₂ − W₁)", $"{F(mda)} × ({F(w2 * 1000)} − {F(s1.W * 1000)}) g/kg", ScalarValue.Of(mda * (w2 - s1.W), "kg/s"), "Steam added");
                c.Record("Q", "ṁ_s h_steam", $"{F(mda * (w2 - s1.W))} × {F(c.Si("h_steam"))}", ScalarValue.Of(mda * (w2 - s1.W) * c.Si("h_steam"), "kW"), "Enthalpy added by the steam");
                c.Output("m_s", "Steam added");
                c.Output("Q", "Heat added by steam");
                break;
            }

            case ProcessEvaporative:
            {
                double t2;
                var h1 = s1.Enthalpy;

                if (c.Has("eff"))
                {
                    c.Input("eff");
                    t2 = s1.T - c.Si("eff") * (s1.T - s1.WetBulb);
                }
                else if (c.Has("RH_2"))
                {
                    c.Input("RH_2");
                    var target = c.Si("RH_2") / 100;
                    if (target < s1.RelativeHumidity) { c.Block("process-input", "RH_2: the target is below the entering relative humidity."); return; }
                    t2 = RootFinding.Bisect(t => MoistAir.FromEnthalpy(t, h1, p).RelativeHumidity - target, s1.WetBulb, s1.T, 1e-10, 200);
                }
                else
                {
                    c.Block("process-input", "RH_2/eff: give the target relative humidity or the saturation efficiency.");
                    return;
                }

                s2 = MoistAir.FromEnthalpy(t2, h1, p);
                WriteState(c, "2", s2, "leaving");
                c.Record("m_w_add", "ṁ (W₂ − W₁)", $"{F(mda)} × ({F(s2.W * 1000)} − {F(s1.W * 1000)}) g/kg", ScalarValue.Of(mda * (s2.W - s1.W), "kg/s"), "Water evaporated");
                c.Record("eff_out", "(t₁ − t₂)/(t₁ − t_wb)", $"({F(s1.T)} − {F(s2.T)}) / ({F(s1.T)} − {F(s1.WetBulb)})", new ScalarValue((s1.T - s2.T) / Math.Max(1e-12, s1.T - s1.WetBulb)), "Saturation efficiency achieved");
                c.Output("m_w_add", "Water evaporated");
                c.Output("eff_out", "Saturation efficiency");
                break;
            }

            case ProcessMixing:
            {
                if (!c.Has("t_db_b") || !c.Has("RH_b") || !c.Has("m_da_b")) { c.Block("process-input", "t_db_b/RH_b/m_da_b: give the second stream's dry-bulb, relative humidity and mass flow."); return; }
                c.Input("t_db_b"); c.Input("RH_b"); c.Input("m_da_b");
                var sb = MoistAir.FromRelativeHumidity(c.Si("t_db_b") - 273.15, c.Si("RH_b") / 100, p);
                var mb = c.Si("m_da_b");
                WriteState(c, "b", sb, "second stream");
                var total = mda + mb;
                if (total <= 0) { c.Block("process-input", "m_da/m_da_b: the mixed mass flow is zero."); return; }
                var w3 = (mda * s1.W + mb * sb.W) / total;
                var h3 = (mda * s1.Enthalpy + mb * sb.Enthalpy) / total;
                s2 = new MoistAirState(MoistAir.TemperatureFromEnthalpy(h3, w3), w3, p);
                c.Record("m_da_3", "ṁ_a + ṁ_b", $"{F(mda)} + {F(mb)}", ScalarValue.Of(total, "kg/s"), "Mixed dry-air mass flow");
                WriteState(c, "2", s2, "mixed");
                if (s2.RelativeHumidity > 1 + 1e-6) c.Warn("no-dehumidification", "t_db_b: the mixture is supersaturated; fog forms and the state shown is nominal.");
                c.Output("m_da_3", "Mixed mass flow");
                break;
            }
        }

        // Chart series.
        c.Series(new CalcSeries("Saturation", "t", "degC", "W", "g/kg", Enumerable.Range(-10, 61).Select(t => ((double)t, MoistAir.SaturationHumidityRatio(t, p) * 1000)).ToList()));

        foreach (var rh in new[] { 0.2, 0.4, 0.6, 0.8 })
            c.Series(new CalcSeries($"RH {rh * 100:0} %", "t", "degC", "W", "g/kg", Enumerable.Range(-10, 61).Select(t => ((double)t, MoistAir.FromRelativeHumidity(t, rh, p).W * 1000)).ToList()));

        var line = new List<(double, double)> { (s1.T, s1.W * 1000) };
        if (s2 is not null) line.Add((s2.T, s2.W * 1000));
        c.Series(new CalcSeries("Process", "t", "degC", "W", "g/kg", line));
    }

    private static void WriteState(CalcRunContext c, string k, MoistAirState s, string label)
    {
        var degC = Engine.Units.UnitCatalog.Parse("degC");
        c.Record($"p_ws_{k}", $"p_ws(t) Hyland–Wexler ({label})", $"t = {F(s.T)} °C", ScalarValue.Of(s.Pws, "kPa"), $"Saturation pressure, state {k}");
        c.Record($"p_w_{k}", "p W/(0.621945 + W)", $"W = {F(s.W)}", ScalarValue.Of(s.Pw, "kPa"), $"Vapour pressure, state {k}");
        c.Record($"t_{k}_out", $"dry-bulb ({label})", "", ScalarValue.Of(s.T, degC), $"Dry-bulb, state {k}");
        c.Record($"W_{k}", "0.621945 p_w/(p − p_w)", $"{F(s.Pw)} / ({F(s.P)} − {F(s.Pw)})", new ScalarValue(s.W * 1000), $"Humidity ratio, state {k} (g/kg dry air)");
        c.Record($"RH_{k}", "p_w / p_ws", $"{F(s.Pw)} / {F(s.Pws)}", new ScalarValue(s.RelativeHumidity * 100), $"Relative humidity, state {k} (%)");
        c.Record($"phi_{k}", "p_w / p_ws", $"{F(s.Pw)} / {F(s.Pws)}", ScalarValue.Of(s.RelativeHumidity * 100, "%"), $"Relative humidity, state {k}, as a percentage quantity (the same value as RH_{k}; a plain-number output cannot be linked to a percentage input)");
        c.Record($"t_dp_{k}", "p_ws(t_dp) = p_w, bisection", $"p_w = {F(s.Pw)} kPa", ScalarValue.Of(s.DewPoint, degC), $"Dew point, state {k}");
        c.Record($"t_wb_{k}", "adiabatic-saturation relation, bisection", $"t = {F(s.T)}, W = {F(s.W)}", ScalarValue.Of(s.WetBulb, degC), $"Wet-bulb, state {k}");
        c.Record($"h_{k}", "1.006 t + W (2501 + 1.86 t)", $"1.006 × {F(s.T)} + {F(s.W)} × (2501 + 1.86 × {F(s.T)})", new ScalarValue(s.Enthalpy), $"Enthalpy, state {k} (kJ/kg dry air)");
        c.Record($"v_{k}", "0.287042 (t + 273.15)(1 + 1.607858 W)/p", $"p = {F(s.P)} kPa", ScalarValue.Of(s.SpecificVolume, "m^3/kg"), $"Specific volume, state {k} (per kg dry air)");
        c.Record($"rho_{k}", "(1 + W)/v", "", ScalarValue.Of(s.Density, "kg/m^3"), $"Moist-air density, state {k}");

        foreach (var (symbol, text) in new[] { ($"t_{k}_out", $"Dry-bulb {k}"), ($"p_ws_{k}", $"p_ws {k}"), ($"p_w_{k}", $"p_w {k}"), ($"W_{k}", $"W {k} (g/kg)"), ($"RH_{k}", $"RH {k} (%)"), ($"phi_{k}", $"φ {k} (RH as a % quantity, for linking)"), ($"t_dp_{k}", $"Dew point {k}"), ($"t_wb_{k}", $"Wet-bulb {k}"), ($"h_{k}", $"h {k} (kJ/kg)"), ($"v_{k}", $"v {k}"), ($"rho_{k}", $"ρ {k}") })
            c.Output(symbol, text);
    }

    private static string SymbolFor(string known) => known switch
    {
        KnownWetBulb => "t_wb",
        KnownW => "W",
        KnownDewPoint => "t_dp",
        KnownEnthalpy => "h",
        _ => "RH",
    };

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
