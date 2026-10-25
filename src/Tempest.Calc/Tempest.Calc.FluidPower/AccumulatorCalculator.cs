using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Physics;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.FluidPower;

/// <summary>
/// F2 — hydraulic accumulator sizing from the gas law <c>p·Vⁿ = const</c> on the pre-charge gas, through the shared
/// <see cref="GasLaw"/> kernel (ADR-0170): the gas volume V0 that delivers (or absorbs) a liquid volume ΔV between the
/// lowest and highest working pressures, with the pre-charge pressure corrected to the charging temperature, the
/// pre-charge rules checked (records), the next listed size selected (a user size record) and the bar·litre product
/// compared with the PSSR Schedule 1 250 bar·L exception (PS and vessel volume as the user enters them). All pressures in the formula are ABSOLUTE; a gauge entry is
/// converted with the atmospheric pressure. Nothing here is a pressure-vessel code check (PD 5500, EN 13445, ASME VIII);
/// the PED / UK PER and PSSR duties of the accumulator stay with the manufacturer and the competent person.
/// </summary>
public sealed class AccumulatorCalculator : ICalcCalculator
{
    public const string Id = "fp.accumulator";

    public const string ModeEnergy = "Energy storage";
    public const string ModeShock = "Shock / pulsation (screening)";
    public const string ModeEmergency = "Emergency stroke";

    public const string Gauge = "Gauge";
    public const string Absolute = "Absolute";

    /// <summary>The bar·litre product below which the Addendum B §B5 indication reads "below the exception".</summary>
    public const double PssrBarLitres = 250.0;

    /// <summary>Real-gas behaviour matters above this absolute pressure (200 bar): the sheet warns.</summary>
    public const double RealGasWarnBar = 200.0;

