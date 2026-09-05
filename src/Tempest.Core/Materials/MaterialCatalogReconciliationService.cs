using System.Text.Json;
using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.Materials;

/// <summary>The concrete <see cref="IMaterialCatalogReconciliationService"/> implementation (`TD-67`).</summary>
public sealed class MaterialCatalogReconciliationService : IMaterialCatalogReconciliationService
{
    /// <summary>A material specification document exists with no matching entry in <see cref="MaterialCatalog.IndexCollectionName"/>.</summary>
    public const string MissingIndexEntryCategory = "MaterialMissingIndexEntry";

    /// <summary>A <c>materialId</c> index entry names a document that is no longer a live material specification.</summary>
    public const string StaleIndexEntryCategory = "StaleMaterialIndexEntry";

    private readonly IEngineeringDocumentStore _documentStore;
    private readonly IPersistenceStore _persistenceStore;
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="MaterialCatalogReconciliationService"/> class.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="documentStore"/> or <paramref name="persistenceStore"/> is <see langword="null"/>.</exception>
    public MaterialCatalogReconciliationService(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(persistenceStore);

        _documentStore = documentStore;
        _persistenceStore = persistenceStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<MaterialCatalogReconciliationReport> DetectAsync(CancellationToken cancellationToken = default) =>
        RunAsync(repair: false, cancellationToken);

    /// <inheritdoc />
    public Task<MaterialCatalogReconciliationReport> SweepAsync(CancellationToken cancellationToken = default) =>
        RunAsync(repair: true, cancellationToken);

    private async Task<MaterialCatalogReconciliationReport> RunAsync(bool repair, CancellationToken cancellationToken)
    {
        var findings = new List<MaterialCatalogReconciliationFinding>();

        // Every MaterialSpecification document. IEngineeringDocumentStore
        // has no "list documents of a Kind" capability of its own
        // (a disclosed, deliberate limitation), so this reads the
        // document store's own backing collection directly — the
        // identical direct-IPersistenceStore dependency MaterialCatalog
        // already has for its own materialId index (`ADR-0055`).
        var materialDocumentIds = new HashSet<Guid>();
        var documentKeys = await _persistenceStore.ListKeysAsync(EngineeringDocumentStore.DocumentsCollectionName, cancellationToken).ConfigureAwait(false);

        foreach (var key in documentKeys)
        {
            if (!Guid.TryParseExact(key, "N", out var documentId))
            {
                _logger?.Warning($"Ignoring non-document key '{key}' in '{EngineeringDocumentStore.DocumentsCollectionName}'.");
                continue;
            }

            var document = await _documentStore.FindAsync(documentId, cancellationToken).ConfigureAwait(false);
            if (document is not null && string.Equals(document.Kind, MaterialCatalog.MaterialSpecificationDocumentKind, StringComparison.Ordinal))
                materialDocumentIds.Add(documentId);
        }

        var indexedMaterialIds = await _persistenceStore.ListKeysAsync(MaterialCatalog.IndexCollectionName, cancellationToken).ConfigureAwait(false);

        var indexedDocumentIds = new HashSet<Guid>();
        foreach (var materialId in indexedMaterialIds)
        {
            var value = await _persistenceStore.ReadAsync(MaterialCatalog.IndexCollectionName, materialId, cancellationToken).ConfigureAwait(false);
            if (value is null || !Guid.TryParseExact(value, "N", out var indexedDocumentId))
                continue;

            indexedDocumentIds.Add(indexedDocumentId);

            if (!materialDocumentIds.Contains(indexedDocumentId))
            {
                var repaired = false;
                if (repair)
                {
                    await _persistenceStore.DeleteAsync(MaterialCatalog.IndexCollectionName, materialId, cancellationToken).ConfigureAwait(false);
                    repaired = true;
                }

                findings.Add(new MaterialCatalogReconciliationFinding(
                    StaleIndexEntryCategory, indexedDocumentId, materialId,
                    $"Material index entry '{materialId}' names document '{indexedDocumentId}', which is not a live material specification.",
                    repaired));
            }
        }

        foreach (var documentId in materialDocumentIds)
        {
            if (indexedDocumentIds.Contains(documentId))
                continue;

            string? materialId = null;
            try
            {
                var history = await _documentStore.GetRevisionHistoryAsync(documentId, cancellationToken).ConfigureAwait(false);
                var dto = JsonSerializer.Deserialize<MaterialSpecificationDto>(history[^1].Content);
                materialId = dto?.MaterialId;
            }
            catch (Exception ex) when (ex is EngineeringDataException or JsonException)
            {
                // A corrupted or crash-window orphan revision is reported
                // as-is rather than aborting the whole sweep (`TD-60`'s
                // established discipline) — it cannot be repaired without
                // a materialId to index it under.
                _logger?.Warning($"Material document '{documentId}' has no readable materialId and could not be reconciled.", ex);
            }

            var repaired = false;
            if (repair && materialId is not null)
            {
                // Never overwrite a genuine collision.
                var existing = await _persistenceStore.ReadAsync(MaterialCatalog.IndexCollectionName, materialId, cancellationToken).ConfigureAwait(false);
                if (existing is null)
                {
                    await _persistenceStore.WriteAsync(MaterialCatalog.IndexCollectionName, materialId, documentId.ToString("N"), cancellationToken).ConfigureAwait(false);
                    repaired = true;
                }
            }

            findings.Add(new MaterialCatalogReconciliationFinding(
                MissingIndexEntryCategory, documentId, materialId,
                materialId is null
                    ? $"Material document '{documentId}' has no index entry and no readable materialId to repair one from."
                    : $"Material document '{documentId}' (materialId '{materialId}') has no index entry.",
                repaired));
        }

        return new MaterialCatalogReconciliationReport(findings);
    }
}
