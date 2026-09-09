namespace Tempest.Core.ReferenceData;

/// <summary>
/// Thrown when a write would leave two records in one library sharing a
/// secondary key the library enforces as unique — a manufacturer and part
/// number, a standard designation, a constant's own symbol.
/// </summary>
/// <remarks>
/// Distinct from <see cref="DuplicateReferenceRecordException"/>, which is
/// about TempestOS identity. This one guards the identity the outside
/// world uses: if two records answer to one designation, a consumer
/// resolving that designation has no way to know which record it got.
/// </remarks>
public sealed class DuplicateReferenceKeyException : ReferenceDataException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateReferenceKeyException"/> class.
    /// </summary>
    /// <param name="library">The reference library.</param>
    /// <param name="keyDescription">A human-readable description of the key (e.g. <c>"manufacturer 'X' part number 'Y'"</c>).</param>
    /// <param name="existingRecordId">The record already holding that key.</param>
    public DuplicateReferenceKeyException(string library, string keyDescription, string existingRecordId)
        : base(library, $"{keyDescription} is already registered as {library} record '{existingRecordId}'.")
    {
        ExistingRecordId = existingRecordId;
    }

    /// <summary>Gets the record already holding that key.</summary>
    public string ExistingRecordId { get; }
}
