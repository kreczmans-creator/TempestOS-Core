using System.Text.Json;
using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.Requirements;

/// <summary>The concrete <see cref="IRequirementsReconciliationService"/> implementation (`TD-67`).</summary>
public sealed class RequirementsReconciliationService : IRequirementsReconciliationService
{
    /// <summary>A Requirement document exists with no matching entry in <see cref="RequirementsService.IdentifierIndexCollectionName"/>.</summary>
    public const string RequirementMissingIndexEntryCategory = "RequirementMissingIndexEntry";

    /// <summary>An identifier-index entry names a document that is no longer a live Requirement of the expected Kind.</summary>
    public const string StaleIdentifierIndexEntryCategory = "StaleIdentifierIndexEntry";

    /// <summary>A RequirementCollection document exists with no matching entry in <see cref="RequirementsService.CollectionRegistryCollectionName"/>.</summary>
    public const string CollectionMissingRegistryEntryCategory = "RequirementCollectionMissingRegistryEntry";

    /// <summary>A collection-registry entry names a document that is no longer a live RequirementCollection of the expected Kind.</summary>
    public const string StaleCollectionRegistryEntryCategory = "StaleRequirementCollectionRegistryEntry";

    /// <summary>A RequirementGroup document exists with no matching entry in <see cref="RequirementsService.GroupRegistryCollectionName"/>.</summary>
    public const string GroupMissingRegistryEntryCategory = "RequirementGroupMissingRegistryEntry";

    /// <summary>A group-registry entry names a document that is no longer a live RequirementGroup of the expected Kind.</summary>
    public const string StaleGroupRegistryEntryCategory = "StaleRequirementGroupRegistryEntry";

