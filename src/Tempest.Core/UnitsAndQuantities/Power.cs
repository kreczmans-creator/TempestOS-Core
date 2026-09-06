namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The power (energy per unit time) dimension. Base unit: watt.</summary>
/// <remarks>
/// Added by `Group A` (P01 Engineering Reference Data) — purely additive.
/// Needed by <c>Tempest.Core.Components</c>, where drive elements and
/// couplings are commonly rated in power rather than torque, and by
/// <c>Tempest.Core.Manufacturing</c> for a process's own published power.
/// </remarks>
public sealed class Power : IDimension
{
    private Power()
    {
    }
}
