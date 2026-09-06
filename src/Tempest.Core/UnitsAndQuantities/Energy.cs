namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The energy dimension. Base unit: joule.</summary>
/// <remarks>Added by `Group A` (P01 Engineering Reference Data) — purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate. Needed by <c>Tempest.Core.Materials</c> for impact energy, and by <c>Tempest.Core.Constants</c>.</remarks>
public sealed class Energy : IDimension
{
    private Energy()
    {
    }
}