    private static readonly IReadOnlyList<CalcColumnSpec> SizeColumns =
    [
        new("V", "Nominal gas volume", CalcInputKind.Quantity, "L", Min: 0.01, Max: 5000, HardMin: 0.01, HardMax: 5000),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.2.0", CalcDiscipline.FluidPower, "Accumulator sizing (gas law)",
        "The pre-charge gas volume V0 of a hydraulic accumulator that delivers, or absorbs, a liquid volume ΔV between the lowest working pressure p1 and the highest p2, from p·Vⁿ = const with absolute pressures and an exponent n between 1 (isothermal) and 1.4 (adiabatic); the volume scaled to the lowest operating temperature (T_max/T_min) and the pre-charge pressure at the charging temperature; the next size from your own size list; and the bar·litre product PS·V of the vessel's own maximum allowable pressure and volume against the PSSR Schedule 1 250 bar·L exception. Three modes: energy storage, shock and pulsation (screening only) and emergency stroke (ΔV is the stroke volume, linkable from the cylinder circuit). Not a pressure-vessel code check.",
        "Absolute pressures: p_a = p_gauge + p_atm (or as entered, in Absolute mode)\n" +
        "Gas volume: V0 = ΔV / [(p0/p1)^(1/n) − (p0/p2)^(1/n)], n = 1 isothermal … 1.4 adiabatic, then × the real-gas factor f_rg (user input, at least 1) and × (T_max + 273.15)/(T_min + 273.15), because p0 is defined at T_max and the gas delivers less when cold\n" +
        "Pre-charge rules (records, applied to absolute pressures): p0 ≤ r_0·p1 at T_max (default 0.9, bladder type) and p2/p0(T_min) ≤ r_p (default 4), p0(T_min) = p0·(T_min + 273.15)/(T_max + 273.15)\n" +
        "Pre-charge at the charging temperature: p0(T_c) = p0(T_max)·(T_c + 273.15)/(T_max + 273.15)\n" +
        "Next size: the smallest listed size not below V0\n" +
        "Bar·litres: PS·V, PS = the vessel's maximum allowable pressure (bar gauge, entered, not below p2 gauge), V = the vessel volume entered (default the selected size); compared with 250 bar·L (PSSR Schedule 1, indication only; not available without PS and a volume)",
        [
            new CalcInputSpec("mode", "Duty", CalcInputKind.Choice, Choices: [ModeEnergy, ModeShock, ModeEmergency], DefaultChoice: ModeEnergy, Group: "Duty"),
            new CalcInputSpec("dV", "Liquid volume ΔV delivered, absorbed or (emergency stroke) needed for the stroke", CalcInputKind.Quantity, "L", Default: 10, Min: 0.001, Max: 1000, HardMin: 0.001, HardMax: 1000, Group: "Duty"),
            new CalcInputSpec("p_basis", "Pressures entered as", CalcInputKind.Choice, Choices: [Gauge, Absolute], DefaultChoice: Gauge, Group: "Pressures"),
            new CalcInputSpec("p_0", "Gas pre-charge pressure p0, at the maximum operating temperature", CalcInputKind.Quantity, "bar", Default: 70, Min: 0, Max: 700, HardMin: 0, HardMax: 700, Group: "Pressures"),
            new CalcInputSpec("p_1", "Lowest working pressure p1", CalcInputKind.Quantity, "bar", Default: 100, Min: 0, Max: 700, HardMin: 0, HardMax: 700, Group: "Pressures"),
            new CalcInputSpec("p_2", "Highest working pressure p2", CalcInputKind.Quantity, "bar", Default: 180, Min: 0, Max: 700, HardMin: 0, HardMax: 700, Group: "Pressures"),
            new CalcInputSpec("p_atm", "Atmospheric pressure (gauge to absolute)", CalcInputKind.Quantity, "bar", Default: 1.01325, Min: 0.5, Max: 1.1, HardMin: 0.5, HardMax: 1.1, Group: "Pressures"),
            new CalcInputSpec("n", "Gas exponent n (1 isothermal, slow; 1.4 adiabatic, fast)", CalcInputKind.Number, Default: 1, Min: 1, Max: 1.4, HardMin: 1, HardMax: 1.4, Group: "Gas"),
            new CalcInputSpec("f_rg", "Real-gas factor on V0 (1 = ideal gas; at least 1, as nitrogen's compressibility factor exceeds 1 at high pressure; from a gas-property source above 200 bar)", CalcInputKind.Number, Default: 1, Min: 1, Max: 1.5, HardMin: 1, HardMax: 1.5, Group: "Gas"),
            new CalcInputSpec("T_max", "Maximum operating temperature (p0 applies here)", CalcInputKind.Quantity, "degC", Default: 50, Min: -40, Max: 150, HardMin: -40, HardMax: 150, Group: "Temperature"),
            new CalcInputSpec("T_min", "Minimum operating temperature (the gas is coldest and delivers least here; V0 is scaled by T_max/T_min)", CalcInputKind.Quantity, "degC", Default: 20, Min: -40, Max: 150, HardMin: -40, HardMax: 150, Group: "Temperature"),
            new CalcInputSpec("T_c", "Charging temperature", CalcInputKind.Quantity, "degC", Default: 20, Min: -40, Max: 100, HardMin: -40, HardMax: 100, Group: "Temperature"),
            new CalcInputSpec("r_0", "Pre-charge rule: p0 (at T_max) at most this fraction of p1, on absolute pressures (state whether the manufacturer's ratio is on gauge or absolute pressures)", CalcInputKind.Number, Default: 0.9, Min: 0.1, Max: 1, HardMin: 0.1, HardMax: 1, Group: "Pre-charge rules"),
            new CalcInputSpec("r_p", "Pre-charge rule: p2/p0 (p0 at T_min) at most this ratio, on absolute pressures", CalcInputKind.Number, Default: 4, Min: 1, Max: 20, HardMin: 1, HardMax: 20, Group: "Pre-charge rules"),
            new CalcInputSpec("sizes", "Available accumulator sizes (from the manufacturer's range)", CalcInputKind.Table, Columns: SizeColumns, MaxRows: 60, IsOptional: true, Group: "Size list"),
            new CalcInputSpec("PS", "Vessel maximum allowable pressure PS, bar gauge (nameplate or relief setting; not below p2 gauge; needed for the PSSR indication)", CalcInputKind.Quantity, "bar", Min: 0, Max: 1500, HardMin: 0, HardMax: 1500, IsOptional: true, Group: "PSSR indication"),
            new CalcInputSpec("V_vessel", "Vessel internal volume V (blank = the selected size; not below V0)", CalcInputKind.Quantity, "L", Min: 0.01, Max: 5000, HardMin: 0.01, HardMax: 5000, IsOptional: true, Group: "PSSR indication"),
        ],
        [
            new CalcAssumption("All gas-law pressures are absolute. A gauge entry is converted with the atmospheric pressure entered here, so the same numbers read as gauge or as absolute give different volumes.", "p·Vⁿ = const on a fixed mass of gas."),
            new CalcAssumption("The exponent n is the user's: 1 for a slow discharge with heat exchange (isothermal), towards 1.4 for a fast one (adiabatic). The larger n gives the larger volume for a given ΔV.", "Gas-law exponents, closed form."),
            new CalcAssumption("The gas is ideal unless a real-gas factor is entered (at least 1: nitrogen's compressibility factor exceeds 1 at high pressure, so the real gas needs more volume, never less); above 200 bar the sheet warns that nitrogen departs from the ideal-gas law and a factor from a gas-property source should be entered.", "Real-gas correction as a user factor."),
            new CalcAssumption("The pre-charge p0 is defined at the maximum operating temperature T_max and V0 is sized there; at the lowest operating temperature T_min the same gas delivers only the fraction T_min/T_max (absolute) of ΔV, so V0 is multiplied by T_max/T_min, and the p2/p0 rule is checked with p0 at T_min. The temperature range is the user's; with T_min = T_max the correction is 1.", "Manufacturer sizing practice; the gas law at constant mass."),
            new CalcAssumption("The pre-charge rules (p0 ≤ 0.9·p1 and p2/p0 ≤ 4 by default) are records for the accumulator type and make and are checked as PASS/FAIL on ABSOLUTE pressures, whichever basis the pressures are entered on; the right values, and whether the manufacturer states the ratio on gauge or absolute pressures, come from the manufacturer (the same numbers read as gauge pressures give a different ratio).", "Rules are records, not built-in limits."),
            new CalcAssumption("The bar·litre product is PS times V of the vessel itself: PS is the vessel's maximum allowable pressure in bar gauge (the nameplate or relief setting, never below the highest working pressure p2) and V its internal volume (the selected size unless entered). Without PS and a volume the indication is not available. It indicates whether the 250 bar·L exception of PSSR Schedule 1 can apply; it is not a compliance statement, and PED / UK PER, PSSR examination and the vessel's own design code stay with the manufacturer and the competent person.", "Addendum B §B5 regulatory gating: no pressure-vessel code check."),
        ],
        [
            new CalcClauseRef("Pressure Systems Safety Regulations 2000 (SI 2000/128)", "Schedule 1", "250 bar·litre exception, indication only"),
            new CalcClauseRef("Pressure Equipment Directive 2014/68/EU and UK PER 2016", "—", "Pressure equipment; not checked here"),
            new CalcClauseRef("Esposito, Fluid Power with Applications", "Ch. on accumulators", "Gas-charged accumulator sizing, p·Vⁿ = const"),
        ],
        [
            new CalcApplicabilityRule("pressures", "Pre-charge, lowest and highest working pressure must be in increasing order (absolute) and above zero.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("realgas", "The highest working pressure is above 200 bar absolute: nitrogen departs from the ideal-gas law.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("shock", "Shock and pulsation sizing is a screening estimate; the dynamic response of the line is not modelled.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("exponent", "A shock or emergency discharge is fast: an exponent near 1.4 is the usual choice.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("link-basis", "A pressure is linked from another calculation while the pressures are entered as Absolute: linked pressures are gauge, so the value is read one atmosphere low.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("size", "No listed size is as large as the calculated volume.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("temperature", "The minimum operating temperature must not be above the maximum.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("PS", "PS (bar gauge) must not be below the highest working pressure p2 (gauge).", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("vessel", "The vessel volume must not be below the calculated gas volume V0.", CalcNoticeSeverity.Block),
        ],
        [
            new CalcTestCase("ka-parker-numbers-absolute", "Parker HY10-4004/UK numbers read as absolute bar: p0 90, p1 100, p2 210, ΔV 14 L, isothermal (analytic)",
                new CalcReferenceSource("Analytic (p·V = const, V0 = ΔV/[(p0/p1) − (p0/p2)])", null, "V0 = 14/(0.9 − 0.428571) = 29.69697 L (T_min = T_max, so no temperature scaling); p0 at 20 °C from 50 °C = 90·293.15/323.15 = 81.64475 bar; p2/p0 = 210/90 = 2.3333, u_rp = 0.58333; no vessel PS is entered, so the PSSR indication is not available"),
                [CalcInputValue.Quantity("dV", 14, "L"), CalcInputValue.Choice("p_basis", Absolute), CalcInputValue.Quantity("p_0", 90, "bar"), CalcInputValue.Quantity("p_1", 100, "bar"), CalcInputValue.Quantity("p_2", 210, "bar"), CalcInputValue.Of("n", 1), CalcInputValue.Quantity("T_min", 50, "degC")],
                [new CalcExpectedValue("V0", 29.69696970, "L", 1e-9), new CalcExpectedValue("p0", 81.64474702, "bar", 1e-9), new CalcExpectedValue("u_r0", 1.0, "", 1e-9), new CalcExpectedValue("u_rp", 0.5833333333, "", 1e-9)]),
            new CalcTestCase("ka-parker-numbers-gauge", "The same numbers read as gauge bar with 1.01325 bar atmosphere (analytic)",
                new CalcReferenceSource("Analytic (absolute = gauge + 1.01325 bar)", null, "p0 = 91.01325, p1 = 101.01325, p2 = 211.01325 bar abs: V0 = 14/(0.901003 − 0.431315) = 29.80704 L; the pre-charge is then 0.9010·p1 and the p0 ≤ 0.9·p1 rule (applied to absolute pressures) fails by a hair (u = 1.00111); with T_min = T_max there is no temperature scaling"),
                [CalcInputValue.Quantity("dV", 14, "L"), CalcInputValue.Choice("p_basis", Gauge), CalcInputValue.Quantity("p_0", 90, "bar"), CalcInputValue.Quantity("p_1", 100, "bar"), CalcInputValue.Quantity("p_2", 210, "bar"), CalcInputValue.Quantity("p_atm", 1.01325, "bar"), CalcInputValue.Of("n", 1), CalcInputValue.Quantity("T_min", 50, "degC")],
                [new CalcExpectedValue("V0", 29.80703643, "L", 1e-9), new CalcExpectedValue("u_r0", 1.001114540, "", 1e-9), new CalcExpectedValue("u_rp", 0.5796223352, "", 1e-9)]),
            new CalcTestCase("ka-adiabatic", "Fast discharge, n = 1.4, p0 90, p1 100, p2 210 bar absolute, ΔV 14 L (analytic)",
                new CalcReferenceSource("Analytic (V0 = ΔV/[(p0/p1)^(1/n) − (p0/p2)^(1/n)])", null, "(0.9)^(1/1.4) = 0.927505, (90/210)^(1/1.4) = 0.545958: V0 = 14/0.381546 = 36.69279 L"),
                [CalcInputValue.Quantity("dV", 14, "L"), CalcInputValue.Choice("p_basis", Absolute), CalcInputValue.Quantity("p_0", 90, "bar"), CalcInputValue.Quantity("p_1", 100, "bar"), CalcInputValue.Quantity("p_2", 210, "bar"), CalcInputValue.Of("n", 1.4), CalcInputValue.Quantity("T_min", 50, "degC")],
                [new CalcExpectedValue("V0", 36.69278925, "L", 1e-9)]),
            new CalcTestCase("ka-charge-temperature", "Pre-charge set at 20 °C for a 80 °C maximum (analytic)",
                new CalcReferenceSource("Analytic (p0(Tc) = p0(Tmax)·(Tc + 273.15)/(Tmax + 273.15))", null, "p0 = 90 bar abs at 80 °C: 90·293.15/353.15 = 74.70905 bar abs; set as gauge on the rig: 73.69580 bar with 1.01325 bar"),
                [CalcInputValue.Quantity("dV", 14, "L"), CalcInputValue.Choice("p_basis", Absolute), CalcInputValue.Quantity("p_0", 90, "bar"), CalcInputValue.Quantity("p_1", 100, "bar"), CalcInputValue.Quantity("p_2", 210, "bar"), CalcInputValue.Quantity("T_max", 80, "degC"), CalcInputValue.Quantity("T_min", 20, "degC"), CalcInputValue.Quantity("T_c", 20, "degC")],
                [new CalcExpectedValue("p0", 74.70904715, "bar", 1e-9), new CalcExpectedValue("p0_set", 74.70904715, "bar", 1e-9), new CalcExpectedValue("p0_min", 74.70904715, "bar", 1e-9)]),
            new CalcTestCase("ka-size-and-real-gas", "Real-gas factor 1.05 and a size list: next size above the corrected volume (analytic)",
                new CalcReferenceSource("Analytic (V0 = f_rg·V0_ideal; next listed size; bar·L = PS·V)", null, "V0 = 1.05·29.69697 = 31.18182 L; sizes 10, 20, 32, 50 L give 32 L (T_min = T_max); vessel PS 230 bar gauge on the selected 32 L: bar·L = 230·32 = 7360; above 250 bar·L"),
                [CalcInputValue.Quantity("dV", 14, "L"), CalcInputValue.Choice("p_basis", Absolute), CalcInputValue.Quantity("p_0", 90, "bar"), CalcInputValue.Quantity("p_1", 100, "bar"), CalcInputValue.Quantity("p_2", 210, "bar"), CalcInputValue.Of("f_rg", 1.05), CalcInputValue.Quantity("T_min", 50, "degC"), CalcInputValue.Quantity("PS", 230, "bar"), Sizes(10, 20, 32, 50)],
                [new CalcExpectedValue("V0", 31.18181818, "L", 1e-9), new CalcExpectedValue("V_sel", 32, "L", 1e-9), new CalcExpectedValue("u_V", 0.9744318182, "", 1e-9), new CalcExpectedValue("bar_litres", 7360, "bar*L", 1e-9)]),
            new CalcTestCase("ka-below-pssr", "A small accumulator below the 250 bar·L indication (analytic)",
                new CalcReferenceSource("Analytic (V0 = ΔV/[(p0/p1) − (p0/p2)]; bar·L = PS·V of the vessel)", null, "ΔV 0.5 L, p0 40, p1 50, p2 100 bar abs, isothermal, T_min = T_max: V0 = 0.5/(0.8 − 0.4) = 1.25 L; vessel PS 110 bar gauge and 2 L: bar·L = 220 < 250"),
                [CalcInputValue.Quantity("dV", 0.5, "L"), CalcInputValue.Choice("p_basis", Absolute), CalcInputValue.Quantity("p_0", 40, "bar"), CalcInputValue.Quantity("p_1", 50, "bar"), CalcInputValue.Quantity("p_2", 100, "bar"), CalcInputValue.Quantity("T_min", 50, "degC"), CalcInputValue.Quantity("PS", 110, "bar"), CalcInputValue.Quantity("V_vessel", 2, "L")],
                [new CalcExpectedValue("V0", 1.25, "L", 1e-9), new CalcExpectedValue("bar_litres", 220, "bar*L", 1e-9), new CalcExpectedValue("u_r0", 0.8888888889, "", 1e-9), new CalcExpectedValue("u_rp", 0.625, "", 1e-9)]),
            new CalcTestCase("ka-ps-above-working-pressure", "PS above the working pressure: the exception that the working pressure alone would allow does not apply (analytic)",
                new CalcReferenceSource("Analytic (bar·L = PS·V, PS the vessel's maximum allowable pressure in bar gauge)", null, "ΔV 0.5 L, p0 40, p1 50, p2 100 bar gauge (101.01325 bar abs), isothermal, T_min = T_max: V0 = 0.5/(41.01325/51.01325 − 41.01325/101.01325) = 1.256427 L; the vessel is 2.4 L with a relief valve at PS = 110 bar gauge: bar·L = 110·2.4 = 264 ≥ 250, where the working pressure 100 bar gauge would give 240 < 250"),
                [CalcInputValue.Quantity("dV", 0.5, "L"), CalcInputValue.Choice("p_basis", Gauge), CalcInputValue.Quantity("p_0", 40, "bar"), CalcInputValue.Quantity("p_1", 50, "bar"), CalcInputValue.Quantity("p_2", 100, "bar"), CalcInputValue.Quantity("p_atm", 1.01325, "bar"), CalcInputValue.Quantity("T_min", 50, "degC"), CalcInputValue.Quantity("PS", 110, "bar"), CalcInputValue.Quantity("V_vessel", 2.4, "L")],
                [new CalcExpectedValue("V0", 1.256426685, "L", 1e-9), new CalcExpectedValue("bar_litres", 264, "bar*L", 1e-9), new CalcExpectedValue("u_rp", 0.6157354635, "", 1e-9)]),
            new CalcTestCase("ka-temperature-range", "Operating range 20 to 50 °C: the gas volume grows by T_max/T_min and the pressure ratio is checked with the cold pre-charge (analytic)",
                new CalcReferenceSource("Analytic (V0 = f_rg·V0_ideal·T_max/T_min; p0(T_min) = p0·T_min/T_max, absolute temperatures)", null, "Parker-style numbers p0 90, p1 100, p2 210 bar abs, ΔV 14 L, isothermal: V0_ideal = 29.69697 L; (50 + 273.15)/(20 + 273.15) = 1.102337, so V0 = 32.73606 L and the next size of 10, 20, 32, 50 L is 50 L (u_V = 0.654721); p0 at 20 °C = 90·293.15/323.15 = 81.64475 bar; p2/p0 = 210/81.64475 = 2.572, u_rp = 0.643030 (against 0.58333 with p0 at T_max)"),
                [CalcInputValue.Quantity("dV", 14, "L"), CalcInputValue.Choice("p_basis", Absolute), CalcInputValue.Quantity("p_0", 90, "bar"), CalcInputValue.Quantity("p_1", 100, "bar"), CalcInputValue.Quantity("p_2", 210, "bar"), CalcInputValue.Quantity("T_max", 50, "degC"), CalcInputValue.Quantity("T_min", 20, "degC"), Sizes(10, 20, 32, 50)],
                [new CalcExpectedValue("f_T", 1.102336688, "", 1e-9), new CalcExpectedValue("V0", 32.73605921, "L", 1e-9), new CalcExpectedValue("V_sel", 50, "L", 1e-9), new CalcExpectedValue("u_V", 0.6547211842, "", 1e-9), new CalcExpectedValue("p0_min", 81.64474702, "bar", 1e-9), new CalcExpectedValue("u_rp", 0.6430297345, "", 1e-9)]),
            new CalcTestCase("ka-parker-hy10-4004", "Parker HY10-4004/UK accumulator selection example: about 29.7 L, catalogue selection 32 L",
                new CalcReferenceSource("Parker Hannifin, HY10-4004/UK, accumulator sizing example as quoted in Addendum B §B6 F2", null, "P2 210, P1 100, P0 90 bar, ΔV 14 L, isothermal: V0 ≈ 29.7–29.8 L, catalogue selection 32 L; to be checked against the owner's copy of the catalogue, including whether the pressures are gauge or absolute", Secondary: true),
                [CalcInputValue.Quantity("dV", 14, "L"), CalcInputValue.Choice("p_basis", Absolute), CalcInputValue.Quantity("p_0", 90, "bar"), CalcInputValue.Quantity("p_1", 100, "bar"), CalcInputValue.Quantity("p_2", 210, "bar"), CalcInputValue.Of("n", 1), CalcInputValue.Quantity("T_min", 50, "degC"), Sizes(10, 20, 32, 50)],
                [new CalcExpectedValue("V0", 29.7, "L", 0.005), new CalcExpectedValue("V_sel", 32, "L", 1e-9)],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Hydraulics",
        OutputPorts:
        [
            new CalcOutputPort("V0", "L", "Accumulator gas pre-charge volume required"),
            new CalcOutputPort("p0", "bar", "Absolute pre-charge pressure at the charging temperature"),
            new CalcOutputPort("bar_litres", "bar*L", "Vessel PS (bar gauge) times vessel volume, when PS and a volume are given"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("accumulator.rules", "Pre-charge rules", ["r_0", "r_p"], "The pre-charge limits for the accumulator type and make (bladder, diaphragm, piston), from the manufacturer's selection guide: the largest p0/p1 (p0 at the maximum operating temperature) and the largest p2/p0 (p0 at the minimum operating temperature). The sheet applies both to ABSOLUTE pressures; record whether the manufacturer's ratio is stated on gauge or absolute pressures, since the same numbers read as gauge give a different ratio."),
            new CalcRecordKind("accumulator.sizes", "Accumulator size list", ["sizes"], "The nominal gas volumes the manufacturer lists for the chosen type and pressure class, read from the catalogue."),
        ]);

    private static CalcInputValue Sizes(params double[] litres) =>
        CalcInputValue.Table("sizes", litres.Select(v => CalcRow.Of(("V", new CalcCell(v, "L")))).ToList());

    public void Run(CalcRunContext c)
    {
        foreach (var s in new[] { "dV", "p_0", "p_1", "p_2", "p_atm", "n", "f_rg", "T_max", "T_min", "T_c", "r_0", "r_p" })
            c.Input(s);

        var mode = c.InputText("mode");
        var absolute = c.InputText("p_basis") == Absolute;
        var litre = UnitCatalog.Parse("L");

        // A pressure linked from another calculation (the cylinder's supply pressure p is gauge) is read on this sheet's basis: Absolute mode would take the gauge value as absolute.
        if (absolute)
        {
            foreach (var symbol in new[] { "p_0", "p_1", "p_2" })
            {
                if (CalcLink.From(c.Inputs.Find(symbol)?.Provenance) is { } link)
                    c.Warn("link-basis", $"{symbol}: linked from '{link.OutputSymbol}' of another calculation while the pressures are entered as Absolute; the outputs that other calculations offer as pressures (the cylinder's supply pressure p) are gauge, so the linked value would be read {F(c.Si("p_atm") / 1e5)} bar low. Switch p_basis to Gauge, or add the atmospheric pressure to the linked value.");
            }
        }

        c.Assign("pa_0", absolute ? "p_0" : "p_0 + p_atm", "bar", "Pre-charge pressure, absolute");
        c.Assign("pa_1", absolute ? "p_1" : "p_1 + p_atm", "bar", "Lowest working pressure, absolute");
        c.Assign("pa_2", absolute ? "p_2" : "p_2 + p_atm", "bar", "Highest working pressure, absolute");

        double pa0 = c.Si("pa_0"), pa1 = c.Si("pa_1"), pa2 = c.Si("pa_2");

        if (!(pa0 > 0))
        {
            c.Block("pressures", "p_0: the absolute pre-charge pressure must be above zero; check whether the pressures are entered as gauge or absolute.");
            return;
        }

        if (!(pa0 <= pa1))
        {
            c.Block("pressures", $"p_0: the pre-charge pressure ({F(pa0 / 1e5)} bar abs) must not be above the lowest working pressure p_1 ({F(pa1 / 1e5)} bar abs); lower p_0 (the usual rule is 0.9·p_1).");
            return;
        }

        if (!(pa1 < pa2))
        {
            c.Block("pressures", $"p_2: the highest working pressure ({F(pa2 / 1e5)} bar abs) must be above the lowest, p_1 ({F(pa1 / 1e5)} bar abs).");
            return;
        }

        if (!(c.Si("T_min") <= c.Si("T_max")))
        {
            c.Block("temperature", $"T_min: the minimum operating temperature ({F(c.Si("T_min") - 273.15)} °C) must not be above the maximum, T_max ({F(c.Si("T_max") - 273.15)} °C).");
            return;
        }

        c.Assign("p2_g", "max(pa_2 - p_atm, 0 bar)", "bar", "Highest working pressure, gauge");
        var hasPs = c.Has("PS");

        if (hasPs)
        {
            c.Input("PS");

            if (c.Si("PS") < c.Si("p2_g") * (1 - 1e-9))
            {
                c.Block("PS", $"PS: the vessel's maximum allowable pressure ({F(c.Si("PS") / 1e5)} bar gauge) must not be below the highest working pressure p_2 ({F(c.Si("p2_g") / 1e5)} bar gauge); enter the nameplate or relief-valve setting.");
                return;
            }
        }

        var n = c.Si("n");
        var dv = c.Scalar("dV");
        double ideal;

        try
        {
            ideal = GasLaw.ChargeVolume(dv.Si, pa0, pa1, pa2, n);
        }
        catch (CalcEvaluationException ex)
        {
            c.Block("pressures", "p_2: " + ex.Message);
            return;
        }

        if (!double.IsFinite(ideal))
        {
            c.Block("pressures", "p_2: the pressures leave no usable gas volume between them; raise p_2 or lower p_1.");
            return;
        }

        var sf = c.Session.SignificantFigures;
        var exponentText = n == GasLaw.Isothermal ? string.Empty : $"^(1/{F(n)})";
        c.Record(
            "V0_ideal",
            "dV / [(pa_0/pa_1)^(1/n) − (pa_0/pa_2)^(1/n)]",
            $"{dv.Format(sf)} / [({F(pa0 / pa1)}){exponentText} − ({F(pa0 / pa2)}){exponentText}]",
            new ScalarValue(ideal, dv.Dimension, litre),
            "Gas volume for the displacement, ideal gas",
            "p·Vⁿ = const");
        c.Assign("f_T", "(T_max / 1 K) / (T_min / 1 K)", null, "Temperature scaling of the gas volume: maximum over minimum absolute temperature");
        c.Assign("V0", "V0_ideal * f_rg * f_T", "L", "Pre-charge gas volume V0 (accumulator size required)");

        c.Assign("T_ratio", "(T_c / 1 K) / (T_max / 1 K)", null, "Charging over maximum absolute temperature");
        c.Assign("p0", "pa_0 * T_ratio", "bar", "Absolute pre-charge pressure at the charging temperature");
        c.Assign("p0_min", "pa_0 / f_T", "bar", "Absolute pre-charge pressure at the minimum operating temperature");
        c.Assign("p0_set", absolute ? "p0" : "p0 - p_atm", "bar", absolute ? "Pre-charge to set at the charging temperature (absolute)" : "Pre-charge to set at the charging temperature (gauge)");

        c.Assign("u_r0", "pa_0 / (r_0 * pa_1)", null, "Pre-charge against the p0/p1 rule");
        c.Assign("u_rp", "(pa_2 / p0_min) / r_p", null, "Pressure ratio p2/p0 (p0 at the minimum temperature) against the rule");

        var outputs = new List<(string, string)>
        {
            ("V0", "Accumulator gas volume required"), ("p0", "Pre-charge, absolute, at the charging temperature"), ("p0_set", "Pre-charge to set at the charging temperature"), ("p0_min", "Pre-charge, absolute, at the minimum operating temperature"), ("f_T", "Gas volume scaling T_max/T_min"),
            ("u_r0", "Pre-charge rule utilisation (p0/p1)"), ("u_rp", "Pressure ratio utilisation (p2/p0 at the minimum temperature)"),
        };

        // The next listed size: the smallest not below V0 (a hair of rounding allowed). The list is the user's record.
        var sizes = c.Rows("sizes").Select(r => SizeSi(r, litre)).Where(v => v > 0).Order().ToList();
        var sizeCovered = true;

        if (sizes.Count > 0)
        {
            var needed = c.Si("V0") * (1 - 1e-9);
            var pick = sizes.FirstOrDefault(v => v >= needed);

            if (pick > 0)
            {
                c.Record("V_sel", "smallest listed size not below V0", $"V0 = {c.Scalar("V0").Format(sf)}, {sizes.Count} sizes listed", new ScalarValue(pick, dv.Dimension, litre), "Selected accumulator size");
                c.Assign("u_V", "V0 / V_sel", null, "Required volume over the selected size");
                outputs.Insert(1, ("V_sel", "Selected size"));
                outputs.Insert(2, ("u_V", "Selected size utilisation"));
            }
            else
            {
                sizeCovered = false;
                c.Warn("size", $"sizes: no listed size is as large as the required {F(c.Si("V0") * 1e3)} L; the largest listed is {F(sizes[^1] * 1e3)} L. Use two accumulators or a larger range.");
            }
        }

        // The PSSR indication needs the vessel's own PS and volume: PS entered, volume entered or else the selected size.
        var vesselEntered = c.Has("V_vessel");
        var selected = sizes.Count > 0 && sizeCovered;

        if (vesselEntered)
        {
            c.Input("V_vessel");

            if (c.Si("V_vessel") < c.Si("V0") * (1 - 1e-9))
            {
                c.Block("vessel", $"V_vessel: the vessel volume ({F(c.Si("V_vessel") * 1e3)} L) must not be below the calculated gas volume V0 ({F(c.Si("V0") * 1e3)} L).");
                return;
            }
        }

        var volumeSymbol = vesselEntered ? "V_vessel" : selected ? "V_sel" : null;
        var indication = hasPs && volumeSymbol is not null;

        if (indication)
        {
            c.Assign("bar_litres", $"PS * {volumeSymbol}", "bar*L", vesselEntered ? "Pressure-volume product PS·V (PS and volume of the vessel as entered)" : "Pressure-volume product PS·V (PS as entered, V the selected size)");
            c.Assign("BL_pssr", $"{PssrBarLitres} bar*L", "bar*L", "PSSR Schedule 1 exception threshold (indication)", "PSSR Schedule 1");
            outputs.Add(("bar_litres", "Pressure-volume product (bar·litres)"));
        }

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        c.Output("pssr", "PSSR Schedule 1 250 bar·L exception (indication only)", new TextValue(!indication
            ? "indication not available — enter the vessel's PS and volume (or a size list)."
            : c.Si("bar_litres") < c.Si("BL_pssr")
                ? $"{F(c.Si("bar_litres") / 100.0)} bar·L is below 250 bar·L: the exception may apply; confirm with the competent person."
                : $"{F(c.Si("bar_litres") / 100.0)} bar·L is at or above 250 bar·L: the exception does not apply; PSSR and PED / UK PER duties follow."));

        c.CheckUtilisation("Pre-charge within the p0/p1 rule", "u_r0", "accumulator.rules record");
        c.CheckUtilisation("Pressure ratio within the p2/p0 rule", "u_rp", "accumulator.rules record");

        if (sizes.Count > 0 && !sizeCovered)
            c.CheckCondition("A listed size covers the required volume", false, $"V0 = {F(c.Si("V0") * 1e3)} L", $"largest listed {F(sizes[^1] * 1e3)} L", "V0");

        if (pa2 > RealGasWarnBar * 1e5)
        {
            c.Warn("realgas", c.Si("f_rg") == 1.0
                ? $"p_2: the highest working pressure is {F(pa2 / 1e5)} bar absolute, above 200 bar; nitrogen departs from the ideal-gas law there and the volume shown is the ideal-gas value. Enter a real-gas factor f_rg from a gas-property source."
                : $"p_2: the highest working pressure is {F(pa2 / 1e5)} bar absolute, above 200 bar; the volume carries your real-gas factor f_rg = {F(c.Si("f_rg"))}.");
        }

        switch (mode)
        {
            case ModeShock:
                c.Assume("Shock and pulsation duty (screening): ΔV is the liquid volume to absorb while the pressure rises from p_1 to the permitted peak p_2 in a time too short for heat exchange; the dynamic response of the line (wave speed, valve closing time, pulsation frequency) is not modelled.", "Gas-law screening only.");
                c.Warn("shock", "mode: shock and pulsation sizing here is a gas-volume screening for the stated ΔV; it does not model the pressure wave. Confirm with the manufacturer's surge calculation.");

                if (n < 1.3)
                    c.Warn("exponent", $"n: a shock event is fast and adiabatic; n = {F(n)} gives a smaller volume than n near 1.4.");

                break;

            case ModeEmergency:
                c.Assume("Emergency stroke duty: ΔV is the volume the emergency stroke needs (swept volume plus line compression; linkable from the cylinder circuit); p_1 is the lowest pressure at which the stroke still has the force it needs; p_2 is the pressure the accumulator is held at.", "Gas-law sizing for a stored-energy stroke.");

                if (n < 1.3)
                    c.Warn("exponent", $"n: an emergency stroke discharges quickly; n = {F(n)} is the slow, isothermal end. Use n near 1.4 unless the stroke takes minutes.");

                break;

            default:
                c.Assume("Energy storage duty: ΔV is the liquid volume the accumulator delivers between p_2 and p_1 over a cycle; n = 1 for a slow discharge, towards 1.4 for a fast one.", "Gas-law sizing for a stored-energy duty.");
                break;
        }
    }

    private static double SizeSi(CalcRow row, Unit litre) =>
        row.Get("V") is { Number: { } v } cell && double.IsFinite(v) ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : litre).ToSi(v) : 0;

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
