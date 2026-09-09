namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The electrical-resistance dimension. Base unit: ohm.</summary>
/// <remarks>Added by `WP 17.3A` (`ADR-0147`), part of this framework's own new electrical set.</remarks>
public sealed class Resistance : IDimension
{
    /// <summary>The runtime dimension vector for <see cref="Resistance"/> — see <see cref="Dimensions.Resistance"/>.</summary>
    public static Dimension Vector => Dimensions.Resistance;

    private Resistance()
    {
    }
}
