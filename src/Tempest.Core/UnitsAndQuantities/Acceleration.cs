namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The acceleration dimension. Base unit: metre per second squared.</summary>
/// <remarks>Added by `Group A` (P01 Engineering Reference Data) — purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate. Needed by <c>Tempest.Core.Constants</c> for standard gravity.</remarks>
public sealed class Acceleration : IDimension
{
    private Acceleration()
    {
    }
}
