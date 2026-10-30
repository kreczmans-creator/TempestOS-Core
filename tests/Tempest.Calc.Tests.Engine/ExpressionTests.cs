using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Tests.Engine;

public sealed class ExpressionTests
{
    private static ICalcSession Session() => CalcEngine.Instance.CreateSession();

    private static ScalarValue Scalar(string expression) => (ScalarValue)Session().Evaluate(expression);

    [Theory]
    [InlineData("1 + 2", 3.0)]
    [InlineData("2 * 3 + 4", 10.0)]
    [InlineData("2 + 3 * 4", 14.0)]
    [InlineData("(2 + 3) * 4", 20.0)]
    [InlineData("10 / 4", 2.5)]
    [InlineData("2 ^ 3 ^ 2", 512.0)]
    [InlineData("-2 ^ 2", -4.0)]
    [InlineData("(-2) ^ 2", 4.0)]
    [InlineData("mod(7, 3)", 1.0)]
    [InlineData("mod(-7, 3)", 2.0)]
    [InlineData("1e3 + 1_000", 2000.0)]
    [InlineData(".5 * 4", 2.0)]
    [InlineData("2 × 3 − 1", 5.0)]
    [InlineData("8 ÷ 2", 4.0)]
    public void Arithmetic_Follows_Precedence(string expression, double expected) => Assert.Equal(expected, Scalar(expression).Si, 12);

    [Theory]
    [InlineData("40 mm + 2 mm", 0.042, "mm")]
    [InlineData("1 m + 500 mm", 1.5, "m")]
    [InlineData("500 mm + 1 m", 1.5, "mm")]
    [InlineData("2 * 40 mm", 0.08, "mm")]
    [InlineData("40 mm * 2", 0.08, "mm")]
    [InlineData("80 mm / 2", 0.04, "mm")]
    [InlineData("3 m * 4 m", 12.0, "m^2")]
    [InlineData("9.81 m/s^2 * 10 kg", 98.1, "N")]
    [InlineData("1 kN*m", 1000.0, "kN*m")]
    [InlineData("2 kN*m + 500 N*m", 2500.0, "kN*m")]
    public void Dimensioned_Arithmetic_Converts_To_Si(string expression, double si, string displayUnit)
    {
        var v = Scalar(expression);
        Assert.Equal(si, v.Si, 9);
        Assert.Equal(UnitCatalog.Parse(displayUnit).Dimension, v.Dimension);
    }

    [Fact]
    public void Stress_From_Force_Over_Area_Is_In_Pascals()
    {
        var v = Scalar("12 kN / 200 mm^2");
        Assert.Equal(60e6, v.Si, 3);
        Assert.Equal(Dimension.Pressure, v.Dimension);
    }

    [Fact]
    public void Percent_Is_A_Unit_Not_Modulo() => Assert.Equal(0.05, Scalar("5 %").Si, 12);

    [Fact]
    public void A_Dimensionless_Factor_Keeps_The_Display_Unit() => Assert.Equal("mm", Scalar("2 * 40 mm").DisplayUnit.Symbol);

    [Fact]
    public void Conversion_Changes_Display_Not_Value()
    {
        var v = Scalar("12 kN / 200 mm^2 in MPa");
        Assert.Equal(60e6, v.Si, 6);
        Assert.Equal("MPa", v.DisplayUnit.Symbol);
        Assert.Equal(60.0, v.Displayed, 9);
        Assert.Equal("60 MPa", v.Format());
    }

    [Fact]
    public void Conversion_Accepts_Compound_Units() => Assert.Equal("kN/m", Scalar("1200 N/m in kN/m").DisplayUnit.Symbol);

    [Fact]
    public void Conversion_Accepts_Bracketed_Units() => Assert.Equal("W/(m·K)", Scalar("50 W/(m*K) in W/(m*K)").DisplayUnit.Symbol);

