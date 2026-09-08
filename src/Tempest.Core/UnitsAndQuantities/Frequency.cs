namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The frequency dimension. Base unit: hertz.</summary>
/// <remarks>
/// Added by `WP 17.3A` (`ADR-0147`). Shares its dimension vector with
/// <see cref="RotationalSpeed"/> (T⁻¹) — a cyclic rate is a cyclic rate
/// dimensionally, but an electrical or vibration frequency counted in
/// cycles per second is a different engineering quantity from a shaft's
/// own rotational speed counted in revolutions per second, and this
/// framework's phantom-typed dimensions exist to keep the two from being
/// assigned to one another by mistake (`ADR-0054`).
/// </remarks>
public sealed class Frequency : IDimension
{
    /// <summary>The runtime dimension vector for <see cref="Frequency"/> — see <see cref="Dimensions.Frequency"/>.</summary>
    public static Dimension Vector => Dimensions.Frequency;

    private Frequency()
    {
    }
}
