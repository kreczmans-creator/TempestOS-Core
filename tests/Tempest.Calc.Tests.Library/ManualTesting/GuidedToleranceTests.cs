using Tempest.Calc.Library.ManualTesting;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.ManualTesting;

/// <summary>The one rule the Guided test compares by: half a unit in the last digit of the less precise of the two, digits as written; text exactly.</summary>
public sealed class GuidedToleranceTests
{
    [Theory]
    [InlineData("4290.3", "h", "4290.3 h")]
    [InlineData("4290.3", "h", "4290.33 h")] // 0.03 is within half of 0.1
    [InlineData("4290.3", "h", "4290.25 h")] // exactly 0.05 away: on the boundary, in
    [InlineData("3.93", "kN", "3.9300 kN")] // trailing zeros do not make the form finer than its figures allow
    [InlineData("418.958", "N*m", "418.96 N*m")] // the form shows 5 figures; the expected has 6
    [InlineData("0.73529", "", "0.73529")]
    [InlineData("1.23457E-05", "m", "1.2346e-5 m")]
    [InlineData("-15", "K", "-15 K")]
    [InlineData("0", "", "0")]
    [InlineData("102.5", "degC", "102.5 °C")]
    public void A_Number_Within_Half_A_Unit_Of_The_Coarser_Last_Digit_Matches(string expected, string unit, string shown)
    {
        var result = GuidedTolerance.CompareShown("P", expected, unit, shown);
        Assert.True(result.IsMatch, result.Detail);
    }

