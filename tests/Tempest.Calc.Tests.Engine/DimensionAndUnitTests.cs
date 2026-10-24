using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;

namespace Tempest.Calc.Tests.Engine;

public sealed class DimensionAndUnitTests
{
    [Fact]
    public void Force_Is_Mass_Length_Over_Time_Squared() => Assert.Equal(new Dimension(1, 1, -2, 0, 0, 0, 0), Dimension.Force);

    [Fact]
    public void Pressure_Divides_Force_By_Area() => Assert.Equal(Dimension.Force / Dimension.Area, Dimension.Pressure);

    [Fact]
    public void Energy_And_Torque_Share_A_Dimension() => Assert.Equal(Dimension.Energy, Dimension.Torque);

    [Fact]
    public void Pow_And_Root_Round_Trip() => Assert.Equal(Dimension.L, Dimension.L.Pow(4).Root(4));

    [Fact]
    public void Root_Returns_Null_When_An_Exponent_Does_Not_Divide() => Assert.Null(Dimension.Volume.Root(2));

    [Fact]
    public void Base_Symbol_Writes_Numerator_And_Denominator() => Assert.Equal("m·kg/s²", Dimension.Force.BaseSymbol());

    [Fact]
    public void Base_Symbol_Of_Inverse_Time_Is_One_Over_Seconds() => Assert.Equal("1/s", Dimension.Frequency.BaseSymbol());

    [Fact]
    public void Exponents_Are_Capped_So_No_Expression_Overflows()
    {
        var d = Dimension.L;

        for (var i = 0; i < 100; i++)
            d = d.Pow(2);

        Assert.Equal(40, d.Length);
    }

    [Theory]
    [InlineData("m", 1.0)]
    [InlineData("mm", 1e-3)]
    [InlineData("km", 1e3)]
    [InlineData("µm", 1e-6)]
    [InlineData("um", 1e-6)]
    [InlineData("in", 0.0254)]
    [InlineData("ft", 0.3048)]
    [InlineData("thou", 2.54e-5)]
    public void Length_Units_Convert_To_Metres(string symbol, double factor)
    {
        var unit = UnitCatalog.Parse(symbol);
        Assert.Equal(Dimension.L, unit.Dimension);
        Assert.Equal(factor, unit.Factor, 12);
    }

    [Theory]
    [InlineData("N", 1.0)]
    [InlineData("kN", 1e3)]
    [InlineData("MN", 1e6)]
    [InlineData("lbf", 4.4482216152605)]
    [InlineData("kip", 4448.2216152605)]
    [InlineData("kgf", 9.80665)]
    public void Force_Units_Convert_To_Newtons(string symbol, double factor)
    {
        var unit = UnitCatalog.Parse(symbol);
        Assert.Equal(Dimension.Force, unit.Dimension);
        Assert.Equal(factor, unit.Factor, 9);
    }

    [Theory]
    [InlineData("Pa", 1.0)]
    [InlineData("kPa", 1e3)]
    [InlineData("MPa", 1e6)]
    [InlineData("GPa", 1e9)]
    [InlineData("bar", 1e5)]
    [InlineData("mbar", 1e2)]
    [InlineData("psi", 6894.757293168)]
    [InlineData("ksi", 6894757.293168)]
    [InlineData("N/mm^2", 1e6)]
    [InlineData("N/mm2", 1e6)]
    [InlineData("N/mm²", 1e6)]
    [InlineData("kN/m^2", 1e3)]
    [InlineData("atm", 101325)]
    [InlineData("mmH2O", 9.80665)]
    public void Pressure_Units_Convert_To_Pascals(string symbol, double factor)
    {
        var unit = UnitCatalog.Parse(symbol);
        Assert.Equal(Dimension.Pressure, unit.Dimension);
        Assert.Equal(factor, unit.Factor, 6);
    }

