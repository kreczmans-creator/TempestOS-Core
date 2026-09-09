namespace Tempest.Core.ReferenceData;

/// <summary>
/// The plain, JSON-serialisable envelope every Group A record is stored
/// as — the <see cref="EngineeringData.IDocumentRevision.Content"/> of its
/// own backing <see cref="EngineeringData.IEngineeringDocument"/>.
/// </summary>
/// <remarks>
/// A thin envelope around the domain's own definition type rather than a
/// parallel DTO graph mirroring each of its nested records (`ADR-0124`).
/// Every dimensioned value in a Group A definition is declared at a
/// statically-known dimension, so the canonical types are directly
/// serialisable and a hand-written parallel graph would add only the
/// opportunity for the two shapes to drift apart. Enums are written as
/// strings (see <see cref="ReferenceSerialisation.Options"/>), so adding
/// or reordering an enum member can never silently reinterpret an
/// already-stored record.
/// </remarks>
/// <typeparam name="TDefinition">The domain's own engineering description type.</typeparam>
/// <param name="RecordId">The record's own TempestOS identity.</param>
/// <param name="Definition">The domain engineering description.</param>
/// <param name="Provenance">Where the data came from.</param>
/// <param name="ValidationState">The record's own lifecycle position.</param>
/// <param name="SupersededByRecordId">The record that replaced this one, if any.</param>
/// <param name="Source">A structured citation of the exact line the record's own values were read from, if held (`ADR-0149`). Absent from content written before this field existed, which deserialises it as <see langword="null"/>.</param>
internal sealed record ReferenceDocumentDto<TDefinition>(
    string RecordId,
    TDefinition Definition,
    ReferenceProvenance Provenance,
    ReferenceValidationState ValidationState,
    string? SupersededByRecordId,
    SourceCitation? Source = null)
    where TDefinition : class;
