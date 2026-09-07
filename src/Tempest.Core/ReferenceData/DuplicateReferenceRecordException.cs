namespace Tempest.Core.ReferenceData;

/// <summary>Thrown when a registration is given an identity that is already registered in the same library.</summary>
public sealed class DuplicateReferenceRecordException : ReferenceDataException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateReferenceRecordException"/> class.
    /// </summary>
    /// <param name="library">The reference library.</param>
    /// <param name="recordId">The identity that is already registered.</param>
    public DuplicateReferenceRecordException(string library, string recordId)
        : base(library, $"A {library} record is already registered with Id '{recordId}'.")
    {
        RecordId = recordId;
    }

    /// <summary>Gets the identity that is already registered.</summary>
    public string RecordId { get; }
}
