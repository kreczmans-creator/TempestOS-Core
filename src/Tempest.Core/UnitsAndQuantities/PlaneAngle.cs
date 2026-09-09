namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The plane-angle dimension. Base unit: radian.</summary>
/// <remarks>
/// Added by `WP A4` (Bearing Library) alongside <see cref="RotationalSpeed"/>,
/// for the same reason: an angular-contact or tapered roller bearing's own
/// nominal contact angle is a dimensioned engineering value, not a number
/// with a degree sign in a string.
/// </remarks>
public sealed class PlaneAngle : IDimension
{
    /// <summary>The runtime dimension vector for PlaneAngle â see <see cref="Dimensions.PlaneAngle"/>.</summary>
    public static Dimension Vector => Dimensions.PlaneAngle;

    private PlaneAngle()
    {
    }
}
