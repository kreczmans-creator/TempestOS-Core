namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The thermodynamic temperature dimension. Base unit: kelvin.</summary>
/// <remarks>Added by `Group A` (P01 Engineering Reference Data) — purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate. The first dimension in this framework with genuinely affine units (degrees Celsius, degrees Fahrenheit); see <see cref="Unit{TDimension}.ToBaseUnitOffset"/> and `ADR-0125`.</remarks>
public sealed class Temperature : IDimension
{
    private Temperature()
    {
    }
}
