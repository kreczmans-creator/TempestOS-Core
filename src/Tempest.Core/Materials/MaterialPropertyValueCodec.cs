using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Materials;

/// <summary>
/// Encodes/decodes a <see cref="MaterialProperty.Value"/> to and from the
/// plain, JSON-serializable shape <see cref="MaterialSpecificationDto"/>
/// stores it as.
/// </summary>
/// <remarks>
/// Bounded to the seven dimensions <c>Tempest.Core.UnitsAndQuantities</c>
/// already defines (`ADR-0054`), via ordinary type-pattern matching — no
/// reflection, no type-name-based deserialization. This is a disclosed,
/// deliberate scope boundary (`ADR-0055`), not an oversight: a boxed
/// <see cref="Quantity{TDimension}"/> value cannot round-trip through
/// <c>System.Text.Json</c> without knowing which closed generic it is, and
/// this framework does not invent a general-purpose polymorphic-object
/// serialization mechanism to solve a problem only seven, already-known
/// types actually have. Extending to an eighth dimension (once
/// <c>Tempest.Core.UnitsAndQuantities</c> itself adds one) is a single new
/// arm in each <see langword="switch"/> below — purely additive.
/// </remarks>
internal static class MaterialPropertyValueCodec
{
    /// <summary>The dimension names this codec supports, for use in exception messages.</summary>
    public const string SupportedDimensionNames = "Length, Mass, Duration, Force, Pressure, Area, Volume";

    /// <summary>Returns whether <paramref name="value"/> is a supported <see cref="Quantity{TDimension}"/> closed generic.</summary>
    public static bool IsSupported(object value) => value switch
    {
        Quantity<Length> => true,
        Quantity<Mass> => true,
        Quantity<Duration> => true,
        Quantity<Force> => true,
        Quantity<Pressure> => true,
        Quantity<Area> => true,
        Quantity<Volume> => true,
        _ => false
    };

    /// <summary>Decomposes a supported, boxed <see cref="Quantity{TDimension}"/> into its plain, serializable parts.</summary>
    /// <exception cref="MaterialsException"><paramref name="value"/> is not one of the seven supported dimensions.</exception>
    public static (string DimensionKind, double Value, string UnitSymbol, double UnitToBaseFactor) Encode(object value) => value switch
    {
        Quantity<Length> q => ("Length", q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor),
        Quantity<Mass> q => ("Mass", q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor),
        Quantity<Duration> q => ("Duration", q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor),
        Quantity<Force> q => ("Force", q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor),
        Quantity<Pressure> q => ("Pressure", q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor),
        Quantity<Area> q => ("Area", q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor),
        Quantity<Volume> q => ("Volume", q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor),
        _ => throw new MaterialsException(
            $"Material property values must be one of the Units & Quantities dimensions this framework supports ({SupportedDimensionNames}) — received '{value.GetType()}'.")
    };

    /// <summary>Reconstructs a boxed <see cref="Quantity{TDimension}"/> from its plain, serializable parts.</summary>
    /// <exception cref="MaterialsException"><paramref name="dimensionKind"/> is not one of the seven supported dimensions — the underlying document may be corrupted or written by an incompatible version.</exception>
    public static object Decode(string dimensionKind, double value, string unitSymbol, double unitToBaseFactor) => dimensionKind switch
    {
        "Length" => new Quantity<Length>(value, new Unit<Length>(unitSymbol, unitToBaseFactor)),
        "Mass" => new Quantity<Mass>(value, new Unit<Mass>(unitSymbol, unitToBaseFactor)),
        "Duration" => new Quantity<Duration>(value, new Unit<Duration>(unitSymbol, unitToBaseFactor)),
        "Force" => new Quantity<Force>(value, new Unit<Force>(unitSymbol, unitToBaseFactor)),
        "Pressure" => new Quantity<Pressure>(value, new Unit<Pressure>(unitSymbol, unitToBaseFactor)),
        "Area" => new Quantity<Area>(value, new Unit<Area>(unitSymbol, unitToBaseFactor)),
        "Volume" => new Quantity<Volume>(value, new Unit<Volume>(unitSymbol, unitToBaseFactor)),
        _ => throw new MaterialsException(
            $"Material property document contains an unrecognised dimension kind '{dimensionKind}' — the underlying document may be corrupted or written by an incompatible version.")
    };
}
