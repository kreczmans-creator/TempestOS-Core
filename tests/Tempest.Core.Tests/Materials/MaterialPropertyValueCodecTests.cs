using Tempest.Core.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Materials;

public class MaterialPropertyValueCodecTests
{
    [Fact]
    public void Encode_ThenDecode_Length_RoundTrips()
    {
        var original = new Quantity<Length>(5.0, LengthUnits.Foot);

        var (dimensionKind, value, unitSymbol, unitToBaseFactor) = MaterialPropertyValueCodec.Encode(original);
        var decoded = (Quantity<Length>)MaterialPropertyValueCodec.Decode(dimensionKind, value, unitSymbol, unitToBaseFactor);

        Assert.Equal("Length", dimensionKind);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Encode_ThenDecode_Mass_RoundTrips()
    {
        var original = new Quantity<Mass>(10.0, MassUnits.Pound);

        var (dimensionKind, value, unitSymbol, unitToBaseFactor) = MaterialPropertyValueCodec.Encode(original);
        var decoded = (Quantity<Mass>)MaterialPropertyValueCodec.Decode(dimensionKind, value, unitSymbol, unitToBaseFactor);

        Assert.Equal("Mass", dimensionKind);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Encode_ThenDecode_Duration_RoundTrips()
    {
        var original = new Quantity<Duration>(3.0, DurationUnits.Hour);

        var (dimensionKind, value, unitSymbol, unitToBaseFactor) = MaterialPropertyValueCodec.Encode(original);
        var decoded = (Quantity<Duration>)MaterialPropertyValueCodec.Decode(dimensionKind, value, unitSymbol, unitToBaseFactor);

        Assert.Equal("Duration", dimensionKind);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Encode_ThenDecode_Force_RoundTrips()
    {
        var original = new Quantity<Force>(2.0, ForceUnits.Kilonewton);

        var (dimensionKind, value, unitSymbol, unitToBaseFactor) = MaterialPropertyValueCodec.Encode(original);
        var decoded = (Quantity<Force>)MaterialPropertyValueCodec.Decode(dimensionKind, value, unitSymbol, unitToBaseFactor);

        Assert.Equal("Force", dimensionKind);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Encode_ThenDecode_Pressure_RoundTrips()
    {
        var original = new Quantity<Pressure>(250.0, PressureUnits.Megapascal);

        var (dimensionKind, value, unitSymbol, unitToBaseFactor) = MaterialPropertyValueCodec.Encode(original);
        var decoded = (Quantity<Pressure>)MaterialPropertyValueCodec.Decode(dimensionKind, value, unitSymbol, unitToBaseFactor);

        Assert.Equal("Pressure", dimensionKind);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Encode_ThenDecode_Area_RoundTrips()
    {
        var original = new Quantity<Area>(4.0, AreaUnits.SquareFoot);

        var (dimensionKind, value, unitSymbol, unitToBaseFactor) = MaterialPropertyValueCodec.Encode(original);
        var decoded = (Quantity<Area>)MaterialPropertyValueCodec.Decode(dimensionKind, value, unitSymbol, unitToBaseFactor);

        Assert.Equal("Area", dimensionKind);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Encode_ThenDecode_Volume_RoundTrips()
    {
        var original = new Quantity<Volume>(1.5, VolumeUnits.Litre);

        var (dimensionKind, value, unitSymbol, unitToBaseFactor) = MaterialPropertyValueCodec.Encode(original);
        var decoded = (Quantity<Volume>)MaterialPropertyValueCodec.Decode(dimensionKind, value, unitSymbol, unitToBaseFactor);

        Assert.Equal("Volume", dimensionKind);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void IsSupported_UnsupportedType_ReturnsFalse()
    {
        Assert.False(MaterialPropertyValueCodec.IsSupported(42.0));
        Assert.False(MaterialPropertyValueCodec.IsSupported("a string"));
    }

    [Fact]
    public void Encode_UnsupportedType_ThrowsMaterialsException()
    {
        Assert.Throws<MaterialsException>(() => MaterialPropertyValueCodec.Encode(42.0));
    }

    [Fact]
    public void Decode_UnrecognisedDimensionKind_ThrowsMaterialsException()
    {
        Assert.Throws<MaterialsException>(() => MaterialPropertyValueCodec.Decode("Temperature", 1.0, "C", 1.0));
    }
}
