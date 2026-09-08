namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The electric-current dimension. Base unit: ampere. One of the seven SI base quantities.</summary>
/// <remarks>Added by `WP 17.3A` (`ADR-0147`) as the base of this framework's own new electrical set (<see cref="Voltage"/>, <see cref="Resistance"/>, <see cref="ElectricCharge"/>).</remarks>
public sealed class ElectricCurrent : IDimension
{
    /// <summary>The runtime dimension vector for <see cref="ElectricCurrent"/> — see <see cref="Dimensions.ElectricCurrent"/>.</summary>
    public static Dimension Vector => Dimensions.ElectricCurrent;

    private ElectricCurrent()
    {
    }
}
