using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Structural;

/// <summary>
/// S7 — EN 1993-1-1 lateral-torsional buckling (6.3.2), flexural buckling (6.3.1) and the bending-and-compression
/// interaction (6.3.3, Annex B or Annex A) of a doubly-symmetric I-section member of uniform section on fork supports.
/// M_cr comes from the general (NCCI SN003) expression with C₁, C₂, k, k_w and z_g; χ_LT from 6.3.2.2 (general case)
/// or 6.3.2.3 (rolled and welded equivalent sections). The section is given as inputs (or linked from S1), the
/// imperfection factors, β and λ̄_LT,0 are a small user-verifiable factor set, and no table of C₁/C₂ values or section
/// data is shipped. Every input has a hard range: outside it the sheet does not run.
/// </summary>
public sealed class LtbInteractionCalculator : ICalcCalculator
{
    public const string Id = "struct.ltb-interaction";

    public const string Edition2005 = "2005+A1:2014";
    public const string Edition2022 = "2022";
    public const string General = "6.3.2.2 (general case)";
    public const string Rolled = "6.3.2.3 (rolled and welded sections)";
    public const string AnnexBMethod = "Annex B (method 2)";
    public const string AnnexAMethod = "Annex A (method 1)";
    public const string Susceptible = "susceptible to torsional deformation (Table B.2)";
    public const string NotSusceptible = "not susceptible (Table B.1)";
    public const string ModePsi = "ψ and M_y,Ed";
    public const string ModeEnds = "end moments";
    /// <summary>The recommended λ̄_LT,0 and β of cl. 6.3.2.3 (the sheet's defaults): the pair for rolled sections, which a National Annex may replace for welded sections.</summary>
    public const double RecommendedLambdaLt0 = 0.4;

    public const double RecommendedBetaLt = 0.75;

    public const string RolledI = "rolled I";
    public const string WeldedI = "welded I";
    public const string CurveAuto = "auto (Table 6.4 / 6.5)";
    public const string FlexuralAuto = "auto (Table 6.2)";

    private static readonly string[] Curves = [FlexuralAuto, "a0", "a", "b", "c", "d"];

    /// <summary>The highest f_y (MPa) for which the sheet selects the Table 6.2 curves itself: the S 235 to S 420 column. Above it the table has another column and the curves are chosen by hand.</summary>
    public const double FlexuralAutoMaxFy = 420;

    /// <summary>The record kind a material from the W3 library fills. Equal to <c>MaterialLibrary.Kind</c>; the library assembly depends on this one's neighbours, so the text is repeated and a Library test pins the two together.</summary>
    public const string MaterialKind = "material.properties";

