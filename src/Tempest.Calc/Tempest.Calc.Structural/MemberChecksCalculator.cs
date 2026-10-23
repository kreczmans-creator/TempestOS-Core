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

    /// <summary>Imperfection factors by buckling curve (Gardner &amp; Nethercot, Designers' Guide to EN 1993-1-1, §6.3.1), overridable per sheet.</summary>
    public static double ImperfectionFactor(string curve) => curve switch
    {
        "a0" => 0.13,
        "a" => 0.21,
        "b" => 0.34,
        "c" => 0.49,
        "d" => 0.76,
        _ => throw new ArgumentOutOfRangeException(nameof(curve), curve, "Buckling curve must be a0, a, b, c or d."),
    };

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Structural, "EN 1993-1-1 member checks (basic)",
        "Resistance checks of a steel member of class 1, 2 or 3 cross-section: tension (6.2.3), compression (6.2.4) and flexural buckling about y and z (6.3.1), bending about y and z (6.2.5), shear with the high-shear reduction (6.2.6, 6.2.8), the linear cross-section interaction (6.2.1(7)) and a simplified member interaction (6.3.3 with user-entered k factors). Lateral-torsional buckling and class 4 sections are outside this sheet.",
        "N_pl,Rd = A f_y/γ_M0; M_c,Rd = W f_y/γ_M0 with W = W_pl (class 1–2) or W_el (class 3); V_pl,Rd = A_v f_y/(√3 γ_M0); ρ = (2V_Ed/V_pl,Rd − 1)² when V_Ed > 0.5 V_pl,Rd reduces the bending resistance by (1 − ρ); N_cr = π²EI/L_cr², λ̄ = √(A f_y/N_cr), Φ = 0.5[1 + α(λ̄ − 0.2) + λ̄²], χ = 1/(Φ + √(Φ² − λ̄²)) ≤ 1, N_b,Rd = χ A f_y/γ_M1; cross-section: N_Ed/N_Rd + M_y,Ed/M_y,Rd + M_z,Ed/M_z,Rd ≤ 1; member: N_Ed/N_b,Rd,min + k_yy M_y,Ed/M_y,Rd + k_zz M_z,Ed/M_z,Rd ≤ 1.",
        [
            new CalcInputSpec("N_Ed", "Design axial force (tension +, compression −)", CalcInputKind.Quantity, "kN", Default: -800, Min: -1e5, Max: 1e5, HardMin: -1e8, HardMax: 1e8, Group: "Actions"),
            new CalcInputSpec("M_y_Ed", "Design moment about y-y (major)", CalcInputKind.Quantity, "kN*m", Default: 60, Min: 0, Max: 1e5, HardMin: 0, HardMax: 1e8, Group: "Actions"),
            new CalcInputSpec("M_z_Ed", "Design moment about z-z (minor)", CalcInputKind.Quantity, "kN*m", Default: 0, Min: 0, Max: 1e5, HardMin: 0, HardMax: 1e8, Group: "Actions"),
            new CalcInputSpec("V_Ed", "Design shear force", CalcInputKind.Quantity, "kN", Default: 40, Min: 0, Max: 1e5, HardMin: 0, HardMax: 1e8, Group: "Actions"),
            new CalcInputSpec("f_y", "Yield strength (for the thickness)", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials", ReferenceProperty: "YieldStrength", ReferenceDimension: "Pressure", Default: 355, Min: 200, Max: 700, HardMin: 1, HardMax: 2000, Group: "Material"),
            new CalcInputSpec("E", "Modulus of elasticity", CalcInputKind.Quantity, "GPa", ReferenceLibrary: "Materials", ReferenceProperty: "YoungsModulus", ReferenceDimension: "Pressure", Default: 210, Min: 190, Max: 220, HardMin: 1, HardMax: 1000, Group: "Material"),
            new CalcInputSpec("gamma_M0", "Partial factor γ_M0 (cross-section resistance)", CalcInputKind.Number, Default: 1.0, Min: 1.0, Max: 1.25, HardMin: 0.5, HardMax: 5, Group: "Material"),
            new CalcInputSpec("gamma_M1", "Partial factor γ_M1 (member buckling)", CalcInputKind.Number, Default: 1.0, Min: 1.0, Max: 1.25, HardMin: 0.5, HardMax: 5, Group: "Material"),
            new CalcInputSpec("section_class", "Cross-section class", CalcInputKind.Choice, Choices: ["1", "2", "3", "4"], DefaultChoice: "1", Group: "Section"),
            new CalcInputSpec("A", "Area A", CalcInputKind.Quantity, "cm^2", Default: 58.7, Min: 1, Max: 1e5, HardMin: 1e-3, HardMax: 1e7, Group: "Section"),
            new CalcInputSpec("W_pl_y", "W_pl,y", CalcInputKind.Quantity, "cm^3", Default: 497, Min: 0.1, Max: 1e7, HardMin: 1e-6, HardMax: 1e9, Group: "Section"),
            new CalcInputSpec("W_el_y", "W_el,y", CalcInputKind.Quantity, "cm^3", Default: 450, Min: 0.1, Max: 1e7, HardMin: 1e-6, HardMax: 1e9, Group: "Section"),
            new CalcInputSpec("W_pl_z", "W_pl,z", CalcInputKind.Quantity, "cm^3", Default: 231, Min: 0.1, Max: 1e7, HardMin: 1e-6, HardMax: 1e9, Group: "Section"),
            new CalcInputSpec("W_el_z", "W_el,z", CalcInputKind.Quantity, "cm^3", Default: 152, Min: 0.1, Max: 1e7, HardMin: 1e-6, HardMax: 1e9, Group: "Section"),
            new CalcInputSpec("I_y", "I_y", CalcInputKind.Quantity, "cm^4", Default: 4568, Min: 0.1, Max: 1e9, HardMin: 1e-6, HardMax: 1e12, Group: "Section"),
            new CalcInputSpec("I_z", "I_z", CalcInputKind.Quantity, "cm^4", Default: 1548, Min: 0.1, Max: 1e9, HardMin: 1e-6, HardMax: 1e12, Group: "Section"),
            new CalcInputSpec("A_v", "Shear area A_v (blank = rolled I from h, b, t_w, t_f, r)", CalcInputKind.Quantity, "cm^2", Min: 0.1, Max: 1e5, HardMin: 1e-3, HardMax: 1e7, IsOptional: true, Group: "Section"),
            new CalcInputSpec("h", "Depth h (shear-area helper)", CalcInputKind.Quantity, "mm", Default: 203.2, Min: 1, Max: 5000, HardMin: 1e-3, HardMax: 1e5, IsOptional: true, Group: "Section"),
            new CalcInputSpec("b", "Width b (helper)", CalcInputKind.Quantity, "mm", Default: 203.6, Min: 1, Max: 5000, HardMin: 1e-3, HardMax: 1e5, IsOptional: true, Group: "Section"),
            new CalcInputSpec("t_w", "Web thickness (helper)", CalcInputKind.Quantity, "mm", Default: 7.2, Min: 0.1, Max: 500, HardMin: 1e-3, HardMax: 1e4, IsOptional: true, Group: "Section"),
            new CalcInputSpec("t_f", "Flange thickness (helper)", CalcInputKind.Quantity, "mm", Default: 11, Min: 0.1, Max: 500, HardMin: 1e-3, HardMax: 1e4, IsOptional: true, Group: "Section"),
            new CalcInputSpec("r", "Root radius (helper)", CalcInputKind.Quantity, "mm", Default: 10.2, Min: 0, Max: 500, HardMin: 0, HardMax: 1e4, IsOptional: true, Group: "Section"),
            new CalcInputSpec("eta", "Shear-area factor η", CalcInputKind.Number, Default: 1.0, Min: 1.0, Max: 1.2, HardMin: 0.5, HardMax: 2, Group: "Section"),
            new CalcInputSpec("L_cr_y", "Buckling length about y-y", CalcInputKind.Quantity, "m", Default: 4, Min: 0.1, Max: 100, HardMin: 1e-3, HardMax: 1e4, Group: "Buckling"),
            new CalcInputSpec("L_cr_z", "Buckling length about z-z", CalcInputKind.Quantity, "m", Default: 4, Min: 0.1, Max: 100, HardMin: 1e-3, HardMax: 1e4, Group: "Buckling"),
            new CalcInputSpec("curve_y", "Buckling curve about y-y", CalcInputKind.Choice, Choices: Curves, DefaultChoice: "b", Group: "Buckling"),
            new CalcInputSpec("curve_z", "Buckling curve about z-z", CalcInputKind.Choice, Choices: Curves, DefaultChoice: "c", Group: "Buckling"),
            new CalcInputSpec("alpha_y", "Imperfection factor α_y (blank = from the curve)", CalcInputKind.Number, Min: 0.1, Max: 0.8, HardMin: 0, HardMax: 2, IsOptional: true, Group: "Buckling"),
            new CalcInputSpec("alpha_z", "Imperfection factor α_z (blank = from the curve)", CalcInputKind.Number, Min: 0.1, Max: 0.8, HardMin: 0, HardMax: 2, IsOptional: true, Group: "Buckling"),
            new CalcInputSpec("k_yy", "Interaction factor k_yy (member check)", CalcInputKind.Number, Default: 1.0, Min: 0.5, Max: 2.0, HardMin: 0.1, HardMax: 10, Group: "Buckling"),
            new CalcInputSpec("k_zz", "Interaction factor k_zz (member check)", CalcInputKind.Number, Default: 1.0, Min: 0.5, Max: 2.0, HardMin: 0.1, HardMax: 10, Group: "Buckling"),
        ],
        [
            new CalcAssumption("The cross-section class is as stated by the user (1, 2 or 3); class 4 effective properties are not derived here.", "6.2.2.5 effective widths are outside this sheet."),
            new CalcAssumption("Lateral-torsional buckling is not checked: the compression flange is assumed restrained, or LTB is checked elsewhere.", "6.3.2 not implemented."),
            new CalcAssumption("The member interaction uses the user's k_yy and k_zz (default 1.0) with the smaller flexural-buckling reduction on the axial term; the Annex A/B interaction factors are not derived.", "6.3.3 simplified."),
            new CalcAssumption("Imperfection factors per curve are the textbook values and may be overridden; partial factors default to 1.0 (UK National Annex) and may be overridden.", "User-editable with provenance."),
        ],
        [
            new CalcClauseRef("BS EN 1993-1-1", "6.2.3", "Tension"),
            new CalcClauseRef("BS EN 1993-1-1", "6.2.4", "Compression"),
            new CalcClauseRef("BS EN 1993-1-1", "6.2.5", "Bending moment"),
            new CalcClauseRef("BS EN 1993-1-1", "6.2.6", "Shear"),
            new CalcClauseRef("BS EN 1993-1-1", "6.2.8", "Bending and shear"),
            new CalcClauseRef("BS EN 1993-1-1", "6.2.1(7)", "Linear summation of utilisations"),
            new CalcClauseRef("BS EN 1993-1-1", "6.3.1.1, 6.3.1.2", "Uniform members in compression: buckling resistance, buckling curves"),
            new CalcClauseRef("BS EN 1993-1-1", "6.3.3", "Uniform members in bending and axial compression"),
            new CalcClauseRef("Gardner & Nethercot, Designers' Guide to Eurocode 3", "§6.3.1", "Imperfection factors for the buckling curves"),
        ],
        [
            new CalcApplicabilityRule("class-4", "Class 4 cross-sections need effective properties: outside this sheet.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("shear-area", "No shear area and no rolled-I helper dimensions: the shear check cannot run.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("high-shear", "V_Ed exceeds half the plastic shear resistance: the bending resistance is reduced (6.2.8).", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("no-ltb", "Lateral-torsional buckling is not checked by this sheet.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("slender", "Non-dimensional slenderness above 2.0: check the member is intended to be this slender.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-uc-compression", "UC 203×203×46 S355, L_cr = 4 m, axial compression (flexural buckling about z-z)",
                new CalcReferenceSource("Analytic (EN 1993-1-1 6.3.1 formulae) in the manner of SCI P362 worked examples; properties from the Blue Book", null, "N_cr,z = π²·210000·1548e4/4000² = 2005.3 kN; λ̄_z = √(5870·355/2005.3e3) = 1.0194; curve c α = 0.49: Φ = 1.2203, χ = 0.5288; N_b,z,Rd = 1101.9 kN; N_pl,Rd = 2083.9 kN"),
                [CalcInputValue.Quantity("N_Ed", -900, "kN"), CalcInputValue.Quantity("M_y_Ed", 0, "kN*m"), CalcInputValue.Quantity("M_z_Ed", 0, "kN*m"), CalcInputValue.Quantity("V_Ed", 0, "kN"), CalcInputValue.Quantity("f_y", 355, "MPa"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Choice("section_class", "1"), CalcInputValue.Quantity("A", 58.7, "cm^2"), CalcInputValue.Quantity("W_pl_y", 497, "cm^3"), CalcInputValue.Quantity("W_el_y", 450, "cm^3"), CalcInputValue.Quantity("W_pl_z", 231, "cm^3"), CalcInputValue.Quantity("W_el_z", 152, "cm^3"), CalcInputValue.Quantity("I_y", 4568, "cm^4"), CalcInputValue.Quantity("I_z", 1548, "cm^4"), CalcInputValue.Quantity("h", 203.2, "mm"), CalcInputValue.Quantity("b", 203.6, "mm"), CalcInputValue.Quantity("t_w", 7.2, "mm"), CalcInputValue.Quantity("t_f", 11, "mm"), CalcInputValue.Quantity("r", 10.2, "mm"), CalcInputValue.Quantity("L_cr_y", 4, "m"), CalcInputValue.Quantity("L_cr_z", 4, "m"), CalcInputValue.Choice("curve_y", "b"), CalcInputValue.Choice("curve_z", "c")],
                [new CalcExpectedValue("N_pl_Rd", 2083.85, "kN", 1e-4), new CalcExpectedValue("N_cr_z", 2005.3, "kN", 1e-3), new CalcExpectedValue("lambda_z", 1.0194, "", 1e-3), new CalcExpectedValue("chi_z", 0.5288, "", 1e-3), new CalcExpectedValue("N_b_z_Rd", 1101.9, "kN", 1e-3), new CalcExpectedValue("u_buckling", 0.8168, "", 1e-3)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-ub-bending-shear", "UB 305×165×40 S275, bending and shear (rolled-I shear area helper)",
                new CalcReferenceSource("Analytic (EN 1993-1-1 6.2.5, 6.2.6) with Blue Book properties", null, "M_c,Rd = 623e3·275 = 171.3 kN·m; A_v = A − 2bt_f + (t_w + 2r)t_f = 5130 − 3366 + 242.8 = 2006.8 mm²; V_pl,Rd = 2006.8·275/√3 = 318.6 kN"),
                [CalcInputValue.Quantity("N_Ed", 0, "kN"), CalcInputValue.Quantity("M_y_Ed", 120, "kN*m"), CalcInputValue.Quantity("M_z_Ed", 0, "kN*m"), CalcInputValue.Quantity("V_Ed", 100, "kN"), CalcInputValue.Quantity("f_y", 275, "MPa"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Choice("section_class", "1"), CalcInputValue.Quantity("A", 51.3, "cm^2"), CalcInputValue.Quantity("W_pl_y", 623, "cm^3"), CalcInputValue.Quantity("W_el_y", 560, "cm^3"), CalcInputValue.Quantity("W_pl_z", 142, "cm^3"), CalcInputValue.Quantity("W_el_z", 92.6, "cm^3"), CalcInputValue.Quantity("I_y", 8503, "cm^4"), CalcInputValue.Quantity("I_z", 764, "cm^4"), CalcInputValue.Quantity("h", 303.4, "mm"), CalcInputValue.Quantity("b", 165, "mm"), CalcInputValue.Quantity("t_w", 6, "mm"), CalcInputValue.Quantity("t_f", 10.2, "mm"), CalcInputValue.Quantity("r", 8.9, "mm"), CalcInputValue.Quantity("L_cr_y", 6, "m"), CalcInputValue.Quantity("L_cr_z", 1.5, "m"), CalcInputValue.Choice("curve_y", "a"), CalcInputValue.Choice("curve_z", "b")],
                [new CalcExpectedValue("M_y_c_Rd", 171.325, "kN*m", 1e-4), new CalcExpectedValue("A_v", 20.068, "cm^2", 1e-4), new CalcExpectedValue("V_pl_Rd", 318.62, "kN", 1e-3), new CalcExpectedValue("u_M_y", 0.70043, "", 1e-3), new CalcExpectedValue("u_V", 0.31385, "", 1e-3)]),
            new CalcTestCase("ka-tension-class3", "Class 3 tie, tension with minor-axis moment (elastic modulus governs)",
                new CalcReferenceSource("Analytic (6.2.3, 6.2.5 class 3, 6.2.1(7))", null, "N_pl,Rd = 2000·355 = 710 kN; M_z,c,Rd = W_el,z f_y = 30e3·355 = 10.65 kN·m; linear interaction 400/710 + 5/10.65 = 0.5634 + 0.4695 = 1.0329 → FAIL"),
                [CalcInputValue.Quantity("N_Ed", 400, "kN"), CalcInputValue.Quantity("M_y_Ed", 0, "kN*m"), CalcInputValue.Quantity("M_z_Ed", 5, "kN*m"), CalcInputValue.Quantity("V_Ed", 0, "kN"), CalcInputValue.Quantity("f_y", 355, "MPa"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Choice("section_class", "3"), CalcInputValue.Quantity("A", 20, "cm^2"), CalcInputValue.Quantity("W_pl_y", 100, "cm^3"), CalcInputValue.Quantity("W_el_y", 80, "cm^3"), CalcInputValue.Quantity("W_pl_z", 40, "cm^3"), CalcInputValue.Quantity("W_el_z", 30, "cm^3"), CalcInputValue.Quantity("I_y", 1000, "cm^4"), CalcInputValue.Quantity("I_z", 200, "cm^4"), CalcInputValue.Quantity("A_v", 8, "cm^2"), CalcInputValue.Quantity("L_cr_y", 3, "m"), CalcInputValue.Quantity("L_cr_z", 3, "m"), CalcInputValue.Choice("curve_y", "b"), CalcInputValue.Choice("curve_z", "c")],
                [new CalcExpectedValue("N_pl_Rd", 710, "kN", 1e-9), new CalcExpectedValue("M_z_c_Rd", 10.65, "kN*m", 1e-9), new CalcExpectedValue("u_section", 1.03294, "", 1e-4)]),
        ],
        DiagramKey: "member-checks",
        Category: "Design");

    public void Run(CalcRunContext c)
    {
        var cls = c.InputText("section_class");

        if (cls == "4")
        {
            c.Block("class-4", "section_class: class 4 needs effective section properties; this sheet covers classes 1 to 3.");
            return;
        }

        foreach (var s in new[] { "N_Ed", "M_y_Ed", "M_z_Ed", "V_Ed", "f_y", "E", "gamma_M0", "gamma_M1", "A", "W_pl_y", "W_el_y", "W_pl_z", "W_el_z", "I_y", "I_z", "eta", "L_cr_y", "L_cr_z", "k_yy", "k_zz" })
            c.Input(s);

        var plastic = cls is "1" or "2";
        c.Warn("no-ltb", "M_y_Ed: lateral-torsional buckling is not checked by this sheet; restrain the compression flange or check 6.3.2 separately.");

        // Shear area.
        if (c.Has("A_v"))
        {
            c.Input("A_v");
        }
        else if (c.Has("h") && c.Has("b") && c.Has("t_w") && c.Has("t_f"))
        {
            c.Input("h"); c.Input("b"); c.Input("t_w"); c.Input("t_f");
            if (c.Has("r")) c.Input("r");
            c.Assign("r_h", c.Has("r") ? "r" : "0 mm", "mm", "Root radius used by the helper");
            c.Assign("A_v_rolled", "A - 2 * b * t_f + (t_w + 2 * r_h) * t_f", "cm^2", "Rolled I shear area, load parallel to the web", "6.2.6(3)");
            c.Assign("A_v_min", "eta * (h - 2 * t_f) * t_w", "cm^2", "Lower bound η h_w t_w", "6.2.6(3)");
            c.Assign("A_v", "max(A_v_rolled, A_v_min)", "cm^2", "Shear area");
        }
        else
        {
            c.Block("shear-area", "A_v: give the shear area, or h, b, t_w and t_f for the rolled-I helper.");
            return;
        }

        // Cross-section resistances.
        c.Assign("N_pl_Rd", "A * f_y / gamma_M0", "kN", "Plastic axial resistance", "6.2.3(2), 6.2.4(2)");
        c.Assign("W_y", plastic ? "W_pl_y" : "W_el_y", "cm^3", plastic ? "Section modulus about y (plastic, class 1–2)" : "Section modulus about y (elastic, class 3)", "6.2.5(2)");
        c.Assign("W_z", plastic ? "W_pl_z" : "W_el_z", "cm^3", plastic ? "Section modulus about z (plastic)" : "Section modulus about z (elastic)", "6.2.5(2)");
        c.Assign("V_pl_Rd", "A_v * f_y / (sqrt(3) * gamma_M0)", "kN", "Plastic shear resistance", "6.2.6(2)");
        c.Assign("u_V", "V_Ed / V_pl_Rd", null, "Shear utilisation");

        var highShear = c.Si("V_Ed") > 0.5 * c.Si("V_pl_Rd");

        if (highShear)
        {
            c.Warn("high-shear", "V_Ed: more than half the plastic shear resistance; the bending resistance is reduced by (1 − ρ).");
            c.Assign("rho", "(2 * V_Ed / V_pl_Rd - 1)^2", null, "Shear reduction factor", "6.2.8(3)");
        }
        else
        {
            c.Assign("rho", "0", null, "Shear reduction factor (V_Ed ≤ 0.5 V_pl,Rd)", "6.2.8(2)");
        }

        c.Assign("M_y_c_Rd", "(1 - rho) * W_y * f_y / gamma_M0", "kN*m", "Bending resistance about y", "6.2.5(2), 6.2.8");
        c.Assign("M_z_c_Rd", "(1 - rho) * W_z * f_y / gamma_M0", "kN*m", "Bending resistance about z", "6.2.5(2)");
        c.Assign("u_M_y", "M_y_Ed / M_y_c_Rd", null, "Bending utilisation, y");
        c.Assign("u_M_z", "M_z_Ed / M_z_c_Rd", null, "Bending utilisation, z");
        c.Assign("u_N", "abs(N_Ed) / N_pl_Rd", null, c.Si("N_Ed") >= 0 ? "Tension utilisation" : "Compression (cross-section) utilisation");
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
        c.Assign("u_buckling", compression ? "abs(N_Ed) / N_b_Rd" : "0", null, compression ? "Flexural buckling utilisation" : "Flexural buckling utilisation (tension: not applicable)", "6.3.1.1(1)");
        c.Assign("u_member", compression ? "abs(N_Ed) / N_b_Rd + k_yy * M_y_Ed / M_y_c_Rd + k_zz * M_z_Ed / M_z_c_Rd" : "u_M_y + u_M_z", null, "Simplified member interaction", "6.3.3(4)");

        foreach (var (symbol, text) in new[] { ("A_v", "Shear area"), ("N_pl_Rd", "N_pl,Rd"), ("V_pl_Rd", "V_pl,Rd"), ("rho", "ρ"), ("M_y_c_Rd", "M_y,c,Rd"), ("M_z_c_Rd", "M_z,c,Rd"), ("N_cr_y", "N_cr,y"), ("N_cr_z", "N_cr,z"), ("lambda_y", "λ̄_y"), ("lambda_z", "λ̄_z"), ("chi_y", "χ_y"), ("chi_z", "χ_z"), ("N_b_y_Rd", "N_b,y,Rd"), ("N_b_z_Rd", "N_b,z,Rd"), ("N_b_Rd", "N_b,Rd"), ("u_N", "Axial utilisation"), ("u_M_y", "Bending y utilisation"), ("u_M_z", "Bending z utilisation"), ("u_V", "Shear utilisation"), ("u_section", "Cross-section interaction"), ("u_buckling", "Buckling utilisation"), ("u_member", "Member interaction") })
            c.Output(symbol, text);

        c.CheckUtilisation(c.Si("N_Ed") >= 0 ? "Tension N_Ed ≤ N_pl,Rd" : "Compression N_Ed ≤ N_c,Rd", "u_N", c.Si("N_Ed") >= 0 ? "6.2.3" : "6.2.4");
        c.CheckUtilisation("Bending y M_y,Ed ≤ M_y,c,Rd", "u_M_y", "6.2.5");
        c.CheckUtilisation("Bending z M_z,Ed ≤ M_z,c,Rd", "u_M_z", "6.2.5");
        c.CheckUtilisation("Shear V_Ed ≤ V_pl,Rd", "u_V", "6.2.6");
        c.CheckUtilisation("Cross-section interaction", "u_section", "6.2.1(7)");

        if (compression)
        {
            c.CheckUtilisation("Flexural buckling N_Ed ≤ N_b,Rd", "u_buckling", "6.3.1.1");
            c.CheckUtilisation("Member interaction (simplified)", "u_member", "6.3.3");
        }
    }
}
