namespace Tempest.Core.EngineeringData;

/// <summary>
/// Thrown when an <see cref="IEngineeringDocumentStore"/> operation is
/// given a <c>documentId</c> that does not exist.
/// </summary>
public sealed class EngineeringDocumentNotFoundException : EngineeringDataException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="EngineeringDocumentNotFoundException"/> class.
    /// </summary>
    /// <param name="documentId">The document identity that does not exist.</param>
    public EngineeringDocumentNotFoundException(Guid documentId)
        : base($"No engineering document exists with Id '{documentId}'.")
    {
        DocumentId = documentId;
    }

    /// <summary>Gets the document identity that does not exist.</summary>
    public Guid DocumentId { get; }
}
