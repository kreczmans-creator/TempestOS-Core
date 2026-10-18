namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The heat-flux dimension. Base unit: watt per square metre.</summary>
/// <remarks>Added for the plane-wall heat transfer module's result. Purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate.</remarks>
public sealed class HeatFlux : IDimension
{
    /// <summary>The runtime dimension vector for HeatFlux — see <see cref="Dimensions.HeatFlux"/>.</summary>
    public static Dimension Vector => Dimensions.HeatFlux;

    private HeatFlux()
    {
    }
}