    public CalcTemplate Template { get; } = new(
        Id, "1.3.2", CalcDiscipline.Structural, "Lateral-torsional buckling and N+M interaction (EN 1993-1-1)",
        "Calculates the lateral-torsional buckling, flexural buckling and bending-with-compression interaction of a uniform doubly-symmetric I-section member on fork supports, to EN 1993-1-1. Class 4 sections are outside this sheet.",
        "Critical moment (NCCI SN003 form): M_cr = C₁·π²E·I_z/(kL)²·[√((k/k_w)²·I_w/I_z + (kL)²·G·I_t/(π²E·I_z) + (C₂z_g)²) − C₂z_g]; C₁ = 1.88 − 1.40ψ + 0.52ψ² ≤ 2.70 (end moments, k = 1) unless entered\n" +
        "LTB slenderness: λ̄_LT = √(W_y·f_y/M_cr), W_y = W_pl,y (class 1, 2) or W_el,y (class 3)\n" +
        "Lateral-torsional buckling, general case (6.3.2.2): Φ_LT = 0.5[1 + α_LT(λ̄_LT − 0.2) + λ̄_LT²]; χ_LT = 1/(Φ_LT + √(Φ_LT² − λ̄_LT²)) ≤ 1\n" +
        "Lateral-torsional buckling, rolled sections (6.3.2.3): Φ_LT = 0.5[1 + α_LT(λ̄_LT − λ̄_LT,0) + β·λ̄_LT²]; χ_LT = 1/(Φ_LT + √(Φ_LT² − β·λ̄_LT²)) ≤ 1, ≤ 1/λ̄_LT²; optional χ_LT,mod = χ_LT/f ≤ 1, ≤ 1/λ̄_LT² (eq. 6.58), f = 1 − 0.5(1 − k_c)[1 − 2(λ̄_LT − 0.8)²] ≤ 1\n" +
        "Buckling resistance in bending: M_b,Rd = χ_LT·W_y·f_y/γ_M1\n" +
        "Flexural buckling: N_cr = π²EI/L_cr², λ̄ = √(A·f_y/N_cr), Φ = 0.5[1 + α(λ̄ − 0.2) + λ̄²], χ = 1/(Φ + √(Φ² − λ̄²)) ≤ 1, N_b,Rd = χ·A·f_y/γ_M1; the curve of each axis from Table 6.2 (section type, h/b, t_f; f_y ≤ 420 MPa) or chosen\n" +
        "Interaction (6.3.3(4)): N_Ed/(χ_y·N_Rk/γ_M1) + k_yy·M_y,Ed/(χ_LT·M_y,Rk/γ_M1) + k_yz·M_z,Ed/(M_z,Rk/γ_M1) ≤ 1; N_Ed/(χ_z·N_Rk/γ_M1) + k_zy·M_y,Ed/(χ_LT·M_y,Rk/γ_M1) + k_zz·M_z,Ed/(M_z,Rk/γ_M1) ≤ 1\n" +
        "Factors k_ij: Annex B from the slenderness, N_Ed/(χ·N_Rk/γ_M1) and C_m (C_m = 0.6 + 0.4ψ ≥ 0.4 for a linear diagram, or entered); Annex A from the μ, w, n_pl, a_LT and C_ij expressions (class 1, 2: plastic column of Table A.1; class 3: elastic column, without the 0.6√(w_i/w_j) factors of k_yz, k_zy)",
        [
            new CalcInputSpec("edition", "EN 1993-1-1 edition (the 2022 choice changes the printed label only)", CalcInputKind.Choice, Choices: [Edition2005, Edition2022], DefaultChoice: Edition2005, Group: "Setup"),
            new CalcInputSpec("ltb_method", "Method for the lateral-torsional buckling reduction factor", CalcInputKind.Choice, Choices: [Rolled, General], DefaultChoice: Rolled, Group: "Setup"),
            new CalcInputSpec("annex", "Annex for the interaction factors", CalcInputKind.Choice, Choices: [AnnexBMethod, AnnexAMethod], DefaultChoice: AnnexBMethod, Group: "Setup"),
            new CalcInputSpec("torsion", "Member behaviour for Annex B", CalcInputKind.Choice, Choices: [Susceptible, NotSusceptible], DefaultChoice: Susceptible, Group: "Setup"),
            new CalcInputSpec("section_class", "Cross-section class (class 4 is outside this sheet)", CalcInputKind.Choice, Choices: ["1", "2", "3", "4"], DefaultChoice: "1", Group: "Setup"),
            new CalcInputSpec("section_type", "Section type, which selects the buckling curve of Table 6.4 or Table 6.5", CalcInputKind.Choice, Choices: [RolledI, WeldedI], DefaultChoice: RolledI, Group: "Setup"),
            new CalcInputSpec("moment_mode", "Moment diagram given as", CalcInputKind.Choice, Choices: [ModePsi, ModeEnds], DefaultChoice: ModePsi, Group: "Setup"),
            new CalcInputSpec("ltb_curve", "Buckling curve for lateral-torsional buckling", CalcInputKind.Choice, Choices: [CurveAuto, "a", "b", "c", "d"], DefaultChoice: CurveAuto, Group: "Setup"),
            new CalcInputSpec("curve_y", "Buckling curve about y-y (auto = Table 6.2 for f_y up to 420 MPa)", CalcInputKind.Choice, Choices: Curves, DefaultChoice: FlexuralAuto, Group: "Setup"),
            new CalcInputSpec("curve_z", "Buckling curve about z-z (auto = Table 6.2 for f_y up to 420 MPa)", CalcInputKind.Choice, Choices: Curves, DefaultChoice: FlexuralAuto, Group: "Setup"),
            new CalcInputSpec("use_f", "Apply the modification factor f of cl. 6.3.2.3(2) to the reduction factor", CalcInputKind.Boolean, Default: 0, Group: "Setup"),

            new CalcInputSpec("h", "Overall section depth", CalcInputKind.Quantity, "mm", Default: 467.2, Min: 100, Max: 1500, HardMin: 50, HardMax: 2500, Group: "Geometry"),
            new CalcInputSpec("b", "Flange width", CalcInputKind.Quantity, "mm", Default: 192.8, Min: 50, Max: 600, HardMin: 30, HardMax: 1000, Group: "Geometry"),
            new CalcInputSpec("t_f", "Flange thickness, which selects the flexural buckling curves of Table 6.2", CalcInputKind.Quantity, "mm", Default: 19.6, Min: 5, Max: 60, HardMin: 2, HardMax: 150, Group: "Geometry"),
            new CalcInputSpec("A", "Cross-section area", CalcInputKind.Quantity, "cm^2", Default: 125.27, Min: 10, Max: 1000, HardMin: 5, HardMax: 3000, Group: "Geometry"),
            new CalcInputSpec("I_y", "Second moment of area about y-y (major axis)", CalcInputKind.Quantity, "cm^4", Default: 45728, Min: 500, Max: 1e6, HardMin: 50, HardMax: 5e6, Group: "Geometry"),
            new CalcInputSpec("I_z", "Second moment of area about z-z (minor axis)", CalcInputKind.Quantity, "cm^4", Default: 2347.0, Min: 50, Max: 1e5, HardMin: 5, HardMax: 5e5, Group: "Geometry"),
            new CalcInputSpec("I_t", "Torsion constant", CalcInputKind.Quantity, "cm^4", Default: 121.29, Min: 1, Max: 1e4, HardMin: 0.1, HardMax: 1e5, Group: "Geometry"),
            new CalcInputSpec("I_w", "Warping constant", CalcInputKind.Quantity, "cm^6", UnitChangedAt: "1.3.0", UnitWas: "dm^6", Default: 1175500, Min: 1e3, Max: 1e9, HardMin: 10, HardMax: 1e11, Group: "Geometry"),
            new CalcInputSpec("W_pl_y", "Plastic section modulus about y-y", CalcInputKind.Quantity, "cm^3", Default: 2232.5, Min: 10, Max: 2e4, HardMin: 1, HardMax: 1e5, Group: "Geometry"),
            new CalcInputSpec("W_el_y", "Elastic section modulus about y-y", CalcInputKind.Quantity, "cm^3", Default: 1957.5, Min: 10, Max: 2e4, HardMin: 1, HardMax: 1e5, Group: "Geometry"),
            new CalcInputSpec("W_pl_z", "Plastic section modulus about z-z", CalcInputKind.Quantity, "cm^3", Default: 378.9, Min: 5, Max: 1e4, HardMin: 0.5, HardMax: 5e4, Group: "Geometry"),
            new CalcInputSpec("W_el_z", "Elastic section modulus about z-z", CalcInputKind.Quantity, "cm^3", Default: 243.47, Min: 5, Max: 1e4, HardMin: 0.5, HardMax: 5e4, Group: "Geometry"),
            new CalcInputSpec("L", "Length between lateral restraints (fork supports)", CalcInputKind.Quantity, "m", Default: 6, Min: 0.5, Max: 50, HardMin: 0.1, HardMax: 100, Group: "Geometry"),

            new CalcInputSpec("f_y", "Yield strength for the thickness", CalcInputKind.Quantity, "MPa", Default: 355, Min: 235, Max: 420, HardMin: 235, HardMax: 460, Group: "Material", MaterialProperty: "f_y"),
            new CalcInputSpec("E", "Young's modulus", CalcInputKind.Quantity, "GPa", Default: 210, Min: 200, Max: 215, HardMin: 190, HardMax: 220, Group: "Material", MaterialProperty: "E"),
            new CalcInputSpec("G", "Shear modulus", CalcInputKind.Quantity, "GPa", Default: 81, Min: 75, Max: 85, HardMin: 70, HardMax: 90, Group: "Material", MaterialProperty: "G"),

            new CalcInputSpec("N_Ed", "Design axial force, tension positive and compression negative (a positive value is outside the range)", CalcInputKind.Quantity, "kN", Default: -500, Min: -2e4, Max: 0, HardMin: -1e5, HardMax: 0, Group: "Loads"),
            new CalcInputSpec("M_z_Ed", "Greatest design moment about z-z", CalcInputKind.Quantity, "kN*m", Default: 0, Min: 0, Max: 1e4, HardMin: 0, HardMax: 1e5, Group: "Loads"),
            new CalcInputSpec("psi_z", "Ratio of the end moments about z-z", CalcInputKind.Number, Default: 1, Min: -0.9, Max: 1, HardMin: -1, HardMax: 1, Group: "Loads"),
            new CalcInputSpec("M_y_Ed", "Greatest design moment about y-y, used in the ψ mode (optional; blank = the sheet is blocked in the ψ mode)", CalcInputKind.Quantity, "kN*m", Default: 300, Min: 0, Max: 1e4, HardMin: 0, HardMax: 1e5, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("psi_y", "Ratio of the end moments about y-y, +1 for a uniform moment (optional; blank = 1, the conservative diagram)", CalcInputKind.Number, Default: 1, Min: -0.9, Max: 1, HardMin: -1, HardMax: 1, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("M_y_A", "Moment at end A about y-y as an ordinate of the bending-moment diagram, with the same sign at both ends for single curvature, unlike the end actions of a stiffness analysis (optional; blank = the sheet is blocked in the end-moment mode)", CalcInputKind.Quantity, "kN*m", Min: -1e4, Max: 1e4, HardMin: -1e5, HardMax: 1e5, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("M_y_B", "Moment at end B about y-y, with the same convention as end A: an ordinate of the bending-moment diagram, the same sign as A for single curvature (optional; blank = the sheet is blocked in the end-moment mode)", CalcInputKind.Quantity, "kN*m", Min: -1e4, Max: 1e4, HardMin: -1e5, HardMax: 1e5, IsOptional: true, Group: "Loads"),

            new CalcInputSpec("k", "Effective length factor for end rotation about the minor axis", CalcInputKind.Number, Default: 1.0, Min: 0.7, Max: 1.0, HardMin: 0.5, HardMax: 1.0, Group: "Conditions"),
            new CalcInputSpec("k_w", "Effective length factor for warping", CalcInputKind.Number, Default: 1.0, Min: 0.7, Max: 1.0, HardMin: 0.5, HardMax: 1.0, Group: "Conditions"),
            new CalcInputSpec("z_g", "Load height above the shear centre, positive towards the compression flange (destabilising)", CalcInputKind.Quantity, "mm", Default: 0, Min: -500, Max: 500, HardMin: -1000, HardMax: 1000, Group: "Conditions"),
            new CalcInputSpec("L_cr_y", "Buckling length about y-y", CalcInputKind.Quantity, "m", Default: 6, Min: 0.5, Max: 50, HardMin: 0.1, HardMax: 100, Group: "Conditions"),
            new CalcInputSpec("L_cr_z", "Buckling length about z-z", CalcInputKind.Quantity, "m", Default: 6, Min: 0.5, Max: 50, HardMin: 0.1, HardMax: 100, Group: "Conditions"),

            new CalcInputSpec("gamma_M1", "Partial factor for member buckling", CalcInputKind.Number, Default: 1.0, Min: 1.0, Max: 1.1, HardMin: 1.0, HardMax: 1.25, Group: "Factors"),
            new CalcInputSpec("alpha_a0", "Imperfection factor for curve a0 (recommended 0.13)", CalcInputKind.Number, Default: 0.13, Min: 0.1, Max: 0.8, HardMin: 0.05, HardMax: 1.0, Group: "Factors"),
            new CalcInputSpec("alpha_a", "Imperfection factor for curve a (recommended 0.21)", CalcInputKind.Number, Default: 0.21, Min: 0.1, Max: 0.8, HardMin: 0.05, HardMax: 1.0, Group: "Factors"),
            new CalcInputSpec("alpha_b", "Imperfection factor for curve b (recommended 0.34)", CalcInputKind.Number, Default: 0.34, Min: 0.1, Max: 0.8, HardMin: 0.05, HardMax: 1.0, Group: "Factors"),
            new CalcInputSpec("alpha_c", "Imperfection factor for curve c (recommended 0.49)", CalcInputKind.Number, Default: 0.49, Min: 0.1, Max: 0.8, HardMin: 0.05, HardMax: 1.0, Group: "Factors"),
            new CalcInputSpec("alpha_d", "Imperfection factor for curve d (recommended 0.76)", CalcInputKind.Number, Default: 0.76, Min: 0.1, Max: 0.8, HardMin: 0.05, HardMax: 1.0, Group: "Factors"),
            new CalcInputSpec("lambda_LT0", "Plateau slenderness for cl. 6.3.2.3 (recommended 0.4)", CalcInputKind.Number, Default: 0.4, Min: 0.2, Max: 0.4, HardMin: 0.2, HardMax: 0.5, Group: "Factors"),
            new CalcInputSpec("beta_LT", "Slenderness correction factor for cl. 6.3.2.3 (recommended 0.75)", CalcInputKind.Number, Default: 0.75, Min: 0.6, Max: 1.0, HardMin: 0.5, HardMax: 1.0, Group: "Factors"),
            new CalcInputSpec("C_2", "Moment diagram factor C₂ (0 for end moments; enter it for a transverse load)", CalcInputKind.Number, Default: 0, Min: 0, Max: 1.0, HardMin: 0, HardMax: 1.5, Group: "Factors"),
            new CalcInputSpec("C_1", "Moment diagram factor C₁ (optional; blank = derived from ψ, valid for end moments with k = 1)", CalcInputKind.Number, Min: 1.0, Max: 3.0, HardMin: 1.0, HardMax: 4.0, IsOptional: true, Group: "Factors"),
            new CalcInputSpec("k_c", "Correction factor for the f modification (optional; blank = 1/(1.33 − 0.33ψ) for a linear diagram)", CalcInputKind.Number, Min: 0.6, Max: 1.0, HardMin: 0.5, HardMax: 1.0, IsOptional: true, Group: "Factors"),
            new CalcInputSpec("C_m_y", "Equivalent uniform moment factor about y-y in Annex B (optional; blank = 0.6 + 0.4ψ_y, at least 0.4)", CalcInputKind.Number, Min: 0.5, Max: 1.0, HardMin: 0.4, HardMax: 1.0, IsOptional: true, Group: "Factors"),
            new CalcInputSpec("C_m_z", "Equivalent uniform moment factor about z-z in Annex B (optional; blank = 0.6 + 0.4ψ_z, at least 0.4)", CalcInputKind.Number, Min: 0.5, Max: 1.0, HardMin: 0.4, HardMax: 1.0, IsOptional: true, Group: "Factors"),
            new CalcInputSpec("C_m_LT", "Equivalent uniform moment factor for lateral-torsional buckling in Annex B (optional; blank = the value of C_my)", CalcInputKind.Number, Min: 0.5, Max: 1.0, HardMin: 0.4, HardMax: 1.0, IsOptional: true, Group: "Factors"),
        ],
        [
            new CalcAssumption("The member is uniform, doubly symmetric and on fork supports, free to warp and to rotate about the minor axis.", "Mono-symmetric, tapered and cantilever members and continuous restraint are not modelled."),
            new CalcAssumption("A k or k_w below 1 models partial restraint of the end rotation or of the warping.", "The factors k and k_w are entered, not derived."),
            new CalcAssumption("The cross-section class is the class the user states, 1, 2 or 3.", "Effective properties of class 4 sections are not derived."),
            new CalcAssumption("The factor C₁ from the ψ formula applies to end-moment diagrams with k = 1.", "For other diagrams the user enters C₁, C₂, k_c and C_my from the source in use."),
            new CalcAssumption("No table of C₁ or C₂ values is shipped, so the user takes them from the source in use.", "The factors are stored as reference records."),
            new CalcAssumption("The imperfection factors, β and λ̄_LT,0 default to the recommended values, which the National Annex may change.", "They are a factor set the user can verify."),
            new CalcAssumption("The 2022 edition choice changes the printed label only, and the arithmetic stays that of the 2005+A1:2014 edition.", "The differences between the editions cannot be stated with certainty, and the sheet warns."),
            new CalcAssumption("The flexural buckling curves chosen automatically are those of Table 6.2 for S 235 to S 420.", "For a higher f_y or a combination without a row the user chooses the curve."),
            new CalcAssumption("The Annex A and Annex B expressions are implemented as published.", "The user checks their coefficients against the owner's copy of the standard."),
        ],
        [
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "cl. 6.3.1.2, 6.3.1.3", "Buckling curves; slenderness for flexural buckling"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "cl. 6.3.2.2", "Lateral-torsional buckling curves: general case"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "cl. 6.3.2.3", "Lateral-torsional buckling curves for rolled sections and equivalent welded sections"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "cl. 6.3.3", "Uniform members in bending and axial compression"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "Annex A, Annex B", "Interaction factors k_ij: methods 1 and 2"),
            new CalcClauseRef("NCCI SN003", "Mcr expression", "Elastic critical moment of a doubly-symmetric member"),
            new CalcClauseRef("BS EN 1993-1-1:2022", "cl. 8.3", "Stability of members (label only, see the edition note)"),
        ],
        [
            new CalcApplicabilityRule("class-4", "Class 4 cross-sections need effective properties: outside this sheet.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("section", "The plastic moduli cannot be below the elastic moduli.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("end-moments", "End-moment mode needs both end moments.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("n-cr", "The design compression must stay below the elastic buckling forces (Annex A).", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("edition", "The 2022 edition choice changes the label only.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("moment", "The ψ mode needs the greatest moment M_y,Ed: a blank one is refused, since the template default of 300 kN·m is not the member's moment (give the diagram as end moments instead).", CalcNoticeSeverity.Block, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("k_c", "With C₁ entered the f modification needs k_c entered (unless the diagram is given as end moments).", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("C_m_y", "With C₁ entered Annex B needs C_my entered (unless the diagram is given as end moments).", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("C_m_LT", "Table B.2 divides by C_mLT − 0.25: C_mLT must exceed 0.25.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("curve", "The automatic flexural buckling curve is not defined for this section (f_y above 420 MPa, or a Table 6.2 combination without a row): choose the curve.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("engine", "The elastic critical moment could not be evaluated for these inputs.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("c1-psi", "C₁ from the ψ formula applies to end moments with k = 1 and can exceed the exact elastic value by up to about 10 % when ψ < 0.25; Annex A takes C_my,0 from ψ.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("table-b1", "Table B.1 is chosen for an open I-section whose χ_LT is below 1, which is susceptible to torsional deformation unless it is restrained against twist: the check uses Table B.2 unless a basis for B.1 is recorded.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("zg-c2", "z_g has no effect while C₂ = 0.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("double-curvature", "End moments of opposite sign are taken as double curvature (ψ < 0, C₁ up to its cap): bending-moment-diagram ordinates, not stiffness-analysis end actions.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("use-f", "The f modification belongs to 6.3.2.3 and is not applied with 6.3.2.2.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("annex-a", "Annex A coefficients are to be checked against the standard.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("slender", "Non-dimensional slenderness above 2.0.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("welded-pair", "A welded section checked by 6.3.2.3 with λ̄_LT,0 and β at the rolled-section pair and no recorded basis: the National Annex may give another pair for welded sections.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
        ],
        [
            new CalcTestCase("ka-annex-b-class2", "Annex B, Table B.2, class 2: N = 1200 kN with M_y = 200 kN·m (ψ = 0.5) and M_z = 15 kN·m",
                new CalcReferenceSource("Analytic from the Annex B expressions as implemented, recomputed in python; the coefficients of Tables B.1 and B.2 are typed from the published expressions and are to be checked against the owner's copy of the standard", "EN 1993-1-1:2005+A1:2014",
                    "Same section, 6.3.2.3 (curve d), C₁ = 1.31, M_cr = 521.515 kN·m, λ̄_LT = 1.194575, χ_LT = 0.457851; C_my = C_mLT = 0.8, C_mz = 1.0; n_y = 0.1, n_z = 0.50548: k_yy = 0.848513, k_yz = 0.6·k_zz = 0.960284, k_zz = 1.600473, k_zy = 0.917840; equations 6.61 = 0.94775, 6.62 = 1.24809"),
                [.. WeldedSection(), Choice("section_class", "2"), Choice("ltb_method", Rolled), N("psi_y", 0.5), Q("M_y_Ed", 200, "kN*m"), Q("M_z_Ed", 15, "kN*m"), Q("N_Ed", -1200, "kN"), Q("L_cr_y", 6, "m"), Q("L_cr_z", 3, "m"), Choice("curve_y", "b"), Choice("curve_z", "c")],
                [new CalcExpectedValue("C_1", 1.31, "", 1e-12), new CalcExpectedValue("Mcr", 521.515295718, "kN*m", 1e-6), new CalcExpectedValue("chi_LT", 0.457851025394, "", 1e-6), new CalcExpectedValue("k_yy", 0.848513034679, "", 1e-6), new CalcExpectedValue("k_yz", 0.96028367882, "", 1e-6), new CalcExpectedValue("k_zy", 0.91783995565, "", 1e-6), new CalcExpectedValue("k_zz", 1.60047279803, "", 1e-6), new CalcExpectedValue("u_61", 0.947749247887, "", 1e-6), new CalcExpectedValue("u_62", 1.24809447033, "", 1e-6)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-mcr-uniform-6322", "Welded I 500×200×16/10, S355, 6 m, uniform moment: M_cr, λ̄_LT, χ_LT (6.3.2.2, curve d), M_b,Rd",
                new CalcReferenceSource("Analytic: the classical closed form M_cr = (π/L)·√(E·I_z·G·I_t)·√(1 + π²E·I_w/(L²·G·I_t)) and the 6.3.2.2 formulae, recomputed in python", "EN 1993-1-1:2005+A1:2014",
                    "Plate section properties computed from the plate dimensions, no fillet welds (A = 110.8 cm², I_y = 46036.549 cm⁴, I_z = 2137.2333 cm⁴, I_t = 70.21333 cm⁴, I_w = I_z,flanges·h_s²/4 = 1.2493653 dm⁶ with h_s = 484 mm, W_pl,y = 2096.36 cm³); E = 210 GPa, G = 81 GPa, f_y = 355 MPa, L = 6 m, ψ = 1 so C₁ = 1: M_cr = 398.103 kN·m, λ̄_LT = √(2096.36 cm³ × 355 MPa/M_cr) = 1.367254, curve d (welded, h/b = 2.5 > 2) α = 0.76, χ_LT = 0.315851, M_b,Rd = 235.059 kN·m"),
                [.. WeldedSection(), Choice("ltb_method", General), N("psi_y", 1), Q("M_y_Ed", 150, "kN*m"), Q("N_Ed", 0, "kN")],
                [new CalcExpectedValue("C_1", 1.0, "", 1e-12), new CalcExpectedValue("Mcr", 398.103279174, "kN*m", 1e-6), new CalcExpectedValue("lambda_LT", 1.3672540904, "", 1e-6), new CalcExpectedValue("alpha_LT", 0.76, "", 1e-12), new CalcExpectedValue("chi_LT", 0.315850961286, "", 1e-6), new CalcExpectedValue("Mb_Rd", 235.058749027, "kN*m", 1e-6), new CalcExpectedValue("u_LTB", 0.638138340398, "", 1e-6)]),
            new CalcTestCase("ka-6323-f-modified", "Same section, 6.3.2.3 with the f modification, triangular moment (ψ = 0)",
                new CalcReferenceSource("Analytic (6.3.2.3 formulae with β = 0.75, λ̄_LT,0 = 0.4; k_c = 1/(1.33 − 0.33ψ)), recomputed in python", "EN 1993-1-1:2005+A1:2014",
                    "ψ = 0: C₁ = 1.88, M_cr = 748.434 kN·m, λ̄_LT = 0.997173; curve d (welded h/b > 2): χ_LT = 0.561531 (≤ 1/λ̄_LT² = 1.0057); k_c = 0.751880, f = 1 − 0.5(1 − k_c)[1 − 2(λ̄_LT − 0.8)²] = 0.885586, χ_LT,mod = 0.634078; M_b,Rd = 471.886 kN·m"),
                [.. WeldedSection(), Choice("ltb_method", Rolled), N("psi_y", 0), Q("M_y_Ed", 250, "kN*m"), Q("N_Ed", 0, "kN"), CalcInputValue.Switch("use_f", true)],
                [new CalcExpectedValue("C_1", 1.88, "", 1e-12), new CalcExpectedValue("Mcr", 748.434164847, "kN*m", 1e-6), new CalcExpectedValue("lambda_LT", 0.997172531358, "", 1e-6), new CalcExpectedValue("chi_LT_0", 0.561530882199, "", 1e-6), new CalcExpectedValue("k_c", 0.751879699248, "", 1e-9), new CalcExpectedValue("f", 0.885586024324, "", 1e-6), new CalcExpectedValue("chi_LT", 0.634078301573, "", 1e-6), new CalcExpectedValue("Mb_Rd", 471.886017841, "kN*m", 1e-6), new CalcExpectedValue("u_LTB", 0.529788954425, "", 1e-6)]),
            new CalcTestCase("ka-mcr-k-zg-c2", "M_cr with k = 0.8, entered C₁ = 1.35, C₂ = 0.45 and a load 250 mm above the shear centre",
                new CalcReferenceSource("Analytic (the general M_cr expression written out term by term), recomputed in python", "NCCI SN003 form",
                    "C₁ = 1.35, C₂ = 0.45, z_g = +250 mm (destabilising), k = 0.8, k_w = 1: M_cr = 1.35·π²E·I_z/(0.8·6000)²·[√(0.64·I_w/I_z + 4800²·G·I_t/(π²E·I_z) + 112.5²) − 112.5] = 440.518 kN·m; λ̄_LT = 1.29977; curve d, 6.3.2.3: χ_LT = 0.411642, M_b,Rd = 306.347 kN·m. With z_g = −250 mm (load hung from the bottom flange) M_cr = 1024.51 kN·m. C_my = 1 is entered (C₁ is entered, so it is not taken from ψ); it does not enter the LTB figures."),
                [.. WeldedSection(), Choice("ltb_method", Rolled), N("k", 0.8), N("k_w", 1.0), N("C_1", 1.35), N("C_2", 0.45), Q("z_g", 250, "mm"), N("C_m_y", 1.0), N("psi_y", 1), Q("M_y_Ed", 150, "kN*m"), Q("N_Ed", 0, "kN")],
                [new CalcExpectedValue("Mcr", 440.518293012, "kN*m", 1e-6), new CalcExpectedValue("lambda_LT", 1.29976588824, "", 1e-6), new CalcExpectedValue("chi_LT", 0.411641587937, "", 1e-6), new CalcExpectedValue("Mb_Rd", 306.346880547, "kN*m", 1e-6)]),
            new CalcTestCase("ka-flexural-buckling", "Flexural buckling of the same section, curves selected from Table 6.2 (welded, t_f 16 mm: b about y-y, c about z-z): L_cr,y = 6 m, L_cr,z = 3 m, N = 1200 kN",
                new CalcReferenceSource("Analytic (6.3.1.2 formulae), recomputed in python", "EN 1993-1-1:2005+A1:2014",
                    "N_Rk = 110.8 cm² × 355 MPa = 3933.4 kN; N_cr,y = 26504.5 kN, N_cr,z = 4921.85 kN; λ̄_y = 0.385234, λ̄_z = 0.893964; χ_y = 0.931891, χ_z = 0.603545; N_b,y,Rd = 3665.50 kN, N_b,z,Rd = 2373.98 kN; utilisation 1200/2373.98 = 0.50548"),
                [.. WeldedSection(), Q("N_Ed", -1200, "kN"), Q("M_y_Ed", 0, "kN*m"), Q("L_cr_y", 6, "m"), Q("L_cr_z", 3, "m")],
                [new CalcExpectedValue("N_Rk", 3933.4, "kN", 1e-9), new CalcExpectedValue("N_cr_y", 26504.4809114886, "kN", 1e-6), new CalcExpectedValue("N_cr_z", 4921.851086325, "kN", 1e-6), new CalcExpectedValue("lambda_y", 0.3852338182, "", 1e-6), new CalcExpectedValue("lambda_z", 0.8939635714, "", 1e-6), new CalcExpectedValue("chi_y", 0.9318906542, "", 1e-6), new CalcExpectedValue("chi_z", 0.6035449215, "", 1e-6), new CalcExpectedValue("N_b_Rd", 2373.9835943742, "kN", 1e-6), new CalcExpectedValue("u_buckling", 0.505479483, "", 1e-6)]),
            new CalcTestCase("ka-annex-b-class3-b1", "Annex B, Table B.1, class 3 (elastic moduli): the same actions",
                new CalcReferenceSource("Analytic from the Annex B expressions as implemented, recomputed in python; coefficients to be checked against the owner's copy of the standard", "EN 1993-1-1:2005+A1:2014",
                    "Class 3: W_el governs, λ̄_LT = 1.119598, χ_LT = 0.494537; Table B.1 elastic: k_yy = C_my(1 + 0.6λ̄_y n_y) = 0.860536, k_yz = k_zz = 1.271128, k_zy = 0.8·k_yy = 0.688429; 6.61 = 1.11105, 6.62 = 1.18268"),
                [.. WeldedSection(), Choice("section_class", "3"), Choice("ltb_method", Rolled), CalcInputValue.Choice("torsion", NotSusceptible, CalcProvenance.Assumed("Table B.1 is checked here as the standard states it: the member is taken as not susceptible to torsional deformation.")), N("psi_y", 0.5), Q("M_y_Ed", 200, "kN*m"), Q("M_z_Ed", 15, "kN*m"), Q("N_Ed", -1200, "kN"), Q("L_cr_y", 6, "m"), Q("L_cr_z", 3, "m"), Choice("curve_y", "b"), Choice("curve_z", "c")],
                [new CalcExpectedValue("lambda_LT", 1.11959778027, "", 1e-6), new CalcExpectedValue("chi_LT", 0.49453731243, "", 1e-6), new CalcExpectedValue("k_yy", 0.860536013656, "", 1e-6), new CalcExpectedValue("k_yz", 1.27112814635, "", 1e-6), new CalcExpectedValue("k_zy", 0.688428810924, "", 1e-6), new CalcExpectedValue("k_zz", 1.27112814635, "", 1e-6), new CalcExpectedValue("u_61", 1.11104576816, "", 1e-6), new CalcExpectedValue("u_62", 1.18267539476, "", 1e-6)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-annex-a-class2", "Annex A, class 2: the actions of the Annex B case",
                new CalcReferenceSource("Analytic from the Annex A expressions as implemented (an independent python transcription of the same expressions); the coefficients are to be checked against the owner's copy of the standard", "EN 1993-1-1:2005+A1:2014",
                    "N_cr,T = 2962.46 kN, λ̄_0 = 1.36725, a_LT = 0.998475, C_my = 0.948882, C_mz = 1.058807, C_mLT = 1.340335: k_yy = 1.348996, k_yz = 2.166104, k_zy = 0.749357, k_zz = 1.926807; 6.61 = 1.39512, 6.62 = 1.19077"),
                [.. WeldedSection(), Choice("section_class", "2"), Choice("ltb_method", Rolled), Choice("annex", AnnexAMethod), N("psi_y", 0.5), Q("M_y_Ed", 200, "kN*m"), Q("M_z_Ed", 15, "kN*m"), Q("N_Ed", -1200, "kN"), Q("L_cr_y", 6, "m"), Q("L_cr_z", 3, "m"), Choice("curve_y", "b"), Choice("curve_z", "c")],
                [new CalcExpectedValue("k_yy", 1.34899628461, "", 1e-6), new CalcExpectedValue("k_yz", 2.16610447704, "", 1e-6), new CalcExpectedValue("k_zy", 0.749356563725, "", 1e-6), new CalcExpectedValue("k_zz", 1.92680651092, "", 1e-6), new CalcExpectedValue("u_61", 1.39511815954, "", 1e-6), new CalcExpectedValue("u_62", 1.19077069504, "", 1e-6)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-annex-a-class3", "Annex A, class 3 (Table A.1, elastic column): the same actions",
                new CalcReferenceSource("Analytic from the Annex A expressions as implemented, the elastic column of Table A.1 (no 0.6√(w_i/w_j) factors in k_yz and k_zy; w = 1 so every C_ij = 1), recomputed in python; the coefficients are to be checked against the owner's copy of the standard", "EN 1993-1-1:2005+A1:2014",
                    "Class 3: W_el governs, λ̄_LT = 1.119598, χ_LT = 0.494537, λ̄_0 = 1.281438; N_cr,T = 2962.46 kN, a_LT = 0.998475, C_my = 0.948882, C_mz = 1.058807, C_mLT = 1.340335: k_yy = 1.327845, k_yz = C_mz·μ_y/(1 − N/N_cr,z) = 1.395680, k_zy = C_my·C_mLT·μ_z/(1 − N/N_cr,y) = 1.181153, k_zz = 1.241494; 6.61 = 1.42477, 6.62 = 1.48164. With the plastic 0.6 factors kept (the error this case guards against) k_yz would be 0.8374 and k_zy 0.7087."),
                [.. WeldedSection(), Choice("section_class", "3"), Choice("ltb_method", Rolled), Choice("annex", AnnexAMethod), N("psi_y", 0.5), Q("M_y_Ed", 200, "kN*m"), Q("M_z_Ed", 15, "kN*m"), Q("N_Ed", -1200, "kN"), Q("L_cr_y", 6, "m"), Q("L_cr_z", 3, "m"), Choice("curve_y", "b"), Choice("curve_z", "c")],
                [new CalcExpectedValue("lambda_LT", 1.11959778027, "", 1e-6), new CalcExpectedValue("chi_LT", 0.49453731243, "", 1e-6), new CalcExpectedValue("k_yy", 1.32784479795, "", 1e-6), new CalcExpectedValue("k_yz", 1.39568003974, "", 1e-6), new CalcExpectedValue("k_zy", 1.18115263105, "", 1e-6), new CalcExpectedValue("k_zz", 1.24149385047, "", 1e-6), new CalcExpectedValue("u_61", 1.42476704306, "", 1e-6), new CalcExpectedValue("u_62", 1.48163658227, "", 1e-6)],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Members",
        OutputPorts:
        [
            new CalcOutputPort("Mb_Rd", "kN*m", "Design buckling resistance moment", "ltb.resistance_moment"),
            new CalcOutputPort("Mcr", "kN*m", "Elastic critical moment for lateral-torsional buckling", "ltb.moment_critical"),
            new CalcOutputPort("chi_LT", "", "Lateral-torsional reduction factor used", "ltb.reduction_ltb"),
            new CalcOutputPort("lambda_LT", "", "Lateral-torsional non-dimensional slenderness", "ltb.slenderness_ltb"),
            new CalcOutputPort("N_b_Rd", "kN", "Flexural buckling resistance, the smaller of the two axes", "ltb.resistance_buckling"),
            new CalcOutputPort("u_LTB", "", "Lateral-torsional buckling utilisation", "ltb.utilisation_ltb"),
            new CalcOutputPort("u_interaction", "", "Greatest utilisation of the two interaction equations 6.61 and 6.62", "ltb.utilisation_interaction"),
            new CalcOutputPort("u_member", "", "Governing utilisation of the member: the greatest of buckling, LTB and the interaction ratios", "ltb.utilisation_member"),
        ],
        RecordKinds:
        [
            new CalcRecordKind(MaterialKind, "Material properties", ["E", "G", "f_y"], "A material from the library: its properties fill these inputs, each citing the material, its revision and the page or table of the value."),
            new CalcRecordKind("ec3.national-annex", "EN 1993-1-1 National Annex factors", ["gamma_M1"], "From the National Annex in use, with its edition."),
            new CalcRecordKind("ec3.ltb-factors", "Imperfection factors and 6.3.2.3 parameters", ["alpha_a0", "alpha_a", "alpha_b", "alpha_c", "alpha_d", "lambda_LT0", "beta_LT"], "The imperfection factors of the buckling curves, λ̄_LT,0 and β, from the National Annex in use (the recommended values are the defaults), with the edition."),
            new CalcRecordKind("ec3.mcr-factors", "C₁, C₂ and k_c for the moment diagram", ["C_1", "C_2", "k_c"], "C₁, C₂ and k_c for the actual loading and end conditions, from the source in use (SN003, a published table or an LTB program), with its edition."),
        ]);

    /// <summary>The 500×200×16/10 welded I from its plate dimensions (no fillet welds): the properties are exact for the plates (I_w = I_z of the two flanges × h_s²/4, the web adding nothing to the warping constant), so a known-answer case can be recomputed by hand.</summary>
    private static CalcInputValue[] WeldedSection() =>
    [
        Choice("section_type", WeldedI), Q("h", 500, "mm"), Q("b", 200, "mm"), Q("t_f", 16, "mm"), Q("A", 110.8, "cm^2"), Q("I_y", 46036.54933333333, "cm^4"), Q("I_z", 2137.2333333333333, "cm^4"), Q("I_t", 70.21333333333334, "cm^4"),
        Q("I_w", 1249365.3333333332, "cm^6"), Q("W_pl_y", 2096.36, "cm^3"), Q("W_el_y", 1841.4619733333332, "cm^3"), Q("W_pl_z", 331.7, "cm^3"), Q("W_el_z", 213.72333333333333, "cm^3"),
    ];

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue N(string symbol, double value) => CalcInputValue.Of(symbol, value);

    private static CalcInputValue Choice(string symbol, string value) => CalcInputValue.Choice(symbol, value);

    /// <summary>The curve of Table 6.4 (6.3.2.2) or Table 6.5 (6.3.2.3) for the section type and h/b.</summary>
    public static string AutoCurve(bool general, bool welded, double hOverB)
    {
        var deep = hOverB > 2;

        if (general)
            return welded ? (deep ? "d" : "c") : (deep ? "b" : "a");

        return welded ? (deep ? "d" : "c") : (deep ? "c" : "b");
    }

    /// <summary>
    /// The flexural buckling curves about y-y and z-z of Table 6.2 for S 235 to S 420 by section type, h/b and flange thickness, or
    /// <see langword="null"/> where the table has no row (a rolled I with h/b &gt; 1.2 and t_f above 100 mm).
    /// </summary>
    public static (string Y, string Z)? AutoFlexuralCurves(bool welded, double hOverB, double flangeThicknessMm)
    {
        if (welded)
            return flangeThicknessMm <= 40 ? ("b", "c") : ("c", "d");

        if (hOverB > 1.2)
        {
            if (flangeThicknessMm <= 40)
                return ("a", "b");

            return flangeThicknessMm <= 100 ? ("b", "c") : null;
        }

        return flangeThicknessMm <= 100 ? ("b", "c") : ("d", "d");
    }

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_61", "u_62", "u_LTB", "u_M_y", "u_M_z", "u_N_y", "u_N_z", "u_buckling", "u_interaction", "u_member");
        var cls = c.InputText("section_class");

        if (cls == "4")
        {
            c.Block("class-4", "section_class: choose class 1, 2 or 3, because class 4 sections are outside this sheet.");
            return;
        }

        foreach (var s in new[] { "f_y", "E", "G", "gamma_M1", "h", "b", "t_f", "A", "I_y", "I_z", "I_t", "I_w", "W_pl_y", "W_el_y", "W_pl_z", "W_el_z", "L", "k", "k_w", "z_g", "C_2", "N_Ed", "M_z_Ed", "psi_z", "L_cr_y", "L_cr_z" })
            c.Input(s);

        if (c.InputSi("W_pl_y") < c.InputSi("W_el_y") || c.InputSi("W_pl_z") < c.InputSi("W_el_z"))
        {
            c.Block("section", "W_pl_y: a plastic modulus cannot be below the elastic modulus. Enter W_pl_y ≥ W_el_y and W_pl_z ≥ W_el_z.");
            return;
        }

        var edition = c.InputText("edition");
        var general = c.InputText("ltb_method") == General;
        var annexA = c.InputText("annex") == AnnexAMethod;
        var torsion = c.InputText("torsion") == Susceptible;
        var welded = c.InputText("section_type") == WeldedI;
        var plastic = cls is "1" or "2";

        c.Output("edition_used", "EN 1993-1-1 edition", new TextValue(edition == Edition2005 ? "EN 1993-1-1:2005+A1:2014" : "EN 1993-1-1:2022 (arithmetic of 2005+A1:2014)"));

        if (edition == Edition2022)
            c.Warn("edition", "edition: the 2022 choice changes only the printed label, not the arithmetic of EN 1993-1-1:2005+A1:2014. Check the 2022 clauses before relying on this sheet.");

        // Units in this method: N, mm, MPa.
        var fy = c.InputSi("f_y") / 1e6;
        var e = c.InputSi("E") / 1e6;
        var g = c.InputSi("G") / 1e6;
        var gM1 = c.InputSi("gamma_M1");
        var h = c.InputSi("h") * 1e3;
        var b = c.InputSi("b") * 1e3;
        var area = c.InputSi("A") * 1e6;
        var iy = c.InputSi("I_y") * 1e12;
        var iz = c.InputSi("I_z") * 1e12;
        var it = c.InputSi("I_t") * 1e12;
        var iw = c.InputSi("I_w") * 1e18;
        var wply = c.InputSi("W_pl_y") * 1e9;
        var wely = c.InputSi("W_el_y") * 1e9;
        var wplz = c.InputSi("W_pl_z") * 1e9;
        var welz = c.InputSi("W_el_z") * 1e9;
        var length = c.InputSi("L") * 1e3;
        var k = c.InputSi("k");
        var kw = c.InputSi("k_w");
        var zg = c.InputSi("z_g") * 1e3;
        var c2 = c.InputSi("C_2");
        var nSigned = c.InputSi("N_Ed");
        var mzEd = c.InputSi("M_z_Ed") * 1e3;
        var psiZ = c.InputSi("psi_z");
        var lcrY = c.InputSi("L_cr_y") * 1e3;
        var lcrZ = c.InputSi("L_cr_z") * 1e3;

        if (Math.Abs(zg) > 1e-12 && c2 == 0)
            c.Warn("zg-c2", "z_g: the load height has no effect while C₂ = 0. Enter C₂ and C₁ for a transverse load, or set z_g to 0.");

        // The moment diagram about y-y.
        double myEd, psiY;
        var shapeEntered = c.InputText("moment_mode") == ModeEnds;

        if (c.InputText("moment_mode") == ModeEnds)
        {
            if (!c.Has("M_y_A") || !c.Has("M_y_B"))
            {
                c.Block("end-moments", "M_y_A: enter both end moments M_y_A and M_y_B in the end-moment mode.");
                return;
            }

            c.Input("M_y_A");
            c.Input("M_y_B");
            var ma = c.InputSi("M_y_A") * 1e3;
            var mb = c.InputSi("M_y_B") * 1e3;
            var big = Math.Abs(ma) >= Math.Abs(mb) ? ma : mb;
            var small = Math.Abs(ma) >= Math.Abs(mb) ? mb : ma;
            myEd = Math.Abs(big);
            psiY = myEd < 1e-9 ? 1.0 : small / big;
            c.Record("M_y_Ed", "max(|M_A|, |M_B|)", $"max(|{F(ma / 1e6)}|, |{F(mb / 1e6)}|) kN·m", ScalarValue.Of(myEd / 1e6, "kN*m"), "Greatest design moment about y-y", "6.3.2");
            c.Record("psi_y", "ψ = M_small/M_large (same sign: single curvature)", $"{F(small / 1e6)} / {F(big / 1e6)}", new ScalarValue(psiY), "Ratio of the end moments, taken as ordinates of the bending-moment diagram (same sign at both ends: single curvature)", "Table B.3");

            if (psiY < 0)
            {
                // A frame program prints member end actions, which have OPPOSITE signs for single curvature. Typed that way the diagram reads as a reversal: C₁ rises to its cap and the check can turn from FAIL to PASS without a sign of it.
                c.Warn("double-curvature", $"M_y_B: opposite signs ({F(ma / 1e6)} and {F(mb / 1e6)} kN·m) make this double curvature, so enter same-sign ordinates for single curvature. This uses {UsedFactors(c, psiY, annexA)}.");
            }
        }
        else
        {
            // M_y_Ed is optional on the template because the end-moment mode does not use it; the ψ mode does, and a blank one is refused (it ran on the template default of 300 kN·m, a
            // moment nobody entered). ψ_y blank is the uniform moment, ψ = 1, which is the conservative diagram: it is listed as a default used.
            if (!c.Has("M_y_Ed"))
            {
                c.Block("moment", "M_y_Ed: enter the greatest design moment about y-y, or give the moment diagram as end moments.");
                return;
            }

            c.Input("M_y_Ed");
            c.Input("psi_y");
            myEd = c.InputSi("M_y_Ed") * 1e3;
            psiY = c.InputSi("psi_y");
        }

        c.Output("psi_y", "ψ of the M_y diagram");
        c.Output("M_y_Ed", "Greatest design moment about y-y");
        var compression = nSigned < 0;
        var nEd = compression ? -nSigned : 0.0;
        c.Record("N_abs", "abs(N_Ed)", "the compression force, positive", ScalarValue.Of(nEd / 1e3, "kN"), "Axial force magnitude");

        // ----------------------------------------------------------------- lateral-torsional buckling
        double c1;
        var c1Entered = c.Has("C_1");

        if (c1Entered)
        {
            c.Input("C_1");
            c1 = c.InputSi("C_1");
        }
        else
        {
            c1 = LtbInteractionMath.C1FromPsi(psiY);
            c.Record("C_1", "1.88 − 1.40ψ + 0.52ψ² ≤ 2.70", $"ψ = {F(psiY)}", new ScalarValue(c1), "C₁ for end moments (derived)", "NCCI SN003 / ENV 1993-1-1 Annex F");

            if (psiY < 0.25)
                c.Warn("c1-psi", $"C_1: the ψ formula gives C₁ = {F(c1)} for ψ = {F(psiY)}, up to 10 % above the exact value. Check C₁ in an elastic buckling program and enter it.");

            if (Math.Abs(k - 1) > 1e-12)
                c.Warn("c1-psi", "k: the ψ formula for C₁ applies to k = 1. Enter C₁ for the actual end conditions.");

            if (Math.Abs(c2 * zg) > 1e-12)
                c.Warn("c1-psi", "z_g: the ψ formula for C₁ describes end moments, where C₂ is 0. Enter C₁ and C₂ for a transverse load.");
        }

        c.Output("C_1", "C₁", new ScalarValue(c1));
        var mcr = LtbInteractionMath.Mcr(e, g, iz, it, iw, length, k, kw, c1, c2, zg);

        if (!(mcr > 0) || !double.IsFinite(mcr))
        {
            c.Block("engine", "L: the elastic critical moment could not be evaluated for these inputs.");
            return;
        }

        c.Record("Mcr", "C₁·π²E·I_z/(kL)²·[√((k/k_w)²I_w/I_z + (kL)²G·I_t/(π²E·I_z) + (C₂z_g)²) − C₂z_g]",
            $"{F(c1)} × π² × {F(e)} × {F(iz)} / ({F(k * length)})² × [√(({F(k)}/{F(kw)})² × {F(iw)}/{F(iz)} + ({F(k * length)})² × {F(g)} × {F(it)} / (π² × {F(e)} × {F(iz)}) + ({F(c2)} × {F(zg)})²) − {F(c2 * zg)}]",
            ScalarValue.Of(mcr / 1e6, "kN*m"), "Elastic critical moment for lateral-torsional buckling", "NCCI SN003");

        var wy = plastic ? wply : wely;
        c.Record("W_y", plastic ? "W_pl,y (class 1, 2)" : "W_el,y (class 3)", plastic ? "plastic" : "elastic", ScalarValue.Of(wy / 1e3, "cm^3"), "Section modulus used for bending about y", "6.3.2.2(1)");
        var lamLt = Math.Sqrt(wy * fy / mcr);
        c.Record("lambda_LT", "√(W_y·f_y/M_cr)", $"√({F(wy / 1e3)} cm³ × {F(fy)} MPa / {F(mcr / 1e6)} kN·m)", new ScalarValue(lamLt), "Non-dimensional slenderness for lateral-torsional buckling", "6.3.2.2(1)");

        var hOverB = h / b;
        var curve = c.InputText("ltb_curve");
        var curveAuto = curve == CurveAuto;

        if (curveAuto)
            curve = AutoCurve(general, welded, hOverB);

        foreach (var s in new[] { "alpha_a0", "alpha_a", "alpha_b", "alpha_c", "alpha_d" })
            c.Input(s);

        var alphaLt = Alpha(c, curve);
        c.Record("alpha_LT", $"imperfection factor, curve {curve}", curveAuto ? $"{(general ? "Table 6.4" : "Table 6.5")}, {(welded ? "welded" : "rolled")}, h/b = {F(hOverB)}" : "chosen", new ScalarValue(alphaLt), "Imperfection factor for lateral-torsional buckling", general ? "6.3.2.2(2), Table 6.4" : "6.3.2.3(1), Table 6.5");

        double chiLt;
        var lam0 = 0.2;
        var beta = 1.0;

        if (general)
        {
            chiLt = LtbInteractionMath.ChiLtGeneral(lamLt, alphaLt);
            var phiLt = 0.5 * (1 + alphaLt * (lamLt - 0.2) + lamLt * lamLt);
            c.Record("Phi_LT", "0.5[1 + α_LT(λ̄_LT − 0.2) + λ̄_LT²]", $"0.5 × [1 + {F(alphaLt)} × ({F(lamLt)} − 0.2) + {F(lamLt)}²]", new ScalarValue(phiLt), "Φ_LT", "6.3.2.2(1)");
            c.Record("chi_LT_0", "1/(Φ_LT + √(Φ_LT² − λ̄_LT²)) ≤ 1", lamLt <= 0.2 ? $"λ̄_LT {F(lamLt)} ≤ 0.2: no reduction" : $"1/({F(phiLt)} + √({F(phiLt)}² − {F(lamLt)}²))", new ScalarValue(chiLt), "Lateral-torsional reduction factor", "6.3.2.2(1)");
        }
        else
        {
            c.Input("lambda_LT0");
            c.Input("beta_LT");
            lam0 = c.InputSi("lambda_LT0");
            beta = c.InputSi("beta_LT");
            chiLt = LtbInteractionMath.ChiLtRolled(lamLt, alphaLt, lam0, beta);

            // DC-03 S7 (owner decision DC-21): the recommended pair of 6.3.2.3 is the one for rolled sections; a National Annex may give another pair for welded
            // sections, which the sheet does not hold (a licensed value: record kind ec3.ltb-factors). A basis recorded on both inputs makes the pair the person's.
            if (welded && Math.Abs(lam0 - RecommendedLambdaLt0) < 1e-12 && Math.Abs(beta - RecommendedBetaLt) < 1e-12 && !(c.HasRecordedBasis("lambda_LT0") && c.HasRecordedBasis("beta_LT")))
                c.Warn("welded-pair", "section_type: the section is welded, but λ̄_LT,0 = 0.4 and β = 0.75 are the rolled-section pair of clause 6.3.2.3. The National Annex may set another pair for welded sections, so enter lambda_LT0 and beta_LT from it.");

            var phiLt = 0.5 * (1 + alphaLt * (lamLt - lam0) + beta * lamLt * lamLt);
            c.Record("Phi_LT", "0.5[1 + α_LT(λ̄_LT − λ̄_LT,0) + β·λ̄_LT²]", $"0.5 × [1 + {F(alphaLt)} × ({F(lamLt)} − {F(lam0)}) + {F(beta)} × {F(lamLt)}²]", new ScalarValue(phiLt), "Φ_LT", "6.3.2.3(1)");
            c.Record("chi_LT_0", "1/(Φ_LT + √(Φ_LT² − β·λ̄_LT²)) ≤ 1, ≤ 1/λ̄_LT²", lamLt <= lam0 ? $"λ̄_LT {F(lamLt)} ≤ λ̄_LT,0 {F(lam0)}: no reduction" : $"1/({F(phiLt)} + √({F(phiLt)}² − {F(beta)} × {F(lamLt)}²))", new ScalarValue(chiLt), "Lateral-torsional reduction factor", "6.3.2.3(1)");
        }

        var chiUsed = chiLt;

        if (c.InputFlag("use_f"))
        {
            if (general)
            {
                c.Warn("use-f", "use_f: the f modification belongs to 6.3.2.3 and is not applied with 6.3.2.2.");
            }
            else
            {
                double kc;

                if (c.Has("k_c"))
                {
                    c.Input("k_c");
                    kc = c.InputSi("k_c");
                }
                else if (c1Entered && !shapeEntered)
                {
                    c.Block("k_c", "k_c: enter k_c for the actual diagram, because C₁ is entered and ψ no longer describes it. Alternatively, enter the diagram as end moments.");
                    return;
                }
                else
                {
                    kc = LtbInteractionMath.KcFromPsi(psiY);
                    c.Record("k_c", "1/(1.33 − 0.33ψ)", $"ψ = {F(psiY)}", new ScalarValue(kc), "k_c for a linear moment diagram (derived)", "Table 6.6");
                }

                var f = LtbInteractionMath.FModification(kc, lamLt);
                c.Record("f", "1 − 0.5(1 − k_c)[1 − 2(λ̄_LT − 0.8)²] ≤ 1", $"1 − 0.5 × (1 − {F(kc)}) × [1 − 2 × ({F(lamLt)} − 0.8)²]", new ScalarValue(f), "Modification factor f", "6.3.2.3(2)");
                chiUsed = LtbInteractionMath.ChiLtModified(chiLt, f, lamLt);
                c.Record("chi_LT_mod", "χ_LT/f ≤ 1, ≤ 1/λ̄_LT² (eq. 6.58)", $"{F(chiLt)} / {F(f)}, 1/λ̄_LT² = {F(1 / (lamLt * lamLt))}", new ScalarValue(chiUsed), "Modified lateral-torsional reduction factor", "6.3.2.3(2)");
            }
        }

        c.Record("chi_LT", general ? "χ_LT (6.3.2.2)" : (c.InputFlag("use_f") && !general ? "χ_LT,mod" : "χ_LT (6.3.2.3)"), "as above", new ScalarValue(chiUsed), "Lateral-torsional reduction factor used", general ? "6.3.2.2" : "6.3.2.3");
        var mbRd = chiUsed * wy * fy / gM1;
        c.Record("Mb_Rd", "χ_LT·W_y·f_y/γ_M1", $"{F(chiUsed)} × {F(wy / 1e3)} cm³ × {F(fy)} MPa / {F(gM1)}", ScalarValue.Of(mbRd / 1e6, "kN*m"), "Design buckling resistance moment", "6.3.2.1(3)");
        c.Assign("u_LTB", "M_y_Ed / Mb_Rd", null, "Lateral-torsional buckling utilisation");

        // ----------------------------------------------------------------- flexural buckling
        var curveY = c.InputText("curve_y");
        var curveZ = c.InputText("curve_z");
        var flexAuto = curveY == FlexuralAuto || curveZ == FlexuralAuto;

        if (flexAuto)
        {
            var tf = c.InputSi("t_f") * 1e3;
            var picked = AutoFlexuralCurves(welded, hOverB, tf);

            if (fy > FlexuralAutoMaxFy + 1e-9)
            {
                c.Block("curve", $"{(curveY == FlexuralAuto ? "curve_y" : "curve_z")}: the automatic curves are those of Table 6.2 for S 235 to S 420 (f_y up to {FlexuralAutoMaxFy:0} MPa); choose the buckling curve by hand for f_y = {F(fy)} MPa.");
                return;
            }

            if (picked is null)
            {
                c.Block("curve", $"{(curveY == FlexuralAuto ? "curve_y" : "curve_z")}: Table 6.2 has no row for a rolled I-section with h/b > 1.2 and t_f above 100 mm; choose the buckling curve by hand.");
                return;
            }

            if (curveY == FlexuralAuto)
                curveY = picked.Value.Y;

            if (curveZ == FlexuralAuto)
                curveZ = picked.Value.Z;

            c.Assume($"Flexural buckling curves {curveY} (y-y) and {curveZ} (z-z) from Table 6.2: {(welded ? "welded" : "rolled")} I-section, h/b = {F(hOverB)}, t_f = {F(tf)} mm.", "Automatic selection; choose the curves by hand to override.");
        }

        var alphaY = Alpha(c, curveY);
        var alphaZ = Alpha(c, curveZ);
        c.Record("alpha_y", $"imperfection factor, curve {curveY}", flexAuto && c.InputText("curve_y") == FlexuralAuto ? "Table 6.2" : "chosen", new ScalarValue(alphaY), "Imperfection factor about y", "6.3.1.2, Table 6.2");
        c.Record("alpha_z", $"imperfection factor, curve {curveZ}", flexAuto && c.InputText("curve_z") == FlexuralAuto ? "Table 6.2" : "chosen", new ScalarValue(alphaZ), "Imperfection factor about z", "6.3.1.2, Table 6.2");
        var nRk = area * fy;
        var ncrY = Math.PI * Math.PI * e * iy / (lcrY * lcrY);
        var ncrZ = Math.PI * Math.PI * e * iz / (lcrZ * lcrZ);
        var lamY = Math.Sqrt(nRk / ncrY);
        var lamZ = Math.Sqrt(nRk / ncrZ);
        var chiY = LtbInteractionMath.ChiFlexural(lamY, alphaY);
        var chiZ = LtbInteractionMath.ChiFlexural(lamZ, alphaZ);
        c.Record("N_Rk", "A·f_y", $"{F(area / 100)} cm² × {F(fy)} MPa", ScalarValue.Of(nRk / 1e3, "kN"), "Characteristic axial resistance", "6.3.1.1(3)");
        c.Record("N_cr_y", "π²E·I_y/L_cr,y²", $"π² × {F(e)} × {F(iy)} / {F(lcrY)}²", ScalarValue.Of(ncrY / 1e3, "kN"), "Elastic critical force about y", "6.3.1.2");
        c.Record("N_cr_z", "π²E·I_z/L_cr,z²", $"π² × {F(e)} × {F(iz)} / {F(lcrZ)}²", ScalarValue.Of(ncrZ / 1e3, "kN"), "Elastic critical force about z", "6.3.1.2");
        c.Record("lambda_y", "√(A·f_y/N_cr,y)", $"√({F(nRk / 1e3)} / {F(ncrY / 1e3)})", new ScalarValue(lamY), "Non-dimensional slenderness about y", "6.3.1.3");
        c.Record("lambda_z", "√(A·f_y/N_cr,z)", $"√({F(nRk / 1e3)} / {F(ncrZ / 1e3)})", new ScalarValue(lamZ), "Non-dimensional slenderness about z", "6.3.1.3");
        c.Record("chi_y", "1/(Φ + √(Φ² − λ̄²)) ≤ 1", $"α = {F(alphaY)}, λ̄ = {F(lamY)}", new ScalarValue(chiY), "Reduction factor about y", "6.3.1.2");
        c.Record("chi_z", "1/(Φ + √(Φ² − λ̄²)) ≤ 1", $"α = {F(alphaZ)}, λ̄ = {F(lamZ)}", new ScalarValue(chiZ), "Reduction factor about z", "6.3.1.2");
        c.Record("N_b_y_Rd", "χ_y·A·f_y/γ_M1", $"{F(chiY)} × {F(nRk / 1e3)} / {F(gM1)}", ScalarValue.Of(chiY * nRk / gM1 / 1e3, "kN"), "Buckling resistance about y", "6.3.1.1(3)");
        c.Record("N_b_z_Rd", "χ_z·A·f_y/γ_M1", $"{F(chiZ)} × {F(nRk / 1e3)} / {F(gM1)}", ScalarValue.Of(chiZ * nRk / gM1 / 1e3, "kN"), "Buckling resistance about z", "6.3.1.1(3)");
        c.Assign("N_b_Rd", "min(N_b_y_Rd, N_b_z_Rd)", "kN", "Flexural buckling resistance (smaller of the two axes)");

        foreach (var (axis, lam) in new[] { ("y", lamY), ("z", lamZ) })
        {
            if (lam > 2.0)
                c.Warn("slender", $"L_cr_{axis}: non-dimensional slenderness {F(lam)} exceeds 2.0.");
        }

        // ----------------------------------------------------------------- interaction
        var mySlab = plastic ? wply : wely;
        var mzSlab = plastic ? wplz : welz;
        var myRk = mySlab * fy;
        var mzRk = mzSlab * fy;
        var ny = nEd / (chiY * nRk / gM1);
        var nz = nEd / (chiZ * nRk / gM1);
        c.Record("M_y_Rk", "W_y·f_y", $"{F(mySlab / 1e3)} cm³ × {F(fy)} MPa", ScalarValue.Of(myRk / 1e6, "kN*m"), "Characteristic bending resistance about y", "6.3.3");
        c.Record("M_z_Rk", "W_z·f_y (W_pl,z class 1, 2; W_el,z class 3)", $"{F(mzSlab / 1e3)} cm³ × {F(fy)} MPa", ScalarValue.Of(mzRk / 1e6, "kN*m"), "Characteristic bending resistance about z", "6.3.3");

        LtbInteractionMath.Factors k2;
        var b1Replaced = false;
        var torsionUsed = torsion;

        if (annexA)
        {
            c.Warn("annex-a", "annex: the Annex A expressions are implemented as published. Check their coefficients against the standard before relying on the result.");
            var iZeroSq = (iy + iz) / area;
            var lT = kw * length;
            var ncrT = (g * it + Math.PI * Math.PI * e * iw / (lT * lT)) / iZeroSq;
            var mcr0 = LtbInteractionMath.Mcr(e, g, iz, it, iw, length, k, kw, 1.0, 0.0, 0.0);
            var lambda0 = Math.Sqrt(wy * fy / mcr0);
            var aLt = Math.Max(0.0, 1 - it / iy);

            if (nEd >= Math.Min(ncrY, Math.Min(ncrZ, ncrT)))
            {
                c.Block("n-cr", $"N_Ed: the compression of {F(nEd / 1e3)} kN reaches N_cr,y {F(ncrY / 1e3)}, N_cr,z {F(ncrZ / 1e3)} or N_cr,T {F(ncrT / 1e3)} kN. The Annex A factors are not defined there.");
                return;
            }

            c.Record("N_cr_T", "(G·I_t + π²E·I_w/l_T²)/i₀², i₀² = (I_y + I_z)/A, l_T = k_w·L", $"({F(g)} × {F(it)} + π² × {F(e)} × {F(iw)} / {F(lT)}²) / {F(iZeroSq)}", ScalarValue.Of(ncrT / 1e3, "kN"), "Elastic torsional buckling force", "Annex A Table A.2");
            c.Record("lambda_0", "√(W_y·f_y/M_cr,0), M_cr,0 for C₁ = 1", $"M_cr,0 = {F(mcr0 / 1e6)} kN·m", new ScalarValue(lambda0), "Lateral-torsional slenderness for a uniform moment", "Annex A Table A.2");
            c.Record("a_LT", "1 − I_t/I_y ≥ 0", $"1 − {F(it / 1e4)} / {F(iy / 1e4)}", new ScalarValue(aLt), "a_LT", "Annex A Table A.1");
            k2 = LtbInteractionMath.AnnexA(new LtbInteractionMath.AnnexAInput(
                nEd, ncrY, ncrZ, ncrT, chiY, chiZ, chiUsed, lamY, lamZ, lambda0, c1, psiY, psiZ, myEd, mzEd, plastic ? wply * fy / gM1 : wely * fy / gM1, plastic ? wplz * fy / gM1 : welz * fy / gM1,
                nRk / gM1, area, wely, plastic ? wply / wely : 1.0, plastic ? wplz / welz : 1.0, aLt, plastic));

            if (c1Entered && !shapeEntered)
                c.Warn("c1-psi", "C_1: Annex A takes C_my,0 from ψ_y, which describes only a linear diagram. Check that ψ_y represents the actual diagram, or enter end moments.");
        }
        else
        {
            if (c1Entered && !shapeEntered && !c.Has("C_m_y"))
            {
                c.Block("C_m_y", "C_m_y: enter C_my for the actual diagram, because C₁ is entered and ψ no longer describes it. Alternatively, enter the diagram as end moments.");
                return;
            }

            var cmy = c.Has("C_m_y") ? c.InputSi("C_m_y") : LtbInteractionMath.CmFromPsi(psiY);
            var cmz = c.Has("C_m_z") ? c.InputSi("C_m_z") : LtbInteractionMath.CmFromPsi(psiZ);
            var cmLt = c.Has("C_m_LT") ? c.InputSi("C_m_LT") : cmy;
            c.Record("C_my", "0.6 + 0.4ψ_y ≥ 0.4 (or entered)", c.Has("C_m_y") ? "entered" : $"ψ_y = {F(psiY)}", new ScalarValue(cmy), "Equivalent uniform moment factor, y", "Table B.3");
            c.Record("C_mz", "0.6 + 0.4ψ_z ≥ 0.4 (or entered)", c.Has("C_m_z") ? "entered" : $"ψ_z = {F(psiZ)}", new ScalarValue(cmz), "Equivalent uniform moment factor, z", "Table B.3");
            c.Record("C_mLT", "as C_my (or entered)", c.Has("C_m_LT") ? "entered" : "C_my", new ScalarValue(cmLt), "Equivalent uniform moment factor for lateral-torsional buckling", "Table B.3");

            if (torsion && cmLt <= 0.25 + 1e-9)
            {
                c.Block("C_m_LT", "C_m_LT: enter a C_mLT above 0.25 (Table B.2 divides by C_mLT − 0.25).");
                return;
            }

            // Table B.1 is for a member not susceptible to torsional deformation. An open I-section that buckles laterally (χ_LT < 1) is susceptible unless it is restrained against twist, and
            // Table B.1 then gives k_zy = 0.6·k_yy, far below Table B.2's (about 20 % unconservative). A B.1 choice with no recorded basis is therefore checked on Table B.2; one the person
            // recorded a basis for (an assumption, a specification, a drawing) is theirs, with the warning kept.
            b1Replaced = !torsion && chiUsed < 1 - 1e-9 && !c.HasRecordedBasis("torsion");
            torsionUsed = torsion || b1Replaced;

            if (b1Replaced && cmLt <= 0.25 + 1e-9)
            {
                c.Block("C_m_LT", "C_m_LT: enter a C_mLT above 0.25 (Table B.2 divides by C_mLT − 0.25).");
                return;
            }

            k2 = LtbInteractionMath.AnnexB(ny, nz, lamY, lamZ, cmy, cmz, cmLt, plastic, torsionUsed);
        }

        if (annexA)
        {
            c.Record("C_my", "C_my,0 + (1 − C_my,0)·√ε_y·a_LT/(1 + √ε_y·a_LT)", "Annex A Table A.2", new ScalarValue(k2.Cmy), "Equivalent uniform moment factor, y", "Annex A Table A.2");
            c.Record("C_mz", "C_mz,0", "Annex A Table A.2", new ScalarValue(k2.Cmz), "Equivalent uniform moment factor, z", "Annex A Table A.2");
            c.Record("C_mLT", "C_my²·a_LT/√((1 − N/N_cr,z)(1 − N/N_cr,T)) ≥ 1", "Annex A Table A.2", new ScalarValue(k2.CmLT), "Equivalent uniform moment factor for lateral-torsional buckling", "Annex A Table A.2");
        }

        var table = annexA ? "Annex A Table A.1" : (torsionUsed ? "Annex B Table B.2" : "Annex B Table B.1");

        if (b1Replaced)
            c.Warn("table-b1", $"torsion: Table B.1 is chosen, but this I-section buckles laterally (χ_LT = {F(chiUsed)}), so the check uses Table B.2. Record the basis for Table B.1 on this input to use it.");
        else if (!annexA && !torsion && chiUsed < 1 - 1e-9)
            c.Warn("table-b1", $"torsion: Table B.1 is used on your recorded basis, but this I-section buckles laterally (χ_LT = {F(chiUsed)}). Table B.1 gives a smaller k_zy than Table B.2, so the interaction can be 20 % unconservative.");
        c.Record("k_yy", annexA ? "Annex A: C_my·C_mLT·μ_y/(1 − N/N_cr,y)/C_yy" : "Annex B: C_my(1 + (λ̄_y − 0.2)n_y) ≤ C_my(1 + 0.8n_y) (class 1, 2)", $"n_y = {F(ny)}", new ScalarValue(k2.Kyy), "Interaction factor k_yy", table);
        c.Record("k_yz", annexA ? "Annex A: C_mz·μ_y/(1 − N/N_cr,z)/C_yz·0.6√(w_z/w_y)" : "Annex B: 0.6k_zz (class 1, 2), k_zz (class 3)", "as above", new ScalarValue(k2.Kyz), "Interaction factor k_yz", table);
        c.Record("k_zy", annexA ? "Annex A: C_my·C_mLT·μ_z/(1 − N/N_cr,y)/C_zy·0.6√(w_y/w_z)" : "Annex B: Table B.1 0.6k_yy / 0.8k_yy; Table B.2 from λ̄_z, n_z and C_mLT", $"n_z = {F(nz)}", new ScalarValue(k2.Kzy), "Interaction factor k_zy", table);
        c.Record("k_zz", annexA ? "Annex A: C_mz·μ_z/(1 − N/N_cr,z)/C_zz" : "Annex B: C_mz(1 + (2λ̄_z − 0.6)n_z) ≤ C_mz(1 + 1.4n_z) (class 1, 2)", "as above", new ScalarValue(k2.Kzz), "Interaction factor k_zz", table);

        var uNy = ny;
        var uNz = nz;
        var uMy = myEd / (chiUsed * myRk / gM1);
        var uMz = mzEd / (mzRk / gM1);
        c.Record("u_N_y", "N_Ed/(χ_y·N_Rk/γ_M1)", $"{F(nEd / 1e3)} kN / ({F(chiY)} × {F(nRk / 1e3)} kN / {F(gM1)})", new ScalarValue(uNy), "Axial term about y", "6.3.3(4)");
        c.Record("u_N_z", "N_Ed/(χ_z·N_Rk/γ_M1)", $"{F(nEd / 1e3)} kN / ({F(chiZ)} × {F(nRk / 1e3)} kN / {F(gM1)})", new ScalarValue(uNz), "Axial term about z", "6.3.3(4)");
        c.Record("u_M_y", "M_y,Ed/(χ_LT·M_y,Rk/γ_M1)", $"{F(myEd / 1e6)} kN·m / ({F(chiUsed)} × {F(myRk / 1e6)} kN·m / {F(gM1)})", new ScalarValue(uMy), "Major-axis bending term", "6.3.3(4)");
        c.Record("u_M_z", "M_z,Ed/(M_z,Rk/γ_M1)", $"{F(mzEd / 1e6)} kN·m / ({F(mzRk / 1e6)} kN·m / {F(gM1)})", new ScalarValue(uMz), "Minor-axis bending term", "6.3.3(4)");
        c.Record("u_61", "N_Ed/(χ_y N_Rk/γ_M1) + k_yy·M_y,Ed/(χ_LT M_y,Rk/γ_M1) + k_yz·M_z,Ed/(M_z,Rk/γ_M1)", $"{F(uNy)} + {F(k2.Kyy)} × {F(uMy)} + {F(k2.Kyz)} × {F(uMz)}", new ScalarValue(uNy + k2.Kyy * uMy + k2.Kyz * uMz), "Interaction, equation 6.61", "6.3.3(4)");
        c.Record("u_62", "N_Ed/(χ_z N_Rk/γ_M1) + k_zy·M_y,Ed/(χ_LT M_y,Rk/γ_M1) + k_zz·M_z,Ed/(M_z,Rk/γ_M1)", $"{F(uNz)} + {F(k2.Kzy)} × {F(uMy)} + {F(k2.Kzz)} × {F(uMz)}", new ScalarValue(uNz + k2.Kzy * uMy + k2.Kzz * uMz), "Interaction, equation 6.62", "6.3.3(4)");
        c.Assign("u_interaction", "max(u_61, u_62)", null, "Greatest interaction ratio", "6.3.3(4)");
        var uBuckling = compression ? nEd / (Math.Min(chiY, chiZ) * nRk / gM1) : 0.0;
        c.Record("u_buckling", compression ? "N_Ed/N_b,Rd" : "0 (no compression)", compression ? $"{F(nEd / 1e3)} kN / {F(c.Si("N_b_Rd") / 1e3)} kN" : "N_Ed is not a compression", new ScalarValue(uBuckling), "Flexural buckling utilisation", "6.3.1.1");
        c.Assign("u_member", "max(max(u_interaction, u_LTB), u_buckling)", null, "Governing utilisation of the member");

        var parts = new[] { ("lateral-torsional buckling", c.Si("u_LTB")), ("flexural buckling", c.Si("u_buckling")), ("interaction 6.61", c.Si("u_61")), ("interaction 6.62", c.Si("u_62")) };
        var worst = parts.MaxBy(p => p.Item2);

        foreach (var (symbol, text) in new[]
        {
            ("Mcr", "M_cr"), ("lambda_LT", "λ̄_LT"), ("chi_LT", "χ_LT"), ("Mb_Rd", "M_b,Rd"), ("N_Rk", "Characteristic axial resistance"), ("N_cr_y", "N_cr,y"), ("N_cr_z", "N_cr,z"), ("lambda_y", "λ̄_y"), ("lambda_z", "λ̄_z"), ("chi_y", "χ_y"), ("chi_z", "χ_z"),
            ("N_b_y_Rd", "N_b,y,Rd"), ("N_b_z_Rd", "N_b,z,Rd"), ("N_b_Rd", "N_b,Rd"), ("k_yy", "Interaction factor for major-axis bending in equation 6.61"), ("k_yz", "Interaction factor for minor-axis bending in equation 6.61"), ("k_zy", "Interaction factor for major-axis bending in equation 6.62"), ("k_zz", "Interaction factor for minor-axis bending in equation 6.62"), ("u_LTB", "LTB utilisation"), ("u_buckling", "Buckling utilisation"),
            ("u_61", "Equation 6.61"), ("u_62", "Equation 6.62"), ("u_interaction", "Interaction utilisation"), ("u_member", "Member utilisation"),
        })
        {
            c.Output(symbol, text);
        }

        foreach (var (symbol, text) in new[]
        {
            ("alpha_LT", "α_LT"), ("alpha_y", "α_y"), ("alpha_z", "α_z"), ("Phi_LT", "Φ_LT"), ("chi_LT_0", "χ_LT before the f modification"), ("k_c", "k_c"), ("f", "f"), ("W_y", "Section modulus for bending about y-y"), ("M_y_Rk", "M_y,Rk"), ("M_z_Rk", "M_z,Rk"),
            ("C_my", "Equivalent uniform moment factor about y-y"), ("C_mz", "Equivalent uniform moment factor about z-z"), ("C_mLT", "Equivalent uniform moment factor for lateral-torsional buckling"), ("N_cr_T", "N_cr,T"), ("lambda_0", "λ̄_0"), ("a_LT", "a_LT"), ("u_N_y", "Axial term, y"), ("u_N_z", "Axial term, z"), ("u_M_y", "Bending term, y"), ("u_M_z", "Bending term, z"),
        })
        {
            if (c.Session.Find(symbol) is not null)
                c.Output(symbol, text);
        }

        c.Output("governing", "Governing check", new TextValue($"{worst.Item1} ({CalcValue.FormatNumber(worst.Item2, 3)})"));

        c.Record("u_limit", "limit", "the utilisation limit", new ScalarValue(1.0), "Utilisation limit");
        c.Check("Bending moment against lateral-torsional buckling resistance", "M_y_Ed", "Mb_Rd", "cl. 6.3.2.1", resultSymbol: "u_LTB");

        if (compression)
        {
            c.Check("Axial force against flexural buckling resistance", "N_abs", "N_b_Rd", "cl. 6.3.1.1", resultSymbol: "u_buckling");
            c.Check("Interaction equation 6.61 against its limit", "u_61", "u_limit", "cl. 6.3.3(4)");
            c.Check("Interaction equation 6.62 against its limit", "u_62", "u_limit", "cl. 6.3.3(4)");
        }
        else
        {
            c.Check("Bending interaction without compression, equation 6.61, against its limit", "u_61", "u_limit", "cl. 6.3.3(4)");
            c.Check("Bending interaction without compression, equation 6.62, against its limit", "u_62", "u_limit", "cl. 6.3.3(4)");
        }
    }

    private static double Alpha(CalcRunContext c, string curve) => curve switch
    {
        "a0" => c.InputSi("alpha_a0"),
        "a" => c.InputSi("alpha_a"),
        "b" => c.InputSi("alpha_b"),
        "c" => c.InputSi("alpha_c"),
        "d" => c.InputSi("alpha_d"),
        _ => throw new ArgumentOutOfRangeException(nameof(curve), curve, "Buckling curve must be a0, a, b, c or d."),
    };

    /// <summary>The C₁ and C_my the run uses, as the double-curvature warning prints them: an entered value overrides the one derived from ψ (B-B-07), and Annex A takes its own C_my from ψ.</summary>
    private static string UsedFactors(CalcRunContext c, double psiY, bool annexA)
    {
        var c1 = c.Has("C_1") ? $"C₁ = {F(c.InputSi("C_1"))} (entered, not the ψ value {F(LtbInteractionMath.C1FromPsi(psiY))})" : $"C₁ = {F(LtbInteractionMath.C1FromPsi(psiY))}";

        if (annexA)
            return $"{c1}, and Annex A takes its C_my from ψ";

        return c.Has("C_m_y") ? $"{c1}, C_my = {F(c.InputSi("C_m_y"))} (entered, not the ψ value {F(LtbInteractionMath.CmFromPsi(psiY))})" : $"{c1}, C_my = {F(LtbInteractionMath.CmFromPsi(psiY))}";
    }

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
