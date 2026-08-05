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

    // ---- WP 9.1A: additive lifecycle/ownership/priority operations (ADR-0084) ----

    /// <summary>Sets the requirement's own current owner. <see langword="null"/> clears it.</summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="requirementId"/> does not exist.</exception>
    Task<IRequirement> SetOwnerAsync(Guid requirementId, string? owner, CancellationToken cancellationToken = default);

    /// <summary>Sets the requirement's own current priority. <see langword="null"/> clears it.</summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="requirementId"/> does not exist.</exception>
    Task<IRequirement> SetPriorityAsync(Guid requirementId, RequirementPriority? priority, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the requirement — never erases it; <see cref="FindAsync"/> still returns it, with <see cref="IRequirement.IsDeleted"/> set.</summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="requirementId"/> does not exist.</exception>
    Task<IRequirement> DeleteAsync(Guid requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the requirement into <paramref name="groupId"/> (or ungroups it, if <see langword="null"/>) — the requirement's own live, current
    /// <see cref="IRequirement.GroupId"/>. Also records a permanent <see cref="RequirementRelationshipKinds.GroupedUnder"/> relationship link to
    /// the new group, never removing any prior one — a full move history survives even though <see cref="IRequirement.GroupId"/> itself only
    /// ever reflects the latest move.
    /// </summary>
    /// <exception cref="RequirementNotFoundException"><paramref name="requirementId"/> does not exist.</exception>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="groupId"/> is not <see langword="null"/> and does not exist.</exception>
    Task<IRequirement> MoveToGroupAsync(Guid requirementId, Guid? groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the group under <paramref name="newParentGroupId"/> (or makes it a root group, if <see langword="null"/>) — the group's own live,
    /// current <see cref="IRequirementGroup.ParentGroupId"/>. Also records a permanent <see cref="RequirementRelationshipKinds.GroupedUnder"/>
    /// relationship link, never removing any prior one, mirroring <see cref="MoveToGroupAsync"/>'s own identical reasoning.
    /// </summary>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="groupId"/>, or <paramref name="newParentGroupId"/> if not <see langword="null"/>, does not exist.</exception>
    Task<IRequirementGroup> MoveGroupAsync(Guid groupId, Guid? newParentGroupId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the group.</summary>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="groupId"/> does not exist.</exception>
    /// <exception cref="RequirementGroupHasChildrenException">The group still has live (non-deleted) sub-groups or grouped requirements.</exception>
    Task<IRequirementGroup> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the collection. Never affects any member requirement — a collection is a view over requirements, not a container that owns them (`WP7.2B Requirements Domain Model.md` §2).</summary>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="collectionId"/> does not exist.</exception>
    Task<IRequirementCollection> DeleteCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default);

    // ---- WP 9.1A: additive enumeration (ADR-0084) ----

    /// <summary>
    /// Every requirement collection currently registered, live and
    /// soft-deleted alike (mirrors <see cref="ListAsync"/>'s own identical
    /// "every one, caller filters" shape). Never <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// <b>Disclosed gap this closes:</b> before `WP 9.1A`, no enumeration
    /// of collections existed at all — <see cref="EngineeringData.IEngineeringDocumentStore"/>
    /// itself has no "list every document of a Kind" capability (confirmed
    /// directly), and collections, unlike requirements, are not
    /// identifier-indexed. Backed by a second, small
    /// <see cref="Persistence.IPersistenceStore"/>-direct registry,
    /// mirroring <see cref="FindByIdentifierAsync"/>'s own already-approved
    /// `ADR-0059` precedent for the identical reason — a new read
    /// capability built the same already-proven way, not a new mechanism.
    /// The Engineering Workspace's own Project Explorer needs this to root
    /// its tree at every live Requirement Set; a defensible, disclosed
    /// extension, not a redesign.
    /// </remarks>
    Task<IReadOnlyList<IRequirementCollection>> ListCollectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every requirement group currently registered, live and
    /// soft-deleted alike. Never <see langword="null"/>.
    /// </summary>
    /// <remarks>Same disclosed gap and fix as <see cref="ListCollectionsAsync"/>, applied to groups instead of collections. Also lets <see cref="DeleteGroupAsync"/>'s own has-children guard now check live sub-groups too — closing the narrower gap <c>RequirementGroupHasChildrenException</c> originally disclosed, before this capability existed to close it.</remarks>
    Task<IReadOnlyList<IRequirementGroup>> ListGroupsAsync(CancellationToken cancellationToken = default);
}
