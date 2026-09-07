namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The torque (moment of force) dimension. Base unit: newton metre.</summary>
/// <remarks>Added by `Group A` (P01 Engineering Reference Data) — purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate. Deliberately a dimension of its own rather than an alias of <see cref="Energy"/>: the two share base units dimensionally but are never interchangeable engineering quantities, and a tightening torque must never be assignable to an energy.</remarks>
public sealed class Torque : IDimension
{
    private Torque()
    {
    }
}
