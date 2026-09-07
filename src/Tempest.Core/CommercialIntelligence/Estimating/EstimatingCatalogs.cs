using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Estimating;

/// <summary>A deterministic filter over the estimate library.</summary>
public sealed record CostEstimateQuery
{
    /// <summary>Matches any estimate whose reference or subject contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches only estimates stated in this currency. <see langword="null"/> to match any.</summary>
    public CurrencyCode? Currency { get; init; }

    /// <summary>Matches only estimates that cite this record. <see langword="null"/> to match any.</summary>
    public ReferencePin? CitesPin { get; init; }

    /// <summary>Matches only estimates against which an outcome has been recorded. <see langword="null"/> to match any.</summary>
    public bool? HasOutcome { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The organisation's own cost estimates.</summary>
public interface ICostEstimateCatalog : IReferenceDataCatalog<CostEstimate>
{
    /// <summary>Returns the estimate registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<CostEstimate>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered estimate matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<CostEstimate>>> SearchAsync(CostEstimateQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every estimate that cites <paramref name="pin"/>'s record, at any
    /// revision.
    /// </summary>
    /// <remarks>
    /// The impact question, asked backwards: a cost record has been
    /// superseded, so which historical estimates rested on it? Those
    /// estimates do not change — that is the point of pinning — but
    /// somebody may want to know they are now out of step with the
    /// library, and cannot ask unless the link is navigable in this
    /// direction too.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="pin"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<CostEstimate>>> FindCitingAsync(ReferencePin pin, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ICostEstimateCatalog"/> implementation.</summary>
public sealed class CostEstimateCatalog : ReferenceDataCatalog<CostEstimate>, ICostEstimateCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every estimate's own backing document carries.</summary>
    public const string EstimateDocumentKind = "CommercialCostEstimate";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string EstimateLibraryName = "CommercialCostEstimates";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>estimateId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "CommercialCostEstimates.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each estimate reference to the <c>estimateId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "CommercialCostEstimates.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="CostEstimateCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own estimates are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public CostEstimateCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => EstimateLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => EstimateDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<CostEstimate>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(CostEstimate.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<CostEstimate>>> SearchAsync(
        CostEstimateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<CostEstimate>>> FindCitingAsync(
        ReferencePin pin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pin);

        return FilterAsync(
            record => record.Definition.AllPins.Any(
                p => string.Equals(p.Library, pin.Library, StringComparison.Ordinal)
                     && string.Equals(p.RecordId, pin.RecordId, StringComparison.Ordinal)),
            cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(CostEstimate definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(CostEstimate definition) => $"Estimate reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<CostEstimate> record, CostEstimateQuery query)
    {
        var estimate = record.Definition;

        if (query.TextContains is { } text
            && !estimate.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !estimate.Subject.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Currency is { } currency && estimate.Currency != currency)
            return false;

        if (query.CitesPin is { } pin && !estimate.AllPins.Contains(pin))
            return false;

        if (query.HasOutcome is { } hasOutcome && (estimate.Outcome is not null) != hasOutcome)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>A deterministic filter over the supplier-quote library.</summary>
public sealed record SupplierQuoteQuery
{
    /// <summary>Matches any quote whose reference, subject or supplier quotation number contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches only quotes from this supplier. <see langword="null"/> to match any.</summary>
    public string? SupplierRecordId { get; init; }

    /// <summary>Matches any of these firmness levels. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<QuoteFirmness> Firmness { get; init; } = [];

    /// <summary>Matches only quotes still binding on this date. <see langword="null"/> to match any.</summary>
    public DateOnly? BindingOn { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>What suppliers have offered.</summary>
public interface ISupplierQuoteCatalog : IReferenceDataCatalog<SupplierQuote>
{
    /// <summary>Returns the quote registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<SupplierQuote>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered quote matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<SupplierQuote>>> SearchAsync(SupplierQuoteQuery query, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ISupplierQuoteCatalog"/> implementation.</summary>
public sealed class SupplierQuoteCatalog : ReferenceDataCatalog<SupplierQuote>, ISupplierQuoteCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every supplier quote's own backing document carries.</summary>
    public const string SupplierQuoteDocumentKind = "CommercialSupplierQuote";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string SupplierQuoteLibraryName = "CommercialSupplierQuotes";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>quoteId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "CommercialSupplierQuotes.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each quote reference to the <c>quoteId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "CommercialSupplierQuotes.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="SupplierQuoteCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own quotes are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public SupplierQuoteCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => SupplierQuoteLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => SupplierQuoteDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<SupplierQuote>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(SupplierQuote.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<SupplierQuote>>> SearchAsync(
        SupplierQuoteQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(SupplierQuote definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(SupplierQuote definition) => $"Supplier quote reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<SupplierQuote> record, SupplierQuoteQuery query)
    {
        var quote = record.Definition;

        if (query.TextContains is { } text
            && !quote.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !quote.Subject.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !(quote.SupplierQuotationNumber?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            return false;

        if (query.SupplierRecordId is { } supplier
            && !string.Equals(quote.SupplierRecordId, supplier, StringComparison.Ordinal))
            return false;

        if (query.Firmness.Count > 0 && !query.Firmness.Contains(quote.Firmness))
            return false;

        if (query.BindingOn is { } asAt && !quote.IsBindingAt(asAt))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>A deterministic filter over the customer-quotation library.</summary>
public sealed record CustomerQuotationQuery
{
    /// <summary>Matches any quotation whose reference, customer or subject contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these statuses. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<QuotationStatus> Statuses { get; init; } = [];

    /// <summary>Matches only quotations still open on this date. <see langword="null"/> to match any.</summary>
    public DateOnly? OpenOn { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>What the organisation has offered.</summary>
public interface ICustomerQuotationCatalog : IReferenceDataCatalog<CustomerQuotation>
{
    /// <summary>Returns the quotation registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<CustomerQuotation>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered quotation matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<CustomerQuotation>>> SearchAsync(CustomerQuotationQuery query, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ICustomerQuotationCatalog"/> implementation.</summary>
public sealed class CustomerQuotationCatalog : ReferenceDataCatalog<CustomerQuotation>, ICustomerQuotationCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every quotation's own backing document carries.</summary>
    public const string QuotationDocumentKind = "CommercialCustomerQuotation";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string QuotationLibraryName = "CommercialCustomerQuotations";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>quotationId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "CommercialCustomerQuotations.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each quotation reference to the <c>quotationId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "CommercialCustomerQuotations.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="CustomerQuotationCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own quotations are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public CustomerQuotationCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => QuotationLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => QuotationDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<CustomerQuotation>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(CustomerQuotation.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<CustomerQuotation>>> SearchAsync(
        CustomerQuotationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(CustomerQuotation definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(CustomerQuotation definition) => $"Customer quotation reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<CustomerQuotation> record, CustomerQuotationQuery query)
    {
        var quotation = record.Definition;

        if (query.TextContains is { } text
            && !quotation.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !quotation.CustomerName.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !quotation.Subject.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Statuses.Count > 0 && !query.Statuses.Contains(quotation.Status))
            return false;

        if (query.OpenOn is { } asAt && !quotation.IsOpenAt(asAt))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}
