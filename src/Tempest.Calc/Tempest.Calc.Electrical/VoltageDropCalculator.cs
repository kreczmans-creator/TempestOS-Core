using Tempest.Calc.Engine.Units;
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
        Id, "1.5.0", CalcDiscipline.Electrical, "Voltage drop and I²R loss",
        "Calculates the voltage drop, the conductor power loss and the estimated conductor temperature of a DC, single-phase or three-phase circuit. The drop limit is a user input, because the limits of BS 7671 are not embedded.",
        "Conductor resistance at its operating temperature, for n conductors in parallel: R = ρ_T L/(A n), with ρ_T = ρ_20 [1 + α (T_c − 20)].\nResistance and reactance from per-length figures instead: R = R′ L/n; X = X′ L/n.\nVoltage drop on a DC circuit: ΔV = 2 I R.\nVoltage drop on a single-phase AC circuit: ΔV = 2 I (R cos φ + X sin φ).\nVoltage drop on a three-phase AC circuit: ΔV = √3 I (R cos φ + X sin φ).\nVoltage drop from a tabulated mV/A/m figure, which already carries the 2 or √3: ΔV = (mV/A/m) I L / 1000.\nPower lost in the cable: P_loss = 2 I² R (DC and single-phase); 3 I² R (three-phase).\nEstimated conductor temperature at this current: T_c,est = T_amb + (T_rated − T_amb)(I/I_z)².",
        [
            new CalcInputSpec("system", "System", CalcInputKind.Choice, Choices: [SystemThree, SystemSingle, SystemDc], DefaultChoice: SystemThree, Group: "Setup"),
            new CalcInputSpec("data", "Conductor data from", CalcInputKind.Choice, Choices: [DataResistivity, DataPerKm, DataMvAm], DefaultChoice: DataResistivity, Group: "Setup"),
            new CalcInputSpec("mVAm_table", "Voltage drop figures by cross-section from your licensed table, read on the mV/A/m route at csa = A_c (optional; blank = the figure is typed as mVAm)", CalcInputKind.Table, Columns: MvAmColumns, MaxRows: 100, IsOptional: true, Group: "Setup"),
            new CalcInputSpec("L", "Route length (one way)", CalcInputKind.Quantity, "m", Default: 80, Min: 0.1, Max: 5000, HardMin: 1e-3, HardMax: 1e6, Group: "Geometry"),
            new CalcInputSpec("n_par", "Parallel conductors per phase", CalcInputKind.Number, Default: 1, Min: 1, Max: 6, HardMin: 1, HardMax: 20, Group: "Geometry"),
            new CalcInputSpec("A_c", "Conductor cross-section", CalcInputKind.Quantity, "mm^2", Default: 35, Min: 1, Max: 1000, HardMin: 0.01, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("alpha", "Temperature coefficient of resistance", CalcInputKind.Number, Default: 0.00393, Min: 0.003, Max: 0.0045, HardMin: 0, HardMax: 0.02, Group: "Material"),
            new CalcInputSpec("X_km", "Reactance per km per conductor", CalcInputKind.Quantity, "ohm/km", Default: 0.08, Min: 0, Max: 1, HardMin: 0, HardMax: 100, Group: "Material"),
            new CalcInputSpec("T_rated", "Conductor rated temperature", CalcInputKind.Quantity, "degC", Default: 70, Min: 60, Max: 90, HardMin: 0, HardMax: 300, Group: "Material"),
            new CalcInputSpec("rho_20", "Resistivity at 20 °C, the effective value for the conductor resistance (default 0.0194 Ω·mm²/m: the largest class 2 maximum conductor resistance of the cable standard for 1.5 to 120 mm², so the drop is not below the cable tables; pure annealed copper is 0.01724, aluminium 0.0282 Ω·mm²/m)", CalcInputKind.Quantity, "ohm*mm^2/m", Default: 0.0194, Min: 0.01, Max: 0.05, HardMin: 1e-4, HardMax: 10, IsOptional: true, Group: "Material"),
            new CalcInputSpec("R_km", "Resistance per km per conductor (tabulated, at its stated temperature)", CalcInputKind.Quantity, "ohm/km", Min: 0.01, Max: 50, HardMin: 1e-6, HardMax: 1e4, IsOptional: true, Group: "Material"),
            new CalcInputSpec("mVAm", "Voltage drop figure (mV/A/m, from the cable table)", CalcInputKind.Number, Min: 0.1, Max: 100, HardMin: 1e-3, HardMax: 1e4, IsOptional: true, Group: "Material"),
            new CalcInputSpec("V_n", "Nominal voltage (line-to-line for three-phase)", CalcInputKind.Quantity, "V", Default: 400, Min: 12, Max: 1000, HardMin: 0.1, HardMax: 1e6, Group: "Loads"),
            new CalcInputSpec("I", "Design current", CalcInputKind.Quantity, "A", Default: 100, Min: 0.1, Max: 5000, HardMin: 1e-6, HardMax: 1e6, Group: "Loads"),
            new CalcInputSpec("cos_phi", "Power factor (AC)", CalcInputKind.Number, Default: 0.85, Min: 0.5, Max: 1, HardMin: 0.01, HardMax: 1, Group: "Loads"),
            new CalcInputSpec("T_c", "Conductor operating temperature for the resistance", CalcInputKind.Quantity, "degC", Default: 70, Min: 20, Max: 90, HardMin: -50, HardMax: 300, Group: "Conditions"),
            new CalcInputSpec("T_amb", "Ambient temperature", CalcInputKind.Quantity, "degC", Default: 30, Min: 0, Max: 60, HardMin: -60, HardMax: 150, Group: "Conditions"),
            new CalcInputSpec("I_z", "Tabulated current-carrying capacity for the installed conditions (temperature estimate)", CalcInputKind.Quantity, "A", Min: 1, Max: 5000, HardMin: 1e-3, HardMax: 1e6, IsOptional: true, Group: "Conditions"),
            new CalcInputSpec("dv_limit", "Voltage-drop limit", CalcInputKind.Number, Default: 5, Min: 1, Max: 10, HardMin: 0.01, HardMax: 100, Group: "Requirements"),
        ],
        [
            new CalcAssumption("The load is balanced across the phases.", "Standard engineering approximation."),
            new CalcAssumption("The conductor reactance is the entered X′ per kilometre times the length.", "Standard engineering approximation."),
            new CalcAssumption("The voltage drop is the arithmetic sum R cos φ + X sin φ, not the vector difference.", "Standard engineering approximation."),
            new CalcAssumption("A mV/A/m figure is used as tabulated, with its own temperature and system convention.", "Cable table convention."),
            new CalcAssumption("The estimated conductor temperature scales the rated temperature rise with (I/I_z)².", "BS 7671:2018 Appendix 4 approach, formula only."),
            new CalcAssumption("I_z is the tabulated capacity for the installation method, with its correction factors applied.", "BS 7671:2018 Appendix 4 approach, formula only."),
            new CalcAssumption("When the estimate is above T_c, the resistance and the drop are computed at the estimate.", "Keeps the drop on the safe side."),
        ],
        [
            new CalcClauseRef("BS 7671:2018", "reg. 525", "Voltage drop in consumers' installations"),
            new CalcClauseRef("BS 7671:2018", "App. 4 §6", "Voltage drop (mV/A/m tables and the temperature-correction method)"),
            new CalcClauseRef("Hughes, Electrical and Electronic Technology", "Ch. on DC and AC circuits, 12th ed. (edition to be checked)", "Resistance, resistivity and temperature coefficient"),
        ],
        [
            new CalcApplicabilityRule("conductor-data", "The chosen conductor-data route needs values that were not given.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("default-conductor", "The conductor cross-section or resistivity was left blank and the template default is used.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("overload", "The design current exceeds the stated current-carrying capacity.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("temperature", "The estimated conductor temperature is above the temperature entered for the resistance, so the resistance and the voltage drop are computed at the estimated one.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("mvam-table", "The mV/A/m table has no row for the cross-section, more than one, or no figure for the system in that row.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("mvam-table-unused", "A mV/A/m figure is entered, so the table is not used.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
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
            new CalcTestCase("ka-mvam-table", "Three-phase from the mV/A/m table row for 16 mm², 63 A, 50 m: the same 2.4 mV/A/m figure read from the table (analytic)",
                new CalcReferenceSource("Analytic (BS 7671 Appendix 4 method, formula only; the table rows are synthetic values invented for this test)", null, "The row csa = 16 mm² gives z_3ph = 2.4 (invented); ΔV = 2.4 × 63 × 50 / 1000 = 7.56 V = 1.89 % of 400 V, as ka-mvam"),
                [CalcInputValue.Choice("system", SystemThree), CalcInputValue.Quantity("V_n", 400, "V"), CalcInputValue.Quantity("I", 63, "A"), CalcInputValue.Quantity("L", 50, "m"), CalcInputValue.Of("n_par", 1), CalcInputValue.Choice("data", DataMvAm), CalcInputValue.Quantity("A_c", 16, "mm^2"), MvAmTable(("copper", 10, 4.4, 3.8), ("copper", 16, 2.8, 2.4), ("copper", 25, 1.75, 1.5)), CalcInputValue.Of("dv_limit", 5)],
                [new CalcExpectedValue("mVAm", 2.4, "", 1e-9), new CalcExpectedValue("dV", 7.56, "V", 1e-9), new CalcExpectedValue("dV_pct", 1.89, "", 1e-9)]),
        ],
        DiagramKey: "voltage-drop",
        Category: "Power",
        OutputPorts:
        [
            new CalcOutputPort("dV", "V", "Voltage drop along the circuit", "circuit.voltage_drop"),
            new CalcOutputPort("dV_pct", "", "Voltage drop relative to the nominal voltage, in %", "circuit.voltage_drop_percent"),
            new CalcOutputPort("P_loss", "kW", "Power lost in the conductors", "circuit.power_loss"),
            new CalcOutputPort("T_c_est", "degC", "Estimated conductor temperature at the design current", "circuit.temperature_conductor_estimated"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("bs7671.voltage-drop", "Voltage drop per ampere per metre", ["mVAm_table"], "Your own licensed table of mV/A/m figures by cross-section for one conductor material; on the mV/A/m route the row for A_c supplies the figure. Never shipped."),
        ]);

    /// <summary>The columns of the user's mV/A/m table (Track P template 05, record kind bs7671.voltage-drop; DC-10). Where the table gives one figure it goes in the z column.</summary>
    private static readonly IReadOnlyList<CalcColumnSpec> MvAmColumns =
    [
        new("conductor", "Conductor material as the table names it", CalcInputKind.Text, IsOptional: true),
        new("csa", "Conductor cross-sectional area", CalcInputKind.Quantity, "mm^2", Min: 1, Max: 1000, HardMin: 0.01, HardMax: 1e5),
        new("r_dc", "Two cables, DC", CalcInputKind.Quantity, "mV/A/m", Min: 0.1, Max: 100, HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("r_1ph", "Two cables, single-phase AC: resistive component", CalcInputKind.Quantity, "mV/A/m", Min: 0.1, Max: 100, HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("x_1ph", "Two cables, single-phase AC: reactive component", CalcInputKind.Quantity, "mV/A/m", Min: 0.1, Max: 100, HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("z_1ph", "Two cables, single-phase AC: impedance", CalcInputKind.Quantity, "mV/A/m", Min: 0.1, Max: 100, HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("r_3ph", "Three or four cables, three-phase AC: resistive component", CalcInputKind.Quantity, "mV/A/m", Min: 0.1, Max: 100, HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("x_3ph", "Three or four cables, three-phase AC: reactive component", CalcInputKind.Quantity, "mV/A/m", Min: 0.1, Max: 100, HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("z_3ph", "Three or four cables, three-phase AC: impedance", CalcInputKind.Quantity, "mV/A/m", Min: 0.1, Max: 100, HardMin: 0, HardMax: 1e4, IsOptional: true),
    ];

    /// <summary>A mV/A/m table from (conductor, csa mm², r_dc, z_3ph) rows; the single-phase columns are left blank (record kind bs7671.voltage-drop).</summary>
    public static CalcInputValue MvAmTable(params (string Conductor, double Csa, double RDc, double Z3)[] rows) =>
        CalcInputValue.Table("mVAm_table", rows.Select(r => CalcRow.Of(("conductor", new CalcCell(Text: r.Conductor)), ("csa", new CalcCell(r.Csa, "mm^2")), ("r_dc", new CalcCell(r.RDc, "mV/A/m")), ("z_3ph", new CalcCell(r.Z3, "mV/A/m")))).ToList());

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_Iz", "u_dv");
        var system = c.InputText("system");
        var data = c.InputText("data");
        foreach (var s in new[] { "V_n", "I", "L", "cos_phi", "n_par", "dv_limit" })
            c.Input(s);

        var ac = system != SystemDc;
        c.Assign("sin_phi", ac ? "sqrt(1 - cos_phi^2)" : "0", null, "sin φ");

        switch (data)
        {
            case DataResistivity:
                if (!c.Has("rho_20") && c.Template.FindInput("rho_20")!.Default is null) { c.Block("conductor-data", "rho_20: enter the resistivity of the conductor."); return; }

                if (!c.Has("A_c") && c.Template.FindInput("A_c")!.Default is null) { c.Block("conductor-data", "A_c: enter the conductor cross-section."); return; }

                c.Input("rho_20"); c.Input("alpha"); c.Input("A_c"); c.Input("T_c"); c.Input("X_km");

                // elec.voltage-drop-02: a blank cross-section or resistivity takes the template default; say so, the result is for that conductor.
                if (!c.Has("A_c"))
                    c.Warn("default-conductor", $"A_c: the cross-section is blank, so the default of {CalcValue.FormatNumber(c.Template.FindInput("A_c")!.Default ?? 0)} mm² is used. Enter the cross-section of the cable.");

                if (!c.Has("rho_20"))
                    c.Warn("default-conductor", $"rho_20: the resistivity is blank, so the default of {CalcValue.FormatNumber(c.Template.FindInput("rho_20")!.Default ?? 0, 4)} Ω·mm²/m is used. Enter the resistivity of the conductor if it differs.");

                // elec.voltage-drop temperature: a conductor that carries I/I_z of its capacity runs hotter than the temperature entered; the resistance, and so the drop, is then taken
                // at the estimated temperature, so the drop is never below what the cable will show. A temperature entered above the estimate is used as entered.
                var temperature = "T_c";

                if (c.Has("I_z"))
                {
                    EstimateTemperature(c);

                    if (c.Si("T_c_est") > c.Si("T_c") + 1e-9)
                    {
                        c.Assign("T_c_used", "T_c_est", "degC", "Conductor temperature used for the resistance: the estimate, which is above the temperature entered");
                        temperature = "T_c_used";
                    }
                }

                c.Assign("rho_T", $"rho_20 * (1 + alpha * ({temperature} - 20 degC) / (1 K))", "ohm*mm^2/m", "Resistivity at the operating temperature");
                c.Assign("R", "rho_T * L / (A_c * n_par)", "ohm", "Conductor resistance (one conductor, parallel paths combined)");
                c.Assign("X", "X_km * L / n_par", "ohm", "Conductor reactance");
                break;

            case DataPerKm:
                if (!c.Has("R_km")) { c.Block("conductor-data", "R_km: enter the resistance per kilometre."); return; }
                c.Input("R_km"); c.Input("X_km");
                c.Assign("R", "R_km * L / n_par", "ohm", "Conductor resistance");
                c.Assign("X", "X_km * L / n_par", "ohm", "Conductor reactance");
                break;

            default:
                if (c.Has("mVAm"))
                {
                    c.Input("mVAm");

                    if (c.Rows("mVAm_table").Count > 0)
                        c.Warn("mvam-table-unused", "mVAm_table: the figure mVAm is entered, so the table is not used. Clear mVAm to read the figure from the table.");
                }
                else if (!TabulatedFigure(c, system))
                {
                    return;
                }

                c.Assign("dV", "mVAm * 1 mV/A/m * I * L / n_par", "V", "Voltage drop from the table figure", "BS 7671 App. 4 §6");
                c.Assign("dV_pct", "100 * dV / V_n", null, "Voltage drop, per cent");
                c.Assign("u_dv", "dV_pct / dv_limit", null, "Drop utilisation against the limit");
                c.Assign("dV_max", "dv_limit / 100 * V_n", "V", "Largest voltage drop the limit allows");
                c.Output("dV", "Voltage drop");
                c.Output("dV_pct", "Voltage drop (%)");
                c.Check("Voltage drop against the limit", "dV", "dV_max", "BS 7671:2018 reg. 525", resultSymbol: "u_dv");
                CapacityCheck(c, data);
                return;
        }

        c.Assign("dV", system switch
        {
            SystemDc => "2 * I * R",
            SystemSingle => "2 * I * (R * cos_phi + X * sin_phi)",
            _ => "sqrt(3) * I * (R * cos_phi + X * sin_phi)",
        }, "V", "Voltage drop");
        c.Assign("dV_pct", "100 * dV / V_n", null, "Voltage drop, per cent");
        c.Assign("P_loss", system == SystemThree ? "3 * I^2 * R" : "2 * I^2 * R", "kW", "Conductor power loss");
        c.Assign("u_dv", "dV_pct / dv_limit", null, "Drop utilisation against the limit");
        c.Assign("dV_max", "dv_limit / 100 * V_n", "V", "Largest voltage drop the limit allows");

        foreach (var (symbol, text) in new[] { ("R", "Resistance"), ("X", "Reactance"), ("dV", "Voltage drop"), ("dV_pct", "Voltage drop (%)"), ("P_loss", "Power loss") })
            c.Output(symbol, text);

        c.Check("Voltage drop against the limit", "dV", "dV_max", "BS 7671:2018 reg. 525", resultSymbol: "u_dv");

        CapacityCheck(c, data);
    }

    /// <summary>
    /// DC-10 (Track P template 05): the mV/A/m figure read from the user's licensed table for the cross-section A_c and the system (r_dc for DC,
    /// z_1ph for single-phase, z_3ph for three-phase). False when the sheet is blocked: no table and no figure, no A_c, no row for A_c, more than
    /// one row, or no figure in the column for the system.
    /// </summary>
    private static bool TabulatedFigure(CalcRunContext c, string system)
    {
        var rows = c.Rows("mVAm_table");

        if (rows.Count == 0)
        {
            c.Block("conductor-data", "mVAm: enter the voltage drop figure in mV/A/m, or give the mV/A/m table.");
            return false;
        }

        if (!c.Has("A_c"))
        {
            c.Block("mvam-table", "A_c: enter the conductor cross-section, which picks the row of the mV/A/m table.");
            return false;
        }

        c.Input("A_c");
        var mm2 = UnitCatalog.Parse("mm^2");
        var figureUnit = UnitCatalog.Parse("mV/A/m");
        var area = mm2.FromSi(c.Si("A_c"));
        var matches = rows.Where(r => Cell(r, "csa", mm2) is { } csa && Math.Abs(mm2.FromSi(csa) - area) <= 1e-9 * Math.Max(1, area)).ToList();

        if (matches.Count == 0)
        {
            c.Block("mvam-table", $"mVAm_table: no row has csa = {CalcValue.FormatNumber(area, 5)} mm². Add the row, or enter mVAm.");
            return false;
        }

        if (matches.Count > 1)
        {
            c.Block("mvam-table", $"mVAm_table: {matches.Count} rows have csa = {CalcValue.FormatNumber(area, 5)} mm². Keep one conductor material per table.");
            return false;
        }

        var column = system switch { SystemDc => "r_dc", SystemSingle => "z_1ph", _ => "z_3ph" };
        var row = matches[0];

        if (Cell(row, column, figureUnit) is not { } figure || !(figure > 0))
        {
            c.Block("mvam-table", $"mVAm_table: the row for csa = {CalcValue.FormatNumber(area, 5)} mm² has no {column} figure. Fill it in, or enter mVAm.");
            return false;
        }

        var conductor = row.Get("conductor")?.Text is { Length: > 0 } text ? CalcSheetTextRenderer.Clean(text) : "the table";
        c.Record("mVAm", $"{column} of the row with csa = A_c in the licensed table", $"{conductor}: csa = {CalcValue.FormatNumber(area, 5)} mm²", new ScalarValue(figureUnit.FromSi(figure)), "Voltage drop figure in mV/A/m from the user's licensed table", "BS 7671 App. 4 §6");
        c.Output("mVAm", "Voltage drop figure from the table (mV/A/m)");
        return true;
    }

    /// <summary>The SI value of a table cell, or null when absent; a cell without a unit is read in <paramref name="unit"/>.</summary>
    private static double? Cell(CalcRow row, string key, Unit unit) => row.Get(key) is { Number: { } n } cell ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : unit).ToSi(n) : null;

    /// <summary>
    /// The overload check I ≤ I_z and the conductor temperature estimate, when the capacity is entered: on every route (elec.voltage-drop-03; the mV/A/m route used to ignore I_z). The warning that the estimate is above
    /// the temperature used for the resistance applies to the resistivity route only, the one that takes a conductor temperature.
    /// </summary>
    private static void CapacityCheck(CalcRunContext c, string data)
    {
        if (!c.Has("I_z"))
            return;

        EstimateTemperature(c);
        c.Output("T_c_est", "Estimated conductor temperature");
        c.Check("Design current against the current-carrying capacity", "I", "I_z", "BS 7671:2018 reg. 523", resultSymbol: "u_Iz");

        if (c.Si("u_Iz") > 1)
            c.Warn("overload", "I: the design current exceeds the current-carrying capacity.");

        if (data == DataResistivity && c.Si("T_c_est") > c.Si("T_c") + 1e-9)
            c.Warn("temperature", $"T_c: the estimated conductor temperature of {CalcValue.FormatNumber(c.Si("T_c_est") - 273.15, 4)} °C is above the {CalcValue.FormatNumber(c.Si("T_c") - 273.15, 4)} °C entered. The resistance and the drop use the estimate.");
    }

    /// <summary>The load against the capacity and the conductor temperature it implies; defined once, before the resistance is found and again by the capacity check.</summary>
    private static void EstimateTemperature(CalcRunContext c)
    {
        if (c.Session.Find("T_c_est") is not null)
            return;

        c.Input("I_z"); c.Input("T_rated"); c.Input("T_amb");
        c.Assign("u_Iz", "I / I_z", null, "Load against the current-carrying capacity");
        c.Assign("T_c_est", "T_amb + (T_rated - T_amb) * u_Iz^2", "degC", "Estimated conductor temperature", "BS 7671 App. 4 §6 form");
    }
}
