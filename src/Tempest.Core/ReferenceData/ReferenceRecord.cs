namespace Tempest.Core.ReferenceData;

/// <summary>The concrete <see cref="IReferenceRecord{TDefinition}"/> every Group A catalogue returns.</summary>
/// <typeparam name="TDefinition">The domain's own engineering description type.</typeparam>
public sealed class ReferenceRecord<TDefinition> : IReferenceRecord<TDefinition>
    where TDefinition : class
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ReferenceRecord{TDefinition}"/> class.
    /// </summary>
    /// <param name="id">The record's own TempestOS identity.</param>
    /// <param name="definition">The domain engineering description.</param>
    /// <param name="provenance">Where the data came from.</param>
    /// <param name="validationState">The record's own lifecycle position.</param>
    /// <param name="supersededByRecordId">The record that replaced this one, if any.</param>
    /// <param name="underlyingDocumentId">The backing document's Id.</param>
    /// <param name="revisionNumber">The backing document's current revision number.</param>
    /// <param name="source">A structured citation of the exact line the record's own values were read from, if held.</param>
    public ReferenceRecord(
        string id,
        TDefinition definition,
        ReferenceProvenance provenance,
        ReferenceValidationState validationState,
        string? supersededByRecordId,
        Guid underlyingDocumentId,
        int revisionNumber,
        SourceCitation? source = null)
    {
        Id = id;
        Definition = definition;
        Provenance = provenance;
        ValidationState = validationState;
        SupersededByRecordId = supersededByRecordId;
        UnderlyingDocumentId = underlyingDocumentId;
        RevisionNumber = revisionNumber;
        Source = source;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public TDefinition Definition { get; }

    /// <inheritdoc />
    public ReferenceProvenance Provenance { get; }

    /// <inheritdoc />
    public SourceCitation? Source { get; }

    /// <inheritdoc />
    public ReferenceValidationState ValidationState { get; }

    /// <inheritdoc />
    public string? SupersededByRecordId { get; }

    /// <inheritdoc />
    public Guid UnderlyingDocumentId { get; }

    /// <inheritdoc />
    public int RevisionNumber { get; }
}
