namespace Tempest.Core.EngineeringData;

/// <summary>
/// The JSON-serializable shape of one revision, stored in
/// <see cref="EngineeringDocumentStore.RevisionsCollectionName"/>, keyed
/// by <c>"{documentId:N}_{revisionNumber:D10}"</c>.
/// </summary>
internal sealed record DocumentRevisionDto(string Content, string? ChangeSummary, string AuthorPrincipalId, DateTimeOffset CreatedAt);
