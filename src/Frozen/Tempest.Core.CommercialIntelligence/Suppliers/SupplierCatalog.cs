using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Suppliers;

/// <summary>A deterministic filter over the supplier database.</summary>
public sealed record SupplierQuery
{
    /// <summary>Matches any supplier answering to a name containing this text, ignoring case. <see langword="null"/> to match any.</summary>
    /// <remarks>Searches every alias, not only the legal name — which is the point of holding aliases.</remarks>
    public string? NameContains { get; init; }

    /// <summary>Matches any of these statuses. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<SupplierStatus> Statuses { get; init; } = [];

    /// <summary>Matches suppliers recording a capability for this `A7` process. <see langword="null"/> to match any.</summary>
    public string? ProcessRecordId { get; init; }

    /// <summary>Matches suppliers whose capability for the named process is at least this well established. <see langword="null"/> for any level.</summary>
    public CapabilityAssurance? MinimumAssurance { get; init; }

    /// <summary>Matches suppliers able to work in this `A1` material. <see langword="null"/> to match any.</summary>
    public string? MaterialRecordId { get; init; }

    /// <summary>Matches suppliers with a site in this country. <see langword="null"/> to match any.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Matches suppliers holding a current certification against this standard on <see cref="AsAt"/>. <see langword="null"/> to match any.</summary>
    public string? CertifiedTo { get; init; }

    /// <summary>The date currency checks are made against. Defaults to no date, which makes <see cref="CertifiedTo"/> match nothing.</summary>
    public DateOnly? AsAt { get; init; }

    /// <summary>Matches suppliers invoicing in this currency. <see langword="null"/> to match any.</summary>
    public BusinessGovernance.CurrencyCode? TradingCurrency { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The organisation's supplier database.</summary>
public interface ISupplierCatalog : IReferenceDataCatalog<SupplierRecord>
{
    /// <summary>Returns the supplier registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<SupplierRecord>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered supplier matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<SupplierRecord>>> SearchAsync(SupplierQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every supplier answering to <paramref name="name"/>, by any alias.
    /// </summary>
    /// <remarks>
    /// Returns a list, never a single record. Two suppliers legitimately
    /// share a trading name, and picking one would be exactly the silent
    /// merge `ADR-0131` forbids.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or whitespace.</exception>
    Task<IReadOnlyList<IReferenceRecord<SupplierRecord>>> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ISupplierCatalog"/> implementation.</summary>
public sealed class SupplierCatalog : ReferenceDataCatalog<SupplierRecord>, ISupplierCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every supplier record's own backing document carries.</summary>
    /// <remarks>
    /// Deliberately not <c>Supplier</c>: that name belongs to the
    /// engineering domain's own <see cref="ISupplier"/> object. This is a
    /// commercial reference record about a supplier, and one value keeps
    /// one meaning.
    /// </remarks>
    public const string SupplierDocumentKind = "CommercialSupplierReference";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string SupplierLibraryName = "CommercialSuppliers";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>supplierId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "CommercialSuppliers.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each supplier reference to the <c>supplierId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "CommercialSuppliers.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="SupplierCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own supplier records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public SupplierCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => SupplierLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => SupplierDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<SupplierRecord>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(SupplierIdentity.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<SupplierRecord>>> SearchAsync(
        SupplierQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<SupplierRecord>>> FindByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();

        return FilterAsync(record => record.Definition.Identity.AnswersTo(trimmed), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(SupplierRecord definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(SupplierRecord definition) => $"Supplier reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<SupplierRecord> record, SupplierQuery query)
    {
        var supplier = record.Definition;

        if (query.NameContains is { } text
            && !supplier.Identity.AllNames.Any(n => n.Contains(text, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.Statuses.Count > 0 && !query.Statuses.Contains(supplier.Status))
            return false;

        if (query.ProcessRecordId is { } process)
        {
            var capability = supplier.FindCapabilityForProcess(process);

            if (capability is null)
                return false;

            if (query.MinimumAssurance is { } minimum
                && CapabilityAssurances.Strength(capability.Assurance) < CapabilityAssurances.Strength(minimum))
                return false;

            if (query.MaterialRecordId is { } material && !capability.CoversMaterial(material))
                return false;
        }
        else if (query.MaterialRecordId is { } anyMaterial
                 && !supplier.Capabilities.Any(c => c.CoversMaterial(anyMaterial)))
        {
            return false;
        }

        if (query.CountryCode is { } country && !supplier.Sites.Any(s => s.Geography.Covers(country)))
            return false;

        if (query.CertifiedTo is { } standard
            && (query.AsAt is not { } asAt || !supplier.HoldsCurrentCertification(standard, asAt)))
            return false;

        if (query.TradingCurrency is { } currency && supplier.TradingCurrency != currency)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}
