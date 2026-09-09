using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Assets;

/// <summary>A deterministic filter over the intellectual property register.</summary>
public sealed record IPAssetQuery
{
    /// <summary>Matches any asset whose reference or name contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these IP types. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<IPType> Types { get; init; } = [];

    /// <summary>Matches any of these origins. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<IPOrigin> Origins { get; init; } = [];

    /// <summary>Matches any of these ownership positions. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<IPOwnership> Ownerships { get; init; } = [];

    /// <summary>Matches assets whose ownership is asserted without evidence, or only those where it is evidenced. <see langword="null"/> to match any.</summary>
    public bool? OwnershipIsEvidenced { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The organisation's intellectual property register.</summary>
public interface IIPAssetCatalog : IReferenceDataCatalog<IPAsset>
{
    /// <summary>Returns the asset registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<IPAsset>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered asset matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<IPAsset>>> SearchAsync(IPAssetQuery query, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IIPAssetCatalog"/> implementation.</summary>
public sealed class IPAssetCatalog : ReferenceDataCatalog<IPAsset>, IIPAssetCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every IP-asset record's own backing document carries.</summary>
    public const string IPAssetDocumentKind = "BusinessIPAsset";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>assetId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessIPAssets.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each asset reference to the <c>assetId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessIPAssets.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="IPAssetCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own asset records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public IPAssetCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "BusinessIPAssets";

    /// <inheritdoc />
    public override string DocumentKind => IPAssetDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<IPAsset>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(IPAsset.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<IPAsset>>> SearchAsync(IPAssetQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(IPAsset definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(IPAsset definition) => $"IP asset reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<IPAsset> record, IPAssetQuery query)
    {
        var asset = record.Definition;

        if (query.TextContains is { } text
            && !asset.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !asset.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Types.Count > 0 && !query.Types.Contains(asset.Type))
            return false;

        if (query.Origins.Count > 0 && !query.Origins.Contains(asset.Origin))
            return false;

        if (query.Ownerships.Count > 0 && !query.Ownerships.Contains(asset.Ownership))
            return false;

        if (query.OwnershipIsEvidenced is { } evidenced && asset.IsOwnershipEvidenced != evidenced)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>A deterministic filter over the data-asset register.</summary>
public sealed record DataAssetQuery
{
    /// <summary>Matches any asset whose reference or name contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these data categories. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<DataCategory> Categories { get; init; } = [];

    /// <summary>Matches only assets holding information about identifiable people. <see langword="null"/> to match any.</summary>
    public bool? IsPersonalData { get; init; }

    /// <summary>Matches assets with a retention rule, without one, or either. <see langword="null"/> to match any.</summary>
    public bool? HasRetentionRule { get; init; }

    /// <summary>Matches any of these compliance-review states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<DeterminationState> ComplianceReviewStates { get; init; } = [];

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The organisation's data-asset register.</summary>
public interface IDataAssetCatalog : IReferenceDataCatalog<DataAsset>
{
    /// <summary>Returns the asset registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<DataAsset>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered asset matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<DataAsset>>> SearchAsync(DataAssetQuery query, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IDataAssetCatalog"/> implementation.</summary>
public sealed class DataAssetCatalog : ReferenceDataCatalog<DataAsset>, IDataAssetCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every data-asset record's own backing document carries.</summary>
    public const string DataAssetDocumentKind = "BusinessDataAsset";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>assetId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessDataAssets.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each asset reference to the <c>assetId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessDataAssets.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="DataAssetCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own asset records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public DataAssetCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "BusinessDataAssets";

    /// <inheritdoc />
    public override string DocumentKind => DataAssetDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<DataAsset>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(DataAsset.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<DataAsset>>> SearchAsync(DataAssetQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(DataAsset definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(DataAsset definition) => $"Data asset reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<DataAsset> record, DataAssetQuery query)
    {
        var asset = record.Definition;

        if (query.TextContains is { } text
            && !asset.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !asset.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Categories.Count > 0 && !query.Categories.Contains(asset.Category))
            return false;

        if (query.IsPersonalData is { } personal && asset.IsPersonalData != personal)
            return false;

        if (query.HasRetentionRule is { } retention && asset.HasRetentionRule != retention)
            return false;

        if (query.ComplianceReviewStates.Count > 0 && !query.ComplianceReviewStates.Contains(asset.ComplianceReviewState))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}