    [Theory]
    [InlineData("40 mm + 2 kg")]
    [InlineData("1 m - 1 s")]
    [InlineData("1 N < 1 m")]
    [InlineData("1 m in kg")]
    [InlineData("sin(2 m)")]
    [InlineData("exp(1 N)")]
    [InlineData("ln(3 m)")]
    [InlineData("2 ^ (1 m)")]
    [InlineData("(1 m) ^ 0.3")]
    [InlineData("sqrt(1 m^3)")]
    [InlineData("min(1 m, 1 s)")]
    [InlineData("[1 m, 2 s]")]
    [InlineData("clamp(1 m, 0, 2)")]
    [InlineData("atan2(1 m, 1 s)")]
    [InlineData("interp(1 m, [1, 2], [3, 4])")]
    public void Dimension_Errors_Are_Reported(string expression) => Assert.Throws<CalcDimensionException>(() => Session().Evaluate(expression));

    [Fact]
    public void Sqrt_Of_An_Area_Is_A_Length() => Assert.Equal(Dimension.L, Scalar("sqrt(16 mm^2)").Dimension);

    [Fact]
    public void Sqrt_Of_An_Area_Has_The_Right_Value() => Assert.Equal(4e-3, Scalar("sqrt(16 mm^2)").Si, 12);

    [Fact]
    public void Fractional_Power_Of_An_Area_Gives_A_Length() => Assert.Equal(Dimension.L, Scalar("(16 mm^2) ^ 0.5").Dimension);

    [Fact]
    public void Radius_Of_Gyration_Works() => Assert.Equal(Dimension.L, Scalar("sqrt(8000 mm^4 / 200 mm^2)").Dimension);

    [Theory]
    [InlineData("sin(30 deg)", 0.5)]
    [InlineData("cos(60 deg)", 0.5)]
    [InlineData("tan(45 deg)", 1.0)]
    [InlineData("sin(pi / 6)", 0.5)]
    [InlineData("sin(π / 2)", 1.0)]
    [InlineData("exp(0)", 1.0)]
    [InlineData("ln(e)", 1.0)]
    [InlineData("log10(1000)", 3.0)]
    [InlineData("log2(8)", 3.0)]
    [InlineData("sqrt(16)", 4.0)]
    [InlineData("cbrt(27)", 3.0)]
    [InlineData("abs(-3)", 3.0)]
    [InlineData("sign(-3)", -1.0)]
    [InlineData("floor(2.7)", 2.0)]
    [InlineData("ceil(2.1)", 3.0)]
    [InlineData("round(2.5)", 3.0)]
    [InlineData("round(2.345, 2)", 2.35)]
    [InlineData("min(3, 1, 2)", 1.0)]
    [InlineData("max(3, 1, 2)", 3.0)]
    [InlineData("clamp(5, 0, 3)", 3.0)]
    [InlineData("sum(1, 2, 3)", 6.0)]
    [InlineData("avg(1, 2, 3)", 2.0)]
    [InlineData("pow(2, 10)", 1024.0)]
    [InlineData("hypot(3, 4)", 5.0)]
    [InlineData("if(1 < 2, 10, 20)", 10.0)]
    [InlineData("if(1 > 2, 10, 20)", 20.0)]
    [InlineData("if(true and not false, 1, 0)", 1.0)]
    [InlineData("if(1 == 1 or 2 == 3, 1, 0)", 1.0)]
    [InlineData("if(1 != 1, 1, 0)", 0.0)]
    [InlineData("if(2 >= 2 and 2 <= 2, 1, 0)", 1.0)]
    [InlineData("interp(1.5, [1, 2, 3], [10, 20, 30])", 15.0)]
    [InlineData("interp(0, [1, 2, 3], [10, 20, 30])", 10.0)]
    [InlineData("interp(0, [1, 2, 3], [10, 20, 30], true)", 0.0)]
    [InlineData("len([4, 5, 6])", 3.0)]
    [InlineData("sum([1, 2, 3])", 6.0)]
    [InlineData("max([1, 7, 3])", 7.0)]
    [InlineData("[4, 5, 6][2]", 5.0)]
    [InlineData("[1, 2; 3, 4][2, 1]", 3.0)]
    [InlineData("dot([1, 2, 3], [4, 5, 6])", 32.0)]
    [InlineData("norm([3, 4])", 5.0)]
    [InlineData("det([1, 2; 3, 4])", -2.0)]
    [InlineData("inv([2, 0; 0, 4])[2, 2]", 0.25)]
    [InlineData("solve([2, 0; 0, 4], [2, 8])[2]", 2.0)]
    [InlineData("transpose([1, 2; 3, 4])[1, 2]", 3.0)]
    [InlineData("([1, 2; 3, 4] * [1, 0; 0, 1])[2, 1]", 3.0)]
    [InlineData("([1, 2; 3, 4] * [1, 1])[2]", 7.0)]
    [InlineData("vec(1, [2, 3])[3]", 3.0)]
    [InlineData("g_n / (1 m/s^2)", 9.80665)]
    public void Functions_Evaluate(string expression, double expected) => Assert.Equal(expected, Scalar(expression).Si, 9);

