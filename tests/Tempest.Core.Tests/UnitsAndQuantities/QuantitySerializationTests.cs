using System.Text.Json;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

public class QuantitySerializationTests
{
    [Fact]
    public void Quantity_JsonRoundTrip_PreservesValueAndUnit()
    {
        var original = new Quantity<Length>(5.5, LengthUnits.Foot);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Quantity<Length>>(json);

        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void Unit_JsonRoundTrip_PreservesSymbolAndFactor()
    {
        var original = LengthUnits.Foot;

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Unit<Length>>(json);

        Assert.Equal(original, deserialized);
    }
}
