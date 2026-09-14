using Tempest.Core.ReferenceData;

namespace Tempest.Core.Evidence;

/// <summary>
/// One governed reference record this evidence stood on, pinned to the
/// revision held at citation time (`ADR-0148`).
/// </summary>
/// <param name="Pin">The library, record and revision cited — minted from the record actually read, never caller-supplied (mirrors <see cref="ReferencePin.For{TDefinition}"/>'s own discipline).</param>
/// <param name="RecordDisplayName">The record's own identity within its library, for display — its <c>IReferenceRecord{TDefinition}.Id</c> (the five governed libraries assign no separate friendly name).</param>
/// <param name="SourceCitationSnapshot">
/// A snapshot of the record's own structured source citation (publisher,
/// work, edition, page, table or figure, row or entry) at the moment this
/// evidence cited it — <see langword="null"/> until the reference-data
/// libraries carry a structured <c>SourceCitation</c> of their own
/// (`ADR-0149`, a sibling Work Package not yet landed in this build). A
/// citation is never refused or degraded for the snapshot's absence; it is
/// simply not yet there to take.
/// </param>
public sealed record EvidenceCitation(ReferencePin Pin, string RecordDisplayName, string? SourceCitationSnapshot);
