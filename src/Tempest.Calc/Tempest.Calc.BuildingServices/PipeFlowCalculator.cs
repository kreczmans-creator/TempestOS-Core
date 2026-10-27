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
    public const string DiameterTrim = "impeller trim (Q ∝ n·D, H ∝ n²·D²)";
    public const string DiameterSimilar = "geometrically similar pump (Q ∝ n·D³, H ∝ n²·D²)";
    public const string ClosedCircuit = "closed (flow and return: the rises cancel, no static lift)";

    private const int MaxDetailedSegments = 50;
    private const int CurvePoints = 31;

    private static readonly IReadOnlyList<CalcColumnSpec> SegmentColumns =
    [
        new("name", "Segment", CalcInputKind.Text, IsOptional: true),
        new("L", "Length", CalcInputKind.Quantity, "m", HardMin: 0, HardMax: 1e6),
        new("D", "Inner diameter", CalcInputKind.Quantity, "mm", HardMin: 0.1, HardMax: 1e4),
        new("OD", "Outside diameter of the pipe (optional; the first segment's is offered as OD_1, the inside diameter of its insulation)", CalcInputKind.Quantity, "mm", HardMin: 0.1, HardMax: 1e4, IsOptional: true),
        new("eps", "Roughness", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 100),
        new("K", "Σ K of fittings", CalcInputKind.Number, HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("L_eq", "Equivalent length of fittings", CalcInputKind.Quantity, "m", HardMin: 0, HardMax: 1e6, IsOptional: true),
        new("dz", "Rise in elevation (+ up)", CalcInputKind.Quantity, "m", HardMin: -1e4, HardMax: 1e4, IsOptional: true),
        new("side", "Pump side", CalcInputKind.Choice, Choices: ["discharge", "suction"], IsOptional: true),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.10.1", CalcDiscipline.BuildingServices, "Pipe pressure drop and pump duty",
        "Calculates the friction and fitting losses of series pipe segments at a given flow, the system curve and the duty point against a pump curve. It also gives the affinity-law shift for a speed or impeller change, and the NPSH.",
        "Design flow and velocity: Q = Φ/(ρ c_p ΔT) when a heat load is given in place of a flow; v = Q/(πD²/4)\n" +
        "Reynolds number and friction factor: Re = ρvD/μ; f = 64/Re for laminar flow (Re < 2300); Colebrook–White 1/√f = −2 log10(ε/3.7D + 2.51/(Re√f)) for turbulent flow, by fixed-point iteration (at most 100 steps) from the Swamee–Jain value, which also cross-checks it\n" +
        "Head lost in a segment: h_f = f (L + L_eq)/D · v²/2g + K v²/2g\n" +
        "System curve: H_sys(Q) = Σdz + h_out + Σh_f,lam·(Q/Q₀) + Σh_f,other·(Q/Q₀)², with a laminar segment's friction scaled as Q and every other friction and fitting loss as Q² (a closed circuit has Σdz = 0)\n" +
        "Water held in the pipes: V_pipe = Σ π/4·D²·L\n" +
        "Duty point: H_pump(Q) = H_sys(Q), every crossing found by a scan and refined by bisection, the stable one nearest the design flow reported; pump affinity laws Q ∝ n·D^a, H ∝ n²·D², with a = 1 for a trimmed impeller (the default) and a = 3 for a geometrically similar pump\n" +
        "Net positive suction head available: NPSH_a = p_atm/ρg + z_s − h_f,suction − p_v/ρg, with h_f,suction that of the 'suction' segments at the larger of the design flow and the duty flow\n" +
        "Shaft torque for the motor: T = P/ω, with P the larger of the design-flow and duty-point shaft power at the speed n_pump, or the shifted duty power at the shifted speed n_pump·n₂/n₁",
        [
            new CalcInputSpec("fluid", "Fluid", CalcInputKind.Choice, Choices: [FluidWater, FluidCustom], DefaultChoice: FluidWater, Group: "Setup"),
            new CalcInputSpec("circuit", "Circuit (a closed flow-and-return loop has no static lift: the rises entered are ignored)", CalcInputKind.Choice, Choices: [OpenCircuit, ClosedCircuit], DefaultChoice: OpenCircuit, Group: "Setup"),
            new CalcInputSpec("segments", "Pipe segments in series", CalcInputKind.Table, Columns: SegmentColumns, MaxRows: 500, Group: "Setup"),
            new CalcInputSpec("rho_custom", "Density (custom fluid)", CalcInputKind.Quantity, "kg/m^3", Min: 500, Max: 2000, HardMin: 1, HardMax: 1e5, IsOptional: true, Group: "Material"),
            new CalcInputSpec("mu_custom", "Dynamic viscosity (custom fluid)", CalcInputKind.Quantity, "mPa*s", Min: 0.1, Max: 1000, HardMin: 1e-4, HardMax: 1e6, IsOptional: true, Group: "Material"),
            new CalcInputSpec("h_out", "Pressure head required at the outlet", CalcInputKind.Quantity, "m", Default: 0, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e5, Group: "Loads"),
            new CalcInputSpec("Q", "Design flow rate (optional; blank = the flow comes from the heat load)", CalcInputKind.Quantity, "m^3/h", Default: 20, Min: 0.01, Max: 1e5, HardMin: 1e-9, HardMax: 1e7, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("Phi_load", "Heat carried by the water (gives the design flow with ΔT; replaces Q)", CalcInputKind.Quantity, "kW", UnitChangedAt: "1.10.0", UnitWas: "W", Min: 0.01, Max: 1e5, HardMin: 1e-6, HardMax: 1e7, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("dT_load", "Flow-to-return temperature difference across the load", CalcInputKind.Quantity, "K", Min: 1, Max: 100, HardMin: 0.01, HardMax: 500, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("cp_load", "Specific heat of the fluid in the heat-load flow (optional; blank = 4186 J/(kg·K) assumed for water, and required for a custom fluid)", CalcInputKind.Quantity, "J/(kg*K)", Min: 1000, Max: 6000, HardMin: 100, HardMax: 1e5, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("T_fluid", "Fluid temperature (mean; link the mean water temperature of the sealed system for a heating loop)", CalcInputKind.Quantity, "degC", Default: 20, Min: 0, Max: 100, HardMin: -50, HardMax: 300, Group: "Conditions", Aliases: ["T"]),
            new CalcInputSpec("p_atm", "Atmospheric pressure (NPSH)", CalcInputKind.Quantity, "kPa", Default: 101.325, Min: 50, Max: 110, HardMin: 1, HardMax: 1000, Group: "Conditions"),
            new CalcInputSpec("z_s", "Static head at the pump suction (+ flooded, − lift)", CalcInputKind.Quantity, "m", Default: 0, Min: -10, Max: 100, HardMin: -1e3, HardMax: 1e4, Group: "Conditions"),
            new CalcInputSpec("T_line", "Design temperature of the line for the insulation step: the coldest (cold line) or hottest (hot line) fluid temperature in it, for example the flow temperature of a 6/12 °C chilled line whose mean is 9 °C (optional; blank = the fluid temperature; offered as a port only when it or the fluid temperature is entered; not used for the fluid properties)", CalcInputKind.Quantity, "degC", Min: -50, Max: 150, HardMin: -100, HardMax: 400, IsOptional: true, Group: "Conditions"),
            new CalcInputSpec("v_limit", "Velocity limit (warning and check)", CalcInputKind.Quantity, "m/s", Default: 3, Min: 0.5, Max: 10, HardMin: 0.01, HardMax: 100, Group: "Requirements", Aliases: ["v_max"]),
            new CalcInputSpec("D_law", "Law for the diameter ratio: a trimmed impeller of the same casing (Q ∝ D, H ∝ D²) or a geometrically similar pump (Q ∝ D³, H ∝ D²)", CalcInputKind.Choice, Choices: [DiameterTrim, DiameterSimilar], DefaultChoice: DiameterTrim, Group: "Pump"),
            new CalcInputSpec("n_ratio", "Speed ratio n₂/n₁ (affinity)", CalcInputKind.Number, Default: 1, Min: 0.2, Max: 2, HardMin: 0.01, HardMax: 10, Group: "Pump"),
            new CalcInputSpec("D_ratio", "Impeller diameter ratio D₂/D₁ (affinity; the law is chosen above)", CalcInputKind.Number, Default: 1, Min: 0.7, Max: 1.1, HardMin: 0.1, HardMax: 5, Group: "Pump"),
            new CalcInputSpec("eta_pump", "Pump efficiency at duty (for shaft power)", CalcInputKind.Number, Default: 0.7, Min: 0.2, Max: 0.95, HardMin: 0.01, HardMax: 1, Group: "Pump"),
            new CalcInputSpec("Q_pump", "Pump curve: flow points (optional; blank = no duty point)", CalcInputKind.Vector, "m^3/h", IsOptional: true, Group: "Pump"),
            new CalcInputSpec("H_pump", "Pump curve: head points (optional; blank = no duty point)", CalcInputKind.Vector, "m", IsOptional: true, Group: "Pump"),
            new CalcInputSpec("n_pump", "Pump rated speed, the speed of the pump curve as entered, which gives the shaft torque (optional; blank = no shaft torque)", CalcInputKind.Quantity, "rpm", Min: 100, Max: 6000, HardMin: 1, HardMax: 1e5, IsOptional: true, Group: "Pump"),
            new CalcInputSpec("p_v_custom", "Vapour pressure of the custom fluid at the fluid temperature, absolute (optional; blank = NPSH_a is not computed; needed for NPSH_a of a custom fluid)", CalcInputKind.Quantity, "kPa", Min: 0, Max: 500, HardMin: 0, HardMax: 1e4, IsOptional: true, Group: "NPSH"),
            new CalcInputSpec("NPSH_r", "NPSH required by the pump, at the flow NPSH_a is evaluated at: the larger of the design flow and the duty flow (optional)", CalcInputKind.Quantity, "m", Min: 0, Max: 50, HardMin: 0, HardMax: 1e3, IsOptional: true, Group: "NPSH"),
        ],
        [
            new CalcAssumption("Flow is steady, incompressible and fully developed, and all segments carry the full design flow in series.", "Darcy–Weisbach."),
            new CalcAssumption("Fitting losses scale with velocity head (K method) or add pipe length (equivalent-length method).", "Crane TP-410 practice."),
            new CalcAssumption("Both fitting methods may be used on one segment.", "Crane TP-410 practice."),
            new CalcAssumption("The system curve scales turbulent and transitional friction and all fitting losses with Q² from the design flow.", "H_sys = H_static + k₁ Q + k₂ Q²."),
            new CalcAssumption("The friction factor is held at its design-flow value on the system curve.", "H_sys = H_static + k₁ Q + k₂ Q²."),
            new CalcAssumption("The friction of a laminar segment scales with Q, and the static part is independent of flow.", "H_sys = H_static + k₁ Q + k₂ Q²."),
            new CalcAssumption("Water properties are from published correlations at atmospheric pressure, 0 to 100 °C.", "Kell 1975; Vogel fit; Hyland & Wexler 1983."),
            new CalcAssumption("Any other fluid is entered with its own density and viscosity and their provenance.", "Stated."),
        ],
        [
            new CalcClauseRef("Crane Co., Flow of Fluids Through Valves, Fittings and Pipe (Technical Paper No. 410)", "—", "Flow of fluids through valves, fittings and pipe"),
            new CalcClauseRef("Çengel & Cimbala, Fluid Mechanics", "Ch. 8, 4th ed.", "Internal flow: friction factor, minor losses, pump duty"),
            new CalcClauseRef("CIBSE Guide C", "§4", "Flow of fluids in pipes and ducts (principles)"),
            new CalcClauseRef("Kell, J. Chem. Eng. Data 20(1), 1975", "—", "Density of liquid water"),
        ],
        [
            new CalcApplicabilityRule("heat-flow", "A heat load needs its temperature difference (and the reverse); without a heat load a design flow is required.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("heat-t", "A heat load is given without the fluid temperature: 20 °C is taken.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("cp_load", "A custom fluid requires its specific heat for the heat-load flow.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("heat-flow-q", "A design flow is entered as well as a heat load: the flow from the heat load is used and Q ignored.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("custom-fluid", "A custom fluid requires its density and viscosity.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("water-range", "Water correlations hold for 0–100 °C.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("segments", "At least one segment is required.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("laminar", "Laminar flow in a segment (Re < 2300): f = 64/Re.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("transition", "Transitional flow (2300 ≤ Re < 4000): the friction factor is uncertain; Colebrook is used.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("fittings", "Fitting loss coefficients K are entered for a segment below Re 4000: K is a turbulent-flow value and rises at low Re, so the fittings term K·v²/2g is understated.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("velocity", "A segment exceeds the velocity limit.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("pump-curve", "The pump curve needs at least two points with matching flow and head counts, flows increasing.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("npsh-custom", "NPSH_r is entered for a custom fluid with no vapour pressure p_v_custom: NPSH_a cannot be computed and no NPSH check could be made, so the sheet is refused.", CalcNoticeSeverity.Block, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("npsh-no-suction", "NPSH_r is entered but no segment is marked 'suction' (a blank side column means discharge): NPSH_a carries no suction friction and is optimistic unless the suction line has no friction.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("no-duty", "The pump curve and the system curve do not cross within the curve's flow range.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("plant-loss", "A heat-load sheet has no outlet head h_out: the pump head then holds pipe friction only, without the water-side loss of the boiler, heat exchanger, coil, valves and emitters.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("fluid-cp", "The specific heat of the heat-load flow differs from water's by more than 5 %: the fluid is not water, but this sheet is on water unless a custom fluid is chosen, so the flow, friction and torque are those of water.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("multi-duty", "The pump curve crosses the system curve more than once: all crossings are listed and the stable one nearest the design flow is reported.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("duty-unstable", "The only crossing of the pump curve and the system curve is on a rising part of the pump curve, where the operating point is unstable: the stable-duty check fails.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("eta-default", "The pump efficiency is the default 0.7 for a pump of under about 1 kW shaft power: small circulators have a far lower overall efficiency, so the shaft power and torque are understated.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("summary-only", "More than 50 segments: per-segment lines are omitted.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-laminar-oil", "Laminar oil flow (analytic Hagen–Poiseuille)",
                new CalcReferenceSource("Çengel & Cimbala, Fluid Mechanics", "4th ed.", "Ch. 8, laminar flow in a pipe: Δp = 32 μ L v / D² (Hagen–Poiseuille); ρ = 888 kg/m³, μ = 0.8 Pa·s, D = 50 mm, L = 40 m, Q = 1 m³/h: v = 0.14147 m/s, Re = 7.852, f = 8.151, h_f = f L/D v²/2g = 6.654 m, Δp = 32 μ L v / D² = 57.95 kPa"),
                [CalcInputValue.Choice("fluid", FluidCustom), CalcInputValue.Quantity("T_fluid", 20, "degC"), CalcInputValue.Quantity("rho_custom", 888, "kg/m^3"), CalcInputValue.Quantity("mu_custom", 800, "mPa*s"), CalcInputValue.Quantity("Q", 1, "m^3/h"), Segments(("oil line", 40, 50, 0.0, 0, 0, 0, "discharge")), CalcInputValue.Quantity("h_out", 0, "m")],
                [new CalcExpectedValue("v_s1", 0.14147, "m/s", 1e-4), new CalcExpectedValue("Re_s1", 7.852, "", 1e-3), new CalcExpectedValue("f_s1", 8.151, "", 1e-3), new CalcExpectedValue("h_f_total", 6.654, "m", 1e-3), new CalcExpectedValue("dp_total", 57.946, "kPa", 1e-3)]),
            new CalcTestCase("ka-turbulent-water", "Turbulent water, Colebrook against an independent solution",
                new CalcReferenceSource("Colebrook–White equation solved independently (Re = 1.0e5, ε/D = 0.001: f = 0.022175; Swamee–Jain 0.022342)", null, "Custom fluid ρ = 1000, μ = 1 mPa·s so Re is exact: D = 100 mm, v = 1 m/s ⇒ Q = 28.274 m³/h, Re = 1e5; ε = 0.1 mm; L = 100 m: h_f = f L/D v²/2g = 0.022175·1000·0.050985 = 1.1306 m"),
                [CalcInputValue.Choice("fluid", FluidCustom), CalcInputValue.Quantity("T_fluid", 20, "degC"), CalcInputValue.Quantity("rho_custom", 1000, "kg/m^3"), CalcInputValue.Quantity("mu_custom", 1, "mPa*s"), CalcInputValue.Quantity("Q", 28.274334, "m^3/h"), Segments(("main", 100, 100, 0.1, 0, 0, 0, "discharge")), CalcInputValue.Quantity("h_out", 0, "m")],
                [new CalcExpectedValue("v_s1", 1.0, "m/s", 1e-6), new CalcExpectedValue("Re_s1", 1e5, "", 1e-6), new CalcExpectedValue("f_s1", 0.022175, "", 5e-4), new CalcExpectedValue("h_f_total", 1.1306, "m", 1e-3), new CalcExpectedValue("f_sj_s1", 0.022342, "", 1e-3)]),
            new CalcTestCase("ka-duty-point", "Duty point against a parabolic pump curve, with K fittings and a rise (analytic)",
                new CalcReferenceSource("Analytic: pump H = 40 − 0.01 Q² (Q in m³/h), system H = 10 + k Q² ⇒ duty where 30 = (k + 0.01) Q²", null, "Water 20 °C, DN80 (D = 80 mm), L = 60 m, ε = 0.045 mm, ΣK = 8, rise 10 m: v = 1.658 m/s, Re ≈ 1.32e5, f ≈ 0.0198, h_f ≈ 3.2 m at 30 m³/h so k ≈ 0.00356 m/(m³/h)²; duty where 30 = (k + 0.01) Q² ⇒ Q ≈ 46.3 m³/h, H ≈ 17.7 m on the piecewise-linear curve through (0, 40), (20, 36), (40, 24), (60, 4); at 90 % speed Q ≈ 40.1 m³/h"),
                [CalcInputValue.Choice("fluid", FluidWater), CalcInputValue.Quantity("T_fluid", 20, "degC"), CalcInputValue.Quantity("Q", 30, "m^3/h"), Segments(("riser", 60, 80, 0.045, 8, 0, 10, "discharge")), CalcInputValue.Quantity("h_out", 0, "m"), CalcInputValue.VectorOf("Q_pump", [0, 20, 40, 60], "m^3/h"), CalcInputValue.VectorOf("H_pump", [40, 36, 24, 4], "m"), CalcInputValue.Of("n_ratio", 0.9), CalcInputValue.Of("D_ratio", 1)],
                [new CalcExpectedValue("H_static", 10, "m", 1e-9), new CalcExpectedValue("H_duty", 17.67, "m", 0.01), new CalcExpectedValue("Q_duty", 46.3, "m^3/h", 0.01), new CalcExpectedValue("Q_duty2", 40.1, "m^3/h", 0.01)]),
            new CalcTestCase("ka-closed-circuit", "The duty-point loop as a closed flow-and-return circuit: no static lift, and the water it holds",
                new CalcReferenceSource("Analytic (closed circuit: Σdz is not a lift; V_pipe = π/4·D²·L)", null, "DN80 (D = 80 mm), L = 60 m, rise 10 m entered but ignored in a closed circuit: H_static = h_out = 0; V_pipe = π/4 × 0.08² × 60 = 0.301593 m³ = 301.593 L"),
                [CalcInputValue.Choice("fluid", FluidWater), CalcInputValue.Quantity("T_fluid", 20, "degC"), CalcInputValue.Quantity("Q", 30, "m^3/h"), Segments(("loop", 60, 80, 0.045, 8, 0, 10, "discharge")), CalcInputValue.Choice("circuit", ClosedCircuit), CalcInputValue.Quantity("h_out", 0, "m")],
                [new CalcExpectedValue("H_static", 0, "m", 1e-9), new CalcExpectedValue("V_pipe", 301.5929, "L", 1e-6)]),
            new CalcTestCase("ka-heat-load-flow", "Design flow from a heat load: 12 kW at a 20 K difference, water at 70 °C (analytic)",
                new CalcReferenceSource("Analytic: Q = Φ / (ρ c_p ΔT)", null, "ρ(70 °C) = 977.7696 kg/m³ (Kell 1975, evaluated from the correlation); c_p = 4186 J/(kg·K) as entered; Q = 12 000 / (977.7696 × 4186 × 20) = 1.465938e-4 m³/s = 0.5277375 m³/h"),
                [CalcInputValue.Choice("fluid", FluidWater), CalcInputValue.Quantity("T_fluid", 70, "degC"), CalcInputValue.Quantity("Phi_load", 12, "kW"), CalcInputValue.Quantity("dT_load", 20, "K"), CalcInputValue.Quantity("cp_load", 4186, "J/(kg*K)"), Segments(("loop", 40, 28, 0.045, 0, 0, 0, "discharge")), CalcInputValue.Quantity("h_out", 0, "m")],
                [new CalcExpectedValue("Q_design", 0.5277375, "m^3/h", 1e-6)]),
        ],
        DiagramKey: "pipe-pump",
        Category: "Hydraulics",
        OutputPorts:
        [
            new CalcOutputPort("Q_design", "m^3/h", "Design flow (as entered, or from the heat load)", "pipe.flow_design"),
            new CalcOutputPort("H_static", "m", "Static head", "pipe.head_static"),
            new CalcOutputPort("V_pipe", "L", "Water held in the pipe segments (→ sealed-system V_pipe, where the chain order allows)", "pipe.volume_water"),
            new CalcOutputPort("H_sys", "m", "System head at the design flow", "pipe.head_system"),
            new CalcOutputPort("h_f_total", "m", "Total friction and fitting loss at the design flow", "pipe.head_loss_total"),
            new CalcOutputPort("dp_total", "kPa", "Friction pressure drop at the design flow", "pipe.pressure_drop_total"),
            new CalcOutputPort("dp_sys", "kPa", "System pressure at the design flow", "pipe.pressure_system"),
            new CalcOutputPort("P_hyd", "kW", "Hydraulic power at the design point", "pump.power_hydraulic"),
            new CalcOutputPort("P_shaft_design", "kW", "Pump shaft power at the design flow (needs the pump speed)", "pump.power_shaft_design"),
            new CalcOutputPort("T_shaft", "kN*m", "Pump shaft torque, for the larger of the design-flow and duty-point shaft power (→ motor-sizing T_L)", "pump.torque_shaft"),
            new CalcOutputPort("n_shaft", "rpm", "Pump shaft speed (→ motor-sizing n_L)", "pump.speed_shaft"),
            new CalcOutputPort("v_peak", "m/s", "Highest velocity", "pipe.velocity_peak"),
            new CalcOutputPort("T_line", "degC", "Design temperature of the line to insulate: T_line as entered, else the fluid temperature when entered (not offered when neither is entered; → insulation T_proc)", "pipe.temperature_line"),
            new CalcOutputPort("OD_1", "mm", "Outside diameter of the first pipe segment, where entered (→ insulation D_i)", "pipe.diameter_outside_first"),
            new CalcOutputPort("NPSH_a", "m", "NPSH available (water, or a custom fluid with its vapour pressure)", "pump.npsh_available"),
            new CalcOutputPort("Q_duty", "m^3/h", "Duty flow against the pump curve", "pump.flow_duty"),
            new CalcOutputPort("H_duty", "m", "Duty head against the pump curve", "pump.head_duty"),
            new CalcOutputPort("P_shaft", "kW", "Shaft power at the duty point", "pump.power_shaft_duty"),
        ]);

    /// <summary>Segments: (name, L m, D mm, ε mm, ΣK, L_eq m, dz m, side).</summary>
    public static CalcInputValue Segments(params (string Name, double L, double D, double Eps, double K, double Leq, double Dz, string Side)[] rows) =>
        CalcInputValue.Table("segments", rows.Select(r => CalcRow.Of(("name", new CalcCell(Text: r.Name)), ("L", new CalcCell(r.L, "m")), ("D", new CalcCell(r.D, "mm")), ("eps", new CalcCell(r.Eps, "mm")), ("K", new CalcCell(r.K)), ("L_eq", new CalcCell(r.Leq, "m")), ("dz", new CalcCell(r.Dz, "m")), ("side", new CalcCell(Text: r.Side)))).ToList());

    private sealed record Segment(int Index, string Name, double L, double D, double Eps, double K, double Leq, double Dz, bool Suction);

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_npsh", "u_v");
        const double g = 9.80665;
        var fluid = c.InputText("fluid");
        var tEntered = c.Has("T_fluid");

        var t = c.InputSi("T_fluid") - 273.15;
        double rho, mu;

        // A temperature left out takes the template default with the validator's own notice; with a heat load that default is
        // wrong for a heating loop, so say so specifically.
        if (!tEntered && c.Has("Phi_load"))
            c.Warn("heat-t", "T_fluid: the fluid temperature was not entered with the heat load, so 20 °C is taken. Enter it, or link the mean water temperature of the sealed system.");

        if (fluid == FluidCustom)
        {
            if (!c.Has("rho_custom") || !c.Has("mu_custom"))
            {
                c.Block("custom-fluid", c.Has("rho_custom") ? "mu_custom: enter the viscosity of the custom fluid." : "rho_custom: enter the density of the custom fluid.");
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
                c.Block("water-range", "T_fluid: the water correlations hold for 0 to 100 °C.");
                return;
            }

            c.Record("rho", "Kell (1975) density of water at T", $"T = {F(t)} °C", ScalarValue.Of(Water.Density(t), "kg/m^3"), "Density", "Kell 1975");
            c.Record("mu", "Vogel fit μ = 0.02939 exp(507.88/(T_K − 149.3)) mPa·s", $"T_K = {F(t + 273.15)} K", ScalarValue.Of(Water.Viscosity(t) * 1e3, "mPa*s"), "Dynamic viscosity");
            c.Record("p_v", "Hyland–Wexler saturation pressure", $"T = {F(t)} °C", ScalarValue.Of(Water.VapourPressure(t) / 1e3, "kPa"), "Vapour pressure (NPSH)");
        }

        rho = c.Si("rho");
        mu = c.Si("mu");

        // The line temperature the insulation step sizes on (anti-condensation on the coldest, a hot surface limit on the hottest), which need not be the mean T:
        // offered only when the user entered it, never the 20 °C default.
        if (c.Has("T_line"))
        {
            c.Input("T_line");
            var tLine = c.Si("T_line") - 273.15;
            c.Record("T_line", "T_line (as entered)", $"{F(tLine)} °C", ScalarValue.Of(tLine, UnitCatalog.Parse("degC")), "Design temperature of the line to insulate (the coldest or hottest fluid temperature in it)");
            c.Output("T_line", "Line design temperature");
        }
        else if (tEntered)
        {
            c.Record("T_line", "T (no T_line entered)", $"{F(t)} °C", ScalarValue.Of(t, UnitCatalog.Parse("degC")), "Design temperature of the line to insulate: the fluid temperature T, since no T_line was entered");
            c.Output("T_line", "Line design temperature");
        }
        c.Input("h_out");
        c.Input("v_limit");

        if (c.Has("Phi_load") || c.Has("dT_load"))
        {
            if (!c.Has("Phi_load") || !c.Has("dT_load"))
            {
                c.Block("heat-flow", "Phi_load: enter the heat load and its temperature difference dT_load together.");
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
                c.Block("cp_load", "cp_load: enter the specific heat of the custom fluid for the heat-load flow.");
                return;
            }
            else
            {
                c.Record("c_p", "c_p of water 4186 J/(kg·K) (assumed)", "rounded constant for 10–90 °C", ScalarValue.Of(4186, "J/(kg*K)"), "Specific heat of water (assumed; enter cp_load to change it)");
                c.Assume("The specific heat of water is taken as 4186 J/(kg·K), a rounded constant for 10–90 °C.", "Assumed constant; enter cp_load to use another value.");
            }

            if (c.Has("Q"))
                c.Warn("heat-flow-q", "Q: the design flow comes from the heat load, so Q is ignored. Clear Q to remove this notice.");

            c.Assign("Q_design", "Phi_load / (rho * c_p * dT_load)", "m^3/h", "Design flow from the heat carried: Φ / (ρ c_p ΔT)");

            if (c.Si("h_out") < 1e-9)
                c.Warn("plant-loss", "h_out: the head required at the outlet is 0, so the pump head holds pipe friction only. Enter the plant's water-side loss in h_out, or as K or L_eq on a segment.");

            if (fluid == FluidWater && c.Has("cp_load") && Math.Abs(c.Si("cp_load") - 4186) > 0.05 * 4186)
                c.Warn("fluid-cp", $"cp_load: {F(c.Si("cp_load"))} J/(kg·K) differs from water's by more than 5 %, so the fluid is not water. Choose the custom fluid and enter its density and viscosity, or the flow, friction and torque are those of water.");
        }
        else
        {
            if (!c.Has("Q"))
            {
                c.Block("heat-flow", "Q: enter the design flow, or a heat load with its temperature difference.");
                return;
            }

            c.Input("Q");
            c.Assign("Q_design", "Q", "m^3/h", "Design flow (as entered)");
        }

        var q = c.Si("Q_design");
        var vMax = c.Si("v_limit");

        var rows = c.Rows("segments");

        if (rows.Count == 0)
        {
            c.Block("segments", c.EnterRows("segments"));
            return;
        }

        var segments = rows.Select((r, i) => new Segment(i + 1, r.Get("name")?.Text is { Length: > 0 } n ? n : $"segment {i + 1}",
            Cell(r, "L", "m"), Cell(r, "D", "mm"), Cell(r, "eps", "mm"), r.Get("K")?.Number ?? 0, Cell(r, "L_eq", "m"), Cell(r, "dz", "m"),
            string.Equals(r.Get("side")?.Text, "suction", StringComparison.OrdinalIgnoreCase))).ToList();

        if (segments.Any(s => s.D <= 0 || s.L < 0))
        {
            c.Block("segments", "segments: enter a positive diameter and a non-negative length for every segment.");
            return;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Get("OD")?.Number is not null && Cell(rows[i], "OD", "mm") <= segments[i].D)
            {
                c.Block("segments", $"segments: segment {i + 1} ('{segments[i].Name}') has an outside diameter of {F(Cell(rows[i], "OD", "mm") * 1000)} mm, not larger than its inner diameter of {F(segments[i].D * 1000)} mm. Enter the outside diameter or clear it.");
                return;
            }
        }

        var detailed = segments.Count <= MaxDetailedSegments;

        if (!detailed)
            c.Warn("summary-only", "segments: there are more than 50 segments. Per-segment lines are omitted.");

        double hfTotal = 0, hfSuction = 0, dzTotal = 0;
        double hfLin = 0, hfQuad = 0, suctionLin = 0, suctionQuad = 0;
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
                c.Warn("laminar", $"segments: {s.Name} is laminar (Re = {F(re)}), so f = 64/Re. The system curve and the duty point scale its friction with Q, not Q².");
            }
            else
            {
                var (fc, iterations) = Friction.Colebrook(re, relRough);
                f = fc;
                fSj = Friction.SwameeJain(re, relRough);
                fText = $"Colebrook–White, {iterations} iterations (Swamee–Jain start {F(fSj)})";

                if (re < Friction.TurbulentLimit)
                    c.Warn("transition", $"segments: {s.Name} is transitional (Re = {F(re)}), so the friction factor is uncertain.");
            }

            if (s.K > 0 && re < Friction.TurbulentLimit)
                c.Warn("fittings", $"segments: {s.Name} has fitting losses (K = {F(s.K)}) at Re = {F(re)}. The K values are for turbulent flow, so the fitting loss is understated here.");

            var hFriction = f * (s.L + s.Leq) / s.D * v * v / (2 * g);
            var hFitting = s.K * v * v / (2 * g);
            var hf = hFriction + hFitting;
            var laminar = re < Friction.LaminarLimit;

            // The system curve scales a laminar segment's friction with Q and everything else with Q².
            if (laminar)
            {
                hfLin += hFriction;
                hfQuad += hFitting;
            }
            else
            {
                hfQuad += hf;
            }

            hfTotal += hf;
            dzTotal += s.Dz;

            if (s.Suction)
            {
                hfSuction += hf;
                suctionLin += laminar ? hFriction : 0;
                suctionQuad += laminar ? hFitting : hf;
            }

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
        if (rows[0].Get("OD")?.Number is not null)
        {
            c.Record("OD_1", "OD of the first segment (as entered)", $"{segments[0].Name}: {F(Cell(rows[0], "OD", "mm") * 1000)} mm", ScalarValue.Of(Cell(rows[0], "OD", "mm") * 1e3, "mm"), "Outside diameter of the first pipe segment (the inside of its insulation)");
            c.Output("OD_1", "Outside diameter of the first segment");
        }

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
        double HSys(double flow)
        {
            var x = flow / q;
            return hStatic + hfLin * x + hfQuad * x * x;
        }

        double SuctionFriction(double flow)
        {
            var x = flow / q;
            return suctionLin * x + suctionQuad * x * x;
        }

        var qMaxCurve = 1.5 * q;

        // Pump curve and duty point.
        var hasPump = c.Has("Q_pump") && c.Has("H_pump");
        var etaRegistered = false;
        double? dutyShaft = null;
        double? dutyShaft2 = null;
        DutyResult? dutyResult = null;
        DutyResult? dutyResult2 = null;
        var shifted = false;
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
                c.Block("pump-curve", "Q_pump: enter at least two points, with the same count in H_pump and increasing flows.");
                return;
            }

            qMaxCurve = Math.Max(qMaxCurve, qp[^1]);
            c.Input("n_ratio");
            c.Input("D_ratio");
            c.Input("D_law");
            c.Input("eta_pump");
            etaRegistered = true;
            var nr = c.Si("n_ratio");
            var dr = c.Si("D_ratio");
            var trim = c.InputText("D_law") == DiameterTrim;
            var qExponent = trim ? 1 : 3;

            dutyResult = Duty(c, "", "as entered", qp, hp, HSys, rho, g, m, kPa, q);
            dutyShaft = dutyResult?.Shaft;

            if (Math.Abs(nr - 1) > 1e-12 || Math.Abs(dr - 1) > 1e-12)
            {
                shifted = true;
                var qs = qp.Select(x => x * nr * Math.Pow(dr, qExponent)).ToList();
                var hs = hp.Select(x => x * nr * nr * dr * dr).ToList();
                c.Record("affinity", trim ? "Q₂ = Q₁ (n₂/n₁)(D₂/D₁), H₂ = H₁ (n₂/n₁)²(D₂/D₁)² (impeller trim)" : "Q₂ = Q₁ (n₂/n₁)(D₂/D₁)³, H₂ = H₁ (n₂/n₁)²(D₂/D₁)² (geometrically similar pump)", $"n₂/n₁ = {F(nr)}, D₂/D₁ = {F(dr)}", new TextValue("curve scaled"), "Affinity laws applied to the pump curve");
                dutyResult2 = Duty(c, "2", "after the affinity shift", qs, hs, HSys, rho, g, m, kPa, q);
                dutyShaft2 = dutyResult2?.Shaft;
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

            if (shifted && dutyShaft2 is not null)
            {
                // chain.wet-heating-R1: the pump runs at the shifted speed, so the motor sees the shifted duty power at the shifted speed.
                c.Assign("P_shaft_sel", "max(P_shaft_design, P_shaft2)", "kW", "Shaft power the torque covers: the larger of the design-flow value and the shifted duty-point value");
                c.Assign("n_shaft", "n_pump * n_ratio", "rpm", "Pump shaft speed at the shifted duty (n_pump × n₂/n₁)");
                c.Assign("T_shaft", "P_shaft_sel / n_shaft", "kN*m", "Pump shaft torque P / ω at the shifted speed, P being the larger of the design-flow and shifted duty-point shaft power");
                c.Output("P_shaft_sel", "Shaft power the torque covers (larger of design flow and shifted duty point)");
            }
            else if (dutyShaft is not null)
            {
                c.Assign("P_shaft_sel", "max(P_shaft_design, P_shaft)", "kW", "Shaft power the torque covers: the larger of the design-flow value and the duty-point value");
                c.Assign("T_shaft", "P_shaft_sel / n_pump", "kN*m", "Pump shaft torque P / ω, P being the larger of the design-flow and duty-point shaft power");
                c.Output("P_shaft_sel", "Shaft power the torque covers (larger of design flow and duty point)");
            }
            else
            {
                c.Assign("T_shaft", "P_shaft_design / n_pump", "kN*m", "Pump shaft torque P / ω at the design flow");
            }

            if (!(shifted && dutyShaft2 is not null))
                c.Assign("n_shaft", "n_pump", "rpm", "Pump shaft speed");

            c.Output("P_shaft_design", "Shaft power at the design flow");
            c.Output("T_shaft", "Shaft torque");
            c.Output("n_shaft", "Shaft speed");
        }

        // Small circulators have a far lower overall efficiency than the default 0.7, so their shaft power (and the torque handed to a motor) is understated.
        if (c.Has("n_pump") || hasPump)
        {
            var eta = c.InputSi("eta_pump");
            var shaftW = c.Si("P_hyd") / eta;

            if (Math.Abs(eta - 0.7) < 1e-9 && shaftW < 1000)
                c.Warn("eta-default", $"eta_pump: the default 0.7 is used for a pump of only {F(shaftW)} W shaft power. A small circulator often has an efficiency of 0.2 or less, so enter the efficiency at its duty point.");
        }

        c.Series(new CalcSeries("System curve", "Q", "m^3/h", "H", "m", Enumerable.Range(0, CurvePoints).Select(i => { var fq = qMaxCurve * i / (CurvePoints - 1); return (fq * 3600, HSys(fq)); }).ToList()));

        // NPSH available.
        c.Input("p_atm");
        c.Input("z_s");

        var customVapour = fluid != FluidWater && c.Has("p_v_custom");

        // A custom fluid with an NPSH_r and no vapour pressure has no NPSH_a, so the cavitation check is not made and a pass said nothing about cavitation (it warned and passed): refused.
        if (fluid != FluidWater && c.Has("NPSH_r") && !customVapour)
        {
            c.Block("npsh-custom", "p_v_custom: enter the absolute vapour pressure of the custom fluid, or clear NPSH_r. Without it NPSH_a cannot be computed.");
            return;
        }

        if (c.Has("NPSH_r") && (fluid == FluidWater || customVapour) && !segments.Any(s => s.Suction))
            c.Warn("npsh-no-suction", "segments: NPSH_r is entered but no segment is marked 'suction', so NPSH_a carries no suction friction. Mark the suction segments, or accept that the suction line has no friction loss.");

        if (fluid == FluidWater || customVapour)
        {
            if (customVapour)
            {
                c.Input("p_v_custom");
                c.Assign("p_v", "p_v_custom", "kPa", "Vapour pressure of the custom fluid at T (as entered)");
            }

            // The pump runs at its duty flow when a curve is entered: NPSH_a is evaluated at the larger of the design flow and that flow (the suction friction rises with the flow).
            var dutyFlow = shifted && dutyResult2 is not null ? dutyResult2.Flow : dutyResult?.Flow;
            var npshFlow = dutyFlow is { } d && d > q * (1 + 1e-9) ? d : q;
            var hfSuctionAt = npshFlow > q ? SuctionFriction(npshFlow) : hfSuction;

            if (npshFlow > q)
                c.Assume($"NPSH_a is evaluated at the duty flow {F(npshFlow * 3600)} m³/h, which is larger than the design flow {F(q * 3600)} m³/h: the pump runs at its duty point, where the suction friction is higher. NPSH_r is to be read at that flow.", "Larger of the design flow and the duty flow.");

            c.Record("h_f_suction", "Σ h_f on the suction side" + (npshFlow > q ? " at the duty flow" : string.Empty), $"{segments.Count(s => s.Suction)} suction segment(s)" + (npshFlow > q ? $", at {F(npshFlow * 3600)} m³/h" : string.Empty), ScalarValue.Of(hfSuctionAt, m), "Suction-side friction loss" + (npshFlow > q ? " at the duty flow" : string.Empty));
            c.Assign("NPSH_a", "p_atm / (rho * 9.80665 m/s^2) + z_s - h_f_suction - p_v / (rho * 9.80665 m/s^2)", "m", "NPSH available", "Çengel Ch. 8");
            c.Output("NPSH_a", "NPSH available");

            if (c.Has("NPSH_r"))
            {
                c.Input("NPSH_r");
                c.Assign("u_npsh", "NPSH_r / NPSH_a", null, "NPSH utilisation (required / available)");
                c.Check("NPSH required against NPSH available", "NPSH_r", "NPSH_a");
            }
        }

        var vPeak = segments.Max(s => q / (Math.PI * s.D * s.D / 4));
        c.Record("v_peak", "highest segment velocity", $"{F(vPeak)} m/s", ScalarValue.Of(vPeak, mps), "Highest velocity");
        c.Record("u_v", "v_peak / v_limit", $"{F(vPeak)} / {F(vMax)}", new ScalarValue(vPeak / vMax), "Velocity utilisation");
        c.Output("v_peak", "Highest velocity");
        c.Check("Highest velocity against limit", "v_peak", "v_limit");
    }

    private sealed record DutyResult(double Shaft, double Flow);

    /// <summary>
    /// The duty point of a pump curve against the system curve. The whole curve is scanned (every pump point and 400 even steps) for every crossing and each is refined by bisection;
    /// a crossing where the pump curve falls through the system curve is stable. When there are several the stable one nearest the design flow is reported and all are listed.
    /// </summary>
    private static DutyResult? Duty(CalcRunContext c, string suffix, string label, IReadOnlyList<double> qp, IReadOnlyList<double> hp, Func<double, double> hSys, double rho, double g, Unit m, Unit kPa, double designFlow)
    {
        double Gap(double flow) => Interpolation.Linear(flow, qp, hp) - hSys(flow);
        var lo = qp[0];
        var hi = qp[^1];
        var points = new SortedSet<double>(qp);

        for (var i = 0; i <= 400; i++)
            points.Add(lo + (hi - lo) * i / 400);

        var grid = points.ToList();
        var crossings = new List<(double Flow, bool Stable)>();

        for (var i = 0; i + 1 < grid.Count; i++)
        {
            double a = grid[i], b = grid[i + 1];
            double ga = Gap(a), gb = Gap(b);

            if (ga > 0 && gb <= 0)
                crossings.Add((gb == 0 ? b : RootFinding.Bisect(Gap, a, b, 1e-12, 200), true));
            else if (ga < 0 && gb >= 0)
                crossings.Add((gb == 0 ? b : RootFinding.Bisect(Gap, a, b, 1e-12, 200), false));
            else if (i == 0 && ga == 0)
                crossings.Add((a, gb <= 0));
        }

        if (crossings.Count == 0)
        {
            c.Warn("no-duty", $"Q_pump: the pump curve {label} does not cross the system curve between {F(lo * 3600)} and {F(hi * 3600)} m³/h.");
            c.CheckCondition($"Duty point against the system curve ({label})", false, "no crossing", "pump ≥ system somewhere", null);
            return null;
        }

        var stable = crossings.Where(x => x.Stable).ToList();
        var chosen = stable.Count > 0 ? stable.MinBy(x => Math.Abs(x.Flow - designFlow)) : crossings[^1];

        if (crossings.Count > 1)
            c.Warn("multi-duty", $"Q_pump: the pump curve {label} crosses the system curve {crossings.Count} times, at {string.Join(", ", crossings.Select(x => $"{F(x.Flow * 3600)} m³/h ({(x.Stable ? "stable" : "unstable")})"))}. The stable crossing nearest the design flow, {F(chosen.Flow * 3600)} m³/h, is reported.");

        // The operating point on a rising part of the pump curve is unstable: the pump does not hold it. That is a failed duty, not a notice (it warned and the sheet passed).
        if (stable.Count == 0)
        {
            c.Warn("duty-unstable", $"Q_pump: the pump curve {label} crosses the system curve only where it rises ({F(chosen.Flow * 3600)} m³/h). The operating point is unstable, so the pump will not hold it.");
            c.CheckCondition($"Duty point against a stable crossing ({label})", false, $"{F(chosen.Flow * 3600)} m³/h on a rising pump curve", "a crossing where the pump curve falls", null);
        }

        var qd = chosen.Flow;
        var hd = hSys(qd);
        c.Record($"Q_duty{suffix}", $"duty flow ({label})", "H_pump(Q) = H_sys(Q), scan and bisection", ScalarValue.Of(qd * 3600, "m^3/h"), $"Duty flow {label}");
        c.Record($"H_duty{suffix}", $"duty head ({label})", $"H_sys({F(qd * 3600)} m³/h)", ScalarValue.Of(hd, m), $"Duty head {label}");
        c.Record($"P_shaft{suffix}", "ρ g Q H / η", $"{F(rho)} × {F(g)} × {F(qd)} × {F(hd)} / {F(c.Si("eta_pump"))}", ScalarValue.Of(rho * g * qd * hd / c.Si("eta_pump") / 1e3, "kW"), $"Shaft power at duty {label}");
        c.Output($"Q_duty{suffix}", $"Duty flow {label}");
        c.Output($"H_duty{suffix}", $"Duty head {label}");
        c.Output($"P_shaft{suffix}", $"Shaft power {label}");
        c.CheckCondition($"Duty point against the system curve ({label})", true, $"{F(qd * 3600)} m³/h", $"{F(hd)} m", $"Q_duty{suffix}");
        return new DutyResult(rho * g * qd * hd / c.Si("eta_pump"), qd);
    }

    private static double Cell(CalcRow row, string key, string unit)
    {
        var cell = row.Get(key);
        return cell?.Number is { } n ? UnitCatalog.Parse(string.IsNullOrEmpty(cell.Unit) ? unit : cell.Unit).ToSi(n) : 0;
    }

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
