using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.BuildingServices;

/// <summary>
/// B4 — U-values of built-up elements and the steady-state design heat loss of one room: fabric loss through each element
/// to its adjacent temperature, a thermal-bridge allowance, ventilation loss (optionally through an MVHR supply
/// temperature), the room load Φ_HL and the heat loss coefficient. No design-data table ships: conductivities, surface
/// resistances, design temperatures and ground-floor U-values are user inputs (records); the only built-in numbers are the
/// conventional BS EN ISO 6946 horizontal-flow surface resistances as a labelled default and the air constant 0.34 Wh/(m³·K).
/// </summary>
public sealed class RoomHeatLossCalculator : ICalcCalculator
{
    public const string Id = "bs.room-heat-loss";
    public const string En12831 = "BS EN 12831-1 elemental";
    public const string Dhdg = "CIBSE DHDG reduced method";

    /// <summary>BS EN ISO 6946 conventional internal surface resistance for horizontal heat flow, m²K/W (also used for an internal partition's second face).</summary>
    public const double ConventionalRsi = 0.13;

    /// <summary>BS EN ISO 6946 conventional external surface resistance, m²K/W.</summary>
    public const double ConventionalRse = 0.04;

    public const string Horizontal = "horizontal";
    public const string Upward = "upward";
    public const string Downward = "downward";

    /// <summary>The conventional internal surface resistance for the heat-flow direction (BS EN ISO 6946: 0.13 horizontal, 0.10 upward, 0.17 downward), m²K/W.</summary>
    public static double ConventionalRsiFor(string? flow) => flow switch { Upward => 0.10, Downward => 0.17, _ => ConventionalRsi };

