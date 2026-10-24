using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Tests.Engine;

public sealed class FormattingTests
{
    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(1.0, "1")]
    [InlineData(-1.0, "-1")]
    [InlineData(3.14159265, "3.1416")]
    [InlineData(123456.0, "123460")]
    [InlineData(0.000123456, "0.00012346")]
    [InlineData(1e20, "1e+20")]
    [InlineData(1.23456e-9, "1.2346e-9")]
    [InlineData(60.0, "60")]
    [InlineData(99999.5, "100000")]
    [InlineData(987654321.0, "987650000")]
    [InlineData(2.5, "2.5")]
    public void Numbers_Format_To_Five_Significant_Figures(double value, string expected) => Assert.Equal(expected, CalcValue.FormatNumber(value));

    [Fact]
    public void Nan_Formats_As_Undefined_Never_As_NaN() => Assert.Equal("undefined", CalcValue.FormatNumber(double.NaN));

    [Fact]
    public void Infinity_Formats_As_A_Symbol() => Assert.Equal("∞", CalcValue.FormatNumber(double.PositiveInfinity));

    [Fact]
    public void Scalar_Formats_With_Its_Display_Unit() => Assert.Equal("12.5 kN", ScalarValue.Of(12.5, "kN").Format());

    [Fact]
    public void Dimensionless_Scalar_Formats_Without_A_Unit() => Assert.Equal("0.85", new ScalarValue(0.85).Format());

    [Fact]
    public void Vector_Formats_Compactly() => Assert.Equal("[1, 2, 3] mm", VectorValue.Of([1, 2, 3], "mm").Format());

    [Fact]
    public void Long_Vector_Is_Elided()
    {
        var text = VectorValue.Of(Enumerable.Range(1, 100).Select(i => (double)i).ToArray(), "mm").Format();
        Assert.Contains("(100 items)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Matrix_Formats_With_Its_Order() => Assert.Equal("[1, 2; 3, 4] (2×2)", new MatrixValue(new double[,] { { 1, 2 }, { 3, 4 } }, Dimension.None).Format());

    [Fact]
    public void Affine_Display_Round_Trips() => Assert.Equal("25 °C", ScalarValue.Of(25, "°C").Format());

    [Fact]
    public void Temperature_Difference_In_Kelvin_Is_Plain() => Assert.Equal("10 K", ScalarValue.Of(10, "K").Format());

    [Fact]
    public void Display_Unit_Mismatch_Falls_Back_To_Preferred() => Assert.Equal("Pa", new ScalarValue(1, Dimension.Pressure, UnitCatalog.Parse("m")).DisplayUnit.Symbol);

    [Fact]
    public void Values_Are_Records_With_Value_Equality()
    {
        Assert.Equal(ScalarValue.Of(1, "m"), ScalarValue.Of(1, "m"));
        Assert.Equal(VectorValue.Of([1, 2], "m"), VectorValue.Of([1, 2], "m"));
        Assert.NotEqual(VectorValue.Of([1, 2], "m"), VectorValue.Of([1, 3], "m"));
    }
}
