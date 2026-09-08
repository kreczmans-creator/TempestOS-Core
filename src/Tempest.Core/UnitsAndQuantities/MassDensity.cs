namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The mass-density dimension. Base unit: kilogram per cubic metre.</summary>
/// <remarks>Added by `Group A` (P01 Engineering Reference Data) — purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate. Needed by <c>Tempest.Core.Materials</c>, which cannot record a material's own density without it.</remarks>
public sealed class MassDensity : IDimension
{
    /// <summary>The runtime dimension vector for MassDensity â see <see cref="Dimensions.MassDensity"/>.</summary>
    public static Dimension Vector => Dimensions.MassDensity;

    private MassDensity()
    {
    }
}
