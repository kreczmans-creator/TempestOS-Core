using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Structural;

/// <summary>
/// S4 — EN 1993-1-8 bolts (category A shear, category D tension) and fillet welds: shear, bearing, tension, punching,
/// combined shear and tension, block tearing, the simplified and directional weld methods, and the spacing limits.
/// Bolt strengths come from the property class (f_ub = first figure × 100, f_yb = f_ub × second figure/10); stress
/// areas from the ISO 898-1 formula with ISO 261 coarse pitches; nothing is read from a resistance table.
/// </summary>
public sealed class ConnectionCalculator : ICalcCalculator
{
    public const string Id = "struct.connection-ec3";
    public const string HoleNormal = "Normal clearance";
    public const string HoleOversize = "Oversize";
    public const string HoleSlot = "Slot across the load";
    public const string BlockConcentric = "Concentric loading (V_eff,1)";
    public const string BlockEccentric = "Eccentric loading (V_eff,2)";

    /// <summary>Metric coarse sizes M12–M36: (d, pitch, nut across-flats) in mm, ISO 261 / ISO 4032.</summary>
    public static IReadOnlyList<(string Name, double D, double Pitch, double AcrossFlats)> Sizes { get; } =
    [
        ("M12", 12, 1.75, 18), ("M16", 16, 2, 24), ("M20", 20, 2.5, 30), ("M22", 22, 2.5, 34),
        ("M24", 24, 3, 36), ("M27", 27, 3, 41), ("M30", 30, 3.5, 46), ("M36", 36, 4, 55),
    ];

    public static IReadOnlyList<string> Grades { get; } = ["4.6", "4.8", "5.6", "5.8", "6.8", "8.8", "10.9"];

    /// <summary>Steel grades with the weld correlation factor β_w (EN 1993-1-8 Table 4.1, a five-value factor set).</summary>
    public static IReadOnlyList<(string Grade, double BetaW)> Steels { get; } = [("S235", 0.8), ("S275", 0.85), ("S355", 0.9), ("S420", 1.0), ("S460", 1.0)];

