namespace Tempest.Core.ReferenceData.Seeding;

/// <summary>
/// A named, versioned set of records one reference library can be
/// populated from.
/// </summary>
/// <remarks>
/// <para>
/// <b>A dataset, not an importer.</b> A seed reads nothing, parses
/// nothing, and reaches nothing outside the process: it hands back records
/// it already holds, and
/// <see cref="ReferenceSeedService"/> writes them through the library's own
/// ordinary <see cref="IReferenceDataCatalog{TDefinition}"/>. There is no
/// second persistence mechanism and no extraction pipeline, because a
/// seed of this size needs neither.
/// </para>
/// <para>
/// <b>Why the records are code rather than a data file.</b> The
/// definitions these datasets build are strongly dimensioned — a yield
/// strength is a <c>Quantity&lt;Pressure&gt;</c>, not a number and a string
/// — so expressing them in the language the units live in means the
/// compiler checks every unit, every property name's expected dimension
/// and every enumeration member. A JSON seed would need a schema, a
/// parser and a unit resolver to reach the same place, and would fail at
/// run time where this fails at build time.
/// </para>
/// </remarks>
/// <typeparam name="TDefinition">The library's own definition type.</typeparam>
public interface IReferenceSeed<TDefinition>
{
    /// <summary>The dataset's own name, as reports and governance refer to it.</summary>
    string DatasetName { get; }

    /// <summary>
    /// The dataset's own revision. Incremented when its content changes, so
    /// a report can say which revision of the seed a library was populated
    /// from.
    /// </summary>
    int DatasetRevision { get; }

    /// <summary>Every record this dataset offers, in a fixed order.</summary>
    IReadOnlyList<ReferenceSeedRecord<TDefinition>> Records { get; }

    /// <summary>
    /// Whether this dataset is shipped reference data the Product Owner has
    /// ruled must be usable from day one (PO decision 2026-10-01), and so is
    /// released at seed when the seeding service carries a
    /// <see cref="ReferenceSeedReleasePolicy"/>. False by default: authored
    /// engineering assets, demonstration content and anything not named by
    /// that decision stay Draft until a person releases them.
    /// </summary>
    bool ReleaseAtSeed => false;
}
