using Tempest.Calc.Sheets;

namespace Tempest.Calc.Structural;

/// <summary>
/// S3 — basic EN 1993-1-1 member checks: tension, compression with
/// flexural buckling about both axes, bending about both axes, shear
/// (with the high-shear reduction), and simplified cross-section and
/// member interaction for bending with axial force. Clause numbers only;
/// the buckling curve's imperfection factor and the partial factors are
/// editable inputs with their textbook provenance. Lateral-torsional
/// buckling is not checked (flagged).
/// </summary>
public sealed class MemberChecksCalculator : ICalcCalculator
{
    public const string Id = "struct.en1993-member";

    private static readonly string[] Curves = ["a0", "a", "b", "c", "d"];

    /// <summary>Imperfection factors by buckling curve (Gardner and Nethercot, Designers' Guide to EN 1993-1-1, §6.3.1), overridable per sheet.</summary>
    public static double ImperfectionFactor(string curve) => curve switch
    {
        "a0" => 0.13,
        "a" => 0.21,
        "b" => 0.34,
        "c" => 0.49,
        "d" => 0.76,
        _ => throw new ArgumentOutOfRangeException(nameof(curve), curve, "Buckling curve must be a0, a, b, c or d."),
    };

    /// <summary>The compression flange is held against lateral-torsional buckling (the assumption of this sheet).</summary>
    public const string Restrained = "Restrained";

    /// <summary>The compression flange is free to buckle laterally: lateral-torsional buckling governs and is checked by the LTB interaction sheet.</summary>
    public const string NotRestrained = "Not restrained";

