namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The length dimension. Base unit: metre.</summary>
public sealed class Length : IDimension
{
    /// <summary>The runtime dimension vector for Length â see <see cref="Dimensions.Length"/>.</summary>
    public static Dimension Vector => Dimensions.Length;

    private Length()
    {
    }
}
