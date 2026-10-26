using CsCheck;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class MemberChecksTests
{
    private static readonly MemberChecksCalculator Calc = new();

    private static CalcInputSet Uc(params CalcInputValue[] overrides)
    {
        var baseline = Calc.Template.TestCases[0].Inputs.ToDictionary(v => v.Symbol);
        foreach (var o in overrides) baseline[o.Symbol] = o;
        return new CalcInputSet([.. baseline.Values]);
    }

    private static CalcInputSet Shs(params CalcInputValue[] more) =>
        Uc([
            CalcInputValue.Quantity("N_Ed", -700, "kN"), CalcInputValue.Quantity("M_y_Ed", 20, "kN*m"), CalcInputValue.Quantity("M_z_Ed", 0, "kN*m"), CalcInputValue.Quantity("V_Ed", 0, "kN"),
            CalcInputValue.Quantity("A", 40, "cm^2"), CalcInputValue.Quantity("W_pl_y", 150, "cm^3"), CalcInputValue.Quantity("W_pl_z", 150, "cm^3"),
            CalcInputValue.Quantity("I_y", 1200, "cm^4"), CalcInputValue.Quantity("I_z", 1200, "cm^4"), CalcInputValue.Quantity("A_v", 20, "cm^2"),
            CalcInputValue.Quantity("L_cr_y", 3, "m"), CalcInputValue.Quantity("L_cr_z", 3, "m"), CalcInputValue.Choice("curve_y", "a"), CalcInputValue.Choice("curve_z", "a"),
            .. more]);

    [Fact]
    public void A_Member_Interaction_With_The_Default_K_Of_One_Under_Heavy_Compression_Warns_And_Uses_The_Annex_B_Bound()
    {
        // P1-struct.en1993-member-02. SHS 40 cm2, W_pl 150 cm3, I 1200 cm4, S355, curve a, L 3 m, N -700, uniform M_y 20.
        // python3: lambda 0.71683, chi 0.83970, N_b 1192.37 kN, n 0.58707; u_member 0.96265 with k_yy = 1; with 1 + 0.8n = 1.46965 it is 1.13905; with k_yy 1.3034 it is 1.07661.
        // C-2 fix (M1): a k left at 1.0 is replaced in the check by the bound, so u_member is 1.13905 (it was 0.96265 with a warning that the bound was higher).
        var r = CalcRunner.Run(Calc, Shs());
        Assert.Equal(1.1390474, r.OutputSi("u_member"), 6);
        var warning = Assert.Single(r.Notices, n => n.RuleId == "k-default");
        Assert.StartsWith("k_yy:", warning.Message, StringComparison.Ordinal);
        Assert.Contains("1.470", warning.Message, StringComparison.Ordinal);

        var entered = CalcRunner.Run(Calc, Shs(CalcInputValue.Of("k_yy", 1.3034)));
        Assert.Equal(1.0766052, entered.OutputSi("u_member"), 6);
        Assert.DoesNotContain(entered.Notices, n => n.RuleId == "k-default");

        // Light compression (N/N_b,Rd <= 0.3), no moment about the axis, or tension: no warning.
        Assert.DoesNotContain(CalcRunner.Run(Calc, Shs(CalcInputValue.Quantity("N_Ed", -300, "kN"))).Notices, n => n.RuleId == "k-default");
        Assert.DoesNotContain(CalcRunner.Run(Calc, Shs(CalcInputValue.Quantity("M_y_Ed", 0, "kN*m"))).Notices, n => n.RuleId == "k-default");
        Assert.DoesNotContain(CalcRunner.Run(Calc, Shs(CalcInputValue.Quantity("N_Ed", 700, "kN"), CalcInputValue.Quantity("A_net", 40, "cm^2"), CalcInputValue.Quantity("f_u", 510, "MPa"))).Notices, n => n.RuleId == "k-default");
    }

    [Fact]
    public void The_Shear_Area_Helper_Has_No_Section_Defaults_So_A_New_Sheet_Cannot_Borrow_Another_Sections_Shear_Area()
    {
        // P1-struct.en1993-member-R1. Before: A 252 cm2 with the seeded UC 203 helper gave A_v 210.24 cm2, V_pl,Rd 4309 kN and u_V 0.348 for V_Ed 1500 kN.
        var opened = CalcTemplateHasher.NewSheetInputs(Calc.Template);

        foreach (var symbol in new[] { "h", "b", "t_w", "t_f", "r" })
            Assert.Null(opened.Find(symbol));

        var blocked = CalcRunner.Run(Calc, opened.With(CalcInputValue.Quantity("A", 252, "cm^2")).With(CalcInputValue.Quantity("V_Ed", 1500, "kN")));
        Assert.True(blocked.IsBlocked);
        Assert.Equal("shear-area", blocked.Blocks[0].RuleId);
    }

    [Fact]
    public void Helper_Dimensions_That_Do_Not_Match_The_Entered_Area_Warn()
    {
        // The UC 203x203x46 helper (2bt_f + (h - 2t_f)t_w = 57.84 cm2 by python3) against A = 252 cm2 gives the old A_v of 210.24 cm2 and u_V 0.348.
        var r = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("A", 252, "cm^2"), CalcInputValue.Quantity("V_Ed", 1500, "kN")));
        Assert.Equal(210.244e-4, r.OutputSi("A_v"), 8);
        var warning = Assert.Single(r.Notices, n => n.RuleId == "helper-area");
        Assert.Contains("57.8", warning.Message, StringComparison.Ordinal);

        // The matching section raises none.
        Assert.DoesNotContain(CalcRunner.Run(Calc, Uc()).Notices, n => n.RuleId == "helper-area");

        // A_v entered: the helper is not used and not checked.
        Assert.DoesNotContain(CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("A", 252, "cm^2"), CalcInputValue.Quantity("A_v", 70, "cm^2"))).Notices, n => n.RuleId == "helper-area");
    }

    [Fact]
    public void Reduction_Factor_Is_One_For_Stocky_And_Falls_With_Slenderness()
    {
        Gen.Select(Gen.Double[0.5, 12], Gen.Double[0.5, 12]).Sample((l1, l2) =>
        {
            var r1 = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("L_cr_z", Math.Min(l1, l2), "m")));
            var r2 = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("L_cr_z", Math.Max(l1, l2), "m")));
            Assert.False(r1.IsBlocked);
            Assert.True(r1.OutputSi("chi_z") <= 1 + 1e-12);
            Assert.True(r1.OutputSi("chi_z") >= r2.OutputSi("chi_z") - 1e-12);
            Assert.True(r1.OutputSi("N_b_z_Rd") <= r1.OutputSi("N_pl_Rd") + 1e-6);
        }, iter: 100);
    }

    [Fact]
    public void Short_Member_Buckling_Resistance_Equals_Plastic_Resistance()
    {
        var r = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("L_cr_y", 0.1, "m"), CalcInputValue.Quantity("L_cr_z", 0.1, "m")));
        Assert.Equal(1, r.OutputSi("chi_z"), 9);
        Assert.Equal(r.OutputSi("N_pl_Rd"), r.OutputSi("N_b_Rd"), 1e-6);
    }

    [Fact]
    public void Tension_Skips_Buckling_Checks()
    {
        var r = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("N_Ed", 500, "kN"), CalcInputValue.Quantity("A_net", 58.7, "cm^2"), CalcInputValue.Quantity("f_u", 510, "MPa")));
        Assert.False(r.IsBlocked);
        Assert.DoesNotContain(r.Checks, c => c.Name.StartsWith("Flexural", StringComparison.Ordinal));
        Assert.Equal(0, r.OutputSi("u_buckling"));
        Assert.Equal(500.0 / 2083.85, r.OutputSi("u_N"), 4);
    }

    [Fact]
    public void High_Shear_Reduces_Bending_Resistance()
    {
        var low = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("V_Ed", 100, "kN")));
        var high = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("V_Ed", 300, "kN")));
        Assert.Equal(0, low.OutputSi("rho"));
        Assert.True(high.OutputSi("rho") > 0);
        Assert.True(high.OutputSi("M_y_c_Rd") < low.OutputSi("M_y_c_Rd"));
        Assert.Contains(high.Notices, n => n.RuleId == "high-shear");
    }

    private static CalcInputSet Ub(params CalcInputValue[] overrides)
    {
        var baseline = Calc.Template.TestCases.Single(t => t.Id == "ka-ub-bending-shear").Inputs.ToDictionary(v => v.Symbol);
        foreach (var o in overrides) baseline[o.Symbol] = o;
        return new CalcInputSet([.. baseline.Values]);
    }

    [Fact]
    public void High_Shear_Reduces_Only_The_Web_Part_Of_The_Plastic_Modulus_About_The_Major_Axis()
    {
        // struct.en1993-member-01: UB 305x165x40 S275, M_y 120 kN.m, V 250 kN (0.785 V_pl,Rd, rho 0.3241): the sheet cut M_y,c,Rd to (1 - rho) W_pl,y f_y = 115.80 kN.m (u 1.036, FAIL).
        // Eq. 6.30 gives (W_pl,y - rho A_w^2/(4 t_w)) f_y = 160.62 kN.m (u 0.747). python3: A_w = (303.4 - 2 * 10.2) * 6 = 1698 mm2.
        var r = CalcRunner.Run(Calc, Ub(CalcInputValue.Quantity("V_Ed", 250, "kN")));
        Assert.Equal(0.324088, r.OutputSi("rho"), 6);
        Assert.Equal(160.618, r.OutputSi("M_y_c_Rd") / 1e3, 3);
        Assert.Equal(0.74711, r.OutputSi("u_M_y"), 4);

        // The minor-axis moment is carried by the flanges: no reduction for load in the web plane (W_pl,z f_y = 142 cm3 x 275 MPa = 39.05 kN.m).
        Assert.Equal(39.05, r.OutputSi("M_z_c_Rd") / 1e3, 6);
    }

    [Fact]
    public void High_Shear_Without_The_Web_Dimensions_Keeps_The_Whole_Section_Reduction_And_Says_So()
    {
        var noDims = new CalcInputSet(Ub(CalcInputValue.Quantity("V_Ed", 250, "kN"), CalcInputValue.Quantity("A_v", 20.0676, "cm^2")).Values.Where(v => v.Symbol is not ("h" or "b" or "t_w" or "t_f" or "r")));
        var r = CalcRunner.Run(Calc, noDims);
        Assert.Equal((1 - 0.324088) * 623 * 275 / 1e3, r.OutputSi("M_y_c_Rd") / 1e3, 2);
        var warning = Assert.Single(r.Notices, n => n.RuleId == "high-shear");
        Assert.Contains("whole", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Member_Interaction_Divides_The_Moments_By_The_Characteristic_Resistance_Over_Gamma_M1()
    {
        // struct.en1993-member-R2: UC 203x203x46, N -900, M_y 40, gamma_M0 1.05, gamma_M1 1.1: u_member 1.1365 (moments over W f_y/gamma_M0) against 1.1479 (EN 6.61, M_Rk/gamma_M1).
        // python3: chi_z 0.5288 (curve c); N_b,z,Rd = chi A f_y/1.1; M_Rk/1.1 = 497e3 * 355/1.1 N.mm = 160.395 kN.m.
        // k_yy and k_zz are the user's 1.0, recorded as an assumption, so the check keeps them (a k left at 1.0 would be replaced by the Annex bound).
        var r = CalcRunner.Run(Calc, Uc(CalcInputValue.Of("gamma_M0", 1.05), CalcInputValue.Of("gamma_M1", 1.1), CalcInputValue.Quantity("N_Ed", -900, "kN"), CalcInputValue.Quantity("M_y_Ed", 40, "kN*m"),
            CalcInputValue.Of("k_yy", 1.0, CalcProvenance.Assumed("Simplified interaction: k_yy = 1.0.")), CalcInputValue.Of("k_zz", 1.0, CalcProvenance.Assumed("Simplified interaction: k_zz = 1.0."))));
        var nb = r.OutputSi("N_b_Rd") / 1e3;
        Assert.Equal(900 / nb + 40 / (497e3 * 355 / 1.1 / 1e6), r.OutputSi("u_member"), 6);
        Assert.Equal(1.1479, r.OutputSi("u_member"), 3);
    }

    [Fact]
    public void A_Tension_Member_Needs_Its_Net_Area_And_Is_Checked_For_Net_Section_Fracture()
    {
        // struct.en1993-member-06: only gross-section yielding was checked and a warning said bolt holes were not accounted for (N_u,Rd = 0.9 A_net f_u/gamma_M2 governs many bolted ties).
        // C-2 fix (M1): without A_net and f_u a tension member is blocked; with them the fracture resistance is a check.
        var blocked = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("N_Ed", 500, "kN")));
        Assert.True(blocked.IsBlocked);
        Assert.Equal("net-section", blocked.Blocks[0].RuleId);
        Assert.StartsWith("A_net:", blocked.Blocks[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain(CalcRunner.Run(Calc, Uc()).Notices, n => n.RuleId == "net-section");

        // python3: A_net 40 cm2 (A 58.7 cm2, bolt holes), f_u 490 MPa, gamma_M2 1.1: N_u,Rd = 0.9 × 4000 × 490 / 1.1 = 1603.64 kN; N = 1700 kN: u = 1.06 (a fail; N_pl,Rd = 5870 × 355 = 2083.85 kN passes).
        var holes = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("N_Ed", 1700, "kN"), CalcInputValue.Quantity("A_net", 40, "cm^2"), CalcInputValue.Quantity("f_u", 490, "MPa")));
        Assert.False(holes.IsBlocked);
        Assert.Equal(1603.6364, holes.OutputSi("N_u_Rd") / 1e3, 3);
        Assert.Equal(1700 / 1603.6364, holes.OutputSi("u_net"), 6);
        Assert.Equal(0.81577, holes.OutputSi("u_N"), 4);
        Assert.False(Assert.Single(holes.Checks, c => c.Name.StartsWith("Tension against the net-section", StringComparison.Ordinal)).Passed);
        Assert.False(holes.Passed);

        // A net area above the gross area is refused.
        var larger = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("N_Ed", 500, "kN"), CalcInputValue.Quantity("A_net", 80, "cm^2"), CalcInputValue.Quantity("f_u", 490, "MPa")));
        Assert.True(larger.IsBlocked);
        Assert.StartsWith("A_net:", larger.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Member_Bending_About_Y_With_An_Unrestrained_Compression_Flange_Is_Blocked_Not_Passed()
    {
        // struct.en1993-member no-ltb: lateral-torsional buckling is not checked, so the sheet used to warn on every run and pass an unrestrained beam.
        var restrained = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("M_y_Ed", 120, "kN*m"), CalcInputValue.Quantity("N_Ed", 0, "kN")));
        Assert.False(restrained.IsBlocked);
        Assert.DoesNotContain(restrained.Notices, n => n.RuleId == "no-ltb");
        Assert.Contains(restrained.RunAssumptions, a => a.Statement.Contains("restrained", StringComparison.Ordinal));

        var free = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("M_y_Ed", 120, "kN*m"), CalcInputValue.Quantity("N_Ed", 0, "kN"), CalcInputValue.Choice("ltb", MemberChecksCalculator.NotRestrained)));
        Assert.True(free.IsBlocked);
        Assert.Equal("no-ltb", free.Blocks[0].RuleId);
        Assert.StartsWith("ltb:", free.Blocks[0].Message, StringComparison.Ordinal);

        // No moment about y: nothing to buckle laterally, the choice is moot.
        Assert.False(CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("M_y_Ed", 0, "kN*m"), CalcInputValue.Choice("ltb", MemberChecksCalculator.NotRestrained))).IsBlocked);
    }

    [Fact]
    public void Class_4_Blocks_And_Class_3_Uses_Elastic_Modulus()
    {
        Assert.True(CalcRunner.Run(Calc, Uc(CalcInputValue.Choice("section_class", "4"))).IsBlocked);
        var c3 = CalcRunner.Run(Calc, Uc(CalcInputValue.Choice("section_class", "3")));
        var c1 = CalcRunner.Run(Calc, Uc(CalcInputValue.Choice("section_class", "1")));
        Assert.Equal(450.0 / 497.0, c3.OutputSi("M_y_c_Rd") / c1.OutputSi("M_y_c_Rd"), 9);
    }

    [Fact]
    public void Imperfection_Override_And_Partial_Factors_Apply()
    {
        var a = CalcRunner.Run(Calc, Uc(CalcInputValue.Of("alpha_z", 0.13)));
        var b = CalcRunner.Run(Calc, Uc());
        Assert.True(a.OutputSi("chi_z") > b.OutputSi("chi_z"));
        var g = CalcRunner.Run(Calc, Uc(CalcInputValue.Of("gamma_M1", 1.1)));
        Assert.Equal(b.OutputSi("N_b_z_Rd") / 1.1, g.OutputSi("N_b_z_Rd"), 1e-6);
    }

    [Fact]
    public void Governing_Check_Is_The_Highest_Utilisation()
    {
        var r = CalcRunner.Run(Calc, Uc());
        Assert.NotNull(r.Governing);
        Assert.Equal(r.Checks.Max(c => c.Utilisation), r.Governing!.Utilisation);
        Assert.True(r.Passed);
        var fail = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("N_Ed", -1500, "kN")));
        Assert.False(fail.Passed);
        Assert.StartsWith("Flexural", fail.Governing!.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_Shear_Area_Blocks_With_The_Symbol()
    {
        var inputs = Calc.Template.TestCases[0].Inputs.Where(v => v.Symbol is not ("h" or "b" or "t_w" or "t_f" or "r")).ToList();
        var r = CalcRunner.Run(Calc, new CalcInputSet(inputs));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.Message.StartsWith("A_v:", StringComparison.Ordinal));
    }
}
