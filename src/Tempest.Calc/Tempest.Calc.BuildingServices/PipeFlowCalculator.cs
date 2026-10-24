using Tempest.Calc.BuildingServices.Fluids;
using Tempest.Calc.Engine.Numerics;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.BuildingServices;

/// <summary>
/// B1 — pipe pressure drop, system curve, pump duty point, affinity laws
/// and NPSH available. Darcy–Weisbach with Colebrook (iterative, capped)
/// and the Swamee–Jain cross-check; laminar and transition flagged;
/// fittings by K and by equivalent length; elevation; series segments;
/// pump curve as user points; duty point by bisection; speed and impeller
/// scaling by the affinity laws.
/// </summary>
public sealed class PipeFlowCalculator : ICalcCalculator
{
    public const string Id = "bs.pipe-pump";

    public const string FluidWater = "water";
    public const string FluidCustom = "custom (ρ and μ given)";
    public const string OpenCircuit = "open (the pump lifts the water by the rises entered)";
    public const string ClosedCircuit = "closed (flow and return: the rises cancel, no static lift)";

    private const int MaxDetailedSegments = 50;
    private const int CurvePoints = 31;

    private static readonly IReadOnlyList<CalcColumnSpec> SegmentColumns =
    [
        new("name", "Segment", CalcInputKind.Text, IsOptional: true),
        new("L", "Length", CalcInputKind.Quantity, "m", HardMin: 0, HardMax: 1e6),
        new("D", "Inner diameter", CalcInputKind.Quantity, "mm", HardMin: 0.1, HardMax: 1e4),
        new("eps", "Roughness", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 100),
        new("K", "Σ K of fittings", CalcInputKind.Number, HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("L_eq", "Equivalent length of fittings", CalcInputKind.Quantity, "m", HardMin: 0, HardMax: 1e6, IsOptional: true),
        new("dz", "Rise in elevation (+ up)", CalcInputKind.Quantity, "m", HardMin: -1e4, HardMax: 1e4, IsOptional: true),
        new("side", "Pump side", CalcInputKind.Choice, Choices: ["discharge", "suction"], IsOptional: true),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.2.0", CalcDiscipline.BuildingServices, "Pipe pressure drop and pump duty",
        "Friction and fitting losses through series pipe segments at a given flow, the static head, the system curve, the duty point against a pump curve entered as points, the affinity-law shift for a speed or impeller change, and the NPSH available at the pump suction.",
        "Q = Φ/(ρ c_p ΔT) when a heat load is given; v = Q/(πD²/4); Re = ρvD/μ; f = 64/Re (Re < 2300) else Colebrook–White 1/√f = −2 log10(ε/3.7D + 2.51/(Re√f)) by fixed-point iteration (≤ 100 steps) with the Swamee–Jain explicit value as start and cross-check; h_f = f (L + L_eq)/D · v²/2g + K v²/2g; H_sys(Q) = Σdz + h_out + Σh_f(Q) with h_f ∝ Q² scaled from the design flow (closed circuit: Σdz = 0, the rise in the flow is recovered in the return); the water held in the pipes V_pipe = Σ π/4·D²·L; duty point where H_pump(Q) = H_sys(Q) (bisection); affinity: Q ∝ n D³, H ∝ n² D²; NPSH_a = p_atm/ρg + z_s − h_f,suction − p_v/ρg.",
        [
            new CalcInputSpec("fluid", "Fluid", CalcInputKind.Choice, Choices: [FluidWater, FluidCustom], DefaultChoice: FluidWater, Group: "Fluid"),
            new CalcInputSpec("T", "Fluid temperature (mean; link B6 t_m for a heating loop)", CalcInputKind.Quantity, "degC", Default: 20, Min: 0, Max: 100, HardMin: -50, HardMax: 300, Group: "Fluid"),
            new CalcInputSpec("rho_custom", "Density (custom fluid)", CalcInputKind.Quantity, "kg/m^3", Min: 500, Max: 2000, HardMin: 1, HardMax: 1e5, IsOptional: true, Group: "Fluid"),
            new CalcInputSpec("mu_custom", "Dynamic viscosity (custom fluid)", CalcInputKind.Quantity, "mPa*s", Min: 0.1, Max: 1000, HardMin: 1e-4, HardMax: 1e6, IsOptional: true, Group: "Fluid"),
            new CalcInputSpec("Q", "Design flow rate (blank when a heat load gives it)", CalcInputKind.Quantity, "m^3/h", Default: 20, Min: 0.01, Max: 1e5, HardMin: 1e-9, HardMax: 1e7, IsOptional: true, Group: "System"),
            new CalcInputSpec("Phi_load", "Heat carried by the water (gives the design flow with ΔT; replaces Q)", CalcInputKind.Quantity, "W", Min: 10, Max: 1e8, HardMin: 1e-3, HardMax: 1e10, IsOptional: true, Group: "System"),
            new CalcInputSpec("dT_load", "Flow-to-return temperature difference across the load", CalcInputKind.Quantity, "K", Min: 1, Max: 100, HardMin: 0.01, HardMax: 500, IsOptional: true, Group: "System"),
            new CalcInputSpec("cp_load", "Specific heat of the fluid (heat-load flow; blank = 4186 J/(kg·K) assumed for water, required for a custom fluid)", CalcInputKind.Quantity, "J/(kg*K)", Min: 1000, Max: 6000, HardMin: 100, HardMax: 1e5, IsOptional: true, Group: "System"),
            new CalcInputSpec("segments", "Pipe segments in series", CalcInputKind.Table, Columns: SegmentColumns, MaxRows: 500, Group: "System"),
            new CalcInputSpec("circuit", "Circuit (a closed flow-and-return loop has no static lift: the rises entered are ignored)", CalcInputKind.Choice, Choices: [OpenCircuit, ClosedCircuit], DefaultChoice: OpenCircuit, Group: "System"),
            new CalcInputSpec("h_out", "Pressure head required at the outlet", CalcInputKind.Quantity, "m", Default: 0, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e5, Group: "System"),
            new CalcInputSpec("v_max", "Velocity limit (warning)", CalcInputKind.Quantity, "m/s", Default: 3, Min: 0.5, Max: 10, HardMin: 0.01, HardMax: 100, Group: "System"),
            new CalcInputSpec("Q_pump", "Pump curve: flow points", CalcInputKind.Vector, "m^3/h", IsOptional: true, Group: "Pump"),
            new CalcInputSpec("H_pump", "Pump curve: head points", CalcInputKind.Vector, "m", IsOptional: true, Group: "Pump"),
            new CalcInputSpec("n_ratio", "Speed ratio n₂/n₁ (affinity)", CalcInputKind.Number, Default: 1, Min: 0.2, Max: 2, HardMin: 0.01, HardMax: 10, Group: "Pump"),
            new CalcInputSpec("D_ratio", "Impeller diameter ratio D₂/D₁ (affinity)", CalcInputKind.Number, Default: 1, Min: 0.7, Max: 1.1, HardMin: 0.1, HardMax: 5, Group: "Pump"),
            new CalcInputSpec("eta_pump", "Pump efficiency at duty (for shaft power)", CalcInputKind.Number, Default: 0.7, Min: 0.2, Max: 0.95, HardMin: 0.01, HardMax: 1, Group: "Pump"),
            new CalcInputSpec("n_pump", "Pump rated speed (speed of the pump curve as entered; gives the shaft torque)", CalcInputKind.Quantity, "rpm", Min: 100, Max: 6000, HardMin: 1, HardMax: 1e5, IsOptional: true, Group: "Pump"),
            new CalcInputSpec("p_atm", "Atmospheric pressure (NPSH)", CalcInputKind.Quantity, "kPa", Default: 101.325, Min: 50, Max: 110, HardMin: 1, HardMax: 1000, Group: "NPSH"),
            new CalcInputSpec("z_s", "Static head at the pump suction (+ flooded, − lift)", CalcInputKind.Quantity, "m", Default: 0, Min: -10, Max: 100, HardMin: -1e3, HardMax: 1e4, Group: "NPSH"),
            new CalcInputSpec("NPSH_r", "NPSH required by the pump (optional)", CalcInputKind.Quantity, "m", Min: 0, Max: 50, HardMin: 0, HardMax: 1e3, IsOptional: true, Group: "NPSH"),
        ],
        [
            new CalcAssumption("Steady, incompressible, fully developed flow; all segments carry the full design flow in series.", "Darcy–Weisbach."),
            new CalcAssumption("Fitting losses scale with velocity head (K method) or add pipe length (equivalent-length method); both may be used on one segment.", "Crane TP-410 practice."),
            new CalcAssumption("The system curve scales friction losses with Q² from the design flow (turbulent assumption); the static part is independent of flow.", "H_sys = H_static + k Q²."),
            new CalcAssumption("Water properties are from published correlations at atmospheric pressure (0–100 °C); any other fluid is entered with its own density and viscosity and their provenance.", "Kell 1975; Vogel fit; Hyland & Wexler 1983."),
        ],
        [
            new CalcClauseRef("Crane Technical Paper 410", "—", "Flow of fluids through valves, fittings and pipe"),
            new CalcClauseRef("Çengel & Cimbala, Fluid Mechanics", "Ch. 8", "Internal flow: friction factor, minor losses, pump duty"),
            new CalcClauseRef("CIBSE Guide C", "Section 4", "Flow of fluids in pipes and ducts (principles)"),
            new CalcClauseRef("Kell, J. Chem. Eng. Data 20(1), 1975", "—", "Density of liquid water"),
        ],
        [
            new CalcApplicabilityRule("heat-flow", "A heat load needs its temperature difference (and the reverse); without a heat load a design flow is needed.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("heat-t", "A heat load is given without the fluid temperature: 20 °C is taken.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("cp_load", "A custom fluid needs its specific heat for the heat-load flow.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("heat-flow-q", "A design flow is entered as well as a heat load: the flow from the heat load is used and Q ignored.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("custom-fluid", "A custom fluid needs its density and viscosity.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("water-range", "Water correlations hold for 0–100 °C.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("segments", "At least one segment is needed.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("laminar", "Laminar flow in a segment (Re < 2300): f = 64/Re.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("transition", "Transitional flow (2300 ≤ Re < 4000): the friction factor is uncertain; Colebrook is used.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("velocity", "A segment exceeds the velocity limit.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("pump-curve", "The pump curve needs at least two points with matching flow and head counts, flows increasing.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("no-duty", "The pump curve and the system curve do not cross within the curve's flow range.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("summary-only", "More than 50 segments: per-segment lines are omitted.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-laminar-oil", "Laminar oil flow (analytic Hagen–Poiseuille)",
                new CalcReferenceSource("Çengel & Cimbala, Fluid Mechanics", "4th ed.", "Ch. 8, laminar flow in a pipe: Δp = 32 μ L v / D² (Hagen–Poiseuille); ρ = 888 kg/m³, μ = 0.8 Pa·s, D = 50 mm, L = 40 m, Q = 1 m³/h: v = 0.14147 m/s, Re = 7.852, f = 8.151, h_f = f L/D v²/2g = 6.654 m, Δp = 32 μ L v / D² = 57.95 kPa"),
                [CalcInputValue.Choice("fluid", FluidCustom), CalcInputValue.Quantity("T", 20, "degC"), CalcInputValue.Quantity("rho_custom", 888, "kg/m^3"), CalcInputValue.Quantity("mu_custom", 800, "mPa*s"), CalcInputValue.Quantity("Q", 1, "m^3/h"), Segments(("oil line", 40, 50, 0.0, 0, 0, 0, "discharge")), CalcInputValue.Quantity("h_out", 0, "m")],
                [new CalcExpectedValue("v_s1", 0.14147, "m/s", 1e-4), new CalcExpectedValue("Re_s1", 7.852, "", 1e-3), new CalcExpectedValue("f_s1", 8.151, "", 1e-3), new CalcExpectedValue("h_f_total", 6.654, "m", 1e-3), new CalcExpectedValue("dp_total", 57.946, "kPa", 1e-3)]),
            new CalcTestCase("ka-turbulent-water", "Turbulent water, Colebrook against an independent solution",
                new CalcReferenceSource("Colebrook–White equation solved independently (Re = 1.0e5, ε/D = 0.001: f = 0.022175; Swamee–Jain 0.022342)", null, "Custom fluid ρ = 1000, μ = 1 mPa·s so Re is exact: D = 100 mm, v = 1 m/s ⇒ Q = 28.274 m³/h, Re = 1e5; ε = 0.1 mm; L = 100 m: h_f = f L/D v²/2g = 0.022175·1000·0.050985 = 1.1306 m"),
                [CalcInputValue.Choice("fluid", FluidCustom), CalcInputValue.Quantity("T", 20, "degC"), CalcInputValue.Quantity("rho_custom", 1000, "kg/m^3"), CalcInputValue.Quantity("mu_custom", 1, "mPa*s"), CalcInputValue.Quantity("Q", 28.274334, "m^3/h"), Segments(("main", 100, 100, 0.1, 0, 0, 0, "discharge")), CalcInputValue.Quantity("h_out", 0, "m")],
                [new CalcExpectedValue("v_s1", 1.0, "m/s", 1e-6), new CalcExpectedValue("Re_s1", 1e5, "", 1e-6), new CalcExpectedValue("f_s1", 0.022175, "", 5e-4), new CalcExpectedValue("h_f_total", 1.1306, "m", 1e-3), new CalcExpectedValue("f_sj_s1", 0.022342, "", 1e-3)]),
            new CalcTestCase("ka-duty-point", "Duty point against a parabolic pump curve, with K fittings and a rise (analytic)",
                new CalcReferenceSource("Analytic: pump H = 40 − 0.01 Q² (Q in m³/h), system H = 10 + k Q² ⇒ duty where 30 = (k + 0.01) Q²", null, "Water 20 °C, DN80 (D = 80 mm), L = 60 m, ε = 0.045 mm, ΣK = 8, rise 10 m: v = 1.658 m/s, Re ≈ 1.32e5, f ≈ 0.0198, h_f ≈ 3.2 m at 30 m³/h so k ≈ 0.00356 m/(m³/h)²; duty where 30 = (k + 0.01) Q² ⇒ Q ≈ 46.3 m³/h, H ≈ 17.7 m on the piecewise-linear curve through (0, 40), (20, 36), (40, 24), (60, 4); at 90 % speed Q ≈ 40.1 m³/h"),
                [CalcInputValue.Choice("fluid", FluidWater), CalcInputValue.Quantity("T", 20, "degC"), CalcInputValue.Quantity("Q", 30, "m^3/h"), Segments(("riser", 60, 80, 0.045, 8, 0, 10, "discharge")), CalcInputValue.Quantity("h_out", 0, "m"), CalcInputValue.VectorOf("Q_pump", [0, 20, 40, 60], "m^3/h"), CalcInputValue.VectorOf("H_pump", [40, 36, 24, 4], "m"), CalcInputValue.Of("n_ratio", 0.9), CalcInputValue.Of("D_ratio", 1)],
                [new CalcExpectedValue("H_static", 10, "m", 1e-9), new CalcExpectedValue("H_duty", 17.67, "m", 0.01), new CalcExpectedValue("Q_duty", 46.3, "m^3/h", 0.01), new CalcExpectedValue("Q_duty2", 40.1, "m^3/h", 0.01)]),
            new CalcTestCase("ka-closed-circuit", "The duty-point loop as a closed flow-and-return circuit: no static lift, and the water it holds",
                new CalcReferenceSource("Analytic (closed circuit: Σdz is not a lift; V_pipe = π/4·D²·L)", null, "DN80 (D = 80 mm), L = 60 m, rise 10 m entered but ignored in a closed circuit: H_static = h_out = 0; V_pipe = π/4 × 0.08² × 60 = 0.301593 m³ = 301.593 L"),
                [CalcInputValue.Choice("fluid", FluidWater), CalcInputValue.Quantity("T", 20, "degC"), CalcInputValue.Quantity("Q", 30, "m^3/h"), Segments(("loop", 60, 80, 0.045, 8, 0, 10, "discharge")), CalcInputValue.Choice("circuit", ClosedCircuit), CalcInputValue.Quantity("h_out", 0, "m")],
                [new CalcExpectedValue("H_static", 0, "m", 1e-9), new CalcExpectedValue("V_pipe", 301.5929, "L", 1e-6)]),
            new CalcTestCase("ka-heat-load-flow", "Design flow from a heat load: 12 kW at a 20 K difference, water at 70 °C (analytic)",
                new CalcReferenceSource("Analytic: Q = Φ / (ρ c_p ΔT)", null, "ρ(70 °C) = 977.7696 kg/m³ (Kell 1975, evaluated from the correlation); c_p = 4186 J/(kg·K) as entered; Q = 12 000 / (977.7696 × 4186 × 20) = 1.465938e-4 m³/s = 0.5277375 m³/h"),
                [CalcInputValue.Choice("fluid", FluidWater), CalcInputValue.Quantity("T", 70, "degC"), CalcInputValue.Quantity("Phi_load", 12, "kW"), CalcInputValue.Quantity("dT_load", 20, "K"), CalcInputValue.Quantity("cp_load", 4186, "J/(kg*K)"), Segments(("loop", 40, 28, 0.045, 0, 0, 0, "discharge")), CalcInputValue.Quantity("h_out", 0, "m")],
                [new CalcExpectedValue("Q_design", 0.5277375, "m^3/h", 1e-6)]),
        ],
        DiagramKey: "pipe-pump",
        Category: "Hydraulics",
        OutputPorts:
        [
            new CalcOutputPort("Q_design", "m^3/h", "Design flow (as entered, or from the heat load)"),
            new CalcOutputPort("H_static", "m", "Static head"),
            new CalcOutputPort("V_pipe", "L", "Water held in the pipe segments (→ B6 V_pipe, where the chain order allows)"),
            new CalcOutputPort("H_sys", "m", "System head at the design flow"),
            new CalcOutputPort("h_f_total", "m", "Total friction and fitting loss at the design flow"),
            new CalcOutputPort("dp_total", "kPa", "Friction pressure drop at the design flow"),
            new CalcOutputPort("dp_sys", "kPa", "System pressure at the design flow"),
            new CalcOutputPort("P_hyd", "kW", "Hydraulic power at the design point"),
            new CalcOutputPort("P_shaft_design", "kW", "Pump shaft power at the design flow (needs the pump speed)"),
            new CalcOutputPort("T_shaft", "N*m", "Pump shaft torque, for the larger of the design-flow and duty-point shaft power (→ E1 load torque)"),
            new CalcOutputPort("n_shaft", "rpm", "Pump shaft speed (→ E1 load speed)"),
            new CalcOutputPort("v_peak", "m/s", "Highest velocity"),
            new CalcOutputPort("NPSH_a", "m", "NPSH available (water)"),
            new CalcOutputPort("Q_duty", "m^3/h", "Duty flow against the pump curve"),
            new CalcOutputPort("H_duty", "m", "Duty head against the pump curve"),
            new CalcOutputPort("P_shaft", "kW", "Shaft power at the duty point"),
        ]);

    /// <summary>Segments: (name, L m, D mm, ε mm, ΣK, L_eq m, dz m, side).</summary>
    public static CalcInputValue Segments(params (string Name, double L, double D, double Eps, double K, double Leq, double Dz, string Side)[] rows) =>
        CalcInputValue.Table("segments", rows.Select(r => CalcRow.Of(("name", new CalcCell(Text: r.Name)), ("L", new CalcCell(r.L, "m")), ("D", new CalcCell(r.D, "mm")), ("eps", new CalcCell(r.Eps, "mm")), ("K", new CalcCell(r.K)), ("L_eq", new CalcCell(r.Leq, "m")), ("dz", new CalcCell(r.Dz, "m")), ("side", new CalcCell(Text: r.Side)))).ToList());

    private sealed record Segment(int Index, string Name, double L, double D, double Eps, double K, double Leq, double Dz, bool Suction);

    public void Run(CalcRunContext c)
    {
        const double g = 9.80665;
        var fluid = c.InputText("fluid");
        var tEntered = c.Has("T");

        var t = c.InputSi("T") - 273.15;
        double rho, mu;

        // A temperature left out takes the template default with the validator's own notice; with a heat load that default is
        // wrong for a heating loop, so say so specifically.
        if (!tEntered && c.Has("Phi_load"))
            c.Warn("heat-t", "T: the fluid temperature was not entered with the heat load; 20 °C is taken, which is not a heating loop's temperature. Enter T (or link B6's t_m).");

        if (fluid == FluidCustom)
        {
            if (!c.Has("rho_custom") || !c.Has("mu_custom"))
            {
                c.Block("custom-fluid", "rho_custom/mu_custom: a custom fluid needs its density and viscosity.");
                return;
            }

            c.Input("rho_custom");
            c.Input("mu_custom");
            c.Assign("rho", "rho_custom", "kg/m^3", "Density (as entered)");
            c.Assign("mu", "mu_custom", "mPa*s", "Dynamic viscosity (as entered)");
        }
        else
        {
            if (t < Water.MinCelsius || t > Water.MaxCelsius)
            {
                c.Block("water-range", "T: the water correlations hold for 0 to 100 °C.");
                return;
            }

            c.Record("rho", "Kell (1975) density of water at T", $"T = {F(t)} °C", ScalarValue.Of(Water.Density(t), "kg/m^3"), "Density", "Kell 1975");
            c.Record("mu", "Vogel fit μ = 0.02939 exp(507.88/(T_K − 149.3)) mPa·s", $"T_K = {F(t + 273.15)} K", ScalarValue.Of(Water.Viscosity(t) * 1e3, "mPa*s"), "Dynamic viscosity");
            c.Record("p_v", "Hyland–Wexler saturation pressure", $"T = {F(t)} °C", ScalarValue.Of(Water.VapourPressure(t) / 1e3, "kPa"), "Vapour pressure (NPSH)");
        }

        rho = c.Si("rho");
        mu = c.Si("mu");
        c.Input("h_out");
        c.Input("v_max");

        if (c.Has("Phi_load") || c.Has("dT_load"))
        {
            if (!c.Has("Phi_load") || !c.Has("dT_load"))
            {
                c.Block("heat-flow", "Phi_load: a heat load gives the flow only with the flow-to-return temperature difference dT_load, and the other way round.");
                return;
            }

            c.Input("Phi_load");
            c.Input("dT_load");

            if (c.Has("cp_load"))
            {
                c.Input("cp_load");
                c.Assign("c_p", "cp_load", "J/(kg*K)", "Specific heat (as entered)");
            }
            else if (fluid == FluidCustom)
            {
                c.Block("cp_load", "cp_load: a custom fluid needs its specific heat for the heat-load flow.");
                return;
            }
            else
            {
                c.Record("c_p", "c_p of water 4186 J/(kg·K) (assumed)", "rounded constant for 10–90 °C", ScalarValue.Of(4186, "J/(kg*K)"), "Specific heat of water (assumed; enter cp_load to change it)");
                c.Assume("The specific heat of water is taken as 4186 J/(kg·K), a rounded constant for 10–90 °C.", "Assumed constant; enter cp_load to use another value.");
            }

            if (c.Has("Q"))
                c.Warn("heat-flow-q", "Q: ignored, the design flow comes from the heat load; clear Q to remove this notice.");

            c.Assign("Q_design", "Phi_load / (rho * c_p * dT_load)", "m^3/h", "Design flow from the heat carried: Φ / (ρ c_p ΔT)");
        }
        else
        {
            if (!c.Has("Q"))
            {
                c.Block("heat-flow", "Q: give the design flow, or a heat load with its temperature difference.");
                return;
            }

            c.Input("Q");
            c.Assign("Q_design", "Q", "m^3/h", "Design flow (as entered)");
        }

        var q = c.Si("Q_design");
        var vMax = c.Si("v_max");

        var rows = c.Rows("segments");

        if (rows.Count == 0)
        {
            c.Block("segments", "segments: at least one pipe segment is needed.");
            return;
        }

        var segments = rows.Select((r, i) => new Segment(i + 1, r.Get("name")?.Text is { Length: > 0 } n ? n : $"segment {i + 1}",
            Cell(r, "L", "m"), Cell(r, "D", "mm"), Cell(r, "eps", "mm"), r.Get("K")?.Number ?? 0, Cell(r, "L_eq", "m"), Cell(r, "dz", "m"),
            string.Equals(r.Get("side")?.Text, "suction", StringComparison.OrdinalIgnoreCase))).ToList();

        if (segments.Any(s => s.D <= 0 || s.L < 0))
        {
            c.Block("segments", "segments: every segment needs a positive diameter and a non-negative length.");
            return;
        }

        var detailed = segments.Count <= MaxDetailedSegments;

        if (!detailed)
            c.Warn("summary-only", "segments: more than 50 segments; per-segment lines are omitted.");

        double hfTotal = 0, hfSuction = 0, dzTotal = 0;
        var mps = UnitCatalog.Parse("m/s");
        var m = UnitCatalog.Parse("m");
        var kPa = UnitCatalog.Parse("kPa");

        foreach (var s in segments)
        {
            var area = Math.PI * s.D * s.D / 4;
            var v = q / area;
            var re = rho * v * s.D / mu;
            var relRough = s.Eps / s.D;
            double f;
            string fText;
            double fSj = double.NaN;

            if (re < Friction.LaminarLimit)
            {
                f = Friction.Laminar(re);
                fText = "64 / Re (laminar)";
                c.Warn("laminar", $"segments: {s.Name} is laminar (Re = {F(re)}); f = 64/Re.");
            }
            else
            {
                var (fc, iterations) = Friction.Colebrook(re, relRough);
                f = fc;
                fSj = Friction.SwameeJain(re, relRough);
                fText = $"Colebrook–White, {iterations} iterations (Swamee–Jain start {F(fSj)})";

                if (re < Friction.TurbulentLimit)
                    c.Warn("transition", $"segments: {s.Name} is transitional (Re = {F(re)}); the friction factor is uncertain.");
            }

            var hf = f * (s.L + s.Leq) / s.D * v * v / (2 * g) + s.K * v * v / (2 * g);
            hfTotal += hf;
            dzTotal += s.Dz;

            if (s.Suction)
                hfSuction += hf;

            if (v > vMax)
                c.Warn("velocity", $"segments: {s.Name} runs at {F(v)} m/s, above the {F(vMax)} m/s limit.");

            if (detailed)
            {
                var k = $"s{s.Index}";
                c.Record($"v_{k}", $"{s.Name}: Q / (π D²/4)", $"{F(q * 3600)} m³/h / (π × {F(s.D * 1e3)}² mm² / 4)", ScalarValue.Of(v, mps), $"Velocity, {s.Name}");
                c.Record($"Re_{k}", $"{s.Name}: ρ v D / μ", $"{F(rho)} × {F(v)} × {F(s.D)} / {F(mu)}", new ScalarValue(re), $"Reynolds number, {s.Name}");
                c.Record($"f_{k}", $"{s.Name}: Darcy friction factor", fText, new ScalarValue(f), $"Friction factor, {s.Name}");

                if (!double.IsNaN(fSj))
                    c.Record($"f_sj_{k}", $"{s.Name}: Swamee–Jain check", "0.25 / log10(ε/3.7D + 5.74/Re^0.9)²", new ScalarValue(fSj), $"Explicit cross-check, {s.Name}");

                c.Record($"h_f_{k}", $"{s.Name}: f (L + L_eq)/D · v²/2g + K v²/2g", $"{F(f)} × ({F(s.L)} + {F(s.Leq)}) / {F(s.D)} × {F(v * v / (2 * g))} + {F(s.K)} × {F(v * v / (2 * g))}", ScalarValue.Of(hf, m), $"Head loss, {s.Name}");
                c.Output($"v_{k}", $"Velocity, {s.Name}");
                c.Output($"Re_{k}", $"Re, {s.Name}");
                c.Output($"f_{k}", $"f, {s.Name}");
                if (!double.IsNaN(fSj)) c.Output($"f_sj_{k}", $"f (Swamee–Jain), {s.Name}");
                c.Output($"h_f_{k}", $"h_f, {s.Name}");
            }
        }

        c.Record("h_f_total", "Σ h_f", $"{segments.Count} segment(s)", ScalarValue.Of(hfTotal, m), "Total friction and fitting loss at the design flow");
        var closed = c.InputText("circuit") == ClosedCircuit;
        var lift = closed ? 0 : dzTotal;

        if (closed && Math.Abs(dzTotal) > 1e-9)
            c.Assume($"The circuit is closed (flow and return), so the rises entered ({F(dzTotal)} m in all) are not a static lift: the water that rises in the flow falls in the return.", "Closed circuit: the pump works against friction and the outlet head only.");

        var vPipe = segments.Sum(s => Math.PI * s.D * s.D / 4 * s.L);
        c.Record("V_pipe", "Σ π/4·D²·L", $"{segments.Count} segment(s)", ScalarValue.Of(vPipe * 1e3, "L"), "Water held in the pipe segments");
        c.Record("H_static", closed ? "h_out (closed circuit: no static lift)" : "Σ dz + h_out", closed ? $"{F(c.Si("h_out"))}" : $"{F(dzTotal)} + {F(c.Si("h_out"))}", ScalarValue.Of(lift + c.Si("h_out"), m), "Static head");
        c.Assign("H_sys", "H_static + h_f_total", "m", "System head at the design flow");
        c.Assign("dp_total", "rho * 9.80665 m/s^2 * h_f_total", "kPa", "Friction pressure drop at the design flow");
        c.Assign("dp_sys", "rho * 9.80665 m/s^2 * H_sys", "kPa", "System pressure at the design flow");
        c.Assign("P_hyd", "rho * 9.80665 m/s^2 * Q_design * H_sys", "kW", "Hydraulic power at the design point");
        c.Output("h_f_total", "Σ h_f");
        c.Output("H_static", "Static head");
        c.Output("V_pipe", "Water held in the pipes");
        c.Output("H_sys", "System head at Q");
        c.Output("dp_total", "Friction Δp");
        c.Output("dp_sys", "System Δp");
        c.Output("Q_design", "Design flow");
        c.Output("P_hyd", "Hydraulic power");

        // System curve.
        var hStatic = lift + c.Si("h_out");
        var kSys = hfTotal / (q * q);
        double HSys(double flow) => hStatic + kSys * flow * flow;
        var qMaxCurve = 1.5 * q;

        // Pump curve and duty point.
        var hasPump = c.Has("Q_pump") && c.Has("H_pump");
        var etaRegistered = false;
        double? dutyShaft = null;
        IReadOnlyList<double> qp = [];
        IReadOnlyList<double> hp = [];

        if (hasPump)
        {
            var qv = (VectorValue)c.Input("Q_pump");
            var hv = (VectorValue)c.Input("H_pump");
            qp = qv.Si;
            hp = hv.Si;

            if (qp.Count < 2 || qp.Count != hp.Count || qp.Zip(qp.Skip(1), (a, b) => b <= a).Any(x => x))
            {
                c.Block("pump-curve", "Q_pump/H_pump: give at least two points with the same count and increasing flows.");
                return;
            }

            qMaxCurve = Math.Max(qMaxCurve, qp[^1]);
            c.Input("n_ratio");
            c.Input("D_ratio");
            c.Input("eta_pump");
            etaRegistered = true;
            var nr = c.Si("n_ratio");
            var dr = c.Si("D_ratio");

            dutyShaft = Duty(c, "", "as entered", qp, hp, HSys, rho, g, m, kPa);

            if (Math.Abs(nr - 1) > 1e-12 || Math.Abs(dr - 1) > 1e-12)
            {
                var qs = qp.Select(x => x * nr * dr * dr * dr).ToList();
                var hs = hp.Select(x => x * nr * nr * dr * dr).ToList();
                c.Record("affinity", "Q₂ = Q₁ (n₂/n₁)(D₂/D₁)³, H₂ = H₁ (n₂/n₁)²(D₂/D₁)²", $"n₂/n₁ = {F(nr)}, D₂/D₁ = {F(dr)}", new TextValue("curve scaled"), "Affinity laws applied to the pump curve");
                Duty(c, "2", "after the affinity shift", qs, hs, HSys, rho, g, m, kPa);
                c.Series(new CalcSeries("Pump curve (scaled)", "Q", "m^3/h", "H", "m", qs.Zip(hs, (a, b) => (a * 3600, b)).ToList()));
            }

            c.Series(new CalcSeries("Pump curve", "Q", "m^3/h", "H", "m", qp.Zip(hp, (a, b) => (a * 3600, b)).ToList()));
        }

        // The pump's shaft power and torque, for the motor that drives it (a rated speed is needed). The torque covers the larger of
        // the design-flow shaft power and, when a pump curve is given, the duty-point shaft power of the curve as entered.
        if (c.Has("n_pump"))
        {
            c.Input("n_pump");

            if (!etaRegistered)
                c.Input("eta_pump");

            c.Assign("P_shaft_design", "P_hyd / eta_pump", "kW", "Pump shaft power at the design flow");

            if (dutyShaft is not null)
            {
                c.Assign("P_shaft_sel", "max(P_shaft_design, P_shaft)", "kW", "Shaft power the torque covers: the larger of the design-flow value and the duty-point value");
                c.Assign("T_shaft", "P_shaft_sel / n_pump", "N*m", "Pump shaft torque P / ω, P being the larger of the design-flow and duty-point shaft power");
                c.Output("P_shaft_sel", "Shaft power the torque covers (larger of design flow and duty point)");
            }
            else
            {
                c.Assign("T_shaft", "P_shaft_design / n_pump", "N*m", "Pump shaft torque P / ω at the design flow");
            }

            c.Assign("n_shaft", "n_pump", "rpm", "Pump shaft speed");
            c.Output("P_shaft_design", "Shaft power at the design flow");
            c.Output("T_shaft", "Shaft torque");
            c.Output("n_shaft", "Shaft speed");
        }

        c.Series(new CalcSeries("System curve", "Q", "m^3/h", "H", "m", Enumerable.Range(0, CurvePoints).Select(i => { var fq = qMaxCurve * i / (CurvePoints - 1); return (fq * 3600, HSys(fq)); }).ToList()));

        // NPSH available.
        c.Input("p_atm");
        c.Input("z_s");

        if (fluid == FluidWater)
        {
            c.Record("h_f_suction", "Σ h_f on the suction side", $"{segments.Count(s => s.Suction)} suction segment(s)", ScalarValue.Of(hfSuction, m), "Suction-side friction loss");
            c.Assign("NPSH_a", "p_atm / (rho * 9.80665 m/s^2) + z_s - h_f_suction - p_v / (rho * 9.80665 m/s^2)", "m", "NPSH available", "Çengel Ch. 8");
            c.Output("NPSH_a", "NPSH available");

            if (c.Has("NPSH_r"))
            {
                c.Input("NPSH_r");
                c.Assign("u_npsh", "NPSH_r / NPSH_a", null, "NPSH utilisation (required / available)");
                c.CheckUtilisation("NPSH_r ≤ NPSH_a", "u_npsh");
            }
        }

        var vPeak = segments.Max(s => q / (Math.PI * s.D * s.D / 4));
        c.Record("v_peak", "highest segment velocity", $"{F(vPeak)} m/s", ScalarValue.Of(vPeak, mps), "Highest velocity");
        c.Record("u_v", "v_peak / v_max", $"{F(vPeak)} / {F(vMax)}", new ScalarValue(vPeak / vMax), "Velocity utilisation");
        c.Output("v_peak", "Highest velocity");
        c.CheckUtilisation("Velocity ≤ limit", "u_v");
    }

    private static double? Duty(CalcRunContext c, string suffix, string label, IReadOnlyList<double> qp, IReadOnlyList<double> hp, Func<double, double> hSys, double rho, double g, Unit m, Unit kPa)
    {
        double Gap(double flow) => Interpolation.Linear(flow, qp, hp) - hSys(flow);
        var lo = qp[0];
        var hi = qp[^1];

        if (Gap(lo) * Gap(hi) > 0)
        {
            c.Warn("no-duty", $"Q_pump: the pump curve {label} does not cross the system curve between {F(lo * 3600)} and {F(hi * 3600)} m³/h.");
            c.CheckCondition($"Duty point exists ({label})", false, "no crossing", "pump ≥ system somewhere", null);
            return null;
        }

        var qd = RootFinding.Bisect(Gap, lo, hi, 1e-12, 200);
        var hd = hSys(qd);
        c.Record($"Q_duty{suffix}", $"duty flow ({label})", "H_pump(Q) = H_sys(Q), bisection", ScalarValue.Of(qd * 3600, "m^3/h"), $"Duty flow {label}");
        c.Record($"H_duty{suffix}", $"duty head ({label})", $"H_sys({F(qd * 3600)} m³/h)", ScalarValue.Of(hd, m), $"Duty head {label}");
        c.Record($"P_shaft{suffix}", "ρ g Q H / η", $"{F(rho)} × {F(g)} × {F(qd)} × {F(hd)} / {F(c.Si("eta_pump"))}", ScalarValue.Of(rho * g * qd * hd / c.Si("eta_pump") / 1e3, "kW"), $"Shaft power at duty {label}");
        c.Output($"Q_duty{suffix}", $"Duty flow {label}");
        c.Output($"H_duty{suffix}", $"Duty head {label}");
        c.Output($"P_shaft{suffix}", $"Shaft power {label}");
        c.CheckCondition($"Duty point exists ({label})", true, $"{F(qd * 3600)} m³/h", $"{F(hd)} m", $"Q_duty{suffix}");
        return rho * g * qd * hd / c.Si("eta_pump");
    }

    private static double Cell(CalcRow row, string key, string unit)
    {
        var cell = row.Get(key);
        return cell?.Number is { } n ? UnitCatalog.Parse(string.IsNullOrEmpty(cell.Unit) ? unit : cell.Unit).ToSi(n) : 0;
    }

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
