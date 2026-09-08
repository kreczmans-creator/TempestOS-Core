namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="SectionModulus"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class SectionModulusUnits
{
    /// <summary>The base unit of <see cref="SectionModulus"/> (SI, derived).</summary>
    public static readonly Unit<SectionModulus> MetreCubed = new("m^3", 1.0);

    /// <summary>SI — the unit a rolled-section catalogue most often quotes.</summary>
    public static readonly Unit<SectionModulus> MillimetreCubed = new("mm^3", 1e-9);

    /// <summary>SI.</summary>
    public static readonly Unit<SectionModulus> CentimetreCubed = new("cm^3", 1e-6);

    /// <summary>Imperial.</summary>
    public static readonly Unit<SectionModulus> InchCubed = new("in^3", 1.6387064e-5);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<SectionModulus>> All { get; } =
        [MetreCubed, MillimetreCubed, CentimetreCubed, InchCubed];
}
