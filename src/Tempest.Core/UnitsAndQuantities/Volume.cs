namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The volume dimension. Base unit: cubic metre.</summary>
public sealed class Volume : IDimension
{
    /// <summary>The runtime dimension vector for Volume â see <see cref="Dimensions.Volume"/>.</summary>
    public static Dimension Vector => Dimensions.Volume;

    private Volume()
    {
    }
}
