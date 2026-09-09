namespace Tempest.Core.ReferenceData;

/// <summary>
/// Thrown when a <see cref="ReferenceValidationState"/> transition is
/// refused because the record's own provenance does not yet support it.
/// </summary>
/// <remarks>
/// The enforcement point for P01's own central rule: reference data earns
/// its status from its provenance, never from a caller asserting one. See
/// <see cref="ReferenceValidationStates.DescribeProvenanceShortfall"/>.
/// </remarks>
public sealed class ReferenceProvenanceIncompleteException : ReferenceDataException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ReferenceProvenanceIncompleteException"/> class.
    /// </summary>
    /// <param name="library">The reference library.</param>
    /// <param name="recordId">The record whose transition was refused.</param>
    /// <param name="requestedState">The state that was requested.</param>
    /// <param name="reason">What the provenance is missing.</param>
    public ReferenceProvenanceIncompleteException(string library, string recordId, ReferenceValidationState requestedState, string reason)
        : base(library, $"{library} record '{recordId}' cannot become '{requestedState}': {reason}")
    {
        RecordId = recordId;
        RequestedState = requestedState;
    }

    /// <summary>Gets the record whose transition was refused.</summary>
    public string RecordId { get; }

    /// <summary>Gets the state that was requested.</summary>
    public ReferenceValidationState RequestedState { get; }
}
