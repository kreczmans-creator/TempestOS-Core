using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Structural;

/// <summary>
/// S6 — an unstiffened rectangular steel base plate under an I/H, RHS or CHS column on a concrete foundation, to
/// EN 1993-1-8 cl. 6.2 (simplified): the bearing strength f_jd and effective bearing area, the axial resistance
/// N_j,Rd, the three Table 6.7 force regimes for a major-axis moment (tension at one bolt row, both sides in
/// compression, both sides in tension), the tension T-stub of the plate and the holding-down bolts, the compression
/// T-stub, shear by friction plus anchor bolts (cl. 6.2.2), and the plate thickness that satisfies bearing, the
/// compression T-stub and the tension T-stub together. The concrete and anchor design is not done here: the bolt
/// tension and shear per anchor are output ports for Hilti PROFIS, and the PROFIS report is filed as Evidence.
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

    /// <summary>The thickness, mm, above which EN 10025-2 lowers the yield strength of the usual grades (the first step).</summary>
    public const double FirstYieldStepMm = 16;

    /// <summary>The column footprint, mm: depth h along the plate length L, width b along the plate width B.</summary>
    public readonly record struct Section(string Column, double H, double B, double Tf, double Tw, double T, double D)
    {
        /// <summary>Half the column depth along L: h/2, or D/2 for a CHS.</summary>
        public double HalfDepth => Column == ColumnChs ? D / 2 : H / 2;
    }

    /// <summary>The effective bearing area for one value of c, the compression-side area, its centroid and the text that shows how they were made.</summary>
    public sealed record BearingArea(
        double Area, double FlangeArea, string Formula, string Substitution, bool Overlap, bool Clipped,
        string FlangeFormula = "", string FlangeSubstitution = "", double Centroid = 0, string CentroidFormula = "", string CentroidSubstitution = "");

    /// <summary>The Table 6.7 regime and the resultants, kN: C at +z_C, C2 at −z_C, T at the bolt row at −x_b, T2 at the row at +x_b.</summary>
    public readonly record struct Forces(int Regime, double C, double C2, double T, double T2);

    /// <summary>The tension T-stub resistances, kN (mode values are NaN when not applicable); Mpl in kN·m, N the prying length in mm.</summary>
    public readonly record struct TStub(double Mpl, double Mode1, double Mode2, double Mode3, double Resistance, double N);

    /// <summary>The required plate thickness: a number in 10–100 mm, or the reason there is none.</summary>
    public readonly record struct Required(double? Value, string? Text);

    public CalcTemplate Template { get; } = new(
        Id, "1.5.0", CalcDiscipline.Structural, "Column base plate (EN 1993-1-8)",
        "An unstiffened rectangular base plate under an I/H, RHS or CHS column on grouted concrete: bearing resistance, the force regime and holding-down bolt tension for a major-axis moment, the tension T-stub of the plate and the compression T-stub, shear by friction and anchor bolts, and the plate thickness that satisfies them together. The bolt tension and shear per anchor go to Hilti PROFIS for the anchorage, which is done outside this sheet.",
        "Bearing (cl. 6.2.5): f_cd = α_cc·f_ck/γ_c; f_jd = β_j·α·f_cd, β_j = 2/3; c = t_p·√[f_y/(3·f_jd·γ_M0)]\n" +
        "Axial (cl. 6.2.8.2): N_j,Rd = f_jd·A_eff, A_eff the bearing strips of width c round the column footprint (full rectangle when they overlap); u_N = N_c,Ed/N_j,Rd\n" +
        "Moment (cl. 6.2.8.3, Table 6.7): z_C from the section ((h − t_f)/2, (h − t)/2, half-annulus centroid) or entered, x_b the bolt row from the axis, z = z_C + x_b; C_Ed = (M_Ed + N_Ed·x_b)/z and T_Ed = C_Ed − N_Ed when both are ≥ 0; with N_Ed > 0 and T_Ed < 0 both sides are in compression, C = N_Ed/2 ± M_Ed/(2z_C); with N_Ed < 0 and C_Ed < 0 both rows are in tension, T = −N_Ed/2 ± M_Ed/(2x_b); F_t,Ed = T_Ed/n_t at the more loaded row\n" +
        "Plate T-stub (cl. 6.2.6.11, 6.2.4, Table 6.2): M_pl,Rd = 0.25·l_eff·t_p²·f_y/γ_M0; without prying min(2·M_pl,Rd/m, n_t·F_t,Rd); with prying the modes 4·M_pl,Rd/m, (2·M_pl,Rd + n·ΣF_t,Rd)/(m + n) with n = min(e_min, 1.25m), and n_t·F_t,Rd; l_eff is the total for the n_t bolts\n" +
        "Compression T-stub (cl. 6.2.6.9, 6.2.6.10): C_Rd = f_jd·A_flange, checked when M_Ed > 0 and the resultant is in compression\n" +
        "Shear (cl. 6.2.2): F_2,vb,Rd = α_bc·f_ub·A_s/γ_M2, α_bc = 0.44 − 0.0003·f_yb; F_vb,Rd = min(F_1,vb,Rd, F_2,vb,Rd); V_Rd = C_f,d·N_c,Ed + n_b·F_vb,Rd; V_b,Ed = max(V_Ed − C_f,d·N_c,Ed, 0)/n_b\n" +
        "Required thickness: the smallest t_p in 10–100 mm with u_N, u_C and u_T all ≤ 1, z_C, C_Ed and T_Ed re-evaluated at every trial t_p",
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
            new CalcInputSpec("f_ck_g", "Grout characteristic strength (optional: checks the β_j = 2/3 condition)", CalcInputKind.Quantity, "MPa", Min: 5, Max: 100, HardMin: 5, HardMax: 100, IsOptional: true, Group: "Materials"),
            new CalcInputSpec("t_g", "Grout thickness (optional: checks the β_j = 2/3 condition)", CalcInputKind.Quantity, "mm", Min: 1, Max: 200, HardMin: 1, HardMax: 200, IsOptional: true, Group: "Materials"),
            new CalcInputSpec("alpha_cc", "α_cc, long-term effects on the concrete strength", CalcInputKind.Number, Default: 0.85, Min: 0.8, Max: 1, HardMin: 0.8, HardMax: 1, Group: "Factors"),
            new CalcInputSpec("gamma_c", "γ_c, partial factor for concrete", CalcInputKind.Number, Default: 1.5, Min: 1.2, Max: 1.8, HardMin: 1.2, HardMax: 1.8, Group: "Factors"),
            new CalcInputSpec("gamma_M0", "γ_M0, partial factor for the plate", CalcInputKind.Number, Default: 1.0, Min: 1, Max: 1.25, HardMin: 1, HardMax: 1.25, Group: "Factors"),
            new CalcInputSpec("gamma_M2", "γ_M2, partial factor for the anchor bolts", CalcInputKind.Number, Default: 1.25, Min: 1.1, Max: 1.5, HardMin: 1.1, HardMax: 1.5, Group: "Factors"),
            new CalcInputSpec("C_fd", "C_f,d, friction coefficient plate to grout", CalcInputKind.Number, Default: 0.2, Min: 0, Max: 0.45, HardMin: 0, HardMax: 0.45, Group: "Factors"),
            new CalcInputSpec("alpha", "α, concentration factor √(A_c1/A_c0) of the foundation (1 when the foundation geometry is not known)", CalcInputKind.Number, Default: 1.0, Min: 1, Max: 3, HardMin: 1, HardMax: 3, Group: "Foundation"),
            new CalcInputSpec("alpha_basis", "Foundation geometry that α rests on (state it when α > 1)", CalcInputKind.Text, IsOptional: true, Group: "Foundation"),
            new CalcInputSpec("N_Ed", "Axial force, COMPRESSION positive (the opposite sign to S3's N_Ed, which is tension positive; S2's R1_y, an upward reaction, is compression positive)", CalcInputKind.Quantity, "kN", Default: 300, Min: -5000, Max: 10000, HardMin: -5000, HardMax: 10000, Group: "Actions"),
            new CalcInputSpec("M_Ed", "Major-axis moment (uniaxial)", CalcInputKind.Quantity, "kN*m", Default: 100, Min: 0, Max: 2000, HardMin: 0, HardMax: 2000, Group: "Actions"),
            new CalcInputSpec("V_Ed", "Shear force", CalcInputKind.Quantity, "kN", Default: 80, Min: 0, Max: 2000, HardMin: 0, HardMax: 2000, Group: "Actions"),
            new CalcInputSpec("n_t", "Holding-down bolts in each bolt row (one row each side)", CalcInputKind.Number, Default: 2, Min: 1, Max: 20, HardMin: 1, HardMax: 20, Group: "Holding-down bolts"),
            new CalcInputSpec("n_b", "Holding-down bolts in total (both sides)", CalcInputKind.Number, Default: 4, Min: 2, Max: 40, HardMin: 2, HardMax: 40, Group: "Holding-down bolts"),
            new CalcInputSpec("x_b", "Bolt row distance from the column axis (outside the column face, inside the plate edge)", CalcInputKind.Quantity, "mm", Default: 165, Min: 50, Max: 1000, HardMin: 50, HardMax: 1000, Group: "Holding-down bolts"),
            new CalcInputSpec("z_C_in", "Compression resultant distance from the column axis (blank: from the section)", CalcInputKind.Quantity, "mm", Min: 10, Max: 1000, HardMin: 10, HardMax: 1000, IsOptional: true, Group: "Holding-down bolts"),
            new CalcInputSpec("m", "Bolt to flange-weld toe distance, tension T-stub (required: it follows from x_b, the column and the weld)", CalcInputKind.Quantity, "mm", Min: 5, Max: 300, HardMin: 5, HardMax: 300, Group: "Holding-down bolts"),
            new CalcInputSpec("l_eff", "Effective length of the tension T-stub, total for the n_t bolts (required: from the cl. 6.2.6.5 patterns for the plate)", CalcInputKind.Quantity, "mm", Min: 10, Max: 2000, HardMin: 10, HardMax: 2000, Group: "Holding-down bolts"),
            new CalcInputSpec("prying", "Prying forces develop (short bolts, L_b ≤ L_b*); off is conservative for the plate", CalcInputKind.Boolean, Default: 0, Group: "Holding-down bolts"),
            new CalcInputSpec("e_min", "Edge distance e_min of the bolt row (used only with prying)", CalcInputKind.Quantity, "mm", Default: 35, Min: 5, Max: 500, HardMin: 5, HardMax: 500, IsOptional: true, Group: "Holding-down bolts"),
            new CalcInputSpec("F_t_Rd", "Tension resistance per bolt (from S4)", CalcInputKind.Quantity, "kN", Default: 141, Min: 1, Max: 2000, HardMin: 1, HardMax: 2000, Group: "Holding-down bolts"),
            new CalcInputSpec("f_ub", "Anchor bolt ultimate strength", CalcInputKind.Quantity, "MPa", Default: 800, Min: 400, Max: 1000, HardMin: 400, HardMax: 1000, Group: "Holding-down bolts"),
            new CalcInputSpec("f_yb", "Anchor bolt yield strength (α_bc is valid for 235–640 MPa)", CalcInputKind.Quantity, "MPa", Default: 640, Min: 235, Max: 640, HardMin: 235, HardMax: 640, Group: "Holding-down bolts"),
            new CalcInputSpec("A_s", "Anchor bolt tensile stress area (from S4)", CalcInputKind.Quantity, "mm^2", Default: 244.79, Min: 50, Max: 1500, HardMin: 50, HardMax: 1500, Group: "Holding-down bolts"),
            new CalcInputSpec("F_1_vb_Rd", "Bolt bearing resistance on the plate, F_1,vb,Rd (from S4 F_b,Rd)", CalcInputKind.Quantity, "kN", Default: 198.8, Min: 1, Max: 2000, HardMin: 1, HardMax: 2000, Group: "Holding-down bolts"),
            new CalcInputSpec("u_profis", "Governing utilisation reported by the PROFIS anchorage design (from the attached report; blank = the anchorage is not assessed here)", CalcInputKind.Number, Min: 0, Max: 10, HardMin: 0, HardMax: 10, IsOptional: true, Group: "Anchorage"),
            new CalcInputSpec("profis_ref", "Hilti PROFIS report reference (the report is filed as Evidence)", CalcInputKind.Text, IsOptional: true, Group: "Anchorage"),
        ],
        [
            new CalcAssumption("N_Ed is positive in compression here, the opposite of S3 (tension positive): a link from S2 must take the reaction R1_y (up positive, compression into the base), never the member force N_e1.", "Sign convention."),
            new CalcAssumption("Static loading; a major-axis moment only (uniaxial); an unstiffened rectangular plate on grout of adequate strength and thickness for β_j = 2/3 (checked when the grout strength and thickness are entered).", "Scope of S6."),
            new CalcAssumption("The simplified T-stub model: one compression resultant at the centre of the compression T-stub (flange centre, or the half-annulus centroid of a CHS) and one tension resultant per bolt row; the force regime follows EN 1993-1-8 Table 6.7. The web is not given credit in the moment check.", "EN 1993-1-8 cl. 6.2.8.3."),
            new CalcAssumption("The effective length l_eff is the total for the n_t bolts of a row, read from the cl. 6.2.6.5 patterns; it is used for modes 1 and 2 alike (l_eff,1 ≤ l_eff,2, so this is conservative for mode 2).", "One input."),
            new CalcAssumption("Concrete, anchor tension, pull-out, breakout, edge and the tension–shear interaction of the anchors are designed in Hilti PROFIS from F_t,Ed and V_b,Ed per anchor; the report is filed as Evidence and referenced. With prying switched on the prying force Q is not in F_t,Ed and PROFIS must add it.", "External anchorage design."),
            new CalcAssumption("The friction resistance counts only the compression, C_f,d·max(N_Ed, 0); a column in net tension has none. Anchor bolts share shear equally, with their holes assumed no larger than the standard clearance (oversized or slotted holes are outside this sheet).", "EN 1993-1-8 cl. 6.2.2."),
            new CalcAssumption("The bearing outstand c is cut back where it would pass the plate edge; the web and the column interior keep the full c. The CHS compression side is half of the annular bearing area, with its centroid as lever arm (an approximation).", "Conservative against an uncut c."),
            new CalcAssumption("The compression T-stub is checked against the flange area alone as soon as M_Ed > 0, while at M_Ed = 0 the whole footprint (web included) resists, so the compression check jumps at M_Ed = 0⁺ (conservative).", "Limitation."),
            new CalcAssumption("Partial factors and the friction coefficient come from the records in use (γ_c, α_cc: EN 1992-1-1 NA; γ_M2: EN 1993-1-8 NA; γ_M0: EN 1993-1-1 NA; C_f,d: the grout used); the values shown default to the EN recommended and UK values.", "A small set of factors only; no table from a standard is shipped."),
        ],
        [
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.2", "Design resistance of column bases in shear"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.4, Table 6.2", "Equivalent T-stub in tension"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.5", "Equivalent T-stub in compression"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.6.9", "Concrete in compression including grout"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.6.10", "Base plate in bending under compression"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.6.11", "Base plate in bending under tension"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.6.12", "Anchor bolts in tension"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.8.2", "Column bases subject to axial forces"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 6.2.8.3, Table 6.7", "Column bases subject to axial force and bending moment"),
            new CalcClauseRef("EN 1992-1-1:2004", "cl. 3.1.6, 6.7", "Design strength of concrete; partial area loading"),
        ],
        [
            new CalcApplicabilityRule("plate", "The column must stand inside the plate.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("section", "The wall or flange thickness must be less than half the section, and the section's dimensions must be entered.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("bolts", "The bolt counts must be whole numbers with n_b at least 2·n_t.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("x_b", "The bolt row must lie outside the column face and inside the plate edge, with m and e_min fitting between.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("z_C", "An entered compression lever arm z_C must lie inside the plate (below L/2).", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("z_C_entered", "An entered compression lever arm that differs from the section's by more than the outstand c is probably the lever arm z, not z_C.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("alpha_bc", "α_bc is defined for f_yb of 235–640 MPa; an anchor bolt strength above 800 MPa has a higher yield strength than the formula covers, so the sheet blocks unless the anchor design's governing utilisation u_profis is entered.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("alpha_bc_profis", "An anchor bolt strength above 800 MPa with u_profis entered: u_V is not reliable, the anchor design's utilisation counts.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("f_y_thickness", "One plate yield strength is used at every thickness; EN 10025-2 lowers it for thicker plate.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("outstand-cut", "With the bearing outstand cut at the plate edge a thicker plate can raise the compression utilisation.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("alpha_basis", "A concentration factor above 1 needs its foundation geometry stated.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("grout", "β_j = 2/3 needs the grout at least 0.2·f_ck and no thicker than 0.2 of the smaller plate dimension.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("prying", "With prying the prying force is not in F_t,Ed.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("anchorage", "The PROFIS report is not attached as Evidence (a typed reference is not enough).", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("anchorage-result", "The anchorage (concrete cone, pull-out, edge and shear failure of the anchors) is not assessed on this sheet and u_max excludes it, unless the report's governing utilisation u_profis is entered.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-ih-moment-bolts", "The 260×256 I-section with N = 300 kN, M = 100 kN·m, V = 80 kN; two bolts a row, bolt rows at 165 mm from the axis, no prying",
                new CalcReferenceSource("Analytic (simplified T-stub model, Table 6.7 regime 1; recomputed independently)", "EN 1993-1-8:2005", "z_C = (260 − 17)/2 = 121.5, z = 286.5 mm: C_Ed = (100 + 300·0.165)/0.2865 = 521.82 kN, T_Ed = 221.82 kN, F_t,Ed = 110.91 kN per bolt; A_flange = 42 482.5 mm², C_Rd = 601.84 kN; M_pl,Rd = 0.25·200·20²·275 = 5.5 kN·m; m = 28: mode 1-2 = 2·5.5/0.028 = 392.86 kN, mode 3 = 2·141 = 282 kN → 282 kN; α_bc = 0.44 − 0.0003·640 = 0.248, F_2,vb,Rd = 0.248·800·244.79/1.25 = 38.853 kN (below F_1,vb,Rd = 198.8), V_Rd = 0.2·300 + 4·38.853 = 215.41 kN; V_b,Ed = (80 − 60)/4 = 5 kN"),
                [Choice("column", "I/H"), Q("h", 260, "mm"), Q("b", 256, "mm"), Q("t_f", 17, "mm"), Q("t_w", 10, "mm"), Q("B", 400, "mm"), Q("L", 400, "mm"), Q("t_p", 20, "mm"), Q("f_y", 275, "MPa"), Q("f_ck", 25, "MPa"), N("alpha_cc", 0.85), N("gamma_c", 1.5), N("gamma_M0", 1), N("gamma_M2", 1.25), N("alpha", 1.5), Q("N_Ed", 300, "kN"), Q("M_Ed", 100, "kN*m"), Q("V_Ed", 80, "kN"), N("n_t", 2), N("n_b", 4), Q("x_b", 165, "mm"), Q("m", 28, "mm"), Q("l_eff", 200, "mm"), CalcInputValue.Switch("prying", false), Q("F_t_Rd", 141, "kN"), Q("f_ub", 800, "MPa"), Q("f_yb", 640, "MPa"), Q("A_s", 244.79, "mm^2"), Q("F_1_vb_Rd", 198.8, "kN"), N("C_fd", 0.2)],
                [new CalcExpectedValue("z_C", 121.5, "mm", 1e-9), new CalcExpectedValue("z", 286.5, "mm", 1e-9), new CalcExpectedValue("C_Ed", 521.8150087, "kN", 1e-6), new CalcExpectedValue("T_Ed", 221.8150087, "kN", 1e-6), new CalcExpectedValue("F_t_Ed", 110.9075044, "kN", 1e-6), new CalcExpectedValue("C_Rd", 601.8358193, "kN", 1e-6), new CalcExpectedValue("u_C", 0.8670388037, "", 1e-6), new CalcExpectedValue("M_pl_Rd", 5.5, "kN*m", 1e-9), new CalcExpectedValue("T_Rd_1", 392.8571429, "kN", 1e-6), new CalcExpectedValue("T_Rd_3", 282, "kN", 1e-9), new CalcExpectedValue("T_Rd", 282, "kN", 1e-9), new CalcExpectedValue("u_T", 0.7865780451, "", 1e-6), new CalcExpectedValue("alpha_bc", 0.248, "", 1e-9), new CalcExpectedValue("F_2_vb_Rd", 38.8530688, "kN", 1e-6), new CalcExpectedValue("F_vb_Rd", 38.8530688, "kN", 1e-6), new CalcExpectedValue("F_f_Rd", 60, "kN", 1e-9), new CalcExpectedValue("V_Rd", 215.4122752, "kN", 1e-6), new CalcExpectedValue("u_V", 0.3713808785, "", 1e-6), new CalcExpectedValue("V_b_Ed", 5, "kN", 1e-9), new CalcExpectedValue("t_req", 17.60887161, "mm", 1e-6), new CalcExpectedValue("u_max", 0.8670388037, "", 1e-6)]),
            new CalcTestCase("ka-ih-moment-prying", "The same base with prying switched on and the edge distance e_min = 35 mm",
                new CalcReferenceSource("Analytic (EN 1993-1-8 Table 6.2 modes 1 to 3)", "EN 1993-1-8:2005", "n = min(35, 1.25·28) = 35 mm: mode 1 = 4·5.5/0.028 = 785.71 kN, mode 2 = (2·5.5e6 + 35·282e3)/(28 + 35) = 331.27 kN, mode 3 = 282 kN → 282 kN"),
                [.. IhBase, Q("N_Ed", 300, "kN"), Q("M_Ed", 100, "kN*m"), Q("V_Ed", 80, "kN"), CalcInputValue.Switch("prying", true), Q("e_min", 35, "mm")],
                [new CalcExpectedValue("T_Rd_1", 785.7142857, "kN", 1e-6), new CalcExpectedValue("n_pry", 35, "mm", 1e-9), new CalcExpectedValue("T_Rd_2", 331.2698413, "kN", 1e-6), new CalcExpectedValue("T_Rd_3", 282, "kN", 1e-9), new CalcExpectedValue("T_Rd", 282, "kN", 1e-9), new CalcExpectedValue("u_T", 0.7865780451, "", 1e-6)]),
            new CalcTestCase("ka-ih-both-compression", "N = 600 kN, M = 40 kN·m: eccentricity below z_C, both flanges in compression (Table 6.7)",
                new CalcReferenceSource("Analytic (EN 1993-1-8 Table 6.7, both sides in compression, lever arm 2·z_C)", "EN 1993-1-8:2005", "e = 0.067 m < z_C = 0.1215 m: C = 600/2 + 40/(2·0.1215) = 464.61 kN, other flange 135.39 kN, no bolt tension"),
                [.. IhBase, Q("N_Ed", 600, "kN"), Q("M_Ed", 40, "kN*m"), Q("V_Ed", 0, "kN")],
                [new CalcExpectedValue("C_Ed", 464.6090535, "kN", 1e-6), new CalcExpectedValue("C_Ed_2", 135.3909465, "kN", 1e-6), new CalcExpectedValue("T_Ed", 0, "kN", 1e-9), new CalcExpectedValue("F_t_Ed", 0, "kN", 1e-9), new CalcExpectedValue("u_C", 0.7719863767, "", 1e-6)]),
            new CalcTestCase("ka-ih-both-tension", "N = −300 kN (tension), M = 20 kN·m: both bolt rows in tension (Table 6.7)",
                new CalcReferenceSource("Analytic (EN 1993-1-8 Table 6.7, both sides in tension, lever arm 2·x_b)", "EN 1993-1-8:2005", "e = 0.067 m < x_b = 0.165 m: rows 300/2 ± 20/(2·0.165) = 210.61 and 89.39 kN, 105.30 kN per bolt at the more loaded row; with M = 0 and N = −500 each row takes 250 kN, 125 kN per bolt"),
                [.. IhBase, Q("N_Ed", -300, "kN"), Q("M_Ed", 20, "kN*m"), Q("V_Ed", 0, "kN")],
                [new CalcExpectedValue("T_Ed", 210.6060606, "kN", 1e-6), new CalcExpectedValue("T_Ed_2", 89.39393939, "kN", 1e-6), new CalcExpectedValue("F_t_Ed", 105.3030303, "kN", 1e-6), new CalcExpectedValue("C_Ed", 0, "kN", 1e-9), new CalcExpectedValue("F_f_Rd", 0, "kN", 1e-9)]),
            new CalcTestCase("ka-ih-axial", "UC-like I-section 260×256, t_f 17, t_w 10, on a 400×400×20 S275 plate, C25/30, axial 1200 kN",
                new CalcReferenceSource("Analytic (EN 1993-1-8 cl. 6.2.5 relations, recomputed independently)", "EN 1993-1-8:2005", "f_cd = 0.85·25/1.5 = 14.1667; f_jd = (2/3)·1.5·14.1667 = 14.1667 MPa; c = 20·√(275/(3·14.1667·1)) = 50.8747 mm; A_eff = 2·(256 + 101.749)(17 + 101.749) + (260 − 34 − 101.749)(10 + 101.749) = 98 849.99 mm²; N_j,Rd = 14.1667·98 849.99 = 1400.37 kN; t_req = 17.0754 mm"),
                [Choice("column", "I/H"), Q("h", 260, "mm"), Q("b", 256, "mm"), Q("t_f", 17, "mm"), Q("t_w", 10, "mm"), Q("B", 400, "mm"), Q("L", 400, "mm"), Q("t_p", 20, "mm"), Q("f_y", 275, "MPa"), Q("f_ck", 25, "MPa"), N("alpha_cc", 0.85), N("gamma_c", 1.5), N("gamma_M0", 1), N("alpha", 1.5), Q("m", 28, "mm"), Q("l_eff", 200, "mm"), Q("N_Ed", 1200, "kN"), Q("M_Ed", 0, "kN*m"), Q("V_Ed", 0, "kN")],
                [new CalcExpectedValue("f_cd", 14.16666667, "MPa", 1e-6), new CalcExpectedValue("f_jd", 14.16666667, "MPa", 1e-6), new CalcExpectedValue("c", 50.87470191, "mm", 1e-6), new CalcExpectedValue("A_eff", 98849.98688, "mm^2", 1e-6), new CalcExpectedValue("N_j_Rd", 1400.374814, "kN", 1e-6), new CalcExpectedValue("u_N", 0.8569134405, "", 1e-6), new CalcExpectedValue("t_req_N", 17.0753989, "mm", 1e-6), new CalcExpectedValue("t_req", 17.0753989, "mm", 1e-6), new CalcExpectedValue("F_t_Ed", 0, "kN", 1e-9)]),
            new CalcTestCase("ka-rhs-axial", "RHS 200×200×10 on a 350×350×20 S275 plate, C25/30, axial 900 kN",
                new CalcReferenceSource("Analytic (EN 1993-1-8 cl. 6.2.5 relations)", "EN 1993-1-8:2005", "c = 50.8747 mm; A_eff = (200 + 101.749)² − (200 − 20 − 101.749)² = 91 052.70 − 6 123.16 = 84 929.55 mm²; N_j,Rd = 14.1667·84 929.55 = 1203.17 kN; t_req = 14.4652 mm"),
                [Choice("column", ColumnRhs), Q("h", 200, "mm"), Q("b", 200, "mm"), Q("t", 10, "mm"), Q("B", 350, "mm"), Q("L", 350, "mm"), Q("t_p", 20, "mm"), Q("f_y", 275, "MPa"), Q("f_ck", 25, "MPa"), N("alpha", 1.5), Q("m", 28, "mm"), Q("l_eff", 200, "mm"), Q("N_Ed", 900, "kN"), Q("M_Ed", 0, "kN*m"), Q("V_Ed", 0, "kN")],
                [new CalcExpectedValue("A_eff", 84929.5469, "mm^2", 1e-6), new CalcExpectedValue("N_j_Rd", 1203.168581, "kN", 1e-6), new CalcExpectedValue("u_N", 0.7480248522, "", 1e-6), new CalcExpectedValue("t_req", 14.4652113, "mm", 1e-6)]),
            new CalcTestCase("ka-chs-axial", "CHS 220×10 on a 350×350×20 S275 plate, C25/30, axial 800 kN",
                new CalcReferenceSource("Analytic (EN 1993-1-8 cl. 6.2.5 relations)", "EN 1993-1-8:2005", "c = 50.8747 mm; A_eff = π[(110 + 50.8747)² − (110 − 10 − 50.8747)²] = 73 724.93 mm²; N_j,Rd = 14.1667·73 724.93 = 1044.44 kN; t_req = 14.8592 mm"),
                [Choice("column", ColumnChs), Q("D", 220, "mm"), Q("t", 10, "mm"), Q("B", 350, "mm"), Q("L", 350, "mm"), Q("t_p", 20, "mm"), Q("f_y", 275, "MPa"), Q("f_ck", 25, "MPa"), N("alpha", 1.5), Q("m", 28, "mm"), Q("l_eff", 200, "mm"), Q("N_Ed", 800, "kN"), Q("M_Ed", 0, "kN*m"), Q("V_Ed", 0, "kN")],
                [new CalcExpectedValue("A_eff", 73724.93227, "mm^2", 1e-6), new CalcExpectedValue("N_j_Rd", 1044.436541, "kN", 1e-6), new CalcExpectedValue("u_N", 0.7659632433, "", 1e-6), new CalcExpectedValue("t_req", 14.85923906, "mm", 1e-6)]),
            new CalcTestCase("ka-chs-moment", "CHS 220×10 on a 350×350×20 plate with N = 200 kN, M = 60 kN·m, bolt rows at 140 mm: lever arm from the half-annulus centroid",
                new CalcReferenceSource("Analytic (EN 1993-1-8 Table 6.7 regime 1 with the half-annulus centroid as z_C)", "EN 1993-1-8:2005", "R = 110 + 50.8747, r = 110 − 10 − 50.8747: z_C = 4(R³ − r³)/(3π(R² − r²)) = 73.1546 mm, z = 213.1546 mm: C_Ed = (60 + 200·0.14)/0.21315 = 412.85 kN, T_Ed = 212.85 kN, 106.42 kN per bolt; C_Rd = 14.1667·36 862.47 = 522.22 kN; t_req = 15.587 mm with z_C, C_Ed and T_Ed re-evaluated at each trial t_p (bisection)"),
                [Choice("column", ColumnChs), Q("D", 220, "mm"), Q("t", 10, "mm"), Q("B", 350, "mm"), Q("L", 350, "mm"), Q("t_p", 20, "mm"), Q("f_y", 275, "MPa"), Q("f_ck", 25, "MPa"), N("alpha", 1.5), Q("N_Ed", 200, "kN"), Q("M_Ed", 60, "kN*m"), Q("V_Ed", 0, "kN"), Q("x_b", 140, "mm"), Q("m", 25, "mm"), Q("l_eff", 200, "mm")],
                [new CalcExpectedValue("z_C", 73.15464965, "mm", 1e-6), new CalcExpectedValue("z", 213.1546497, "mm", 1e-6), new CalcExpectedValue("C_Ed", 412.8457913, "kN", 1e-6), new CalcExpectedValue("T_Ed", 212.8457913, "kN", 1e-6), new CalcExpectedValue("F_t_Ed", 106.4228956, "kN", 1e-6), new CalcExpectedValue("C_Rd", 522.2182703, "kN", 1e-6), new CalcExpectedValue("u_C", 0.7905617532, "", 1e-6), new CalcExpectedValue("t_req", 15.58734927, "mm", 1e-6)]),
            new CalcTestCase("ka-ih-overlap-clip", "Small I-section 150×150 on a 300×400×40 plate: the strips overlap and the outstand across the width is cut back to the plate edge",
                new CalcReferenceSource("Analytic (EN 1993-1-8 cl. 6.2.5 relations, outstand cut at the plate edge)", "EN 1993-1-8:2005", "c = 40·√(275/(3·14.1667)) = 101.749 mm; h − 2t_f − 2c < 0 so the full rectangle is used; the outstands are min(c, 75) = 75 mm across the width and min(c, 125) = 101.749 mm along the length: A_eff = (150 + 150)(150 + 203.499) = 106 049.64 mm² (the uncut rectangle clipped to B·L would be 120 000 mm²); N_j,Rd = 1502.37 kN"),
                [Choice("column", "I/H"), Q("h", 150, "mm"), Q("b", 150, "mm"), Q("t_f", 10, "mm"), Q("t_w", 6, "mm"), Q("B", 300, "mm"), Q("L", 400, "mm"), Q("t_p", 40, "mm"), Q("f_y", 275, "MPa"), Q("f_ck", 25, "MPa"), N("alpha", 1.5), Q("m", 28, "mm"), Q("l_eff", 200, "mm"), Q("N_Ed", 1000, "kN"), Q("M_Ed", 0, "kN*m"), Q("V_Ed", 0, "kN")],
                [new CalcExpectedValue("c", 101.7494038, "mm", 1e-6), new CalcExpectedValue("A_eff", 106049.6423, "mm^2", 1e-6), new CalcExpectedValue("N_j_Rd", 1502.369932, "kN", 1e-6)]),
        ],
        Category: "Connections",
        OutputPorts:
        [
            new CalcOutputPort("F_t_Ed", "kN", "Holding-down bolt tension per bolt at the more loaded row (→ Hilti PROFIS)"),
            new CalcOutputPort("V_b_Ed", "kN", "Shear per anchor bolt after friction (→ Hilti PROFIS)"),
            new CalcOutputPort("N_j_Rd", "kN", "Axial bearing resistance of the joint"),
            new CalcOutputPort("u_max", "", "Governing utilisation"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("ec2.national-annex", "EN 1992-1-1 National Annex factors", ["alpha_cc", "gamma_c"], "From the National Annex in use, with its edition (UK NA to EN 1992-1-1 for α_cc)."),
            new CalcRecordKind("ec3.national-annex", "EN 1993-1-8 National Annex factor", ["gamma_M2"], "From the National Annex in use, with its edition."),
            new CalcRecordKind("ec3-1-1.national-annex", "EN 1993-1-1 National Annex factor", ["gamma_M0"], "From the National Annex to EN 1993-1-1 in use, with its edition."),
            new CalcRecordKind("joint.friction", "Friction coefficient plate to grout", ["C_fd"], "The value for the grout used, from EN 1993-1-8 cl. 6.2.2 or the grout manufacturer, with its source."),
            new CalcRecordKind("steel.strengths", "Plate strength", ["f_y"], "From the product standard for the grade and thickness (EN 10025-2)."),
        ],
        IndependentCheck: true);

    /// <summary>The 260×256 I-section on a 400×400×20 S275 plate with two bolts a row at 165 mm: the common part of the moment cases.</summary>
    private static readonly CalcInputValue[] IhBase =
    [
        CalcInputValue.Choice("column", ColumnIH), Q("h", 260, "mm"), Q("b", 256, "mm"), Q("t_f", 17, "mm"), Q("t_w", 10, "mm"), Q("B", 400, "mm"), Q("L", 400, "mm"), Q("t_p", 20, "mm"), Q("f_y", 275, "MPa"), Q("f_ck", 25, "MPa"),
        N("alpha", 1.5), N("n_t", 2), N("n_b", 4), Q("x_b", 165, "mm"), Q("m", 28, "mm"), Q("l_eff", 200, "mm"), Q("F_t_Rd", 141, "kN"), Q("f_ub", 800, "MPa"), Q("f_yb", 640, "MPa"), Q("A_s", 244.79, "mm^2"), Q("F_1_vb_Rd", 198.8, "kN"),
    ];

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue N(string symbol, double value) => CalcInputValue.Of(symbol, value);

    private static CalcInputValue Choice(string symbol, string value) => CalcInputValue.Choice(symbol, value);

    /// <summary>c (mm) for a plate thickness: c = t_p·√[f_y/(3·f_jd·γ_M0)], f_y and f_jd in MPa.</summary>
    public static double Outstand(double tp, double fy, double fjd, double gammaM0) => tp * Math.Sqrt(fy / (3 * fjd * gammaM0));

    /// <summary>
    /// The effective bearing area (mm²) of the footprint for an outstand <paramref name="c"/> on a plate B × L. The strips run
    /// round the column; where they overlap the full rectangle is used. The outstand across the plate edge is cut back to the
    /// edge, so no area outside the plate is counted. Also returns the area of the compression T-stub and its centroid, the lever
    /// arm z_C of the compression resultant from the column axis.
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
                    $"({F(s.B + 2 * cb)})({F(s.H + 2 * ch)}) − ({F(ib)})({F(ih)})", false, cb < c || ch < c,
                    "(b + 2c)(t + 2c), at most A_eff/2", $"min({F(s.B + 2 * cb)} × {F(s.T + ch + c)}, A_eff/2)", StripCentroid(s.H, s.T, c, ch), StripFormula(c, ch, "(h − t)/2"), StripSubstitution(s.H, s.T, c, ch));
            }

            case ColumnChs:
            {
                var co = Math.Max(0, Math.Min(c, (Math.Min(plateB, plateL) - s.D) / 2));
                var outer = s.D / 2 + co;
                var inner = Math.Max(0, s.D / 2 - s.T - c);
                var area = Math.Min(Math.PI * (outer * outer - inner * inner), plate);
                var centroid = outer > inner ? 4 * (outer * outer * outer - inner * inner * inner) / (3 * Math.PI * (outer * outer - inner * inner)) : 0;
                return new BearingArea(area, area / 2, "π[(D/2 + c)² − (D/2 − t − c)²], inner term ≥ 0" + Cut(co < c),
                    $"π[({F(outer)})² − ({F(inner)})²]", false, co < c,
                    "half of A_eff (the compressed half of the annulus)", "A_eff/2", centroid, "4(R³ − r³)/(3π(R² − r²)), R = D/2 + c, r = D/2 − t − c", $"R = {F(outer)}, r = {F(inner)}");
            }

            default:
            {
                var cb = Math.Max(0, Math.Min(c, (plateB - s.B) / 2));
                var ch = Math.Max(0, Math.Min(c, (plateL - s.H) / 2));
                var clipped = cb < c || ch < c;
                var web = s.H - 2 * s.Tf - 2 * c;
                var centroid = StripCentroid(s.H, s.Tf, c, ch);
                var centroidFormula = StripFormula(c, ch, "(h − t_f)/2");
                var centroidSubstitution = StripSubstitution(s.H, s.Tf, c, ch);
                var flangeSubstitution = $"min({F(s.B + 2 * cb)} × {F(s.Tf + ch + c)}, A_eff/2)";

                if (web < 0)
                {
                    var rect = Math.Min((s.B + 2 * cb) * (s.H + 2 * ch), plate);
                    var flangeRect = Math.Min((s.B + 2 * cb) * (s.Tf + ch + c), rect / 2);
                    return new BearingArea(rect, flangeRect, "(b + 2c)(h + 2c): the strips overlap, so the full rectangle is used" + Cut(clipped), $"({F(s.B + 2 * cb)})({F(s.H + 2 * ch)})", true, clipped,
                        "(b + 2c)(t_f + 2c), at most A_eff/2", flangeSubstitution, centroid, centroidFormula, centroidSubstitution);
                }

                var flangeStrip = (s.B + 2 * cb) * (s.Tf + ch + c);
                var webStrip = Math.Min(s.Tw + 2 * c, s.B + 2 * cb) * web;
                var total = Math.Min(2 * flangeStrip + webStrip, plate);
                return new BearingArea(total, Math.Min(flangeStrip, total / 2), "2(b + 2c)(t_f + 2c) + (h − 2t_f − 2c)(t_w + 2c)" + Cut(clipped), $"2 × {F(s.B + 2 * cb)} × {F(s.Tf + ch + c)} + {F(web)} × {F(Math.Min(s.Tw + 2 * c, s.B + 2 * cb))}", false, clipped,
                    "(b + 2c)(t_f + 2c), at most A_eff/2", flangeSubstitution, centroid, centroidFormula, centroidSubstitution);
            }
        }
    }

    /// <summary>
    /// The lever arm of the compression strip from the column axis, mm: the strip runs from c inside the flange (or wall) to the outstand
    /// <paramref name="ch"/> outside it, so its centroid is (h − t)/2 − (c − ch)/2, which is (h − t)/2 only while the outstand is not cut
    /// back to the plate edge (Green G-A-10).
    /// </summary>
    private static double StripCentroid(double h, double t, double c, double ch) => (h - t) / 2 - (c - ch) / 2;

    private static string StripFormula(double c, double ch, string whole) => c - ch > 1e-9 ? whole + " − (c − c_cut)/2 (the outstand is cut back to the plate edge)" : whole;

    private static string StripSubstitution(double h, double t, double c, double ch) => c - ch > 1e-9 ? $"({F(h)} − {F(t)})/2 − ({F(c)} − {F(ch)})/2" : $"({F(h)} − {F(t)})/2";

    /// <summary>
    /// The EN 1993-1-8 Table 6.7 forces (kN) for N (compression positive, kN), M (kN·m) about the major axis, the compression
    /// lever arm <paramref name="zC"/> and the bolt row distance <paramref name="xB"/> (mm). Regime 1: tension at the row at −x_b,
    /// compression at +z_C. Regime 2 (N &gt; 0, small eccentricity): compression under both flanges. Regime 3 (N &lt; 0, small
    /// eccentricity): both bolt rows in tension.
    /// </summary>
    public static Forces Statics(double nKn, double mKnm, double zC, double xB)
    {
        var z = zC + xB;
        var cPlus = (mKnm * 1e3 + nKn * xB) / z;
        var tMinus = cPlus - nKn;

        if (cPlus >= 0 && tMinus >= 0)
            return new Forces(1, cPlus, 0, tMinus, 0);

        if (nKn > 0)
            return new Forces(2, nKn / 2 + mKnm * 1e3 / (2 * zC), nKn / 2 - mKnm * 1e3 / (2 * zC), 0, 0);

        return new Forces(3, 0, 0, -nKn / 2 + mKnm * 1e3 / (2 * xB), -nKn / 2 - mKnm * 1e3 / (2 * xB));
    }

    /// <summary>The tension T-stub resistances of one bolt row of <paramref name="nT"/> bolts (EN 1993-1-8 Table 6.2), fy in MPa, lengths in mm, F_t,Rd in kN.</summary>
    public static TStub TensionTStub(double tp, double fy, double gammaM0, double lEff, double m, double eMin, double nT, double ftRd, bool prying)
    {
        var mpl = 0.25 * lEff * tp * tp * fy / gammaM0;
        var mode3 = nT * ftRd;

        if (!prying)
        {
            var mode12 = 2 * mpl / m / 1e3;
            return new TStub(mpl / 1e6, mode12, double.NaN, mode3, Math.Min(mode12, mode3), double.NaN);
        }

        var n = Math.Min(eMin, 1.25 * m);
        var mode1 = 4 * mpl / m / 1e3;
        var mode2 = (2 * mpl + n * mode3 * 1e3) / (m + n) / 1e3;
        return new TStub(mpl / 1e6, mode1, mode2, mode3, Math.Min(mode1, Math.Min(mode2, mode3)), n);
    }

    /// <summary>
    /// The smallest plate thickness in 10–100 mm whose bearing resistance f_jd·A_eff(c(t)) carries <paramref name="nEdKn"/>, by
    /// bisection; <see cref="MinThickness"/> when it is carried already at 10 mm, <see langword="null"/> when 100 mm is not enough.
    /// </summary>
    public static double? RequiredThickness(Section s, double plateB, double plateL, double fy, double fjd, double gammaM0, double nEdKn) =>
        Bisect(t => nEdKn <= 0 || fjd * Effective(s, Outstand(t, fy, fjd, gammaM0), plateB, plateL).Area / 1e3 >= nEdKn);

    /// <summary>
    /// The smallest plate thickness in 10–100 mm with u_N, u_C and u_T all ≤ 1: bearing under the axial force, the compression
    /// T-stub and the tension T-stub. The compression lever arm z_C (the strip or half-annulus centroid, which depends on the outstand
    /// c and so on t_p, unless <paramref name="zCEntered"/> is given) and with it the Table 6.7 forces C_Ed and T_Ed are recomputed at
    /// every trial thickness, so the reported t_req passes its own checks (P1-struct.base-plate-01). The search scans the range in
    /// 0.05 mm steps and bisects the first passing step, so it does not rely on the forces falling monotonically with t_p. When the
    /// bolts alone cannot carry the tension at the 100 mm end (mode 3) no thickness helps.
    /// </summary>
    public static Required RequiredThicknessAll(Section s, double plateB, double plateL, double fy, double fjd, double gammaM0, double nEdSignedKn, double mEdKnm, double xB, double? zCEntered, double lEff, double m, double eMin, double nT, double ftRd, bool prying)
    {
        var nEdKn = Math.Max(nEdSignedKn, 0);

        bool Carries(double t)
        {
            var area = Effective(s, Outstand(t, fy, fjd, gammaM0), plateB, plateL);
            var forces = Statics(nEdSignedKn, mEdKnm, zCEntered ?? area.Centroid, xB);
            var checkCompression = mEdKnm > 0 && forces.C > 0;
            return (nEdKn <= 0 || fjd * area.Area / 1e3 >= nEdKn)
                && (!checkCompression || forces.C <= fjd * area.FlangeArea / 1e3)
                && forces.T <= TensionTStub(t, fy, gammaM0, lEff, m, eMin, nT, ftRd, prying).Resistance + 1e-12;
        }

        var found = Search(Carries);

        if (found is { } value)
            return new Required(value, null);

        var top = Effective(s, Outstand(MaxThickness, fy, fjd, gammaM0), plateB, plateL);
        var tAtMax = Statics(nEdSignedKn, mEdKnm, zCEntered ?? top.Centroid, xB).T;

        return tAtMax > nT * ftRd * (1 + 1e-9)
            ? new Required(null, "bolts govern (T_Ed exceeds n_t·F_t,Rd at the 100 mm end of the range, so no thickness helps)")
            : new Required(null, "> 100 mm");
    }

    /// <summary>The compression T-stub utilisation u_C at a trial plate thickness (mm), with z_C and C_Ed re-evaluated at that thickness.</summary>
    private static double CompressionUtilisation(Section s, double plateB, double plateL, double fy, double fjd, double gammaM0, double nEdKn, double mEdKnm, double xB, double? zCEntered, double t)
    {
        var area = Effective(s, Outstand(t, fy, fjd, gammaM0), plateB, plateL);
        var forces = Statics(nEdKn, mEdKnm, zCEntered ?? area.Centroid, xB);
        return forces.C / (fjd * area.FlangeArea / 1e3);
    }

    /// <summary>The first passing thickness of a 0.05 mm scan of 10–100 mm, refined by bisection inside that step; <see langword="null"/> when none passes.</summary>
    private static double? Search(Func<double, bool> carries)
    {
        if (carries(MinThickness))
            return MinThickness;

        const double step = 0.05;
        var previous = MinThickness;

        for (var i = 1; MinThickness + i * step <= MaxThickness + 1e-9; i++)
        {
            var t = Math.Min(MinThickness + i * step, MaxThickness);

            if (carries(t))
            {
                double lo = previous, hi = t;

                for (var k = 0; k < 100; k++)
                {
                    var mid = (lo + hi) / 2;

                    if (carries(mid))
                        hi = mid;
                    else
                        lo = mid;
                }

                return hi;
            }

            previous = t;
        }

        return null;
    }

    private static double? Bisect(Func<double, bool> carries)
    {
        if (carries(MinThickness))
            return MinThickness;

        if (!carries(MaxThickness))
            return null;

        double lo = MinThickness, hi = MaxThickness;

        for (var i = 0; i < 200; i++)
        {
            var mid = (lo + hi) / 2;

            if (carries(mid))
                hi = mid;
            else
                lo = mid;
        }

        return hi;
    }

    public void Run(CalcRunContext c)
    {
        foreach (var s in new[] { "B", "L", "t_p", "f_y", "f_ck", "alpha_cc", "gamma_c", "gamma_M0", "gamma_M2", "C_fd", "alpha", "N_Ed", "M_Ed", "V_Ed", "n_t", "n_b", "x_b", "m", "l_eff", "F_t_Rd", "f_ub", "f_yb", "A_s", "F_1_vb_Rd" })
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
            c.Block("bolts", $"n_b: the total ({F(nB)}) must be at least twice the bolts in each row ({F(nT)}).");
            return;
        }

        var prying = c.InputFlag("prying");
        var xB = c.Si("x_b") * 1e3;
        var mDist = c.Si("m") * 1e3;
        var half = section.HalfDepth;

        if (!(xB > half && xB < plateL / 2))
        {
            c.Block("x_b", $"x_b: the bolt row ({F(xB)} mm from the axis) must lie outside the column face ({F(half)} mm) and inside the plate edge ({F(plateL / 2)} mm).");
            return;
        }

        if (mDist > xB - half + 1e-9)
        {
            c.Block("x_b", $"m: the bolt to weld toe distance ({F(mDist)} mm) cannot exceed the room between the column face and the bolt row ({F(xB - half)} mm).");
            return;
        }

        double eMin = 0;

        if (prying)
        {
            if (!Need(c, "prying", "e_min"))
                return;

            c.Input("e_min");
            eMin = c.Si("e_min") * 1e3;

            if (eMin > plateL / 2 - xB + 1e-9)
            {
                c.Block("x_b", $"e_min: the edge distance ({F(eMin)} mm) cannot exceed the room between the bolt row and the plate edge ({F(plateL / 2 - xB)} mm).");
                return;
            }
        }

        var kN = Engine.Units.UnitCatalog.Parse("kN");
        var mm = Engine.Units.UnitCatalog.Parse("mm");

        // Bearing strength and the outstand c.
        c.Assign("f_cd", "alpha_cc * f_ck / gamma_c", "MPa", "Concrete design compressive strength", "EN 1992-1-1 cl. 3.1.6");
        c.Record("beta_j", "2/3, fixed", "grout conditions of cl. 6.2.5 assumed met", new ScalarValue(BetaJ), "Joint coefficient β_j", "EN 1993-1-8 cl. 6.2.5");
        c.Assign("f_jd", "beta_j * alpha * f_cd", "MPa", "Design bearing strength of the joint", "EN 1993-1-8 cl. 6.2.5");
        c.Assign("c", "t_p * sqrt(f_y / (3 * f_jd * gamma_M0))", "mm", "Additional bearing width round the column footprint", "EN 1993-1-8 cl. 6.2.5");

        var fy = c.Si("f_y") / 1e6;
        var fjd = c.Si("f_jd") / 1e6;
        var gM0 = c.InputSi("gamma_M0");
        var cMm = c.Si("c") * 1e3;
        var bearing = Effective(section, cMm, plateB, plateL);
        c.Record("A_eff", bearing.Formula, bearing.Substitution + " mm²", ScalarValue.Of(bearing.Area, "mm^2"), "Effective bearing area", "EN 1993-1-8 cl. 6.2.5");

        if (bearing.Overlap)
            c.Assume("The bearing strips round the column overlap, so the full rectangle (b + 2c)(h + 2c) is used for A_eff.", "EN 1993-1-8 cl. 6.2.5.");

        if (bearing.Clipped)
            c.Assume("The outstand c is cut back to the plate edge where it would pass it, so no area outside the plate is counted.", "Conservative against an uncut c.");

        GroutNote(c, plateB, plateL);

        c.Assign("N_j_Rd", "f_jd * A_eff", "kN", "Axial bearing resistance of the joint", "EN 1993-1-8 cl. 6.2.8.2, 6.2.5");
        c.Record("N_c_Ed", "max(N_Ed, 0)", $"N_Ed = {F(c.Si("N_Ed") / 1e3)} kN", ScalarValue.Of(Math.Max(c.Si("N_Ed"), 0) / 1e3, kN), "Design compression in the column");
        c.Assign("u_N", "N_c_Ed / N_j_Rd", null, "Axial bearing utilisation");

        var nCompression = Math.Max(c.Si("N_Ed"), 0) / 1e3;
        var outputs = new List<(string, string)> { ("f_cd", "Concrete design strength"), ("beta_j", "Joint coefficient β_j"), ("f_jd", "Bearing strength f_jd"), ("c", "Bearing outstand c"), ("A_eff", "Effective bearing area"), ("N_j_Rd", "Axial bearing resistance"), ("u_N", "Axial bearing utilisation"), ("t_req_N", "Plate thickness required for axial bearing alone"), ("t_req", "Plate thickness required (bearing, compression and tension T-stubs)") };
        var requiredN = RequiredThickness(section, plateB, plateL, fy, fjd, gM0, nCompression);
        TextValue? tReqNText = null;

        if (nCompression <= 0)
            tReqNText = new TextValue("— (no compression)");
        else if (requiredN is null)
            tReqNText = new TextValue("> 100 mm");
        else
            c.Record("t_req_N", "smallest t_p in 10–100 mm with f_jd·A_eff(c(t_p)) ≥ N_c,Ed (bisection)", $"N_c,Ed = {F(nCompression)} kN" + (requiredN <= MinThickness + 1e-9 ? "; carried at the 10 mm minimum" : string.Empty), ScalarValue.Of(requiredN.Value, mm), "Plate thickness required for axial bearing alone", "EN 1993-1-8 cl. 6.2.5");

        // Moment: the Table 6.7 regime, the compression lever arm and the bolt row forces.
        var nEd = c.Si("N_Ed") / 1e3;
        var mEd = c.Si("M_Ed") / 1e3;
        double zC;

        if (c.Has("z_C_in"))
        {
            c.Input("z_C_in");
            zC = c.Si("z_C_in") * 1e3;

            if (zC >= plateL / 2)
            {
                c.Block("z_C", $"z_C_in: the compression resultant ({F(zC)} mm from the column axis) must lie inside the plate, below L/2 = {F(plateL / 2)} mm. z_C is the distance to the centre of the compression flange (or wall), not the lever arm z to the bolt row.");
                return;
            }

            if (Math.Abs(zC - bearing.Centroid) > cMm)
                c.Warn("z_C_entered", $"z_C_in: {F(zC)} mm differs from the section's own z_C = {F(bearing.Centroid)} mm by more than the outstand c = {F(cMm)} mm. z_C is the distance from the column axis to the centre of the compression flange (or wall), not the lever arm z = z_C + x_b; check that the entered value is z_C.");

            c.Record("z_C", "z_C entered", $"{F(zC)} mm", ScalarValue.Of(zC, mm), "Compression resultant distance from the column axis", "EN 1993-1-8 cl. 6.2.8.3");
        }
        else
        {
            zC = bearing.Centroid;
            c.Record("z_C", bearing.CentroidFormula, bearing.CentroidSubstitution + " mm", ScalarValue.Of(zC, mm), "Compression resultant distance from the column axis", "EN 1993-1-8 cl. 6.2.8.3");
        }

        var z = zC + xB;
        var forces = Statics(nEd, mEd, zC, xB);
        var regime = forces.Regime switch
        {
            1 => "tension at one bolt row, compression under the other flange (Table 6.7, z = z_C + x_b)",
            2 => "both sides in compression (Table 6.7, z = 2·z_C)",
            _ => "both bolt rows in tension (Table 6.7, z = 2·x_b)",
        };

        c.Record("z", "z_C + x_b", $"{F(zC)} + {F(xB)}", ScalarValue.Of(z, mm), "Lever arm between the compression resultant and the bolt row", "EN 1993-1-8 cl. 6.2.8.3");

        switch (forces.Regime)
        {
            case 1:
                c.Record("C_Ed", "(M_Ed + N_Ed·x_b)/z", $"({F(mEd)} kN·m + {F(nEd)} kN × {F(xB)} mm)/{F(z)} mm", ScalarValue.Of(forces.C, kN), $"Compression resultant under the {Side(column)}", "EN 1993-1-8 cl. 6.2.8.3, Table 6.7");
                c.Record("T_Ed", "C_Ed − N_Ed", $"{F(forces.C)} − {F(nEd)}", ScalarValue.Of(forces.T, kN), "Tension resultant at the bolt row", "EN 1993-1-8 cl. 6.2.8.3, Table 6.7");
                break;
            case 2:
                c.Record("C_Ed", "N_Ed/2 + M_Ed/(2·z_C)", $"{F(nEd)}/2 + {F(mEd)} kN·m/(2 × {F(zC)} mm)", ScalarValue.Of(forces.C, kN), $"Compression resultant under the more loaded {Side(column)}", "EN 1993-1-8 cl. 6.2.8.3, Table 6.7");
                c.Record("T_Ed", "no tension", "both sides in compression", ScalarValue.Of(0, kN), "Tension resultant at the bolt row", "EN 1993-1-8 cl. 6.2.8.3, Table 6.7");
                break;
            default:
                c.Record("C_Ed", "no compression", "both rows in tension", ScalarValue.Of(0, kN), "Compression resultant", "EN 1993-1-8 cl. 6.2.8.3, Table 6.7");
                c.Record("T_Ed", "−N_Ed/2 + M_Ed/(2·x_b)", $"{F(-nEd)}/2 + {F(mEd)} kN·m/(2 × {F(xB)} mm)", ScalarValue.Of(forces.T, kN), "Tension resultant at the more loaded bolt row", "EN 1993-1-8 cl. 6.2.8.3, Table 6.7");
                break;
        }

        c.Record("C_Ed_2", forces.Regime == 2 ? "N_Ed/2 − M_Ed/(2·z_C)" : "none", forces.Regime == 2 ? $"{F(forces.C2)} kN" : "other side", ScalarValue.Of(forces.C2, kN), "Compression resultant under the other flange");
        c.Record("T_Ed_2", forces.Regime == 3 ? "−N_Ed/2 − M_Ed/(2·x_b)" : "none", forces.Regime == 3 ? $"{F(forces.T2)} kN" : "other row", ScalarValue.Of(forces.T2, kN), "Tension resultant at the other bolt row");
        c.Assign("F_t_Ed", "T_Ed / n_t", "kN", "Holding-down bolt tension per bolt at the more loaded row (→ PROFIS)", "EN 1993-1-8 cl. 6.2.6.12");
        outputs.AddRange([("z_C", "Compression lever arm z_C"), ("z", "Lever arm z"), ("C_Ed", "Compression resultant"), ("C_Ed_2", "Compression resultant, other side"), ("T_Ed", "Tension resultant, more loaded row"), ("T_Ed_2", "Tension resultant, other row"), ("F_t_Ed", "Bolt tension per bolt (to PROFIS)")]);
        var utilisations = new List<string> { "u_N" };
        var checkCompression = mEd > 0 && forces.C > 0;

        if (checkCompression)
        {
            c.Record("A_flange", bearing.FlangeFormula, bearing.FlangeSubstitution + " mm²", ScalarValue.Of(bearing.FlangeArea, "mm^2"), $"Bearing area of the {Side(column)}", "EN 1993-1-8 cl. 6.2.6.9");
            c.Assign("C_Rd", "f_jd * A_flange", "kN", $"Bearing resistance of the {Side(column)}", "EN 1993-1-8 cl. 6.2.6.9, 6.2.6.10");
            c.Assign("u_C", "C_Ed / C_Rd", null, $"{Capital(Side(column))} utilisation");
            outputs.AddRange([("A_flange", "Compression-side bearing area"), ("C_Rd", "Compression-side resistance"), ("u_C", "Compression-side utilisation")]);
            utilisations.Add("u_C");
        }

        // Tension T-stub of the plate at the bolts.
        c.Assign("M_pl_Rd", "0.25 * l_eff * t_p^2 * f_y / gamma_M0", "kN*m", "Plastic moment resistance of the plate over l_eff", "EN 1993-1-8 Table 6.2");
        var modes = new List<string>();

        if (prying)
        {
            var n = Math.Min(eMin, 1.25 * mDist);
            c.Assign("T_Rd_1", "4 * M_pl_Rd / m", "kN", "Mode 1: plate yields, no bolt failure", "EN 1993-1-8 Table 6.2");
            c.Record("n_pry", "min(e_min, 1.25·m)", $"min({F(eMin)}, 1.25 × {F(mDist)}) mm", ScalarValue.Of(n, mm), "Prying length n", "EN 1993-1-8 Table 6.2");
            c.Assign("T_Rd_2", "(2 * M_pl_Rd + n_pry * n_t * F_t_Rd) / (m + n_pry)", "kN", "Mode 2: plate yields and bolts fail", "EN 1993-1-8 Table 6.2");
            c.Assign("T_Rd_3", "n_t * F_t_Rd", "kN", "Mode 3: bolt failure", "EN 1993-1-8 Table 6.2");
            c.Assign("T_Rd", "min(T_Rd_1, T_Rd_2, T_Rd_3)", "kN", "Tension T-stub resistance", "EN 1993-1-8 cl. 6.2.4");
            modes.AddRange(["T_Rd_1", "T_Rd_2", "T_Rd_3"]);
            outputs.AddRange([("M_pl_Rd", "Plate plastic moment"), ("T_Rd_1", "T-stub mode 1"), ("n_pry", "Prying length n"), ("T_Rd_2", "T-stub mode 2"), ("T_Rd_3", "T-stub mode 3"), ("T_Rd", "Tension T-stub resistance")]);
            c.Warn("prying", "prying: the prying force Q is not included in F_t_Ed; Hilti PROFIS must add it to the bolt tension.");
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

        // Required thickness for bearing, the compression T-stub and the tension T-stub together.
        var required = RequiredThicknessAll(section, plateB, plateL, fy, fjd, gM0, nEd, mEd, xB, c.Has("z_C_in") ? zC : null, c.Si("l_eff") * 1e3, mDist, eMin, nT, c.Si("F_t_Rd") / 1e3, prying);
        var tpMm = c.Si("t_p") * 1e3;

        // One f_y at every thickness: EN 10025-2 steps the yield strength down for thicker product (the first step is above 16 mm).
        if (tpMm > FirstYieldStepMm || required.Value is > FirstYieldStepMm)
        {
            var above = new List<string>();

            if (tpMm > FirstYieldStepMm)
                above.Add($"t_p = {F(tpMm)} mm");

            if (required.Value is > FirstYieldStepMm and var rq)
                above.Add($"t_req = {F(rq)} mm");

            c.Warn("f_y_thickness", $"f_y: one yield strength ({F(fy)} MPa) is used at every plate thickness, but EN 10025-2 lowers the yield strength of a grade as the thickness passes {F(FirstYieldStepMm)} mm and again above 40 mm. {string.Join(" and ", above)} {(above.Count > 1 ? "are" : "is")} above the first step: enter the yield strength for the plate thickness in use (steel.strengths record), so M_pl,Rd and the T-stub are not overstated.");
        }

        // With the outstand cut at the plate edge the lever arm shortens as c grows, so a thicker plate can raise u_C (the required-thickness search takes the thinnest passing plate).
        if (checkCompression && bearing.Clipped && tpMm < MaxThickness)
        {
            var uHere = CompressionUtilisation(section, plateB, plateL, fy, fjd, gM0, nEd, mEd, xB, c.Has("z_C_in") ? zC : null, tpMm);
            var tThicker = Math.Min(1.25 * tpMm, MaxThickness);
            var uThicker = CompressionUtilisation(section, plateB, plateL, fy, fjd, gM0, nEd, mEd, xB, c.Has("z_C_in") ? zC : null, tThicker);

            if (uThicker > uHere * (1 + 1e-9))
                c.Warn("outstand-cut", $"t_p: the bearing outstand is cut back at the plate edge, so the compression lever arm shortens as the plate gets thicker and a thicker plate does not lower u_C here (u_C = {F(uHere)} at t_p = {F(tpMm)} mm, {F(uThicker)} at {F(tThicker)} mm). t_req is the thinnest plate that passes, not a lower bound for every thicker plate.");
        }

        if (required.Value is { } tReq)
            c.Record("t_req", "smallest t_p in 10–100 mm with u_N, u_C, u_T ≤ 1, with z_C, C_Ed and T_Ed recomputed at every trial t_p (scan and bisection)", $"N_c,Ed = {F(nCompression)} kN, C_Ed = {F(forces.C)} kN, T_Ed = {F(forces.T)} kN" + (tReq <= MinThickness + 1e-9 ? "; carried at the 10 mm minimum" : string.Empty), ScalarValue.Of(tReq, mm), "Plate thickness required", "EN 1993-1-8 cl. 6.2.5, 6.2.6.9, 6.2.6.11");

        // Shear: friction on the compression plus the anchor bolts (cl. 6.2.2).
        var fyb = c.Si("f_yb") / 1e6;
        var alphaBc = 0.44 - 0.0003 * fyb;
        c.Record("alpha_bc", "0.44 − 0.0003·f_yb", $"0.44 − 0.0003 × {F(fyb)}", new ScalarValue(alphaBc), "Anchor bolt shear factor α_bc", "EN 1993-1-8 cl. 6.2.2");

        if (c.Si("f_ub") / 1e6 > 800 + 1e-9)
        {
            var fubMpa = c.Si("f_ub") / 1e6;
            var detail = $"f_ub: α_bc = 0.44 − 0.0003·f_yb is defined for an anchor bolt yield strength of 235–640 MPa, so f_yb is limited to 640 MPa. A bolt of f_ub = {F(fubMpa)} MPa (class 10.9 and above) has a yield strength of about 900 MPa, where the formula gives α_bc = {F(0.44 - 0.0003 * 900)}, not {F(alphaBc)}; F_2,vb,Rd and u_V would be overstated.";

            if (c.Has("u_profis"))
                c.Warn("alpha_bc_profis", detail + " The anchor shear resistance is taken from the anchor design: u_profis (the PROFIS report's governing utilisation, which includes the steel shear failure of the anchors) is counted in u_max, so do not rely on u_V.");
            else
                c.Block("alpha_bc", detail + " α_bc is not defined above 800 MPa, so the shear check is not run: use f_ub of 800 MPa or less, or enter u_profis, the governing utilisation of the anchor design (PROFIS), which takes the anchor shear resistance.");
        }

        c.Assign("F_2_vb_Rd", "alpha_bc * f_ub * A_s / gamma_M2", "kN", "Anchor bolt shear resistance F_2,vb,Rd", "EN 1993-1-8 cl. 6.2.2");
        c.Assign("F_vb_Rd", "min(F_1_vb_Rd, F_2_vb_Rd)", "kN", "Anchor bolt shear resistance per bolt (bearing or shear)", "EN 1993-1-8 cl. 6.2.2");
        c.Assign("F_f_Rd", "C_fd * N_c_Ed", "kN", "Friction resistance, compression only", "EN 1993-1-8 cl. 6.2.2");
        c.Assign("V_Rd", "F_f_Rd + n_b * F_vb_Rd", "kN", "Shear resistance of the base", "EN 1993-1-8 cl. 6.2.2");
        c.Assign("u_V", "V_Ed / V_Rd", null, "Shear utilisation");
        var vb = Math.Max(c.Si("V_Ed") - c.Si("F_f_Rd"), 0) / 1e3 / nB;
        c.Record("V_b_Ed", "max(V_Ed − F_f,Rd, 0)/n_b", $"max({F(c.Si("V_Ed") / 1e3)} − {F(c.Si("F_f_Rd") / 1e3)}, 0)/{F(nB)}", ScalarValue.Of(vb, kN), "Shear per anchor bolt after friction (→ PROFIS)", "EN 1993-1-8 cl. 6.2.2");
        outputs.AddRange([("alpha_bc", "Anchor shear factor α_bc"), ("F_2_vb_Rd", "Anchor shear resistance F_2,vb,Rd"), ("F_vb_Rd", "Anchor shear resistance per bolt"), ("F_f_Rd", "Friction resistance"), ("V_Rd", "Shear resistance"), ("u_V", "Shear utilisation"), ("V_b_Ed", "Shear per anchor bolt (to PROFIS)")]);
        utilisations.Add("u_V");

        if (c.Has("u_profis"))
        {
            c.Input("u_profis");
            c.Assign("u_anchorage", "u_profis", null, "Anchorage utilisation (governing value of the PROFIS report)");
            utilisations.Add("u_anchorage");
            outputs.Add(("u_anchorage", "Anchorage utilisation (PROFIS)"));
        }

        var uMax = utilisations.Max(sym => c.Si(sym));
        c.Record("u_max", "max(" + string.Join(", ", utilisations) + ")", string.Join(", ", utilisations.Select(sym => $"{sym} = {F(c.Si(sym))}")), new ScalarValue(uMax), "Governing utilisation");
        outputs.Add(("u_max", "Governing utilisation"));

        foreach (var (symbol, text) in outputs)
        {
            switch (symbol)
            {
                case "t_req_N" when tReqNText is not null:
                    c.Output(symbol, text, tReqNText);
                    break;
                case "t_req" when required.Value is null:
                    c.Output(symbol, text, new TextValue(required.Text ?? "—"));
                    break;
                default:
                    c.Output(symbol, text);
                    break;
            }
        }

        c.Output("regime", "Force regime (Table 6.7)", new TextValue(regime));
        c.Output("T_mode", "Governing T-stub mode", new TextValue(modeName));

        // Warnings and checks.
        var basis = c.Has("alpha_basis") && !string.IsNullOrWhiteSpace(c.InputText("alpha_basis"));

        if (c.Si("alpha") > 1 + 1e-9 && !basis)
            c.Warn("alpha_basis", "alpha_basis: α above 1 needs the foundation geometry it rests on (A_c1/A_c0, α ≤ 3, EN 1992-1-1 cl. 6.7) stated.");

        c.CheckUtilisation("Concrete bearing under the axial force", "u_N", "EN 1993-1-8 cl. 6.2.8.2, 6.2.5");

        if (checkCompression)
            c.CheckUtilisation($"{Capital(Side(column))} bearing", "u_C", "EN 1993-1-8 cl. 6.2.6.9, 6.2.6.10");

        c.CheckUtilisation("Base plate tension T-stub (bending at the bolts)", "u_T", "EN 1993-1-8 cl. 6.2.6.11");
        c.CheckUtilisation("Holding-down bolt tension", "u_bt", "EN 1993-1-8 cl. 6.2.6.12");
        c.CheckUtilisation("Shear: friction plus anchor bolts", "u_V", "EN 1993-1-8 cl. 6.2.2");

        if (c.Has("u_profis"))
            c.CheckUtilisation("Anchorage (PROFIS report, governing utilisation)", "u_anchorage", "PROFIS report (EN 1992-4)");
        else
            c.Warn("anchorage-result", "u_profis: the anchorage is not assessed on this sheet. The concrete failure modes of the anchors (cone, pull-out, edge, shear, interaction), which usually govern, are designed in PROFIS; u_max covers the steel parts only, whether or not a report is attached. Enter the report's governing utilisation to include it.");

        var profis = c.Has("profis_ref") ? c.InputText("profis_ref").Trim() : string.Empty;

        // The anchorage is not designed on this sheet, so the sheet can only record where the design is filed: a check is made (and passes) only
        // when the reference is an attached Evidence document, never on typed text (Green G-A-03).
        if (profis.Length > 0 && c.Inputs.Find("profis_ref")?.Provenance is { Kind: CalcProvenanceKind.Evidence, Reference.Length: > 0 })
            c.CheckCondition("Anchorage designed externally (PROFIS report attached as Evidence)", true, $"external report attached: {profis}", $"bolt tension {F(c.Si("F_t_Ed") / 1e3)} kN and shear {F(vb)} kN per anchor designed in PROFIS");
        else if (profis.Length > 0)
            c.Warn("anchorage", $"profis_ref: '{profis}' is typed text, not an attached report; attach the Hilti PROFIS report as Evidence so the reference points at the filed document. The anchorage design is not checked on this sheet.");
        else
            c.Warn("anchorage", "profis_ref: not attached. The anchorage is designed outside this sheet; attach the Hilti PROFIS report as Evidence and enter its reference.");
    }

    /// <summary>The grout conditions behind β_j = 2/3: checked when the grout is entered, otherwise recorded as assumed.</summary>
    private static void GroutNote(CalcRunContext c, double plateB, double plateL)
    {
        var fck = c.Si("f_ck") / 1e6;
        var problems = new List<string>();

        if (c.Has("f_ck_g"))
        {
            c.Input("f_ck_g");

            if (c.Si("f_ck_g") / 1e6 < 0.2 * fck)
                problems.Add($"the grout strength {F(c.Si("f_ck_g") / 1e6)} MPa is below 0.2·f_ck = {F(0.2 * fck)} MPa");
        }

        if (c.Has("t_g"))
        {
            c.Input("t_g");

            if (c.Si("t_g") * 1e3 > 0.2 * Math.Min(plateB, plateL))
                problems.Add($"the grout thickness {F(c.Si("t_g") * 1e3)} mm exceeds 0.2 × the smaller plate dimension = {F(0.2 * Math.Min(plateB, plateL))} mm");
        }

        if (problems.Count > 0)
            c.Warn("grout", "f_ck_g: β_j = 2/3 does not apply (cl. 6.2.5): " + string.Join("; ", problems) + ".");
        else if (!c.Has("f_ck_g") || !c.Has("t_g"))
            c.Assume("β_j = 2/3 assumes the grout is at least 0.2·f_ck and no thicker than 0.2 × the smaller plate dimension; enter the grout strength and thickness to check it.", "EN 1993-1-8 cl. 6.2.5.");
    }

    /// <summary>Whether the dimensions a column type needs are entered; blocks naming the first missing one when not.</summary>
    private static bool Need(CalcRunContext c, string what, params string[] symbols)
    {
        var missing = symbols.FirstOrDefault(sym => !c.Has(sym));

        if (missing is null)
            return true;

        c.Block(what == "prying" ? "x_b" : "section", $"{missing}: {(what == "prying" ? "prying needs the edge distance e_min" : $"a {what} column needs {string.Join(", ", symbols)}")}; {missing} is not entered.");
        return false;
    }

    private static string Side(string column) => column switch { ColumnRhs => "compression wall", ColumnChs => "compression-side half annulus", _ => "compression flange" };

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private static string Cut(bool cut) => cut ? " (outstand cut back to the plate edge)" : string.Empty;

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