    private static readonly IReadOnlyList<CalcColumnSpec> LayerColumns =
    [
        new("element", "Element", CalcInputKind.Text),
        new("name", "Layer", CalcInputKind.Text, IsOptional: true),
        new("d", "Thickness", CalcInputKind.Quantity, "mm", Min: 1, Max: 500, HardMin: 1, HardMax: 500),
        new("lambda", "Thermal conductivity λ", CalcInputKind.Quantity, "W/(m*K)", Min: 0.01, Max: 5, HardMin: 0.01, HardMax: 5),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> ElementColumns =
    [
        new("name", "Element", CalcInputKind.Text),
        new("construction", "Layers of (blank = this element's name)", CalcInputKind.Text, IsOptional: true),
        new("flow", "Heat flow direction through the element (blank = horizontal; upward for a roof or ceiling, downward for a floor)", CalcInputKind.Choice, Choices: [Horizontal, Upward, Downward], IsOptional: true),
        new("area", "Area", CalcInputKind.Quantity, "m^2", Min: 0.1, Max: 500, HardMin: 0.1, HardMax: 500),
        new("theta_adj", "Adjacent temperature (blank or θ_e = outside)", CalcInputKind.Quantity, "degC", Min: -20, Max: 35, HardMin: -50, HardMax: 60, IsOptional: true),
        new("f", "Temperature factor f (blank = 1)", CalcInputKind.Number, Min: 0, Max: 1.5, HardMin: 0, HardMax: 3, IsOptional: true),
        new("Rsi", "Rsi (blank = 0.13 horizontal, 0.10 upward, 0.17 downward)", CalcInputKind.Quantity, "m^2*K/W", Min: 0.04, Max: 0.25, HardMin: 0, HardMax: 1, IsOptional: true),
        new("Rse", "Rse (blank = 0.04 outside, the same as Rsi where it faces another space)", CalcInputKind.Quantity, "m^2*K/W", Min: 0.04, Max: 0.25, HardMin: 0, HardMax: 1, IsOptional: true),
        new("Rg", "Air-gap resistance Rg", CalcInputKind.Quantity, "m^2*K/W", Min: 0, Max: 0.6, HardMin: 0, HardMax: 2, IsOptional: true),
        new("U_user", "U override (ground floor, glazing)", CalcInputKind.Quantity, "W/(m^2*K)", Min: 0.05, Max: 8, HardMin: 0.01, HardMax: 10, IsOptional: true),
        new("U_max", "Target U for this element", CalcInputKind.Quantity, "W/(m^2*K)", Min: 0.05, Max: 8, HardMin: 0.01, HardMax: 10, IsOptional: true),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.3.0", CalcDiscipline.BuildingServices, "U-values and room heat loss",
        "U-values of built-up elements from their layers and surface resistances, and the steady-state design heat loss of one room: fabric loss through each element to its adjacent temperature, a thermal-bridge allowance, ventilation loss (through the outside temperature or an MVHR supply temperature), the room load for sizing emitters, the heat loss coefficient and the loss per floor area; optionally checked against target U-values.",
        "Element U-value: U = 1/(R_si + Σ d/λ + R_g + R_se), or the user's U override for ground floors and glazing\n" +
        "Element fabric loss: Φ_i = A·U·f·(θ_int − θ_adj), θ_adj = θ_e where the element faces outside\n" +
        "Thermal bridges: Φ_tb = y_tb·Σ(A·f)_ext·(θ_int − θ_e) on the elements facing outside\n" +
        "Fabric total: Φ_T = ΣΦ_i + Φ_tb\n" +
        "Ventilation flow: q_v = n·V\n" +
        "Ventilation loss: Φ_V = 0.34·q_v·(θ_int − θ_e), or (θ_int − θ_supply) through an MVHR supply, plus 0.34·n_inf·V·(θ_int − θ_e) infiltration with MVHR\n" +
        "Room load: Φ_HL = Φ_T + Φ_V (CIBSE DHDG method: × (1 + additional allowance))\n" +
        "Heat loss coefficient: HLC = Φ_HL/(θ_int − θ_e); loss per floor area Φ_HL/A_floor\n" +
        "U-value check: U against the target U of each element (the global target applies to built-up elements facing outside only; an element's own target applies wherever it faces)",
        [
            new CalcInputSpec("method", "Calculation method (changes the citations; the DHDG option adds the allowance below)", CalcInputKind.Choice, Choices: [En12831, Dhdg], DefaultChoice: En12831, Group: "Method"),
            new CalcInputSpec("theta_int", "Internal design temperature", CalcInputKind.Quantity, "degC", Default: 21, Min: 16, Max: 24, HardMin: 16, HardMax: 24, Group: "Conditions"),
            new CalcInputSpec("theta_e", "External design temperature (location record)", CalcInputKind.Quantity, "degC", Default: -3, Min: -15, Max: 5, HardMin: -15, HardMax: 5, Group: "Conditions"),
            new CalcInputSpec("layers", "Construction layers (several elements share the table, keyed by element name)", CalcInputKind.Table, Columns: LayerColumns, MaxRows: 200, IsOptional: true, Group: "Construction"),
            new CalcInputSpec("elements", "Building elements bounding the room", CalcInputKind.Table, Columns: ElementColumns, MaxRows: 60, Group: "Construction"),
            new CalcInputSpec("y_tb", "Thermal-bridge allowance on the area facing outside", CalcInputKind.Quantity, "W/(m^2*K)", Default: 0, Min: 0, Max: 0.3, HardMin: 0, HardMax: 1, Group: "Construction"),
            new CalcInputSpec("U_target", "Target U for built-up elements without their own target", CalcInputKind.Quantity, "W/(m^2*K)", Min: 0.05, Max: 8, HardMin: 0.01, HardMax: 10, IsOptional: true, Group: "Construction"),
            new CalcInputSpec("n", "Air change rate", CalcInputKind.Quantity, "1/h", Default: 0.5, Min: 0.1, Max: 3, HardMin: 0.1, HardMax: 3, Group: "Ventilation"),
            new CalcInputSpec("V", "Room volume", CalcInputKind.Quantity, "m^3", Default: 40, Min: 5, Max: 1000, HardMin: 1, HardMax: 5000, Group: "Ventilation"),
            new CalcInputSpec("theta_supply", "MVHR supply air temperature after heat recovery (blank = natural ventilation, outside air)", CalcInputKind.Quantity, "degC", Min: -15, Max: 24, HardMin: -20, HardMax: 40, IsOptional: true, Group: "Ventilation"),
            new CalcInputSpec("n_inf", "Infiltration air change rate at outside temperature, with MVHR only (n is then the MVHR rate; blank = no infiltration loss, with a warning; 0 = airtight)", CalcInputKind.Quantity, "1/h", Min: 0, Max: 1, HardMin: 0, HardMax: 3, IsOptional: true, Group: "Ventilation"),
            new CalcInputSpec("A_floor", "Floor area, for the loss per floor area", CalcInputKind.Quantity, "m^2", Min: 1, Max: 200, HardMin: 0.1, HardMax: 2000, IsOptional: true, Group: "Result"),
            new CalcInputSpec("f_add", "Additional allowance on the room load (CIBSE DHDG method only)", CalcInputKind.Quantity, "%", Default: 0, Min: 0, Max: 20, HardMin: 0, HardMax: 50, Group: "Result"),
        ],
        [
            new CalcAssumption("Steady state: every temperature is constant, with no solar or internal gains, no thermal mass and no dynamic simulation.", "Design heat loss, not an annual energy estimate."),
            new CalcAssumption("Layers are homogeneous and in series (one-dimensional flow); no bridged layers (timber studs, mortar joints, fixings) are corrected for: a thermal-bridge allowance y_tb is added instead.", "BS EN ISO 6946 homogeneous-layer calculation."),
            new CalcAssumption("A blank Rsi is the conventional BS EN ISO 6946 value for the element's heat-flow direction (0.13 m²K/W horizontal, 0.10 upward, 0.17 downward) and a blank Rse is 0.04 m²K/W for an element facing outside (no adjacent temperature, or one at or below θ_e), or the same as Rsi where it faces another space.", "Small conventional BS EN ISO 6946 set (three values), not a table of design data."),
            new CalcAssumption("The global target U applies to built-up elements facing outside only: an internal partition or floor has no outside target, so it is checked only against a target entered on its own row.", "Scope of the target check."),
            new CalcAssumption("Ground-floor U-values and glazing U-values are entered by the user (override); the calculator does not model ground heat transfer.", "Stated limit."),
            new CalcAssumption("The air constant 0.34 Wh/(m³·K) is the conventional rounded ρ·c_p of air (about 1.2 kg/m³ × 1005 J/(kg·K) ÷ 3600); it is derived, not read from a table.", "Derived."),
            new CalcAssumption("Design temperatures, conductivities, surface resistances and the thermal-bridge allowance are user records from the user's own licensed copies or manufacturers' data; none is embedded.", "No table from a standard or a manufacturer is shipped."),
        ],
        [
            new CalcClauseRef("BS EN ISO 6946", "homogeneous layers and surface resistances (clause numbers to be checked against the owner's copy)", "Building components — thermal resistance and thermal transmittance"),
            new CalcClauseRef("BS EN 12831-1", "cl. 6.3.2 (clause number to be checked against the owner's copy)", "Heating systems in buildings — design heat load: transmission heat loss"),
            new CalcClauseRef("BS EN 12831-1", "cl. 6.3.3 (clause number to be checked against the owner's copy)", "Heating systems in buildings — design heat load: ventilation heat loss"),
            new CalcClauseRef("CIBSE Domestic Heating Design Guide", "2026 ed., reduced method (name and edition unverified; to be checked against the owner's copy)", "Room heat loss and the additional allowance"),
        ],
        [
            new CalcApplicabilityRule("temperatures", "The internal design temperature must be above the external design temperature.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("elements", "Enter at least one element; names must be unique; each needs layers or a U override.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("layers", "A built-up element needs at least one layer; layers are matched to elements by name.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("supply", "The MVHR supply temperature must not exceed the internal design temperature.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("u-override-layers", "Layers of an element with a U override are ignored.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("orphan-layers", "Layers naming an element that is not in the elements table are ignored.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("f-and-adjacent", "An element with both a temperature factor f below 1 and an adjacent temperature above θ_e takes both reductions; f is for elements facing the outside only.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("f-add-ignored", "The additional allowance applies to the DHDG method only.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("mvhr-no-infiltration", "An MVHR supply temperature is entered and the infiltration rate n_inf is blank: the room has no infiltration loss at the outside temperature.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("n-inf-ignored", "The infiltration rate applies with an MVHR supply temperature only.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("gain", "The room gains heat from warmer neighbours (Φ_HL ≤ 0).", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("supply-below-external", "The MVHR supply is colder than the outside design temperature.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-cavity-wall", "Insulated cavity wall, a window and natural ventilation",
                new CalcReferenceSource("Analytic (series resistance; Φ = A·U·Δθ + 0.34·n·V·Δθ)", null, "R = 0.13 + 0.1025/0.84 + 0.1/0.03 + 0.1/0.2 + 0.013/0.4 + 0.04 = 4.157857 m²K/W, U = 0.240509 W/m²K; wall 20 m² × U × 24 K = 115.444 W; window 3 m² × 1.4 × 24 K = 100.8 W; Φ_T = 216.244 W; q_v = 20 m³/h, Φ_V = 0.34 × 20 × 24 = 163.2 W; Φ_HL = 379.444 W; HLC = 15.8102 W/K; against a target U of 0.30 the wall's utilisation is 0.8017 (the window has a U override and is not checked against the global target). The layer conductivities are invented illustrative inputs, not design data"),
                CavityRoom(),
                [new CalcExpectedValue("R_1", 4.157857, "m^2*K/W", 1e-6), new CalcExpectedValue("U_1", 0.2405085, "W/(m^2*K)", 1e-6), new CalcExpectedValue("u_1", 0.8016950, "", 1e-6), new CalcExpectedValue("Phi_1", 115.44408, "W", 1e-6), new CalcExpectedValue("Phi_2", 100.8, "W", 1e-9), new CalcExpectedValue("Phi_T", 216.2441, "W", 1e-6), new CalcExpectedValue("q_v", 20, "m^3/h", 1e-9), new CalcExpectedValue("Phi_V", 163.2, "W", 1e-9), new CalcExpectedValue("Phi_HL", 379.4441, "W", 1e-6), new CalcExpectedValue("HLC", 15.81017, "W/K", 1e-6)]),
            new CalcTestCase("ka-mvhr-partition", "MVHR supply, an 18 °C partition, a floor with a user U and a thermal-bridge allowance",
                new CalcReferenceSource("Analytic (series resistance; MVHR Φ_V = 0.34·q_v·(θ_int − θ_supply))", null, "Partition: R = 0.13 + 2 × 0.0125/0.22 + 0.18 + 0.13 = 0.553636, U = 1.806240, Φ = 12 × U × 1 × 3 K = 65.025 W; wall 15 m² × 0.240509 × 24 = 86.583 W; floor 10 m² × 0.25 × 0.6 × 24 = 36 W; Φ_tb = 0.05 × (15 + 10 × 0.6) m² (partition excluded, f applied) × 24 = 25.2 W; Φ_T = 212.808 W; q_v = 30 m³/h, Φ_V = 0.34 × 30 × (21 − 16) = 51 W; Φ_HL = 263.808 W; HLC = 10.9920 W/K. Layer conductivities and the floor U are invented illustrative inputs, not design data"),
                MvhrRoom(),
                [new CalcExpectedValue("U_2", 1.806240, "W/(m^2*K)", 1e-6), new CalcExpectedValue("Phi_1", 86.58306, "W", 1e-6), new CalcExpectedValue("Phi_2", 65.02463, "W", 1e-6), new CalcExpectedValue("Phi_3", 36, "W", 1e-9), new CalcExpectedValue("A_tb", 21, "m^2", 1e-9), new CalcExpectedValue("Phi_tb", 25.2, "W", 1e-9), new CalcExpectedValue("Phi_T", 212.8077, "W", 1e-6), new CalcExpectedValue("Phi_V", 51, "W", 1e-9), new CalcExpectedValue("Phi_HL", 263.8077, "W", 1e-6), new CalcExpectedValue("HLC", 10.99199, "W/K", 1e-6)]),
            new CalcTestCase("ka-dhdg-allowance", "CIBSE DHDG reduced method with a 10 % additional allowance",
                new CalcReferenceSource("Analytic (the first case with Φ_HL × 1.10)", null, "Φ_HL = 379.444 × 1.10 = 417.389 W; HLC = 417.389/24 = 17.3912 W/K; the elemental arithmetic is unchanged. Layer conductivities are invented illustrative inputs, not design data"),
                [.. CavityRoom(), CalcInputValue.Choice("method", Dhdg), CalcInputValue.Quantity("f_add", 10, "%")],
                [new CalcExpectedValue("Phi_T", 216.2441, "W", 1e-6), new CalcExpectedValue("Phi_V", 163.2, "W", 1e-9), new CalcExpectedValue("Phi_HL", 417.3885, "W", 1e-6), new CalcExpectedValue("HLC", 17.39119, "W/K", 1e-6)]),
            new CalcTestCase("ka-single-layer-target", "One 100 mm layer against a target U",
                new CalcReferenceSource("Analytic (series resistance)", null, "R = 0.13 + 0.1/1.4 + 0.04 = 0.241429 m²K/W, U = 4.142012 W/m²K; against a target of 4.5 the utilisation is 0.920447; Φ = 10 × U × 24 = 994.083 W. The conductivity 1.4 is an invented illustrative input, not design data"),
                [Layers(("Slab", "Concrete", 100, 1.4)), Elements(("Slab", 10, null, null, null, null, null, null, null)), CalcInputValue.Quantity("U_target", 4.5, "W/(m^2*K)"), CalcInputValue.Quantity("n", 0.1, "1/h"), CalcInputValue.Quantity("V", 5, "m^3")],
                [new CalcExpectedValue("U_1", 4.142012, "W/(m^2*K)", 1e-6), new CalcExpectedValue("u_1", 0.9204471, "", 1e-6), new CalcExpectedValue("Phi_1", 994.0828, "W", 1e-6), new CalcExpectedValue("Phi_V", 4.08, "W", 1e-9)]),
            new CalcTestCase("ka-mvhr-infiltration", "MVHR supply with a separate infiltration rate",
                new CalcReferenceSource("Analytic (Φ_V = 0.34·[n·V·(θ_int − θ_supply) + n_inf·V·(θ_int − θ_e)])", null, "As the MVHR case with n_inf = 0.1 h⁻¹: MVHR flow 30 m³/h × 5 K and infiltration 5 m³/h × 24 K, Φ_V = 0.34 × (150 + 120) = 91.8 W; Φ_T = 212.808 W; Φ_HL = 304.608 W; HLC = 12.6920 W/K. Layer conductivities are invented illustrative inputs, not design data"),
                [.. MvhrRoom(), CalcInputValue.Quantity("n_inf", 0.1, "1/h")],
                [new CalcExpectedValue("Phi_V", 91.8, "W", 1e-9), new CalcExpectedValue("Phi_T", 212.8077, "W", 1e-6), new CalcExpectedValue("Phi_HL", 304.6077, "W", 1e-6), new CalcExpectedValue("HLC", 12.69199, "W/K", 1e-6)]),
            new CalcTestCase("ka-roof-upward", "A roof: upward heat flow takes R_si = 0.10, and a heated neighbour's partition is not held to the outside target",
                new CalcReferenceSource("Analytic (series resistance; BS EN ISO 6946 conventional surface resistances by flow direction)", null, "Roof, 100 mm of λ = 0.05 (invented): R = 0.10 + 0.1/0.05 + 0.04 = 2.14 m²K/W, U = 0.467290 W/m²K; Φ = 10 × U × 24 K = 112.150 W; a 12 m² partition to a 18 °C neighbour, U = 1/(0.13 + 0.1/0.5 + 0.13) = 2.173913, is checked only against its own target"),
                [Layers(("Roof", "Insulation", 100, 0.05), ("Partition", "Board", 100, 0.5)), RoofAndPartition(), CalcInputValue.Quantity("U_target", 0.5, "W/(m^2*K)"), CalcInputValue.Quantity("n", 0.1, "1/h"), CalcInputValue.Quantity("V", 5, "m^3")],
                [new CalcExpectedValue("R_1", 2.14, "m^2*K/W", 1e-9), new CalcExpectedValue("U_1", 0.4672897, "W/(m^2*K)", 1e-6), new CalcExpectedValue("u_1", 0.9345794, "", 1e-6), new CalcExpectedValue("Phi_1", 112.1495, "W", 1e-6), new CalcExpectedValue("U_2", 2.173913, "W/(m^2*K)", 1e-6)]),
        ],
        Category: "Thermal",
        OutputPorts:
        [
            new CalcOutputPort("Phi_HL", "W", "Room design heat loss (→ B6 room heat required)"),
            new CalcOutputPort("HLC", "W/K", "Heat loss coefficient"),
            new CalcOutputPort("Phi_T", "W", "Fabric heat loss including the thermal-bridge allowance"),
            new CalcOutputPort("Phi_V", "W", "Ventilation heat loss"),
            new CalcOutputPort("U_1", "W/(m^2*K)", "U-value of the first element in the elements table (→ B7 wall U-value cross-check)"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("climate.design-temperature", "External design temperature", ["theta_e"], "The location's external design temperature from the user's own licensed or published source, with its edition."),
            new CalcRecordKind("material.conductivity", "Layer thermal conductivity", ["layers"], "The λ column of the layers table: manufacturer's declared value or the user's own licensed reference."),
            new CalcRecordKind("thermal.bridging", "Thermal-bridge allowance", ["y_tb"], "From the user's own source or a bridging calculation for the construction."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static readonly (string, string, double, double)[] CavityLayers =
    [
        ("Cavity wall", "Outer brick leaf", 102.5, 0.84),
        ("Cavity wall", "Insulation", 100, 0.03),
        ("Cavity wall", "Inner block leaf", 100, 0.2),
        ("Cavity wall", "Plaster", 13, 0.4),
    ];

    /// <summary>A layers table from (element, layer, d mm, λ W/(m·K)) rows.</summary>
    public static CalcInputValue Layers(params (string Element, string Name, double D, double Lambda)[] rows) =>
        CalcInputValue.Table("layers", rows.Select(r => CalcRow.Of(("element", new CalcCell(Text: r.Element)), ("name", new CalcCell(Text: r.Name)), ("d", new CalcCell(r.D, "mm")), ("lambda", new CalcCell(r.Lambda, "W/(m*K)")))).ToList());

    /// <summary>
    /// An elements table from (name, area m², adjacent °C, f, Rsi, Rse, Rg, U override, target U) rows; a null leaves the cell blank.
    /// </summary>
    public static CalcInputValue Elements(params (string Name, double Area, double? ThetaAdj, double? F, double? Rsi, double? Rse, double? Rg, double? UUser, double? UMax)[] rows) =>
        CalcInputValue.Table("elements", rows.Select(r =>
        {
            var cells = new List<(string, CalcCell)> { ("name", new CalcCell(Text: r.Name)), ("area", new CalcCell(r.Area, "m^2")) };

            if (r.ThetaAdj is { } t)
                cells.Add(("theta_adj", new CalcCell(t, "degC")));

            if (r.F is { } f)
                cells.Add(("f", new CalcCell(f)));

            if (r.Rsi is { } rsi)
                cells.Add(("Rsi", new CalcCell(rsi, "m^2*K/W")));

            if (r.Rse is { } rse)
                cells.Add(("Rse", new CalcCell(rse, "m^2*K/W")));

            if (r.Rg is { } rg)
                cells.Add(("Rg", new CalcCell(rg, "m^2*K/W")));

            if (r.UUser is { } u)
                cells.Add(("U_user", new CalcCell(u, "W/(m^2*K)")));

            if (r.UMax is { } m)
                cells.Add(("U_max", new CalcCell(m, "W/(m^2*K)")));

            return CalcRow.Of([.. cells]);
        }).ToList());

    /// <summary>The elements table with the layers key (construction) set per row; a null leaves a row keyed by its own name.</summary>
    public static CalcInputValue WithConstructions(CalcInputValue elements, params string?[] constructions) =>
        elements with
        {
            Rows = [.. elements.Rows!.Select((row, i) => i < constructions.Length && constructions[i] is { } text
                ? new CalcRow(new Dictionary<string, CalcCell>(row.Cells, StringComparer.Ordinal) { ["construction"] = new CalcCell(Text: text) })
                : row)],
        };

    private static CalcInputValue[] CavityRoom() =>
    [
        Q("theta_int", 21, "degC"), Q("theta_e", -3, "degC"),
        Layers(CavityLayers),
        Elements(("Cavity wall", 20, null, null, null, null, null, null, null), ("Window", 3, null, null, null, null, null, 1.4, null)),
        Q("U_target", 0.3, "W/(m^2*K)"), Q("n", 0.5, "1/h"), Q("V", 40, "m^3"),
    ];

    /// <summary>A roof (upward heat flow) and a partition to an 18 °C neighbour; the layers are supplied by the caller.</summary>
    private static CalcInputValue RoofAndPartition() =>
        CalcInputValue.Table("elements",
        [
            CalcRow.Of(("name", new CalcCell(Text: "Roof")), ("flow", new CalcCell(Text: Upward)), ("area", new CalcCell(10, "m^2"))),
            CalcRow.Of(("name", new CalcCell(Text: "Partition")), ("area", new CalcCell(12, "m^2")), ("theta_adj", new CalcCell(18, "degC"))),
        ]);

    private static CalcInputValue[] MvhrRoom() =>
    [
        Q("theta_int", 21, "degC"), Q("theta_e", -3, "degC"),
        Layers(CavityLayers.Concat([("Partition", "Plasterboard", 12.5, 0.22), ("Partition", "Plasterboard", 12.5, 0.22)]).ToArray()),
        Elements(("Cavity wall", 15, null, null, null, null, null, null, null), ("Partition", 12, 18, null, null, null, 0.18, null, null), ("Ground floor", 10, null, 0.6, null, null, null, 0.25, null)),
        Q("y_tb", 0.05, "W/(m^2*K)"), Q("n", 0.6, "1/h"), Q("V", 50, "m^3"), Q("theta_supply", 16, "degC"),
    ];

    private sealed record Element(string Name, double Area, bool External, double ThetaAdj, double F, double U, double? Target, bool BuiltUp);

    public void Run(CalcRunContext c)
    {
        foreach (var s in new[] { "theta_int", "theta_e", "n", "V", "y_tb" })
            c.Input(s);

        var dhdg = c.InputText("method") == Dhdg;
        var tClause = dhdg ? "CIBSE DHDG (2026 ed.), reduced method" : "BS EN 12831-1 cl. 6.3.2";
        var vClause = dhdg ? "CIBSE DHDG (2026 ed.), reduced method" : "BS EN 12831-1 cl. 6.3.3";

        if (!(c.Si("theta_int") > c.Si("theta_e")))
        {
            c.Block("temperatures", "theta_e: the external design temperature must be below the internal design temperature.");
            return;
        }

        var mvhr = c.Has("theta_supply");

        if (mvhr)
            c.Input("theta_supply");

        if (mvhr && c.Si("theta_supply") > c.Si("theta_int") + 1e-9)
        {
            c.Block("supply", "theta_supply: the MVHR supply temperature must not exceed the internal design temperature.");
            return;
        }

        var rows = c.Rows("elements");

        if (rows.Count == 0)
        {
            c.Block("elements", "elements: enter at least one building element (walls, windows, floor, roof): its name, its area and, for an element facing another space, the adjacent temperature; then give each built-up element its layers or a U override.");
            return;
        }

        var layerRows = c.Rows("layers");
        var theta = c.Si("theta_int");
        var thetaE = c.Si("theta_e");
        var m2kw = UnitCatalog.Parse("m^2*K/W");
        var uUnit = UnitCatalog.Parse("W/(m^2*K)");
        var w = UnitCatalog.Parse("W");
        var m2 = UnitCatalog.Parse("m^2");
        var wk = UnitCatalog.Parse("W/K");
        var names = new List<string>();

        for (var i = 0; i < rows.Count; i++)
        {
            var name = ElementName(rows[i], i);

            if (names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                c.Block("elements", $"elements: the name '{name}' is used twice; layers are matched to elements by name, so each name must be unique.");
                return;
            }

            names.Add(name);
        }

        var used = new HashSet<int>();
        var defaulted = new List<string>();
        var elements = new List<Element>();
        var outputs = new List<(string, string)>();
        var hasTarget = c.Has("U_target");
        var globalTarget = hasTarget ? c.InputSi("U_target") : 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var name = names[i];
            var no = i + 1;
            var area = Si(row, "area", m2) ?? 0;
            var adjEntered = Si(row, "theta_adj", UnitCatalog.Parse("degC"));
            var external = adjEntered is null || adjEntered <= thetaE + 1e-9;
            var adj = adjEntered ?? thetaE;
            var f = row.Get("f")?.Number ?? 1.0;

            if (f < 1 - 1e-9 && adjEntered is { } adjacent && adjacent > thetaE + 1e-9)
                c.Warn("f-and-adjacent", $"elements: {name} has both a temperature factor f = {F(f)} and an adjacent temperature of {F(adjacent - 273.15)} °C, so the temperature reduction is counted twice (θ_int − θ_adj and f). Use f for elements facing the outside only (a floor to ground, say) and the adjacent temperature for a neighbouring space; leave one of them blank.");
            var user = Si(row, "U_user", uUnit);
            var flow = row.Get("flow")?.Text is { Length: > 0 } flowText ? Clean(flowText) : Horizontal;
            var mine = new List<(string Layer, double D, double Lambda)>();
            var key = Clean(row.Get("construction")?.Text) is { Length: > 0 } shared ? shared : name;

            for (var j = 0; j < layerRows.Count; j++)
            {
                if (!string.Equals(Clean(layerRows[j].Get("element")?.Text), key, StringComparison.OrdinalIgnoreCase))
                    continue;

                used.Add(j);

                if (Si(layerRows[j], "d", UnitCatalog.Parse("mm")) is { } d && Si(layerRows[j], "lambda", UnitCatalog.Parse("W/(m*K)")) is { } lambda)
                    mine.Add((Clean(layerRows[j].Get("name")?.Text) is { Length: > 0 } ln ? ln : $"layer {mine.Count + 1}", d, lambda));
            }

            double u;
            var builtUp = user is null;

            if (user is { } given)
            {
                u = given;

                if (mine.Count > 0)
                    c.Warn("u-override-layers", $"layers: element '{name}' has a U override, so its {mine.Count} layer(s) are ignored.");

                c.Record($"U_{no}", "U override (user-entered)", $"{name}: {F(u)} W/m²K", ScalarValue.Of(u, uUnit), $"{name}: U-value as entered (ground floor or glazing)");
            }
            else
            {
                if (mine.Count == 0)
                {
                    c.Block("layers", $"layers: element '{name}' has no layers and no U override; add its layers (matched by element name) or enter a U-value.");
                    return;
                }

                var rsi = Si(row, "Rsi", m2kw);
                var rse = Si(row, "Rse", m2kw);

                if (rsi is null || rse is null)
                    defaulted.Add(name);

                var rsiValue = rsi ?? ConventionalRsiFor(flow);
                var rseValue = rse ?? (external ? ConventionalRse : ConventionalRsiFor(flow));
                var rg = Si(row, "Rg", m2kw) ?? 0;
                var parts = mine.Select(l => l.D / l.Lambda).ToList();
                var r = rsiValue + parts.Sum() + rg + rseValue;

                if (!(r > 0) || !double.IsFinite(r))
                {
                    c.Block("layers", $"layers: element '{name}' has no usable thermal resistance.");
                    return;
                }

                u = 1 / r;
                var list = parts.Count > 6 ? string.Join(" + ", parts.Take(6).Select(F)) + " + …" : string.Join(" + ", parts.Select(F));
                c.Record($"R_{no}", "R_si + Σ d/λ + R_g + R_se", $"{name}: {F(rsiValue)} + ({list}) + {F(rg)} + {F(rseValue)}", ScalarValue.Of(r, m2kw), $"{name}: total thermal resistance", "BS EN ISO 6946");
                c.Record($"U_{no}", "1/R", $"1 / {F(r)}", ScalarValue.Of(u, uUnit), $"{name}: U-value", "BS EN ISO 6946");
            }

            var phi = area * u * f * (theta - adj);
            var adjText = adjEntered is null ? $"θ_e = {F(thetaE - 273.15)} °C" : $"θ_adj = {F(adj - 273.15)} °C";
            c.Record($"Phi_{no}", "A·U·f·(θ_int − θ_adj)", $"{F(area)} × {F(u)} × {F(f)} × ({F(theta - 273.15)} − {F(adj - 273.15)}) °C", ScalarValue.Of(phi, w), $"{name}: fabric loss to {adjText}", tClause);

            if (builtUp)
                outputs.Add(($"R_{no}", $"{name}: thermal resistance"));

            outputs.Add(($"U_{no}", $"{name}: U-value"));
            outputs.Add(($"Phi_{no}", $"{name}: fabric loss"));

            // The global target is an outside-facing target: a partition to a heated neighbour is checked only against its own (Black B-A-04).
            var target = Si(row, "U_max", uUnit) ?? (builtUp && hasTarget && external ? globalTarget : (double?)null);
            elements.Add(new Element(name, area, external, adj, f, u, target, builtUp));
        }

        var orphans = layerRows.Count - used.Count;

        if (orphans > 0)
            c.Warn("orphan-layers", $"layers: {orphans} layer row(s) name an element that is not in the elements table and are ignored.");

        if (defaulted.Count > 0)
            c.Assume($"Conventional BS EN ISO 6946 surface resistances (R_si 0.13 horizontal, 0.10 upward, 0.17 downward; R_se 0.04 outside, or the same as R_si where the element faces another space; m²K/W) are used where blank, for: {string.Join(", ", defaulted)}.", "Small conventional set; enter your own where the construction needs other values.");

        var sumPhi = elements.Sum(e => e.Area * e.U * e.F * (theta - e.ThetaAdj));
        var areaAll = elements.Sum(e => e.Area);
        var areaExt = elements.Where(e => e.External).Sum(e => e.Area);
        var areaTb = elements.Where(e => e.External).Sum(e => e.Area * e.F);
        var hT = elements.Sum(e => e.Area * e.U * e.F);
        var hU = elements.Sum(e => e.Area * e.U);

        c.Record("A_fab", "Σ A", $"{F(areaAll)} m²", ScalarValue.Of(areaAll, m2), "Total fabric area");
        c.Record("A_ext", "Σ A facing outside", $"{F(areaExt)} m²", ScalarValue.Of(areaExt, m2), "Fabric area facing outside");
        c.Record("A_tb", "Σ A·f facing outside", $"{F(areaTb)} m²", ScalarValue.Of(areaTb, m2), "Fabric area facing outside weighted by the temperature factor (thermal-bridge allowance applies)", tClause);
        c.Record("U_m", "Σ A·U / Σ A", $"{F(hU)} / {F(areaAll)}", ScalarValue.Of(hU / areaAll, uUnit), "Area-weighted mean U-value of the fabric (without the temperature factor f)");
        c.Record("U_eff", "Σ A·U·f / Σ A", $"{F(hT)} / {F(areaAll)}", ScalarValue.Of(hT / areaAll, uUnit), "Heat-loss weighted mean U-value of the fabric (includes the temperature factor f)");
        c.Assign("dT", "theta_int - theta_e", "K", "Design temperature difference, inside to outside");
        c.Record("Phi_el", "Σ Φ_i", $"{F(sumPhi)} W", ScalarValue.Of(sumPhi, w), "Fabric loss through the elements", tClause);
        c.Assign("Phi_tb", "y_tb * A_tb * dT", "W", "Thermal-bridge allowance", tClause);
        c.Assign("Phi_T", "Phi_el + Phi_tb", "W", "Fabric heat loss", tClause);

        c.Assign("q_v", "n * V", "m^3/h", "Ventilation air flow");

        if (mvhr)
        {
            if (c.Si("theta_supply") < thetaE - 1e-9)
                c.Warn("supply-below-external", "theta_supply: the MVHR supply temperature is below the external design temperature; check the heat-recovery figure.");

            c.Assign("dT_v", "theta_int - theta_supply", "K", "Ventilation temperature difference, inside to MVHR supply", "BS EN 12831-1 cl. 6.3.3");
        }
        else
        {
            c.Assign("dT_v", "dT", "K", "Ventilation temperature difference, inside to outside");
        }

        var ninf = c.Has("n_inf") ? c.InputSi("n_inf") : 0.0;

        // bs.room-heat-loss-R1: with MVHR the air change rate n is the MVHR rate only; a room still leaks air at the outside temperature.
        if (mvhr && !c.Has("n_inf"))
            c.Warn("mvhr-no-infiltration", "n_inf: an MVHR supply temperature is entered but the infiltration rate is blank, so the room has no infiltration loss: n is the MVHR rate only, and a room with MVHR still leaks air at the outside temperature. Enter n_inf (0 if the envelope is taken as airtight).");

        if (mvhr && ninf > 0)
        {
            c.Assign("q_inf", "n_inf * V", "m^3/h", "Infiltration air flow at the outside temperature (not through the MVHR)");
            c.Assign("Phi_V", "0.34 Wh/(m^3*K) * (q_v * dT_v + q_inf * dT)", "W", "Ventilation heat loss: MVHR flow to the supply temperature plus infiltration to outside (0.34 Wh/m³K = conventional ρ·c_p of air, derived)", vClause);
        }
        else
        {
            if (!mvhr && ninf > 0)
                c.Warn("n-inf-ignored", "n_inf: the infiltration rate applies with an MVHR supply temperature only; with natural ventilation n is the whole air change rate, so n_inf is ignored.");

            c.Assign("Phi_V", "0.34 Wh/(m^3*K) * q_v * dT_v", "W", "Ventilation heat loss (0.34 Wh/m³K = conventional ρ·c_p of air, derived)", vClause);
        }

        if (dhdg)
        {
            c.Input("f_add");
            c.Assign("Phi_HL", "(Phi_T + Phi_V) * (1 + f_add)", "W", "Room design heat loss with the additional allowance", vClause);
        }
        else
        {
            if (c.Has("f_add") && c.InputSi("f_add") > 0)
                c.Warn("f-add-ignored", "f_add: the additional allowance applies to the CIBSE DHDG method only and is ignored for BS EN 12831-1.");

            c.Assign("Phi_HL", "Phi_T + Phi_V", "W", "Room design heat loss", "BS EN 12831-1 cl. 6.3.2");
        }

        c.Assign("HLC", "Phi_HL / dT", "W/K", "Heat loss coefficient");

        if (c.Si("Phi_HL") <= 0)
            c.Warn("gain", "Phi_HL: the room gains heat from warmer neighbours; Φ_HL ≤ 0, so no emitter is needed for design heating.");

        var hasFloor = c.Has("A_floor");

        if (hasFloor)
        {
            c.Input("A_floor");
            c.Assign("Phi_A", "Phi_HL / A_floor", "W/m^2", "Room design heat loss per floor area");
        }

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        foreach (var (symbol, text) in new[] { ("A_fab", "Total fabric area"), ("A_ext", "Fabric area facing outside"), ("A_tb", "Fabric area facing outside weighted by f"), ("U_m", "Mean fabric U-value (area-weighted)"), ("U_eff", "Heat-loss weighted fabric U-value (with f)"), ("dT", "Design temperature difference"), ("Phi_el", "Element fabric loss"), ("Phi_tb", "Thermal-bridge allowance"), ("Phi_T", "Fabric heat loss"), ("q_v", "Ventilation air flow"), ("Phi_V", "Ventilation heat loss"), ("Phi_HL", "Room design heat loss"), ("HLC", "Heat loss coefficient") })
            c.Output(symbol, text);

        if (hasFloor)
            c.Output("Phi_A", "Heat loss per floor area");

        for (var i = 0; i < elements.Count; i++)
        {
            var e = elements[i];

            if (e.Target is not { } target)
                continue;

            var no = i + 1;
            c.Record($"u_{no}", "U / U_target", $"{F(e.U)} / {F(target)}", new ScalarValue(e.U / target), $"{e.Name}: utilisation of the target U-value");
            c.Output($"u_{no}", $"{e.Name}: U against target");
            c.CheckUtilisation($"U-value of {e.Name} against its target ({F(target)} W/m²K)", $"u_{no}");
        }
    }

    private static string ElementName(CalcRow row, int index) => Clean(row.Get("name")?.Text) is { Length: > 0 } text ? text : $"Element {index + 1}";

    private static string Clean(string? text) => CalcSheetTextRenderer.Clean(text ?? string.Empty).Trim();

    private static double? Si(CalcRow row, string key, Unit unit) => row.Get(key) is { Number: { } n } cell ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : unit).ToSi(n) : null;

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
