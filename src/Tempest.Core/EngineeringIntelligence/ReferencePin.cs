namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// An exact, permanent identification of one reference-data record at one
/// revision — the mechanism that stops a later reference-data change
/// silently rewriting the meaning of an engineering decision already
/// taken.
/// </summary>
/// <remarks>
/// <para>
/// <b>Identity plus revision, never identity alone.</b> A record Id says
/// which material was used; it does not say which values that material
/// held at the time. `P01` already versions every record — each is an
/// <c>IEngineeringDocument</c> whose every revision is retained — so a pin
/// needs to carry nothing but the coordinates, and resolving it later is
/// a call to that library's own
/// <c>IReferenceDataCatalog&lt;T&gt;.GetRevisionAsync</c>. No revision
/// infrastructure is duplicated here.
/// </para>
/// <para>
/// <b>Why the library name is a string.</b> The seven `P01` catalogues are
/// each generic over their own definition type, so no single typed handle
/// spans them. <see cref="Library"/> is exactly
/// <c>IReferenceDataCatalog&lt;T&gt;.LibraryName</c>, and the caller that
/// resolves a pin is the one that already knows which catalogue it means.
/// A generic resolver would have to know all seven, which would make every
/// consumer depend on every library.
/// </para>
/// <para>
/// A pin is a statement about the past. It is never updated in place: an
/// assessment made against revision 3 stays pinned to revision 3 for as
/// long as the assessment exists, and re-running the assessment against a
/// later revision produces a new result with a new pin.
/// </para>
/// </remarks>
/// <param name="Library">The library the record belongs to, exactly as that catalogue names itself (e.g. <c>"Materials"</c>). Required.</param>
/// <param name="RecordId">The record's own TempestOS identity within that library. Required.</param>
/// <param name="RevisionNumber">The revision the assessment actually read. Must be at least 1.</param>
public sealed record ReferencePin(string Library, string RecordId, int RevisionNumber)
{
    /// <summary>The library the record belongs to.</summary>
    public string Library { get; } = string.IsNullOrWhiteSpace(Library)
        ? throw new ArgumentException("A reference pin must name the library the record belongs to.", nameof(Library))
        : Library.Trim();

    /// <summary>The record's own identity within that library.</summary>
    public string RecordId { get; } = string.IsNullOrWhiteSpace(RecordId)
        ? throw new ArgumentException("A reference pin must name the record it pins.", nameof(RecordId))
        : RecordId.Trim();

    /// <summary>The revision the assessment actually read.</summary>
    public int RevisionNumber { get; } = RevisionNumber >= 1
        ? RevisionNumber
        : throw new ArgumentOutOfRangeException(
            nameof(RevisionNumber),
            RevisionNumber,
            "A reference pin must name a real revision; revisions are numbered from 1. A record that has not been read cannot be pinned.");

    /// <summary>A stable, human-readable form: <c>Library/RecordId@Revision</c>.</summary>
    public override string ToString() => $"{Library}/{RecordId}@{RevisionNumber}";

    /// <summary>Pins a record this code has just read, taking the revision from the record itself rather than from a caller.</summary>
    /// <typeparam name="TDefinition">The library's own definition type.</typeparam>
    /// <param name="library">The library the record belongs to.</param>
    /// <param name="record">The record actually read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
    public static ReferencePin For<TDefinition>(string library, ReferenceData.IReferenceRecord<TDefinition> record)
        where TDefinition : class
    {
        ArgumentNullException.ThrowIfNull(record);

        return new ReferencePin(library, record.Id, record.RevisionNumber);
    }
}
