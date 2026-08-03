using System.Text.Json;
using Tempest.Core.Concurrency;
using Tempest.Core.EngineeringData;
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

    /// <summary>The <see cref="IRequirement.CreatedByPrincipalId"/> recorded when no principal is currently established.</summary>
    public const string UnknownPrincipalId = "unknown";

    private readonly IEngineeringDocumentStore _documentStore;
    private readonly IPersistenceStore _persistenceStore;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IVerificationService _verificationService;
    private readonly ILogger? _logger;
    private readonly AsyncKeyedLock _identifierLock = new();

    /// <summary>Initialises a new instance of the <see cref="RequirementsService"/> class.</summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="documentStore"/>, <paramref name="persistenceStore"/>,
    /// <paramref name="currentPrincipalAccessor"/>, or <paramref name="verificationService"/>
    /// is <see langword="null"/>.
    /// </exception>
    public RequirementsService(
        IEngineeringDocumentStore documentStore,
        IPersistenceStore persistenceStore,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IVerificationService verificationService,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(persistenceStore);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(verificationService);

        _documentStore = documentStore;
        _persistenceStore = persistenceStore;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _verificationService = verificationService;
        _logger = logger;
    }

    /// <inheritdoc />
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

            var dto = new RequirementDto(identifier, statement, category, RequirementStatus.Draft, createdBy, createdAt);
            var document = await _documentStore.CreateAsync(RequirementDocumentKind, JsonSerializer.Serialize(dto), cancellationToken)
                .ConfigureAwait(false);

            await _persistenceStore.WriteAsync(IdentifierIndexCollectionName, identifier, document.Id.ToString("N"), cancellationToken)
                .ConfigureAwait(false);

            _logger?.Information($"Requirement created: '{identifier}' (document '{document.Id}').");

            return new Requirement(document.Id, identifier, statement, category, RequirementStatus.Draft, document.CurrentRevisionNumber, createdBy, createdAt);
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
        var revision = await _documentStore.ReviseAsync(requirementId, JsonSerializer.Serialize(dto), changeSummary, cancellationToken)
            .ConfigureAwait(false);

        _logger?.Information($"Requirement revised: '{dto.Identifier}' (revision {revision.RevisionNumber}).");

        return new Requirement(requirementId, dto.Identifier, newStatement, dto.Category, dto.Status, revision.RevisionNumber, dto.CreatedByPrincipalId, dto.CreatedAt);
    }

    /// <inheritdoc />
    public async Task SetStatusAsync(Guid requirementId, RequirementStatus status, CancellationToken cancellationToken = default)
    {
        var current = await ReadDtoAsync(requirementId, cancellationToken).ConfigureAwait(false)
            ?? throw new RequirementNotFoundException(requirementId);

        if (!RequirementStatusTransitions.IsPermitted(current.Status, status))
            throw new InvalidRequirementStatusTransitionException(current.Status, status);

        var dto = current with { Status = status };
        await _documentStore.ReviseAsync(requirementId, JsonSerializer.Serialize(dto), $"Status changed to {status}.", cancellationToken)
            .ConfigureAwait(false);

        _logger?.Information($"Requirement status changed: '{dto.Identifier}' → '{status}'.");
    }

    /// <inheritdoc />
    public async Task LinkAsync(Guid sourceRequirementId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipKind);

        if (await _documentStore.FindAsync(sourceRequirementId, cancellationToken).ConfigureAwait(false) is null)
            throw new RequirementNotFoundException(sourceRequirementId);

        await _documentStore.LinkAsync(sourceRequirementId, targetDocumentId, relationshipKind, cancellationToken).ConfigureAwait(false);

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
        var document = await _documentStore.CreateAsync(RequirementCollectionDocumentKind, JsonSerializer.Serialize(dto), cancellationToken)
            .ConfigureAwait(false);

        _logger?.Information($"Requirement collection created: '{name}' (document '{document.Id}').");

        return new RequirementCollection(document.Id, name, []);
    }

    /// <inheritdoc />
    public async Task<IRequirementCollection?> FindCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        var document = await _documentStore.FindAsync(collectionId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, RequirementCollectionDocumentKind, StringComparison.Ordinal))
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(collectionId, cancellationToken).ConfigureAwait(false);
        var dto = JsonSerializer.Deserialize<RequirementCollectionDto>(history[^1].Content)
            ?? throw new EngineeringDataException($"Requirement collection '{collectionId}' could not be deserialised.");

        var members = await _documentStore.GetReferencesAsync(collectionId, cancellationToken).ConfigureAwait(false);
        var memberIds = members
            .Where(r => string.Equals(r.RelationshipKind, RequirementRelationshipKinds.CollectedIn, StringComparison.Ordinal))
            .Select(r => r.TargetDocumentId)
            .ToList();

        return new RequirementCollection(collectionId, dto.Name, memberIds);
    }

    /// <inheritdoc />
    public async Task AddToCollectionAsync(Guid collectionId, Guid requirementId, CancellationToken cancellationToken = default)
    {
        var collectionDocument = await _documentStore.FindAsync(collectionId, cancellationToken).ConfigureAwait(false);
        if (collectionDocument is null || !string.Equals(collectionDocument.Kind, RequirementCollectionDocumentKind, StringComparison.Ordinal))
            throw new EngineeringDocumentNotFoundException(collectionId);

        if (await _documentStore.FindAsync(requirementId, cancellationToken).ConfigureAwait(false) is null)
            throw new RequirementNotFoundException(requirementId);

        await _documentStore.LinkAsync(collectionId, requirementId, RequirementRelationshipKinds.CollectedIn, cancellationToken).ConfigureAwait(false);

        _logger?.Information($"Requirement '{requirementId}' added to collection '{collectionId}'.");
    }

    /// <inheritdoc />
    public async Task<IRequirementGroup> CreateGroupAsync(string name, Guid? parentGroupId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (parentGroupId is not null && await _documentStore.FindAsync(parentGroupId.Value, cancellationToken).ConfigureAwait(false) is null)
            throw new EngineeringDocumentNotFoundException(parentGroupId.Value);

        var dto = new RequirementGroupDto(name);
        var document = await _documentStore.CreateAsync(RequirementGroupDocumentKind, JsonSerializer.Serialize(dto), cancellationToken)
            .ConfigureAwait(false);

        if (parentGroupId is not null)
            await _documentStore.LinkAsync(document.Id, parentGroupId.Value, RequirementRelationshipKinds.GroupedUnder, cancellationToken).ConfigureAwait(false);

        _logger?.Information($"Requirement group created: '{name}' (document '{document.Id}').");

        return new RequirementGroup(document.Id, name, parentGroupId);
    }

    /// <inheritdoc />
    public async Task<IRequirementGroup?> FindGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var document = await _documentStore.FindAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, RequirementGroupDocumentKind, StringComparison.Ordinal))
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(groupId, cancellationToken).ConfigureAwait(false);
        var dto = JsonSerializer.Deserialize<RequirementGroupDto>(history[^1].Content)
            ?? throw new EngineeringDataException($"Requirement group '{groupId}' could not be deserialised.");

        var references = await _documentStore.GetReferencesAsync(groupId, cancellationToken).ConfigureAwait(false);
        var parentGroupId = references
            .Where(r => string.Equals(r.RelationshipKind, RequirementRelationshipKinds.GroupedUnder, StringComparison.Ordinal))
            .Select(r => (Guid?)r.TargetDocumentId)
            .FirstOrDefault();

        return new RequirementGroup(groupId, dto.Name, parentGroupId);
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

    private async Task<Guid?> ReadDocumentIdAsync(string identifier, CancellationToken cancellationToken)
    {
        var value = await _persistenceStore.ReadAsync(IdentifierIndexCollectionName, identifier, cancellationToken).ConfigureAwait(false);
        return value is null ? null : Guid.ParseExact(value, "N");
    }

    private async Task<RequirementDto?> ReadDtoAsync(Guid requirementId, CancellationToken cancellationToken)
    {
        var document = await _documentStore.FindAsync(requirementId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, RequirementDocumentKind, StringComparison.Ordinal))
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(requirementId, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<RequirementDto>(history[^1].Content)
            ?? throw new EngineeringDataException($"Requirement '{requirementId}' could not be deserialised.");
    }

    private async Task<IRequirement?> ReadRequirementAsync(Guid requirementId, CancellationToken cancellationToken)
    {
        var document = await _documentStore.FindAsync(requirementId, cancellationToken).ConfigureAwait(false);
        if (document is null)
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(requirementId, cancellationToken).ConfigureAwait(false);
        var currentRevision = history[^1];
        var dto = JsonSerializer.Deserialize<RequirementDto>(currentRevision.Content)
            ?? throw new EngineeringDataException($"Requirement '{requirementId}' could not be deserialised.");

        return new Requirement(requirementId, dto.Identifier, dto.Statement, dto.Category, dto.Status, currentRevision.RevisionNumber, dto.CreatedByPrincipalId, dto.CreatedAt);
    }

    private string ResolveCurrentPrincipalId() =>
        _currentPrincipalAccessor.Current?.Identity.Id ?? UnknownPrincipalId;
}
