namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The force dimension. Base unit: newton.</summary>
public sealed class Force : IDimension
{
    /// <summary>The runtime dimension vector for Force â see <see cref="Dimensions.Force"/>.</summary>
    public static Dimension Vector => Dimensions.Force;

    private Force()
    {
    }
}