    public CalcTemplate Template { get; } = new(
        Id, "1.8.0", CalcDiscipline.Structural, "Bolts and fillet welds (EN 1993-1-8)",
        "Calculates the design resistance of one bolt in a plate, block tearing of the bolt group, the spacing limits, and a fillet weld by the simplified and directional methods, each against its design force. Loading is static.",
        "Bolt strengths: f_ub = class × 100 MPa; A_s = π/4·((d₂ + d₃)/2)², d₂ = d − 0.6495P, d₃ = d − 1.2269P\n" +
        "Shear (cl. 3.6, Table 3.4): F_v,Rd = α_v·f_ub·A·n_s/γ_M2; α_v = 0.6 (4.6, 5.6, 8.8 or shank in the plane), 0.5 (4.8, 5.8, 6.8, 10.9 with threads in the plane)\n" +
        "Bolt bearing resistance: F_b,Rd = k_h·min(k₁·α_b·f_u·d·t/γ_M2, [single lap, one bolt row: 1.5·f_u·d·t/γ_M2]); α_b = min(α_d, f_ub/f_u, 1), α_d = e₁/3d₀ (end) or p₁/3d₀ − 1/4 (inner); k₁ = min(2.8e₂/d₀ − 1.7, 1.4p₂/d₀ − 1.7, 2.5); k_h = 1 (normal holes), 0.8 (oversize), 0.6 (slot across the load)\n" +
        "Bolt tension resistance: F_t,Rd = k₂·f_ub·A_s/γ_M2, k₂ = 0.9; punching B_p,Rd = 0.6π·d_m·t·f_u/γ_M2, d_m = (s + e)/2 from the across-flats s and the across-corners e of the smaller of head and nut, or d_m = s when e is not entered (conservative); combined F_v/F_v,Rd + F_t/(1.4F_t,Rd) ≤ 1\n" +
        "Block tearing (cl. 3.10.2): V_eff,1,Rd = f_u·A_nt/γ_M2 + f_y·A_nv/(√3·γ_M0) for a group loaded concentrically; V_eff,2,Rd = 0.5·f_u·A_nt/γ_M2 + f_y·A_nv/(√3·γ_M0) for an eccentrically loaded group\n" +
        "Weld, simplified (cl. 4.5.3.3): f_vw,d = f_u/(√3·β_w·γ_M2), F_w,Rd = f_vw,d·a; directional (cl. 4.5.3.2): √(σ⊥² + 3(τ⊥² + τ∥²)) ≤ f_u/(β_w·γ_M2), σ⊥ ≤ 0.9f_u/γ_M2",
        [
            new CalcInputSpec("grade", "Bolt property class", CalcInputKind.Choice, Choices: Grades, DefaultChoice: "8.8", Group: "Setup"),
            new CalcInputSpec("size", "Bolt size", CalcInputKind.Choice, Choices: [.. Sizes.Select(s => s.Name)], DefaultChoice: "M20", Group: "Setup"),
            new CalcInputSpec("steel", "Plate steel grade (β_w)", CalcInputKind.Choice, Choices: [.. Steels.Select(s => s.Grade)], DefaultChoice: "S275", Group: "Setup"),
            new CalcInputSpec("hole_type", "Hole type: oversize holes reduce the bearing resistance by 0.8, a slot across the load by 0.6 (a d₀ above the normal clearance is treated as oversize)", CalcInputKind.Choice, Choices: [HoleNormal, HoleOversize, HoleSlot], DefaultChoice: HoleNormal, Group: "Setup"),
            new CalcInputSpec("block_load", "Block tearing loading: concentric (V_eff,1) or eccentric (V_eff,2, the tension term halved)", CalcInputKind.Choice, Choices: [BlockConcentric, BlockEccentric], DefaultChoice: BlockConcentric, Group: "Setup"),
            new CalcInputSpec("single_lap", "Single-lap joint with only one bolt row (limits the bearing resistance to 1.5·f_u·d·t/γ_M2, cl. 3.6.1)", CalcInputKind.Boolean, Default: 0, Group: "Setup"),
            new CalcInputSpec("threads", "Threads in the shear plane", CalcInputKind.Boolean, Default: 1, Group: "Setup"),
            new CalcInputSpec("n_s", "Shear planes", CalcInputKind.Number, Default: 1, Min: 1, Max: 2, HardMin: 1, HardMax: 4, Group: "Setup"),
            new CalcInputSpec("t_p", "Plate thickness", CalcInputKind.Quantity, "mm", Default: 10, Min: 4, Max: 40, HardMin: 3, HardMax: 50, Group: "Geometry", Aliases: ["t"]),
            new CalcInputSpec("e_1", "End distance (load direction)", CalcInputKind.Quantity, "mm", Default: 40, Min: 15, Max: 250, HardMin: 10, HardMax: 500, Group: "Geometry"),
            new CalcInputSpec("e_2", "Edge distance (across the load)", CalcInputKind.Quantity, "mm", Default: 40, Min: 15, Max: 250, HardMin: 10, HardMax: 500, Group: "Geometry"),
            new CalcInputSpec("d_0", "Hole diameter (optional; blank = normal clearance d + 2, or d + 3 from M27)", CalcInputKind.Quantity, "mm", Min: 14, Max: 40, HardMin: 13, HardMax: 40, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("t_2", "Thickness of the other ply, such as the column flange behind an end plate (optional; blank = one ply only)", CalcInputKind.Quantity, "mm", Min: 4, Max: 40, HardMin: 3, HardMax: 50, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("p_1", "Pitch in the load direction (optional; blank = one row)", CalcInputKind.Quantity, "mm", Min: 30, Max: 300, HardMin: 20, HardMax: 1000, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("p_2", "Gauge across the load (optional; blank = one line)", CalcInputKind.Quantity, "mm", Min: 30, Max: 300, HardMin: 20, HardMax: 1000, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("e_head", "Across-corners dimension of the smaller of bolt head and nut (optional; blank = punching takes the across-flats dimension, which is conservative)", CalcInputKind.Quantity, "mm", Min: 18, Max: 70, HardMin: 13, HardMax: 70, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("A_nt", "Net area in tension (optional; blank = no block tearing check)", CalcInputKind.Quantity, "mm^2", Min: 10, Max: 5e4, HardMin: 1, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("A_nv", "Net area in shear (optional; blank = no block tearing check)", CalcInputKind.Quantity, "mm^2", Min: 10, Max: 5e4, HardMin: 1, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("a", "Weld throat (optional; blank = no weld check)", CalcInputKind.Quantity, "mm", Min: 3, Max: 15, HardMin: 3, HardMax: 25, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("f_y", "Plate yield strength", CalcInputKind.Quantity, "MPa", Default: 275, Min: 235, Max: 440, HardMin: 235, HardMax: 460, Group: "Material"),
            new CalcInputSpec("f_u", "Plate ultimate tensile strength", CalcInputKind.Quantity, "MPa", Default: 410, Min: 340, Max: 700, HardMin: 340, HardMax: 720, Group: "Material"),
            new CalcInputSpec("F_v_Ed", "Shear per bolt", CalcInputKind.Quantity, "kN", Default: 50, Min: 0, Max: 1000, HardMin: 0, HardMax: 2000, Group: "Loads"),
            new CalcInputSpec("F_t_Ed", "Tension per bolt", CalcInputKind.Quantity, "kN", Default: 0, Min: 0, Max: 1000, HardMin: 0, HardMax: 2000, Group: "Loads"),
            new CalcInputSpec("V_Ed", "Group force for block tearing (optional; blank = no block tearing check)", CalcInputKind.Quantity, "kN", Min: 0, Max: 5000, HardMin: 0, HardMax: 1e4, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("F_w_Ed", "Weld force per unit length (optional; blank = no weld check)", CalcInputKind.Quantity, "kN/m", Min: 0, Max: 5000, HardMin: 0, HardMax: 1e4, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("sigma_perp", "Directional method: normal stress perpendicular to the throat (optional; blank = no directional check)", CalcInputKind.Quantity, "MPa", Min: -500, Max: 500, HardMin: -1000, HardMax: 1000, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("tau_perp", "Directional method: shear perpendicular to the weld axis (optional; blank = no directional check)", CalcInputKind.Quantity, "MPa", Min: -500, Max: 500, HardMin: -1000, HardMax: 1000, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("tau_par", "Directional method: shear parallel to the weld axis (optional; blank = no directional check)", CalcInputKind.Quantity, "MPa", Min: -500, Max: 500, HardMin: -1000, HardMax: 1000, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("gamma_M2", "Partial factor for bolt and weld resistance", CalcInputKind.Number, Default: 1.25, Min: 1.1, Max: 1.4, HardMin: 1.1, HardMax: 1.5, Group: "Factors"),
            new CalcInputSpec("gamma_M0", "Partial factor for cross-section resistance", CalcInputKind.Number, Default: 1.0, Min: 1, Max: 1.1, HardMin: 1, HardMax: 1.2, Group: "Factors"),
            new CalcInputSpec("k_e1", "Minimum e₁/d₀", CalcInputKind.Number, Default: 1.2, Min: 1, Max: 2.5, HardMin: 1, HardMax: 3, Group: "Requirements"),
            new CalcInputSpec("k_e2", "Minimum e₂/d₀", CalcInputKind.Number, Default: 1.2, Min: 1, Max: 2.5, HardMin: 1, HardMax: 3, Group: "Requirements"),
            new CalcInputSpec("k_p1", "Minimum p₁/d₀", CalcInputKind.Number, Default: 2.2, Min: 2, Max: 3.5, HardMin: 2, HardMax: 4, Group: "Requirements"),
            new CalcInputSpec("k_p2", "Minimum p₂/d₀", CalcInputKind.Number, Default: 2.4, Min: 2, Max: 3.5, HardMin: 2, HardMax: 4, Group: "Requirements"),
        ],
        [
            new CalcAssumption("Loading is static.", "Scope of this sheet."),
            new CalcAssumption("Bolts are category A in shear (bearing type) and category D in tension (non-preloaded).", "Scope of this sheet."),
            new CalcAssumption("The joint has no slip-resistant bolts and no moment end plates.", "Scope of this sheet."),
            new CalcAssumption("Bearing is taken at the worst of an end bolt and an inner bolt.", "Conservative for every bolt in the group."),
            new CalcAssumption("Bearing across the load is taken at an edge bolt, with k₁ from e₂ and p₂.", "Conservative for every bolt in the group."),
            new CalcAssumption("Oversize holes reduce the bearing resistance by 0.8, and a slot across the load by 0.6.", "BS EN 1993-1-8:2005 cl. 3.6.1, Table 3.4."),
            new CalcAssumption("A hole above the normal clearance, with the hole type left at normal, is treated as oversize.", "BS EN 1993-1-8:2005 cl. 3.6.1, Table 3.4."),
            new CalcAssumption("The single-lap cap on the bearing resistance applies only when the single-lap switch is on.", "BS EN 1993-1-8:2005 cl. 3.6.1; a warning says when it would bind."),
            new CalcAssumption("Spacing minima and γ_M factors are entered from a National Annex record.", "A small set of factors only; no table from a standard is shipped."),
            new CalcAssumption("The defaults for the spacing minima and γ_M factors are the recommended values of BS EN 1993-1-8.", "A small set of factors only; no table from a standard is shipped."),
            new CalcAssumption("Group forces and weld forces per length come from the bolt group and weld group sheets by link.", "Linked from the bolt-group and weld-group calculations."),
        ],
        [
            new CalcClauseRef("BS EN 1993-1-8:2005", "cl. 3.5, Table 3.3", "Positioning of holes for bolts"),
            new CalcClauseRef("BS EN 1993-1-8:2005", "cl. 3.6.1, Table 3.4", "Design resistance of individual fasteners"),
            new CalcClauseRef("BS EN 1993-1-8:2005", "cl. 3.10.2", "Block tearing"),
            new CalcClauseRef("BS EN 1993-1-8:2005", "cl. 4.5.3.2, 4.5.3.3, Table 4.1", "Fillet welds: directional and simplified methods; correlation factor"),
            new CalcClauseRef("ISO 898-1:2013", "cl. 9.1", "Nominal stress area (formula)"),
        ],
        [
            new CalcApplicabilityRule("hole", "The hole must be larger than the bolt; an oversize or slotted hole type needs its hole size d_0 entered.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("hole_clearance", "A d₀ above the normal clearance with the hole type left at normal is treated as oversize (bearing × 0.8).", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("single_lap", "With one shear plane and k₁·α_b above 1.5 the bearing resistance is capped for a single-lap joint with one bolt row; the sheet warns until the joint type is stated.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("spacing", "The edge distance e₂, the gauge p₂ and the pitch p₁ must leave a positive bearing factor (k₁ and α_b above 0).", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("e_head", "The across-corners dimension cannot be less than the across-flats dimension of the nut.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("block", "Block tearing needs the group force and both net areas.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("weld", "The weld check needs a throat and a force per length.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
        ],
        [
            new CalcTestCase("ka-m20-88-shear-bearing", "M20 8.8, single shear through threads, 10 mm S275 plate",
                new CalcReferenceSource("Analytic (EN 1993-1-8 Table 3.4 relations)", "BS EN 1993-1-8:2005", "A_s = 244.79 mm²; F_v,Rd = 0.6·800·244.79/1.25 = 94.00 kN; F_t,Rd = 0.9·800·244.79/1.25 = 141.00 kN; d₀ 22, e₁ = e₂ = 40, p₁ = p₂ = 70: α_d = 40/66 = 0.6061, k₁ = 2.5, F_b,Rd = 2.5·0.6061·410·20·10/1.25 = 99.39 kN; punching with d_m = s = 30 mm (the across-corners e is not entered): B_p,Rd = 0.6π·30·10·410/1.25 = 185.48 kN"),
                [Choice("grade", "8.8"), Choice("size", "M20"), Q("p_1", 70, "mm"), Q("p_2", 70, "mm"), Q("F_v_Ed", 50, "kN"), Q("F_t_Ed", 30, "kN")],
                [new CalcExpectedValue("A_s", 244.794, "mm^2", 1e-5), new CalcExpectedValue("F_v_Rd", 94.00105, "kN", 1e-6), new CalcExpectedValue("F_t_Rd", 141.0016, "kN", 1e-6), new CalcExpectedValue("F_b_Rd", 99.39394, "kN", 1e-6), new CalcExpectedValue("B_p_Rd", 185.4796, "kN", 1e-6), new CalcExpectedValue("u_vt", 0.6838832, "", 1e-6)]),
            new CalcTestCase("ka-m20-punching-e-head", "M20 punching with the across-corners of the nut entered",
                new CalcReferenceSource("Analytic (EN 1993-1-8 Table 3.4, d_m = (s + e)/2)", "BS EN 1993-1-8:2005", "s = 30 mm, e = 34 mm (illustrative): d_m = 32 mm, B_p,Rd = 0.6π·32·10·410/1.25 = 197.8449 kN" ),
                [Choice("grade", "8.8"), Choice("size", "M20"), Q("e_head", 34, "mm")],
                [new CalcExpectedValue("B_p_Rd", 197.8449, "kN", 1e-6)]),
            new CalcTestCase("ka-m20-single-lap-cap", "M20 8.8, 10 mm S275, e₁ = e₂ = 60 in a single-lap joint with one bolt row: the cap 1.5·f_u·d·t/γ_M2 governs",
                new CalcReferenceSource("Analytic (EN 1993-1-8 cl. 3.6.1 single-lap limit)", "BS EN 1993-1-8:2005", "d₀ 22, e₁ = e₂ = 60: α_d = 60/66 = 0.9091, α_b = min(0.9091, 800/410, 1) = 0.9091, k₁ = min(2.8·60/22 − 1.7, 2.5) = 2.5, so k₁α_b = 2.2727 > 1.5 and F_b,Rd = 2.5·0.9091·410·20·10/1.25 = 149.09 kN is limited to 1.5·410·20·10/1.25 = 98.4 kN"),
                [Choice("grade", "8.8"), Choice("size", "M20"), Q("e_1", 60, "mm"), Q("e_2", 60, "mm"), CalcInputValue.Switch("single_lap", true)],
                [new CalcExpectedValue("F_b_Rd", 98.4, "kN", 1e-9)]),
            new CalcTestCase("ka-m20-oversize-hole", "M20 8.8, 10 mm S275, e₁ = e₂ = 40, an oversize hole d₀ = 24: bearing × 0.8",
                new CalcReferenceSource("Analytic (EN 1993-1-8 cl. 3.6.1, Table 3.4 hole factor)", "BS EN 1993-1-8:2005", "d₀ 24: α_d = 40/72 = 0.5556, k₁ = min(2.8·40/24 − 1.7, 2.5) = 2.5, 2.5·0.5556·410·20·10/1.25 = 91.111 kN; × 0.8 (oversize) = 72.889 kN; × 0.6 (slot across the load) = 54.667 kN"),
                [Choice("grade", "8.8"), Choice("size", "M20"), Q("d_0", 24, "mm"), Choice("hole_type", HoleOversize)],
                [new CalcExpectedValue("F_b_Rd", 72.88889, "kN", 1e-6)]),
            new CalcTestCase("ka-m20-109-threads", "M20 10.9 with threads in the shear plane: α_v = 0.5",
                new CalcReferenceSource("Analytic (EN 1993-1-8 Table 3.4)", "BS EN 1993-1-8:2005", "F_v,Rd = 0.5·1000·244.79/1.25 = 97.918 kN (with the shank in the plane it would be 0.6·1000·314.16/1.25 = 150.80 kN)"),
                [Choice("grade", "10.9"), Choice("size", "M20")],
                [new CalcExpectedValue("alpha_v", 0.5, "", 1e-12), new CalcExpectedValue("F_v_Rd", 97.91776, "kN", 1e-6)]),
            new CalcTestCase("ka-weld-s275", "6 mm fillet (a = 4.2 mm) in S275, simplified method",
                new CalcReferenceSource("Analytic (EN 1993-1-8 cl. 4.5.3.3)", "BS EN 1993-1-8:2005", "f_vw,d = 410/(√3·0.85·1.25) = 222.79 MPa; F_w,Rd = 222.79·4.2 = 935.71 N/mm"),
                [Q("a", 4.2, "mm"), Q("F_w_Ed", 700, "N/mm")],
                [new CalcExpectedValue("f_vw_d", 222.7893, "MPa", 1e-6), new CalcExpectedValue("F_w_Rd", 935.715, "N/mm", 1e-6)]),
            new CalcTestCase("ka-p363-m20-88", "M20 8.8 resistances against the SCI Blue Book (P363) tables",
                new CalcReferenceSource("SCI P363 Steel building design: design data (Blue Book)", "2013 edition", "Bolt resistances quoted as about 94.1 kN single shear (threads) and 141 kN tension for M20 8.8, cross-checked within 1 %; to be confirmed against the owner's copy"),
                [Choice("grade", "8.8"), Choice("size", "M20")],
                [new CalcExpectedValue("F_v_Rd", 94.1, "kN", 0.01), new CalcExpectedValue("F_t_Rd", 141, "kN", 0.01)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-p358-fin-plate", "Fin-plate bolt bearing pattern (SCI P358)",
                new CalcReferenceSource("SCI P358 Joints in Steel Construction: Simple Joints to Eurocode 3", "2014", "Fin-plate worked example pattern (M20 8.8, 10 mm S275 plate, e₁ = 40, p₁ = 70 mm): bearing per bolt with α_b = 0.606, k₁ = 2.5 → 99.4 kN; the numbers are analytic and await the owner's check against the publication"),
                [Choice("grade", "8.8"), Choice("size", "M20"), Q("p_1", 70, "mm"), Q("p_2", 70, "mm")],
                [new CalcExpectedValue("F_b_Rd", 99.4, "kN", 0.01)],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Connections",
        OutputPorts:
        [
            new CalcOutputPort("F_v_Rd", "kN", "Shear resistance per bolt", "bolt.resistance_shear"),
            new CalcOutputPort("F_b_Rd", "kN", "Bearing resistance per bolt (→ base-plate F_1_vb_Rd)", "bolt.resistance_bearing"),
            new CalcOutputPort("F_t_Rd", "kN", "Tension resistance per bolt (→ base-plate F_t_Rd)", "bolt.resistance_tension"),
            new CalcOutputPort("A_s", "mm^2", "Bolt tensile stress area (→ base-plate A_s)", "bolt.area_stress"),
            new CalcOutputPort("f_ub", "MPa", "Bolt ultimate tensile strength (→ base-plate f_ub)", "bolt.strength_ultimate"),
            new CalcOutputPort("f_yb", "MPa", "Bolt yield strength (→ base-plate f_yb)", "bolt.strength_yield"),
            new CalcOutputPort("f_vw_d", "MPa", "Weld design shear strength (→ weld-group f_w)", "weld.strength_shear_design"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("ec3.national-annex", "EN 1993-1-8 National Annex factors", ["gamma_M2", "gamma_M0", "k_e1", "k_e2", "k_p1", "k_p2"], "From the National Annex in use, with its edition."),
            new CalcRecordKind("steel.strengths", "Plate strengths", ["f_y", "f_u"], "From the product standard for the grade and thickness (EN 10025-2 for S275)."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue Choice(string symbol, string value) => CalcInputValue.Choice(symbol, value);

    /// <summary>EN 1993-1-8 Table 3.4 α_v for a class: 0.5 for 4.8, 5.8, 6.8 and 10.9 when threads are in the shear plane, otherwise 0.6.</summary>
    public static double AlphaV(string grade, bool threadsInPlane) => threadsInPlane && grade is "4.8" or "5.8" or "6.8" or "10.9" ? 0.5 : 0.6;

    /// <summary>f_ub and f_yb from the property class name.</summary>
    public static (double Fub, double Fyb) Strengths(string grade)
    {
        var parts = grade.Split('.');
        var fub = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) * 100;
        return (fub, fub * double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) / 10);
    }

    /// <summary>The ISO 898-1 nominal stress area from d and the pitch, mm².</summary>
    public static double StressArea(double d, double pitch)
    {
        var d2 = d - 0.649519 * pitch;
        var d3 = d - 1.226869 * pitch;
        return Math.PI / 4 * Math.Pow((d2 + d3) / 2, 2);
    }

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_b", "u_block", "u_dir", "u_perp", "u_t", "u_v", "u_vt", "u_w");
        foreach (var s in new[] { "n_s", "t_p", "f_y", "f_u", "e_1", "e_2", "k_e1", "k_e2", "k_p1", "k_p2", "gamma_M2", "gamma_M0", "F_v_Ed", "F_t_Ed" })
            c.Input(s);

        var grade = c.InputText("grade");
        var size = Sizes.Single(s => s.Name == c.InputText("size"));
        var threads = c.InputFlag("threads");
        var (fub, fyb) = Strengths(grade);
        var d = size.D;
        var holeTypeEntered = c.InputText("hole_type");

        // An oversize or slotted hole has no size the sheet may assume (the clearance of an oversize hole is a table of the execution standard, not shipped here): with d_0 blank the normal hole
        // would be used in alpha_d, k_1 and the edge checks and the bearing resistance would be overstated (M20: 79.52 kN against 72.89 kN at 24 mm).
        if (holeTypeEntered != HoleNormal && !c.Has("d_0"))
        {
            c.Block("hole", $"d_0: enter the hole diameter, or its width across the load for a slot, because the hole type is '{holeTypeEntered}'. The normal clearance would overstate the bearing resistance.");
            return;
        }

        var d0 = c.Has("d_0") ? c.InputSi("d_0") * 1e3 : d + (d >= 27 ? 3 : 2);

        if (!(d0 > d))
        {
            c.Block("hole", $"d_0: the hole of {F(d0)} mm must be larger than the bolt of {F(d)} mm.");
            return;
        }

        var t = c.Si("t_p") * 1e3;
        var fu = c.Si("f_u") / 1e6;
        var fy = c.Si("f_y") / 1e6;
        var g2 = c.InputSi("gamma_M2");
        var g0 = c.InputSi("gamma_M0");
        var ns = Math.Round(c.InputSi("n_s"));
        var e1 = c.Si("e_1") * 1e3;
        var e2 = c.Si("e_2") * 1e3;
        double? p1 = c.Has("p_1") ? c.InputSi("p_1") * 1e3 : null;
        double? p2 = c.Has("p_2") ? c.InputSi("p_2") * 1e3 : null;
        var kN = Tempest.Calc.Engine.Units.UnitCatalog.Parse("kN");

        c.Record("f_ub", "class first figure × 100", $"class {grade}", ScalarValue.Of(fub, "MPa"), "Bolt ultimate tensile strength", "EN 1993-1-8 Table 3.1");
        c.Record("f_yb", "f_ub × second figure / 10", $"class {grade}", ScalarValue.Of(fyb, "MPa"), "Bolt yield strength", "EN 1993-1-8 Table 3.1");
        var asArea = StressArea(d, size.Pitch);
        c.Record("A_s", "π/4·((d₂ + d₃)/2)², d₂ = d − 0.6495P, d₃ = d − 1.2269P", $"d = {F(d)}, P = {F(size.Pitch)} mm", ScalarValue.Of(asArea, "mm^2"), "Tensile stress area", "ISO 898-1 cl. 9.1");
        c.Record("d_0", "d + 2 (≤ M24), d + 3 (≥ M27) unless entered", $"{F(d0)} mm", ScalarValue.Of(d0, "mm"), "Hole diameter");
        var alphaV = AlphaV(grade, threads);
        var shearArea = threads ? asArea : Math.PI / 4 * d * d;
        c.Record("alpha_v", threads ? "α_v (threads in the shear plane)" : "α_v (shank in the shear plane)", $"class {grade}", new ScalarValue(alphaV), "Shear factor", "EN 1993-1-8 Table 3.4");
        c.Record("F_v_Rd", "α_v·f_ub·A·n_s/γ_M2", $"{F(alphaV)} × {F(fub)} × {F(shearArea)} × {F(ns)} / {F(g2)}", ScalarValue.Of(alphaV * fub * shearArea * ns / g2 / 1e3, kN), "Shear resistance per bolt", "EN 1993-1-8 Table 3.4");

        var alphaEnd = e1 / (3 * d0);
        var alphaInner = p1 is { } pitch ? pitch / (3 * d0) - 0.25 : double.PositiveInfinity;
        var alphaD = Math.Min(alphaEnd, alphaInner);
        var alphaB = Math.Min(Math.Min(alphaD, fub / fu), 1.0);
        var k1 = Math.Min(2.8 * e2 / d0 - 1.7, 2.5);

        if (p2 is { } gauge)
            k1 = Math.Min(k1, 1.4 * gauge / d0 - 1.7);

        if (!(k1 > 0) || !(alphaB > 0))
        {
            var message = 2.8 * e2 / d0 - 1.7 <= 0
                ? $"e_2: the edge distance of {F(e2)} mm gives no bearing resistance with d₀ = {F(d0)} mm. BS EN 1993-1-8 Table 3.3 needs at least {F(c.InputSi("k_e2") * d0)} mm."
                : p2 is { } g2m && 1.4 * g2m / d0 - 1.7 <= 0
                    ? $"p_2: the gauge of {F(g2m)} mm gives no bearing resistance with d₀ = {F(d0)} mm. BS EN 1993-1-8 Table 3.3 needs at least {F(c.InputSi("k_p2") * d0)} mm."
                    : $"p_1: the pitch of {(p1 is { } pp ? F(pp) : "—")} mm gives no bearing resistance with d₀ = {F(d0)} mm. BS EN 1993-1-8 Table 3.3 needs at least {F(c.InputSi("k_p1") * d0)} mm.";
            c.Block("spacing", message);
            return;
        }

        c.Record("alpha_b", "min(α_d, f_ub/f_u, 1), α_d = min(e₁/3d₀, p₁/3d₀ − 1/4)", $"min({F(alphaEnd)}, {(double.IsInfinity(alphaInner) ? "—" : F(alphaInner))}, {F(fub / fu)}, 1)", new ScalarValue(alphaB), "Bearing factor α_b", "EN 1993-1-8 Table 3.4");
        c.Record("k_1", "min(2.8e₂/d₀ − 1.7, 1.4p₂/d₀ − 1.7, 2.5)", $"e₂ = {F(e2)}, p₂ = {(p2 is { } g ? F(g) : "—")}, d₀ = {F(d0)}", new ScalarValue(k1), "Bearing factor k₁", "EN 1993-1-8 Table 3.4");
        var normalHole = d + (d >= 27 ? 3 : 2);
        var holeType = c.InputText("hole_type");
        var holeFactor = holeType switch { HoleSlot => 0.6, HoleOversize => 0.8, _ => d0 > normalHole + 1e-9 ? 0.8 : 1.0 };

        if (holeType == HoleNormal && d0 > normalHole + 1e-9)
            c.Warn("hole_clearance", $"d_0: the hole of {F(d0)} mm exceeds the normal clearance of {F(normalHole)} mm. The bearing resistance is reduced by 0.8 as for an oversize hole, so select the hole type.");

        // Bearing is on the thinner of the plies the bolt passes through (an end plate and the column flange behind it).
        var tBearing = t;

        if (c.Has("t_2"))
        {
            c.Input("t_2");
            tBearing = Math.Min(t, c.Si("t_2") * 1e3);
            c.Assume($"Bearing is taken on the thinner ply, {F(tBearing)} mm, with this sheet's f_u, edge distances and spacings.", $"t_p = {F(t)} mm and t_2 = {F(c.Si("t_2") * 1e3)} mm; where the other ply differs, run the sheet for it as well (BS EN 1993-1-8 cl. 3.6.1, Table 3.4).");
        }

        var fbBase = Math.Max(k1, 0) * alphaB * fu * d * tBearing / g2;
        var fbCap = 1.5 * fu * d * tBearing / g2;
        var singleLap = c.InputFlag("single_lap") && Math.Abs(ns - 1) < 1e-9;
        var fbLap = singleLap ? Math.Min(fbBase, fbCap) : fbBase;

        if (!singleLap && Math.Abs(ns - 1) < 1e-9 && fbBase > fbCap + 1e-9)
            c.Warn("single_lap", $"single_lap: k₁·α_b = {F(k1 * alphaB)} exceeds 1.5, which caps bearing resistance of a single lap at {F(fbCap * holeFactor / 1e3)} kN, not {F(fbBase * holeFactor / 1e3)} kN. Switch single_lap on if that is the joint.");

        var fb = holeFactor * fbLap;
        var fbFormula = (singleLap ? "min(k₁·α_b, 1.5)" : "k₁·α_b") + "·f_u·d·t/γ_M2" + (holeFactor < 1 ? " × k_h" : string.Empty);
        var fbSubstitution = singleLap ? $"min({F(k1)} × {F(alphaB)}, 1.5) × {F(fu)} × {F(d)} × {F(tBearing)} / {F(g2)}" : $"{F(k1)} × {F(alphaB)} × {F(fu)} × {F(d)} × {F(tBearing)} / {F(g2)}";
        c.Record("F_b_Rd", fbFormula, fbSubstitution + (holeFactor < 1 ? $" × {F(holeFactor)}" : string.Empty), ScalarValue.Of(fb / 1e3, kN), "Bearing resistance per bolt", "EN 1993-1-8 cl. 3.6.1, Table 3.4");
        c.Record("F_t_Rd", "k₂·f_ub·A_s/γ_M2, k₂ = 0.9", $"0.9 × {F(fub)} × {F(asArea)} / {F(g2)}", ScalarValue.Of(0.9 * fub * asArea / g2 / 1e3, kN), "Tension resistance per bolt", "EN 1993-1-8 Table 3.4");
        // d_m is the mean of the across-flats and the across-corners dimension of the smaller of head and nut (Table 3.4). The across-corners of a
        // sharp hexagon (1.1547·s) is an upper bound the real nut does not reach, so without the entered e the across-flats s is used (Green G-A-09).
        var acrossCorners = c.Has("e_head") ? c.InputSi("e_head") * 1e3 : (double?)null;

        if (acrossCorners is { } ec && ec < size.AcrossFlats)
        {
            c.Block("e_head", $"e_head: the across-corners dimension ({F(ec)} mm) cannot be less than the across-flats dimension of the nut ({F(size.AcrossFlats)} mm).");
            return;
        }

        var dm = acrossCorners is { } e ? (size.AcrossFlats + e) / 2 : size.AcrossFlats;
        c.Record("B_p_Rd", acrossCorners is null ? "0.6π·d_m·t·f_u/γ_M2, d_m = s (e not entered)" : "0.6π·d_m·t·f_u/γ_M2, d_m = (s + e)/2", $"0.6π × {F(dm)} × {F(t)} × {F(fu)} / {F(g2)}", ScalarValue.Of(0.6 * Math.PI * dm * t * fu / g2 / 1e3, kN), "Punching shear resistance", "EN 1993-1-8 Table 3.4");

        c.Assign("u_v", "F_v_Ed / F_v_Rd", null, "Shear utilisation");
        c.Assign("u_b", "F_v_Ed / F_b_Rd", null, "Bearing utilisation");
        c.Assign("u_t", "F_t_Ed / min(F_t_Rd, B_p_Rd)", null, "Tension utilisation (tension or punching)");
        c.Assign("u_vt", "F_v_Ed / F_v_Rd + F_t_Ed / (1.4 * F_t_Rd)", null, "Combined shear and tension", "EN 1993-1-8 Table 3.4");

        var spacingOk = e1 >= c.InputSi("k_e1") * d0 && e2 >= c.InputSi("k_e2") * d0 && (p1 is not { } a1 || a1 >= c.InputSi("k_p1") * d0) && (p2 is not { } a2 || a2 >= c.InputSi("k_p2") * d0);
        var outputs = new List<(string, string)> { ("f_ub", "Bolt ultimate tensile strength"), ("f_yb", "Bolt yield strength"), ("A_s", "Stress area"), ("alpha_v", "Shear factor"), ("F_v_Rd", "Shear resistance"), ("alpha_b", "Bearing factor"), ("k_1", "Bearing factor k₁"), ("F_b_Rd", "Bearing resistance"), ("F_t_Rd", "Tension resistance"), ("B_p_Rd", "Punching resistance"), ("u_v", "Shear utilisation"), ("u_b", "Bearing utilisation"), ("u_t", "Tension utilisation"), ("u_vt", "Combined utilisation") };

        if (c.Has("V_Ed") || c.Has("A_nt") || c.Has("A_nv"))
        {
            if (c.Has("V_Ed") && c.Has("A_nt") && c.Has("A_nv"))
            {
                c.Input("V_Ed");
                c.Input("A_nt");
                c.Input("A_nv");
                var eccentric = c.InputText("block_load") == BlockEccentric;
                c.Assign("V_eff_Rd", eccentric ? "0.5 * f_u * A_nt / gamma_M2 + f_y * A_nv / (sqrt(3) * gamma_M0)" : "f_u * A_nt / gamma_M2 + f_y * A_nv / (sqrt(3) * gamma_M0)", "kN", eccentric ? "Block tearing resistance V_eff,2 (eccentric loading)" : "Block tearing resistance V_eff,1 (concentric loading)", "EN 1993-1-8 cl. 3.10.2");
                c.Assign("u_block", "V_Ed / V_eff_Rd", null, "Block tearing utilisation");
                outputs.AddRange([("V_eff_Rd", "Block tearing resistance"), ("u_block", "Block tearing utilisation")]);
            }
            else
            {
                c.Warn("block", "V_Ed: enter the group force and both net areas for block tearing. Block tearing is not checked.");
            }
        }

        var steel = Steels.Single(s => s.Grade == c.InputText("steel"));
        var weld = c.Has("a") && c.Has("F_w_Ed");

        if (c.Has("a") || c.Has("F_w_Ed"))
        {
            if (weld)
            {
                c.Input("a");
                c.Input("F_w_Ed");
                c.Record("beta_w", "β_w for the steel grade", steel.Grade, new ScalarValue(steel.BetaW), "Weld correlation factor", "EN 1993-1-8 Table 4.1");
                c.Assign("f_vw_d", "f_u / (sqrt(3) * beta_w * gamma_M2)", "MPa", "Design shear strength of the weld", "EN 1993-1-8 cl. 4.5.3.3");
                c.Assign("F_w_Rd", "f_vw_d * a", "kN/m", "Weld resistance per unit length");
                c.Assign("u_w", "F_w_Ed / F_w_Rd", null, "Weld utilisation (simplified)");
                outputs.AddRange([("f_vw_d", "Weld design strength"), ("F_w_Rd", "Weld resistance per length"), ("u_w", "Weld utilisation")]);
            }
            else
            {
                c.Warn("weld", "a: enter the throat and the force per length for the weld check. The weld is not checked.");
            }
        }

        if (c.Has("sigma_perp") || c.Has("tau_perp") || c.Has("tau_par"))
        {
            var sp = c.Has("sigma_perp") ? c.InputSi("sigma_perp") / 1e6 : 0;
            var tp = c.Has("tau_perp") ? c.InputSi("tau_perp") / 1e6 : 0;
            var tl = c.Has("tau_par") ? c.InputSi("tau_par") / 1e6 : 0;
            var eq = Math.Sqrt(sp * sp + 3 * (tp * tp + tl * tl));
            c.Record("u_dir", "√(σ⊥² + 3(τ⊥² + τ∥²))/(f_u/(β_w·γ_M2))", $"√({F(sp)}² + 3({F(tp)}² + {F(tl)}²)) / ({F(fu)}/({F(steel.BetaW)} × {F(g2)}))", new ScalarValue(eq / (fu / (steel.BetaW * g2))), "Directional method utilisation", "EN 1993-1-8 cl. 4.5.3.2");
            c.Record("u_perp", "|σ⊥|/(0.9f_u/γ_M2)", $"{F(Math.Abs(sp))} / ({F(0.9 * fu)}/{F(g2)})", new ScalarValue(Math.Abs(sp) / (0.9 * fu / g2)), "Normal stress utilisation", "EN 1993-1-8 cl. 4.5.3.2");
            outputs.AddRange([("u_dir", "Directional utilisation"), ("u_perp", "Normal stress utilisation")]);
        }

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        var (mode, worst) = new[] { ("shear", c.Si("u_v")), ("bearing", c.Si("u_b")), ("tension", c.Si("u_t")), ("combined", c.Si("u_vt")) }.MaxBy(p => p.Item2);
        c.Output("governing", "Governing bolt mode", new TextValue($"{mode} ({CalcValue.FormatNumber(worst, 3)})"));

        c.Record("u_limit", "limit", "the utilisation limit", new ScalarValue(1.0), "Utilisation limit");
        c.Assign("F_t_lim", "min(F_t_Rd, B_p_Rd)", "kN", "Governing tension or punching resistance");
        c.Check("Shear force against bolt shear resistance", "F_v_Ed", "F_v_Rd", "EN 1993-1-8 Table 3.4", resultSymbol: "u_v");
        c.Check("Shear force against bolt bearing resistance", "F_v_Ed", "F_b_Rd", "EN 1993-1-8 Table 3.4", resultSymbol: "u_b");
        c.Check("Tension force against bolt tension or punching resistance", "F_t_Ed", "F_t_lim", "EN 1993-1-8 Table 3.4", resultSymbol: "u_t");
        c.Check("Combined shear and tension against its limit", "u_vt", "u_limit", "EN 1993-1-8 Table 3.4");
        c.CheckCondition("Spacing and edge distances against the Table 3.3 minima", spacingOk, $"e₁ {F(e1)}, e₂ {F(e2)}, p₁ {(p1 is { } x ? F(x) : "—")}, p₂ {(p2 is { } y ? F(y) : "—")} mm", $"≥ {F(c.InputSi("k_e1") * d0)}, {F(c.InputSi("k_e2") * d0)}, {F(c.InputSi("k_p1") * d0)}, {F(c.InputSi("k_p2") * d0)} mm", null, "EN 1993-1-8 Table 3.3");

        if (c.Has("V_Ed") && c.Has("A_nt") && c.Has("A_nv"))
            c.Check("Group force against block tearing resistance", "V_Ed", "V_eff_Rd", "EN 1993-1-8 cl. 3.10.2", resultSymbol: "u_block");

        if (weld)
            c.Check("Weld force against simplified weld resistance", "F_w_Ed", "F_w_Rd", "EN 1993-1-8 cl. 4.5.3.3", resultSymbol: "u_w");

        if (c.Has("sigma_perp") || c.Has("tau_perp") || c.Has("tau_par"))
        {
            c.Check("Directional stress against its limit", "u_dir", "u_limit", "EN 1993-1-8 cl. 4.5.3.2");
            c.Check("Normal stress against its limit", "u_perp", "u_limit", "EN 1993-1-8 cl. 4.5.3.2");
        }
    }

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
