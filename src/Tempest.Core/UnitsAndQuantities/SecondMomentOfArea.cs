namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The second-moment-of-area (area moment of inertia) dimension. Base unit: metre to the fourth power.</summary>
/// <remarks>Added by `WP 17.3A` (`ADR-0147`) — a beam or bracket section's own bending stiffness cannot be recorded without it, and every existing dimension this framework held stopped one power of length short.</remarks>
public sealed class SecondMomentOfArea : IDimension
{
    /// <summary>The runtime dimension vector for <see cref="SecondMomentOfArea"/> — see <see cref="Dimensions.SecondMomentOfArea"/>.</summary>
    public static Dimension Vector => Dimensions.SecondMomentOfArea;

    private SecondMomentOfArea()
    {
    }
}
