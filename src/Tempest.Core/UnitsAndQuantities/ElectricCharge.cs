namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The electric-charge dimension. Base unit: coulomb.</summary>
/// <remarks>Added by `WP 17.3A` (`ADR-0147`), part of this framework's own new electrical set.</remarks>
public sealed class ElectricCharge : IDimension
{
    /// <summary>The runtime dimension vector for <see cref="ElectricCharge"/> — see <see cref="Dimensions.ElectricCharge"/>.</summary>
    public static Dimension Vector => Dimensions.ElectricCharge;

    private ElectricCharge()
    {
    }
}