    private readonly IEngineeringDocumentStore _documentStore;
    private readonly IPersistenceStore _persistenceStore;
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="RequirementsReconciliationService"/> class.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="documentStore"/> or <paramref name="persistenceStore"/> is <see langword="null"/>.</exception>
    public RequirementsReconciliationService(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(persistenceStore);

        _documentStore = documentStore;
        _persistenceStore = persistenceStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<RequirementsReconciliationReport> DetectAsync(CancellationToken cancellationToken = default) =>
        RunAsync(repair: false, cancellationToken);

    /// <inheritdoc />
    public Task<RequirementsReconciliationReport> SweepAsync(CancellationToken cancellationToken = default) =>
        RunAsync(repair: true, cancellationToken);

    /// <remarks>
    /// <b>Read order (`WP 16.4B-R2`):</b> every index/registry entry this
    /// service can ever see was written strictly <em>after</em> the
    /// document it names — <see cref="RequirementsService.CreateAsync"/>,
    /// <c>CreateCollectionAsync</c> and <c>CreateGroupAsync</c> each create
    /// their own document first and write the index/registry entry
    /// second, never the other way round, and no document is ever deleted
    /// out from under a live entry (`DeleteAsync` is a soft delete that
    /// revises the document; it never removes the row or changes its
    /// Kind). So a sweep that reads every index/registry (the derived
    /// side) <em>before</em> it reads the documents (the authoritative
    /// side) can never observe an index/registry entry whose document it
    /// then fails to see: if the entry's write had already landed when
    /// this sweep's derived-side read ran, the document write that
    /// strictly preceded it had already landed too, and the
    /// authoritative-side read — running later, not earlier — is
    /// guaranteed to see it. Reading in the other order (documents first)
    /// is exactly what let a document created after the document scan but
    /// indexed before the index scan look "stale" and be deleted — the
    /// live-data-loss shape this ordering removes by construction, not
    /// merely narrows. A registration caught mid-flight by this ordering
    /// (its document visible, its index/registry entry not yet, because
    /// this sweep's own derived-side read simply ran first) is reported as
    /// <c>*MissingIndexEntryCategory</c>/<c>*MissingRegistryEntryCategory</c>
    /// — a false "missing", not a deletion — and the repair path underneath
    /// re-reads the live value before ever writing, so it can never
    /// clobber the write the in-flight registration is about to make.
    /// </remarks>
    private async Task<RequirementsReconciliationReport> RunAsync(bool repair, CancellationToken cancellationToken)
    {
        var findings = new List<RequirementsReconciliationFinding>();

        // Derived side first — every index/registry this service reconciles.
        var identifierIndexEntries = await ReadIndexEntriesAsync(RequirementsService.IdentifierIndexCollectionName, cancellationToken).ConfigureAwait(false);
        var collectionRegistryIds = await ReadRegistryIdsAsync(RequirementsService.CollectionRegistryCollectionName, cancellationToken).ConfigureAwait(false);
        var groupRegistryIds = await ReadRegistryIdsAsync(RequirementsService.GroupRegistryCollectionName, cancellationToken).ConfigureAwait(false);

        // Authoritative side second — the documents themselves.
        var (requirementIds, collectionIds, groupIds) = await PartitionDocumentsByKindAsync(cancellationToken).ConfigureAwait(false);

        await ReconcileIdentifierIndexAsync(requirementIds, identifierIndexEntries, findings, repair, cancellationToken).ConfigureAwait(false);
        await ReconcileRegistryAsync(
            RequirementsService.CollectionRegistryCollectionName, collectionIds, collectionRegistryIds,
            CollectionMissingRegistryEntryCategory, StaleCollectionRegistryEntryCategory,
            findings, repair, cancellationToken).ConfigureAwait(false);
        await ReconcileRegistryAsync(
            RequirementsService.GroupRegistryCollectionName, groupIds, groupRegistryIds,
            GroupMissingRegistryEntryCategory, StaleGroupRegistryEntryCategory,
            findings, repair, cancellationToken).ConfigureAwait(false);

        return new RequirementsReconciliationReport(findings);
    }

    /// <summary>
    /// Every <c>(key, documentId)</c> pair currently in
    /// <paramref name="indexCollectionName"/> — the raw scan
    /// <see cref="ReconcileIdentifierIndexAsync"/> used to do inline,
    /// pulled out so <see cref="RunAsync"/> can run every derived-side
    /// read before any authoritative-side one (`WP 16.4B-R2`).
    /// </summary>
    private async Task<List<(string Key, Guid DocumentId)>> ReadIndexEntriesAsync(string indexCollectionName, CancellationToken cancellationToken)
    {
        var keys = await _persistenceStore.ListKeysAsync(indexCollectionName, cancellationToken).ConfigureAwait(false);
        var entries = new List<(string Key, Guid DocumentId)>(keys.Count);

        foreach (var key in keys)
        {
            var value = await _persistenceStore.ReadAsync(indexCollectionName, key, cancellationToken).ConfigureAwait(false);
            if (value is not null && Guid.TryParseExact(value, "N", out var documentId))
                entries.Add((key, documentId));
        }

        return entries;
    }

    /// <summary>
    /// Every document Id currently registered in a Collection/Group
    /// registry (both keyed by the document's own Id, mapped to itself)
    /// — the raw scan <see cref="ReconcileRegistryAsync"/> used to do
    /// inline, pulled out for the same reordering reason as
    /// <see cref="ReadIndexEntriesAsync"/>.
    /// </summary>
    private async Task<HashSet<Guid>> ReadRegistryIdsAsync(string registryCollectionName, CancellationToken cancellationToken)
    {
        var registryKeys = await _persistenceStore.ListKeysAsync(registryCollectionName, cancellationToken).ConfigureAwait(false);

        var registeredIds = new HashSet<Guid>();
        foreach (var key in registryKeys)
        {
            if (Guid.TryParseExact(key, "N", out var registeredId))
                registeredIds.Add(registeredId);
        }

        return registeredIds;
    }

    /// <summary>
    /// Every document this service cares about, split by Kind —
    /// <see cref="IEngineeringDocumentStore"/> has no "list documents of
    /// a Kind" capability of its own (a disclosed, deliberate limitation
    /// this namespace's own <see cref="RequirementsService"/> already
    /// works around for its registries), so this reads the document
    /// store's own backing collection directly, exactly the sibling
    /// direct-<see cref="IPersistenceStore"/> dependency
    /// <see cref="RequirementsService"/> already has for the identical
    /// reason.
    /// </summary>
    private async Task<(HashSet<Guid> RequirementIds, HashSet<Guid> CollectionIds, HashSet<Guid> GroupIds)> PartitionDocumentsByKindAsync(CancellationToken cancellationToken)
    {
        var requirementIds = new HashSet<Guid>();
        var collectionIds = new HashSet<Guid>();
        var groupIds = new HashSet<Guid>();

        var documentKeys = await _persistenceStore.ListKeysAsync(EngineeringDocumentStore.DocumentsCollectionName, cancellationToken).ConfigureAwait(false);

        foreach (var key in documentKeys)
        {
            if (!Guid.TryParseExact(key, "N", out var documentId))
            {
                _logger?.Warning($"Ignoring non-document key '{key}' in '{EngineeringDocumentStore.DocumentsCollectionName}'.");
                continue;
            }

            var document = await _documentStore.FindAsync(documentId, cancellationToken).ConfigureAwait(false);
            if (document is null)
                continue;

            if (string.Equals(document.Kind, RequirementsService.RequirementDocumentKind, StringComparison.Ordinal))
                requirementIds.Add(documentId);
            else if (string.Equals(document.Kind, RequirementsService.RequirementCollectionDocumentKind, StringComparison.Ordinal))
                collectionIds.Add(documentId);
            else if (string.Equals(document.Kind, RequirementsService.RequirementGroupDocumentKind, StringComparison.Ordinal))
                groupIds.Add(documentId);
        }

        return (requirementIds, collectionIds, groupIds);
    }

    private async Task ReconcileIdentifierIndexAsync(
        HashSet<Guid> requirementDocumentIds,
        List<(string Key, Guid DocumentId)> indexEntries,
        List<RequirementsReconciliationFinding> findings,
        bool repair,
        CancellationToken cancellationToken)
    {
        var indexedDocumentIds = new HashSet<Guid>();
        foreach (var (identifier, indexedDocumentId) in indexEntries)
        {
            indexedDocumentIds.Add(indexedDocumentId);

            // Reverse direction: an index entry naming a document that is
            // not a live Requirement (deleted document row entirely, or
            // repurposed under another Kind — neither is possible through
            // this platform's own normal write paths, but a hand-edited
            // store or a partial restore can produce one).
            if (!requirementDocumentIds.Contains(indexedDocumentId))
            {
                var repaired = false;
                if (repair)
                {
                    await _persistenceStore.DeleteAsync(RequirementsService.IdentifierIndexCollectionName, identifier, cancellationToken).ConfigureAwait(false);
                    repaired = true;
                }

                findings.Add(new RequirementsReconciliationFinding(
                    StaleIdentifierIndexEntryCategory, indexedDocumentId, identifier,
                    $"Identifier index entry '{identifier}' names document '{indexedDocumentId}', which is not a live Requirement.",
                    repaired));
            }
        }

        // Forward direction: a Requirement document with nothing indexing
        // it — the orphan `TD-67`'s own register entry names.
        foreach (var documentId in requirementDocumentIds)
        {
            if (indexedDocumentIds.Contains(documentId))
                continue;

            string? identifier = null;
            try
            {
                var history = await _documentStore.GetRevisionHistoryAsync(documentId, cancellationToken).ConfigureAwait(false);
                var dto = JsonSerializer.Deserialize<RequirementDto>(history[^1].Content);
                identifier = dto?.Identifier;
            }
            catch (Exception ex) when (ex is EngineeringDataException or JsonException)
            {
                // A corrupted or (post-`TD-67` ordering fix) crash-window
                // orphan revision is reported as-is rather than aborting
                // the whole sweep (`TD-60`'s established discipline) — it
                // cannot be repaired without an identifier to index it
                // under.
                _logger?.Warning($"Requirement document '{documentId}' has no readable identifier and could not be reconciled.", ex);
            }

            var repaired = false;
            if (repair && identifier is not null)
            {
                // Never overwrite a genuine collision — a different
                // document already registered under this identifier is a
                // real conflict this sweep must not silently resolve by
                // picking a winner.
                var existing = await _persistenceStore.ReadAsync(RequirementsService.IdentifierIndexCollectionName, identifier, cancellationToken).ConfigureAwait(false);
                if (existing is null)
                {
                    await _persistenceStore.WriteAsync(RequirementsService.IdentifierIndexCollectionName, identifier, documentId.ToString("N"), cancellationToken).ConfigureAwait(false);
                    repaired = true;
                }
            }

            findings.Add(new RequirementsReconciliationFinding(
                RequirementMissingIndexEntryCategory, documentId, identifier,
                identifier is null
                    ? $"Requirement document '{documentId}' has no identifier-index entry and no readable identifier to repair one from."
                    : $"Requirement document '{documentId}' (identifier '{identifier}') has no identifier-index entry.",
                repaired));
        }
    }

    /// <summary>
    /// The shared shape of the Collection/Group registry check — both are
    /// keyed by the document's own Id, mapped to itself, so no DTO content
    /// needs reading to repair a missing entry.
    /// </summary>
    private async Task ReconcileRegistryAsync(
        string registryCollectionName,
        HashSet<Guid> documentIds,
        HashSet<Guid> registeredIds,
        string missingCategory,
        string staleCategory,
        List<RequirementsReconciliationFinding> findings,
        bool repair,
        CancellationToken cancellationToken)
    {
        foreach (var documentId in documentIds)
        {
            if (registeredIds.Contains(documentId))
                continue;

            var repaired = false;
            if (repair)
            {
                await _persistenceStore.WriteAsync(registryCollectionName, documentId.ToString("N"), documentId.ToString("N"), cancellationToken).ConfigureAwait(false);
                repaired = true;
            }

            findings.Add(new RequirementsReconciliationFinding(
                missingCategory, documentId, null,
                $"Document '{documentId}' has no entry in registry '{registryCollectionName}'.",
                repaired));
        }

        foreach (var registeredId in registeredIds)
        {
            if (documentIds.Contains(registeredId))
                continue;

            var repaired = false;
            if (repair)
            {
                await _persistenceStore.DeleteAsync(registryCollectionName, registeredId.ToString("N"), cancellationToken).ConfigureAwait(false);
                repaired = true;
            }

            findings.Add(new RequirementsReconciliationFinding(
                staleCategory, registeredId, null,
                $"Registry '{registryCollectionName}' entry '{registeredId}' names a document that is no longer live under the expected Kind.",
                repaired));
        }
    }
}
