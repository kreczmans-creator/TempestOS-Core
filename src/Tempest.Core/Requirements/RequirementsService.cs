using System.Text.Json;
using Tempest.Core.Audit;
using Tempest.Core.Concurrency;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.Verification;

namespace Tempest.Core.Requirements;

/// <summary>
/// The concrete <see cref="IRequirementsService"/> implementation — the
/// first implementation of the Systems Engineering Foundation.
/// </summary>
/// <remarks>
/// <para>
/// <b>A thin, typed index over <see cref="IEngineeringDocumentStore"/>,
/// not a second storage mechanism</b> (`ADR-0058`): every requirement,
/// collection, and group is itself an <see cref="IEngineeringDocument"/>
/// (<c>Kind = "Requirement"</c>, <c>"RequirementCollection"</c>, or
/// <c>"RequirementGroup"</c> respectively). Every relationship — group
/// hierarchy, collection membership, allocation, traceability — is a
/// <see cref="DocumentReference"/> created via <see cref="IEngineeringDocumentStore.LinkAsync"/>,
/// never a field stored on any DTO, mirroring `ADR-0057`'s own identical
/// reuse-of-existing-mechanism discipline one layer up.
/// </para>
/// <para>
/// <b>A direct <see cref="IPersistenceStore"/> dependency for its own
/// <c>identifier</c> index</b> (`ADR-0059`), mirroring
/// <see cref="Materials.MaterialCatalog"/>'s own identical `materialId`
/// index precedent (`ADR-0055` Decision 3) — <see cref="IEngineeringDocumentStore"/>
/// itself has no lookup-by-arbitrary-string capability to build
/// <see cref="FindByIdentifierAsync"/> on top of otherwise.
/// </para>
/// <para>
/// <b>No internal permission gating anywhere in this service</b>
/// (`ADR-0061`) — every method is calling-layer-enforced only, mirroring
/// <see cref="Materials.MaterialCatalog"/>/<see cref="Calculations.CalculationEngine"/>'s
/// own majority precedent, not <see cref="IVerificationService.GetVerificationHistoryAsync"/>'s
/// own single, narrower exception. Requirement data (statements,
/// relationships) is ordinary operational engineering content the
/// calling layer's own context already governs, not an audit-adjacent
/// sensitive history this framework itself must protect. <see cref="GetEvidenceAsync"/>
/// still ends up permission-gated in practice — transitively, through
/// its own call to <see cref="IVerificationService.GetVerificationHistoryAsync"/>,
/// which remains gated unchanged.
/// </para>
/// <para>
/// <b>No compare-and-swap concurrency protection on <see cref="ReviseAsync"/>
/// or <see cref="SetStatusAsync"/></b> — disclosed, accepted debt
/// (`TD-25`, `ADR-0060`), not resolved here: no real, demonstrated
/// multi-author collaborative-editing incident has occurred to justify
/// the added complexity of an expected-prior-revision parameter, which
/// the approved contract's own <see cref="ReviseAsync"/> signature does
/// not carry.
/// </para>
/// <para>
/// <b>Orphan-avoidance:</b> <see cref="LinkAsync"/> and
/// <see cref="AddToCollectionAsync"/> both explicitly confirm their own
/// source/collection exists before delegating to
/// <see cref="IEngineeringDocumentStore.LinkAsync"/> — the identical
/// "make orphaned evidence difficult" discipline
/// <see cref="VerificationService.RecordAsync"/> already established for
/// its own subject-document check.
/// </para>
/// <para>
/// <b>Every mutating write announces itself on the platform's change
/// feed</b> (`WP 18.1A`, `TD-28` closure). Before this, every write here
/// went straight through <see cref="IEngineeringDocumentStore"/>/
/// <see cref="IPersistenceStore"/> and never through
/// <see cref="EngineeringDomain.EngineeringDomainContext.ExecuteWriteAsync"/>
/// — the only place the change feed was ever published from — so a
/// docked Requirements Explorer or Cockpit went stale until the user
/// navigated away and back. Each of the fourteen mutators now commits its
/// document-store write(s) through <see cref="_transactionalStore"/>'s own
/// <see cref="IQueryablePersistenceStore.ExecuteInTransactionAsync"/> —
/// the only operation that advances <see cref="IQueryablePersistenceStore.CurrentSequence"/>
/// — and, once that transaction has committed, publishes exactly one
/// <see cref="WorkspaceChange"/> naming the touched object, its own Kind,
/// and what happened to it, through the same <see cref="IWorkspaceChangePublisher"/>
/// and the same store-wide sequence counter every other engineering write
/// announces itself through (`ADR-0145`). Never published before commit;
/// never published for a write this service refused.
/// </para>
/// </remarks>
public sealed class RequirementsService : IRequirementsService
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every requirement's own backing document carries.</summary>
    public const string RequirementDocumentKind = "Requirement";

    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every requirement collection's own backing document carries.</summary>
    public const string RequirementCollectionDocumentKind = "RequirementCollection";

    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every requirement group's own backing document carries.</summary>
    public const string RequirementGroupDocumentKind = "RequirementGroup";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>identifier</c> to its own backing document Id.</summary>
    public const string IdentifierIndexCollectionName = "Requirements.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection registering every created collection's own document Id, for <see cref="ListCollectionsAsync"/> (`WP 9.1A`) — see that method's own disclosed remarks.</summary>
    public const string CollectionRegistryCollectionName = "Requirements.CollectionRegistry";

    /// <summary>The <see cref="IPersistenceStore"/> collection registering every created group's own document Id, for <see cref="ListGroupsAsync"/> (`WP 9.1A`) — see that method's own disclosed remarks.</summary>
    public const string GroupRegistryCollectionName = "Requirements.GroupRegistry";

    /// <summary>The <see cref="IRequirement.CreatedByPrincipalId"/> recorded when no principal is currently established.</summary>
    public const string UnknownPrincipalId = "unknown";

    private readonly IEngineeringDocumentStore _documentStore;
    private readonly EngineeringDomain.ITransactionalDocumentWriter _documentWriter;
    private readonly IPersistenceStore _persistenceStore;
    private readonly IQueryablePersistenceStore _transactionalStore;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IVerificationService _verificationService;
    private readonly ILogger? _logger;
    private readonly IWorkspaceChangePublisher? _workspaceChanges;
    private readonly AsyncKeyedLock _identifierLock = new();

    /// <summary>Initialises a new instance of the <see cref="RequirementsService"/> class.</summary>
    /// <remarks>
    /// <paramref name="documentStore"/> and <paramref name="persistenceStore"/>
    /// must also implement <see cref="EngineeringDomain.ITransactionalDocumentWriter"/>
    /// and <see cref="IQueryablePersistenceStore"/> respectively (`ADR-0144`,
    /// `ADR-0145`) — the stores this platform ships do; a caller-supplied
    /// double that implements only the narrower, non-transactional
    /// interface is refused here rather than letting <see cref="CreateAsync"/>
    /// silently fall back to two separate writes (`TD-67`).
    /// <paramref name="workspaceChanges"/> (`TD-28` closure) is where every
    /// mutator publishes once its own write has committed — the same
    /// <see cref="IWorkspaceChangePublisher"/> instance
    /// <see cref="EngineeringDomain.EngineeringDomainContext"/> is given
    /// (`TempestHost`'s composition root registers one <see cref="WorkspaceChangeFeed"/>
    /// under both publish and subscribe interfaces before either consumer
    /// resolves it). <see langword="null"/> — the default — is a
    /// legitimate, silent no-op, mirroring <see cref="EngineeringDomain.EngineeringDomainContext"/>'s
    /// own identical optional collaborator.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="documentStore"/>, <paramref name="persistenceStore"/>,
    /// <paramref name="currentPrincipalAccessor"/>, or <paramref name="verificationService"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="documentStore"/> does not implement <see cref="EngineeringDomain.ITransactionalDocumentWriter"/>,
    /// or <paramref name="persistenceStore"/> does not implement <see cref="IQueryablePersistenceStore"/>.
    /// </exception>
    public RequirementsService(
        IEngineeringDocumentStore documentStore,
        IPersistenceStore persistenceStore,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IVerificationService verificationService,
        ILogger? logger = null,
        IWorkspaceChangePublisher? workspaceChanges = null)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(persistenceStore);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(verificationService);

        _documentStore = documentStore;
        _documentWriter = documentStore as EngineeringDomain.ITransactionalDocumentWriter
            ?? throw new ArgumentException(
                $"'{documentStore.GetType().Name}' does not implement the transactional document writer contract " +
                "('ADR-0145'), so a Requirement and its identifier-index entry cannot be written as one " +
                "transaction ('TD-67'). Use the store this platform ships.",
                nameof(documentStore));
        _persistenceStore = persistenceStore;
        _transactionalStore = persistenceStore as IQueryablePersistenceStore
            ?? throw new ArgumentException(
                $"'{persistenceStore.GetType().Name}' does not implement '{nameof(IQueryablePersistenceStore)}' " +
                "('ADR-0144'), so a Requirement and its identifier-index entry cannot be written as one " +
                "transaction ('TD-67'). Use the store this platform ships.",
                nameof(persistenceStore));
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _verificationService = verificationService;
        _logger = logger;
        _workspaceChanges = workspaceChanges;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>The document and its identifier-index entry are one transaction
    /// (`TD-67`, `ADR-0145`).</b> A fault between the two used to leave a
    /// Requirement that existed as a document but could never be found by
    /// its own identifier — the same two-write shape `VerificationService.RecordAsync`
    /// closed under `TD-23`, fixed with the same primitive.
    /// </remarks>
    public async Task<IRequirement> CreateAsync(string identifier, string statement, string? category = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);

        using (await _identifierLock.AcquireAsync(identifier, cancellationToken).ConfigureAwait(false))
        {
            if (await ReadDocumentIdAsync(identifier, cancellationToken).ConfigureAwait(false) is not null)
                throw new DuplicateRequirementIdentifierException(identifier);

            var createdAt = DateTimeOffset.UtcNow;
            var createdBy = ResolveCurrentPrincipalId();
            var documentId = Guid.NewGuid();

            var dto = new RequirementDto(identifier, statement, category, RequirementStatus.Draft, createdBy, createdAt);

            await _transactionalStore.ExecuteInTransactionAsync(
                async (transaction, token) =>
                {
                    await _documentWriter.CreateAsync(transaction, documentId, RequirementDocumentKind, JsonSerializer.Serialize(dto), token)
                        .ConfigureAwait(false);

                    await transaction.WriteAsync(IdentifierIndexCollectionName, identifier, documentId.ToString("N"), token)
                        .ConfigureAwait(false);

                    await WriteAuditAsync(transaction, documentId, RequirementDocumentKind, RequirementsAuditActions.Created, $"'{identifier}'.", token)
                        .ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);

            PublishChange(documentId, RequirementDocumentKind, WorkspaceChangeType.Created);

            _logger?.Information($"Requirement created: '{identifier}' (document '{documentId}').");

            return ToRequirement(documentId, dto, revisionNumber: 1);
        }
    }

    /// <inheritdoc />
    public async Task<IRequirement?> FindAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        var document = await _documentStore.FindAsync(requirementId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, RequirementDocumentKind, StringComparison.Ordinal))
            return null;

        return await ReadRequirementAsync(requirementId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IRequirement?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        var documentId = await ReadDocumentIdAsync(identifier, cancellationToken).ConfigureAwait(false);
        return documentId is null ? null : await ReadRequirementAsync(documentId.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IRequirement> ReviseAsync(Guid requirementId, string newStatement, string? changeSummary, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newStatement);

        var current = await ReadDtoAsync(requirementId, cancellationToken).ConfigureAwait(false)
            ?? throw new RequirementNotFoundException(requirementId);

        var dto = current with { Statement = newStatement };
        IDocumentRevision? revision = null;

        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                revision = await _documentWriter.ReviseAsync(transaction, requirementId, JsonSerializer.Serialize(dto), changeSummary, token)
                    .ConfigureAwait(false);

                await WriteAuditAsync(transaction, requirementId, RequirementDocumentKind, RequirementsAuditActions.Revised, changeSummary, token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(requirementId, RequirementDocumentKind, WorkspaceChangeType.Updated);

        _logger?.Information($"Requirement revised: '{dto.Identifier}' (revision {revision!.RevisionNumber}).");

        return ToRequirement(requirementId, dto, revision!.RevisionNumber);
    }

    /// <inheritdoc />
    public async Task SetStatusAsync(Guid requirementId, RequirementStatus status, CancellationToken cancellationToken = default)
    {
        var current = await ReadDtoAsync(requirementId, cancellationToken).ConfigureAwait(false)
            ?? throw new RequirementNotFoundException(requirementId);

        if (!RequirementStatusTransitions.IsPermitted(current.Status, status))
            throw new InvalidRequirementStatusTransitionException(current.Status, status);

        var dto = current with { Status = status };

        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                await _documentWriter.ReviseAsync(transaction, requirementId, JsonSerializer.Serialize(dto), $"Status changed to {status}.", token)
                    .ConfigureAwait(false);

                await WriteAuditAsync(transaction, requirementId, RequirementDocumentKind, RequirementsAuditActions.StatusChanged, $"{current.Status} -> {status}.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(requirementId, RequirementDocumentKind, WorkspaceChangeType.StatusChanged);

        _logger?.Information($"Requirement status changed: '{dto.Identifier}' → '{status}'.");
    }

    /// <inheritdoc />
    public Task<IRequirement> SetOwnerAsync(Guid requirementId, string? owner, CancellationToken cancellationToken = default) =>
        SetOwnerAsync(requirementId, owner, ownerPersonId: null, cancellationToken);

    /// <inheritdoc />
    public async Task<IRequirement> SetOwnerAsync(Guid requirementId, string? owner, string? ownerPersonId, CancellationToken cancellationToken = default)
    {
        var current = await ReadDtoAsync(requirementId, cancellationToken).ConfigureAwait(false)
            ?? throw new RequirementNotFoundException(requirementId);

        var dto = current with { Owner = owner, OwnerPersonId = ownerPersonId };
        IDocumentRevision? revision = null;

        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                revision = await _documentWriter.ReviseAsync(transaction, requirementId, JsonSerializer.Serialize(dto), $"Owner changed to '{owner ?? "(none)"}'.", token)
                    .ConfigureAwait(false);

                await WriteAuditAsync(transaction, requirementId, RequirementDocumentKind, RequirementsAuditActions.OwnerChanged, $"Owner changed to '{owner ?? "(none)"}'.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(requirementId, RequirementDocumentKind, WorkspaceChangeType.Updated);

        _logger?.Information($"Requirement owner changed: '{dto.Identifier}' → '{owner ?? "(none)"}'.");

        return ToRequirement(requirementId, dto, revision!.RevisionNumber);
    }

    /// <inheritdoc />
    public async Task<IRequirement> SetPriorityAsync(Guid requirementId, RequirementPriority? priority, CancellationToken cancellationToken = default)
    {
        var current = await ReadDtoAsync(requirementId, cancellationToken).ConfigureAwait(false)
            ?? throw new RequirementNotFoundException(requirementId);

        var dto = current with { Priority = priority };
        IDocumentRevision? revision = null;

        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                revision = await _documentWriter.ReviseAsync(transaction, requirementId, JsonSerializer.Serialize(dto), $"Priority changed to '{priority?.ToString() ?? "(none)"}'.", token)
                    .ConfigureAwait(false);

                await WriteAuditAsync(transaction, requirementId, RequirementDocumentKind, RequirementsAuditActions.PriorityChanged, $"Priority changed to '{priority?.ToString() ?? "(none)"}'.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(requirementId, RequirementDocumentKind, WorkspaceChangeType.Updated);

        _logger?.Information($"Requirement priority changed: '{dto.Identifier}' → '{priority?.ToString() ?? "(none)"}'.");

        return ToRequirement(requirementId, dto, revision!.RevisionNumber);
    }

    /// <inheritdoc />
    public async Task<IRequirement> DeleteAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        var current = await ReadDtoAsync(requirementId, cancellationToken).ConfigureAwait(false)
            ?? throw new RequirementNotFoundException(requirementId);

        var dto = current with { IsDeleted = true };
        IDocumentRevision? revision = null;

        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                revision = await _documentWriter.ReviseAsync(transaction, requirementId, JsonSerializer.Serialize(dto), "Deleted.", token)
                    .ConfigureAwait(false);

                await WriteAuditAsync(transaction, requirementId, RequirementDocumentKind, RequirementsAuditActions.Deleted, "Deleted.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(requirementId, RequirementDocumentKind, WorkspaceChangeType.Deleted);

        _logger?.Information($"Requirement deleted: '{dto.Identifier}'.");

        return ToRequirement(requirementId, dto, revision!.RevisionNumber);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>`WP 21.6A`.</b> The Undo half of <see cref="DeleteAsync"/>'s own
    /// compensation, and the Redo half of <see cref="CreateAsync"/>'s —
    /// mirrors <c>EngineeringDomain.EngineeringObjectBase.UndeleteAsync</c>'s
    /// own identical two-guard shape: refuses a requirement that is not
    /// currently deleted, and refuses restoring one whose own current group
    /// has itself been deleted in the meantime (a requirement restored
    /// under a gone group would be reachable only by direct Id).
    /// </remarks>
    public async Task<IRequirement> UndeleteAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        var current = await ReadDtoAsync(requirementId, cancellationToken).ConfigureAwait(false)
            ?? throw new RequirementNotFoundException(requirementId);

        if (!current.IsDeleted)
            throw new RequirementNotDeletedException(requirementId);

        if (current.GroupId is { } groupId)
        {
            var group = await ReadGroupDtoAsync(groupId, cancellationToken).ConfigureAwait(false);
            if (group is null || group.IsDeleted)
                throw new RequirementGroupDeletedException(requirementId, groupId);
        }

        var dto = current with { IsDeleted = false };
        IDocumentRevision? revision = null;

        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                revision = await _documentWriter.ReviseAsync(transaction, requirementId, JsonSerializer.Serialize(dto), "Restored from deletion.", token)
                    .ConfigureAwait(false);

                await WriteAuditAsync(transaction, requirementId, RequirementDocumentKind, RequirementsAuditActions.Undeleted, "Restored from deletion.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(requirementId, RequirementDocumentKind, WorkspaceChangeType.Updated);

        _logger?.Information($"Requirement undeleted: '{dto.Identifier}'.");

        return ToRequirement(requirementId, dto, revision!.RevisionNumber);
    }

    /// <inheritdoc />
    public async Task<IRequirement> MoveToGroupAsync(Guid requirementId, Guid? groupId, CancellationToken cancellationToken = default)
    {
        var current = await ReadDtoAsync(requirementId, cancellationToken).ConfigureAwait(false)
            ?? throw new RequirementNotFoundException(requirementId);

        if (groupId is not null && await _documentStore.FindAsync(groupId.Value, cancellationToken).ConfigureAwait(false) is null)
            throw new EngineeringDocumentNotFoundException(groupId.Value);

        var dto = current with { GroupId = groupId };
        IDocumentRevision? revision = null;

        // The revision and the GroupedUnder link land in one transaction
        // (`TD-28`) — a single logical move is one committed fact, and one
        // WorkspaceChange entry, never two.
        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                revision = await _documentWriter.ReviseAsync(transaction, requirementId, JsonSerializer.Serialize(dto), $"Moved to group '{groupId?.ToString() ?? "(none)"}'.", token)
                    .ConfigureAwait(false);

                if (groupId is not null)
                    await _documentWriter.LinkAsync(transaction, requirementId, groupId.Value, RequirementRelationshipKinds.GroupedUnder, token)
                        .ConfigureAwait(false);

                await WriteAuditAsync(transaction, requirementId, RequirementDocumentKind, RequirementsAuditActions.Moved, $"Moved to group '{groupId?.ToString() ?? "(none)"}'.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(requirementId, RequirementDocumentKind, WorkspaceChangeType.Moved);

        _logger?.Information($"Requirement moved to group: '{dto.Identifier}' → '{groupId?.ToString() ?? "(none)"}'.");

        return ToRequirement(requirementId, dto, revision!.RevisionNumber);
    }

    /// <inheritdoc />
    public async Task LinkAsync(Guid sourceRequirementId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipKind);

        if (await _documentStore.FindAsync(sourceRequirementId, cancellationToken).ConfigureAwait(false) is null)
            throw new RequirementNotFoundException(sourceRequirementId);

        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                await _documentWriter.LinkAsync(transaction, sourceRequirementId, targetDocumentId, relationshipKind, token)
                    .ConfigureAwait(false);

                await WriteAuditAsync(transaction, sourceRequirementId, RequirementDocumentKind, RequirementsAuditActions.Linked, $"{relationshipKind} to '{targetDocumentId:N}'.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(sourceRequirementId, RequirementDocumentKind, WorkspaceChangeType.Updated);

        _logger?.Information($"Requirement relationship recorded: '{sourceRequirementId}' --[{relationshipKind}]--> '{targetDocumentId}'.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentReference>> GetRelationshipsAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        if (await _documentStore.FindAsync(requirementId, cancellationToken).ConfigureAwait(false) is null)
            throw new RequirementNotFoundException(requirementId);

        return await _documentStore.GetReferencesAsync(requirementId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IRequirement>> ListAsync(CancellationToken cancellationToken = default)
    {
        var identifiers = await _persistenceStore.ListKeysAsync(IdentifierIndexCollectionName, cancellationToken).ConfigureAwait(false);
        var requirements = new List<IRequirement>(identifiers.Count);

        foreach (var identifier in identifiers)
        {
            var documentId = await ReadDocumentIdAsync(identifier, cancellationToken).ConfigureAwait(false);
            if (documentId is null)
                continue;

            var requirement = await ReadRequirementAsync(documentId.Value, cancellationToken).ConfigureAwait(false);
            if (requirement is not null)
                requirements.Add(requirement);
        }

        return requirements;
    }

    /// <inheritdoc />
    public async Task<IRequirementCollection> CreateCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var dto = new RequirementCollectionDto(name);
        var documentId = Guid.NewGuid();

        // The document and its registry entry land in one transaction
        // (`TD-28`) — the same one-write-per-logical-change shape
        // CreateAsync's own TD-67 closure already established for
        // Requirements' identifier index.
        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                await _documentWriter.CreateAsync(transaction, documentId, RequirementCollectionDocumentKind, JsonSerializer.Serialize(dto), token)
                    .ConfigureAwait(false);

                await transaction.WriteAsync(CollectionRegistryCollectionName, documentId.ToString("N"), documentId.ToString("N"), token)
                    .ConfigureAwait(false);

                await WriteAuditAsync(transaction, documentId, RequirementCollectionDocumentKind, RequirementsAuditActions.CollectionCreated, $"'{name}'.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(documentId, RequirementCollectionDocumentKind, WorkspaceChangeType.Created);

        _logger?.Information($"Requirement collection created: '{name}' (document '{documentId}').");

        return new RequirementCollection(documentId, name, [], dto.IsDeleted);
    }

    /// <inheritdoc />
    public async Task<IRequirementCollection?> FindCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        var document = await _documentStore.FindAsync(collectionId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, RequirementCollectionDocumentKind, StringComparison.Ordinal))
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(collectionId, cancellationToken).ConfigureAwait(false);
        var dto = DeserialiseContent<RequirementCollectionDto>(history[^1].Content, $"Requirement collection '{collectionId}'");

        var members = await _documentStore.GetReferencesAsync(collectionId, cancellationToken).ConfigureAwait(false);
        var memberIds = members
            .Where(r => string.Equals(r.RelationshipKind, RequirementRelationshipKinds.CollectedIn, StringComparison.Ordinal))
            .Select(r => r.TargetDocumentId)
            .ToList();

        return new RequirementCollection(collectionId, dto.Name, memberIds, dto.IsDeleted);
    }

    /// <inheritdoc />
    public async Task<IRequirementCollection> DeleteCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        var collectionDocument = await _documentStore.FindAsync(collectionId, cancellationToken).ConfigureAwait(false);
        if (collectionDocument is null || !string.Equals(collectionDocument.Kind, RequirementCollectionDocumentKind, StringComparison.Ordinal))
            throw new EngineeringDocumentNotFoundException(collectionId);

        var history = await _documentStore.GetRevisionHistoryAsync(collectionId, cancellationToken).ConfigureAwait(false);
        var current = DeserialiseContent<RequirementCollectionDto>(history[^1].Content, $"Requirement collection '{collectionId}'");

        var dto = current with { IsDeleted = true };

        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                await _documentWriter.ReviseAsync(transaction, collectionId, JsonSerializer.Serialize(dto), "Deleted.", token)
                    .ConfigureAwait(false);

                await WriteAuditAsync(transaction, collectionId, RequirementCollectionDocumentKind, RequirementsAuditActions.CollectionDeleted, "Deleted.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(collectionId, RequirementCollectionDocumentKind, WorkspaceChangeType.Deleted);

        _logger?.Information($"Requirement collection deleted: '{dto.Name}'.");

        var members = await _documentStore.GetReferencesAsync(collectionId, cancellationToken).ConfigureAwait(false);
        var memberIds = members
            .Where(r => string.Equals(r.RelationshipKind, RequirementRelationshipKinds.CollectedIn, StringComparison.Ordinal))
            .Select(r => r.TargetDocumentId)
            .ToList();

        return new RequirementCollection(collectionId, dto.Name, memberIds, dto.IsDeleted);
    }

    /// <inheritdoc />
    public async Task AddToCollectionAsync(Guid collectionId, Guid requirementId, CancellationToken cancellationToken = default)
    {
        var collectionDocument = await _documentStore.FindAsync(collectionId, cancellationToken).ConfigureAwait(false);
        if (collectionDocument is null || !string.Equals(collectionDocument.Kind, RequirementCollectionDocumentKind, StringComparison.Ordinal))
            throw new EngineeringDocumentNotFoundException(collectionId);

        if (await _documentStore.FindAsync(requirementId, cancellationToken).ConfigureAwait(false) is null)
            throw new RequirementNotFoundException(requirementId);

        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                await _documentWriter.LinkAsync(transaction, collectionId, requirementId, RequirementRelationshipKinds.CollectedIn, token)
                    .ConfigureAwait(false);

                await WriteAuditAsync(transaction, collectionId, RequirementCollectionDocumentKind, RequirementsAuditActions.AddedToCollection, $"Requirement '{requirementId:N}' added.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(collectionId, RequirementCollectionDocumentKind, WorkspaceChangeType.Updated);

        _logger?.Information($"Requirement '{requirementId}' added to collection '{collectionId}'.");
    }

    /// <inheritdoc />
    public async Task<IRequirementGroup> CreateGroupAsync(string name, Guid? parentGroupId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (parentGroupId is not null && await _documentStore.FindAsync(parentGroupId.Value, cancellationToken).ConfigureAwait(false) is null)
            throw new EngineeringDocumentNotFoundException(parentGroupId.Value);

        var dto = new RequirementGroupDto(name, parentGroupId);
        var documentId = Guid.NewGuid();

        // The document, its registry entry, and the optional GroupedUnder
        // link to the parent all land in one transaction (`TD-28`) — the
        // same one-write-per-logical-change shape CreateCollectionAsync
        // now uses.
        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                await _documentWriter.CreateAsync(transaction, documentId, RequirementGroupDocumentKind, JsonSerializer.Serialize(dto), token)
                    .ConfigureAwait(false);

                await transaction.WriteAsync(GroupRegistryCollectionName, documentId.ToString("N"), documentId.ToString("N"), token)
                    .ConfigureAwait(false);

                if (parentGroupId is not null)
                    await _documentWriter.LinkAsync(transaction, documentId, parentGroupId.Value, RequirementRelationshipKinds.GroupedUnder, token)
                        .ConfigureAwait(false);

                await WriteAuditAsync(transaction, documentId, RequirementGroupDocumentKind, RequirementsAuditActions.GroupCreated, $"'{name}'.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(documentId, RequirementGroupDocumentKind, WorkspaceChangeType.Created);

        _logger?.Information($"Requirement group created: '{name}' (document '{documentId}').");

        return new RequirementGroup(documentId, name, parentGroupId, dto.IsDeleted);
    }

    /// <inheritdoc />
    public async Task<IRequirementGroup?> FindGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var document = await _documentStore.FindAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, RequirementGroupDocumentKind, StringComparison.Ordinal))
            return null;

        var dto = await ReadGroupDtoAsync(groupId, cancellationToken).ConfigureAwait(false)
            ?? throw new EngineeringDataException($"Requirement group '{groupId}' could not be deserialised.");

        // WP 9.1A: ParentGroupId is now this DTO's own live, current value —
        // see RequirementGroupDto's own disclosed remarks for why the prior
        // .FirstOrDefault()-over-relationships resolution was unsafe once a
        // real Move existed.
        return new RequirementGroup(groupId, dto.Name, dto.ParentGroupId, dto.IsDeleted);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>`TD-67` closure — Kind and ancestry-cycle guards.</b> Before this
    /// fix, <paramref name="newParentGroupId"/> was checked only for
    /// existence (via <see cref="IEngineeringDocumentStore.FindAsync"/>),
    /// never for being a <c>RequirementGroup</c> itself or for creating a
    /// cycle in the group hierarchy — either would have silently corrupted
    /// <see cref="IRequirementGroup.ParentGroupId"/>'s own tree shape (a
    /// group parented under a Requirement document, or under its own
    /// descendant, produces an infinite loop the first caller that walks
    /// the hierarchy — a Property Inspector facet or a future ancestry
    /// query — would hang on). The
    /// ancestry walk in <see cref="GuardAgainstGroupCycleAsync"/> mirrors
    /// <c>EngineeringDomain.EngineeringObjectBase.GuardAgainstCircularParentAsync</c>'s
    /// own identical algorithm one layer up, over
    /// <see cref="RequirementGroupDto.ParentGroupId"/> instead of
    /// <c>IHasParent.ParentId</c>.
    /// </remarks>
    public async Task<IRequirementGroup> MoveGroupAsync(Guid groupId, Guid? newParentGroupId, CancellationToken cancellationToken = default)
    {
        var current = await ReadGroupDtoAsync(groupId, cancellationToken).ConfigureAwait(false)
            ?? throw new EngineeringDocumentNotFoundException(groupId);

        if (newParentGroupId is { } candidateParentId)
        {
            // Wrong-Kind and missing both read as "not found" — the
            // identical idiom this service's own FindGroupAsync/
            // DeleteCollectionAsync already use for a Kind mismatch, never
            // a raw existence check alone.
            if (await ReadGroupDtoAsync(candidateParentId, cancellationToken).ConfigureAwait(false) is null)
                throw new EngineeringDocumentNotFoundException(candidateParentId);

            await GuardAgainstGroupCycleAsync(groupId, candidateParentId, cancellationToken).ConfigureAwait(false);
        }

        var dto = current with { ParentGroupId = newParentGroupId };

        // The revision and the GroupedUnder link land in one transaction
        // (`TD-28`), mirroring MoveToGroupAsync's own identical reasoning.
        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                await _documentWriter.ReviseAsync(transaction, groupId, JsonSerializer.Serialize(dto), $"Moved under group '{newParentGroupId?.ToString() ?? "(none)"}'.", token)
                    .ConfigureAwait(false);

                if (newParentGroupId is not null)
                    await _documentWriter.LinkAsync(transaction, groupId, newParentGroupId.Value, RequirementRelationshipKinds.GroupedUnder, token)
                        .ConfigureAwait(false);

                await WriteAuditAsync(transaction, groupId, RequirementGroupDocumentKind, RequirementsAuditActions.GroupMoved, $"Moved under group '{newParentGroupId?.ToString() ?? "(none)"}'.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(groupId, RequirementGroupDocumentKind, WorkspaceChangeType.Moved);

        _logger?.Information($"Requirement group moved: '{dto.Name}' → '{newParentGroupId?.ToString() ?? "(none)"}'.");

        return new RequirementGroup(groupId, dto.Name, dto.ParentGroupId, dto.IsDeleted);
    }

    /// <inheritdoc />
    public async Task<IRequirementGroup> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var current = await ReadGroupDtoAsync(groupId, cancellationToken).ConfigureAwait(false)
            ?? throw new EngineeringDocumentNotFoundException(groupId);

        var liveChildren = await CountLiveGroupChildrenAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (liveChildren > 0)
            throw new RequirementGroupHasChildrenException(groupId, liveChildren);

        var dto = current with { IsDeleted = true };

        await _transactionalStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                await _documentWriter.ReviseAsync(transaction, groupId, JsonSerializer.Serialize(dto), "Deleted.", token)
                    .ConfigureAwait(false);

                await WriteAuditAsync(transaction, groupId, RequirementGroupDocumentKind, RequirementsAuditActions.GroupDeleted, "Deleted.", token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        PublishChange(groupId, RequirementGroupDocumentKind, WorkspaceChangeType.Deleted);

        _logger?.Information($"Requirement group deleted: '{dto.Name}'.");

        return new RequirementGroup(groupId, dto.Name, dto.ParentGroupId, dto.IsDeleted);
    }

    /// <inheritdoc />
    public async Task<IRequirementEvidence> GetEvidenceAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        if (await _documentStore.FindAsync(requirementId, cancellationToken).ConfigureAwait(false) is null)
            throw new RequirementNotFoundException(requirementId);

        var verificationHistory = await _verificationService.GetVerificationHistoryAsync(requirementId, cancellationToken).ConfigureAwait(false);
        var linkedReferences = await _documentStore.GetReferencesAsync(requirementId, cancellationToken).ConfigureAwait(false);

        return new RequirementEvidence(requirementId, verificationHistory, linkedReferences);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IRequirementCollection>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var documentIds = await _persistenceStore.ListKeysAsync(CollectionRegistryCollectionName, cancellationToken).ConfigureAwait(false);
        var collections = new List<IRequirementCollection>(documentIds.Count);

        foreach (var documentIdKey in documentIds)
        {
            // A registry key is by construction a "N"-format document Id;
            // any other file name in the registry directory (a foreign
            // file dropped beside the store's own) is not a registry
            // entry, and must not abort the whole listing (`TD-60`).
            if (!Guid.TryParseExact(documentIdKey, "N", out var documentId))
            {
                _logger?.Warning($"Ignoring non-registry key '{documentIdKey}' in collection registry '{CollectionRegistryCollectionName}'.");
                continue;
            }

            var collection = await FindCollectionAsync(documentId, cancellationToken).ConfigureAwait(false);
            if (collection is not null)
                collections.Add(collection);
        }

        return collections;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IRequirementGroup>> ListGroupsAsync(CancellationToken cancellationToken = default)
    {
        var documentIds = await _persistenceStore.ListKeysAsync(GroupRegistryCollectionName, cancellationToken).ConfigureAwait(false);
        var groups = new List<IRequirementGroup>(documentIds.Count);

        foreach (var documentIdKey in documentIds)
        {
            // Same non-registry-key guard as ListCollectionsAsync (`TD-60`).
            if (!Guid.TryParseExact(documentIdKey, "N", out var documentId))
            {
                _logger?.Warning($"Ignoring non-registry key '{documentIdKey}' in group registry '{GroupRegistryCollectionName}'.");
                continue;
            }

            var group = await FindGroupAsync(documentId, cancellationToken).ConfigureAwait(false);
            if (group is not null)
                groups.Add(group);
        }

        return groups;
    }

    /// <summary>
    /// Announces one committed write on the platform's change feed
    /// (`WP 18.1A`, `TD-28` closure) — always called after the transaction
    /// that made the change has committed, naming the object it touched,
    /// that object's own Kind, and what happened to it.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="IQueryablePersistenceStore.CurrentSequence"/> from
    /// <see cref="_transactionalStore"/> at the moment of the call — the
    /// same store, and the same counter,
    /// <see cref="EngineeringDomain.EngineeringDomainContext.ExecuteWriteAsync"/>
    /// reads for every other engineering write, since both resolve to the
    /// one platform persistence store instance (`ADR-0144`). A caller with
    /// no feed to publish to (most tests) leaves <see cref="_workspaceChanges"/>
    /// <see langword="null"/>, making this a legitimate, silent no-op —
    /// exactly as <see cref="EngineeringDomain.EngineeringDomainContext.ExecuteWriteAsync"/>'s
    /// own identical <c>WorkspaceChanges is not null</c> guard.
    /// </remarks>
    private void PublishChange(Guid objectId, string kind, WorkspaceChangeType changeType) =>
        _workspaceChanges?.Publish(new WorkspaceChange(_transactionalStore.CurrentSequence, [new WorkspaceChangeEntry(objectId, kind, changeType)]));

    /// <summary>
    /// Resolves <paramref name="identifier"/>'s backing document Id from
    /// the index. A malformed index value throws a controlled
    /// <see cref="EngineeringDataException"/> naming the entry (`TD-60`) —
    /// never a raw <see cref="FormatException"/>, and never
    /// <see langword="null"/>, which would silently misreport corruption
    /// as "no such requirement".
    /// </summary>
    private async Task<Guid?> ReadDocumentIdAsync(string identifier, CancellationToken cancellationToken)
    {
        var value = await _persistenceStore.ReadAsync(IdentifierIndexCollectionName, identifier, cancellationToken).ConfigureAwait(false);
        if (value is null)
            return null;

        if (!Guid.TryParseExact(value, "N", out var documentId))
            throw new EngineeringDataException(
                $"Requirement index entry for '{identifier}' is corrupted: '{value}' is not a valid document Id.");

        return documentId;
    }

    private async Task<RequirementDto?> ReadDtoAsync(Guid requirementId, CancellationToken cancellationToken)
    {
        var document = await _documentStore.FindAsync(requirementId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, RequirementDocumentKind, StringComparison.Ordinal))
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(requirementId, cancellationToken).ConfigureAwait(false);
        return DeserialiseContent<RequirementDto>(history[^1].Content, $"Requirement '{requirementId}'");
    }

    private async Task<IRequirement?> ReadRequirementAsync(Guid requirementId, CancellationToken cancellationToken)
    {
        var document = await _documentStore.FindAsync(requirementId, cancellationToken).ConfigureAwait(false);
        if (document is null)
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(requirementId, cancellationToken).ConfigureAwait(false);
        var currentRevision = history[^1];
        var dto = DeserialiseContent<RequirementDto>(currentRevision.Content, $"Requirement '{requirementId}'");

        return ToRequirement(requirementId, dto, currentRevision.RevisionNumber);
    }

    private string ResolveCurrentPrincipalId() =>
        _currentPrincipalAccessor.Current?.Identity.Id ?? UnknownPrincipalId;

    /// <summary>
    /// Writes one audit row for <paramref name="objectId"/> inside
    /// <paramref name="transaction"/> — the same
    /// <see cref="AuditTransactionWriter"/> primitive
    /// <c>EngineeringDomain.EngineeringObjectBase</c> uses, so the row
    /// commits or rolls back with the mutator's own write (`WP 21.6A`,
    /// OSA-15).
    /// </summary>
    private Task WriteAuditAsync(IPersistenceTransaction transaction, Guid objectId, string kind, string action, string? detail, CancellationToken cancellationToken) =>
        AuditTransactionWriter.WriteAsync(
            transaction, objectId, kind, action, ResolveCurrentPrincipalId(), detail, DateTimeOffset.UtcNow, cancellationToken);

    /// <summary>Builds an <see cref="IRequirement"/> snapshot from a DTO — the one place every read/write construction site goes through, so a new DTO field is never forgotten at a second call site (`WP 9.1A`).</summary>
    private static IRequirement ToRequirement(Guid id, RequirementDto dto, int revisionNumber) =>
        new Requirement(
            id, dto.Identifier, dto.Statement, dto.Category, dto.Status, revisionNumber, dto.CreatedByPrincipalId, dto.CreatedAt, dto.Owner,
            dto.Priority, dto.IsDeleted, dto.GroupId, dto.OwnerPersonId);

    private async Task<RequirementGroupDto?> ReadGroupDtoAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var document = await _documentStore.FindAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, RequirementGroupDocumentKind, StringComparison.Ordinal))
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(groupId, cancellationToken).ConfigureAwait(false);
        return DeserialiseContent<RequirementGroupDto>(history[^1].Content, $"Requirement group '{groupId}'");
    }

    /// <summary>Deserialises one revision's content, converting any malformed-content failure into a controlled <see cref="EngineeringDataException"/> (`TD-60`) rather than a raw <see cref="JsonException"/>.</summary>
    private static T DeserialiseContent<T>(string content, string subject)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content)
                ?? throw new EngineeringDataException($"{subject} could not be deserialised.");
        }
        catch (JsonException ex)
        {
            throw new EngineeringDataException($"{subject} could not be deserialised.", ex);
        }
    }

    /// <summary>
    /// Counts live (non-deleted) requirements grouped directly under
    /// <paramref name="groupId"/>, plus live (non-deleted) sub-groups
    /// parented directly under it — `WP 9.1A`'s own
    /// <see cref="DeleteGroupAsync"/> guard, mirroring
    /// <c>EngineeringDomain.EngineeringObjectBase.DeleteAsync</c>'s own
    /// identical has-children reasoning.
    /// </summary>
    /// <remarks>
    /// <b>Superseded disclosure:</b> this method originally checked live
    /// grouped requirements only — <see cref="EngineeringData.IEngineeringDocumentStore"/>
    /// has no "list every document of a Kind" capability, and groups
    /// were not otherwise enumerable, so live sub-groups could not be
    /// discovered. <see cref="ListGroupsAsync"/> (added within this same
    /// Work Package, for the Engineering Workspace's own Project Explorer)
    /// closes that gap; this guard now uses it, so both live children
    /// kinds are checked, matching `EngineeringDomain`'s own equivalent
    /// guard exactly. See `WP9.1A Technical Debt Assessment.md` for the
    /// full, disclosed history of this correction.
    /// </remarks>
    private async Task<int> CountLiveGroupChildrenAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var allRequirements = await ListAsync(cancellationToken).ConfigureAwait(false);
        var liveRequirementChildren = allRequirements.Count(r => !r.IsDeleted && r.GroupId == groupId);

        var allGroups = await ListGroupsAsync(cancellationToken).ConfigureAwait(false);
        var liveGroupChildren = allGroups.Count(g => !g.IsDeleted && g.Id != groupId && g.ParentGroupId == groupId);

        return liveRequirementChildren + liveGroupChildren;
    }

    /// <summary>
    /// Throws <see cref="RequirementGroupCycleException"/> if moving
    /// <paramref name="groupId"/> under <paramref name="candidateParentId"/>
    /// would make <paramref name="groupId"/> its own ancestor (`TD-67`) —
    /// <see cref="MoveGroupAsync"/>'s own guard, mirroring
    /// <c>EngineeringDomain.EngineeringObjectBase.GuardAgainstCircularParentAsync</c>'s
    /// own identical walk-the-chain algorithm, over
    /// <see cref="RequirementGroupDto.ParentGroupId"/> instead of
    /// <c>IHasParent.ParentId</c>.
    /// </summary>
    private async Task GuardAgainstGroupCycleAsync(Guid groupId, Guid candidateParentId, CancellationToken cancellationToken)
    {
        if (candidateParentId == groupId)
            throw new RequirementGroupCycleException(groupId, candidateParentId);

        var current = candidateParentId;
        var visited = new HashSet<Guid> { groupId };

        while (visited.Add(current))
        {
            var candidateDto = await ReadGroupDtoAsync(current, cancellationToken).ConfigureAwait(false);
            if (candidateDto?.ParentGroupId is not { } nextParentId)
                return;

            if (nextParentId == groupId)
                throw new RequirementGroupCycleException(groupId, candidateParentId);

            current = nextParentId;
        }
    }
}
