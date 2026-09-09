namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The area dimension. Base unit: square metre.</summary>
public sealed class Area : IDimension
{
    /// <summary>The runtime dimension vector for Area â see <see cref="Dimensions.Area"/>.</summary>
    public static Dimension Vector => Dimensions.Area;

    private Area()
    {
    }
}
