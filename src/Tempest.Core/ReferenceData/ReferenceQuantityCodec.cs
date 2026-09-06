using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData;

/// <summary>
/// Encodes and decodes a boxed <see cref="Quantity{TDimension}"/> to and
/// from the plain, JSON-serialisable parts a dictionary-valued reference
/// record stores it as.
/// </summary>
/// <remarks>
/// <para>
/// Most Group A values are declared at a statically-known dimension and
/// need no codec at all (`ADR-0124`). Two libraries genuinely cannot be:
/// <c>Tempest.Core.Materials</c>, whose property set is deliberately open
/// (`ADR-0055`), and <c>Tempest.Core.Constants</c>, whose whole purpose is
/// to hold values of whatever dimension a constant happens to have. Both
/// hold a boxed <see cref="Quantity{TDimension}"/>, whose closed generic
/// <c>System.Text.Json</c> cannot recover — hence this.
/// </para>
/// <para>
/// Generalises <c>MaterialPropertyValueCodec</c> (`ADR-0055`), which was
/// bounded to the seven dimensions that existed when it was written and
/// disclosed that bound as a deliberate scope boundary. The decision that
/// ADR recorded is unchanged; its scope is now every dimension this
/// framework defines, and one codec serves both libraries rather than
/// each growing its own copy. Ordinary type-pattern matching, no
/// reflection, no type-name-based deserialisation; extending to a new
/// dimension remains a single new arm in each <see langword="switch"/>.
/// </para>
/// </remarks>
public static class ReferenceQuantityCodec
{
    /// <summary>The dimension names this codec supports, for use in exception messages.</summary>
    public const string SupportedDimensionNames =
        "Length, Mass, Duration, Force, Pressure, Area, Volume, RotationalSpeed, PlaneAngle, Temperature, "
        + "MassDensity, Stiffness, Torque, ThermalConductivity, ThermalExpansion, SpecificHeatCapacity, "
        + "Acceleration, Energy, Velocity, Power, TorsionalStiffness, Dimensionless";

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
        Quantity<RotationalSpeed> => true,
        Quantity<PlaneAngle> => true,
        Quantity<Temperature> => true,
        Quantity<MassDensity> => true,
        Quantity<Stiffness> => true,
        Quantity<Torque> => true,
        Quantity<ThermalConductivity> => true,
        Quantity<ThermalExpansion> => true,
        Quantity<SpecificHeatCapacity> => true,
        Quantity<Acceleration> => true,
        Quantity<Energy> => true,
        Quantity<Velocity> => true,
        Quantity<Power> => true,
        Quantity<TorsionalStiffness> => true,
        Quantity<Dimensionless> => true,
        _ => false
    };

    /// <summary>Returns the dimension name <paramref name="value"/> encodes as, or <see langword="null"/> if it is not a supported quantity.</summary>
    public static string? DimensionNameOf(object value) => value switch
    {
        Quantity<Length> => "Length",
        Quantity<Mass> => "Mass",
        Quantity<Duration> => "Duration",
        Quantity<Force> => "Force",
        Quantity<Pressure> => "Pressure",
        Quantity<Area> => "Area",
        Quantity<Volume> => "Volume",
        Quantity<RotationalSpeed> => "RotationalSpeed",
        Quantity<PlaneAngle> => "PlaneAngle",
        Quantity<Temperature> => "Temperature",
        Quantity<MassDensity> => "MassDensity",
        Quantity<Stiffness> => "Stiffness",
        Quantity<Torque> => "Torque",
        Quantity<ThermalConductivity> => "ThermalConductivity",
        Quantity<ThermalExpansion> => "ThermalExpansion",
        Quantity<SpecificHeatCapacity> => "SpecificHeatCapacity",
        Quantity<Acceleration> => "Acceleration",
        Quantity<Energy> => "Energy",
        Quantity<Velocity> => "Velocity",
        Quantity<Power> => "Power",
        Quantity<TorsionalStiffness> => "TorsionalStiffness",
        Quantity<Dimensionless> => "Dimensionless",
        _ => null
    };

    /// <summary>Decomposes a supported, boxed <see cref="Quantity{TDimension}"/> into its plain, serialisable parts.</summary>
    /// <exception cref="ReferenceDataException"><paramref name="value"/> is not a supported dimension.</exception>
    public static EncodedQuantity Encode(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var dimension = DimensionNameOf(value)
            ?? throw new ReferenceDataException(
                "ReferenceData",
                $"A reference quantity must be one of the Units & Quantities dimensions this framework supports ({SupportedDimensionNames}) — received '{value.GetType()}'.");

        var (magnitude, symbol, factor, offset) = value switch
        {
            Quantity<Length> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Mass> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Duration> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Force> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Pressure> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Area> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Volume> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<RotationalSpeed> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<PlaneAngle> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Temperature> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<MassDensity> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Stiffness> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Torque> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<ThermalConductivity> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<ThermalExpansion> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<SpecificHeatCapacity> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Acceleration> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Energy> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Velocity> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Power> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<TorsionalStiffness> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            Quantity<Dimensionless> q => Parts(q.Value, q.Unit.Symbol, q.Unit.ToBaseUnitFactor, q.Unit.ToBaseUnitOffset),
            _ => throw new ReferenceDataException("ReferenceData", $"Unsupported quantity '{value.GetType()}'."),
        };

        return new EncodedQuantity(dimension, magnitude, symbol, factor, offset);
    }

    /// <summary>Reconstructs a boxed <see cref="Quantity{TDimension}"/> from its plain, serialisable parts.</summary>
    /// <exception cref="ReferenceDataException"><paramref name="encoded"/> names a dimension this build does not recognise — the underlying document may be corrupted or written by an incompatible version.</exception>
    public static object Decode(EncodedQuantity encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        var (dimension, value, symbol, factor, offset) = encoded;

        return dimension switch
        {
            "Length" => new Quantity<Length>(value, new Unit<Length>(symbol, factor, offset)),
            "Mass" => new Quantity<Mass>(value, new Unit<Mass>(symbol, factor, offset)),
            "Duration" => new Quantity<Duration>(value, new Unit<Duration>(symbol, factor, offset)),
            "Force" => new Quantity<Force>(value, new Unit<Force>(symbol, factor, offset)),
            "Pressure" => new Quantity<Pressure>(value, new Unit<Pressure>(symbol, factor, offset)),
            "Area" => new Quantity<Area>(value, new Unit<Area>(symbol, factor, offset)),
            "Volume" => new Quantity<Volume>(value, new Unit<Volume>(symbol, factor, offset)),
            "RotationalSpeed" => new Quantity<RotationalSpeed>(value, new Unit<RotationalSpeed>(symbol, factor, offset)),
            "PlaneAngle" => new Quantity<PlaneAngle>(value, new Unit<PlaneAngle>(symbol, factor, offset)),
            "Temperature" => new Quantity<Temperature>(value, new Unit<Temperature>(symbol, factor, offset)),
            "MassDensity" => new Quantity<MassDensity>(value, new Unit<MassDensity>(symbol, factor, offset)),
            "Stiffness" => new Quantity<Stiffness>(value, new Unit<Stiffness>(symbol, factor, offset)),
            "Torque" => new Quantity<Torque>(value, new Unit<Torque>(symbol, factor, offset)),
            "ThermalConductivity" => new Quantity<ThermalConductivity>(value, new Unit<ThermalConductivity>(symbol, factor, offset)),
            "ThermalExpansion" => new Quantity<ThermalExpansion>(value, new Unit<ThermalExpansion>(symbol, factor, offset)),
            "SpecificHeatCapacity" => new Quantity<SpecificHeatCapacity>(value, new Unit<SpecificHeatCapacity>(symbol, factor, offset)),
            "Acceleration" => new Quantity<Acceleration>(value, new Unit<Acceleration>(symbol, factor, offset)),
            "Energy" => new Quantity<Energy>(value, new Unit<Energy>(symbol, factor, offset)),
            "Velocity" => new Quantity<Velocity>(value, new Unit<Velocity>(symbol, factor, offset)),
            "Power" => new Quantity<Power>(value, new Unit<Power>(symbol, factor, offset)),
            "TorsionalStiffness" => new Quantity<TorsionalStiffness>(value, new Unit<TorsionalStiffness>(symbol, factor, offset)),
            "Dimensionless" => new Quantity<Dimensionless>(value, new Unit<Dimensionless>(symbol, factor, offset)),
            _ => throw new ReferenceDataException(
                "ReferenceData",
                $"A stored reference quantity names an unrecognised dimension '{dimension}' — the underlying document may be corrupted or written by an incompatible version."),
        };
    }

    /// <summary>The base-unit magnitude of a supported, boxed quantity, for ordering values recorded in different units.</summary>
    /// <exception cref="ReferenceDataException"><paramref name="value"/> is not a supported dimension.</exception>
    public static double CanonicalValueOf(object value)
    {
        var encoded = Encode(value);
        return (encoded.Value * encoded.UnitToBaseFactor) + encoded.UnitToBaseOffset;
    }

    private static (double Value, string Symbol, double Factor, double Offset) Parts(double value, string symbol, double factor, double offset) =>
        (value, symbol, factor, offset);
}

/// <summary>The plain, JSON-serialisable parts a boxed <see cref="Quantity{TDimension}"/> decomposes into.</summary>
/// <param name="DimensionKind">The dimension's own name, as <see cref="ReferenceQuantityCodec.DimensionNameOf"/> returns it.</param>
/// <param name="Value">The numeric magnitude, in <paramref name="UnitSymbol"/>.</param>
/// <param name="UnitSymbol">The unit's own display symbol.</param>
/// <param name="UnitToBaseFactor">The unit's own multiplicative conversion factor.</param>
/// <param name="UnitToBaseOffset">The unit's own conversion offset — zero for every unit but an affine one (`ADR-0125`).</param>
public sealed record EncodedQuantity(
    string DimensionKind,
    double Value,
    string UnitSymbol,
    double UnitToBaseFactor,
    double UnitToBaseOffset = 0.0);
