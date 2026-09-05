namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The rotational-speed (rotational frequency) dimension. Base unit: revolution per second.</summary>
/// <remarks>
/// Added by `WP A4` (Bearing Library) — the eighth dimension in this
/// framework, purely additive exactly as <see cref="LengthUnits"/>'s own
/// "starting set, extensible" remarks anticipate. Bearing reference data
/// quotes limiting and reference speeds in rev/min; storing those as bare
/// <see cref="double"/> would have been the one place A4 abandoned this
/// framework, so the dimension is introduced here rather than worked
/// around.
/// </remarks>
public sealed class RotationalSpeed : IDimension
{
    private RotationalSpeed()
    {
    }
}
