using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Electrical;

/// <summary>
/// E7 — printed-circuit trace width from the legacy IPC-2221 current curve-fit <c>I = k·ΔT^0.44·A^0.725</c> (A in mil²):
/// the minimum cross-section and width for a current and a temperature rise on an external or internal layer, then the
/// resistance of the trace as drawn at its operating temperature, the voltage drop and the I²R loss. The curve-fit
/// constants (k = 0.048 external, 0.024 internal; exponents 0.44 and 0.725) are a method record, published closed-form
/// physics; the IPC-2152 charts and plane factors are not implemented and nothing from them ships.
/// </summary>
public sealed class PcbTraceCalculator : ICalcCalculator
{
    public const string Id = "elec.pcb-trace";

    public const string External = "External layer";
    public const string Internal = "Internal layer";

    /// <summary>The label every sheet carries (Addendum B §B6 E7).</summary>
    public const string SheetLabel = "legacy IPC-2221 curve-fit, conservative for multilayer boards";

    /// <summary>The widest trace (10 mm) the source curve-fit was drawn for; beyond it the sheet warns.</summary>
    public const double WidthWarnMm = 10.0;

    /// <summary>The copper weights (oz/ft²) the source curve-fit was drawn for; outside them the sheet warns.</summary>
    public const double FitMinOz = 1.0;

    /// <inheritdoc cref="FitMinOz"/>
    public const double FitMaxOz = 3.0;

    /// <summary>The smallest temperature rise (K) the source curve-fit was drawn for; below it the sheet warns.</summary>
    public const double FitMinRiseK = 10.0;

