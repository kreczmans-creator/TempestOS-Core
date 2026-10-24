using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Structural;

/// <summary>
/// S6 — an unstiffened rectangular steel base plate under an I/H, RHS or CHS column on a concrete foundation, to
/// EN 1993-1-8 cl. 6.2 (simplified): the bearing strength f_jd and effective bearing area, the axial resistance
/// N_j,Rd and the plate thickness that just carries the axial force, the tension T-stub of the plate and the
/// holding-down bolts for the moment, the compression flange T-stub, and shear by friction plus bolts. The concrete
/// and anchor design is not done here: the bolt tension per bolt is an output port for Hilti PROFIS, and the PROFIS
/// report is filed as Evidence and referenced.
/// </summary>
public sealed class BasePlateCalculator : ICalcCalculator
{
    public const string Id = "struct.base-plate";
    public const string ColumnIH = "I/H";
    public const string ColumnRhs = "RHS";
    public const string ColumnChs = "CHS";

    /// <summary>β_j for a grouted joint, fixed (EN 1993-1-8 cl. 6.2.5).</summary>
    public const double BetaJ = 2.0 / 3.0;

    /// <summary>The thickness range, mm, over which the required plate thickness is searched (the valid range of t_p).</summary>
    public const double MinThickness = 10;

    public const double MaxThickness = 100;

    /// <summary>The column footprint, mm: depth h along the plate length L, width b along the plate width B.</summary>
    public readonly record struct Section(string Column, double H, double B, double Tf, double Tw, double T, double D);

