using CsCheck;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class BasePlateTests
{
    private static readonly BasePlateCalculator Plate = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Plate.Template);

    private static CalcInputSet Loads(double nEd, double mEd, double vEd = 0) =>
        Defaults().With(CalcInputValue.Quantity("N_Ed", nEd, "kN")).With(CalcInputValue.Quantity("M_Ed", mEd, "kN*m")).With(CalcInputValue.Quantity("V_Ed", vEd, "kN"));

    private static CalcInputSet Axial(double nEd) => Loads(nEd, 0);

    private static CalcInputSet Column(CalcInputSet set, string column) => column switch
    {
        BasePlateCalculator.ColumnRhs => set.With(CalcInputValue.Choice("column", column)).With(CalcInputValue.Quantity("h", 200, "mm")).With(CalcInputValue.Quantity("b", 200, "mm")).With(CalcInputValue.Quantity("t", 10, "mm")),
        BasePlateCalculator.ColumnChs => set.With(CalcInputValue.Choice("column", column)).With(CalcInputValue.Quantity("D", 220, "mm")).With(CalcInputValue.Quantity("t", 10, "mm")),
        _ => set,
    };

    private static string Text(CalcRunResult r, string symbol) => r.FindOutput(symbol)?.Value is TextValue t ? t.Value : throw new InvalidOperationException($"{symbol} is not a text output");

    private static double Kn(CalcRunResult r, string symbol) => r.OutputSi(symbol) / 1e3;

    [Fact]
    public void Demands_An_Independent_Check() => Assert.True(Plate.Template.IndependentCheck);

    [Theory]
    [InlineData("t_p", 9, "mm")]
    [InlineData("t_p", 101, "mm")]
    [InlineData("f_ck", 55, "MPa")]
    [InlineData("f_ck", 19, "MPa")]
    [InlineData("f_y", 200, "MPa")]
    [InlineData("B", 90, "mm")]
    [InlineData("L", 1600, "mm")]
    [InlineData("h", 40, "mm")]
    [InlineData("t_f", 2, "mm")]
    [InlineData("N_Ed", 10001, "kN")]
    [InlineData("N_Ed", -5001, "kN")]
    [InlineData("M_Ed", -1, "kN*m")]
    [InlineData("V_Ed", 2001, "kN")]
    [InlineData("alpha", 3.5, "")]
    [InlineData("alpha", 0.9, "")]
    [InlineData("alpha_cc", 0.75, "")]
    [InlineData("alpha_cc", 1.05, "")]
    [InlineData("gamma_c", 1.1, "")]
    [InlineData("gamma_c", 1.9, "")]
    [InlineData("gamma_M0", 0.95, "")]
    [InlineData("gamma_M0", 1.3, "")]
    [InlineData("gamma_M2", 1.0, "")]
    [InlineData("gamma_M2", 1.6, "")]
    [InlineData("C_fd", 0.5, "")]
    [InlineData("C_fd", -0.1, "")]
    [InlineData("x_b", 40, "mm")]
    [InlineData("f_yb", 700, "MPa")]
    [InlineData("f_ub", 300, "MPa")]
    [InlineData("A_s", 20, "mm^2")]
    [InlineData("n_t", 0, "")]
    public void Inputs_Outside_Their_Range_Are_A_Hard_Fail(string symbol, double value, string unit)
    {
        var value2 = unit.Length > 0 ? CalcInputValue.Quantity(symbol, value, unit) : CalcInputValue.Of(symbol, value);
        var notices = CalcInputValidator.Validate(Plate.Template, Defaults().With(value2));
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.True(CalcRunner.Run(Plate, Defaults().With(value2)).IsBlocked);
    }

    [Fact]
    public void Every_Numeric_Input_Has_A_Hard_Limit_Equal_To_Its_Stated_Range() =>
        Assert.All(Plate.Template.Inputs.Where(i => i.Kind is CalcInputKind.Number or CalcInputKind.Quantity), i =>
        {
            Assert.Equal(i.Min, i.HardMin);
            Assert.Equal(i.Max, i.HardMax);
        });

    [Theory]
    [InlineData(BasePlateCalculator.ColumnIH)]
    [InlineData(BasePlateCalculator.ColumnRhs)]
    [InlineData(BasePlateCalculator.ColumnChs)]
    public void A_Thicker_Plate_Never_Lowers_The_Bearing_Resistance(string column)
    {
        var baseline = Column(Axial(500), column);

        Gen.Double[10, 90].Sample(t =>
        {
            var thinner = CalcRunner.Run(Plate, baseline.With(CalcInputValue.Quantity("t_p", t, "mm"))).OutputSi("N_j_Rd");
            var thicker = CalcRunner.Run(Plate, baseline.With(CalcInputValue.Quantity("t_p", t * 1.1, "mm"))).OutputSi("N_j_Rd");
            return thicker >= thinner - 1e-6;
        }, iter: 60);
    }

    [Theory]
    [InlineData(BasePlateCalculator.ColumnIH, 1200.0)]
    [InlineData(BasePlateCalculator.ColumnRhs, 900.0)]
    [InlineData(BasePlateCalculator.ColumnChs, 800.0)]
    public void The_Bearing_Thickness_Gives_A_Utilisation_Of_One(string column, double nEd)
    {
        var first = CalcRunner.Run(Plate, Column(Axial(nEd), column));
        var tReq = first.OutputSi("t_req_N") * 1e3;
        Assert.InRange(tReq, 10.0, 100.0);

        var again = CalcRunner.Run(Plate, Column(Axial(nEd), column).With(CalcInputValue.Quantity("t_p", tReq, "mm")));
        Assert.Equal(1.0, again.OutputSi("u_N"), 6);
    }

    [Fact]
    public void The_Required_Thickness_Satisfies_Bearing_Compression_And_Tension_Together()
    {
        // The default case at its 20 mm plate passes, but a 10 mm plate fails the compression and tension T-stubs, not bearing.
        var thin = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("t_p", 10, "mm")));
        Assert.True(thin.OutputSi("u_C") > 1 || thin.OutputSi("u_T") > 1);
        Assert.True(thin.OutputSi("u_N") < 1);

        var tReq = CalcRunner.Run(Plate, Defaults()).OutputSi("t_req") * 1e3;
        Assert.InRange(tReq, 10.01, 100.0);
        Assert.True(tReq > CalcRunner.Run(Plate, Defaults()).OutputSi("t_req_N") * 1e3, "the T-stubs govern, not bearing alone");

        var at = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("t_p", tReq, "mm")));
        var worst = new[] { at.OutputSi("u_N"), at.OutputSi("u_C"), at.OutputSi("u_T") }.Max();
        Assert.Equal(1.0, worst, 6);

        var below = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("t_p", tReq * 0.99, "mm")));
        Assert.True(new[] { below.OutputSi("u_N"), below.OutputSi("u_C"), below.OutputSi("u_T") }.Max() > 1);
    }

    [Fact]
    public void When_The_Bolts_Govern_No_Thickness_Helps_And_The_Sheet_Says_So()
    {
        // M = 150 kN·m puts 396 kN on the row, above 2 × 141 kN.
        var r = CalcRunner.Run(Plate, Loads(300, 150));
        Assert.StartsWith("bolts govern", Text(r, "t_req"), StringComparison.Ordinal);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Base plate tension", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void A_Plate_That_Cannot_Carry_The_Force_Reports_More_Than_100_Mm()
    {
        var r = CalcRunner.Run(Plate, Axial(3000));
        Assert.Equal("> 100 mm", Text(r, "t_req_N"));
        Assert.Equal("> 100 mm", Text(r, "t_req"));
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Concrete bearing", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void No_Moment_Means_No_Bolt_Tension_And_A_Moment_Does()
    {
        var none = CalcRunner.Run(Plate, Axial(500));
        Assert.Equal(0, none.OutputSi("T_Ed"), 12);
        Assert.Equal(0, none.OutputSi("F_t_Ed"), 12);
        Assert.Equal(0, none.OutputSi("u_T"), 12);

        var moment = CalcRunner.Run(Plate, Loads(500, 250));
        Assert.True(moment.OutputSi("F_t_Ed") > 0);
    }

    [Fact]
    public void Regime_One_Takes_Tension_At_One_Row_With_The_Section_Lever_Arm()
    {
        var r = CalcRunner.Run(Plate, Loads(300, 100));
        Assert.StartsWith("tension at one bolt row", Text(r, "regime"), StringComparison.Ordinal);
        Assert.Equal(121.5, r.OutputSi("z_C") * 1e3, 9);                       // (h − t_f)/2
        Assert.Equal(286.5, r.OutputSi("z") * 1e3, 9);
        Assert.Equal((100.0 - 300 * 0.1215) / 0.2865, Kn(r, "T_Ed"), 6);
        Assert.Equal(Kn(r, "T_Ed") + 300, Kn(r, "C_Ed"), 6);                    // vertical equilibrium
        Assert.Equal(Kn(r, "T_Ed") / 2, Kn(r, "F_t_Ed"), 9);

        // Bolts outside the flange: with x_b = 178.5 the old symmetric z/2 would understate this.
        var wide = CalcRunner.Run(Plate, Loads(300, 120).With(CalcInputValue.Quantity("x_b", 178.5, "mm")));
        Assert.Equal(278.5, Kn(wide, "T_Ed"), 9);
        Assert.Equal(139.25, Kn(wide, "F_t_Ed"), 9);
    }

    [Fact]
    public void Regime_Two_Has_Both_Flanges_In_Compression_And_No_Bolt_Tension()
    {
        var r = CalcRunner.Run(Plate, Loads(600, 40));
        Assert.StartsWith("both sides in compression", Text(r, "regime"), StringComparison.Ordinal);
        Assert.Equal(600.0 / 2 + 40.0 / (2 * 0.1215), Kn(r, "C_Ed"), 6);
        Assert.Equal(600.0 / 2 - 40.0 / (2 * 0.1215), Kn(r, "C_Ed_2"), 6);
        Assert.Equal(600.0, Kn(r, "C_Ed") + Kn(r, "C_Ed_2"), 9);
        Assert.Equal(0, Kn(r, "F_t_Ed"), 12);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Compression flange", StringComparison.Ordinal));
    }

    [Fact]
    public void Regime_Three_Has_Both_Rows_In_Tension_With_The_Bolt_Lever_Arm()
    {
        var pure = CalcRunner.Run(Plate, Loads(-500, 0));
        Assert.StartsWith("both bolt rows in tension", Text(pure, "regime"), StringComparison.Ordinal);
        Assert.Equal(250, Kn(pure, "T_Ed"), 9);
        Assert.Equal(250, Kn(pure, "T_Ed_2"), 9);
        Assert.Equal(125, Kn(pure, "F_t_Ed"), 9);                                // the old symmetric formula gave 101.25 with z_C 121.5

        var r = CalcRunner.Run(Plate, Loads(-300, 20));
        Assert.Equal(150 + 20.0 / (2 * 0.165), Kn(r, "T_Ed"), 6);
        Assert.Equal(150 - 20.0 / (2 * 0.165), Kn(r, "T_Ed_2"), 6);
        Assert.Equal(300, Kn(r, "T_Ed") + Kn(r, "T_Ed_2"), 9);
        Assert.Equal(0, Kn(r, "C_Ed"), 12);
        Assert.DoesNotContain(r.Checks, c => c.Name.StartsWith("Compression flange", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Statics_Balance_The_Axial_Force_And_The_Moment_In_Every_Regime()
    {
        Gen.Select(Gen.Double[-1500, 2500], Gen.Double[0, 600], Gen.Double[50, 400], Gen.Double[80, 400]).Sample((n, m, zc, xb) =>
        {
            var f = BasePlateCalculator.Statics(n, m, zc, xb);
            var vertical = f.C + f.C2 - f.T - f.T2;
            var moment = ((f.C - f.C2) * zc + (f.T - f.T2) * xb) / 1e3;
            return Math.Abs(vertical - n) < 1e-9 && Math.Abs(moment - m) < 1e-9 && f.C >= -1e-12 && f.C2 >= -1e-12 && f.T >= -1e-12 && f.T2 >= -1e-12;
        }, iter: 400);
    }

    [Fact]
    public void A_Chs_Uses_The_Half_Annulus_Centroid_As_Its_Lever_Arm()
    {
        var r = CalcRunner.Run(Plate, Column(Loads(200, 60), BasePlateCalculator.ColumnChs).With(CalcInputValue.Quantity("B", 350, "mm")).With(CalcInputValue.Quantity("L", 350, "mm")).With(CalcInputValue.Quantity("x_b", 140, "mm")).With(CalcInputValue.Quantity("m", 25, "mm")));
        var outer = 110 + r.OutputSi("c") * 1e3;
        var inner = 100 - r.OutputSi("c") * 1e3;
        Assert.Equal(4 * (Math.Pow(outer, 3) - Math.Pow(inner, 3)) / (3 * Math.PI * (outer * outer - inner * inner)), r.OutputSi("z_C") * 1e3, 6);
        Assert.DoesNotContain(r.Checks, c => c.Name.Contains("flange", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Compression-side half annulus", StringComparison.Ordinal));
    }

    private static CalcInputSet ChsMoment(double tp, double alpha) =>
        Column(Loads(100, 150), BasePlateCalculator.ColumnChs)
            .With(CalcInputValue.Quantity("D", 219.1, "mm")).With(CalcInputValue.Quantity("t", 8, "mm"))
            .With(CalcInputValue.Quantity("B", 500, "mm")).With(CalcInputValue.Quantity("L", 500, "mm"))
            .With(CalcInputValue.Quantity("x_b", 200, "mm")).With(CalcInputValue.Quantity("m", 25, "mm"))
            .With(CalcInputValue.Quantity("l_eff", 400, "mm")).With(CalcInputValue.Quantity("F_t_Rd", 250, "kN"))
            .With(CalcInputValue.Of("alpha", alpha)).With(CalcInputValue.Quantity("t_p", tp, "mm"));

    [Theory]
    [InlineData(60.0, 1.0, 29.24365869)]
    [InlineData(25.0, 1.0, 29.24365869)]
    [InlineData(60.0, 1.5, 33.64871534)]
    [InlineData(40.0, 1.5, 33.64871534)]
    public void A_Chs_Required_Thickness_Does_Not_Depend_On_The_Entered_Thickness_And_Passes_Its_Own_Checks(double enteredTp, double alpha, double expected)
    {
        // P1-struct.base-plate-01: z_C, C_Ed and T_Ed move with t_p, so t_req must re-evaluate them at every trial thickness.
        // Independent check (python3): bisection over t with the half-annulus centroid, C_Ed and T_Ed re-evaluated at each t.
        // The entered-thickness search gave 27.18 mm (alpha 1) and 21.67 mm (alpha 1.5) at t_p 60, both failing their own checks.
        var r = CalcRunner.Run(Plate, ChsMoment(enteredTp, alpha));
        var tReq = r.OutputSi("t_req") * 1e3;
        Assert.Equal(expected, tReq, 6);

        var again = CalcRunner.Run(Plate, ChsMoment(tReq + 1e-6, alpha));

        foreach (var u in new[] { "u_N", "u_C", "u_T" })
            Assert.True(again.OutputSi(u) <= 1 + 1e-6, $"{u} = {again.OutputSi(u)} at t_p = t_req");

        var below = CalcRunner.Run(Plate, ChsMoment(tReq - 0.01, alpha));
        Assert.True(new[] { below.OutputSi("u_N"), below.OutputSi("u_C"), below.OutputSi("u_T") }.Max() > 1);
    }

    [Fact]
    public void A_Lever_Arm_Can_Be_Entered_And_Overrides_The_Section()
    {
        var r = CalcRunner.Run(Plate, Loads(300, 100).With(CalcInputValue.Quantity("z_C_in", 150, "mm")));
        Assert.Equal(150, r.OutputSi("z_C") * 1e3, 9);
        Assert.Equal((100.0 - 300 * 0.15) / 0.315, Kn(r, "T_Ed"), 6);
    }

    [Fact]
    public void Anchor_Shear_Is_The_Smaller_Of_Bearing_And_The_Alpha_Bc_Shear()
    {
        var r = CalcRunner.Run(Plate, Loads(300, 100, 80));
        Assert.Equal(0.44 - 0.0003 * 640, r.OutputSi("alpha_bc"), 12);
        var f2 = (0.44 - 0.0003 * 640) * 800 * 244.79 / 1.25 / 1e3;
        Assert.Equal(f2, Kn(r, "F_2_vb_Rd"), 6);
        Assert.Equal(f2, Kn(r, "F_vb_Rd"), 6);
        Assert.Equal(0.2 * 300 + 4 * f2, Kn(r, "V_Rd"), 6);

        var bearing = CalcRunner.Run(Plate, Loads(300, 100, 80).With(CalcInputValue.Quantity("F_1_vb_Rd", 20, "kN")));
        Assert.Equal(20, Kn(bearing, "F_vb_Rd"), 9);
    }

    [Fact]
    public void The_Shear_Per_Anchor_Is_What_Friction_Does_Not_Carry()
    {
        Assert.Equal((80 - 0.2 * 300) / 4, Kn(CalcRunner.Run(Plate, Loads(300, 100, 80)), "V_b_Ed"), 9);
        Assert.Equal(0, Kn(CalcRunner.Run(Plate, Loads(300, 100, 40)), "V_b_Ed"), 12);
        Assert.Equal(80.0 / 4, Kn(CalcRunner.Run(Plate, Loads(-100, 0, 80)), "V_b_Ed"), 9);
        Assert.Contains(Plate.Template.OutputPorts!, p => p.Symbol == "V_b_Ed" && p.Unit == "kN");
    }

    [Fact]
    public void A_Missing_Profis_Reference_Warns_But_Does_Not_Fail()
    {
        var r = CalcRunner.Run(Plate, Defaults());
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Warnings, w => w.RuleId == "anchorage");
        Assert.DoesNotContain(r.Checks, c => c.Name.StartsWith("Anchorage", StringComparison.Ordinal));
        Assert.True(r.Passed);
    }

    [Fact]
    public void A_Typed_Profis_Reference_Is_Not_A_Passed_Check()
    {
        // Green G-A-03: any non-empty text used to pass "Anchorage designed externally" (the issued pack showed 6 of 6 PASS on a placeholder file name).
        var r = CalcRunner.Run(Plate, Defaults().With(new CalcInputValue("profis_ref", Text: "PROFIS-2026-014 rev A")).With(CalcInputValue.Choice("alpha_basis", "A_c1/A_c0 = 2.25")));
        Assert.DoesNotContain(r.Checks, c => c.Name.StartsWith("Anchorage", StringComparison.Ordinal));
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "anchorage");
        Assert.Contains("typed text", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "alpha_basis");

        var manual = CalcRunner.Run(Plate, Defaults().With(new CalcInputValue("profis_ref", Text: "PROFIS-2026-014 rev A", Provenance: CalcProvenance.Specification("PROFIS"))));
        Assert.DoesNotContain(manual.Checks, c => c.Name.StartsWith("Anchorage", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Compression_Lever_Arm_Follows_The_Strip_When_The_Outstand_Is_Cut_At_The_Plate_Edge()
    {
        // Green G-A-10: h = 300, t_f = 17, L = 340 (only 20 mm of plate outside the flange), c = 50.87: z_C = 141.5 − (50.87 − 20)/2 = 126.063 mm and
        // C_Ed = 513.64 kN; the uncut centroid 141.5 gave 487.77 kN, 5 % low (python3).
        var r = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("h", 300, "mm")).With(CalcInputValue.Quantity("b", 300, "mm")).With(CalcInputValue.Quantity("L", 340, "mm")).With(CalcInputValue.Quantity("m", 12, "mm")).With(CalcInputValue.Quantity("N_Ed", 300, "kN")).With(CalcInputValue.Quantity("M_Ed", 100, "kN*m")));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(126.06264904654, r.OutputSi("z_C") * 1e3, 6);
        Assert.Equal(513.63512456761, Kn(r, "C_Ed"), 6);

        // Uncut (a long plate): the centroid is the plain (h − t_f)/2.
        var uncut = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("h", 300, "mm")).With(CalcInputValue.Quantity("b", 300, "mm")).With(CalcInputValue.Quantity("L", 600, "mm")).With(CalcInputValue.Quantity("m", 12, "mm")));
        Assert.Equal(141.5, uncut.OutputSi("z_C") * 1e3, 9);
    }

    [Fact]
    public void The_Tension_T_Stub_Geometry_And_The_Concentration_Factor_Have_No_Silent_Unsafe_Defaults()
    {
        // Green G-A-11: l_eff = 200 mm, m = 28 mm and α = 1.5 were invented defaults (α alone raises f_jd by 50 %).
        var spec = Plate.Template.Inputs;
        Assert.Null(spec.Single(i => i.Symbol == "l_eff").Default);
        Assert.Null(spec.Single(i => i.Symbol == "m").Default);
        Assert.False(spec.Single(i => i.Symbol == "l_eff").IsOptional);
        Assert.False(spec.Single(i => i.Symbol == "m").IsOptional);
        Assert.Equal(1.0, spec.Single(i => i.Symbol == "alpha").Default);

        var without = new CalcInputSet(Defaults().Values.Where(v => v.Symbol is not ("l_eff" or "m")));
        var r = CalcRunner.Run(Plate, without);
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.Message.StartsWith("l_eff", StringComparison.Ordinal) || b.Message.StartsWith("m", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Axial_Force_Sign_Is_Stated_As_Opposite_To_The_Member_Check()
    {
        // Black B-A-22: S3 takes N_Ed tension positive, S6 compression positive; the same symbol must say so on both sheets.
        var s6 = Plate.Template.Inputs.Single(i => i.Symbol == "N_Ed").Description;
        var s3 = new MemberChecksCalculator().Template.Inputs.Single(i => i.Symbol == "N_Ed").Description;
        Assert.Contains("COMPRESSION positive", s6, StringComparison.Ordinal);
        Assert.Contains("opposite", s6, StringComparison.Ordinal);
        Assert.Contains("TENSION positive", s3, StringComparison.Ordinal);
        Assert.Contains("opposite", s3, StringComparison.Ordinal);
        Assert.Contains(Plate.Template.Assumptions, a => a.Statement.Contains("R1_y", StringComparison.Ordinal) && a.Statement.Contains("N_e1", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Concentration_Factor_Above_One_Needs_Its_Basis_Stated()
    {
        Assert.Contains(CalcRunner.Run(Plate, Defaults()).Warnings, w => w.RuleId == "alpha_basis");
        Assert.DoesNotContain(CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Of("alpha", 1))).Warnings, w => w.RuleId == "alpha_basis");
    }

    [Fact]
    public void A_Column_In_Tension_Has_No_Friction_Resistance()
    {
        var r = CalcRunner.Run(Plate, Axial(-200));
        Assert.Equal(0, r.OutputSi("F_f_Rd"), 12);
        Assert.Equal(4 * Kn(r, "F_vb_Rd"), Kn(r, "V_Rd"), 9);

        // Net tension of 200 kN shared by the two bolt rows: 100 kN a row, 50 kN a bolt.
        Assert.Equal(100.0, Kn(r, "T_Ed"), 9);
        Assert.Equal(50.0, Kn(r, "F_t_Ed"), 9);
    }

    [Fact]
    public void Compression_Gives_Friction_Of_The_Coefficient_Times_The_Force()
    {
        var r = CalcRunner.Run(Plate, Axial(800).With(CalcInputValue.Quantity("V_Ed", 150, "kN")));
        Assert.Equal(0.2 * 800, Kn(r, "F_f_Rd"), 9);
        Assert.Equal(150 / (0.2 * 800 + 4 * Kn(r, "F_vb_Rd")), r.OutputSi("u_V"), 9);
    }

    [Fact]
    public void Prying_Is_Off_By_Default_Is_Conservative_And_Warns_That_Q_Is_Not_Included()
    {
        Assert.False(Defaults().Find("prying")!.Flag);
        var off = CalcRunner.Run(Plate, Defaults());
        Assert.DoesNotContain(off.Warnings, w => w.RuleId == "prying");
        Assert.Equal(2 * 5.5e3 / 28.0, Kn(off, "T_Rd_1"), 6);
        Assert.Null(off.FindOutput("T_Rd_2"));

        var on = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Switch("prying", true)));
        Assert.Contains(on.Warnings, w => w.RuleId == "prying" && w.Message.Contains("not included", StringComparison.Ordinal));
        Assert.Equal(2 * Kn(off, "T_Rd_1"), Kn(on, "T_Rd_1"), 6);
        Assert.True(Kn(on, "T_Rd_2") > 0);
    }

    [Fact]
    public void Prying_Needs_The_Edge_Distance_And_Without_It_The_Edge_Distance_Is_Ignored()
    {
        var noEdge = new CalcInputSet(Defaults().Values.Where(v => v.Symbol != "e_min"));
        Assert.False(CalcRunner.Run(Plate, noEdge).IsBlocked);
        Assert.True(CalcRunner.Run(Plate, noEdge.With(CalcInputValue.Switch("prying", true))).IsBlocked);
        Assert.True(CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Switch("prying", true)).With(CalcInputValue.Quantity("e_min", 36, "mm"))).IsBlocked);
    }

    [Fact]
    public void A_Thin_Plate_Is_Governed_By_Mode_One_And_A_Thick_One_By_The_Bolts()
    {
        var thin = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("t_p", 10, "mm")));
        Assert.StartsWith("mode 1", Text(thin, "T_mode"), StringComparison.Ordinal);
        Assert.Equal(thin.OutputSi("T_Rd_1"), thin.OutputSi("T_Rd"), 6);

        var thick = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("t_p", 30, "mm")));
        Assert.StartsWith("mode 3", Text(thick, "T_mode"), StringComparison.Ordinal);
        Assert.Equal(2 * 141.0, Kn(thick, "T_Rd"), 9);
    }

    [Fact]
    public void Overlapping_Strips_Use_The_Full_Rectangle_And_Say_So()
    {
        var small = Defaults().With(CalcInputValue.Quantity("h", 150, "mm")).With(CalcInputValue.Quantity("b", 150, "mm")).With(CalcInputValue.Quantity("t_f", 10, "mm")).With(CalcInputValue.Quantity("t_w", 6, "mm")).With(CalcInputValue.Quantity("B", 500, "mm")).With(CalcInputValue.Quantity("L", 500, "mm")).With(CalcInputValue.Quantity("t_p", 40, "mm")).With(CalcInputValue.Quantity("x_b", 200, "mm"));
        var r = CalcRunner.Run(Plate, small);
        var c = r.OutputSi("c") * 1e3;
        Assert.Equal((150 + 2 * c) * (150 + 2 * c), r.OutputSi("A_eff") * 1e6, 3);
        Assert.Contains(r.RunAssumptions, a => a.Statement.Contains("overlap", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Bearing_Area_Never_Exceeds_The_Plate()
    {
        Gen.Double[10, 100].Sample(t =>
        {
            var r = CalcRunner.Run(Plate, Axial(100).With(CalcInputValue.Quantity("t_p", t, "mm")).With(CalcInputValue.Quantity("B", 300, "mm")).With(CalcInputValue.Quantity("L", 320, "mm")).With(CalcInputValue.Quantity("x_b", 155, "mm")).With(CalcInputValue.Quantity("m", 20, "mm")));
            return r.OutputSi("A_eff") * 1e6 <= 300 * 320 + 1e-6;
        }, iter: 50);
    }

    [Fact]
    public void Inconsistent_Geometry_Blocks()
    {
        var outside = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("B", 200, "mm")));
        Assert.Equal("plate", outside.Blocks[0].RuleId);

        var bolts = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Of("n_t", 3)).With(CalcInputValue.Of("n_b", 4)));
        Assert.Equal("bolts", bolts.Blocks[0].RuleId);

        var fraction = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Of("n_t", 2.5)).With(CalcInputValue.Of("n_b", 6)));
        Assert.Equal("bolts", fraction.Blocks[0].RuleId);

        // Bolt row under the flange (h/2 = 130), beyond the plate edge (L/2 = 200), m wider than the room (35 mm), e_min beyond the edge.
        foreach (var (symbol, value) in new[] { ("x_b", 125.0), ("x_b", 200.0), ("x_b", 130.0), ("m", 36.0) })
        {
            var r = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity(symbol, value, "mm")));
            Assert.True(r.IsBlocked, $"{symbol} = {value}");
            Assert.Equal("x_b", r.Blocks[0].RuleId);
            Assert.StartsWith(symbol + ":", r.Blocks[0].Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Grout_Conditions_For_Beta_J_Are_Checked_When_Entered_And_Assumed_Otherwise()
    {
        var assumed = CalcRunner.Run(Plate, Defaults());
        Assert.Contains(assumed.RunAssumptions, a => a.Statement.StartsWith("β_j = 2/3 assumes", StringComparison.Ordinal));

        var good = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("f_ck_g", 30, "MPa")).With(CalcInputValue.Quantity("t_g", 40, "mm")));
        Assert.DoesNotContain(good.Warnings, w => w.RuleId == "grout");
        Assert.DoesNotContain(good.RunAssumptions, a => a.Statement.StartsWith("β_j = 2/3 assumes", StringComparison.Ordinal));

        var weak = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("f_ck", 50, "MPa")).With(CalcInputValue.Quantity("f_ck_g", 8, "MPa")).With(CalcInputValue.Quantity("t_g", 100, "mm")));
        var warning = Assert.Single(weak.Warnings, w => w.RuleId == "grout");
        Assert.Contains("strength", warning.Message, StringComparison.Ordinal);
        Assert.Contains("thickness", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_The_Dimensions_Of_The_Chosen_Section_Are_Needed()
    {
        var ih = new CalcInputSet(Defaults().Values.Where(v => v.Symbol is not ("t" or "D")));
        var r = CalcRunner.Run(Plate, ih);
        Assert.False(r.IsBlocked);
        Assert.DoesNotContain(r.Warnings, w => w.Message.StartsWith("t ", StringComparison.Ordinal) || w.Message.StartsWith("D ", StringComparison.Ordinal));

        var rhs = new CalcInputSet(Defaults().Values.Where(v => v.Symbol != "t")).With(CalcInputValue.Choice("column", BasePlateCalculator.ColumnRhs));
        var blocked = CalcRunner.Run(Plate, rhs);
        Assert.True(blocked.IsBlocked);
        Assert.StartsWith("t:", blocked.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Governing_Utilisation_Is_The_Largest_Check_And_Every_Port_Is_An_Output()
    {
        var r = CalcRunner.Run(Plate, Defaults());
        Assert.Equal(r.Checks.Where(c => c.Utilisation < double.PositiveInfinity).Max(c => c.Utilisation), r.OutputSi("u_max"), 9);
        Assert.Contains(Plate.Template.OutputPorts!, p => p.Symbol == "F_t_Ed" && p.Unit == "kN");
        Assert.All(Plate.Template.OutputPorts!, p => Assert.NotNull(r.FindOutput(p.Symbol)));
    }

    private static CalcInputSet Q2(CalcInputSet set, string symbol, double value, string unit) => set.With(CalcInputValue.Quantity(symbol, value, unit));

    [Fact]
    public void The_Anchorage_Result_Is_Not_Assessed_Unless_The_Report_Utilisation_Is_Entered()
    {
        // chain.steel-support-04 (T4): an attached Evidence placeholder passed the anchorage check with u 0 while no concrete failure mode of the anchors was computed; u_max covers the steel parts only.
        // Now the sheet warns that the anchorage is not assessed until the governing utilisation of the PROFIS report is entered, and then checks it and counts it in u_max.
        var none = CalcRunner.Run(Plate, Defaults());
        var warning = Assert.Single(none.Warnings, w => w.RuleId == "anchorage-result");
        Assert.Contains("u_max", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(none.Checks, c => c.Name.StartsWith("Anchorage (PROFIS", StringComparison.Ordinal));

        var pass = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Of("u_profis", 0.8)));
        Assert.DoesNotContain(pass.Warnings, w => w.RuleId == "anchorage-result");
        Assert.Contains(pass.Checks, c => c.Name.StartsWith("Anchorage (PROFIS", StringComparison.Ordinal) && c.Passed);

        var fail = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Of("u_profis", 1.25)));
        Assert.Contains(fail.Checks, c => c.Name.StartsWith("Anchorage (PROFIS", StringComparison.Ordinal) && !c.Passed);
        Assert.Equal(1.25, fail.OutputSi("u_max"), 9);
        Assert.False(fail.Passed);
    }

    [Fact]
    public void An_Entered_Lever_Arm_Beyond_The_Plate_Half_Length_Is_Blocked()
    {
        // struct.base-plate-02: z_C_in 1000 mm put the compression resultant outside the plate and gave T_Ed 128.76 kN (u_T 0.457, pass) against 370.24 kN
        // (u_T 1.313, FAIL) for the section's own z_C = 121.5 mm. python3: T = (M + N x_b)/(z_C + x_b) - N with N -50, M 100, x_b 165.
        var set = Loads(-50, 100);
        var blank = CalcRunner.Run(Plate, set);
        Assert.Equal(370.2, Kn(blank, "T_Ed"), 1);

        var r = CalcRunner.Run(Plate, Q2(set, "z_C_in", 1000, "mm"));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.RuleId == "z_C" && b.Message.StartsWith("z_C_in:", StringComparison.Ordinal));

        // Just inside the half-length is accepted; the plate here is L = 400, so 199 mm is the last whole millimetre.
        Assert.False(CalcRunner.Run(Plate, Q2(set, "z_C_in", 199, "mm")).IsBlocked);
    }

    [Fact]
    public void An_Entered_Lever_Arm_Far_From_The_Section_Value_Warns_That_It_Is_Not_The_Lever_Arm_Z()
    {
        // The user who enters z (286.5 mm) instead of z_C (121.5 mm) got a smaller T_Ed with no notice.
        var r = CalcRunner.Run(Plate, Q2(Loads(300, 100), "z_C_in", 190, "mm"));
        Assert.False(r.IsBlocked);
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "z_C_entered");
        Assert.Contains("121.5", warning.Message, StringComparison.Ordinal);

        Assert.DoesNotContain(CalcRunner.Run(Plate, Q2(Loads(300, 100), "z_C_in", 150, "mm")).Warnings, w => w.RuleId == "z_C_entered");
    }

    [Fact]
    public void An_Anchor_Bolt_Strength_Above_800_MPa_Is_Blocked_Unless_The_Anchor_Design_Is_Entered()
    {
        // struct.base-plate-03 (and its verification): f_ub 1000 with f_yb capped at 640 gave alpha_bc 0.248 and u_V 0.983 (pass) with only a warning; at the f_yb of 900 MPa a
        // class 10.9 bolt has, the same formula gives 0.17 and u_V 1.29 (FAIL). python3: 0.44 - 0.0003*900 = 0.17; V_Rd = 0.2*300 + 4*0.17*1000*244.79/1.25/1e3 = 193.2 kN, u_V = 250/193.2 = 1.294.
        // alpha_bc is not defined above 800 MPa, so the sheet is blocked; entering the anchor design's governing utilisation (u_profis) lets it run with a warning that u_V is not reliable.
        var set = Q2(Q2(Loads(300, 100, 250), "f_ub", 1000, "MPa"), "f_yb", 640, "MPa");
        var r = CalcRunner.Run(Plate, set);
        Assert.True(r.IsBlocked);
        var block = Assert.Single(r.Blocks, b => b.RuleId == "alpha_bc");
        Assert.Contains("not defined above 800 MPa", block.Message, StringComparison.Ordinal);
        Assert.Contains("u_profis", block.Message, StringComparison.Ordinal);

        var withReport = CalcRunner.Run(Plate, set.With(CalcInputValue.Of("u_profis", 0.9)));
        Assert.False(withReport.IsBlocked);
        Assert.Equal(0.248, withReport.OutputSi("alpha_bc"), 9);
        Assert.Single(withReport.Warnings, w => w.RuleId == "alpha_bc_profis" && w.Message.Contains("640", StringComparison.Ordinal));

        var atLimit = CalcRunner.Run(Plate, Q2(Loads(300, 100, 250), "f_ub", 800, "MPa"));
        Assert.False(atLimit.IsBlocked);
        Assert.DoesNotContain(atLimit.Warnings, w => w.RuleId is "alpha_bc" or "alpha_bc_profis");
    }

    [Fact]
    public void A_Plate_Thicker_Than_16_Mm_Warns_That_One_Yield_Strength_Is_Used_At_Every_Thickness()
    {
        // struct.base-plate-R1: S275 is 275 MPa to 16 mm, 265 MPa above it and 255 MPa above 40 mm (EN 10025-2); the sheet used the entered 275 MPa at t_p 50 mm
        // (M_pl,Rd 6.875 against 6.375 kN.m, 7.8 % high). python3: 0.25 * 40 mm * (50 mm)^2 * 275 MPa = 6.875 kN.m; at 255 MPa 6.375 kN.m.
        var thick = CalcRunner.Run(Plate, Q2(Q2(Defaults(), "t_p", 50, "mm"), "l_eff", 40, "mm"));
        Assert.Equal(6.875, thick.OutputSi("M_pl_Rd") / 1e3, 6);
        var warning = Assert.Single(thick.Warnings, w => w.RuleId == "f_y_thickness");
        Assert.Contains("50", warning.Message, StringComparison.Ordinal);

        var thin = CalcRunner.Run(Plate, Q2(Loads(100, 0), "t_p", 12, "mm"));
        Assert.DoesNotContain(thin.Warnings, w => w.RuleId == "f_y_thickness");
    }

    [Fact]
    public void The_Thickness_Warning_Names_Only_The_Thicknesses_Above_16_Mm()
    {
        // struct.base-plate-R1 (verification): steel-support S6 printed "t_p = 25 mm and t_req = 15.423 mm pass the first step" although t_req is below 16 mm.
        var r = CalcRunner.Run(Plate, Q2(Loads(100, 0), "t_p", 25, "mm"));
        var tReq = r.OutputSi("t_req") * 1e3;
        Assert.InRange(tReq, 10, 16);

        var warning = Assert.Single(r.Warnings, w => w.RuleId == "f_y_thickness");
        Assert.Contains("t_p = 25 mm", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("t_req", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Anchorage_Check_Cites_The_Report_And_EN_1992_4_Not_The_Steel_Anchor_Bolt_Clause()
    {
        // chain.steel-support-04 (verification): the anchorage is designed to EN 1992-4 or taken from the PROFIS report, not to EN 1993-1-8 cl. 6.2.6.12 (steel anchor bolts in tension).
        var r = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Of("u_profis", 0.8)));
        var check = Assert.Single(r.Checks, c => c.Name.StartsWith("Anchorage (PROFIS", StringComparison.Ordinal));
        Assert.Equal("PROFIS report (EN 1992-4)", check.Clause);
        Assert.Equal("EN 1993-1-8 cl. 6.2.6.12", r.Checks.Single(c => c.Name == "Holding-down bolt tension").Clause);
    }

    private static CalcInputSet E1(string column, double tp) => column == "ih"
        ? Q2(Q2(Q2(Q2(Q2(Loads(500, 100), "B", 360, "mm"), "L", 360, "mm"), "t_p", tp, "mm"), "m", 28, "mm"), "l_eff", 200, "mm").With(CalcInputValue.Of("alpha", 1.5))
        : Q2(Q2(Q2(Q2(Q2(Q2(Q2(Column(Loads(300, 60), BasePlateCalculator.ColumnRhs), "B", 230, "mm"), "L", 300, "mm"), "t_p", tp, "mm"), "x_b", 140, "mm"), "m", 28, "mm"), "l_eff", 200, "mm"), "F_t_Rd", 141, "kN").With(CalcInputValue.Of("alpha", 1.5));

    [Fact]
    public void A_Required_Thickness_Passes_Its_Own_Checks_On_I_H_And_RHS_Columns_Whatever_Thickness_Is_Entered()
    {
        // struct.base-plate-E1: I/H 260x256x17x10 on a 360x360 plate, N 500, M 100, x_b 165, m 28, l_eff 200, alpha 1.5: t_p 20 gave t_req 22.84 mm and u_C 1.013 at 22.84.
        foreach (var column in new[] { "ih", "rhs" })
        {
            var tReqs = new List<double>();

            foreach (var tp in new[] { 20.0, 40.0, 60.0 })
                tReqs.Add(CalcRunner.Run(Plate, E1(column, tp)).OutputSi("t_req") * 1e3);

            Assert.All(tReqs, t => Assert.Equal(tReqs[0], t, 6));
            var again = CalcRunner.Run(Plate, E1(column, tReqs[0] + 1e-6));

            foreach (var u in new[] { "u_N", "u_C", "u_T" })
                Assert.True(again.OutputSi(u) <= 1 + 1e-6, $"{column}: {u} = {again.OutputSi(u)} at t_p = t_req");
        }
    }

    [Fact]
    public void A_Thicker_Plate_That_Raises_The_Compression_Utilisation_Because_The_Outstand_Is_Cut_Warns()
    {
        // struct.base-plate-E1: RHS 200x200x10 on a 230x300 plate, N 300, M 60: u_C is 0.998 at t_p 40 and 1.063 at 50, 1.136 at 60 (FAIL) because the strip's lever arm
        // shortens as c grows against the plate edge; t_req 32.79 mm is the thinnest passing plate, not a lower bound for every thicker one.
        var r40 = CalcRunner.Run(Plate, E1("rhs", 40));
        Assert.Equal(0.998, r40.OutputSi("u_C"), 3);
        Assert.Contains(r40.Warnings, w => w.RuleId == "outstand-cut");

        // Uncut outstand (a long, wide plate): u_C falls with t_p, so there is nothing to warn about.
        var uncut = Q2(Q2(E1("rhs", 40), "L", 700, "mm"), "B", 600, "mm");
        Assert.DoesNotContain(CalcRunner.Run(Plate, uncut).Warnings, w => w.RuleId == "outstand-cut");
    }
}