    public CalcTemplate Template { get; } = new(
        Id, "1.0.3", CalcDiscipline.Electrical, "PCB trace width (IPC-2221 legacy)",
        "The minimum width of a printed-circuit trace for a current and an allowed temperature rise, from the legacy IPC-2221 curve-fit, and the resistance, voltage drop and I²R loss of the trace as drawn: " + SheetLabel + ". External or internal layer; copper weight in oz/ft²; resistance at the trace's operating temperature (ambient plus rise). The IPC-2152 method (charts, plane and board-thickness factors) is not implemented.",
        "Trace cross-section: I = k·ΔT^0.44·A^0.725, so A = [I/(k·ΔT^0.44)]^(1/0.725) with A in mil², k = 0.048 external, 0.024 internal\n" +
        "Copper thickness: t = w_Cu·t_oz, t_oz = 1.378 mil per oz/ft² (35 µm, derived)\n" +
        "Minimum width: w_min = A/t; utilisation u_w = w_min/w_act\n" +
        "Resistivity at the operating temperature: ρ_T = ρ_20·[1 + α·(T_amb + ΔT − 20)]\n" +
        "Trace resistance: R = ρ_T·L/(w_act·t); voltage drop ΔV = I·R; loss P = I²·R",
        [
            new CalcInputSpec("I", "Trace current", CalcInputKind.Quantity, "A", Default: 3, Min: 0.01, Max: 35, HardMin: 1e-3, HardMax: 1000, Group: "Load"),
            new CalcInputSpec("dT", "Allowed temperature rise above ambient", CalcInputKind.Quantity, "K", Default: 10, Min: 1, Max: 100, HardMin: 0.1, HardMax: 300, Group: "Load"),
            new CalcInputSpec("T_amb", "Ambient temperature at the board", CalcInputKind.Quantity, "degC", Default: 25, Min: -40, Max: 125, HardMin: -100, HardMax: 500, Group: "Load"),
            new CalcInputSpec("layer", "Layer", CalcInputKind.Choice, Choices: [External, Internal], DefaultChoice: External, Group: "Trace"),
            new CalcInputSpec("Cu_oz", "Finished copper weight in oz/ft² (0.5 to 6), not a thickness in µm: 17.5 µm is ½ oz, 35 µm is 1 oz, 70 µm is 2 oz, 105 µm is 3 oz", CalcInputKind.Number, Default: 1, Min: 0.5, Max: 6, HardMin: 0.1, HardMax: 6, Group: "Trace"),
            new CalcInputSpec("w_act", "Trace width as drawn", CalcInputKind.Quantity, "mm", Default: 2, Min: 0.05, Max: 10, HardMin: 0.01, HardMax: 1000, Group: "Trace"),
            new CalcInputSpec("L", "Trace length", CalcInputKind.Quantity, "mm", Default: 50, Min: 1, Max: 1000, HardMin: 1e-3, HardMax: 1e5, Group: "Trace"),
            new CalcInputSpec("k_ext", "Curve-fit constant k, external layer", CalcInputKind.Number, Default: 0.048, Min: 0.01, Max: 0.1, HardMin: 0.001, HardMax: 1, Group: "Method (IPC-2221 legacy)"),
            new CalcInputSpec("k_int", "Curve-fit constant k, internal layer", CalcInputKind.Number, Default: 0.024, Min: 0.005, Max: 0.05, HardMin: 0.001, HardMax: 1, Group: "Method (IPC-2221 legacy)"),
            new CalcInputSpec("n_dT", "Exponent of the temperature rise", CalcInputKind.Number, Default: 0.44, Min: 0.3, Max: 0.6, HardMin: 0.1, HardMax: 1, Group: "Method (IPC-2221 legacy)"),
            new CalcInputSpec("n_A", "Exponent of the cross-section", CalcInputKind.Number, Default: 0.725, Min: 0.5, Max: 1, HardMin: 0.3, HardMax: 1.5, Group: "Method (IPC-2221 legacy)"),
            new CalcInputSpec("t_oz", "Copper thickness per oz/ft² (35 µm = 1.378 mil, derived)", CalcInputKind.Quantity, "mil", Default: 1.378, Min: 1.2, Max: 1.5, HardMin: 0.5, HardMax: 3, Group: "Method (IPC-2221 legacy)"),
            new CalcInputSpec("rho_20", "Copper resistivity at 20 °C (annealed copper, 1.724e-8 Ω·m)", CalcInputKind.Quantity, "ohm*mm^2/m", Default: 0.01724, Min: 0.0165, Max: 0.0185, HardMin: 0.01, HardMax: 0.05, Group: "Copper"),
            new CalcInputSpec("alpha", "Temperature coefficient of resistance of copper", CalcInputKind.Quantity, "1/K", Default: 0.00393, Min: 0.0035, Max: 0.0042, HardMin: 0.001, HardMax: 0.01, Group: "Copper"),
        ],
        [
            new CalcAssumption("The curve-fit is the legacy IPC-2221 relation, drawn for a trace on an outer or inner layer with no plane or convective help, 1 to 3 oz copper, up to 35 A, rises of 10 to 100 K and widths to 10 mm; it is conservative for multilayer boards and is a screening of width, not a thermal analysis of the board.", "IPC-2221 curve-fit (legacy); IPC-2152 is not implemented."),
            new CalcAssumption("Copper thickness is the nominal 1.378 mil (35 µm) per oz/ft² of finished copper; plating build-up on outer layers and etch-back are not modelled.", "Derived: 35 µm / 25.4 µm per mil."),
            new CalcAssumption("Resistance uses the trace as drawn at the temperature ambient + rise, from the resistivity at 20 °C and a linear temperature coefficient; vias, pads, bends and skin effect are ignored.", "Derived constants for annealed copper."),
            new CalcAssumption("Steady direct current (or RMS current at low frequency); the rise is the allowed rise in the worst case.", "Scope of the curve-fit."),
        ],
        [
            new CalcClauseRef("IPC-2221", "6", "Electrical properties: current-carrying capacity of printed-board conductors (legacy curve-fit)"),
            new CalcClauseRef("Hughes, Electrical and Electronic Technology", "Ch. on DC circuits", "Resistivity and temperature coefficient of resistance"),
        ],
        [
            new CalcApplicabilityRule("width", "The minimum width exceeds 10 mm, beyond the range of the source curve-fit.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("input.I", "The current exceeds 35 A (the usual-range notice on I), beyond the range of the source curve-fit.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("copper", "The copper weight is outside 1 to 3 oz/ft², the range the source curve-fit was drawn for.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("rise", "The allowed temperature rise is below 10 K, the smallest rise the source curve-fit was drawn for.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-3a-10k-1oz-external", "3 A, 10 K rise, 1 oz copper, external layer (analytic)",
                new CalcReferenceSource("Analytic (IPC-2221 legacy curve-fit, recomputed)", null, "A = [3/(0.048·10^0.44)]^(1/0.725) = 74.1616 mil²; t = 1.378 mil; w = 53.8183 mil = 1.366985 mm"),
                [CalcInputValue.Quantity("I", 3, "A"), CalcInputValue.Quantity("dT", 10, "K"), CalcInputValue.Choice("layer", External), CalcInputValue.Of("Cu_oz", 1), CalcInputValue.Quantity("w_act", 2, "mm")],
                [new CalcExpectedValue("A_req", 74.16164155, "mil^2", 1e-9), new CalcExpectedValue("w_min", 1.366985265, "mm", 1e-9), new CalcExpectedValue("u_w", 0.6834926326, "", 1e-9)]),
            new CalcTestCase("ka-3a-10k-1oz-internal", "3 A, 10 K rise, 1 oz copper, internal layer (analytic)",
                new CalcReferenceSource("Analytic (IPC-2221 legacy curve-fit, recomputed)", null, "k = 0.024: A = [3/(0.024·10^0.44)]^(1/0.725) = 192.9270 mil²; w = 140.0051 mil = 3.556130 mm"),
                [CalcInputValue.Quantity("I", 3, "A"), CalcInputValue.Quantity("dT", 10, "K"), CalcInputValue.Choice("layer", Internal), CalcInputValue.Of("Cu_oz", 1), CalcInputValue.Quantity("w_act", 4, "mm")],
                [new CalcExpectedValue("A_req", 192.927049, "mil^2", 1e-9), new CalcExpectedValue("w_min", 3.556129930, "mm", 1e-9), new CalcExpectedValue("u_w", 0.8890324826, "", 1e-9)]),
            new CalcTestCase("ka-5a-20k-2oz-resistance", "5 A, 20 K rise, 2 oz copper, external, 100 mm long at 2.5 mm; resistance, drop and loss (analytic)",
                new CalcReferenceSource("Analytic (IPC-2221 legacy curve-fit, copper resistivity)", null, "A = 98.5107 mil², t = 2.756 mil = 70.0024 µm, w_min = 0.907900 mm; T = 45 °C, ρ = 0.01724·(1 + 0.00393·25) = 0.01893383 Ω·mm²/m; R = ρ·0.1 m/(2.5 mm × 0.0700024 mm) = 10.81896 mΩ; ΔV = 54.0948 mV; P = 270.474 mW"),
                [CalcInputValue.Quantity("I", 5, "A"), CalcInputValue.Quantity("dT", 20, "K"), CalcInputValue.Quantity("T_amb", 25, "degC"), CalcInputValue.Choice("layer", External), CalcInputValue.Of("Cu_oz", 2), CalcInputValue.Quantity("w_act", 2.5, "mm"), CalcInputValue.Quantity("L", 100, "mm")],
                [new CalcExpectedValue("w_min", 0.9079000638, "mm", 1e-9), new CalcExpectedValue("trace_R", 10.818960492, "mohm", 1e-9), new CalcExpectedValue("vdrop", 54.09480246, "mV", 1e-9), new CalcExpectedValue("trace_Ploss", 270.4740123, "mW", 1e-9), new CalcExpectedValue("u_w", 0.3631600255, "", 1e-9)]),
            new CalcTestCase("ka-half-oz-internal-narrow", "1 A, 30 K rise, 0.5 oz copper, internal, 200 mm long at 0.5 mm: too narrow (analytic)",
                new CalcReferenceSource("Analytic (IPC-2221 legacy curve-fit, copper resistivity)", null, "A = 21.7636 mil², t = 0.689 mil, w_min = 0.802317 mm > 0.5 mm so u_w = 1.60463 (FAIL); T = 70 °C, ρ = 0.02062766 Ω·mm²/m; R = 471.473 mΩ; ΔV = 0.471473 V; P = 0.471473 W"),
                [CalcInputValue.Quantity("I", 1, "A"), CalcInputValue.Quantity("dT", 30, "K"), CalcInputValue.Quantity("T_amb", 40, "degC"), CalcInputValue.Choice("layer", Internal), CalcInputValue.Of("Cu_oz", 0.5), CalcInputValue.Quantity("w_act", 0.5, "mm"), CalcInputValue.Quantity("L", 200, "mm")],
                [new CalcExpectedValue("w_min", 0.8023171756, "mm", 1e-9), new CalcExpectedValue("u_w", 1.604634351, "", 1e-9), new CalcExpectedValue("trace_R", 471.4732066, "mohm", 1e-9), new CalcExpectedValue("trace_Ploss", 471.4732066, "mW", 1e-9)]),
            new CalcTestCase("ka-published-3a-10k-1oz", "3 A, 10 K, 1 oz: widely quoted widths of about 1.37 mm external and 3.56 mm internal",
                new CalcReferenceSource("IPC-2221 trace-width calculators and PCB design guides as quoted second-hand", null, "≈ 1.37 mm external and ≈ 3.56 mm internal for 3 A, 10 K rise, 1 oz copper; to be checked against the owner's copy of IPC-2221 and a trace-width calculator", Secondary: true),
                [CalcInputValue.Quantity("I", 3, "A"), CalcInputValue.Quantity("dT", 10, "K"), CalcInputValue.Choice("layer", External), CalcInputValue.Of("Cu_oz", 1), CalcInputValue.Quantity("w_act", 2, "mm")],
                [new CalcExpectedValue("w_min", 1.37, "mm", 0.005)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-published-3a-10k-1oz-internal", "3 A, 10 K, 1 oz, internal layer: about 3.56 mm",
                new CalcReferenceSource("IPC-2221 trace-width calculators and PCB design guides as quoted second-hand", null, "≈ 3.56 mm internal for 3 A, 10 K rise, 1 oz copper; to be checked against the owner's copy of IPC-2221 and a trace-width calculator", Secondary: true),
                [CalcInputValue.Quantity("I", 3, "A"), CalcInputValue.Quantity("dT", 10, "K"), CalcInputValue.Choice("layer", Internal), CalcInputValue.Of("Cu_oz", 1), CalcInputValue.Quantity("w_act", 4, "mm")],
                [new CalcExpectedValue("w_min", 3.56, "mm", 0.005)],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Electronics",
        OutputPorts:
        [
            new CalcOutputPort("trace_R", "ohm", "Resistance of the trace as drawn at its operating temperature"),
            new CalcOutputPort("trace_Ploss", "W", "I²R power loss in the trace"),
            new CalcOutputPort("vdrop", "V", "Voltage drop along the trace"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("pcb.method", "IPC-2221 legacy curve-fit constants", ["k_ext", "k_int", "n_dT", "n_A", "t_oz"], "The legacy IPC-2221 constants: k = 0.048 (external) and 0.024 (internal), exponents 0.44 (temperature rise) and 0.725 (cross-section), and 1.378 mil of copper per oz/ft². Published closed-form constants; cite the edition of IPC-2221 you take them from."),
            new CalcRecordKind("conductor.material", "Copper resistivity constants", ["rho_20", "alpha"], "Annealed copper at 20 °C: 1.724e-8 Ω·m (0.01724 Ω·mm²/m) and 0.00393 per K; derived physical constants, or your own values for a plated or alloyed trace."),
        ]);

    public void Run(CalcRunContext c)
    {
        foreach (var s in new[] { "I", "dT", "T_amb", "Cu_oz", "w_act", "L", "n_dT", "n_A", "t_oz", "rho_20", "alpha" })
            c.Input(s);

        var internalLayer = c.InputText("layer") == Internal;
        c.Input(internalLayer ? "k_int" : "k_ext");

        // The curve-fit takes the current in amperes, the rise in kelvin and the area in mil² as plain numbers.
        c.Assign("k", internalLayer ? "k_int" : "k_ext", null, internalLayer ? "Curve-fit constant k (internal layer)" : "Curve-fit constant k (external layer)", "IPC-2221 §6 (legacy)");
        c.Assign("I_n", "I / (1 A)", null, "Current as a number of amperes");
        c.Assign("dT_n", "dT / (1 K)", null, "Temperature rise as a number of kelvin");
        c.Assign("A_req", "(I_n / (k * dT_n^n_dT))^(1 / n_A) * (1 mil^2)", "mil^2", "Minimum conductor cross-section", "IPC-2221 §6 (legacy)");
        c.Assign("t_Cu", "Cu_oz * t_oz", "um", "Copper thickness");
        c.Assign("w_min", "A_req / t_Cu", "mm", "Minimum trace width");
        c.Assign("u_w", "w_min / w_act", null, "Width utilisation (minimum / as drawn)");

        c.Assign("T_trace", "T_amb + dT", "degC", "Trace operating temperature");
        c.Assign("rho_T", "rho_20 * (1 + alpha * (T_trace - 20 degC))", "ohm*mm^2/m", "Copper resistivity at the operating temperature");
        c.Assign("A_act", "w_act * t_Cu", "mm^2", "Cross-section of the trace as drawn");
        c.Assign("trace_R", "rho_T * L / A_act", "mohm", "Trace resistance at the operating temperature");
        c.Assign("vdrop", "I * trace_R", "mV", "Voltage drop along the trace");
        c.Assign("trace_Ploss", "I^2 * trace_R", "mW", "Power loss in the trace (I²R)");

        foreach (var (symbol, text) in new[] { ("A_req", "Minimum cross-section"), ("w_min", "Minimum trace width"), ("u_w", "Width utilisation"), ("trace_R", "Trace resistance"), ("vdrop", "Voltage drop"), ("trace_Ploss", "Power loss") })
            c.Output(symbol, text);

        c.CheckUtilisation("Trace width as drawn against the minimum for the rise", "u_w", "IPC-2221 §6 (legacy)");

        var oz = c.Si("Cu_oz");

        if (oz < FitMinOz || oz > FitMaxOz)
            c.Warn("copper", $"Cu_oz: {CalcValue.FormatNumber(oz)} oz/ft² is outside the 1 to 3 oz/ft² the source curve-fit was drawn for; the width is an extrapolation, check by test or a thermal analysis.");

        if (c.Si("dT") < FitMinRiseK)
            c.Warn("rise", $"dT: a rise of {CalcValue.FormatNumber(c.Si("dT"))} K is below the 10 K the source curve-fit was drawn for; the width is an extrapolation, check by test or a thermal analysis.");

        if (c.Si("w_min") > WidthWarnMm / 1000.0)
            c.Warn("width", $"w_min: the minimum width {CalcValue.FormatNumber(c.Si("w_min") * 1000, 4)} mm is above {CalcValue.FormatNumber(WidthWarnMm)} mm, beyond the range of the source curve-fit; use a plane, a pour or parallel layers and check by test.");
    }
}