    [Fact]
    public void Asin_Returns_Radians_Displayed() => Assert.Equal("rad", Scalar("asin(0.5)").DisplayUnit.Symbol);

    [Fact]
    public void Angle_Result_Converts_To_Degrees() => Assert.Equal(30.0, Scalar("asin(0.5) in deg").Displayed, 9);

    [Fact]
    public void Atan2_Takes_Dimensioned_Arguments() => Assert.Equal(Math.PI / 4, Scalar("atan2(1 m, 1000 mm)").Si, 12);

    [Fact]
    public void Floor_Rounds_In_The_Display_Unit() => Assert.Equal(40e-3, Scalar("floor(40.6 mm)").Si, 12);

    [Fact]
    public void Min_Keeps_Dimension_And_Unit()
    {
        var v = Scalar("min(1 m, 500 mm)");
        Assert.Equal(0.5, v.Si, 12);
        Assert.Equal("m", v.DisplayUnit.Symbol);
    }

    [Fact]
    public void Interp_Carries_The_Table_Unit()
    {
        var v = Scalar("interp(2 m, [1 m, 3 m], [10 N, 30 N])");
        Assert.Equal(20.0, v.Si, 9);
        Assert.Equal(Dimension.Force, v.Dimension);
    }

    [Fact]
    public void Vectors_Carry_Dimension()
    {
        var v = (VectorValue)Session().Evaluate("[1 kN, 2 kN] * 2");
        Assert.Equal(Dimension.Force, v.Dimension);
        Assert.Equal(4000.0, v.Si[1], 9);
    }

    [Fact]
    public void Matrix_Inverse_Has_Inverse_Dimension() => Assert.Equal(Dimension.None / Dimension.Force, ((MatrixValue)Session().Evaluate("inv([1 N, 0 N; 0 N, 1 N])")).Dimension);

    [Fact]
    public void Solve_Divides_Dimensions() => Assert.Equal(Dimension.L, ((VectorValue)Session().Evaluate("solve([2 N/m, 0 N/m; 0 N/m, 4 N/m], [2 N, 8 N])")).Dimension);

    [Theory]
    [InlineData("1 / 0")]
    [InlineData("1 m / 0")]
    [InlineData("[1, 2] / 0")]
    [InlineData("mod(7, 0)")]
    public void Division_By_Zero_Is_An_Evaluation_Error(string expression) => Assert.Throws<CalcEvaluationException>(() => Session().Evaluate(expression));

