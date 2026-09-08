namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The section-modulus dimension. Base unit: metre cubed.</summary>
/// <remarks>
/// Added by `WP 17.3A` (`ADR-0147`). Shares its dimension vector with
/// <see cref="Volume"/> (L³) — deliberately a dimension of its own, in
/// exactly the same spirit as <see cref="Torque"/> alongside
/// <see cref="Energy"/>: a section modulus and a volume are never
/// interchangeable engineering quantities, and the compile-time facade
/// must keep a bending calculation from being handed a tank capacity by
/// mistake.
/// </remarks>
public sealed class SectionModulus : IDimension
{
    /// <summary>The runtime dimension vector for <see cref="SectionModulus"/> — see <see cref="Dimensions.SectionModulus"/>.</summary>
    public static Dimension Vector => Dimensions.SectionModulus;

    private SectionModulus()
    {
    }
}
