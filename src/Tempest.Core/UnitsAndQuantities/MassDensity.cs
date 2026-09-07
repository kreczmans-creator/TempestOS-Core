namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The mass-density dimension. Base unit: kilogram per cubic metre.</summary>
/// <remarks>Added by `Group A` (P01 Engineering Reference Data) — purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate. Needed by <c>Tempest.Core.Materials</c>, which cannot record a material's own density without it.</remarks>
public sealed class MassDensity : IDimension
{
    private MassDensity()
    {
    }
}