    /// <summary>The effective bearing area for one value of c and the text that shows how it was made.</summary>
    public sealed record BearingArea(double Area, double FlangeArea, string Formula, string Substitution, bool Overlap, bool Clipped, string FlangeSubstitution = "");

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Structural, "Column base plate (EN 1993-1-8)",
        "An unstiffened rectangular base plate under an I/H, RHS or CHS column on grouted concrete: bearing resistance and the plate thickness it needs, the tension T-stub and holding-down bolts and the compression flange T-stub for a major-axis moment, and shear by friction and bolts. The bolt tension per bolt goes to Hilti PROFIS for the anchorage, which is done outside this sheet.",
        "Bearing (cl. 6.2.5): f_cd = α_cc·f_ck/γ_c; f_jd = β_j·α·f_cd, β_j = 2/3; c = t_p·√[f_y/(3·f_jd·γ_M0)]\n" +
        "Axial: N_j,Rd = f_jd·A_eff, A_eff the bearing strips of width c around the column footprint (full rectangle when they overlap); u_N = N_c,Ed/N_j,Rd; t_req the smallest t_p in 10–100 mm with N_j,Rd ≥ N_c,Ed\n" +
        "Moment (cl. 6.2.8.3, simplified): C_Ed = (M_Ed + N_Ed·z_T)/z, T_Ed = max(C_Ed − N_Ed, 0) = max((M_Ed − N_Ed·z_C)/z, 0); symmetric z_C = z_T = z/2: M_Ed/z ± N_Ed/2; F_t,Ed = T_Ed/n_t; C_Rd = f_jd·A_flange\n" +
        "Plate T-stub (cl. 6.2.6.11, 6.2.4, Table 6.2): M_pl,Rd = 0.25·l_eff·t_p²·f_y/γ_M0; mode 1: 4·M_pl,Rd/m; mode 2: (2·M_pl,Rd + n·ΣF_t,Rd)/(m + n), n = min(e_min, 1.25m); mode 3: ΣF_t,Rd = n_t·F_t,Rd; without prying: mode 1-2 = 2·M_pl,Rd/m\n" +
        "Shear (cl. 6.2.2): V_Rd = C_f,d·N_c,Ed + n_b·F_vb,Rd, N_c,Ed = max(N_Ed, 0); u_V = V_Ed/V_Rd",
        [
            new CalcInputSpec("column", "Column section type", CalcInputKind.Choice, Choices: [ColumnIH, ColumnRhs, ColumnChs], DefaultChoice: ColumnIH, Group: "Column"),
            new CalcInputSpec("h", "Column depth, along the plate length L (I/H and RHS)", CalcInputKind.Quantity, "mm", Default: 260, Min: 50, Max: 1200, HardMin: 50, HardMax: 1200, IsOptional: true, Group: "Column"),
            new CalcInputSpec("b", "Column width, along the plate width B (I/H and RHS)", CalcInputKind.Quantity, "mm", Default: 256, Min: 50, Max: 1200, HardMin: 50, HardMax: 1200, IsOptional: true, Group: "Column"),
            new CalcInputSpec("t_f", "Flange thickness (I/H)", CalcInputKind.Quantity, "mm", Default: 17, Min: 3, Max: 100, HardMin: 3, HardMax: 100, IsOptional: true, Group: "Column"),
            new CalcInputSpec("t_w", "Web thickness (I/H)", CalcInputKind.Quantity, "mm", Default: 10, Min: 3, Max: 100, HardMin: 3, HardMax: 100, IsOptional: true, Group: "Column"),
            new CalcInputSpec("t", "Wall thickness (RHS and CHS)", CalcInputKind.Quantity, "mm", Default: 10, Min: 3, Max: 100, HardMin: 3, HardMax: 100, IsOptional: true, Group: "Column"),
            new CalcInputSpec("D", "Outside diameter (CHS)", CalcInputKind.Quantity, "mm", Default: 220, Min: 50, Max: 1200, HardMin: 50, HardMax: 1200, IsOptional: true, Group: "Column"),
            new CalcInputSpec("B", "Plate width", CalcInputKind.Quantity, "mm", Default: 400, Min: 100, Max: 1500, HardMin: 100, HardMax: 1500, Group: "Base plate"),
            new CalcInputSpec("L", "Plate length", CalcInputKind.Quantity, "mm", Default: 400, Min: 100, Max: 1500, HardMin: 100, HardMax: 1500, Group: "Base plate"),
            new CalcInputSpec("t_p", "Plate thickness", CalcInputKind.Quantity, "mm", Default: 20, Min: 10, Max: 100, HardMin: 10, HardMax: 100, Group: "Base plate"),
            new CalcInputSpec("f_y", "Plate yield strength", CalcInputKind.Quantity, "MPa", Default: 275, Min: 235, Max: 460, HardMin: 235, HardMax: 460, Group: "Materials"),
            new CalcInputSpec("f_ck", "Concrete characteristic cylinder strength", CalcInputKind.Quantity, "MPa", Default: 25, Min: 20, Max: 50, HardMin: 20, HardMax: 50, Group: "Materials"),
            new CalcInputSpec("alpha_cc", "α_cc, long-term effects on the concrete strength", CalcInputKind.Number, Default: 0.85, Min: 0.8, Max: 1, HardMin: 0.7, HardMax: 1, Group: "Factors"),
            new CalcInputSpec("gamma_c", "γ_c, partial factor for concrete", CalcInputKind.Number, Default: 1.5, Min: 1.4, Max: 1.6, HardMin: 1.2, HardMax: 1.8, Group: "Factors"),
            new CalcInputSpec("gamma_M0", "γ_M0, partial factor for the plate", CalcInputKind.Number, Default: 1.0, Min: 1, Max: 1.1, HardMin: 1, HardMax: 1.25, Group: "Factors"),
            new CalcInputSpec("C_fd", "C_f,d, friction coefficient plate to grout", CalcInputKind.Number, Default: 0.2, Min: 0.1, Max: 0.4, HardMin: 0, HardMax: 0.5, Group: "Factors"),
            new CalcInputSpec("alpha", "α, concentration factor √(A_c1/A_c0) of the foundation", CalcInputKind.Number, Default: 1.5, Min: 1, Max: 3, HardMin: 1, HardMax: 3, Group: "Foundation"),
            new CalcInputSpec("alpha_basis", "Foundation geometry that α rests on (state it when α > 1)", CalcInputKind.Text, IsOptional: true, Group: "Foundation"),
            new CalcInputSpec("N_Ed", "Axial force, compression positive", CalcInputKind.Quantity, "kN", Default: 300, Min: -5000, Max: 10000, HardMin: -5000, HardMax: 10000, Group: "Actions"),
            new CalcInputSpec("M_Ed", "Major-axis moment (uniaxial)", CalcInputKind.Quantity, "kN*m", Default: 120, Min: 0, Max: 2000, HardMin: 0, HardMax: 2000, Group: "Actions"),
            new CalcInputSpec("V_Ed", "Shear force", CalcInputKind.Quantity, "kN", Default: 80, Min: 0, Max: 2000, HardMin: 0, HardMax: 2000, Group: "Actions"),
            new CalcInputSpec("n_t", "Holding-down bolts on the tension side", CalcInputKind.Number, Default: 2, Min: 1, Max: 20, HardMin: 1, HardMax: 20, Group: "Holding-down bolts"),
            new CalcInputSpec("n_b", "Holding-down bolts in total (both sides)", CalcInputKind.Number, Default: 4, Min: 2, Max: 40, HardMin: 2, HardMax: 40, Group: "Holding-down bolts"),
            new CalcInputSpec("z", "Lever arm between the compression resultant and the tension bolt row", CalcInputKind.Quantity, "mm", Default: 300, Min: 50, Max: 2000, HardMin: 50, HardMax: 2000, Group: "Holding-down bolts"),
            new CalcInputSpec("z_C", "Compression resultant distance from the column axis (blank: z/2, symmetric)", CalcInputKind.Quantity, "mm", Min: 10, Max: 1000, HardMin: 10, HardMax: 1000, IsOptional: true, Group: "Holding-down bolts"),
            new CalcInputSpec("m", "Bolt to flange-weld toe distance, tension T-stub", CalcInputKind.Quantity, "mm", Default: 35, Min: 5, Max: 300, HardMin: 5, HardMax: 300, Group: "Holding-down bolts"),
            new CalcInputSpec("l_eff", "Effective length of the tension T-stub (cl. 6.2.6.5 patterns)", CalcInputKind.Quantity, "mm", Default: 200, Min: 10, Max: 2000, HardMin: 10, HardMax: 2000, Group: "Holding-down bolts"),
            new CalcInputSpec("e_min", "Edge distance e_min of the bolt row (limits the prying length n)", CalcInputKind.Quantity, "mm", Default: 50, Min: 5, Max: 500, HardMin: 5, HardMax: 500, Group: "Holding-down bolts"),
            new CalcInputSpec("prying", "Prying forces develop (bolts short enough, L_b ≤ L_b*)", CalcInputKind.Boolean, Default: 1, Group: "Holding-down bolts"),
            new CalcInputSpec("F_t_Rd", "Tension resistance per bolt (from S4)", CalcInputKind.Quantity, "kN", Default: 141, Min: 1, Max: 2000, HardMin: 1, HardMax: 2000, Group: "Holding-down bolts"),
            new CalcInputSpec("F_vb_Rd", "Shear resistance per bolt (from S4)", CalcInputKind.Quantity, "kN", Default: 94, Min: 1, Max: 2000, HardMin: 1, HardMax: 2000, Group: "Holding-down bolts"),
            new CalcInputSpec("profis_ref", "Hilti PROFIS report reference (the report is filed as Evidence)", CalcInputKind.Text, IsOptional: true, Group: "Anchorage"),
        ],
        [
            new CalcAssumption("Static loading; a major-axis moment only (uniaxial); an unstiffened rectangular plate on grout of adequate strength and thickness for β_j = 2/3.", "Scope of S6."),
            new CalcAssumption("The simplified T-stub model: the compression resultant acts at the centre of the compression flange T-stub and the tension at the bolt row; the plate is checked as a tension T-stub (modes 1, 2 and 3) at the bolts and the compression flange T-stub is checked against f_jd. The web is not given credit in the moment check.", "EN 1993-1-8 cl. 6.2.6.11."),
            new CalcAssumption("The effective length l_eff is the one the user reads from the cl. 6.2.6.5 patterns; it is used for modes 1 and 2 alike (l_eff,1 ≤ l_eff,2, so this is conservative for mode 2).", "One input."),
            new CalcAssumption("Concrete, anchor tension, pull-out, breakout, edge and shear-on-anchor are designed in Hilti PROFIS from the bolt tension per bolt that this sheet outputs; the report is filed as Evidence and referenced.", "External anchorage design."),
            new CalcAssumption("The friction resistance counts only the compression, C_f,d·max(N_Ed, 0); a column in net tension has none. Bolts are assumed to share shear equally.", "EN 1993-1-8 cl. 6.2.2."),
            new CalcAssumption("The bearing outstand c is cut back where it would pass the plate edge; the web and the column interior keep the full c. The CHS compression side is taken as half of the annular bearing area (an approximation).", "Conservative against an uncut c."),
            new CalcAssumption("Partial factors and the friction coefficient come from the National Annex records in use; the values shown default to the EN recommended and UK values.", "§A5 small factor set."),
        ],
        [
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.2", "Design resistance of column bases in shear"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.4, Table 6.2", "Equivalent T-stub in tension"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.5", "Equivalent T-stub in compression"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.6.11", "Base plate in bending and anchor bolts in tension"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.8.3", "Column bases subject to axial force and bending moment"),
            new CalcClauseRef("EN 1992-1-1:2004", "cl. 3.1.6, 6.7", "Design strength of concrete; partial area loading"),
        ],
        [
            new CalcApplicabilityRule("plate", "The column must stand inside the plate.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("section", "The wall or flange thickness must be less than half the section.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("bolts", "The bolt counts must be whole numbers with n_b at least 2·n_t.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("alpha_basis", "A concentration factor above 1 needs its foundation geometry stated.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("anchorage", "The PROFIS report reference is missing.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-ih-moment-bolts", "The 260×256 I-section with N = 300 kN, M = 120 kN·m, V = 80 kN, two M20-class bolts on the tension side of four",
                new CalcReferenceSource("Analytic (simplified T-stub model; recomputed independently)", "EN 1993-1-8:2005", "z = 300: C_Ed = 120/0.3 + 150 = 550 kN, T_Ed = 400 − 150 = 250 kN, F_t,Ed = 125 kN per bolt; A_flange = 42 482.5 mm², C_Rd = 601.84 kN; M_pl,Rd = 0.25·200·20²·275 = 5.5 kN·m; m = 35: mode 1 = 628.57 kN, n = min(50, 43.75) = 43.75, mode 2 = (2·5.5e6 + 43.75·282e3)/(78.75) = 296.35 kN, mode 3 = 2·141 = 282 kN → 282 kN; V_Rd = 0.2·300 + 4·94 = 436 kN"),
                [Choice("column", "I/H"), Q("h", 260, "mm"), Q("b", 256, "mm"), Q("t_f", 17, "mm"), Q("t_w", 10, "mm"), Q("B", 400, "mm"), Q("L", 400, "mm"), Q("t_p", 20, "mm"), Q("f_y", 275, "MPa"), Q("f_ck", 25, "MPa"), Q("N_Ed", 300, "kN"), Q("M_Ed", 120, "kN*m"), Q("V_Ed", 80, "kN"), N("n_t", 2), N("n_b", 4), Q("z", 300, "mm"), Q("m", 35, "mm"), Q("l_eff", 200, "mm"), Q("e_min", 50, "mm"), Q("F_t_Rd", 141, "kN"), Q("F_vb_Rd", 94, "kN"), N("C_fd", 0.2)],
                [new CalcExpectedValue("C_Ed", 550, "kN", 1e-9), new CalcExpectedValue("T_Ed", 250, "kN", 1e-9), new CalcExpectedValue("F_t_Ed", 125, "kN", 1e-9), new CalcExpectedValue("C_Rd", 601.8358193, "kN", 1e-6), new CalcExpectedValue("u_C", 0.9138704982, "", 1e-6), new CalcExpectedValue("M_pl_Rd", 5.5, "kN*m", 1e-9), new CalcExpectedValue("T_Rd_1", 628.5714286, "kN", 1e-6), new CalcExpectedValue("T_Rd_2", 296.3492063, "kN", 1e-6), new CalcExpectedValue("T_Rd_3", 282, "kN", 1e-9), new CalcExpectedValue("T_Rd", 282, "kN", 1e-9), new CalcExpectedValue("u_T", 0.8865248227, "", 1e-6), new CalcExpectedValue("F_f_Rd", 60, "kN", 1e-9), new CalcExpectedValue("V_Rd", 436, "kN", 1e-9), new CalcExpectedValue("u_V", 0.1834862385, "", 1e-6), new CalcExpectedValue("u_max", 0.9138704982, "", 1e-6)]),
            new CalcTestCase("ka-ih-axial", "UC-like I-section 260×256, t_f 17, t_w 10, on a 400×400×20 S275 plate, C25/30, axial 1200 kN",
                new CalcReferenceSource("Analytic (EN 1993-1-8 cl. 6.2.5 relations, recomputed independently)", "EN 1993-1-8:2005", "f_cd = 0.85·25/1.5 = 14.1667; f_jd = (2/3)·1.5·14.1667 = 14.1667 MPa; c = 20·√(275/(3·14.1667·1)) = 50.8747 mm; A_eff = 2·(256 + 101.749)(17 + 101.749) + (260 − 34 − 101.749)(10 + 101.749) = 98 849.99 mm²; N_j,Rd = 14.1667·98 849.99 = 1400.37 kN; t_req = 17.0754 mm"),
                [Choice("column", "I/H"), Q("h", 260, "mm"), Q("b", 256, "mm"), Q("t_f", 17, "mm"), Q("t_w", 10, "mm"), Q("B", 400, "mm"), Q("L", 400, "mm"), Q("t_p", 20, "mm"), Q("f_y", 275, "MPa"), Q("f_ck", 25, "MPa"), N("alpha_cc", 0.85), N("gamma_c", 1.5), N("gamma_M0", 1), N("alpha", 1.5), Q("N_Ed", 1200, "kN"), Q("M_Ed", 0, "kN*m"), Q("V_Ed", 0, "kN")],
                [new CalcExpectedValue("f_cd", 14.16666667, "MPa", 1e-6), new CalcExpectedValue("f_jd", 14.16666667, "MPa", 1e-6), new CalcExpectedValue("c", 50.87470191, "mm", 1e-6), new CalcExpectedValue("A_eff", 98849.98688, "mm^2", 1e-6), new CalcExpectedValue("N_j_Rd", 1400.374814, "kN", 1e-6), new CalcExpectedValue("u_N", 0.8569134405, "", 1e-6), new CalcExpectedValue("t_req", 17.0753989, "mm", 1e-6), new CalcExpectedValue("F_t_Ed", 0, "kN", 1e-9)]),
            new CalcTestCase("ka-rhs-axial", "RHS 200×200×10 on a 350×350×20 S275 plate, C25/30, axial 900 kN",
                new CalcReferenceSource("Analytic (EN 1993-1-8 cl. 6.2.5 relations)", "EN 1993-1-8:2005", "c = 50.8747 mm; A_eff = (200 + 101.749)² − (200 − 20 − 101.749)² = 91 052.70 − 6 123.16 = 84 929.55 mm²; N_j,Rd = 14.1667·84 929.55 = 1203.17 kN; t_req = 14.4652 mm"),
                [Choice("column", ColumnRhs), Q("h", 200, "mm"), Q("b", 200, "mm"), Q("t", 10, "mm"), Q("B", 350, "mm"), Q("L", 350, "mm"), Q("t_p", 20, "mm"), Q("f_y", 275, "MPa"), Q("f_ck", 25, "MPa"), Q("N_Ed", 900, "kN"), Q("M_Ed", 0, "kN*m"), Q("V_Ed", 0, "kN")],
                [new CalcExpectedValue("A_eff", 84929.5469, "mm^2", 1e-6), new CalcExpectedValue("N_j_Rd", 1203.168581, "kN", 1e-6), new CalcExpectedValue("u_N", 0.7480248522, "", 1e-6), new CalcExpectedValue("t_req", 14.4652113, "mm", 1e-6)]),
            new CalcTestCase("ka-chs-axial", "CHS 220×10 on a 350×350×20 S275 plate, C25/30, axial 800 kN",
                new CalcReferenceSource("Analytic (EN 1993-1-8 cl. 6.2.5 relations)", "EN 1993-1-8:2005", "c = 50.8747 mm; A_eff = π[(110 + 50.8747)² − (110 − 10 − 50.8747)²] = 73 724.93 mm²; N_j,Rd = 14.1667·73 724.93 = 1044.44 kN; t_req = 14.8592 mm"),
                [Choice("column", ColumnChs), Q("D", 220, "mm"), Q("t", 10, "mm"), Q("B", 350, "mm"), Q("L", 350, "mm"), Q("t_p", 20, "mm"), Q("f_y", 275, "MPa"), Q("f_ck", 25, "MPa"), Q("N_Ed", 800, "kN"), Q("M_Ed", 0, "kN*m"), Q("V_Ed", 0, "kN")],
                [new CalcExpectedValue("A_eff", 73724.93227, "mm^2", 1e-6), new CalcExpectedValue("N_j_Rd", 1044.436541, "kN", 1e-6), new CalcExpectedValue("u_N", 0.7659632433, "", 1e-6), new CalcExpectedValue("t_req", 14.85923906, "mm", 1e-6)]),
            new CalcTestCase("ka-ih-overlap-clip", "Small I-section 150×150 on a 300×400×40 plate: the strips overlap and the outstand across the width is cut back to the plate edge",
                new CalcReferenceSource("Analytic (EN 1993-1-8 cl. 6.2.5 relations, outstand cut at the plate edge)", "EN 1993-1-8:2005", "c = 40·√(275/(3·14.1667)) = 101.749 mm; h − 2t_f − 2c < 0 so the full rectangle is used; the outstands are min(c, 75) = 75 mm across the width and min(c, 125) = 101.749 mm along the length: A_eff = (150 + 150)(150 + 203.499) = 106 049.64 mm² (the uncut rectangle clipped to B·L would be 120 000 mm²); N_j,Rd = 1502.37 kN"),
                [Choice("column", "I/H"), Q("h", 150, "mm"), Q("b", 150, "mm"), Q("t_f", 10, "mm"), Q("t_w", 6, "mm"), Q("B", 300, "mm"), Q("L", 400, "mm"), Q("t_p", 40, "mm"), Q("f_y", 275, "MPa"), Q("f_ck", 25, "MPa"), Q("N_Ed", 1000, "kN"), Q("M_Ed", 0, "kN*m"), Q("V_Ed", 0, "kN")],
                [new CalcExpectedValue("c", 101.7494038, "mm", 1e-6), new CalcExpectedValue("A_eff", 106049.6423, "mm^2", 1e-6), new CalcExpectedValue("N_j_Rd", 1502.369932, "kN", 1e-6)]),
        ],
        Category: "Connections",
        OutputPorts:
        [
            new CalcOutputPort("F_t_Ed", "kN", "Holding-down bolt tension per bolt (→ Hilti PROFIS)"),
            new CalcOutputPort("N_j_Rd", "kN", "Axial bearing resistance of the joint"),
            new CalcOutputPort("u_max", "", "Governing utilisation"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("ec2.national-annex", "EN 1992-1-1 National Annex factors", ["alpha_cc", "gamma_c"], "From the National Annex in use, with its edition (UK NA to EN 1992-1-1 for α_cc)."),
            new CalcRecordKind("ec3.national-annex", "EN 1993-1-8 National Annex factors", ["gamma_M0", "C_fd"], "From the National Annex in use, with its edition; C_f,d is the friction coefficient for the grout used."),
            new CalcRecordKind("steel.strengths", "Plate strength", ["f_y"], "From the product standard for the grade and thickness (EN 10025-2)."),
        ],
        IndependentCheck: true);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue N(string symbol, double value) => CalcInputValue.Of(symbol, value);

    private static CalcInputValue Choice(string symbol, string value) => CalcInputValue.Choice(symbol, value);

    /// <summary>c (mm) for a plate thickness: c = t_p·√[f_y/(3·f_jd·γ_M0)], f_y and f_jd in MPa.</summary>
    public static double Outstand(double tp, double fy, double fjd, double gammaM0) => tp * Math.Sqrt(fy / (3 * fjd * gammaM0));

    /// <summary>
    /// The effective bearing area (mm²) of the footprint for an outstand <paramref name="c"/> on a plate B × L. The strips run
    /// round the column; where they overlap the full rectangle is used. The outstand across the plate edge is cut back to the
    /// edge, so no area outside the plate is counted. Also returns the area of one compression flange T-stub.
    /// </summary>
    public static BearingArea Effective(Section s, double c, double plateB, double plateL)
    {
        var plate = plateB * plateL;

        switch (s.Column)
        {
            case ColumnRhs:
            {
                var cb = Math.Max(0, Math.Min(c, (plateB - s.B) / 2));
                var ch = Math.Max(0, Math.Min(c, (plateL - s.H) / 2));
                var ib = Math.Max(0, s.B - 2 * s.T - 2 * c);
                var ih = Math.Max(0, s.H - 2 * s.T - 2 * c);
                var area = Math.Min((s.B + 2 * cb) * (s.H + 2 * ch) - ib * ih, plate);
                var flange = Math.Min((s.B + 2 * cb) * (s.T + ch + c), area / 2);
                return new BearingArea(area, flange, "(b + 2c)(h + 2c) − (b − 2t − 2c)(h − 2t − 2c), inner term ≥ 0" + Cut(cb < c || ch < c),
                    $"({F(s.B + 2 * cb)})({F(s.H + 2 * ch)}) − ({F(ib)})({F(ih)})", false, cb < c || ch < c, $"min({F(s.B + 2 * cb)} × {F(s.T + ch + c)}, A_eff/2)");
            }

            case ColumnChs:
            {
                var co = Math.Max(0, Math.Min(c, (Math.Min(plateB, plateL) - s.D) / 2));
                var inner = Math.Max(0, s.D / 2 - s.T - c);
                var area = Math.Min(Math.PI * ((s.D / 2 + co) * (s.D / 2 + co) - inner * inner), plate);
                return new BearingArea(area, area / 2, "π[(D/2 + c)² − (D/2 − t − c)²], inner term ≥ 0" + Cut(co < c),
                    $"π[({F(s.D / 2 + co)})² − ({F(inner)})²]", false, co < c, "A_eff/2");
            }

            default:
            {
                var cb = Math.Max(0, Math.Min(c, (plateB - s.B) / 2));
                var ch = Math.Max(0, Math.Min(c, (plateL - s.H) / 2));
                var clipped = cb < c || ch < c;
                var web = s.H - 2 * s.Tf - 2 * c;

                if (web < 0)
                {
                    var rect = Math.Min((s.B + 2 * cb) * (s.H + 2 * ch), plate);
                    var flangeRect = Math.Min((s.B + 2 * cb) * (s.Tf + ch + c), rect / 2);
                    return new BearingArea(rect, flangeRect, "(b + 2c)(h + 2c): the strips overlap, so the full rectangle is used" + Cut(clipped), $"({F(s.B + 2 * cb)})({F(s.H + 2 * ch)})", true, clipped, $"min({F(s.B + 2 * cb)} × {F(s.Tf + ch + c)}, A_eff/2)");
                }

                var flangeStrip = (s.B + 2 * cb) * (s.Tf + ch + c);
                var webStrip = Math.Min(s.Tw + 2 * c, s.B + 2 * cb) * web;
                var total = Math.Min(2 * flangeStrip + webStrip, plate);
                return new BearingArea(total, Math.Min(flangeStrip, total / 2), "2(b + 2c)(t_f + 2c) + (h − 2t_f − 2c)(t_w + 2c)" + Cut(clipped), $"2 × {F(s.B + 2 * cb)} × {F(s.Tf + ch + c)} + {F(web)} × {F(Math.Min(s.Tw + 2 * c, s.B + 2 * cb))}", false, clipped, $"min({F(s.B + 2 * cb)} × {F(s.Tf + ch + c)}, A_eff/2)");
            }
        }
    }

    /// <summary>
    /// The smallest plate thickness in 10–100 mm whose bearing resistance f_jd·A_eff(c(t)) carries <paramref name="nEdKn"/>, by
    /// bisection; <see cref="MinThickness"/> when it is carried already at 10 mm, <see langword="null"/> when 100 mm is not enough.
    /// </summary>
    public static double? RequiredThickness(Section s, double plateB, double plateL, double fy, double fjd, double gammaM0, double nEdKn)
    {
        bool Carries(double t) => fjd * Effective(s, Outstand(t, fy, fjd, gammaM0), plateB, plateL).Area / 1e3 >= nEdKn;

        if (nEdKn <= 0 || Carries(MinThickness))
            return MinThickness;

        if (!Carries(MaxThickness))
            return null;

        double lo = MinThickness, hi = MaxThickness;

        for (var i = 0; i < 200; i++)
        {
            var mid = (lo + hi) / 2;

            if (Carries(mid))
                hi = mid;
            else
                lo = mid;
        }

        return hi;
    }

    public void Run(CalcRunContext c)
    {
        foreach (var s in new[] { "B", "L", "t_p", "f_y", "f_ck", "alpha_cc", "gamma_c", "gamma_M0", "C_fd", "alpha", "N_Ed", "M_Ed", "V_Ed", "n_t", "n_b", "z", "m", "l_eff", "e_min", "F_t_Rd", "F_vb_Rd" })
            c.Input(s);

        var column = c.InputText("column");
        var plateB = c.Si("B") * 1e3;
        var plateL = c.Si("L") * 1e3;
        var section = new Section(column, 0, 0, 0, 0, 0, 0);

        switch (column)
        {
            case ColumnChs:
                if (!Need(c, column, "D", "t"))
                    return;

                c.Input("D");
                c.Input("t");
                section = section with { D = c.Si("D") * 1e3, T = c.Si("t") * 1e3 };

                if (!(2 * section.T < section.D))
                {
                    c.Block("section", $"t: twice the wall thickness ({F(2 * section.T)} mm) must be less than the diameter D ({F(section.D)} mm).");
                    return;
                }

                if (section.D > Math.Min(plateB, plateL))
                {
                    c.Block("plate", $"D: the column diameter ({F(section.D)} mm) must not exceed the plate (B = {F(plateB)}, L = {F(plateL)} mm).");
                    return;
                }

                break;

            case ColumnRhs:
                if (!Need(c, column, "h", "b", "t"))
                    return;

                foreach (var s in new[] { "h", "b", "t" })
                    c.Input(s);

                section = section with { H = c.Si("h") * 1e3, B = c.Si("b") * 1e3, T = c.Si("t") * 1e3 };

                if (!(2 * section.T < Math.Min(section.H, section.B)))
                {
                    c.Block("section", $"t: twice the wall thickness ({F(2 * section.T)} mm) must be less than the smaller of h and b ({F(Math.Min(section.H, section.B))} mm).");
                    return;
                }

                break;

            default:
                if (!Need(c, column, "h", "b", "t_f", "t_w"))
                    return;

                foreach (var s in new[] { "h", "b", "t_f", "t_w" })
                    c.Input(s);

                section = section with { H = c.Si("h") * 1e3, B = c.Si("b") * 1e3, Tf = c.Si("t_f") * 1e3, Tw = c.Si("t_w") * 1e3 };

                if (!(2 * section.Tf < section.H))
                {
                    c.Block("section", $"t_f: twice the flange thickness ({F(2 * section.Tf)} mm) must be less than the section depth h ({F(section.H)} mm).");
                    return;
                }

                if (section.Tw > section.B)
                {
                    c.Block("section", $"t_w: the web thickness ({F(section.Tw)} mm) must not exceed the flange width b ({F(section.B)} mm).");
                    return;
                }

                break;
        }

        if (column != ColumnChs && (section.H > plateL || section.B > plateB))
        {
            c.Block("plate", $"h: the column ({F(section.H)} × {F(section.B)} mm) must stand inside the plate (L = {F(plateL)}, B = {F(plateB)} mm).");
            return;
        }

        var nT = c.InputSi("n_t");
        var nB = c.InputSi("n_b");

        if (Math.Abs(nT - Math.Round(nT)) > 1e-9 || Math.Abs(nB - Math.Round(nB)) > 1e-9)
        {
            c.Block("bolts", "n_t: the bolt counts must be whole numbers.");
            return;
        }

        if (nB < 2 * nT)
        {
            c.Block("bolts", $"n_b: the total ({F(nB)}) must be at least twice the bolts on the tension side ({F(nT)}).");
            return;
        }

        var z = c.Si("z") * 1e3;
        var hasZc = c.Has("z_C");

        if (hasZc)
        {
            c.Input("z_C");

            if (!(c.Si("z_C") * 1e3 < z))
            {
                c.Block("z_C", $"z_C: the compression resultant distance ({F(c.Si("z_C") * 1e3)} mm) must be less than the lever arm z ({F(z)} mm).");
                return;
            }
        }

        var prying = c.InputFlag("prying");
        var kN = Engine.Units.UnitCatalog.Parse("kN");
        var mm = Engine.Units.UnitCatalog.Parse("mm");

        // Bearing strength and the outstand c.
        c.Assign("f_cd", "alpha_cc * f_ck / gamma_c", "MPa", "Concrete design compressive strength", "EN 1992-1-1 cl. 3.1.6");
        c.Record("beta_j", "β_j, fixed", "grouted joint", new ScalarValue(BetaJ), "Joint coefficient β_j = 2/3 (grout conditions assumed met)", "EN 1993-1-8 cl. 6.2.5");
        c.Assign("f_jd", "beta_j * alpha * f_cd", "MPa", "Design bearing strength of the joint", "EN 1993-1-8 cl. 6.2.5");
        c.Assign("c", "t_p * sqrt(f_y / (3 * f_jd * gamma_M0))", "mm", "Additional bearing width round the column footprint", "EN 1993-1-8 cl. 6.2.5");

        var fy = c.Si("f_y") / 1e6;
        var fjd = c.Si("f_jd") / 1e6;
        var gM0 = c.InputSi("gamma_M0");
        var tp = c.Si("t_p") * 1e3;
        var cMm = c.Si("c") * 1e3;
        var bearing = Effective(section, cMm, plateB, plateL);
        c.Record("A_eff", bearing.Formula, bearing.Substitution + " mm", ScalarValue.Of(bearing.Area, "mm^2"), "Effective bearing area", "EN 1993-1-8 cl. 6.2.5");

        if (bearing.Overlap)
            c.Assume("The bearing strips round the column overlap, so the full rectangle (b + 2c)(h + 2c) is used for A_eff.", "EN 1993-1-8 cl. 6.2.5.");

        if (bearing.Clipped)
            c.Assume("The outstand c is cut back to the plate edge where it would pass it, so no area outside the plate is counted.", "Conservative against an uncut c.");

        c.Assign("N_j_Rd", "f_jd * A_eff", "kN", "Axial bearing resistance of the joint", "EN 1993-1-8 cl. 6.2.5");
        c.Record("N_c_Ed", "max(N_Ed, 0)", $"N_Ed = {F(c.Si("N_Ed") / 1e3)} kN", ScalarValue.Of(Math.Max(c.Si("N_Ed"), 0) / 1e3, kN), "Design compression in the column");
        c.Assign("u_N", "N_c_Ed / N_j_Rd", null, "Axial bearing utilisation");

        var nCompression = Math.Max(c.Si("N_Ed"), 0) / 1e3;
        var outputs = new List<(string, string)> { ("f_cd", "Concrete design strength"), ("beta_j", "Joint coefficient β_j"), ("f_jd", "Bearing strength f_jd"), ("c", "Bearing outstand c"), ("A_eff", "Effective bearing area"), ("N_j_Rd", "Axial bearing resistance"), ("u_N", "Axial bearing utilisation") };
        var required = RequiredThickness(section, plateB, plateL, fy, fjd, gM0, nCompression);
        TextValue? tReqText = null;

        if (nCompression <= 0)
            tReqText = new TextValue("— (no compression)");
        else if (required is null)
            tReqText = new TextValue("> 100 mm");
        else
            c.Record("t_req", "smallest t_p in 10–100 mm with f_jd·A_eff(c(t_p)) ≥ N_c,Ed (bisection)", $"N_c,Ed = {F(nCompression)} kN" + (required <= MinThickness + 1e-9 ? "; carried at the 10 mm minimum" : string.Empty), ScalarValue.Of(required.Value, mm), "Plate thickness required for bearing", "EN 1993-1-8 cl. 6.2.5");

        // Moment: compression resultant and tension at the bolt row.
        var nEd = c.Si("N_Ed") / 1e3;
        var mEd = c.Si("M_Ed") / 1e3;
        var zC = hasZc ? c.Si("z_C") * 1e3 : z / 2;
        var zT = z - zC;
        var cEd = (mEd * 1e3 + nEd * zT) / z;
        var tEd = Math.Max(cEd - nEd, 0);
        c.Record("z_T", "z − z_C", $"{F(z)} − {F(zC)}" + (hasZc ? string.Empty : " (symmetric, z_C = z/2)"), ScalarValue.Of(zT, mm), "Tension bolt row distance from the column axis");
        c.Record("C_Ed", "(M_Ed + N_Ed·z_T)/z", $"({F(mEd)} kN·m + {F(nEd)} kN × {F(zT)} mm)/{F(z)} mm", ScalarValue.Of(cEd, kN), "Compression resultant under the compression flange", "EN 1993-1-8 cl. 6.2.8.3");
        c.Record("T_Ed", "max(C_Ed − N_Ed, 0) = max((M_Ed − N_Ed·z_C)/z, 0)", $"max({F(cEd)} − {F(nEd)}, 0)", ScalarValue.Of(tEd, kN), "Tension resultant at the bolt row", "EN 1993-1-8 cl. 6.2.8.3");
        c.Assign("F_t_Ed", "T_Ed / n_t", "kN", "Holding-down bolt tension per bolt (→ PROFIS)", "EN 1993-1-8 cl. 6.2.6.11");
        outputs.AddRange([("z_T", "Tension row distance z_T"), ("C_Ed", "Compression resultant"), ("T_Ed", "Tension resultant"), ("F_t_Ed", "Bolt tension per bolt (to PROFIS)")]);
        var utilisations = new List<string> { "u_N" };

        if (mEd > 0)
        {
            c.Record("A_flange", "(b + 2c)(t_f + 2c) for the compression flange; RHS (b + 2c)(t + 2c); CHS half of A_eff", $"{bearing.FlangeSubstitution} mm, c = {F(cMm)} mm", ScalarValue.Of(bearing.FlangeArea, "mm^2"), "Bearing area of the compression flange T-stub", "EN 1993-1-8 cl. 6.2.6.11");
            c.Assign("C_Rd", "f_jd * A_flange", "kN", "Compression flange T-stub bearing resistance", "EN 1993-1-8 cl. 6.2.6.11");
            c.Record("C_Ed_pos", "max(C_Ed, 0)", $"C_Ed = {F(cEd)} kN", ScalarValue.Of(Math.Max(cEd, 0), kN), "Compression resultant carried by the flange");
            c.Assign("u_C", "C_Ed_pos / C_Rd", null, "Compression flange T-stub utilisation");
            outputs.AddRange([("A_flange", "Compression flange area"), ("C_Rd", "Compression flange resistance"), ("u_C", "Compression flange utilisation")]);
            utilisations.Add("u_C");
        }

        // Tension T-stub of the plate at the bolts.
        c.Assign("M_pl_Rd", "0.25 * l_eff * t_p^2 * f_y / gamma_M0", "kN*m", "Plastic moment resistance of the plate over l_eff", "EN 1993-1-8 Table 6.2");
        var modes = new List<string>();

        if (prying)
        {
            var n = Math.Min(c.Si("e_min"), 1.25 * c.Si("m")) * 1e3;
            c.Assign("T_Rd_1", "4 * M_pl_Rd / m", "kN", "Mode 1: plate yields, no bolt failure", "EN 1993-1-8 Table 6.2");
            c.Record("n_pry", "min(e_min, 1.25·m)", $"min({F(c.Si("e_min") * 1e3)}, 1.25 × {F(c.Si("m") * 1e3)}) mm", ScalarValue.Of(n, mm), "Prying length n", "EN 1993-1-8 Table 6.2");
            c.Assign("T_Rd_2", "(2 * M_pl_Rd + n_pry * n_t * F_t_Rd) / (m + n_pry)", "kN", "Mode 2: plate yields and bolts fail", "EN 1993-1-8 Table 6.2");
            c.Assign("T_Rd_3", "n_t * F_t_Rd", "kN", "Mode 3: bolt failure", "EN 1993-1-8 Table 6.2");
            c.Assign("T_Rd", "min(T_Rd_1, T_Rd_2, T_Rd_3)", "kN", "Tension T-stub resistance", "EN 1993-1-8 cl. 6.2.4");
            modes.AddRange(["T_Rd_1", "T_Rd_2", "T_Rd_3"]);
            outputs.AddRange([("M_pl_Rd", "Plate plastic moment"), ("T_Rd_1", "T-stub mode 1"), ("n_pry", "Prying length n"), ("T_Rd_2", "T-stub mode 2"), ("T_Rd_3", "T-stub mode 3"), ("T_Rd", "Tension T-stub resistance")]);
        }
        else
        {
            c.Assign("T_Rd_1", "2 * M_pl_Rd / m", "kN", "Mode 1-2 without prying forces", "EN 1993-1-8 Table 6.2");
            c.Assign("T_Rd_3", "n_t * F_t_Rd", "kN", "Mode 3: bolt failure", "EN 1993-1-8 Table 6.2");
            c.Assign("T_Rd", "min(T_Rd_1, T_Rd_3)", "kN", "Tension T-stub resistance (no prying)", "EN 1993-1-8 cl. 6.2.4");
            modes.AddRange(["T_Rd_1", "T_Rd_3"]);
            outputs.AddRange([("M_pl_Rd", "Plate plastic moment"), ("T_Rd_1", "T-stub mode 1-2, no prying"), ("T_Rd_3", "T-stub mode 3"), ("T_Rd", "Tension T-stub resistance")]);
        }

        c.Assign("u_T", "T_Ed / T_Rd", null, "Plate bending and bolts in tension, T-stub utilisation");
        c.Assign("u_bt", "F_t_Ed / F_t_Rd", null, "Holding-down bolt tension utilisation");
        outputs.AddRange([("u_T", "Tension T-stub utilisation"), ("u_bt", "Bolt tension utilisation")]);
        utilisations.AddRange(["u_T", "u_bt"]);
        var governingMode = modes.MinBy(sym => c.Si(sym))!;
        var modeName = governingMode switch { "T_Rd_1" => prying ? "mode 1 (plate yielding)" : "mode 1-2 (plate yielding, no prying)", "T_Rd_2" => "mode 2 (plate yielding and bolt failure)", _ => "mode 3 (bolt failure)" };

        // Shear: friction on the compression plus the bolts.
        c.Assign("F_f_Rd", "C_fd * N_c_Ed", "kN", "Friction resistance, compression only", "EN 1993-1-8 cl. 6.2.2");
        c.Assign("V_Rd", "F_f_Rd + n_b * F_vb_Rd", "kN", "Shear resistance of the base", "EN 1993-1-8 cl. 6.2.2");
        c.Assign("u_V", "V_Ed / V_Rd", null, "Shear utilisation");
        outputs.AddRange([("F_f_Rd", "Friction resistance"), ("V_Rd", "Shear resistance"), ("u_V", "Shear utilisation")]);
        utilisations.Add("u_V");

        var uMax = utilisations.Max(sym => c.Si(sym));
        c.Record("u_max", "max(" + string.Join(", ", utilisations) + ")", string.Join(", ", utilisations.Select(sym => $"{sym} = {F(c.Si(sym))}")), new ScalarValue(uMax), "Governing utilisation");
        outputs.Add(("u_max", "Governing utilisation"));

        foreach (var (symbol, text) in outputs)
        {
            c.Output(symbol, text);

            if (symbol == "u_N")
            {
                if (tReqText is not null)
                    c.Output("t_req", "Plate thickness required for bearing", tReqText);
                else
                    c.Output("t_req", "Plate thickness required for bearing");
            }
        }

        c.Output("T_mode", "Governing T-stub mode", new TextValue(modeName));

        // Warnings and checks.
        var basis = c.Has("alpha_basis") && !string.IsNullOrWhiteSpace(c.InputText("alpha_basis"));

        if (c.Si("alpha") > 1 + 1e-9 && !basis)
            c.Warn("alpha_basis", "alpha_basis: α above 1 needs the foundation geometry it rests on (A_c1/A_c0, α ≤ 3, EN 1992-1-1 cl. 6.7) stated.");

        c.CheckUtilisation("Concrete bearing under the axial force", "u_N", "EN 1993-1-8 cl. 6.2.5");

        if (mEd > 0)
            c.CheckUtilisation("Compression flange T-stub bearing", "u_C", "EN 1993-1-8 cl. 6.2.6.11");

        c.CheckUtilisation("Base plate tension T-stub (bending at the bolts)", "u_T", "EN 1993-1-8 cl. 6.2.6.11");
        c.CheckUtilisation("Holding-down bolt tension", "u_bt", "EN 1993-1-8 Table 3.4");
        c.CheckUtilisation("Shear: friction plus bolts", "u_V", "EN 1993-1-8 cl. 6.2.2");

        if (c.Has("profis_ref") && !string.IsNullOrWhiteSpace(c.InputText("profis_ref")))
            c.CheckCondition("Anchorage designed externally (PROFIS report filed as Evidence)", true, $"PROFIS report {c.InputText("profis_ref").Trim()}", $"bolt tension {F(c.Si("F_t_Ed") / 1e3)} kN per bolt designed in PROFIS");
        else
            c.Warn("anchorage", "profis_ref: the anchorage is designed outside this sheet; attach the Hilti PROFIS report as Evidence and enter its reference.");
    }

    /// <summary>Whether the dimensions a column type needs are entered; blocks naming the first missing one when not.</summary>
    private static bool Need(CalcRunContext c, string column, params string[] symbols)
    {
        var missing = symbols.FirstOrDefault(sym => !c.Has(sym));

        if (missing is null)
            return true;

        c.Block("section", $"{missing}: a {column} column needs {string.Join(", ", symbols)}; {missing} is not entered.");
        return false;
    }

    private static string Cut(bool cut) => cut ? " (outstand cut back to the plate edge)" : string.Empty;

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
