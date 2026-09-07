namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The translational-stiffness (force per unit deflection) dimension. Base unit: newton per metre.</summary>
/// <remarks>Added by `Group A` (P01 Engineering Reference Data) — purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate. Needed by <c>Tempest.Core.Components</c> for a spring rate.</remarks>
public sealed class Stiffness : IDimension
{
    private Stiffness()
    {
    }
}
