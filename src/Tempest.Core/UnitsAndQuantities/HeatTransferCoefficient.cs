namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The heat-transfer-coefficient dimension. Base unit: watt per square metre kelvin.</summary>
/// <remarks>
/// Added for the plane-wall heat transfer module: a convection film
/// coefficient h, or an overall coefficient U. Purely additive, exactly as
/// <see cref="LengthUnits"/>'s own "starting set, extensible" remarks
/// anticipate. Per temperature <em>interval</em>, so purely multiplicative.
/// </remarks>
public sealed class HeatTransferCoefficient : IDimension
{
    /// <summary>The runtime dimension vector for HeatTransferCoefficient — see <see cref="Dimensions.HeatTransferCoefficient"/>.</summary>
    public static Dimension Vector => Dimensions.HeatTransferCoefficient;

    private HeatTransferCoefficient()
    {
    }
}
