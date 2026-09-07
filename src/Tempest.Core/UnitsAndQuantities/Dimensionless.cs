namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The dimensionless quantity — a pure number, ratio or fraction. Base unit: the unit one.</summary>
/// <remarks>Added by `Group A` (P01 Engineering Reference Data) — purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate. Needed by <c>Tempest.Core.Constants</c>, which must be able to record a genuinely dimensionless constant as a quantity rather than as a bare <see cref="double"/>, and by every library recording a ratio.</remarks>
public sealed class Dimensionless : IDimension
{
    private Dimensionless()
    {
    }
}
