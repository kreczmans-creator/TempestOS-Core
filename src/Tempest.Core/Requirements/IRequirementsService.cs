using Tempest.Core.EngineeringData;

namespace Tempest.Core.Requirements;

/// <summary>
/// Registers, retrieves, revises, and relates requirements — the
/// canonical entry point for the Systems Engineering Foundation. Each
/// requirement is itself an <see cref="IEngineeringDocument"/> of
/// <c>Kind = "Requirement"</c>; this service is an indexed, typed view
/// over that shared store, never a second storage mechanism
/// (<c>WP7.2C Requirements Platform Contracts.md</c> §1).
/// </summary>
public interface IRequirementsService
{
    /// <summary>Creates a new requirement with the given business identifier and statement.</summary>
    /// <exception cref="DuplicateRequirementIdentifierException"><paramref name="identifier"/> is already registered.</exception>
    /// <exception cref="ArgumentException"><paramref name="identifier"/> or <paramref name="statement"/> is null, empty, or whitespace.</exception>
    Task<IRequirement> CreateAsync(string identifier, string statement, string? category = null, CancellationToken cancellationToken = default);

    /// <summary>Returns the requirement, or <see langword="null"/> if none exists.</summary>
    Task<IRequirement?> FindAsync(Guid requirementId, CancellationToken cancellationToken = default);

    /// <summary>Returns the requirement registered under <paramref name="identifier"/>, or <see langword="null"/> if none is registered.</summary>
    /// <exception cref="ArgumentException"><paramref name="identifier"/> is null, empty, or whitespace.</exception>
    Task<IRequirement?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>Records a new revision of the requirement's own statement.</summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="requirementId"/> does not exist.</exception>
    /// <exception cref="ArgumentException"><paramref name="newStatement"/> is null, empty, or whitespace.</exception>
    Task<IRequirement> ReviseAsync(Guid requirementId, string newStatement, string? changeSummary, CancellationToken cancellationToken = default);

    /// <summary>Sets the requirement's own current lifecycle status.</summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="requirementId"/> does not exist.</exception>
    /// <exception cref="InvalidRequirementStatusTransitionException">The requested transition is not permitted from the requirement's own current status.</exception>
    Task SetStatusAsync(Guid requirementId, RequirementStatus status, CancellationToken cancellationToken = default);

    /// <summary>Records a typed, directed relationship from a requirement to another requirement, a group, a collection, or any other document.</summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="sourceRequirementId"/> does not exist.</exception>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="targetDocumentId"/> does not exist.</exception>
    /// <exception cref="ArgumentException"><paramref name="relationshipKind"/> is null, empty, or whitespace.</exception>
    Task LinkAsync(Guid sourceRequirementId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default);

    /// <summary>Every relationship recorded with <paramref name="requirementId"/> as its own source. Never <see langword="null"/>.</summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="requirementId"/> does not exist.</exception>
    Task<IReadOnlyList<DocumentReference>> GetRelationshipsAsync(Guid requirementId, CancellationToken cancellationToken = default);

    /// <summary>Every requirement currently registered. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IRequirement>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new, empty requirement collection.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or whitespace.</exception>
    Task<IRequirementCollection> CreateCollectionAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Returns the collection, or <see langword="null"/> if none exists.</summary>
    Task<IRequirementCollection?> FindCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default);

    /// <summary>Adds an existing requirement to an existing collection.</summary>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="collectionId"/> does not exist.</exception>
    /// <exception cref="RequirementNotFoundException"><paramref name="requirementId"/> does not exist.</exception>
    Task AddToCollectionAsync(Guid collectionId, Guid requirementId, CancellationToken cancellationToken = default);

    /// <summary>Creates a new requirement group, optionally nested under an existing parent group.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or whitespace.</exception>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="parentGroupId"/> is not <see langword="null"/> and does not exist.</exception>
    Task<IRequirementGroup> CreateGroupAsync(string name, Guid? parentGroupId = null, CancellationToken cancellationToken = default);

    /// <summary>Returns the group, or <see langword="null"/> if none exists.</summary>
    Task<IRequirementGroup?> FindGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates every verification, and every linked reference,
    /// recorded against a requirement into one coherent read — never a
    /// new stored entity, only a composition of already-recorded facts
    /// (<c>WP7.2C Requirements Platform Contracts.md</c> §7).
    /// </summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="requirementId"/> does not exist.</exception>
    /// <exception cref="Identity.PermissionDeniedException">The current principal does not hold <see cref="Verification.VerificationService.ReadPermission"/> — inherited transitively from <see cref="Verification.IVerificationService.GetVerificationHistoryAsync"/>, never enforced by this service itself (`ADR-0061`).</exception>
    Task<IRequirementEvidence> GetEvidenceAsync(Guid requirementId, CancellationToken cancellationToken = default);
}
