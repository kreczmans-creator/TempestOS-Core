namespace Tempest.Core.ReferenceData.Seeding;

/// <summary>What applying one seed record did.</summary>
public enum ReferenceSeedAction
{
    /// <summary>The record was not present and was registered.</summary>
    Registered,

    /// <summary>
    /// A record already existed under this identity and was left exactly as
    /// it was. Seeding never overwrites: a library that has been edited,
    /// checked or released since it was seeded keeps that work.
    /// </summary>
    AlreadyPresent,
}

/// <summary>The outcome of applying one seed record.</summary>
/// <param name="RecordId">The record's own identity.</param>
/// <param name="Action">What applying it did.</param>
public sealed record ReferenceSeedEntry(string RecordId, ReferenceSeedAction Action);

/// <summary>The outcome of applying one dataset to one library.</summary>
/// <param name="LibraryName">The library that was seeded.</param>
/// <param name="DatasetName">The dataset that was applied.</param>
/// <param name="DatasetRevision">That dataset's own revision.</param>
/// <param name="Entries">One entry per record the dataset offered, in the dataset's own order.</param>
public sealed record ReferenceSeedOutcome(
    string LibraryName,
    string DatasetName,
    int DatasetRevision,
    IReadOnlyList<ReferenceSeedEntry> Entries)
{
    /// <summary>How many records this run actually registered.</summary>
    public int RegisteredCount => Entries.Count(e => e.Action == ReferenceSeedAction.Registered);

    /// <summary>How many records were already present and were therefore left alone.</summary>
    public int AlreadyPresentCount => Entries.Count(e => e.Action == ReferenceSeedAction.AlreadyPresent);

    /// <summary>
    /// Whether this run changed nothing — the state a second run over an
    /// already-seeded library reaches, and the property that makes seeding
    /// safe to repeat.
    /// </summary>
    public bool MadeNoChange => RegisteredCount == 0;
}
