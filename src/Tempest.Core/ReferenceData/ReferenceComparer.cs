using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData;

/// <summary>
/// The shared mechanics of building a <see cref="ReferenceComparisonResult"/>.
/// </summary>
/// <remarks>
/// Pure and synchronous — it reads only the records it is given, touches
/// no store, and is usable anywhere from a workspace view to a future
/// selection engine. Each library supplies its own property list and its
/// own cell projection; everything else about comparing records is the
/// same in every domain and lives here.
/// </remarks>
public static class ReferenceComparer
{
    /// <summary>
    /// Compares <paramref name="records"/> across <paramref name="properties"/>,
    /// projecting each cell with <paramref name="cellFor"/>.
    /// </summary>
    /// <typeparam name="TDefinition">The domain's own engineering description type.</typeparam>
    /// <param name="records">The records to compare, in the order the result should present them.</param>
    /// <param name="properties">The property keys to compare across, in row order.</param>
    /// <param name="cellFor">Projects one record's own value for one property.</param>
    /// <param name="familyOf">The record's own family, for the single-family flag. <see langword="null"/> where the domain has no family concept.</param>
    /// <exception cref="ArgumentNullException">Any required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="records"/> is empty or contains a <see langword="null"/>.</exception>
    public static ReferenceComparisonResult Compare<TDefinition>(
        IReadOnlyList<IReferenceRecord<TDefinition>> records,
        IReadOnlyList<string> properties,
        Func<IReferenceRecord<TDefinition>, string, ReferenceComparisonCell> cellFor,
        Func<IReferenceRecord<TDefinition>, string>? familyOf = null)
        where TDefinition : class
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(cellFor);

        if (records.Count == 0)
            throw new ArgumentException("A comparison needs at least one record.", nameof(records));

        if (records.Any(record => record is null))
            throw new ArgumentException("A comparison cannot include a null record.", nameof(records));

        var rows = properties
            .Select(property => new ReferenceComparisonRow(
                property,
                records.Select(record => cellFor(record, property)).ToList()))
            .ToList();

        return new ReferenceComparisonResult(records.Select(record => record.Id).ToList(), rows)
        {
            IsSingleFamily = familyOf is null || records.Select(familyOf).Distinct(StringComparer.Ordinal).Count() == 1,
        };
    }

    /// <summary>A cell for a dimensioned value, carrying both its own display form and its canonical value for ordering.</summary>
    public static ReferenceComparisonCell Dimensioned<TDimension>(Quantity<TDimension>? quantity)
        where TDimension : IDimension =>
        quantity is null
            ? ReferenceComparisonCell.NotRecorded
            : new ReferenceComparisonCell(ReferencePropertyAvailability.Recorded, quantity.Value.ToString(), quantity.Value.BaseValue);

    /// <summary>A cell for a sourced engineering value.</summary>
    public static ReferenceComparisonCell Sourced<TDimension>(ReferenceValue<TDimension>? value)
        where TDimension : IDimension =>
        value is null
            ? ReferenceComparisonCell.NotRecorded
            : new ReferenceComparisonCell(ReferencePropertyAvailability.Recorded, value.Value.ToString(), value.CanonicalValue);

    /// <summary>A cell for a sourced engineering range, displayed as its own two ends.</summary>
    public static ReferenceComparisonCell Ranged<TDimension>(ReferenceRange<TDimension>? range)
        where TDimension : IDimension
    {
        if (range is null || !range.IsRecorded)
            return ReferenceComparisonCell.NotRecorded;

        var display = (range.Minimum, range.Maximum) switch
        {
            ({ } min, { } max) => $"{min} to {max}",
            ({ } min, null) => $"{min} or more",
            (null, { } max) => $"up to {max}",
            _ => null,
        };

        // A range orders by its own lower end where it has one, so a
        // reader sorting candidates gets the answer they expect; a
        // maximum-only range orders by that maximum instead.
        var canonical = range.Minimum?.BaseValue ?? range.Maximum?.BaseValue;

        return new ReferenceComparisonCell(ReferencePropertyAvailability.Recorded, display, canonical);
    }
}
