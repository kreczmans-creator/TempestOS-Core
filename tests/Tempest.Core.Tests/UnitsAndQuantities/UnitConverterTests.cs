using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

public class UnitConverterTests
{
    [Fact]
    public void Convert_DelegatesToQuantityConvertTo()
    {
        IUnitConverter converter = new UnitConverter();
        var oneMetre = new Quantity<Length>(1.0, LengthUnits.Metre);

        var converted = converter.Convert(oneMetre, LengthUnits.Centimetre);

        Assert.Equal(100.0, converted.Value, precision: 9);
        Assert.Equal(LengthUnits.Centimetre, converted.Unit);
    }

    [Fact]
    public void Constructor_RequiresNoArguments()
    {
        var converter = new UnitConverter();

        Assert.NotNull(converter);
    }
}
