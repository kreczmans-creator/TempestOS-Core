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
        Id, "1.1.0", CalcDiscipline.Structural, "Bolts and fillet welds (EN 1993-1-8)",
        "The design resistances of one bolt in a plate (shear, bearing, tension, punching, combined shear and tension), an optional block-tearing check of the bolt group, the spacing limits, and a fillet weld by the simplified and the directional method, each against its design force.",
        "Bolt strengths: f_ub = class × 100 MPa; A_s = π/4·((d₂ + d₃)/2)², d₂ = d − 0.6495P, d₃ = d − 1.2269P\n" +
        "Shear (cl. 3.6, Table 3.4): F_v,Rd = α_v·f_ub·A·n_s/γ_M2; α_v = 0.6 (4.6, 5.6, 8.8 or shank in the plane), 0.5 (4.8, 5.8, 6.8, 10.9 with threads in the plane)\n" +
        "Bearing: F_b,Rd = k₁·α_b·f_u·d·t/γ_M2; α_b = min(α_d, f_ub/f_u, 1), α_d = e₁/3d₀ (end) or p₁/3d₀ − 1/4 (inner); k₁ = min(2.8e₂/d₀ − 1.7, 1.4p₂/d₀ − 1.7, 2.5)\n" +
        "Tension: F_t,Rd = k₂·f_ub·A_s/γ_M2, k₂ = 0.9; punching B_p,Rd = 0.6π·d_m·t·f_u/γ_M2; combined F_v/F_v,Rd + F_t/(1.4F_t,Rd) ≤ 1\n" +
        "Block tearing (cl. 3.10.2): V_eff,1,Rd = f_u·A_nt/γ_M2 + f_y·A_nv/(√3·γ_M0)\n" +
        "Weld, simplified (cl. 4.5.3.3): f_vw,d = f_u/(√3·β_w·γ_M2), F_w,Rd = f_vw,d·a; directional (cl. 4.5.3.2): √(σ⊥² + 3(τ⊥² + τ∥²)) ≤ f_u/(β_w·γ_M2), σ⊥ ≤ 0.9f_u/γ_M2",
        [
            new CalcInputSpec("grade", "Bolt property class", CalcInputKind.Choice, Choices: Grades, DefaultChoice: "8.8", Group: "Bolt"),
            new CalcInputSpec("size", "Bolt size", CalcInputKind.Choice, Choices: [.. Sizes.Select(s => s.Name)], DefaultChoice: "M20", Group: "Bolt"),
            new CalcInputSpec("d_0", "Hole diameter (blank: normal clearance d + 2, or d + 3 from M27)", CalcInputKind.Quantity, "mm", Min: 13, Max: 40, HardMin: 13, HardMax: 40, IsOptional: true, Group: "Bolt"),
            new CalcInputSpec("n_s", "Shear planes", CalcInputKind.Number, Default: 1, Min: 1, Max: 4, HardMin: 1, HardMax: 4, Group: "Bolt"),
            new CalcInputSpec("threads", "Threads in the shear plane", CalcInputKind.Boolean, Default: 1, Group: "Bolt"),
            new CalcInputSpec("steel", "Plate steel grade (β_w)", CalcInputKind.Choice, Choices: [.. Steels.Select(s => s.Grade)], DefaultChoice: "S275", Group: "Plate"),
            new CalcInputSpec("t", "Plate thickness", CalcInputKind.Quantity, "mm", Default: 10, Min: 3, Max: 50, HardMin: 3, HardMax: 50, Group: "Plate"),
            new CalcInputSpec("f_y", "Plate yield strength", CalcInputKind.Quantity, "MPa", Default: 275, Min: 235, Max: 460, HardMin: 235, HardMax: 460, Group: "Plate"),
            new CalcInputSpec("f_u", "Plate ultimate strength", CalcInputKind.Quantity, "MPa", Default: 410, Min: 340, Max: 720, HardMin: 340, HardMax: 720, Group: "Plate"),
            new CalcInputSpec("e_1", "End distance (load direction)", CalcInputKind.Quantity, "mm", Default: 40, Min: 10, Max: 500, HardMin: 10, HardMax: 500, Group: "Spacing"),
            new CalcInputSpec("e_2", "Edge distance (across the load)", CalcInputKind.Quantity, "mm", Default: 40, Min: 10, Max: 500, HardMin: 10, HardMax: 500, Group: "Spacing"),
            new CalcInputSpec("p_1", "Pitch (load direction; blank: one row)", CalcInputKind.Quantity, "mm", Min: 20, Max: 1000, HardMin: 20, HardMax: 1000, IsOptional: true, Group: "Spacing"),
            new CalcInputSpec("p_2", "Gauge (across the load; blank: one line)", CalcInputKind.Quantity, "mm", Min: 20, Max: 1000, HardMin: 20, HardMax: 1000, IsOptional: true, Group: "Spacing"),
            new CalcInputSpec("k_e1", "Minimum e₁/d₀", CalcInputKind.Number, Default: 1.2, Min: 1, Max: 3, HardMin: 1, HardMax: 3, Group: "Spacing limits"),
            new CalcInputSpec("k_e2", "Minimum e₂/d₀", CalcInputKind.Number, Default: 1.2, Min: 1, Max: 3, HardMin: 1, HardMax: 3, Group: "Spacing limits"),
            new CalcInputSpec("k_p1", "Minimum p₁/d₀", CalcInputKind.Number, Default: 2.2, Min: 2, Max: 4, HardMin: 2, HardMax: 4, Group: "Spacing limits"),
            new CalcInputSpec("k_p2", "Minimum p₂/d₀", CalcInputKind.Number, Default: 2.4, Min: 2, Max: 4, HardMin: 2, HardMax: 4, Group: "Spacing limits"),
            new CalcInputSpec("gamma_M2", "γ_M2", CalcInputKind.Number, Default: 1.25, Min: 1.1, Max: 1.5, HardMin: 1.1, HardMax: 1.5, Group: "Factors"),
            new CalcInputSpec("gamma_M0", "γ_M0", CalcInputKind.Number, Default: 1.0, Min: 1, Max: 1.2, HardMin: 1, HardMax: 1.2, Group: "Factors"),
            new CalcInputSpec("F_v_Ed", "Shear per bolt", CalcInputKind.Quantity, "kN", Default: 50, Min: 0, Max: 2000, HardMin: 0, HardMax: 2000, Group: "Forces"),
            new CalcInputSpec("F_t_Ed", "Tension per bolt", CalcInputKind.Quantity, "kN", Default: 0, Min: 0, Max: 2000, HardMin: 0, HardMax: 2000, Group: "Forces"),
            new CalcInputSpec("V_Ed", "Group force for block tearing", CalcInputKind.Quantity, "kN", Min: 0, Max: 1e4, HardMin: 0, HardMax: 1e4, IsOptional: true, Group: "Block tearing"),
            new CalcInputSpec("A_nt", "Net area in tension", CalcInputKind.Quantity, "mm^2", Min: 1, Max: 1e5, HardMin: 1, HardMax: 1e5, IsOptional: true, Group: "Block tearing"),
            new CalcInputSpec("A_nv", "Net area in shear", CalcInputKind.Quantity, "mm^2", Min: 1, Max: 1e5, HardMin: 1, HardMax: 1e5, IsOptional: true, Group: "Block tearing"),
            new CalcInputSpec("a", "Weld throat", CalcInputKind.Quantity, "mm", Min: 3, Max: 25, HardMin: 3, HardMax: 25, IsOptional: true, Group: "Weld"),
            new CalcInputSpec("F_w_Ed", "Weld force per unit length", CalcInputKind.Quantity, "N/mm", Min: 0, Max: 1e4, HardMin: 0, HardMax: 1e4, IsOptional: true, Group: "Weld"),
            new CalcInputSpec("sigma_perp", "Directional: normal stress ⊥ throat", CalcInputKind.Quantity, "MPa", Min: -1000, Max: 1000, HardMin: -1000, HardMax: 1000, IsOptional: true, Group: "Weld"),
            new CalcInputSpec("tau_perp", "Directional: shear ⊥ weld axis", CalcInputKind.Quantity, "MPa", Min: -1000, Max: 1000, HardMin: -1000, HardMax: 1000, IsOptional: true, Group: "Weld"),
            new CalcInputSpec("tau_par", "Directional: shear ∥ weld axis", CalcInputKind.Quantity, "MPa", Min: -1000, Max: 1000, HardMin: -1000, HardMax: 1000, IsOptional: true, Group: "Weld"),
        ],
        [
            new CalcAssumption("Static loading; category A (bearing type) shear and category D (non-preloaded) tension; no slip-resistant joints or moment end plates.", "Scope of S4."),
            new CalcAssumption("Bearing is taken at the worst of an end bolt and an inner bolt, and at an edge bolt across the load (k₁ with e₂ and p₂).", "Conservative for every bolt in the group."),
            new CalcAssumption("Spacing minima (Table 3.3) and γ_M factors are entered from a National Annex record; the values shown default to EN 1993-1-8 recommended.", "§A5 small factor set."),
            new CalcAssumption("Group forces come from M3 and weld forces per length from M6 by link.", "§A6 S4 links."),
        ],
        [
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 3.5, Table 3.3", "Positioning of holes for bolts"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 3.6.1, Table 3.4", "Design resistance of individual fasteners"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 3.10.2", "Block tearing"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 4.5.3.2, 4.5.3.3, Table 4.1", "Fillet welds: directional and simplified methods; correlation factor"),
            new CalcClauseRef("ISO 898-1:2013", "cl. 9.1", "Nominal stress area (formula)"),
        ],
        [
            new CalcApplicabilityRule("hole", "The hole must be larger than the bolt.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("block", "Block tearing needs the group force and both net areas.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("weld", "The weld check needs a throat and a force per length.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-m20-88-shear-bearing", "M20 8.8, single shear through threads, 10 mm S275 plate",
                new CalcReferenceSource("Analytic (EN 1993-1-8 Table 3.4 relations)", "EN 1993-1-8:2005", "A_s = 244.79 mm²; F_v,Rd = 0.6·800·244.79/1.25 = 94.00 kN; F_t,Rd = 0.9·800·244.79/1.25 = 141.00 kN; d₀ 22, e₁ = e₂ = 40, p₁ = p₂ = 70: α_d = 40/66 = 0.6061, k₁ = 2.5, F_b,Rd = 2.5·0.6061·410·20·10/1.25 = 99.39 kN; d_m = 32.32 mm, B_p,Rd = 199.83 kN"),
                [Choice("grade", "8.8"), Choice("size", "M20"), Q("p_1", 70, "mm"), Q("p_2", 70, "mm"), Q("F_v_Ed", 50, "kN"), Q("F_t_Ed", 30, "kN")],
                [new CalcExpectedValue("A_s", 244.794, "mm^2", 1e-5), new CalcExpectedValue("F_v_Rd", 94.00105, "kN", 1e-6), new CalcExpectedValue("F_t_Rd", 141.0016, "kN", 1e-6), new CalcExpectedValue("F_b_Rd", 99.39394, "kN", 1e-6), new CalcExpectedValue("B_p_Rd", 199.8265, "kN", 1e-6), new CalcExpectedValue("u_vt", 0.6838832, "", 1e-6)]),
            new CalcTestCase("ka-m20-109-threads", "M20 10.9 with threads in the shear plane: α_v = 0.5",
                new CalcReferenceSource("Analytic (EN 1993-1-8 Table 3.4)", "EN 1993-1-8:2005", "F_v,Rd = 0.5·1000·244.79/1.25 = 97.918 kN (with the shank in the plane it would be 0.6·1000·314.16/1.25 = 150.80 kN)"),
                [Choice("grade", "10.9"), Choice("size", "M20")],
                [new CalcExpectedValue("alpha_v", 0.5, "", 1e-12), new CalcExpectedValue("F_v_Rd", 97.91776, "kN", 1e-6)]),
            new CalcTestCase("ka-weld-s275", "6 mm fillet (a = 4.2 mm) in S275, simplified method",
                new CalcReferenceSource("Analytic (EN 1993-1-8 cl. 4.5.3.3)", "EN 1993-1-8:2005", "f_vw,d = 410/(√3·0.85·1.25) = 222.79 MPa; F_w,Rd = 222.79·4.2 = 935.71 N/mm"),
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
            new CalcOutputPort("F_v_Rd", "kN", "Shear resistance per bolt"),
            new CalcOutputPort("F_b_Rd", "kN", "Bearing resistance per bolt (→ S6 F_1_vb_Rd)"),
            new CalcOutputPort("F_t_Rd", "kN", "Tension resistance per bolt (→ S6 holding-down bolts)"),
            new CalcOutputPort("A_s", "mm^2", "Bolt tensile stress area (→ S6 holding-down bolts)"),
            new CalcOutputPort("f_vw_d", "MPa", "Weld design shear strength (→ M6 f_w)"),
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
        foreach (var s in new[] { "n_s", "t", "f_y", "f_u", "e_1", "e_2", "k_e1", "k_e2", "k_p1", "k_p2", "gamma_M2", "gamma_M0", "F_v_Ed", "F_t_Ed" })
            c.Input(s);

        var grade = c.InputText("grade");
        var size = Sizes.Single(s => s.Name == c.InputText("size"));
        var threads = c.InputFlag("threads");
        var (fub, fyb) = Strengths(grade);
        var d = size.D;
        var d0 = c.Has("d_0") ? c.InputSi("d_0") * 1e3 : d + (d >= 27 ? 3 : 2);

        if (!(d0 > d))
        {
            c.Block("hole", $"d_0: the hole ({F(d0)} mm) must be larger than the bolt ({F(d)} mm).");
            return;
        }

        var t = c.Si("t") * 1e3;
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

        c.Record("f_ub", "class first figure × 100", $"class {grade}", ScalarValue.Of(fub, "MPa"), "Bolt ultimate strength", "EN 1993-1-8 Table 3.1");
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

        c.Record("alpha_b", "min(α_d, f_ub/f_u, 1), α_d = min(e₁/3d₀, p₁/3d₀ − 1/4)", $"min({F(alphaEnd)}, {(double.IsInfinity(alphaInner) ? "—" : F(alphaInner))}, {F(fub / fu)}, 1)", new ScalarValue(alphaB), "Bearing factor α_b", "EN 1993-1-8 Table 3.4");
        c.Record("k_1", "min(2.8e₂/d₀ − 1.7, 1.4p₂/d₀ − 1.7, 2.5)", $"e₂ = {F(e2)}, p₂ = {(p2 is { } g ? F(g) : "—")}, d₀ = {F(d0)}", new ScalarValue(k1), "Bearing factor k₁", "EN 1993-1-8 Table 3.4");
        var fb = Math.Max(k1, 0) * alphaB * fu * d * t / g2;
        c.Record("F_b_Rd", "k₁·α_b·f_u·d·t/γ_M2", $"{F(k1)} × {F(alphaB)} × {F(fu)} × {F(d)} × {F(t)} / {F(g2)}", ScalarValue.Of(fb / 1e3, kN), "Bearing resistance per bolt", "EN 1993-1-8 Table 3.4");
        c.Record("F_t_Rd", "k₂·f_ub·A_s/γ_M2, k₂ = 0.9", $"0.9 × {F(fub)} × {F(asArea)} / {F(g2)}", ScalarValue.Of(0.9 * fub * asArea / g2 / 1e3, kN), "Tension resistance per bolt", "EN 1993-1-8 Table 3.4");
        var dm = (size.AcrossFlats + size.AcrossFlats / Math.Cos(Math.PI / 6)) / 2;
        c.Record("B_p_Rd", "0.6π·d_m·t·f_u/γ_M2, d_m = (s + e)/2", $"0.6π × {F(dm)} × {F(t)} × {F(fu)} / {F(g2)}", ScalarValue.Of(0.6 * Math.PI * dm * t * fu / g2 / 1e3, kN), "Punching shear resistance", "EN 1993-1-8 Table 3.4");

        c.Assign("u_v", "F_v_Ed / F_v_Rd", null, "Shear utilisation");
        c.Assign("u_b", "F_v_Ed / F_b_Rd", null, "Bearing utilisation");
        c.Assign("u_t", "F_t_Ed / min(F_t_Rd, B_p_Rd)", null, "Tension utilisation (tension or punching)");
        c.Assign("u_vt", "F_v_Ed / F_v_Rd + F_t_Ed / (1.4 * F_t_Rd)", null, "Combined shear and tension", "EN 1993-1-8 Table 3.4");

        var spacingOk = e1 >= c.InputSi("k_e1") * d0 && e2 >= c.InputSi("k_e2") * d0 && (p1 is not { } a1 || a1 >= c.InputSi("k_p1") * d0) && (p2 is not { } a2 || a2 >= c.InputSi("k_p2") * d0);
        var outputs = new List<(string, string)> { ("f_ub", "Bolt ultimate strength"), ("A_s", "Stress area"), ("alpha_v", "Shear factor"), ("F_v_Rd", "Shear resistance"), ("alpha_b", "Bearing factor"), ("k_1", "Bearing factor k₁"), ("F_b_Rd", "Bearing resistance"), ("F_t_Rd", "Tension resistance"), ("B_p_Rd", "Punching resistance"), ("u_v", "Shear utilisation"), ("u_b", "Bearing utilisation"), ("u_t", "Tension utilisation"), ("u_vt", "Combined utilisation") };

        if (c.Has("V_Ed") || c.Has("A_nt") || c.Has("A_nv"))
        {
            if (c.Has("V_Ed") && c.Has("A_nt") && c.Has("A_nv"))
            {
                c.Input("V_Ed");
                c.Input("A_nt");
                c.Input("A_nv");
                c.Assign("V_eff_Rd", "f_u * A_nt / gamma_M2 + f_y * A_nv / (sqrt(3) * gamma_M0)", "kN", "Block tearing resistance (concentric)", "EN 1993-1-8 cl. 3.10.2");
                c.Assign("u_block", "V_Ed / V_eff_Rd", null, "Block tearing utilisation");
                outputs.AddRange([("V_eff_Rd", "Block tearing resistance"), ("u_block", "Block tearing utilisation")]);
            }
            else
            {
                c.Warn("block", "V_Ed: block tearing needs the group force and both net areas; it is not checked.");
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
                c.Assign("F_w_Rd", "f_vw_d * a", "N/mm", "Weld resistance per unit length");
                c.Assign("u_w", "F_w_Ed / F_w_Rd", null, "Weld utilisation (simplified)");
                outputs.AddRange([("f_vw_d", "Weld design strength"), ("F_w_Rd", "Weld resistance per length"), ("u_w", "Weld utilisation")]);
            }
            else
            {
                c.Warn("weld", "a: the weld check needs a throat and a force per length; it is not checked.");
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

        c.CheckUtilisation("Bolt shear", "u_v", "EN 1993-1-8 Table 3.4");
        c.CheckUtilisation("Bolt bearing", "u_b", "EN 1993-1-8 Table 3.4");
        c.CheckUtilisation("Bolt tension and punching", "u_t", "EN 1993-1-8 Table 3.4");
        c.CheckUtilisation("Combined shear and tension", "u_vt", "EN 1993-1-8 Table 3.4");
        c.CheckCondition("Spacing and edge distances (Table 3.3 minima)", spacingOk, $"e₁ {F(e1)}, e₂ {F(e2)}, p₁ {(p1 is { } x ? F(x) : "—")}, p₂ {(p2 is { } y ? F(y) : "—")} mm", $"≥ {F(c.InputSi("k_e1") * d0)}, {F(c.InputSi("k_e2") * d0)}, {F(c.InputSi("k_p1") * d0)}, {F(c.InputSi("k_p2") * d0)} mm", null, "EN 1993-1-8 Table 3.3");

        if (c.Has("V_Ed") && c.Has("A_nt") && c.Has("A_nv"))
            c.CheckUtilisation("Block tearing", "u_block", "EN 1993-1-8 cl. 3.10.2");

        if (weld)
            c.CheckUtilisation("Fillet weld, simplified method", "u_w", "EN 1993-1-8 cl. 4.5.3.3");

        if (c.Has("sigma_perp") || c.Has("tau_perp") || c.Has("tau_par"))
        {
            c.CheckUtilisation("Fillet weld, directional method", "u_dir", "EN 1993-1-8 cl. 4.5.3.2");
            c.CheckUtilisation("Fillet weld, normal stress", "u_perp", "EN 1993-1-8 cl. 4.5.3.2");
        }
    }

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
