using CsCheck;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class BasePlateTests
{
    private static readonly BasePlateCalculator Plate = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Plate.Template);

    private static CalcInputSet Axial(double nEd) => Defaults().With(CalcInputValue.Quantity("N_Ed", nEd, "kN")).With(CalcInputValue.Quantity("M_Ed", 0, "kN*m")).With(CalcInputValue.Quantity("V_Ed", 0, "kN"));

    private static CalcInputSet Column(CalcInputSet set, string column) => column switch
    {
        BasePlateCalculator.ColumnRhs => set.With(CalcInputValue.Choice("column", column)).With(CalcInputValue.Quantity("h", 200, "mm")).With(CalcInputValue.Quantity("b", 200, "mm")).With(CalcInputValue.Quantity("t", 10, "mm")),
        BasePlateCalculator.ColumnChs => set.With(CalcInputValue.Choice("column", column)).With(CalcInputValue.Quantity("D", 220, "mm")).With(CalcInputValue.Quantity("t", 10, "mm")),
        _ => set,
    };

    private static string Text(CalcRunResult r, string symbol) => r.FindOutput(symbol)?.Value is TextValue t ? t.Value : throw new InvalidOperationException($"{symbol} is not a text output");

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
    public void Inputs_Outside_Their_Range_Are_A_Hard_Fail(string symbol, double value, string unit)
    {
        var value2 = unit.Length > 0 ? CalcInputValue.Quantity(symbol, value, unit) : CalcInputValue.Of(symbol, value);
        var notices = CalcInputValidator.Validate(Plate.Template, Defaults().With(value2));
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.True(CalcRunner.Run(Plate, Defaults().With(value2)).IsBlocked);
    }

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
    public void The_Required_Thickness_Gives_A_Utilisation_Of_One(string column, double nEd)
    {
        var first = CalcRunner.Run(Plate, Column(Axial(nEd), column));
        var tReq = first.OutputSi("t_req") * 1e3;
        Assert.InRange(tReq, 10.0, 100.0);
        Assert.True(first.OutputSi("u_N") <= 1.0 || tReq > 20.0, "a 20 mm plate that is too thin has a utilisation above 1");

        var again = CalcRunner.Run(Plate, Column(Axial(nEd), column).With(CalcInputValue.Quantity("t_p", tReq, "mm")));
        Assert.Equal(1.0, again.OutputSi("u_N"), 6);
    }

    [Fact]
    public void A_Plate_That_Cannot_Carry_The_Force_Reports_More_Than_100_Mm()
    {
        var r = CalcRunner.Run(Plate, Axial(3000));
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

        var moment = CalcRunner.Run(Plate, Axial(500).With(CalcInputValue.Quantity("M_Ed", 250, "kN*m")));
        Assert.True(moment.OutputSi("F_t_Ed") > 0);
    }

    [Fact]
    public void The_Resultants_Balance_The_Axial_Force_And_The_Moment()
    {
        // Asymmetric arrangement: compression at 121.5 mm from the axis, bolt row 178.5 mm on the other side.
        var r = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("z_C", 121.5, "mm")));
        var c = r.OutputSi("C_Ed") / 1e3;
        var t = r.OutputSi("T_Ed") / 1e3;
        Assert.Equal(300.0, c - t, 6);                                       // vertical equilibrium
        Assert.Equal(120.0, (c * 121.5 + t * 178.5) / 1e3, 6);               // moment about the axis, kN·m
        Assert.Equal((120.0 - 300 * 0.1215) / 0.3, t, 6);
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
    public void A_Profis_Reference_Passes_The_Anchorage_Condition()
    {
        var r = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Choice("profis_ref", "PROFIS-2026-014 rev A")).With(CalcInputValue.Choice("alpha_basis", "A_c1/A_c0 = 2.25")));
        Assert.DoesNotContain(r.Warnings, w => w.RuleId is "anchorage" or "alpha_basis");
        var check = Assert.Single(r.Checks, c => c.Name.StartsWith("Anchorage designed externally", StringComparison.Ordinal));
        Assert.True(check.Passed);
        Assert.Contains("PROFIS-2026-014", check.DemandText, StringComparison.Ordinal);
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
        Assert.Equal(4 * 94.0, r.OutputSi("V_Rd") / 1e3, 9);

        // Net tension of 200 kN shared by the two bolt groups: 100 kN a side, 50 kN a bolt.
        Assert.Equal(100.0, r.OutputSi("T_Ed") / 1e3, 9);
        Assert.Equal(50.0, r.OutputSi("F_t_Ed") / 1e3, 9);
    }

    [Fact]
    public void Compression_Gives_Friction_Of_The_Coefficient_Times_The_Force()
    {
        var r = CalcRunner.Run(Plate, Axial(800).With(CalcInputValue.Quantity("V_Ed", 300, "kN")));
        Assert.Equal(0.2 * 800, r.OutputSi("F_f_Rd") / 1e3, 9);
        Assert.Equal(300 / (0.2 * 800 + 4 * 94.0), r.OutputSi("u_V"), 9);
    }

    [Fact]
    public void A_Thin_Plate_Is_Governed_By_Mode_One_And_A_Thick_One_By_The_Bolts()
    {
        var thin = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("t_p", 10, "mm")));
        Assert.StartsWith("mode 1", Text(thin, "T_mode"), StringComparison.Ordinal);
        Assert.Equal(thin.OutputSi("T_Rd_1"), thin.OutputSi("T_Rd"), 6);

        var thick = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("t_p", 30, "mm")));
        Assert.StartsWith("mode 3", Text(thick, "T_mode"), StringComparison.Ordinal);
        Assert.Equal(2 * 141.0, thick.OutputSi("T_Rd") / 1e3, 9);
    }

    [Fact]
    public void Without_Prying_Mode_One_Is_Half_The_Prying_Value()
    {
        var with = CalcRunner.Run(Plate, Defaults());
        var without = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Switch("prying", false)));
        Assert.Equal(with.OutputSi("T_Rd_1") / 2, without.OutputSi("T_Rd_1"), 6);
        Assert.True(without.OutputSi("T_Rd") <= with.OutputSi("T_Rd") + 1e-9);
    }

    [Fact]
    public void Overlapping_Strips_Use_The_Full_Rectangle_And_Say_So()
    {
        var small = Defaults().With(CalcInputValue.Quantity("h", 150, "mm")).With(CalcInputValue.Quantity("b", 150, "mm")).With(CalcInputValue.Quantity("t_f", 10, "mm")).With(CalcInputValue.Quantity("t_w", 6, "mm")).With(CalcInputValue.Quantity("B", 500, "mm")).With(CalcInputValue.Quantity("L", 500, "mm")).With(CalcInputValue.Quantity("t_p", 40, "mm"));
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
            var r = CalcRunner.Run(Plate, Axial(100).With(CalcInputValue.Quantity("t_p", t, "mm")).With(CalcInputValue.Quantity("B", 300, "mm")).With(CalcInputValue.Quantity("L", 320, "mm")));
            return r.OutputSi("A_eff") * 1e6 <= 300 * 320 + 1e-6;
        }, iter: 50);
    }

    [Fact]
    public void A_Column_Larger_Than_The_Plate_And_Bad_Bolt_Counts_Block()
    {
        var outside = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("B", 200, "mm")));
        Assert.True(outside.IsBlocked);
        Assert.Equal("plate", outside.Blocks[0].RuleId);

        var bolts = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Of("n_t", 3)).With(CalcInputValue.Of("n_b", 4)));
        Assert.True(bolts.IsBlocked);
        Assert.Equal("bolts", bolts.Blocks[0].RuleId);

        var fraction = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Of("n_t", 2.5)).With(CalcInputValue.Of("n_b", 6)));
        Assert.Equal("bolts", fraction.Blocks[0].RuleId);

        var zc = CalcRunner.Run(Plate, Defaults().With(CalcInputValue.Quantity("z_C", 320, "mm")));
        Assert.Equal("z_C", zc.Blocks[0].RuleId);
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
    public void The_Governing_Utilisation_Is_The_Largest_Check()
    {
        var r = CalcRunner.Run(Plate, Defaults());
        Assert.Equal(r.Checks.Where(c => c.Utilisation < double.PositiveInfinity).Max(c => c.Utilisation), r.OutputSi("u_max"), 9);
        Assert.Contains(Plate.Template.OutputPorts!, p => p.Symbol == "F_t_Ed" && p.Unit == "kN");
        Assert.All(Plate.Template.OutputPorts!, p => Assert.NotNull(r.FindOutput(p.Symbol)));
    }
}
