namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The pressure dimension. Base unit: pascal.</summary>
public sealed class Pressure : IDimension
{
    /// <summary>The runtime dimension vector for Pressure â see <see cref="Dimensions.Pressure"/>.</summary>
    public static Dimension Vector => Dimensions.Pressure;

    private Pressure()
    {
    }
}
