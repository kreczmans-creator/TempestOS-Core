namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The specific-heat-capacity dimension. Base unit: joule per kilogram kelvin.</summary>
/// <remarks>Added by `Group A` (P01 Engineering Reference Data) — purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate. Needed by <c>Tempest.Core.Materials</c> for a material's own thermal properties.</remarks>
public sealed class SpecificHeatCapacity : IDimension
{
    private SpecificHeatCapacity()
    {
    }
}
