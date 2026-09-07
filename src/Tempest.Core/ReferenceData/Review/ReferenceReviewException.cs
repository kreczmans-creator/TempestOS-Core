namespace Tempest.Core.ReferenceData.Review;

/// <summary>
/// A governed review or release was refused.
/// </summary>
/// <remarks>
/// Distinct from <see cref="ReferenceProvenanceIncompleteException"/>, which
/// says a record's provenance cannot support a state. This says the
/// <em>act</em> was not permitted — nobody was signed in to perform it, or
/// the person performing it is not entitled to.
/// </remarks>
public sealed class ReferenceReviewException : ReferenceDataException
{
    /// <summary>Initialises a new instance of the <see cref="ReferenceReviewException"/> class.</summary>
    /// <param name="library">The library the record belongs to.</param>
    /// <param name="recordId">The record the act was attempted on.</param>
    /// <param name="reason">Why the act was refused.</param>
    public ReferenceReviewException(string library, string recordId, string reason)
        : base(library, $"Review of {library} record '{recordId}' was refused: {reason}")
    {
        RecordId = recordId;
        Reason = reason;
    }

    /// <summary>The record the act was attempted on.</summary>
    public string RecordId { get; }

    /// <summary>Why the act was refused.</summary>
    public string Reason { get; }
}