    public CalcTemplate Template { get; } = new(
        Id, "1.6.0", CalcDiscipline.Structural, "Member checks (EN 1993-1-1)",
        "Calculates the resistance of a class 1, 2 or 3 steel member to tension, compression, buckling, bending, shear and their interaction, to EN 1993-1-1. Tension is positive; class 4 sections and lateral-torsional buckling are outside this sheet.",
        "Axial plastic resistance: N_pl,Rd = A f_y/γ_M0.\nBending resistance, plastic modulus for class 1–2 and elastic for class 3: M_c,Rd = W f_y/γ_M0.\nShear resistance: V_pl,Rd = A_v f_y/(√3 γ_M0).\nBending resistance reduced by high shear, when V_Ed > 0.5 V_pl,Rd: ρ = (2V_Ed/V_pl,Rd − 1)². Class 1–2 I-section with h, t_w, t_f entered: M_y,V,Rd = (W_pl,y − ρ A_w²/(4 t_w)) f_y/γ_M0, A_w = (h − 2t_f) t_w, and M_z,c,Rd is not reduced; otherwise the whole resistance × (1 − ρ).\nElastic critical load and non-dimensional slenderness: N_cr = π²EI/L_cr²; λ̄ = √(A f_y/N_cr).\nBuckling reduction factor and buckling resistance: Φ = 0.5[1 + α(λ̄ − 0.2) + λ̄²]; χ = 1/(Φ + √(Φ² − λ̄²)) ≤ 1; N_b,Rd = χ A f_y/γ_M1.\nCross-section check, linear interaction: N_Ed/N_Rd + M_y,Ed/M_y,Rd + M_z,Ed/M_z,Rd ≤ 1.\nMember check, axial force and bending with buckling (6.61, 6.62): N_Ed/N_b,Rd,min + k_yy M_y,Ed/(W_y f_y/γ_M1) + k_zz M_z,Ed/(W_z f_y/γ_M1) ≤ 1, the characteristic bending resistance M_Rk over γ_M1 without the shear reduction.",
        [
            new CalcInputSpec("section_class", "Cross-section class", CalcInputKind.Choice, Choices: ["1", "2", "3", "4"], DefaultChoice: "1", Group: "Setup"),
            new CalcInputSpec("curve_y", "Buckling curve about y-y", CalcInputKind.Choice, Choices: Curves, DefaultChoice: "b", Group: "Setup"),
            new CalcInputSpec("curve_z", "Buckling curve about z-z", CalcInputKind.Choice, Choices: Curves, DefaultChoice: "c", Group: "Setup"),
            new CalcInputSpec("ltb", "Compression flange against lateral-torsional buckling (optional; blank = blocks when M_y_Ed is above 0)", CalcInputKind.Choice, Choices: [Restrained, NotRestrained], IsOptional: true, Group: "Setup"),
            new CalcInputSpec("A", "Area", CalcInputKind.Quantity, "cm^2", Default: 58.7, Min: 1, Max: 1e5, HardMin: 1e-3, HardMax: 1e7, Group: "Geometry"),
            new CalcInputSpec("W_pl_y", "Plastic section modulus about y-y", CalcInputKind.Quantity, "cm^3", Default: 497, Min: 0.1, Max: 1e7, HardMin: 1e-6, HardMax: 1e9, Group: "Geometry"),
            new CalcInputSpec("W_el_y", "Elastic section modulus about y-y", CalcInputKind.Quantity, "cm^3", Default: 450, Min: 0.1, Max: 1e7, HardMin: 1e-6, HardMax: 1e9, Group: "Geometry"),
            new CalcInputSpec("W_pl_z", "Plastic section modulus about z-z", CalcInputKind.Quantity, "cm^3", Default: 231, Min: 0.1, Max: 1e7, HardMin: 1e-6, HardMax: 1e9, Group: "Geometry"),
            new CalcInputSpec("W_el_z", "Elastic section modulus about z-z", CalcInputKind.Quantity, "cm^3", Default: 152, Min: 0.1, Max: 1e7, HardMin: 1e-6, HardMax: 1e9, Group: "Geometry"),
            new CalcInputSpec("I_y", "Second moment of area about y-y", CalcInputKind.Quantity, "cm^4", Default: 4568, Min: 0.1, Max: 1e9, HardMin: 1e-6, HardMax: 1e12, Group: "Geometry"),
            new CalcInputSpec("I_z", "Second moment of area about z-z", CalcInputKind.Quantity, "cm^4", Default: 1548, Min: 0.1, Max: 1e9, HardMin: 1e-6, HardMax: 1e12, Group: "Geometry"),
            new CalcInputSpec("A_v", "Shear area (optional; blank = rolled I-section from h, b, t_w, t_f and r)", CalcInputKind.Quantity, "cm^2", Min: 0.1, Max: 1e5, HardMin: 1e-3, HardMax: 1e7, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("h", "Depth for the shear area (optional; blank = use A_v)", CalcInputKind.Quantity, "mm", Min: 1, Max: 5000, HardMin: 1e-3, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("b", "Width for the shear area (optional; blank = use A_v)", CalcInputKind.Quantity, "mm", Min: 1, Max: 5000, HardMin: 1e-3, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("t_w", "Web thickness for the shear area (optional; blank = use A_v)", CalcInputKind.Quantity, "mm", Min: 0.1, Max: 500, HardMin: 1e-3, HardMax: 1e4, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("t_f", "Flange thickness for the shear area (optional; blank = use A_v)", CalcInputKind.Quantity, "mm", Min: 0.1, Max: 500, HardMin: 1e-3, HardMax: 1e4, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("r", "Root radius for the shear area (optional; blank = 0)", CalcInputKind.Quantity, "mm", Min: 0, Max: 500, HardMin: 0, HardMax: 1e4, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("A_net", "Net area of a tension member (optional; blank = the sheet is blocked when N_Ed is tension)", CalcInputKind.Quantity, "cm^2", Min: 0.1, Max: 1e5, HardMin: 1e-3, HardMax: 1e7, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("f_y", "Yield strength for the thickness", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials", ReferenceProperty: "YieldStrength", ReferenceDimension: "Pressure", Default: 355, Min: 200, Max: 700, HardMin: 1, HardMax: 2000, Group: "Material"),
            new CalcInputSpec("E", "Young's modulus", CalcInputKind.Quantity, "GPa", ReferenceLibrary: "Materials", ReferenceProperty: "YoungsModulus", ReferenceDimension: "Pressure", Default: 210, Min: 190, Max: 220, HardMin: 1, HardMax: 1000, Group: "Material"),
            new CalcInputSpec("f_u", "Ultimate tensile strength (optional; blank = the sheet is blocked when N_Ed is tension)", CalcInputKind.Quantity, "MPa", Min: 300, Max: 900, HardMin: 1, HardMax: 3000, IsOptional: true, Group: "Material"),
            new CalcInputSpec("N_Ed", "Design axial force, tension positive and compression negative, opposite in sign to the base plate sheet's axial force", CalcInputKind.Quantity, "kN", Default: -800, Min: -1e5, Max: 1e5, HardMin: -1e8, HardMax: 1e8, Group: "Loads"),
            new CalcInputSpec("M_y_Ed", "Design moment about y-y (major axis)", CalcInputKind.Quantity, "kN*m", Default: 60, Min: 0, Max: 1e5, HardMin: 0, HardMax: 1e8, Group: "Loads"),
            new CalcInputSpec("M_z_Ed", "Design moment about z-z (minor axis)", CalcInputKind.Quantity, "kN*m", Default: 0, Min: 0, Max: 1e5, HardMin: 0, HardMax: 1e8, Group: "Loads"),
            new CalcInputSpec("V_Ed", "Design shear force", CalcInputKind.Quantity, "kN", Default: 40, Min: 0, Max: 1e5, HardMin: 0, HardMax: 1e8, Group: "Loads"),
            new CalcInputSpec("L_cr_y", "Buckling length about y-y", CalcInputKind.Quantity, "m", Default: 4, Min: 0.1, Max: 100, HardMin: 1e-3, HardMax: 1e4, Group: "Conditions"),
            new CalcInputSpec("L_cr_z", "Buckling length about z-z", CalcInputKind.Quantity, "m", Default: 4, Min: 0.1, Max: 100, HardMin: 1e-3, HardMax: 1e4, Group: "Conditions"),
            new CalcInputSpec("gamma_M0", "Partial factor for cross-section resistance", CalcInputKind.Number, Default: 1.0, Min: 1.0, Max: 1.25, HardMin: 0.5, HardMax: 5, Group: "Factors"),
            new CalcInputSpec("gamma_M1", "Partial factor for member buckling", CalcInputKind.Number, Default: 1.0, Min: 1.0, Max: 1.25, HardMin: 0.5, HardMax: 5, Group: "Factors"),
            new CalcInputSpec("gamma_M2", "Partial factor for net-section fracture", CalcInputKind.Number, Default: 1.1, Min: 1.0, Max: 1.35, HardMin: 0.5, HardMax: 5, Group: "Factors"),
            new CalcInputSpec("eta", "Shear-area factor", CalcInputKind.Number, Default: 1.0, Min: 1.0, Max: 1.2, HardMin: 0.5, HardMax: 2, Group: "Factors"),
            new CalcInputSpec("k_yy", "Interaction factor for bending about y-y in the member check", CalcInputKind.Number, Default: 1.0, Min: 0.5, Max: 2.0, HardMin: 0.1, HardMax: 10, Group: "Factors"),
            new CalcInputSpec("k_zz", "Interaction factor for bending about z-z in the member check", CalcInputKind.Number, Default: 1.0, Min: 0.5, Max: 2.0, HardMin: 0.1, HardMax: 10, Group: "Factors"),
            new CalcInputSpec("alpha_y", "Imperfection factor about y-y (optional; blank = from the buckling curve)", CalcInputKind.Number, Min: 0.1, Max: 0.8, HardMin: 0, HardMax: 2, IsOptional: true, Group: "Factors"),
            new CalcInputSpec("alpha_z", "Imperfection factor about z-z (optional; blank = from the buckling curve)", CalcInputKind.Number, Min: 0.1, Max: 0.8, HardMin: 0, HardMax: 2, IsOptional: true, Group: "Factors"),
        ],
        [
            new CalcAssumption("The cross-section class is the class the user states, 1, 2 or 3.", "Class 4 effective properties are not derived here."),
            new CalcAssumption("The compression flange is restrained against lateral-torsional buckling, as the input ltb says.", "Lateral-torsional buckling of a member that is not restrained is checked on the LTB interaction sheet."),
            new CalcAssumption("A k left at 1.0 is replaced by the Annex A or B upper bound C_m(1 + 0.8n).", "Applies under compression with a moment; the smaller flexural-buckling reduction is used on the axial term and the factors are not derived."),
            new CalcAssumption("Imperfection factors are the textbook values for the buckling curve unless the user overrides them.", "Gardner and Nethercot, Designers' Guide to EN 1993-1-1."),
            new CalcAssumption("Partial factors are 1.0 as in the UK National Annex unless the user overrides them.", "UK National Annex to BS EN 1993-1-1."),
        ],
        [
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "cl. 6.2.3", "Tension"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "cl. 6.2.4", "Compression"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "cl. 6.2.5", "Bending moment"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "cl. 6.2.6", "Shear"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "cl. 6.2.8", "Bending and shear"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "cl. 6.2.1(7)", "Linear summation of utilisations"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "cl. 6.3.1.1, 6.3.1.2", "Uniform members in compression: buckling resistance, buckling curves"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "cl. 6.3.3", "Uniform members in bending and axial compression"),
            new CalcClauseRef("Gardner and Nethercot, Designers' Guide to EN 1993-1-1", "§6.3.1, 1st ed.", "Imperfection factors for the buckling curves"),
        ],
        [
            new CalcApplicabilityRule("class-4", "Class 4 cross-sections need effective properties: outside this sheet.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("shear-area", "No shear area and no rolled-I helper dimensions: the shear check cannot run.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("helper-area", "The rolled-I helper dimensions give a web-and-flange area that differs from A by more than 10 %: the helper dimensions probably belong to another section.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("k-default", "k_yy or k_zz is left at 1.0 under compression with a moment: Annex A or B give k above 1, so the check uses the upper bound C_m(1 + 0.8n); a k entered as 1.0 with a recorded basis is used as entered.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("high-shear", "V_Ed exceeds half the plastic shear resistance: the bending resistance is reduced (6.2.8).", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("net-section", "A member in tension needs its net area A_net (A where there are no holes) and f_u: the net-section fracture resistance N_u,Rd (6.2.3(2)(b)) is checked, and the sheet is blocked when they are blank.", CalcNoticeSeverity.Block, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("ltb", "A member bending about y must say whether its compression flange is restrained against lateral-torsional buckling: the sheet has no default for it, and blocks until it is entered.", CalcNoticeSeverity.Block, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("no-ltb", "A member bending about y whose compression flange is not restrained is outside this sheet: lateral-torsional buckling (6.3.2) is checked on the LTB interaction sheet.", CalcNoticeSeverity.Block, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("slender", "Non-dimensional slenderness above 2.0: check the member is intended to be this slender.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-uc-compression", "UC 203×203×46 S355, L_cr = 4 m, axial compression (flexural buckling about z-z)",
                new CalcReferenceSource("Analytic (EN 1993-1-1 6.3.1 formulae) in the manner of SCI P362 worked examples; properties from the Blue Book", null, "N_cr,z = π²·210000·1548e4/4000² = 2005.3 kN; λ̄_z = √(5870·355/2005.3e3) = 1.0194; curve c α = 0.49: Φ = 1.2203, χ = 0.5288; N_b,z,Rd = 1101.9 kN; N_pl,Rd = 2083.9 kN"),
                [CalcInputValue.Quantity("N_Ed", -900, "kN"), CalcInputValue.Quantity("M_y_Ed", 0, "kN*m"), CalcInputValue.Quantity("M_z_Ed", 0, "kN*m"), CalcInputValue.Quantity("V_Ed", 0, "kN"), CalcInputValue.Quantity("f_y", 355, "MPa"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Choice("section_class", "1"), CalcInputValue.Quantity("A", 58.7, "cm^2"), CalcInputValue.Quantity("W_pl_y", 497, "cm^3"), CalcInputValue.Quantity("W_el_y", 450, "cm^3"), CalcInputValue.Quantity("W_pl_z", 231, "cm^3"), CalcInputValue.Quantity("W_el_z", 152, "cm^3"), CalcInputValue.Quantity("I_y", 4568, "cm^4"), CalcInputValue.Quantity("I_z", 1548, "cm^4"), CalcInputValue.Quantity("h", 203.2, "mm"), CalcInputValue.Quantity("b", 203.6, "mm"), CalcInputValue.Quantity("t_w", 7.2, "mm"), CalcInputValue.Quantity("t_f", 11, "mm"), CalcInputValue.Quantity("r", 10.2, "mm"), CalcInputValue.Quantity("L_cr_y", 4, "m"), CalcInputValue.Quantity("L_cr_z", 4, "m"), CalcInputValue.Choice("curve_y", "b"), CalcInputValue.Choice("curve_z", "c")],
                [new CalcExpectedValue("N_pl_Rd", 2083.85, "kN", 1e-4), new CalcExpectedValue("N_cr_z", 2005.3, "kN", 1e-3), new CalcExpectedValue("lambda_z", 1.0194, "", 1e-3), new CalcExpectedValue("chi_z", 0.5288, "", 1e-3), new CalcExpectedValue("N_b_z_Rd", 1101.9, "kN", 1e-3), new CalcExpectedValue("u_buckling", 0.8168, "", 1e-3)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-ub-bending-shear", "UB 305×165×40 S275, bending and shear (rolled-I shear area helper)",
                new CalcReferenceSource("Analytic (EN 1993-1-1 6.2.5, 6.2.6) with Blue Book properties", null, "M_c,Rd = 623e3·275 = 171.3 kN·m; A_v = A − 2bt_f + (t_w + 2r)t_f = 5130 − 3366 + 242.8 = 2006.8 mm²; V_pl,Rd = 2006.8·275/√3 = 318.6 kN"),
                [CalcInputValue.Quantity("N_Ed", 0, "kN"), CalcInputValue.Quantity("M_y_Ed", 120, "kN*m"), CalcInputValue.Quantity("M_z_Ed", 0, "kN*m"), CalcInputValue.Quantity("V_Ed", 100, "kN"), CalcInputValue.Quantity("f_y", 275, "MPa"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Choice("section_class", "1"), CalcInputValue.Choice("ltb", Restrained), CalcInputValue.Quantity("A", 51.3, "cm^2"), CalcInputValue.Quantity("W_pl_y", 623, "cm^3"), CalcInputValue.Quantity("W_el_y", 560, "cm^3"), CalcInputValue.Quantity("W_pl_z", 142, "cm^3"), CalcInputValue.Quantity("W_el_z", 92.6, "cm^3"), CalcInputValue.Quantity("I_y", 8503, "cm^4"), CalcInputValue.Quantity("I_z", 764, "cm^4"), CalcInputValue.Quantity("h", 303.4, "mm"), CalcInputValue.Quantity("b", 165, "mm"), CalcInputValue.Quantity("t_w", 6, "mm"), CalcInputValue.Quantity("t_f", 10.2, "mm"), CalcInputValue.Quantity("r", 8.9, "mm"), CalcInputValue.Quantity("L_cr_y", 6, "m"), CalcInputValue.Quantity("L_cr_z", 1.5, "m"), CalcInputValue.Choice("curve_y", "a"), CalcInputValue.Choice("curve_z", "b")],
                [new CalcExpectedValue("M_y_c_Rd", 171.325, "kN*m", 1e-4), new CalcExpectedValue("A_v", 20.068, "cm^2", 1e-4), new CalcExpectedValue("V_pl_Rd", 318.62, "kN", 1e-3), new CalcExpectedValue("u_M_y", 0.70043, "", 1e-3), new CalcExpectedValue("u_V", 0.31385, "", 1e-3)]),
            new CalcTestCase("ka-tension-class3", "Class 3 tie, tension with minor-axis moment (elastic modulus governs)",
                new CalcReferenceSource("Analytic (6.2.3, 6.2.5 class 3, 6.2.1(7))", null, "N_pl,Rd = 2000·355 = 710 kN; M_z,c,Rd = W_el,z f_y = 30e3·355 = 10.65 kN·m; linear interaction 400/710 + 5/10.65 = 0.5634 + 0.4695 = 1.0329 → FAIL"),
                [CalcInputValue.Quantity("N_Ed", 400, "kN"), CalcInputValue.Quantity("M_y_Ed", 0, "kN*m"), CalcInputValue.Quantity("M_z_Ed", 5, "kN*m"), CalcInputValue.Quantity("V_Ed", 0, "kN"), CalcInputValue.Quantity("f_y", 355, "MPa"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Choice("section_class", "3"), CalcInputValue.Quantity("A", 20, "cm^2"), CalcInputValue.Quantity("W_pl_y", 100, "cm^3"), CalcInputValue.Quantity("W_el_y", 80, "cm^3"), CalcInputValue.Quantity("W_pl_z", 40, "cm^3"), CalcInputValue.Quantity("W_el_z", 30, "cm^3"), CalcInputValue.Quantity("I_y", 1000, "cm^4"), CalcInputValue.Quantity("I_z", 200, "cm^4"), CalcInputValue.Quantity("A_v", 8, "cm^2"), CalcInputValue.Quantity("L_cr_y", 3, "m"), CalcInputValue.Quantity("L_cr_z", 3, "m"), CalcInputValue.Choice("curve_y", "b"), CalcInputValue.Choice("curve_z", "c"), CalcInputValue.Quantity("A_net", 20, "cm^2"), CalcInputValue.Quantity("f_u", 490, "MPa")],
                [new CalcExpectedValue("N_pl_Rd", 710, "kN", 1e-9), new CalcExpectedValue("M_z_c_Rd", 10.65, "kN*m", 1e-9), new CalcExpectedValue("u_section", 1.03294, "", 1e-4), new CalcExpectedValue("N_u_Rd", 801.8182, "kN", 1e-6), new CalcExpectedValue("u_net", 0.498866, "", 1e-5)]),
        ],
        DiagramKey: "member-checks",
        Category: "Members",
        OutputPorts:
        [
            new CalcOutputPort("N_pl_Rd", "kN", "Plastic axial resistance", "member.resistance_axial_plastic"),
            new CalcOutputPort("N_b_Rd", "kN", "Governing buckling resistance", "member.resistance_buckling"),
            new CalcOutputPort("M_y_c_Rd", "kN*m", "Bending resistance about y-y", "member.resistance_bending_y"),
            new CalcOutputPort("M_z_c_Rd", "kN*m", "Bending resistance about z-z", "member.resistance_bending_z"),
            new CalcOutputPort("V_pl_Rd", "kN", "Plastic shear resistance", "member.resistance_shear"),
            new CalcOutputPort("rho_shear", "", "Shear reduction factor", "member.reduction_shear", ["rho"]),
            new CalcOutputPort("u_section", "", "Cross-section interaction utilisation", "member.utilisation_section"),
            new CalcOutputPort("u_buckling", "", "Buckling utilisation", "member.utilisation_buckling"),
            new CalcOutputPort("u_member", "", "Member interaction utilisation", "member.utilisation_member"),
        ]);

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_M_y", "u_M_z", "u_N", "u_V", "u_buckling", "u_member", "u_section", "u_net");
        var cls = c.InputText("section_class");

        if (cls == "4")
        {
            c.Block("class-4", "section_class: enter class 1, 2 or 3. Class 4 sections are outside this sheet.");
            return;
        }

        foreach (var s in new[] { "N_Ed", "M_y_Ed", "M_z_Ed", "V_Ed", "f_y", "E", "gamma_M0", "gamma_M1", "A", "W_pl_y", "W_el_y", "W_pl_z", "W_el_z", "I_y", "I_z", "eta", "L_cr_y", "L_cr_z", "k_yy", "k_zz" })
            c.Input(s);

        var plastic = cls is "1" or "2";

        // Lateral-torsional buckling is not checked here. A member bending about y must say whether its compression flange is restrained (no default: a sheet that assumed it
        // passed an unrestrained beam), and one that is not restrained is refused, not passed.
        if (c.Si("M_y_Ed") > 0 && !c.Has("ltb"))
        {
            c.Block("ltb", "ltb: enter whether the compression flange is restrained against lateral-torsional buckling.");
            return;
        }

        if (c.Si("M_y_Ed") > 0 && c.InputText("ltb") == NotRestrained)
        {
            c.Block("no-ltb", "ltb: a member bending about y with an unrestrained compression flange is checked on the LTB interaction sheet, not here.");
            return;
        }

        if (c.Si("M_y_Ed") > 0)
            c.Assume("The compression flange is restrained against lateral-torsional buckling, as the input ltb says.", "Input ltb.");

        // A tension member is checked for gross-section yielding and for fracture at the net section (a tie with bolt holes is governed by the second): without A_net and f_u the sheet is blocked.
        if (c.Si("N_Ed") > 0)
        {
            if (!c.Has("A_net") || !c.Has("f_u"))
            {
                c.Block("net-section", "A_net: enter the net area, and f_u, for a tension member. Use A where there are no holes.");
                return;
            }

            c.Input("A_net"); c.Input("f_u"); c.Input("gamma_M2");

            if (c.Si("A_net") > c.Si("A") * (1 + 1e-9))
            {
                c.Block("net-section", "A_net: the net area cannot exceed the gross area A.");
                return;
            }

            c.Assign("N_u_Rd", "0.9 * A_net * f_u / gamma_M2", "kN", "Net-section fracture resistance", "6.2.3(2)(b)");
            c.Assign("u_net", "N_Ed / N_u_Rd", null, "Net-section fracture utilisation");
        }

        // Shear area.
        if (c.Has("A_v"))
        {
            c.Input("A_v");
        }
        else if (c.Has("h") && c.Has("b") && c.Has("t_w") && c.Has("t_f"))
        {
            c.Input("h"); c.Input("b"); c.Input("t_w"); c.Input("t_f");
            if (c.Has("r")) c.Input("r");
            var helperArea = 2 * c.InputSi("b") * c.InputSi("t_f") + (c.InputSi("h") - 2 * c.InputSi("t_f")) * c.InputSi("t_w");
            var givenArea = c.InputSi("A");

            if (Math.Abs(helperArea - givenArea) > 0.10 * givenArea)
                c.Warn("helper-area", $"h: the web and flange area is {helperArea * 1e4:0.0} cm², more than 10 % from A = {givenArea * 1e4:0.0} cm². Enter the dimensions of this section, or A_v.");

            c.Assign("r_h", c.Has("r") ? "r" : "0 mm", "mm", "Root radius used by the helper");
            c.Assign("A_v_rolled", "A - 2 * b * t_f + (t_w + 2 * r_h) * t_f", "cm^2", "Rolled I shear area, load parallel to the web", "6.2.6(3)");
            c.Assign("A_v_min", "eta * (h - 2 * t_f) * t_w", "cm^2", "Lower bound η h_w t_w", "6.2.6(3)");
            c.Assign("A_v", "max(A_v_rolled, A_v_min)", "cm^2", "Shear area");
        }
        else
        {
            c.Block("shear-area", "A_v: enter the shear area, or h, b, t_w and t_f of the section for the rolled-I helper.");
            return;
        }

        // Cross-section resistances.
        c.Assign("N_pl_Rd", "A * f_y / gamma_M0", "kN", "Plastic axial resistance", "6.2.3(2), 6.2.4(2)");
        c.Assign("W_y", plastic ? "W_pl_y" : "W_el_y", "cm^3", plastic ? "Section modulus about y (plastic, class 1–2)" : "Section modulus about y (elastic, class 3)", "6.2.5(2)");
        c.Assign("W_z", plastic ? "W_pl_z" : "W_el_z", "cm^3", plastic ? "Section modulus about z (plastic)" : "Section modulus about z (elastic)", "6.2.5(2)");
        c.Assign("V_pl_Rd", "A_v * f_y / (sqrt(3) * gamma_M0)", "kN", "Plastic shear resistance", "6.2.6(2)");
        c.Assign("u_V", "V_Ed / V_pl_Rd", null, "Shear utilisation");

        var highShear = c.Si("V_Ed") > 0.5 * c.Si("V_pl_Rd");
        var iSection = c.Has("h") && c.Has("t_w") && c.Has("t_f");

        if (highShear)
        {
            c.Warn("high-shear", iSection && plastic
                ? "V_Ed: above half the plastic shear resistance, so the major-axis bending resistance is reduced for the web part only. The minor-axis resistance is not reduced."
                : "V_Ed: above half the plastic shear resistance, so the whole bending resistance is reduced by (1 − ρ). This is conservative, because the web-only reduction needs h, t_w, t_f and class 1 or 2.");
            c.Assign("rho_shear", "(2 * V_Ed / V_pl_Rd - 1)^2", null, "Shear reduction factor", "6.2.8(3)");
        }
        else
        {
            c.Assign("rho_shear", "0", null, "Shear reduction factor (V_Ed ≤ 0.5 V_pl,Rd)", "6.2.8(2)");
        }


        if (highShear && iSection && plastic)
        {
            // Class 1-2 I section: only the web part of the plastic modulus is reduced, W_pl,y - rho_shear A_w^2/(4 t_w) (Eq. 6.30); the flanges carry the minor-axis moment, so M_z is not reduced.
            c.Input("h"); c.Input("t_w"); c.Input("t_f");
            c.Assign("A_w", "(h - 2 * t_f) * t_w", "mm^2", "Web area A_w = h_w t_w", "6.2.8(5)");
            c.Assign("M_y_c_Rd", "(W_y - rho_shear * A_w^2 / (4 * t_w)) * f_y / gamma_M0", "kN*m", "Bending resistance about y, reduced by high shear (web part only)", "6.2.8(5), Eq. 6.30");
            c.Assign("M_z_c_Rd", "W_z * f_y / gamma_M0", "kN*m", "Bending resistance about z (no shear reduction: the web carries almost no minor-axis moment)", "6.2.5(2)");
        }
        else
        {
            c.Assign("M_y_c_Rd", "(1 - rho_shear) * W_y * f_y / gamma_M0", "kN*m", "Bending resistance about y", "6.2.5(2), 6.2.8");
            c.Assign("M_z_c_Rd", "(1 - rho_shear) * W_z * f_y / gamma_M0", "kN*m", "Bending resistance about z", "6.2.5(2)");
        }

        c.Assign("u_M_y", "M_y_Ed / M_y_c_Rd", null, "Bending utilisation, y");
        c.Assign("u_M_z", "M_z_Ed / M_z_c_Rd", null, "Bending utilisation, z");
        c.Assign("N_abs", "abs(N_Ed)", "kN", "Axial force magnitude");
        c.Assign("u_N", "N_abs / N_pl_Rd", null, c.Si("N_Ed") >= 0 ? "Tension utilisation" : "Compression (cross-section) utilisation");
        c.Assign("u_section", "u_N + u_M_y + u_M_z", null, "Linear cross-section interaction", "6.2.1(7)");

        // Flexural buckling (compression only).
        var compression = c.Si("N_Ed") < 0;
        var alphaY = c.Has("alpha_y") ? c.InputSi("alpha_y") : ImperfectionFactor(c.InputText("curve_y"));
        var alphaZ = c.Has("alpha_z") ? c.InputSi("alpha_z") : ImperfectionFactor(c.InputText("curve_z"));
        c.Record("alpha_y", $"imperfection factor, curve {c.InputText("curve_y")}", c.Has("alpha_y") ? "user override" : "Designers' Guide §6.3.1", new Engine.Values.ScalarValue(alphaY), "Imperfection factor about y", "6.3.1.2");
        c.Record("alpha_z", $"imperfection factor, curve {c.InputText("curve_z")}", c.Has("alpha_z") ? "user override" : "Designers' Guide §6.3.1", new Engine.Values.ScalarValue(alphaZ), "Imperfection factor about z", "6.3.1.2");

        foreach (var axis in new[] { "y", "z" })
        {
            c.Assign($"N_cr_{axis}", $"pi^2 * E * I_{axis} / L_cr_{axis}^2", "kN", $"Elastic critical force about {axis}", "6.3.1.2(1)");
            c.Assign($"lambda_{axis}", $"sqrt(A * f_y / N_cr_{axis})", null, $"Non-dimensional slenderness about {axis}", "6.3.1.2(1)");
            c.Assign($"Phi_{axis}", $"0.5 * (1 + alpha_{axis} * (lambda_{axis} - 0.2) + lambda_{axis}^2)", null, $"Φ about {axis}", "6.3.1.2(1)");
            c.Assign($"chi_{axis}", $"min(1, 1 / (Phi_{axis} + sqrt(Phi_{axis}^2 - lambda_{axis}^2)))", null, $"Reduction factor χ about {axis}", "6.3.1.2(1)");
            c.Assign($"N_b_{axis}_Rd", $"chi_{axis} * A * f_y / gamma_M1", "kN", $"Buckling resistance about {axis}", "6.3.1.1(3)");

            if (c.Si($"lambda_{axis}") > 2.0)
                c.Warn("slender", $"L_cr_{axis}: non-dimensional slenderness {c.Si($"lambda_{axis}"):0.00} exceeds 2.0.");
        }

        c.Assign("N_b_Rd", "min(N_b_y_Rd, N_b_z_Rd)", "kN", "Governing buckling resistance");
        c.Assign("u_buckling", compression ? "N_abs / N_b_Rd" : "0", null, compression ? "Flexural buckling utilisation" : "Flexural buckling utilisation (tension: not applicable)", "6.3.1.1(1)");
        c.Assign("M_y_b_Rd", "W_y * f_y / gamma_M1", "kN*m", "Characteristic bending resistance about y over γ_M1 (member check, no shear reduction)", "6.3.3, Eq. 6.61");
        c.Assign("M_z_b_Rd", "W_z * f_y / gamma_M1", "kN*m", "Characteristic bending resistance about z over γ_M1 (member check, no shear reduction)", "6.3.3, Eq. 6.62");

        // k_yy and k_zz open at 1.0. Under compression with a moment that is not a safe value (Annex A and B give k up to C_m(1 + 0.8 n), which is above 1 for any n),
        // so a k left at 1.0 (not entered, or entered as 1.0 with no recorded basis) is replaced in the check by that bound, whatever the compression; a k the user recorded as
        // an assumption, a specification or any other basis is theirs. (Until 1.5.0 this applied only above n = 0.3: N_Ed/N_b,Rd = 0.297 passed at 0.99 where the bound gives 1.157.)
        var nBuckling = compression ? c.Si("u_buckling") : 0.0;
        var kyOne = compression && c.Si("M_y_Ed") > 0 && Math.Abs(c.InputSi("k_yy") - 1.0) < 1e-12;
        var kzOne = compression && c.Si("M_z_Ed") > 0 && Math.Abs(c.InputSi("k_zz") - 1.0) < 1e-12;
        var kyLeft = kyOne && !c.HasRecordedBasis("k_yy");
        var kzLeft = kzOne && !c.HasRecordedBasis("k_zz");
        var kyTerm = "k_yy";
        var kzTerm = "k_zz";

        if (kyLeft)
        {
            c.Assign("k_yy_eff", "1 + 0.8 * abs(N_Ed) / N_b_y_Rd", null, "Interaction factor k_yy used: the Annex A/B upper bound C_m(1 + 0.8n) with C_m = 1, because k_yy was left at 1.0", "Annex A, B");
            kyTerm = "k_yy_eff";
        }

        if (kzLeft)
        {
            c.Assign("k_zz_eff", "1 + 0.8 * abs(N_Ed) / N_b_z_Rd", null, "Interaction factor k_zz used: the Annex A/B upper bound C_m(1 + 0.8n) with C_m = 1, because k_zz was left at 1.0", "Annex A, B");
            kzTerm = "k_zz_eff";
        }

        c.Assign("u_member", compression ? $"N_abs / N_b_Rd + {kyTerm} * M_y_Ed / M_y_b_Rd + {kzTerm} * M_z_Ed / M_z_b_Rd" : "u_M_y + u_M_z", null, "Simplified member interaction", "6.3.3(4)");

        // One warning for each k left at, or entered as, 1.0 (one symbol each).
        foreach (var (symbol, one, left, n) in new[] { ("k_yy", kyOne, kyLeft, Math.Abs(c.Si("N_Ed")) / c.Si("N_b_y_Rd")), ("k_zz", kzOne, kzLeft, Math.Abs(c.Si("N_Ed")) / c.Si("N_b_z_Rd")) })
        {
            if (!one)
                continue;

            var bound = 1 + 0.8 * n;

            if (left)
            {
                c.Warn("k-default", $"{symbol}: left at 1.0 with N_Ed/N_b,Rd = {nBuckling:0.000}, so the check uses the Annex A or B bound {bound:0.000}. Enter k from Annex A or B to use your own.");
            }
            else
            {
                var uAtBound = nBuckling + (symbol == "k_yy" ? bound * c.Si("M_y_Ed") / c.Si("M_y_b_Rd") + c.InputSi("k_zz") * c.Si("M_z_Ed") / c.Si("M_z_b_Rd") : c.InputSi("k_yy") * c.Si("M_y_Ed") / c.Si("M_y_b_Rd") + bound * c.Si("M_z_Ed") / c.Si("M_z_b_Rd"));
                c.Warn("k-default", $"{symbol}: entered as 1.0, but Annex A or B give k up to {bound:0.000} here. With it the interaction would be {uAtBound:0.000} against {c.Si("u_member"):0.000} shown.");
            }
        }

        foreach (var (symbol, text) in new[] { ("A_v", "Shear area"), ("N_pl_Rd", "Plastic axial resistance"), ("V_pl_Rd", "Plastic shear resistance"), ("rho_shear", "Shear reduction factor ρ"), ("M_y_c_Rd", "Bending resistance about y-y"), ("M_z_c_Rd", "Bending resistance about z-z"), ("M_y_b_Rd", "Characteristic bending resistance about y-y over the partial factor"), ("M_z_b_Rd", "Characteristic bending resistance about z-z over the partial factor"), ("N_cr_y", "Elastic critical force about y-y"), ("N_cr_z", "Elastic critical force about z-z"), ("lambda_y", "Non-dimensional slenderness about y-y"), ("lambda_z", "Non-dimensional slenderness about z-z"), ("chi_y", "Buckling reduction factor about y-y"), ("chi_z", "Buckling reduction factor about z-z"), ("N_b_y_Rd", "Buckling resistance about y-y"), ("N_b_z_Rd", "Buckling resistance about z-z"), ("N_b_Rd", "Governing buckling resistance"), ("u_N", "Axial utilisation"), ("u_M_y", "Bending y-y utilisation"), ("u_M_z", "Bending z-z utilisation"), ("u_V", "Shear utilisation"), ("u_section", "Cross-section interaction"), ("u_buckling", "Buckling utilisation"), ("u_member", "Member interaction") })
            c.Output(symbol, text);

        if (kyLeft)
            c.Output("k_yy_eff", "Interaction factor k_yy used (Annex upper bound)");

        if (kzLeft)
            c.Output("k_zz_eff", "Interaction factor k_zz used (Annex upper bound)");

        c.Record("u_limit", "limit", "the utilisation limit", new Engine.Values.ScalarValue(1.0), "Utilisation limit");
        c.Check(c.Si("N_Ed") >= 0 ? "Tension against plastic resistance" : "Compression against plastic resistance", "N_abs", "N_pl_Rd", "cl. 6.2.3, 6.2.4", resultSymbol: "u_N");

        if (c.Si("N_Ed") > 0)
        {
            c.Output("N_u_Rd", "Net-section fracture resistance");
            c.Output("u_net", "Net-section fracture utilisation");
            c.Check("Tension against the net-section fracture resistance", "N_Ed", "N_u_Rd", "cl. 6.2.3(2)(b)", resultSymbol: "u_net");
        }

        c.Check("Bending moment y-y against bending resistance", "M_y_Ed", "M_y_c_Rd", "cl. 6.2.5", resultSymbol: "u_M_y");
        c.Check("Bending moment z-z against bending resistance", "M_z_Ed", "M_z_c_Rd", "cl. 6.2.5", resultSymbol: "u_M_z");
        c.Check("Shear force against plastic shear resistance", "V_Ed", "V_pl_Rd", "cl. 6.2.6", resultSymbol: "u_V");
        c.Check("Cross-section interaction against its limit", "u_section", "u_limit", "cl. 6.2.1(7)");

        if (compression)
        {
            c.Check("Axial force against buckling resistance", "N_abs", "N_b_Rd", "cl. 6.3.1.1", resultSymbol: "u_buckling");
            c.Check("Member interaction against its limit", "u_member", "u_limit", "cl. 6.3.3");
        }
    }
}
