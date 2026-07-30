namespace Tempest.Core.EngineeringData;

/// <summary>
/// Identity and current-revision pointer for a document tracked by
/// <see cref="IEngineeringDocumentStore"/>.
/// </summary>
/// <remarks>
/// Carries no <c>Content</c> of its own — content lives only on
/// <see cref="IDocumentRevision"/>, since a document's content changes
/// with every revision while its identity does not.
/// </remarks>
public interface IEngineeringDocument
{
    /// <summary>Gets the document's permanent identity.</summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the caller-declared kind (e.g. <c>"Requirement"</c>,
    /// <c>"MaterialSpecification"</c>) — opaque to this namespace,
    /// interpreted only by the calling consumer.
    /// </summary>
    string Kind { get; }

    /// <summary>Gets the revision number of this document's current (most recent) revision.</summary>
    int CurrentRevisionNumber { get; }

    /// <summary>Gets the instant this document was first created.</summary>
    DateTimeOffset CreatedAt { get; }
}