    [Theory]
    [InlineData("sqrt(-1)")]
    [InlineData("ln(0)")]
    [InlineData("log10(-1)")]
    [InlineData("asin(2)")]
    [InlineData("[1, 2][3]")]
    [InlineData("[1, 2][0]")]
    [InlineData("[1, 2][1.5]")]
    [InlineData("[1, 2; 3, 4][3, 1]")]
    [InlineData("unknown + 1")]
    [InlineData("nosuch(1)")]
    [InlineData("if(1, 2, 3)")]
    [InlineData("if(true, 2)")]
    [InlineData("1 and 2")]
    [InlineData("not 1")]
    [InlineData("[1, 2] + [1, 2, 3]")]
    [InlineData("[1, 2; 3, 4] * [1, 2, 3]")]
    [InlineData("det([1, 2, 3; 4, 5, 6])")]
    [InlineData("inv([1, 2; 2, 4])")]
    [InlineData("solve([1, 2; 2, 4], [1, 2])")]
    [InlineData("interp(1, [1], [1])")]
    [InlineData("interp(1, [2, 1], [1, 2])")]
    [InlineData("clamp(1, 3, 2)")]
    [InlineData("[1, 2; 3]")]
    [InlineData("min()")]
    [InlineData("\"a\" + 1")]
    [InlineData("\"a\" < \"b\"")]
    public void Invalid_Evaluations_Are_Clean_Errors(string expression) => Assert.Throws<CalcEvaluationException>(() => Session().Evaluate(expression));

    [Theory]
    [InlineData("1 +")]
    [InlineData("(1 + 2")]
    [InlineData("1 + 2)")]
    [InlineData("1 2")]
    [InlineData("[1, 2")]
    [InlineData("f(1,")]
    [InlineData("1 < 2 < 3")]
    [InlineData("a = 1")]
    [InlineData("1 ! 2")]
    [InlineData("\"unterminated")]
    [InlineData("1 in")]
    [InlineData("#")]
    [InlineData("")]
    [InlineData("1..2")]
    public void Syntax_Errors_Are_Parse_Errors(string expression) => Assert.ThrowsAny<CalcException>(() => Session().Evaluate(expression));

    [Fact]
    public void Text_Values_Compare_With_Equality()
    {
        Assert.Equal(1.0, Scalar("if(\"H7\" == \"H7\", 1, 0)").Si);
        Assert.Equal(0.0, Scalar("if(\"H7\" == \"h6\", 1, 0)").Si);
    }

    [Fact]
    public void Unknown_Unit_After_A_Number_Names_The_Number() =>
        Assert.Contains("after '2'", Assert.Throws<CalcUnitException>(() => Session().Evaluate("2 furlongs")).Message, StringComparison.Ordinal);

    [Fact]
    public void Unit_After_Number_Does_Not_Swallow_An_Operator() => Assert.Equal(6e-3, Scalar("2 mm *3").Si, 12);

    [Fact]
    public void Keyword_After_Number_Is_Not_A_Unit() => Assert.Equal(1.0, Scalar("if(2 m in mm > 1 m, 1, 0)").Si);

    [Fact]
    public void Conversion_Binds_Tighter_Than_Comparison() => Assert.Equal(1.0, Scalar("if(1500 N in kN > 1 kN, 1, 0)").Si);

    [Fact]
    public void Units_Inside_Calls_Do_Not_Swallow_Brackets() => Assert.Equal(1.0, Scalar("max(1 m, 500 mm) / (1 m)").Si, 12);

    [Fact]
    public void Bracketed_Unit_After_Number_Parses() => Assert.Equal(50.0, Scalar("50 W/(m*K) / (1 W/(m*K))").Si, 12);

    [Fact]
    public void Comparison_Across_Units_Converts() => Assert.Equal(1.0, Scalar("if(1 m > 999 mm, 1, 0)").Si);

    [Fact]
    public void Isfinite_Reports_Overflow() => Assert.Equal(0.0, Scalar("if(isfinite(10 ^ 400), 1, 0)").Si);
}
