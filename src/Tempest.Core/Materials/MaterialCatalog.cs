using System.Text.Json;
using Tempest.Core.Concurrency;
using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.Materials;

/// <summary>
/// The concrete <see cref="IMaterialCatalog"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// <b>A thin, typed index over <see cref="IEngineeringDocumentStore"/>,
/// not a second storage mechanism</b> (`ADR-0055`, continuing `ADR-0053`'s
/// own precedent): every material specification is itself an
/// <see cref="IEngineeringDocument"/> of <c>Kind = "MaterialSpecification"</c>,
/// with its properties serialized as JSON into the document's own
/// <see cref="IDocumentRevision.Content"/>.
/// </para>
/// <para>
/// <b>One genuine, disclosed implementation-time finding</b> (`ADR-0055`):
/// <see cref="IEngineeringDocumentStore"/> provides no way to look up a
/// document by an arbitrary caller-chosen string, and no way to enumerate
/// every document of a given <c>Kind</c> — both are needed for
/// <see cref="FindAsync"/>/<see cref="ListAsync"/>/duplicate-registration
/// checking. This class therefore depends directly on
/// <see cref="IPersistenceStore"/> too, for its own small
/// <c>materialId</c>-to-<c>documentId</c> index
/// (<see cref="IndexCollectionName"/>) — a direct, not merely indirect,
/// Persistence dependency the approved contract's own "indirectly, through
/// <see cref="IEngineeringDocumentStore"/>" framing did not anticipate.
/// </para>
/// <para>
/// <b>Duplicate-registration atomicity:</b> a per-<c>materialId</c>
/// <see cref="AsyncKeyedLock"/> serialises <see cref="RegisterAsync"/>'s own
/// check-then-write sequence, mirroring
/// <see cref="EngineeringDocumentStore"/>'s own per-document lock rationale
/// for <see cref="IEngineeringDocumentStore.ReviseAsync"/> — two concurrent
/// <see cref="RegisterAsync"/> calls for the same <c>materialId</c> can
/// never both succeed.
/// </para>
/// </remarks>
public sealed class MaterialCatalog : IMaterialCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every material specification's own backing document carries.</summary>
    public const string MaterialSpecificationDocumentKind = "MaterialSpecification";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>materialId</c> to its own backing document Id.</summary>
    public const string IndexCollectionName = "Materials.Index";

    private readonly IEngineeringDocumentStore _documentStore;
    private readonly IPersistenceStore _persistenceStore;
    private readonly ILogger? _logger;
    private readonly AsyncKeyedLock _registrationLock = new();

    /// <summary>
    /// Initialises a new instance of the <see cref="MaterialCatalog"/> class.
    /// </summary>
    /// <param name="documentStore">The store this instance's own material specifications are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own <c>materialId</c> index is held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="documentStore"/> or <paramref name="persistenceStore"/> is <see langword="null"/>.</exception>
    public MaterialCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(persistenceStore);

        _documentStore = documentStore;
        _persistenceStore = persistenceStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IMaterialSpecification> RegisterAsync(
        string materialId,
        string name,
        IReadOnlyDictionary<string, MaterialProperty> properties,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(properties);

        using (await _registrationLock.AcquireAsync(materialId, cancellationToken).ConfigureAwait(false))
        {
            if (await ReadDocumentIdAsync(materialId, cancellationToken).ConfigureAwait(false) is not null)
                throw new DuplicateMaterialException(materialId);

            var dto = new MaterialSpecificationDto(materialId, name, category, EncodeProperties(properties));
            var document = await _documentStore.CreateAsync(MaterialSpecificationDocumentKind, JsonSerializer.Serialize(dto), cancellationToken)
                .ConfigureAwait(false);

            await _persistenceStore.WriteAsync(IndexCollectionName, materialId, document.Id.ToString("N"), cancellationToken)
                .ConfigureAwait(false);

            _logger?.Information($"Material registered: '{materialId}' (document '{document.Id}').");

            return new MaterialSpecification(materialId, name, category, properties, document.Id, document.CurrentRevisionNumber);
        }
    }

    /// <inheritdoc />
    public async Task<IMaterialSpecification?> FindAsync(string materialId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialId);

        var documentId = await ReadDocumentIdAsync(materialId, cancellationToken).ConfigureAwait(false);
        if (documentId is null)
            return null;

        return await ReadSpecificationAsync(materialId, documentId.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IMaterialSpecification>> ListAsync(CancellationToken cancellationToken = default)
    {
        var materialIds = await _persistenceStore.ListKeysAsync(IndexCollectionName, cancellationToken).ConfigureAwait(false);
        var specifications = new List<IMaterialSpecification>(materialIds.Count);

        foreach (var materialId in materialIds)
        {
            var documentId = await ReadDocumentIdAsync(materialId, cancellationToken).ConfigureAwait(false);
            if (documentId is null)
                continue;

            // A stale index entry (its backing document gone, or of another
            // Kind) is skipped rather than aborting the whole listing —
            // mirroring RequirementsService.ListAsync's own identical guard.
            var specification = await ReadSpecificationAsync(materialId, documentId.Value, cancellationToken).ConfigureAwait(false);
            if (specification is not null)
                specifications.Add(specification);
        }

        return specifications;
    }

    /// <inheritdoc />
    public async Task<IMaterialSpecification> ReviseAsync(
        string materialId,
        IReadOnlyDictionary<string, MaterialProperty> properties,
        string? changeSummary,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialId);
        ArgumentNullException.ThrowIfNull(properties);

        var documentId = await ReadDocumentIdAsync(materialId, cancellationToken).ConfigureAwait(false)
            ?? throw new MaterialNotFoundException(materialId);

        var current = await ReadDtoAsync(materialId, documentId, cancellationToken).ConfigureAwait(false)
            ?? throw new MaterialNotFoundException(materialId);
        var dto = current with { Properties = EncodeProperties(properties) };

        var revision = await _documentStore.ReviseAsync(documentId, JsonSerializer.Serialize(dto), changeSummary, cancellationToken)
            .ConfigureAwait(false);

        _logger?.Information($"Material revised: '{materialId}' (revision {revision.RevisionNumber}).");

        return new MaterialSpecification(materialId, dto.Name, dto.Category, properties, documentId, revision.RevisionNumber);
    }

    /// <summary>
    /// Resolves <paramref name="materialId"/>'s backing document Id from
    /// the index. A malformed index value throws a controlled
    /// <see cref="MaterialsException"/> naming the entry (`TD-60`) —
    /// never a raw <see cref="FormatException"/>, and never
    /// <see langword="null"/>, which would silently misreport corruption
    /// as "no such material".
    /// </summary>
    private async Task<Guid?> ReadDocumentIdAsync(string materialId, CancellationToken cancellationToken)
    {
        var value = await _persistenceStore.ReadAsync(IndexCollectionName, materialId, cancellationToken).ConfigureAwait(false);
        if (value is null)
            return null;

        if (!Guid.TryParseExact(value, "N", out var documentId))
            throw new MaterialsException(
                $"Material index entry for '{materialId}' is corrupted: '{value}' is not a valid document Id.");

        return documentId;
    }

    private async Task<MaterialSpecificationDto?> ReadDtoAsync(string materialId, Guid documentId, CancellationToken cancellationToken)
    {
        // A stale index entry — backing document missing, or of another
        // Kind — reads as "no material", mirroring
        // RequirementsService.ReadDtoAsync's own identical guard, rather
        // than letting GetRevisionHistoryAsync throw for a document this
        // catalog never wrote.
        var document = await _documentStore.FindAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, MaterialSpecificationDocumentKind, StringComparison.Ordinal))
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (history.Count == 0)
            throw new MaterialsException($"Material '{materialId}' (document '{documentId}') has no revisions.");

        return DeserialiseDto(materialId, documentId, history[^1].Content);
    }

    private async Task<IMaterialSpecification?> ReadSpecificationAsync(string materialId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _documentStore.FindAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, MaterialSpecificationDocumentKind, StringComparison.Ordinal))
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (history.Count == 0)
            throw new MaterialsException($"Material '{materialId}' (document '{documentId}') has no revisions.");

        var currentRevision = history[^1];
        var dto = DeserialiseDto(materialId, documentId, currentRevision.Content);

        return new MaterialSpecification(
            dto.MaterialId, dto.Name, dto.Category, DecodeProperties(dto.Properties), documentId, currentRevision.RevisionNumber);
    }

    /// <summary>Deserialises one revision's content, converting any malformed-content failure into a controlled <see cref="MaterialsException"/> (`TD-60`) rather than a raw <see cref="JsonException"/>.</summary>
    private static MaterialSpecificationDto DeserialiseDto(string materialId, Guid documentId, string content)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<MaterialSpecificationDto>(content)
                ?? throw new MaterialsException($"Material '{materialId}' (document '{documentId}') could not be deserialised.");

            // A structurally-valid JSON object missing the Properties
            // field would otherwise surface later as a raw
            // NullReferenceException inside DecodeProperties.
            if (dto.Properties is null)
                throw new MaterialsException($"Material '{materialId}' (document '{documentId}') is missing its Properties.");

            return dto;
        }
        catch (JsonException ex)
        {
            throw new MaterialsException($"Material '{materialId}' (document '{documentId}') could not be deserialised.", ex);
        }
    }

    private static IReadOnlyDictionary<string, MaterialPropertyDto> EncodeProperties(IReadOnlyDictionary<string, MaterialProperty> properties)
    {
        var encoded = new Dictionary<string, MaterialPropertyDto>(properties.Count);
        foreach (var (name, property) in properties)
        {
            var (dimensionKind, value, unitSymbol, unitToBaseFactor) = MaterialPropertyValueCodec.Encode(property.Value);
            encoded[name] = new MaterialPropertyDto(dimensionKind, value, unitSymbol, unitToBaseFactor, property.Provenance);
        }

        return encoded;
    }

    private static IReadOnlyDictionary<string, MaterialProperty> DecodeProperties(IReadOnlyDictionary<string, MaterialPropertyDto> properties)
    {
        var decoded = new Dictionary<string, MaterialProperty>(properties.Count);
        foreach (var (name, dto) in properties)
        {
            var value = MaterialPropertyValueCodec.Decode(dto.DimensionKind, dto.Value, dto.UnitSymbol, dto.UnitToBaseFactor);
            decoded[name] = new MaterialProperty(value, dto.Provenance);
        }

        return decoded;
    }
}
