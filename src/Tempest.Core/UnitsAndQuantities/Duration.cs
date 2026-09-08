namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// The time dimension. Base unit: second. Named <see cref="Duration"/>,
/// not <c>Time</c>, to avoid ambiguity with <see cref="System.DateTime"/>/<see cref="System.TimeSpan"/>
/// in consuming code.
/// </summary>
public sealed class Duration : IDimension
{
    /// <summary>The runtime dimension vector for Duration â see <see cref="Dimensions.Duration"/>.</summary>
    public static Dimension Vector => Dimensions.Duration;

    private Duration()
    {
    }
}