    [Theory]
    [InlineData("4290.3", "h", "4290.4 h")] // one unit in the last digit
    [InlineData("4290.3", "h", "4290.2 h")]
    [InlineData("3.93", "kN", "3.94 kN")]
    [InlineData("418.958", "N*m", "418.97 N*m")] // 0.012 away; the form's last digit is 0.01, so half of it is 0.005
    [InlineData("0", "", "0.001")] // a bare zero is an exact zero, not "zero to the nearest whole number"
    [InlineData("0", "", "1")]
    [InlineData("1.23457E-05", "m", "1.2400e-5 m")]
    [InlineData("12", "mm", "-12 mm")]
    public void A_Number_Further_Than_That_Is_A_Mismatch(string expected, string unit, string shown)
    {
        var result = GuidedTolerance.CompareShown("P", expected, unit, shown);
        Assert.Equal(GuidedCompareOutcome.Mismatch, result.Outcome);
        Assert.StartsWith("P:", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Allowance_Is_Exact_Integer_Arithmetic_At_The_Boundary_Whatever_The_Scale()
    {
        // 0.1 + 0.2 style floating point errors must not decide a boundary: these are exactly half a unit apart.
        Assert.True(GuidedTolerance.CompareNumbers("x", "0.3", "0.35").IsMatch);
        Assert.True(GuidedTolerance.CompareNumbers("x", "0.3", "0.25").IsMatch);
        Assert.False(GuidedTolerance.CompareNumbers("x", "0.3", "0.351").IsMatch);
        Assert.True(GuidedTolerance.CompareNumbers("x", "1E+6", "1500000").IsMatch);
        Assert.False(GuidedTolerance.CompareNumbers("x", "1E+6", "1500001").IsMatch);
        Assert.True(GuidedTolerance.CompareNumbers("x", "1.5E-10", "1.54E-10").IsMatch);
        Assert.False(GuidedTolerance.CompareNumbers("x", "1.5E-10", "1.56E-10").IsMatch);
    }

    [Theory]
    [InlineData("PASS", "PASS", true)]
    [InlineData("PASS", "FAIL", false)]
    [InlineData("PASS", "pass", false)]
    [InlineData("ball", "ball", true)]
    [InlineData("ball", "roller", false)]
    public void A_Text_Must_Be_Identical(string expected, string shown, bool match)
    {
        var result = GuidedTolerance.CompareShown("check", expected, string.Empty, shown);
        Assert.Equal(match, result.IsMatch);
    }

    [Fact]
    public void A_Different_Unit_Is_Left_To_The_Tester_Not_Decided()
    {
        var result = GuidedTolerance.CompareShown("L", "12", "mm", "0.012 m");
        Assert.Equal(GuidedCompareOutcome.NotComparable, result.Outcome);
        Assert.Contains("'m'", result.Detail, StringComparison.Ordinal);
        Assert.Contains("'mm'", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_Shown_Is_Not_Comparable_And_A_Non_Number_Where_A_Number_Is_Expected_Differs()
    {
        Assert.Equal(GuidedCompareOutcome.NotComparable, GuidedTolerance.CompareShown("P", "3.93", "kN", null).Outcome);
        Assert.Equal(GuidedCompareOutcome.NotComparable, GuidedTolerance.CompareShown("P", "3.93", "kN", "  ").Outcome);
        Assert.Equal(GuidedCompareOutcome.Mismatch, GuidedTolerance.CompareShown("P", "3.93", "kN", "undefined kN").Outcome);
        Assert.Equal(GuidedCompareOutcome.Mismatch, GuidedTolerance.CompareNumbers("P", "3.93", "abc").Outcome);
    }

    [Theory]
    [InlineData("1E+999")]
    [InlineData("1e400")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345")]
    public void An_Absurd_Number_Is_Refused_Rather_Than_Parsed(string absurd)
    {
        var result = GuidedTolerance.CompareNumbers("x", "1", absurd);
        Assert.Equal(GuidedCompareOutcome.Mismatch, result.Outcome);
    }

    [Fact]
    public void The_Utilisation_Is_Compared_At_The_Digits_The_Form_Shows()
    {
        Assert.True(GuidedTolerance.CompareShown("u", "0.728", string.Empty, "0.728").IsMatch);
        Assert.True(GuidedTolerance.CompareShown("u", "12.345", string.Empty, "12.3").IsMatch);
        Assert.False(GuidedTolerance.CompareShown("u", "12.345", string.Empty, "12.4").IsMatch);

        // The governing banner prints two decimals (C2-22): 0.73 is a match for 0.7285 (half of 0.01 is 0.005) and 0.74 is not.
        Assert.True(GuidedTolerance.CompareShown("u", "0.7285", string.Empty, "0.73").IsMatch);
        Assert.False(GuidedTolerance.CompareShown("u", "0.7285", string.Empty, "0.74").IsMatch);
    }

    // ---------------------------------------------------------------- C2-22: the allowance is the digits shown, not an assumed five figures

    [Theory]
    [InlineData("150001", "h", "150000 h", false)] // a whole number is exact: 1 apart is not half a unit (an assumed five figures allowed 5)
    [InlineData("150000.4", "h", "150000 h", true)] // the form's last digit is 1, half of it covers 0.4
    [InlineData("3.1596", "cm^3", "3.160 cm³", true)] // four figures shown: 0.0004 is within half of 0.001 (an assumed five figures allowed 0.00005)
    [InlineData("3.1590", "cm^3", "3.160 cm³", false)] // 0.001 away is a whole unit of the last digit shown
    [InlineData("0.0034003", "N*m", "0.003400 N·m", true)] // six decimals shown: half of 0.000001 covers 0.0000003
    [InlineData("0.0034012", "N*m", "0.003400 N·m", false)]
    [InlineData("144.93", "MPa", "144.9 MPa", true)]
    [InlineData("144.96", "MPa", "144.9 MPa", false)]
    [InlineData("1.8626E-15", "MPa", "1.8626e-15 MPa", true)] // an exponent form is read at its own last digit (1e-19)
    public void The_Allowance_Is_Half_The_Last_Digit_The_Form_Printed(string expected, string unit, string shown, bool match)
    {
        var result = GuidedTolerance.CompareShown("x", expected, unit, shown);
        Assert.Equal(match, result.IsMatch);
    }

    [Fact]
    public void The_Shown_Resolution_Of_The_Display_Decides_The_Allowance_And_Nothing_Else_Does()
    {
        foreach (var (shown, half) in new[] { ("144.9 MPa", 0.05), ("150000 h", 0.5), ("0.003400 N·m", 5e-7), ("2.4942E+06 W", 50.0) })
        {
            var resolution = Tempest.Calc.Sheets.CalcDisplay.ShownResolution(shown);
            Assert.NotNull(resolution);
            Assert.Equal(half, resolution!.Value / 2, 12);
            Assert.Equal(half.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture), GuidedTolerance.CompareShown("x", shown.Split(' ')[0], string.Empty, shown.Split(' ')[0]).Allowance![1..]);
        }
    }

    [Theory]
    [InlineData("3.93", "kN", "4 kN", false)] // the form prints fewer than four figures only for an exactly whole number: 4 kN is 4 kN, not "4 to the nearest kN"
    [InlineData("4", "kN", "4 kN", true)]
    [InlineData("4.004", "kN", "4 kN", false)] // 0.004 is a whole expected last digit away from an exact 4
    [InlineData("150", "mm", "150 mm", true)]
    [InlineData("149.6", "mm", "150 mm", false)]
    [InlineData("4290.3", "h", "4290 h", true)] // four figures or more with no point: the last digit may be rounded, half of it is allowed
    [InlineData("4291", "h", "4290 h", false)]
    public void A_Short_Whole_Number_Is_Exact_And_Allows_Nothing_For_Rounding(string expected, string unit, string shown, bool match) =>
        Assert.Equal(match, GuidedTolerance.CompareShown("x", expected, unit, shown).IsMatch);

    [Fact]
    public void A_Value_The_Form_Rounds_In_A_Direction_May_Differ_By_The_Whole_Last_Digit_On_That_Side()
    {
        // A safety factor is rounded down (1.69089 prints 1.6908) and a failing utilisation up (1.0435 prints 1.05): the reference 1.6909 and 1.044 are a whole digit and 0.6 of one away.
        Assert.False(GuidedTolerance.CompareShown("n_f", "1.6909", string.Empty, "1.6908").IsMatch);
        Assert.True(GuidedTolerance.CompareShown("n_f", "1.6909", string.Empty, "1.6908", CalcDisplay.Rounding.Down).IsMatch);
        Assert.False(GuidedTolerance.CompareShown("u", "1.044", string.Empty, "1.05").IsMatch);

        var up = GuidedTolerance.CompareShown("u", "1.044", string.Empty, "1.05", CalcDisplay.Rounding.Up);
        Assert.True(up.IsMatch, up.Detail);
        Assert.Equal("±0.01", up.Allowance);

        // Never more than that: two digits away is a mismatch whichever way it was rounded.
        Assert.False(GuidedTolerance.CompareShown("n_f", "1.6909", string.Empty, "1.6907", CalcDisplay.Rounding.Down).IsMatch);
        Assert.False(GuidedTolerance.CompareShown("u", "1.044", string.Empty, "1.07", CalcDisplay.Rounding.Up).IsMatch);

        // The cap of 1 % still holds for a directed display: an expected 1 against a shown 1.02 differs by two last digits.
        Assert.False(GuidedTolerance.CompareShown("u", "1", string.Empty, "1.02", CalcDisplay.Rounding.Up).IsMatch);
    }

    [Theory]
    [InlineData("90", "deg", "90°", GuidedCompareOutcome.Match)] // the form writes the degree sign straight after the number
    [InlineData("40.004", "deg", "40.00°", GuidedCompareOutcome.Match)]
    [InlineData("40.004", "deg", "40.02°", GuidedCompareOutcome.Mismatch)]
    [InlineData("0.5", "rad", "0.5000°", GuidedCompareOutcome.NotComparable)] // a degree is not a radian: left to the tester
    public void A_Degree_Sign_Directly_After_The_Number_Is_The_Unit_Degree(string expected, string unit, string shown, GuidedCompareOutcome outcome) =>
        Assert.Equal(outcome, GuidedTolerance.CompareShown("theta", expected, unit, shown).Outcome);

    [Fact]
    public void The_Cap_Of_One_Percent_Stays_And_Never_Goes_Below_Half_The_Last_Digit_Shown()
    {
        // SEC-C-02: an expected '1' written exactly allows 1 %, not half of a whole unit; a utilisation shown to two decimals keeps half of 0.01.
        Assert.False(GuidedTolerance.CompareShown("u", "1", string.Empty, "1.02").IsMatch);
        Assert.True(GuidedTolerance.CompareShown("u", "1", string.Empty, "1.01").IsMatch);
        Assert.True(GuidedTolerance.CompareShown("u", "0.0031", string.Empty, "0.0031").IsMatch);
        Assert.True(GuidedTolerance.CompareShown("u", "0.5", string.Empty, "0.50").IsMatch);

        // The cap is 1 % of 0.004 = 0.00004, less than half the form's last digit (0.005), so the display's half is allowed.
        Assert.True(GuidedTolerance.CompareShown("u", "0.004", string.Empty, "0.00").IsMatch);
        Assert.False(GuidedTolerance.CompareShown("u", "0.004", string.Empty, "0.01").IsMatch);
    }

    [Theory]
    [InlineData("4290.3", "±0.05")]
    [InlineData("3.93", "±0.005")]
    [InlineData("2000", "±0.5")]
    [InlineData("1.5E+06", "±1 % of 1.5E+06")] // half its last place (50000) is more than 1 % of it (SEC-C-02)
    [InlineData("5", "±1 % of 5")]
    [InlineData("0", "exactly zero")]
    [InlineData("PASS", "exact text")]
    public void The_Allowance_Is_Stated_For_The_Screen(string expected, string allowance) =>
        Assert.Equal(allowance, GuidedTolerance.AllowanceText(expected));

    [Fact]
    public void The_Rule_Is_Stated_In_Words_For_The_Screen() =>
        Assert.Contains("half a unit in the last digit", GuidedTolerance.RuleText, StringComparison.Ordinal);

    // ---------------------------------------------------------------- L10: a pressure basis is part of the unit

    [Theory]
    [InlineData("bar", "81.645 bar a")] // a bare 'bar' states no basis: neither absolute nor gauge is assumed
    [InlineData("bar", "81.645 bar g")]
    [InlineData("bar a", "81.645 bar g")] // absolute and gauge differ by the atmosphere
    [InlineData("bar g", "81.645 bar a")]
    [InlineData("bar a", "81.645 bar")]
    [InlineData("bar g", "81.645 bar")]
    public void Absolute_Gauge_And_Bare_Bar_Are_Not_Taken_For_Each_Other_And_Are_Left_To_The_Tester(string expectedUnit, string shown)
    {
        var result = GuidedTolerance.CompareShown("p0", "81.6447", expectedUnit, shown);

        Assert.Equal(GuidedCompareOutcome.NotComparable, result.Outcome);
        Assert.Contains("compare them by hand", result.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("bar", "81.645 bar")]
    [InlineData("bar a", "81.645 bar a")]
    [InlineData("bar g", "81.645 bar g")]
    [InlineData("bar a", "81.645 bara")] // spaces do not matter, as for every unit
    public void The_Same_Pressure_Basis_Compares_By_The_Rule(string expectedUnit, string shown) =>
        Assert.Equal(GuidedCompareOutcome.Match, GuidedTolerance.CompareShown("p0", "81.6447", expectedUnit, shown).Outcome);

    // ---------------------------------------------------------------- L6: the allowance actually applied

    [Theory]
    [InlineData("12.34", "kN", "12.30 kN", "±0.005")] // both show two decimals: half of 0.01
    [InlineData("12.34", "kN", "12.3 kN", "±0.05")] // the shown value is the coarser: one decimal
    [InlineData("3.930000", "kN", "3.930 kN", "±0.0005")] // the form's last digit is the coarser: 3.930 (three decimals)
    [InlineData("2494200", "W", "2.4942E+06 W", "±50")] // the form writes it to five figures (tens), the expected value to units: the coarser, tens
    [InlineData("4290.3", "h", "4290.3 h", "±0.05")]
    public void The_Comparison_Reports_The_Allowance_Applied_Which_Is_The_Coarser_Of_The_Two_Last_Places(string expected, string unit, string shown, string applied)
    {
        var result = GuidedTolerance.CompareShown("x", expected, unit, shown);
        Assert.Equal(applied, result.Allowance);
    }

    [Fact]
    public void The_Applied_Allowance_Is_Reported_For_A_Mismatch_Too_And_Is_Absent_For_A_Text_Or_A_Unit_Left_To_The_Tester()
    {
        Assert.Equal("±0.005", GuidedTolerance.CompareShown("P", "3.93", "kN", "3.95 kN").Allowance);
        Assert.Null(GuidedTolerance.CompareText("check", "PASS", "PASS").Allowance);
        Assert.Null(GuidedTolerance.CompareShown("P", "3.93", "kN", "3.93 m").Allowance);
    }
}
