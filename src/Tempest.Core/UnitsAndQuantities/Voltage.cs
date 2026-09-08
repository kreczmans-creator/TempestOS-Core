namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The voltage (electric potential difference) dimension. Base unit: volt.</summary>
/// <remarks>Added by `WP 17.3A` (`ADR-0147`), part of this framework's own new electrical set.</remarks>
public sealed class Voltage : IDimension
{
    /// <summary>The runtime dimension vector for <see cref="Voltage"/> — see <see cref="Dimensions.Voltage"/>.</summary>
    public static Dimension Vector => Dimensions.Voltage;

    private Voltage()
    {
    }
}
