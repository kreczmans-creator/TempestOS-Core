namespace Tempest.Core.Materials;

/// <summary>The plain, JSON-serializable shape a material specification is stored as — this is the <see cref="EngineeringData.IDocumentRevision.Content"/> of its own backing <see cref="EngineeringData.IEngineeringDocument"/>.</summary>
internal sealed record MaterialSpecificationDto(
    string MaterialId,
    string Name,
    string? Category,
    IReadOnlyDictionary<string, MaterialPropertyDto> Properties);
