namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// The time dimension. Base unit: second. Named <see cref="Duration"/>,
/// not <c>Time</c>, to avoid ambiguity with <see cref="System.DateTime"/>/<see cref="System.TimeSpan"/>
/// in consuming code.
/// </summary>
public sealed class Duration : IDimension
{
    private Duration()
    {
    }
}
