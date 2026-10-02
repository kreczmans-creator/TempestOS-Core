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

    /// <summary>
    /// The shipped record was already present exactly as an earlier seed
    /// left it — still <see cref="ReferenceValidationState.Draft"/>, never
    /// revised, never verified, from the same source organisation — and was
    /// brought up to the current shipped dataset before being released
    /// (PO decision 2026-10-01). A record any person has touched is never
    /// refreshed; it is <see cref="AlreadyPresent"/>.
    /// </summary>
    Refreshed,

    /// <summary>
    /// The record was absent, but another record already holds the secondary
    /// key (designation, part number) it would take. Nothing was written —
    /// the other record, whoever wrote it, wins.
    /// </summary>
    KeyConflict,
}

/// <summary>The outcome of applying one seed record.</summary>
/// <param name="RecordId">The record's own identity.</param>
/// <param name="Action">What applying it did.</param>
/// <param name="Released">
/// Whether this run also released the record for engineering use through
/// <see cref="ReferenceSeedReleasePolicy"/>.
/// </param>
public sealed record ReferenceSeedEntry(string RecordId, ReferenceSeedAction Action, bool Released = false);

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

    /// <summary>How many untouched shipped records this run brought up to the current dataset.</summary>
    public int RefreshedCount => Entries.Count(e => e.Action == ReferenceSeedAction.Refreshed);

    /// <summary>How many records were refused because another record already held their secondary key.</summary>
    public int KeyConflictCount => Entries.Count(e => e.Action == ReferenceSeedAction.KeyConflict);

    /// <summary>How many records this run released for engineering use.</summary>
    public int ReleasedCount => Entries.Count(e => e.Released);

    /// <summary>
    /// Whether this run changed nothing — the state a second run over an
    /// already-seeded library reaches, and the property that makes seeding
    /// safe to repeat.
    /// </summary>
    public bool MadeNoChange => RegisteredCount == 0 && RefreshedCount == 0 && ReleasedCount == 0;
}
