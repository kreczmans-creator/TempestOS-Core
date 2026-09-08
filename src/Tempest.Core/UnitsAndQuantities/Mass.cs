namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The mass dimension. Base unit: kilogram.</summary>
public sealed class Mass : IDimension
{
    /// <summary>The runtime dimension vector for Mass â see <see cref="Dimensions.Mass"/>.</summary>
    public static Dimension Vector => Dimensions.Mass;

    private Mass()
    {
    }
}
