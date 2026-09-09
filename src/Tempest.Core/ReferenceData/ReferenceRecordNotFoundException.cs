namespace Tempest.Core.ReferenceData;

/// <summary>
/// Thrown when an operation requiring an existing reference record is
/// given an identity that does not exist.
/// </summary>
/// <remarks>
/// A catalogue's own <c>FindAsync</c> never throws this — a nullable
/// return is used there instead, since "not found" is an ordinary,
/// expected outcome for a catalogue lookup.
/// </remarks>
public sealed class ReferenceRecordNotFoundException : ReferenceDataException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ReferenceRecordNotFoundException"/> class.
    /// </summary>
    /// <param name="library">The reference library the record was sought in.</param>
    /// <param name="recordId">The identity that does not exist.</param>
    public ReferenceRecordNotFoundException(string library, string recordId)
        : base(library, $"No {library} record is registered with Id '{recordId}'.")
    {
        RecordId = recordId;
    }

    /// <summary>Gets the identity that does not exist.</summary>
    public string RecordId { get; }
}
