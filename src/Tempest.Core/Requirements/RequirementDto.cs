namespace Tempest.Core.Requirements;

/// <summary>The plain, JSON-serializable shape a requirement is stored as — this is the <see cref="EngineeringData.IDocumentRevision.Content"/> of its own backing <see cref="EngineeringData.IEngineeringDocument"/>.</summary>
internal sealed record RequirementDto(
    string Identifier,
    string Statement,
    string? Category,
    RequirementStatus Status,
    string CreatedByPrincipalId,
    DateTimeOffset CreatedAt);
