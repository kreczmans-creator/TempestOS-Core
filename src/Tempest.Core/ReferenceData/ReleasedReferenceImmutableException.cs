namespace Tempest.Core.ReferenceData;

/// <summary>
/// Thrown when a caller attempts to revise the engineering content of a
/// record that is <see cref="ReferenceValidationState.Released"/> or
/// <see cref="ReferenceValidationState.Superseded"/>.
/// </summary>
/// <remarks>
/// Released engineering reference data is immutable by design. Downstream
/// calculations, verifications and baselines have already consumed the
/// released values; silently changing them underneath would make every
/// consumer's own record of what it used a lie. The supported path is
/// supersession — register the corrected record and supersede the old one,
/// so both survive and the change is traceable.
/// </remarks>
public sealed class ReleasedReferenceImmutableException : ReferenceDataException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ReleasedReferenceImmutableException"/> class.
    /// </summary>
    /// <param name="library">The reference library.</param>
    /// <param name="recordId">The record whose revision was refused.</param>
    /// <param name="state">The record's own current state.</param>
    public ReleasedReferenceImmutableException(string library, string recordId, ReferenceValidationState state)
        : base(library, $"{library} record '{recordId}' is '{state}' and cannot be revised. Register the corrected record and supersede this one instead.")
    {
        RecordId = recordId;
        State = state;
    }

    /// <summary>Gets the record whose revision was refused.</summary>
    public string RecordId { get; }

    /// <summary>Gets the record's own current state.</summary>
    public ReferenceValidationState State { get; }
}
