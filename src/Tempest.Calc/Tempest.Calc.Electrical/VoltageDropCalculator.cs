using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Electrical;

/// <summary>
/// E3 — voltage drop and I²R loss for DC, single-phase and three-phase
/// circuits from conductor resistivity (temperature corrected) and area,
/// from R and X per kilometre, or from a mV/A/m figure; parallel
/// conductors; power factor; limits as user inputs; a conductor
/// temperature estimate from the load against the tabulated capacity.
/// </summary>
public sealed class VoltageDropCalculator : ICalcCalculator
{
    public const string Id = "elec.voltage-drop";

    public const string SystemDc = "DC";
    public const string SystemSingle = "AC single-phase";
    public const string SystemThree = "AC three-phase";

    public const string DataResistivity = "resistivity and area";
    public const string DataPerKm = "R and X per km";
    public const string DataMvAm = "mV/A/m figure";

    public CalcTemplate Template { get; } = new(
        Id, "1.0.1", CalcDiscipline.Electrical, "Voltage drop and I²R loss",
        "Voltage drop along a circuit in volts and per cent of the nominal voltage, the conductor power loss, and an estimate of the conductor operating temperature, for DC, single-phase and three-phase AC, from resistivity and cross-section (with temperature correction), from tabulated R and X per kilometre, or from a mV/A/m figure; parallel conductors per phase; the drop limit is a user input (the regulatory limits live in BS 7671 Appendix 4 and Regulation 525 and are not embedded).",
        "Conductor resistance at its operating temperature, for n conductors in parallel: R = ρ_T L/(A n), with ρ_T = ρ_20 [1 + α (T_c − 20)].\nResistance and reactance from per-length figures instead: R = R′ L/n; X = X′ L/n.\nVoltage drop on a DC circuit: ΔV = 2 I R.\nVoltage drop on a single-phase AC circuit: ΔV = 2 I (R cos φ + X sin φ).\nVoltage drop on a three-phase AC circuit: ΔV = √3 I (R cos φ + X sin φ).\nVoltage drop from a tabulated mV/A/m figure, which already carries the 2 or √3: ΔV = (mV/A/m) I L / 1000.\nPower lost in the cable: P_loss = 2 I² R (DC and single-phase); 3 I² R (three-phase).\nEstimated conductor temperature at this current: T_c,est = T_amb + (T_rated − T_amb)(I/I_z)².",
        [
            new CalcInputSpec("system", "System", CalcInputKind.Choice, Choices: [SystemThree, SystemSingle, SystemDc], DefaultChoice: SystemThree, Group: "Circuit"),
            new CalcInputSpec("V_n", "Nominal voltage (line-to-line for three-phase)", CalcInputKind.Quantity, "V", Default: 400, Min: 12, Max: 1000, HardMin: 0.1, HardMax: 1e6, Group: "Circuit"),
            new CalcInputSpec("I", "Design current", CalcInputKind.Quantity, "A", Default: 100, Min: 0.1, Max: 5000, HardMin: 1e-6, HardMax: 1e6, Group: "Circuit"),
            new CalcInputSpec("L", "Route length (one way)", CalcInputKind.Quantity, "m", Default: 80, Min: 0.1, Max: 5000, HardMin: 1e-3, HardMax: 1e6, Group: "Circuit"),
            new CalcInputSpec("cos_phi", "Power factor (AC)", CalcInputKind.Number, Default: 0.85, Min: 0.5, Max: 1, HardMin: 0.01, HardMax: 1, Group: "Circuit"),
            new CalcInputSpec("n_par", "Parallel conductors per phase", CalcInputKind.Number, Default: 1, Min: 1, Max: 6, HardMin: 1, HardMax: 20, Group: "Circuit"),
            new CalcInputSpec("data", "Conductor data from", CalcInputKind.Choice, Choices: [DataResistivity, DataPerKm, DataMvAm], DefaultChoice: DataResistivity, Group: "Conductor"),
            new CalcInputSpec("rho_20", "Resistivity at 20 °C (copper 0.01724, aluminium 0.0282 Ω·mm²/m)", CalcInputKind.Quantity, "ohm*mm^2/m", Default: 0.01724, Min: 0.01, Max: 0.05, HardMin: 1e-4, HardMax: 10, IsOptional: true, Group: "Conductor"),
            new CalcInputSpec("alpha", "Temperature coefficient of resistance", CalcInputKind.Number, Default: 0.00393, Min: 0.003, Max: 0.0045, HardMin: 0, HardMax: 0.02, Group: "Conductor"),
            new CalcInputSpec("A_c", "Conductor cross-section", CalcInputKind.Quantity, "mm^2", Default: 35, Min: 1, Max: 1000, HardMin: 0.01, HardMax: 1e5, IsOptional: true, Group: "Conductor"),
            new CalcInputSpec("T_c", "Conductor operating temperature for the resistance", CalcInputKind.Quantity, "degC", Default: 70, Min: 20, Max: 90, HardMin: -50, HardMax: 300, Group: "Conductor"),
            new CalcInputSpec("R_km", "Resistance per km per conductor (tabulated, at its stated temperature)", CalcInputKind.Quantity, "ohm/km", Min: 0.01, Max: 50, HardMin: 1e-6, HardMax: 1e4, IsOptional: true, Group: "Conductor"),
            new CalcInputSpec("X_km", "Reactance per km per conductor", CalcInputKind.Quantity, "ohm/km", Default: 0.08, Min: 0, Max: 1, HardMin: 0, HardMax: 100, Group: "Conductor"),
            new CalcInputSpec("mVAm", "Voltage drop figure (mV/A/m, from the cable table)", CalcInputKind.Number, Min: 0.1, Max: 100, HardMin: 1e-3, HardMax: 1e4, IsOptional: true, Group: "Conductor"),
            new CalcInputSpec("dv_limit", "Voltage-drop limit", CalcInputKind.Number, Default: 5, Min: 1, Max: 10, HardMin: 0.01, HardMax: 100, Group: "Limits"),
            new CalcInputSpec("I_z", "Tabulated current-carrying capacity for the installed conditions (temperature estimate)", CalcInputKind.Quantity, "A", Min: 1, Max: 5000, HardMin: 1e-3, HardMax: 1e6, IsOptional: true, Group: "Limits"),
            new CalcInputSpec("T_rated", "Conductor rated temperature", CalcInputKind.Quantity, "degC", Default: 70, Min: 60, Max: 90, HardMin: 0, HardMax: 300, Group: "Limits"),
            new CalcInputSpec("T_amb", "Ambient temperature", CalcInputKind.Quantity, "degC", Default: 30, Min: 0, Max: 60, HardMin: -60, HardMax: 150, Group: "Limits"),
        ],
        [
            new CalcAssumption("Balanced load; conductor reactance from the stated X′ (zero when using resistivity alone unless X_km is given); the drop is the arithmetic (R cos φ + X sin φ) form.", "Standard engineering approximation."),
            new CalcAssumption("A mV/A/m figure is used as tabulated, with its own temperature and system convention.", "Cable table convention."),
            new CalcAssumption("The conductor temperature estimate scales the rated rise with (I/I_z)²; I_z is the user's tabulated value for the installation method and corrections.", "BS 7671 Appendix 4 approach, formula only."),
        ],
        [
            new CalcClauseRef("BS 7671", "525", "Voltage drop in consumers' installations"),
            new CalcClauseRef("BS 7671", "Appendix 4, section 6", "Voltage drop (mV/A/m tables and the temperature-correction method)"),
            new CalcClauseRef("Hughes, Electrical and Electronic Technology", "Ch. on DC and AC circuits", "Resistance, resistivity and temperature coefficient"),
        ],
        [
            new CalcApplicabilityRule("conductor-data", "The chosen conductor-data route needs values that were not given.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("overload", "The design current exceeds the stated current-carrying capacity.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("temperature", "The estimated conductor temperature is above the temperature used for the resistance; re-run with T_c raised.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-dc", "DC 48 V, 20 A, 30 m, 6 mm² copper at 70 °C (analytic)",
                new CalcReferenceSource("Analytic (Hughes, resistivity with temperature coefficient)", null, "ρ_70 = 0.01724 × (1 + 0.00393 × 50) = 0.020628 Ω·mm²/m; R = 0.020628 × 30/6 = 0.10314 Ω; ΔV = 2 × 20 × 0.10314 = 4.1256 V = 8.595 %; loss 2 I² R = 82.51 W"),
                [CalcInputValue.Choice("system", SystemDc), CalcInputValue.Quantity("V_n", 48, "V"), CalcInputValue.Quantity("I", 20, "A"), CalcInputValue.Quantity("L", 30, "m"), CalcInputValue.Of("n_par", 1), CalcInputValue.Choice("data", DataResistivity), CalcInputValue.Quantity("rho_20", 0.01724, "ohm*mm^2/m"), CalcInputValue.Of("alpha", 0.00393), CalcInputValue.Quantity("A_c", 6, "mm^2"), CalcInputValue.Quantity("T_c", 70, "degC"), CalcInputValue.Quantity("X_km", 0, "ohm/km"), CalcInputValue.Of("dv_limit", 5)],
                [new CalcExpectedValue("R", 0.10314, "ohm", 1e-4), new CalcExpectedValue("dV", 4.1256, "V", 1e-4), new CalcExpectedValue("dV_pct", 8.595, "", 1e-3), new CalcExpectedValue("P_loss", 82.51, "W", 1e-3)]),
            new CalcTestCase("ka-three-phase", "Three-phase 400 V, 100 A, 80 m, 35 mm² copper, X′ = 0.08 Ω/km, cos φ = 0.85 (analytic)",
                new CalcReferenceSource("Analytic (√3 I (R cos φ + X sin φ))", null, "R = 0.020628 × 80/35 = 0.047150 Ω; X = 0.0064 Ω; ΔV = 1.7321 × 100 × (0.047150 × 0.85 + 0.0064 × 0.5268) = 7.526 V = 1.881 %; loss 3 I² R = 1414.5 W"),
                [CalcInputValue.Choice("system", SystemThree), CalcInputValue.Quantity("V_n", 400, "V"), CalcInputValue.Quantity("I", 100, "A"), CalcInputValue.Quantity("L", 80, "m"), CalcInputValue.Of("cos_phi", 0.85), CalcInputValue.Of("n_par", 1), CalcInputValue.Choice("data", DataResistivity), CalcInputValue.Quantity("rho_20", 0.01724, "ohm*mm^2/m"), CalcInputValue.Of("alpha", 0.00393), CalcInputValue.Quantity("A_c", 35, "mm^2"), CalcInputValue.Quantity("T_c", 70, "degC"), CalcInputValue.Quantity("X_km", 0.08, "ohm/km"), CalcInputValue.Of("dv_limit", 5), CalcInputValue.Quantity("I_z", 119, "A"), CalcInputValue.Quantity("T_rated", 70, "degC"), CalcInputValue.Quantity("T_amb", 30, "degC")],
                [new CalcExpectedValue("R", 0.047150, "ohm", 1e-4), new CalcExpectedValue("dV", 7.526, "V", 1e-3), new CalcExpectedValue("dV_pct", 1.8815, "", 1e-3), new CalcExpectedValue("P_loss", 1414.5, "W", 1e-3), new CalcExpectedValue("T_c_est", 58.25, "degC", 1e-3)]),
            new CalcTestCase("ka-mvam", "Three-phase from a 2.4 mV/A/m table figure, 63 A, 50 m (analytic)",
                new CalcReferenceSource("Analytic (BS 7671 Appendix 4 method, formula only)", null, "ΔV = 2.4 × 63 × 50 / 1000 = 7.56 V = 1.89 % of 400 V"),
                [CalcInputValue.Choice("system", SystemThree), CalcInputValue.Quantity("V_n", 400, "V"), CalcInputValue.Quantity("I", 63, "A"), CalcInputValue.Quantity("L", 50, "m"), CalcInputValue.Of("n_par", 1), CalcInputValue.Choice("data", DataMvAm), CalcInputValue.Of("mVAm", 2.4), CalcInputValue.Of("dv_limit", 5)],
                [new CalcExpectedValue("dV", 7.56, "V", 1e-9), new CalcExpectedValue("dV_pct", 1.89, "", 1e-9)]),
        ],
        DiagramKey: "voltage-drop",
        Category: "Power");

    public void Run(CalcRunContext c)
    {
        var system = c.InputText("system");
        var data = c.InputText("data");
        foreach (var s in new[] { "V_n", "I", "L", "cos_phi", "n_par", "dv_limit" })
            c.Input(s);

        var ac = system != SystemDc;
        c.Assign("sin_phi", ac ? "sqrt(1 - cos_phi^2)" : "0", null, "sin φ");

        switch (data)
        {
            case DataResistivity:
                if (!c.Has("rho_20") && c.Template.FindInput("rho_20")!.Default is null || !c.Has("A_c") && c.Template.FindInput("A_c")!.Default is null) { c.Block("conductor-data", "rho_20/A_c: resistivity and cross-section are needed."); return; }
                c.Input("rho_20"); c.Input("alpha"); c.Input("A_c"); c.Input("T_c"); c.Input("X_km");
                c.Assign("rho_T", "rho_20 * (1 + alpha * (T_c - 20 degC) / (1 K))", "ohm*mm^2/m", "Resistivity at the operating temperature");
                c.Assign("R", "rho_T * L / (A_c * n_par)", "ohm", "Conductor resistance (one conductor, parallel paths combined)");
                c.Assign("X", "X_km * L / n_par", "ohm", "Conductor reactance");
                break;

            case DataPerKm:
                if (!c.Has("R_km")) { c.Block("conductor-data", "R_km: the resistance per km is needed."); return; }
                c.Input("R_km"); c.Input("X_km");
                c.Assign("R", "R_km * L / n_par", "ohm", "Conductor resistance");
                c.Assign("X", "X_km * L / n_par", "ohm", "Conductor reactance");
                break;

            default:
                if (!c.Has("mVAm")) { c.Block("conductor-data", "mVAm: the mV/A/m figure is needed."); return; }
                c.Input("mVAm");
                c.Assign("dV", "mVAm * 1 mV/A/m * I * L / n_par", "V", "Voltage drop from the table figure", "BS 7671 App. 4 §6");
                c.Assign("dV_pct", "100 * dV / V_n", null, "Voltage drop, per cent");
                c.Assign("u_dv", "dV_pct / dv_limit", null, "Drop utilisation against the limit");
                c.Output("dV", "Voltage drop");
                c.Output("dV_pct", "Voltage drop (%)");
                c.CheckUtilisation("Voltage drop ≤ limit", "u_dv", "BS 7671 525");
                return;
        }

        c.Assign("dV", system switch
        {
            SystemDc => "2 * I * R",
            SystemSingle => "2 * I * (R * cos_phi + X * sin_phi)",
            _ => "sqrt(3) * I * (R * cos_phi + X * sin_phi)",
        }, "V", "Voltage drop");
        c.Assign("dV_pct", "100 * dV / V_n", null, "Voltage drop, per cent");
        c.Assign("P_loss", system == SystemThree ? "3 * I^2 * R" : "2 * I^2 * R", "W", "Conductor power loss");
        c.Assign("u_dv", "dV_pct / dv_limit", null, "Drop utilisation against the limit");

        foreach (var (symbol, text) in new[] { ("R", "Resistance"), ("X", "Reactance"), ("dV", "Voltage drop"), ("dV_pct", "Voltage drop (%)"), ("P_loss", "Power loss") })
            c.Output(symbol, text);

        c.CheckUtilisation("Voltage drop ≤ limit", "u_dv", "BS 7671 525");

        if (c.Has("I_z"))
        {
            c.Input("I_z"); c.Input("T_rated"); c.Input("T_amb");
            c.Assign("u_Iz", "I / I_z", null, "Load against the current-carrying capacity");
            c.Assign("T_c_est", "T_amb + (T_rated - T_amb) * u_Iz^2", "degC", "Estimated conductor temperature", "BS 7671 App. 4 §6 form");
            c.Output("T_c_est", "Estimated conductor temperature");
            c.CheckUtilisation("I ≤ I_z", "u_Iz", "BS 7671 523");

            if (c.Si("u_Iz") > 1)
                c.Warn("overload", "I: the design current exceeds the current-carrying capacity.");

            if (data == DataResistivity && c.Si("T_c_est") > c.Si("T_c") + 1e-9)
                c.Warn("temperature", $"T_c: the estimated conductor temperature {CalcValue.FormatNumber(c.Si("T_c_est") - 273.15, 4)} °C is above the {CalcValue.FormatNumber(c.Si("T_c") - 273.15, 4)} °C used for the resistance.");
        }
    }
}
