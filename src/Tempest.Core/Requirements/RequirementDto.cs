namespace Tempest.Core.Requirements;

/// <summary>The plain, JSON-serializable shape a requirement is stored as — this is the <see cref="EngineeringData.IDocumentRevision.Content"/> of its own backing <see cref="EngineeringData.IEngineeringDocument"/>.</summary>
/// <remarks>
/// <c>Owner</c>/<c>Priority</c>/<c>IsDeleted</c>/<c>GroupId</c> are `WP 9.1A`
/// additions — appended with default values so every existing call site
/// constructing this record positionally is unaffected. <c>GroupId</c> is
/// this record's own live, current group membership — the same
/// direct-storage fix `RequirementGroupDto.ParentGroupId` needed, applied
/// here for the identical reason: a requirement grouped and later moved
/// would otherwise need a second <c>groupedUnder</c> relationship link
/// resolved by unordered iteration, exactly the class of risk `WP 9.0B`'s
/// own `TD-27` already found and fixed for a different repository. The
/// <c>groupedUnder</c> relationship link is still recorded on every
/// group/move, for Digital Thread compatibility — it stops being the
/// *resolution* mechanism, without stopping being part of the historical
/// record.
/// </remarks>
internal sealed record RequirementDto(
    string Identifier,
    string Statement,
    string? Category,
    RequirementStatus Status,
    string CreatedByPrincipalId,
    DateTimeOffset CreatedAt,
    string? Owner = null,
    RequirementPriority? Priority = null,
    bool IsDeleted = false,
    Guid? GroupId = null);
