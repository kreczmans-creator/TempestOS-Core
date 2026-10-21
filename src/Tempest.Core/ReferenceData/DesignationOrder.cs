using System.Globalization;

namespace Tempest.Core.ReferenceData;

/// <summary>
/// The order a picker lists reference designations in: numbers compared as
/// numbers, so M3, M4, M5 … M10, M12 rather than M10, M12 … M3 (`v0.25.2`
/// PO request: standard parts smallest to largest).
/// </summary>
public static class DesignationOrder
{
    /// <summary>Compares designations with digit runs ordered by value.</summary>
    public static StringComparer Comparer { get; } = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering | CompareOptions.IgnoreCase);
}
