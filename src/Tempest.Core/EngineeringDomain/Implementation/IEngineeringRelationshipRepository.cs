namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// Records the <see cref="RelationshipCategory"/>/<see cref="IEngineeringRelationship.CreatedByPrincipalId"/>/<see cref="IEngineeringRelationship.CreatedAt"/>
/// metadata <see cref="IEngineeringRelationship"/> requires but <see cref="EngineeringData.DocumentReference"/> does not carry — a disclosed,
/// implementation-time contract gap (WP 8.2C). <see cref="EngineeringData.IEngineeringDocumentStore.LinkAsync"/> remains the authoritative
/// record of whether a link exists (ADR-0073); this repository only supplements it with the richer shape.
/// </summary>
public interface IEngineeringRelationshipRepository
{
    void Record(IEngineeringRelationship relationship);
    Task<IReadOnlyList<IEngineeringRelationship>> GetOutgoingAsync(Guid sourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IEngineeringRelationship>> GetIncomingAsync(Guid targetId, CancellationToken cancellationToken = default);
}
