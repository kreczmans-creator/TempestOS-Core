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

    /// <summary>The gas process is taken from the duty: a slow charge for an emergency stroke, the one exponent both ways otherwise.</summary>
    public const string ProcessByDuty = "By duty";

    /// <summary>The gas is charged as fast as it is discharged (one exponent n from the pre-charge to both working pressures), as in a pulsation or a fast cycle.</summary>
    public const string ProcessSame = "Same exponent both ways";

    /// <summary>The gas is held at p2 after a slow (isothermal) charge and discharged at n.</summary>
    public const string ProcessSlowCharge = "Slow charge, discharge at n";

    public const string Gauge = "Gauge";
    public const string Absolute = "Absolute";

    /// <summary>The bar·litre product below which the Addendum B §B5 indication reads "below the exception".</summary>
    public const double PssrBarLitres = 250.0;

    /// <summary>Real-gas behaviour matters above this absolute pressure (200 bar): the sheet warns.</summary>
    public const double RealGasWarnBar = 200.0;

    /// <summary>
    /// Above this absolute pressure (300 bar) an ideal-gas volume (f_rg left at 1) is refused: reference-EOS nitrogen at 20 °C needs 1.25 to 1.35 times the ideal volume in a narrow window at 300 bar, 1.6 at 500 bar and 1.7 to 2.0 times from 450 to 700 bar,
    /// so the sheet would size an accumulator about half as big as the duty needs (fp.accumulator-01). Between 200 and 300 bar the sheet only warns: the template's own reference case (Parker, 90/100/210 bar absolute, real factor 1.05) sits there; blocking from 200 bar, as the finding first proposed, would refuse it.
    /// </summary>
    public const double RealGasBlockBar = 300.0;

    /// <summary>A shock or emergency discharge is fast: an exponent below this sizes a smaller gas volume than the adiabatic 1.4 and fails the exponent check.</summary>
    public const double FastExponent = 1.3;

    /// <summary>Real-gas behaviour of nitrogen is already worth a notice from this absolute pressure (100 bar) when the ideal gas is left on, and from 50 bar for an exponent above 1.</summary>
    public const double RealGasEarlyWarnBar = 100.0;

    /// <summary>An exponent above 1 (the discharge not isothermal) is compared with the real nitrogen's own, larger, isentropic exponent from this absolute pressure (50 bar).</summary>
    public const double RealExponentWarnBar = 50.0;

    /// <summary>The largest real-gas factor f_rg the sheet takes: reference-EOS nitrogen at 20 °C needs 2.0 for a 600 to 700 bar window, and a narrow window needs more than Z(p_2).</summary>
    public const double RealGasFactorMax = 3.0;

    private static readonly IReadOnlyList<CalcColumnSpec> SizeColumns =
    [
        new("V", "Nominal gas volume", CalcInputKind.Quantity, "L", Min: 0.01, Max: 5000, HardMin: 0.01, HardMax: 5000),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.6.0", CalcDiscipline.FluidPower, "Accumulator sizing (gas law)",
        "The pre-charge gas volume V0 of a hydraulic accumulator that delivers, or absorbs, a liquid volume ΔV between the lowest working pressure p1 and the highest p2, from p·Vⁿ = const with absolute pressures and an exponent n between 1 (isothermal) and 1.4 (adiabatic), the gas either charged as fast as it is discharged or (a held accumulator, the default for an emergency stroke) charged slowly and isothermally to p2 and then discharged at n; the volume scaled to the lowest operating temperature (T_max/T_min) and the pre-charge pressure at the charging temperature; the next size from your own size list; and the bar·litre product PS·V of the vessel's own maximum allowable pressure and volume against the PSSR Schedule 1 250 bar·L exception. Three modes: energy storage, shock and pulsation (screening only) and emergency stroke (ΔV is the stroke volume, linkable from the cylinder circuit). Not a pressure-vessel code check.",
        "Absolute pressures: p_a = p_gauge + p_atm (or as entered, in Absolute mode)\n" +
        "Gas volume, one exponent both ways (charged as fast as it is discharged): V0 = ΔV / [(p0/p1)^(1/n) − (p0/p2)^(1/n)], n = 1 isothermal … 1.4 adiabatic\n" +
        "Gas volume, slow charge and discharge at n (a held accumulator): charged isothermally to p2, V2 = V0·p0/p2, then discharged at n to p1, V1 = V2·(p2/p1)^(1/n), ΔV = V1 − V2, so V0 = ΔV·(p2/p0) / [(p2/p1)^(1/n) − 1]\n" +
        "Either volume × the real-gas factor f_rg (user input, at least 1) and × the cold-gas factor, because p0 is defined at T_max and the gas delivers less when cold: (T_max + 273.15)/(T_min + 273.15) for the held accumulator, that ratio to the power 1/n for one exponent both ways\n" +
        "Pre-charge rules (records, applied to absolute pressures): p0 ≤ r_0·p1 at T_max (default 0.9, bladder type) and p2/p0(T_min) ≤ r_p (default 4), p0(T_min) = p0·(T_min + 273.15)/(T_max + 273.15)\n" +
        "Pre-charge at the charging temperature: p0(T_c) = p0(T_max)·(T_c + 273.15)/(T_max + 273.15)\n" +
        "Next size: the smallest listed size not below V0\n" +
        "PSSR bar-litre product: PS·V, PS = the vessel's maximum allowable pressure (bar gauge, entered, not below p2 gauge), V = the vessel volume entered (default the selected size); compared with 250 bar·L (PSSR Schedule 1, indication only; not available without PS and a volume)",
        [
            new CalcInputSpec("mode", "Duty", CalcInputKind.Choice, Choices: [ModeEnergy, ModeShock, ModeEmergency], DefaultChoice: ModeEnergy, Group: "Duty"),
            new CalcInputSpec("dV", "Liquid volume ΔV delivered, absorbed or (emergency stroke) needed for the stroke", CalcInputKind.Quantity, "L", Default: 10, Min: 0.001, Max: 1000, HardMin: 0.001, HardMax: 1000, Group: "Duty"),
            new CalcInputSpec("process", "Gas process (By duty: an emergency stroke is charged slowly to p2 and discharged at n, the other duties use one exponent both ways)", CalcInputKind.Choice, Choices: [ProcessByDuty, ProcessSame, ProcessSlowCharge], DefaultChoice: ProcessByDuty, Group: "Duty"),
            new CalcInputSpec("p_basis", "Pressures entered as", CalcInputKind.Choice, Choices: [Gauge, Absolute], DefaultChoice: Gauge, Group: "Pressures"),
            new CalcInputSpec("p_0", "Gas pre-charge pressure p0, at the maximum operating temperature", CalcInputKind.Quantity, "bar", Default: 70, Min: 0, Max: 700, HardMin: 0, HardMax: 700, Group: "Pressures", PressureBasis: "@p_basis"),
            new CalcInputSpec("p_1", "Lowest working pressure p1", CalcInputKind.Quantity, "bar", Default: 100, Min: 0, Max: 700, HardMin: 0, HardMax: 700, Group: "Pressures", PressureBasis: "@p_basis"),
            new CalcInputSpec("p_2", "Highest working pressure p2", CalcInputKind.Quantity, "bar", Default: 180, Min: 0, Max: 700, HardMin: 0, HardMax: 700, Group: "Pressures", PressureBasis: "@p_basis"),
            new CalcInputSpec("p_atm", "Atmospheric pressure (gauge to absolute)", CalcInputKind.Quantity, "bar", Default: 1.01325, Min: 0.5, Max: 1.1, HardMin: 0.5, HardMax: 1.1, Group: "Pressures", PressureBasis: CalcInputSpec.BasisAbsolute),
            new CalcInputSpec("n", "Gas exponent n of the discharge (1 isothermal, slow; 1.4 adiabatic, fast); with the same exponent both ways it is also the charge exponent", CalcInputKind.Number, Default: 1, Min: 1, Max: 1.4, HardMin: 1, HardMax: 1.4, Group: "Gas"),
            new CalcInputSpec("f_rg", "Real-gas factor on V0 (1 = ideal gas; at least 1). It is the ratio of the real to the ideal gas volume for THIS pressure window, not the compressibility factor Z at p_2: from a gas-property source, f_rg = Z_0·(1/p_1 − 1/p_2)/(Z_1/p_1 − Z_2/p_2) for an isothermal discharge, Z at p_0, p_1 and p_2, which is larger than Z_2 for a narrow window", CalcInputKind.Number, Default: 1, Min: 1, Max: RealGasFactorMax, HardMin: 1, HardMax: RealGasFactorMax, Group: "Gas"),
            new CalcInputSpec("T_max", "Maximum operating temperature (p0 applies here)", CalcInputKind.Quantity, "degC", Default: 50, Min: -40, Max: 150, HardMin: -40, HardMax: 150, Group: "Temperature"),
            new CalcInputSpec("T_min", "Minimum operating temperature (the gas is coldest and delivers least here; V0 is scaled by T_max/T_min)", CalcInputKind.Quantity, "degC", Default: 20, Min: -40, Max: 150, HardMin: -40, HardMax: 150, Group: "Temperature"),
            new CalcInputSpec("T_c", "Charging temperature", CalcInputKind.Quantity, "degC", Default: 20, Min: -40, Max: 100, HardMin: -40, HardMax: 100, Group: "Temperature"),
            new CalcInputSpec("r_0", "Pre-charge rule: p0 (at T_max) at most this fraction of p1, on absolute pressures (state whether the manufacturer's ratio is on gauge or absolute pressures)", CalcInputKind.Number, Default: 0.9, Min: 0.1, Max: 1, HardMin: 0.1, HardMax: 1, Group: "Pre-charge rules"),
            new CalcInputSpec("r_p", "Pre-charge rule: p2/p0 (p0 at T_min) at most this ratio, on absolute pressures", CalcInputKind.Number, Default: 4, Min: 1, Max: 20, HardMin: 1, HardMax: 20, Group: "Pre-charge rules"),
            new CalcInputSpec("sizes", "Available accumulator sizes (from the manufacturer's range)", CalcInputKind.Table, Columns: SizeColumns, MaxRows: 60, IsOptional: true, Group: "Size list"),
            new CalcInputSpec("PS", "Vessel maximum allowable pressure PS, bar gauge (nameplate or relief setting; not below p2 gauge; needed for the PSSR indication)", CalcInputKind.Quantity, "bar", Min: 0, Max: 1500, HardMin: 0, HardMax: 1500, IsOptional: true, Group: "PSSR indication", PressureBasis: CalcInputSpec.BasisGauge),
            new CalcInputSpec("V_vessel", "Vessel internal volume V (blank = the selected size; not below V0)", CalcInputKind.Quantity, "L", Min: 0.01, Max: 5000, HardMin: 0.01, HardMax: 5000, IsOptional: true, Group: "PSSR indication"),
            new CalcInputSpec("p_load", "Lowest pressure the load still needs, bar gauge (an emergency stroke: the cylinder pressure its push load needs, linked from the cylinder circuit's p_push; p_1 must not be below it; blank = not checked)", CalcInputKind.Quantity, "bar", Min: 0, Max: 700, HardMin: 0, HardMax: 700, IsOptional: true, Group: "Duty", PressureBasis: CalcInputSpec.BasisGauge),
        ],
        [
            new CalcAssumption("All gas-law pressures are absolute. A gauge entry is converted with the atmospheric pressure entered here, so the same numbers read as gauge or as absolute give different volumes.", "p·Vⁿ = const on a fixed mass of gas."),
            new CalcAssumption("The exponent n is the user's: 1 for a slow discharge with heat exchange (isothermal), towards 1.4 for a fast one (adiabatic). With the same exponent both ways the larger n gives the larger volume for a given ΔV.", "Gas-law exponents, closed form."),
            new CalcAssumption("The gas process is stated on the sheet. A held accumulator (an emergency stroke, by default) is charged slowly, so isothermally with charge exponent 1 (V2 = V0·p0/p2), and discharged at the exponent n; sizing it with one exponent from the pre-charge to both pressures understates the volume. The one-exponent form (charge exponent = n) is for a gas charged as fast as it is discharged, as in a pulsation or a fast cycle; choose it in the energy-storage duty for that case. The shock and pulsation duty always uses one exponent.", "Gas-law sizing of a held accumulator: slow charge, fast discharge."),
            new CalcAssumption("The gas is ideal unless a real-gas factor is entered (at least 1: nitrogen's compressibility factor exceeds 1 at high pressure, so the real gas needs more volume, never less); above 200 bar the sheet warns that nitrogen departs from the ideal-gas law and a factor from a gas-property source should be entered.", "Real-gas correction as a user factor."),
            new CalcAssumption("The pre-charge p0 is defined at the maximum operating temperature T_max and V0 is sized there; at the lowest operating temperature T_min the same gas delivers only the fraction T_min/T_max (absolute) of ΔV, so V0 is multiplied by T_max/T_min for a held accumulator (isothermal charge to p2) and by (T_max/T_min)^(1/n) when one exponent n holds both ways (the pre-charge at T_min is p0·T_min/T_max), and the p2/p0 rule is checked with p0 at T_min. The temperature range is the user's; with T_min = T_max the correction is 1.", "Manufacturer sizing practice; the gas law at constant mass."),
            new CalcAssumption("The pre-charge rules (p0 ≤ 0.9·p1 and p2/p0 ≤ 4 by default) are records for the accumulator type and make and are checked as PASS/FAIL on ABSOLUTE pressures, whichever basis the pressures are entered on; the right values, and whether the manufacturer states the ratio on gauge or absolute pressures, come from the manufacturer (the same numbers read as gauge pressures give a different ratio).", "Rules are records, not built-in limits."),
            new CalcAssumption("The bar·litre product is PS times V of the vessel itself: PS is the vessel's maximum allowable pressure in bar gauge (the nameplate or relief setting, never below the highest working pressure p2) and V its internal volume (the selected size unless entered). Without PS and a volume the indication is not available. It indicates whether the 250 bar·L exception of PSSR Schedule 1 can apply; it is not a compliance statement, and PED / UK PER, PSSR examination and the vessel's own design code stay with the manufacturer and the competent person.", "Regulatory gating: no pressure-vessel code check."),
        ],
        [
            new CalcClauseRef("Pressure Systems Safety Regulations 2000 (SI 2000/128)", "Schedule 1", "250 bar·litre exception, indication only"),
            new CalcClauseRef("Pressure Equipment Directive 2014/68/EU and UK PER 2016", "—", "Pressure equipment; not checked here"),
            new CalcClauseRef("Esposito, Fluid Power with Applications", "Ch. on accumulators", "Gas-charged accumulator sizing, p·Vⁿ = const"),
        ],
        [
            new CalcApplicabilityRule("pressures", "Pre-charge, lowest and highest working pressure must be in increasing order (absolute) and above zero.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("basis", "On the Absolute basis, a pressure below the atmospheric pressure entered here cannot be an absolute pressure: a gauge value was probably typed as absolute.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("realgas-ideal", "The highest working pressure is above 300 bar absolute and the real-gas factor is left at 1 (ideal gas): the volume would be about half the real nitrogen volume; enter f_rg.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("realgas", "The highest working pressure is above 200 bar absolute, or above 100 bar absolute with the ideal gas left on (f_rg = 1): nitrogen departs from the ideal-gas law and the volume is too small, so with f_rg left at 1 and no recorded basis for it the real-gas check fails (a block above 300 bar absolute).", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("realgas-window", "A narrow pressure window above 100 bar absolute: the real-gas factor on V0 is larger than the compressibility factor at p_2.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("exponent-real", "An exponent above 1 from 50 bar absolute: real nitrogen is stiffer than the ideal gas of that exponent, so an adiabatic discharge needs more gas volume than shown.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("load-pressure", "An emergency stroke without the pressure its load needs: p_1 is not checked against the push load.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("shock", "Shock and pulsation sizing is a screening estimate; the dynamic response of the line is not modelled.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("exponent", "A shock or emergency discharge is fast: an exponent below 1.3 sizes a smaller volume than the usual 1.4, so the exponent check fails unless a basis for n is recorded (a stroke that takes minutes).", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("process", "The gas process chosen does not suit the duty: an emergency stroke sized with one exponent both ways understates the volume of a held accumulator, and the shock and pulsation duty uses one exponent whatever process is chosen.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("link-basis", "A pressure is linked from another calculation while the pressures are entered as Absolute: linked pressures are gauge, so the atmospheric pressure is added to the linked value.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("size", "No listed size is as large as the calculated volume.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("temperature", "The minimum operating temperature must not be above the maximum.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("PS", "PS (bar gauge) must not be below the highest working pressure p2 (gauge).", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("vessel", "The vessel volume must not be below the calculated gas volume V0.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
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
            new CalcTestCase("ka-adiabatic", "Fast charge and fast discharge (one exponent both ways, as in a pulsation cycle), n = 1.4, p0 90, p1 100, p2 210 bar absolute, ΔV 14 L (analytic)",
                new CalcReferenceSource("Analytic (V0 = ΔV/[(p0/p1)^(1/n) − (p0/p2)^(1/n)], the gas charged as fast as it is discharged)", null, "(0.9)^(1/1.4) = 0.927505, (90/210)^(1/1.4) = 0.545958: V0 = 14/0.381546 = 36.69279 L; an analytic recompute of the one-exponent relation, not a published example"),
                [CalcInputValue.Quantity("dV", 14, "L"), CalcInputValue.Choice("process", ProcessSame), CalcInputValue.Choice("p_basis", Absolute), CalcInputValue.Quantity("p_0", 90, "bar"), CalcInputValue.Quantity("p_1", 100, "bar"), CalcInputValue.Quantity("p_2", 210, "bar"), CalcInputValue.Of("n", 1.4), CalcInputValue.Quantity("T_min", 50, "degC")],
                [new CalcExpectedValue("V0", 36.69278925, "L", 1e-9)]),
            new CalcTestCase("ka-held-slow-charge-fast-discharge", "Emergency stroke, held at p2: charged isothermally, discharged at n = 1.4, p0 90, p1 100, p2 210 bar absolute, ΔV 14 L (analytic)",
                new CalcReferenceSource("Parker Hannifin, HY10-4004/UK, accumulator sizing (mixed isothermal charge and adiabatic discharge relation, as in manufacturers' sizing guides); analytic recompute in python3", null, "V2 = V0·p0/p2 (isothermal charge), V1 = V2·(p2/p1)^(1/n) (discharge): V0 = ΔV·(p2/p0)/[(p2/p1)^(1/n) − 1] = 14 × 2.333333/(2.1^(1/1.4) − 1) = 14 × 2.333333/0.698856 = 46.74304 L, against 36.69279 L when the charge is as fast as the discharge (ka-adiabatic); the gas held at p2 is V0·p0/p2 = 20.03273 L and expands to 34.03273 L at p1 (ΔV = 14 L); T_min = T_max, so no temperature scaling; p2/p0 = 2.3333, u_rp = 0.58333. The relation is recomputed, not pinned to a published worked example: to be checked against the manufacturer's sizing guide"),
                [CalcInputValue.Choice("mode", ModeEmergency), CalcInputValue.Quantity("dV", 14, "L"), CalcInputValue.Choice("p_basis", Absolute), CalcInputValue.Quantity("p_0", 90, "bar"), CalcInputValue.Quantity("p_1", 100, "bar"), CalcInputValue.Quantity("p_2", 210, "bar"), CalcInputValue.Of("n", 1.4), CalcInputValue.Quantity("T_min", 50, "degC")],
                [new CalcExpectedValue("V0", 46.74303967, "L", 1e-9), new CalcExpectedValue("u_r0", 1.0, "", 1e-9), new CalcExpectedValue("u_rp", 0.5833333333, "", 1e-9)],
                CalcReferenceStatus.Unconfirmed),
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
                new CalcReferenceSource("Parker Hannifin, HY10-4004/UK, accumulator sizing example as quoted second-hand", null, "P2 210, P1 100, P0 90 bar, ΔV 14 L, isothermal: V0 ≈ 29.7–29.8 L, catalogue selection 32 L; to be checked against the owner's copy of the catalogue, including whether the pressures are gauge or absolute", Secondary: true),
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
        c.MarkUtilisation("u_V", "u_load", "u_r0", "u_rp");
        foreach (var s in new[] { "dV", "p_0", "p_1", "p_2", "p_atm", "n", "f_rg", "T_max", "T_min", "T_c", "r_0", "r_p" })
            c.Input(s);

        var mode = c.InputText("mode");
        var absolute = c.InputText("p_basis") == Absolute;
        var litre = UnitCatalog.Parse("L");

        // A pressure linked from another calculation (the cylinder's supply pressure p is gauge) is gauge whatever this sheet's basis is: Absolute mode took the gauge value as absolute,
        // one atmosphere low and a smaller volume (1.5.1 and before: a warning that did not stop a pass). The atmospheric pressure is now added to a linked pressure on the Absolute basis.
        var linkedGauge = new HashSet<string>();

        if (absolute)
        {
            foreach (var symbol in new[] { "p_0", "p_1", "p_2" })
            {
                if (CalcLink.From(c.Inputs.Find(symbol)?.Provenance) is { } link)
                {
                    linkedGauge.Add(symbol);
                    c.Warn("link-basis", $"{symbol}: linked from '{link.OutputSymbol}' of another calculation while the pressures are entered as Absolute; the outputs that other calculations offer as pressures (the cylinder's supply pressure p) are gauge, so the atmospheric pressure ({F(c.Si("p_atm") / 1e5)} bar) is added to the linked value here. Set p_basis to Gauge to enter every pressure the same way.");
                }
            }
        }

        string Absolutised(string symbol) => !absolute || linkedGauge.Contains(symbol) ? $"{symbol} + p_atm" : symbol;

        c.Assign("pa_0", Absolutised("p_0"), UnitCatalog.BarAbsolute, "Pre-charge pressure, absolute");
        c.Assign("pa_1", Absolutised("p_1"), UnitCatalog.BarAbsolute, "Lowest working pressure, absolute");
        c.Assign("pa_2", Absolutised("p_2"), UnitCatalog.BarAbsolute, "Highest working pressure, absolute");

        double pa0 = c.Si("pa_0"), pa1 = c.Si("pa_1"), pa2 = c.Si("pa_2");

        if (!(pa0 > 0))
        {
            c.Block("pressures", "p_0: the absolute pre-charge pressure must be above zero; check whether the pressures are entered as gauge or absolute.");
            return;
        }

        // An absolute pressure is never below the atmosphere: a lower number is a gauge value typed on the Absolute basis (it would size the gas volume on pressures one atmosphere low).
        if (absolute)
        {
            foreach (var (symbol, value) in new[] { ("p_0", pa0), ("p_1", pa1), ("p_2", pa2) })
            {
                if (value < c.Si("p_atm") * (1 - 1e-9))
                {
                    c.Block("basis", $"{symbol}: {F(value / 1e5)} bar a is below the atmospheric pressure ({F(c.Si("p_atm") / 1e5)} bar a), which no absolute pressure can be; it looks like a gauge value entered on the Absolute basis. Set p_basis to Gauge, or enter the absolute pressure.");
                    return;
                }
            }
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

        // Real nitrogen needs 1.7 to 2.0 times the ideal volume from 450 to 700 bar absolute (reference EOS, 20 °C), so the ideal-gas number with f_rg left at 1 would be about
        // half the real gas volume: no safe sheet comes out of it. The volume is refused until the real-gas factor is entered (fp.accumulator-01).
        if (pa2 > RealGasBlockBar * 1e5 && c.Si("f_rg") == 1.0)
        {
            c.Block("realgas-ideal", $"f_rg: the highest working pressure is {F(pa2 / 1e5)} bar absolute, above {F(RealGasBlockBar)} bar, and the real-gas factor is left at 1 (ideal gas). Nitrogen needs 1.7 to 2.0 times the ideal volume from 450 to 700 bar absolute (reference equation of state, 20 °C), so the volume would be about half the real one. Enter the real-gas factor f_rg (up to {F(RealGasFactorMax)}) for your pressure window from a gas-property source.");
            return;
        }

        c.Assign("p2_g", "max(pa_2 - p_atm, 0 bar)", UnitCatalog.BarGauge, "Highest working pressure, gauge");
        var hasLoad = c.Has("p_load");

        if (hasLoad)
        {
            c.Input("p_load");
            c.Assign("p1_g", "max(pa_1 - p_atm, 0.001 bar)", UnitCatalog.BarGauge, "Lowest working pressure, gauge");
            c.Assign("u_load", "p_load / p1_g", null, "Pressure the load needs over the lowest working pressure");
        }

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
        var chosen = c.InputText("process");

        // The shock and pulsation duty is a fast event on a gas already at p1: one exponent. An emergency stroke is held at p2 (charged slowly) by default; the other duties use one exponent unless a slow charge is chosen.
        var slowCharge = mode != ModeShock && (chosen == ProcessSlowCharge || (chosen == ProcessByDuty && mode == ModeEmergency));
        double ideal, singleExponent;

        try
        {
            singleExponent = GasLaw.ChargeVolume(dv.Si, pa0, pa1, pa2, n);
            ideal = slowCharge ? GasLaw.ChargeVolumeSplit(dv.Si, pa0, pa1, pa2, GasLaw.Isothermal, n) : singleExponent;
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

        if (slowCharge)
        {
            c.Record(
                "V0_ideal",
                "dV · (pa_2/pa_0) / [(pa_2/pa_1)^(1/n) − 1], charged isothermally to pa_2, discharged at n",
                $"{dv.Format(sf)} · {F(pa2 / pa0)} / [({F(pa2 / pa1)}){exponentText} − 1]",
                new ScalarValue(ideal, dv.Dimension, litre),
                "Gas volume for the displacement, ideal gas, slow charge and discharge at n",
                "p·Vⁿ = const, charge exponent 1, discharge exponent n");
        }
        else
        {
            c.Record(
                "V0_ideal",
                "dV / [(pa_0/pa_1)^(1/n) − (pa_0/pa_2)^(1/n)]",
                $"{dv.Format(sf)} / [({F(pa0 / pa1)}){exponentText} − ({F(pa0 / pa2)}){exponentText}]",
                new ScalarValue(ideal, dv.Dimension, litre),
                "Gas volume for the displacement, ideal gas",
                "p·Vⁿ = const");
        }

        // The pre-charge is defined at T_max and the gas cools at constant volume: p0' = p0·T_min/T_max. A held accumulator (isothermal charge to p2, then n) needs V0 ∝ p2/p0', the first power of the
        // temperature ratio; a gas charged as fast as it is discharged (one exponent) needs V0 ∝ (p0/p0')^(1/n), the ratio to the power 1/n (fp.accumulator-03).
        c.Assign("f_Tabs", "(T_max / 1 K) / (T_min / 1 K)", null, "Absolute temperature ratio T_max/T_min");
        c.Assign("f_T", slowCharge ? "f_Tabs" : "f_Tabs^(1 / n)", null, slowCharge ? "Temperature scaling of the gas volume: the temperature ratio (isothermal charge to p2)" : "Temperature scaling of the gas volume: the temperature ratio to the power 1/n (one exponent both ways)");
        c.Assign("V0", "V0_ideal * f_rg * f_T", "L", "Pre-charge gas volume V0 (accumulator size required)");

        c.Assign("T_ratio", "(T_c / 1 K) / (T_max / 1 K)", null, "Charging over maximum absolute temperature");
        c.Assign("p0", "pa_0 * T_ratio", UnitCatalog.BarAbsolute, "Absolute pre-charge pressure at the charging temperature");
        c.Assign("p0_min", "pa_0 / f_Tabs", UnitCatalog.BarAbsolute, "Absolute pre-charge pressure at the minimum operating temperature");
        c.Assign("p0_set", absolute ? "p0" : "p0 - p_atm", absolute ? UnitCatalog.BarAbsolute : UnitCatalog.BarGauge, absolute ? "Pre-charge to set at the charging temperature (absolute)" : "Pre-charge to set at the charging temperature (gauge)");

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

        if (hasLoad)
            outputs.Add(("u_load", "Load pressure over the lowest working pressure p_1"));

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        c.Output("pssr", "PSSR Schedule 1 250 bar·L exception (indication only)", new TextValue(!indication
            ? "indication not available — enter the vessel's PS and volume (or a size list)."
            : c.Si("bar_litres") < c.Si("BL_pssr")
                ? $"{F(c.Si("bar_litres") / 100.0)} bar·L is below 250 bar·L: the exception may apply; confirm with the competent person."
                : $"{F(c.Si("bar_litres") / 100.0)} bar·L is at or above 250 bar·L: the exception does not apply; PSSR and PED / UK PER duties follow."));

        c.CheckUtilisation("Pre-charge within the p0/p1 rule", "u_r0", "accumulator.rules record");
        c.CheckUtilisation("Pressure ratio within the p2/p0 rule", "u_rp", "accumulator.rules record");

        if (hasLoad)
            c.CheckUtilisation("Lowest working pressure p_1 carries the load", "u_load");

        if (sizes.Count > 0 && !sizeCovered)
            c.CheckCondition("A listed size covers the required volume", false, $"V0 = {F(c.Si("V0") * 1e3)} L", $"largest listed {F(sizes[^1] * 1e3)} L", "V0");

        var frg = c.Si("f_rg");

        if (pa2 > RealGasWarnBar * 1e5)
        {
            c.Warn("realgas", frg == 1.0
                ? $"p_2: the highest working pressure is {F(pa2 / 1e5)} bar absolute, above 200 bar; nitrogen departs from the ideal-gas law there and the volume shown is the ideal-gas value, too small (reference-EOS nitrogen at 20 °C needs 1.7 to 2.0 times the ideal volume from 450 to 700 bar). Enter a real-gas factor f_rg (up to {F(RealGasFactorMax)}) from a gas-property source."
                : $"p_2: the highest working pressure is {F(pa2 / 1e5)} bar absolute, above 200 bar; the volume carries your real-gas factor f_rg = {F(frg)}.");
        }
        else if (frg == 1.0 && pa2 > RealGasEarlyWarnBar * 1e5)
        {
            c.Warn("realgas", $"p_2: the highest working pressure is {F(pa2 / 1e5)} bar absolute and the real-gas factor is left at 1; nitrogen already departs from the ideal-gas law above about 100 bar and the volume shown is the ideal-gas value, too small (reference-EOS nitrogen at 20 °C needs 1.13 times the ideal volume for a 170/190/200 bar absolute window). Enter f_rg from a gas-property source.");
        }

        // With the ideal gas left on above 100 bar the volume is the ideal-gas value, 13 % and more too small (it warned and the sheet passed): the real-gas check fails until f_rg is entered, or until
        // the person records a basis for taking the ideal gas (an assumption, a specification): that is theirs, and the warning stays.
        if (frg == 1.0 && pa2 > RealGasEarlyWarnBar * 1e5 && !c.HasRecordedBasis("f_rg"))
            c.CheckCondition("Real-gas factor against the ideal gas above 100 bar absolute", false, $"ideal gas (f_rg = 1) at p_2 = {F(pa2 / 1e5)} bar abs", "real-gas factor f_rg entered", "V0");

        if (pa2 > RealGasEarlyWarnBar * 1e5 && pa2 / pa1 < 1.3)
            c.Warn("realgas-window", $"p_2/p_1: the pressure window is narrow ({F(pa1 / 1e5)} to {F(pa2 / 1e5)} bar absolute, p_2/p_1 = {F(pa2 / pa1)}); the volume is the small difference of two large gas volumes, so the real-gas factor on V0 is larger than the compressibility factor Z at p_2: f_rg = Z_0 (1/p_1 − 1/p_2)/(Z_1/p_1 − Z_2/p_2), with Z at p_0, p_1 and p_2.");

        if (n > 1.0 && pa2 >= RealExponentWarnBar * 1e5)
            c.Warn("exponent-real", frg == 1.0
                ? $"n: the exponent {F(n)} is the ideal-gas value; real nitrogen at {F(pa1 / 1e5)} to {F(pa2 / 1e5)} bar absolute is stiffer (an effective isentropic exponent of about 1.5 to 2.1 between 60 and 350 bar), so an adiabatic discharge needs about 13 to 24 % more gas volume than shown. Enter a real-gas factor f_rg for the discharge path."
                : $"n: the exponent {F(n)} is the ideal-gas value; real nitrogen at {F(pa1 / 1e5)} to {F(pa2 / 1e5)} bar absolute is stiffer (an effective isentropic exponent of about 1.5 to 2.1 between 60 and 350 bar). Your real-gas factor f_rg = {F(frg)} is applied on top: it must be the factor for the discharge path, not for an isothermal one.");

        if (mode == ModeEmergency && !hasLoad)
            c.Warn("load-pressure", "p_load: no load pressure entered; the emergency stroke is not checked against the pressure its push load needs at the end of the stroke (p_1). Enter it (link it from the cylinder circuit's p_push).");

        var processText = mode == ModeShock
            ? "one exponent both ways (the shock and pulsation duty)"
            : slowCharge
                ? $"charged slowly to p_2 (isothermal, exponent 1), discharged at n = {F(n)}"
                : $"charged as fast as it is discharged (one exponent n = {F(n)} from the pre-charge to both working pressures)";
        c.Output("process", "Gas process the volume is sized for", new TextValue(processText));

        if (mode == ModeEmergency && !slowCharge)
            c.Warn("process", $"process: an emergency stroke is held at p_2, charged slowly; sizing it with one exponent both ways gives {F(singleExponent * 1e3)} L, short of the {F(GasLaw.ChargeVolumeSplit(dv.Si, pa0, pa1, pa2, GasLaw.Isothermal, n) * 1e3)} L (before the real-gas and temperature factors) of a slow charge with the discharge at n = {F(n)}. Keep the duty's default unless the accumulator is recharged as fast as it discharges.");

        if (mode == ModeShock && chosen == ProcessSlowCharge)
            c.Warn("process", "process: the shock and pulsation duty sizes with one exponent both ways; the slow-charge choice is not applied to it.");

        switch (mode)
        {
            case ModeShock:
                c.Assume("Shock and pulsation duty (screening): ΔV is the liquid volume to absorb while the pressure rises from p_1 to the permitted peak p_2 in a time too short for heat exchange; the dynamic response of the line (wave speed, valve closing time, pulsation frequency) is not modelled.", "Gas-law screening only.");
                c.Warn("shock", "mode: shock and pulsation sizing here is a gas-volume screening for the stated ΔV; it does not model the pressure wave. Confirm with the manufacturer's surge calculation.");

                if (n < FastExponent)
                    ExponentTooLow(c, n, $"n: a shock event is fast and adiabatic; n = {F(n)} gives a smaller volume than n near 1.4.");

                break;

            case ModeEmergency:
                c.Assume("Emergency stroke duty: ΔV is the volume the emergency stroke needs (swept volume plus line compression; linkable from the cylinder circuit); p_1 is the lowest pressure at which the stroke still has the force it needs; p_2 is the pressure the accumulator is held at, reached by a slow charge.", "Gas-law sizing for a stored-energy stroke.");

                if (n < FastExponent)
                    ExponentTooLow(c, n, $"n: an emergency stroke discharges quickly; n = {F(n)} is the slow, isothermal end. Use n near 1.4 unless the stroke takes minutes.");

                break;

            default:
                c.Assume(slowCharge
                    ? "Energy storage duty, held at p_2: ΔV is the liquid volume the accumulator delivers between p_2 and p_1; the gas is charged slowly (isothermal) to p_2 and discharged at n."
                    : "Energy storage duty: ΔV is the liquid volume the accumulator delivers between p_2 and p_1 over a cycle; n = 1 for a slow discharge, towards 1.4 for a fast one.", "Gas-law sizing for a stored-energy duty.");
                break;
        }
    }

    /// <summary>
    /// A shock or emergency discharge with an exponent below <see cref="FastExponent"/> sizes a smaller gas volume than the adiabatic one (it warned and the sheet passed). The exponent check fails,
    /// unless the person recorded a basis for n (an assumption, a specification, a measured stroke time): that is theirs, and the warning stays.
    /// </summary>
    private static void ExponentTooLow(CalcRunContext c, double n, string message)
    {
        if (c.HasRecordedBasis("n"))
        {
            c.Warn("exponent", message + " Used on your recorded basis for n.");
            return;
        }

        c.Warn("exponent", message + $" The exponent check fails until n is at least {F(FastExponent)}, or the basis of n is recorded on this input.");
        c.CheckCondition("Gas exponent against a fast discharge", false, $"n = {F(n)}", $"n at least {F(FastExponent)}", "V0");
    }

    private static double SizeSi(CalcRow row, Unit litre) =>
        row.Get("V") is { Number: { } v } cell && double.IsFinite(v) ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : litre).ToSi(v) : 0;

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
