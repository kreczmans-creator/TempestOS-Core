using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Quotations;

/// <summary>A deterministic filter over the quotation library.</summary>
public sealed record QuotationQuery
{
    /// <summary>Matches any quotation whose reference contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? ReferenceContains { get; init; }

    /// <summary>Matches any quotation whose client's legal name contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? ClientNameContains { get; init; }

    /// <summary>Matches any of these statuses. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<QuotationStatus> Statuses { get; init; } = [];

    /// <summary>Matches open quotations whose follow-up date is on or before this date. <see langword="null"/> to match any.</summary>
    public DateOnly? FollowUpDueBy { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The library of quotations the organisation has sent, or is preparing to send, to clients.</summary>
public interface IQuotationCatalog : IReferenceDataCatalog<Quotation>
{
    /// <summary>Returns the quotation registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<Quotation>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered quotation matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<Quotation>>> SearchAsync(
        QuotationQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IQuotationCatalog"/> implementation.</summary>
/// <remarks>
/// A quotation is a governed record for the same reasons a contract is —
/// it is authored, evidenced, approved, revisioned and superseded — so it
/// uses the shared lifecycle rather than a store of its own (`ADR-0129`).
/// Its commercial position lives in <see cref="Quotation.Status"/>, a
/// separate axis from the record's own validation state.
/// </remarks>
public sealed class QuotationCatalog : ReferenceDataCatalog<Quotation>, IQuotationCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every quotation record's own backing document carries.</summary>
    public const string QuotationDocumentKind = "BusinessQuotation";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>quotationId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessQuotations.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each quotation reference to the <c>quotationId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessQuotations.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="QuotationCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own quotation records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public QuotationCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "BusinessQuotations";

    /// <inheritdoc />
    public override string DocumentKind => QuotationDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<Quotation>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(Quotation.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<Quotation>>> SearchAsync(
        QuotationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(Quotation definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(Quotation definition) => $"Quotation reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<Quotation> record, QuotationQuery query)
    {
        var quotation = record.Definition;

        if (query.ReferenceContains is not null
            && !quotation.Reference.Contains(query.ReferenceContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.ClientNameContains is { } client
            && !quotation.Client.LegalName.Contains(client, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Statuses.Count > 0 && !query.Statuses.Contains(quotation.Status))
            return false;

        if (query.FollowUpDueBy is { } dueBy && !quotation.IsFollowUpOverdueAt(dueBy))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}
