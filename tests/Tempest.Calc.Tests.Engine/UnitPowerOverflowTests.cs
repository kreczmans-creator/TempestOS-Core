using CsCheck;
using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;

namespace Tempest.Calc.Tests.Engine;

/// <summary>
/// Security review of W1, M4: unit text reaches the parser from imported files and pasted CSV, so no text may make
/// <see cref="UnitCatalog"/> throw anything but a unit error or return a wrong unit by wrapping an exponent.
/// </summary>
public sealed class UnitPowerOverflowTests
{
    [Theory]
    [InlineData("m^99999999999999999999")]
    [InlineData("m99999999999")]
    [InlineData("m^2147483648")]
    [InlineData("m^-2147483648")]
    [InlineData("m²¹⁴⁷⁴⁸³⁶⁴⁸")]
    [InlineData("m⁻²¹⁴⁷⁴⁸³⁶⁴⁸")]
    [InlineData("m⁴²⁹⁴⁹⁶⁷²⁹⁸")]
    [InlineData("kg^000000000000000000000009")]
    [InlineData("m^9")]
    [InlineData("m^0")]
    [InlineData("m0")]
    public void A_Huge_Or_Out_Of_Range_Power_Is_A_Unit_Error_Never_An_Overflow_Or_A_Wrapped_Value(string text)
    {
        Assert.False(UnitCatalog.TryParse(text, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
        Assert.Throws<CalcUnitException>(() => UnitCatalog.Parse(text));
    }

    [Theory]
    [InlineData("m^2", 2)]
    [InlineData("m^-2", -2)]
    [InlineData("m^+3", 3)]
    [InlineData("m^0002", 2)]
    [InlineData("m2", 2)]
    [InlineData("m²", 2)]
    [InlineData("m⁻³", -3)]
    [InlineData("m^8", 8)]
    [InlineData("m^-8", -8)]
    public void Ordinary_Powers_Still_Parse(string text, int power)
    {
        var unit = UnitCatalog.Parse(text);
        Assert.Equal(Dimension.L.Pow(power), unit.Dimension);
    }

    [Fact]
    public void The_Wrapped_Value_That_Used_To_Pass_As_Metres_Squared_Is_Refused()
    {
        // 4294967298 = 2^32 + 2: an unchecked int accumulation wraps it to 2.
        Assert.False(UnitCatalog.TryParse("m⁴²⁹⁴⁹⁶⁷²⁹⁸", out _, out _));
    }

    [Fact]
    public void Random_Digit_Runs_After_An_Exponent_Sign_Never_Throw_Anything_But_A_Unit_Error()
    {
        Gen.String[Gen.Char["0123456789^-+²³¹⁰⁴⁵⁶⁷⁸⁹⁻m"], 0, 40].Sample(tail =>
        {
            try
            {
                UnitCatalog.TryParse("m" + tail, out _, out _);
            }
            catch (CalcUnitException)
            {
                // A unit error is the documented failure.
            }
        }, iter: 5000);
    }
}
