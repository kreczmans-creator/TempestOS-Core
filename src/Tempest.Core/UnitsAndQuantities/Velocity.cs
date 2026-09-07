namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The velocity dimension. Base unit: metre per second.</summary>
/// <remarks>Added by `Group A` (P01 Engineering Reference Data) — purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate. Needed by <c>Tempest.Core.Manufacturing</c> for process speeds.</remarks>
public sealed class Velocity : IDimension
{
    private Velocity()
    {
    }
}