    [Theory]
    [InlineData("kN*m", "m²·kg/s²", 1e3)]
    [InlineData("kN·m", "m²·kg/s²", 1e3)]
    [InlineData("W/(m*K)", "m·kg/(s³·K)", 1.0)]
    [InlineData("J/(kg*K)", "m²/(s²·K)", 1.0)]
    [InlineData("kg/m^3", "kg/m³", 1.0)]
    [InlineData("m/s^2", "m/s²", 1.0)]
    [InlineData("mm^4", "m⁴", 1e-12)]
    [InlineData("cm^4", "m⁴", 1e-8)]
    [InlineData("L/s", "m³/s", 1e-3)]
    [InlineData("m^3/h", "m³/s", 1.0 / 3600)]
    [InlineData("rpm", "1/s", 2 * Math.PI / 60)]
    [InlineData("Pa*s", "kg/(m·s)", 1.0)]
    [InlineData("mPa*s", "kg/(m·s)", 1e-3)]
    [InlineData("cSt", "m²/s", 1e-6)]
    [InlineData("kWh", "m²·kg/s²", 3.6e6)]
    [InlineData("mV/A/m", "m·kg/(s³·A²)", 1e-3)]
    [InlineData("Ω/km", "m·kg/(s³·A²)", 1e-3)]
    [InlineData("1/K", "1/K", 1.0)]
    public void Compound_Units_Parse_To_The_Right_Dimension_And_Factor(string symbol, string baseSymbol, double factor)
    {
        var unit = UnitCatalog.Parse(symbol);
        Assert.Equal(baseSymbol, unit.Dimension.BaseSymbol());
        Assert.Equal(factor, unit.Factor, 12);
    }

    [Theory]
    [InlineData("degC", 25.0, 298.15)]
    [InlineData("°C", -40.0, 233.15)]
    [InlineData("degF", 212.0, 373.15)]
    [InlineData("K", 300.0, 300.0)]
    public void Temperature_Units_Are_Affine(string symbol, double value, double kelvin)
    {
        var unit = UnitCatalog.Parse(symbol);
        Assert.Equal(kelvin, unit.ToSi(value), 9);
        Assert.Equal(value, unit.FromSi(kelvin), 9);
    }

    [Theory]
    [InlineData("degC/m")]
    [InlineData("degC^2")]
    [InlineData("N*°C")]
    public void Affine_Units_Cannot_Be_Combined(string symbol) => Assert.Throws<CalcUnitException>(() => UnitCatalog.Parse(symbol));

    [Theory]
    [InlineData("furlong")]
    [InlineData("m/")]
    [InlineData("(m")]
    [InlineData("m^9")]
    [InlineData("m^0")]
    [InlineData("kkm")]
    [InlineData("N**m")]
    public void Unknown_Or_Malformed_Units_Are_Refused(string symbol)
    {
        Assert.False(UnitCatalog.TryParse(symbol, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Unit_Expressions_Are_Capped_At_64_Characters()
    {
        var s = string.Join("*", Enumerable.Repeat("m", 40));
        Assert.False(UnitCatalog.TryParse(s, out _, out var error));
        Assert.Contains("64", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Preferred_Unit_For_Pressure_Is_Pascal() => Assert.Equal("Pa", UnitCatalog.PreferredFor(Dimension.Pressure).Symbol);

    [Fact]
    public void Preferred_Unit_For_An_Unknown_Dimension_Is_Its_Base_Symbol() => Assert.Equal("m⁵", UnitCatalog.PreferredFor(Dimension.L.Pow(5)).Symbol);

    [Fact]
    public void Deg_And_Rad_Are_Dimensionless()
    {
        Assert.True(UnitCatalog.Parse("deg").Dimension.IsDimensionless);
        Assert.Equal(Math.PI / 180, UnitCatalog.Parse("deg").Factor, 15);
    }

    [Fact]
    public void Percent_Is_A_Dimensionless_Hundredth() => Assert.Equal(0.01, UnitCatalog.Parse("%").Factor);
}
