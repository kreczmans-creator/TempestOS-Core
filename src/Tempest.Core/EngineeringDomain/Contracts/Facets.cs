namespace Tempest.Core.EngineeringDomain;

public interface IHasBusinessIdentifier
{
    string? Identifier { get; }
    string DisplayName { get; }
}

public interface IHasMetadata
{
    string? Category { get; }
    string? Discipline { get; }
    string? Owner { get; }
    IReadOnlyList<string> Tags { get; }
    string? Classification { get; }
    string? Notes { get; }
}

public interface IHasLifecycle
{
    LifecycleState Status { get; }
    IReadOnlyList<ILifecycleTransitionRecord> History { get; }
    Task TransitionAsync(LifecycleState target, CancellationToken cancellationToken = default);
}

/// <summary>A single, immutable content revision — a same-shape analogue of <see cref="EngineeringData.IDocumentRevision"/>, scoped to one object's own history. Not defined by <c>WP8.2B Interface Catalogue.md</c> despite being referenced by <see cref="IHasRevisions.GetRevisionHistoryAsync"/> — a disclosed, implementation-time contract gap closed here (WP 8.2C).</summary>
public interface IRevisionRecord
{
    int RevisionNumber { get; }
    string Content { get; }
    string? ChangeSummary { get; }
    string AuthorPrincipalId { get; }
    DateTimeOffset CreatedAt { get; }
}

public interface IHasRevisions
{
    string Content { get; }
    string AuthorPrincipalId { get; }
    Task<IHasRevisions> ReviseAsync(string newContent, string? changeSummary, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IRevisionRecord>> GetRevisionHistoryAsync(CancellationToken cancellationToken = default);
}

public interface IHasRelationships
{
    Task LinkAsync(Guid targetId, string relationshipKind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IEngineeringRelationship>> GetRelationshipsAsync(CancellationToken cancellationToken = default);
}

public interface ITraceable
{
    Task<IEvidence> GetEvidenceAsync(CancellationToken cancellationToken = default);
}

public interface IValidatable
{
    Task<IValidationResult> ValidateAsync(CancellationToken cancellationToken = default);
}

public interface IHasAttachments
{
    Task AttachAsync(IAttachment attachment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IAttachment>> GetAttachmentsAsync(CancellationToken cancellationToken = default);
}

public interface ISearchable
{
    string SearchableText { get; }
}
