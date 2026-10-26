using Tempest.Calc.Library.ManualTesting;

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
    public void The_Utilisation_Is_Compared_At_The_Three_Figures_The_Form_Shows()
    {
        Assert.True(GuidedTolerance.CompareShown("u", "0.728", string.Empty, "0.728", 3).IsMatch);
        Assert.True(GuidedTolerance.CompareShown("u", "12.345", string.Empty, "12.3", 3).IsMatch);
        Assert.False(GuidedTolerance.CompareShown("u", "12.345", string.Empty, "12.4", 3).IsMatch);
    }

    [Theory]
    [InlineData("4290.3", "±0.05")]
    [InlineData("3.93", "±0.005")]
    [InlineData("2000", "±0.5")]
    [InlineData("1.5E+06", "±50000")]
    [InlineData("0", "exactly zero")]
    [InlineData("PASS", "exact text")]
    public void The_Allowance_Is_Stated_For_The_Screen(string expected, string allowance) =>
        Assert.Equal(allowance, GuidedTolerance.AllowanceText(expected));

    [Fact]
    public void The_Rule_Is_Stated_In_Words_For_The_Screen() =>
        Assert.Contains("half a unit in the last digit", GuidedTolerance.RuleText, StringComparison.Ordinal);
}
