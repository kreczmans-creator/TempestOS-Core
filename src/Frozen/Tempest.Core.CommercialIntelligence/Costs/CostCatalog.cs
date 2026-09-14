using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Manufacturing;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Costs;

/// <summary>A deterministic filter over the process-and-cost library.</summary>
public sealed record ProcessCostQuery
{
    /// <summary>Matches any record whose reference or description contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>The commercial question being asked. <see langword="null"/> to leave every dimension open.</summary>
    /// <remarks>
    /// Where supplied, a record matches only if its own applicability
    /// covers the enquiry — and a record with no quantity basis covers no
    /// quantity, rather than all of them.
    /// </remarks>
    public CommercialEnquiry? Enquiry { get; init; }

    /// <summary>Matches any of these cost bases. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<CostBasis> Bases { get; init; } = [];

    /// <summary>Matches records stated in this currency. <see langword="null"/> to match any.</summary>
    public CurrencyCode? Currency { get; init; }

    /// <summary>Matches records whose figure is at least this well known. <see langword="null"/> for any certainty.</summary>
    public CostCertainty? MinimumCertainty { get; init; }

    /// <summary>Matches records whose lowest possible figure is at least this. <see langword="null"/> for no floor.</summary>
    public Money? CostAtLeast { get; init; }

    /// <summary>Matches records whose highest possible figure is at most this. <see langword="null"/> for no ceiling.</summary>
    public Money? CostAtMost { get; init; }

    /// <summary>Matches records contradicted by another, or only uncontradicted ones. <see langword="null"/> to match any.</summary>
    public bool? IsContradicted { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The commercial process-and-cost library.</summary>
public interface IProcessCostCatalog : IReferenceDataCatalog<ProcessCostRecord>
{
    /// <summary>Returns the cost record registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<ProcessCostRecord>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered cost record matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<ProcessCostRecord>>> SearchAsync(
        ProcessCostQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every released cost record that applies to <paramref name="enquiry"/>,
    /// best-qualified first.
    /// </summary>
    /// <remarks>
    /// Returns a list, never one answer. Several records legitimately
    /// apply — a supplier price and a market figure, or two suppliers —
    /// and choosing between them is a commercial judgement rather than a
    /// lookup. The ordering puts the most specific quantity band first,
    /// so a caller taking the head gets the tightest applicable figure
    /// rather than an arbitrary one.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="enquiry"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<ProcessCostRecord>>> FindApplicableAsync(
        CommercialEnquiry enquiry,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IProcessCostCatalog"/> implementation.</summary>
public sealed class ProcessCostCatalog : ReferenceDataCatalog<ProcessCostRecord>, IProcessCostCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every cost record's own backing document carries.</summary>
    public const string ProcessCostDocumentKind = "CommercialProcessCost";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>costId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "CommercialProcessCosts.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each cost reference to the <c>costId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "CommercialProcessCosts.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="ProcessCostCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own cost records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public ProcessCostCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "CommercialProcessCosts";

    /// <inheritdoc />
    public override string DocumentKind => ProcessCostDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<ProcessCostRecord>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(ProcessCostRecord.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<ProcessCostRecord>>> SearchAsync(
        ProcessCostQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<ProcessCostRecord>>> FindApplicableAsync(
        CommercialEnquiry enquiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(enquiry);

        var applicable = await FilterAsync(
            record => record.ValidationState == ReferenceValidationState.Released
                      && record.Definition.AppliesTo(enquiry),
            cancellationToken).ConfigureAwait(false);

        return applicable
            .OrderBy(r => r.Definition.Applicability.Quantities?.Width ?? int.MaxValue)
            .ThenByDescending(r => r.Definition.Applicability.IsSupplierSpecific)
            .ThenByDescending(r => (int)r.Definition.Cost.Certainty)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(ProcessCostRecord definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(ProcessCostRecord definition) => $"Cost reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<ProcessCostRecord> record, ProcessCostQuery query)
    {
        var cost = record.Definition;

        if (query.TextContains is { } text
            && !cost.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !cost.Description.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Enquiry is { } enquiry && !cost.AppliesTo(enquiry))
            return false;

        if (query.Bases.Count > 0 && !query.Bases.Contains(cost.Basis))
            return false;

        if (query.Currency is { } currency && cost.Currency != currency)
            return false;

        if (query.MinimumCertainty is { } certainty && cost.Cost.Certainty < certainty)
            return false;

        // A cost floor or ceiling excludes an unknown figure rather than
        // admitting it: an unpriced record satisfies no numeric bound.
        if (query.CostAtLeast is { } floor
            && (cost.Cost.Lowest is not { } lowest || lowest.Currency != floor.Currency || lowest < floor))
            return false;

        if (query.CostAtMost is { } ceiling
            && (cost.Cost.Highest is not { } highest || highest.Currency != ceiling.Currency || highest > ceiling))
            return false;

        if (query.IsContradicted is { } contradicted && cost.IsContradicted != contradicted)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes D2's validation service reports.</summary>
public static class CostValidationRules
{
    /// <summary>The record does not say what its figure is charged against.</summary>
    public const string CostBasisMustBeStated = "TEMPEST-CIP-001";

    /// <summary>The figure is unknown, so the record states no cost at all.</summary>
    public const string CostIsUnknown = "TEMPEST-CIP-002";

    /// <summary>The figure is zero, which is rarely what anybody means.</summary>
    public const string CostIsZero = "TEMPEST-CIP-003";

    /// <summary>Setup, tooling or the minimum charge is in a different currency from the cost.</summary>
    public const string CurrencyMustBeConsistent = "TEMPEST-CIP-004";

    /// <summary>The record names no `A7` process, so nothing ties the figure to a piece of work.</summary>
    public const string ProcessNotIdentified = "TEMPEST-CIP-005";

    /// <summary>The record names an `A7` process the manufacturing library does not hold.</summary>
    public const string ProcessMustResolve = "TEMPEST-CIP-006";

    /// <summary>The record names a supplier the supplier database does not hold.</summary>
    public const string SupplierMustResolve = "TEMPEST-CIP-007";

    /// <summary>A minimum charge is below the figure it qualifies, so it can never apply.</summary>
    public const string MinimumChargeIsIneffective = "TEMPEST-CIP-008";

    /// <summary>The record's components do not add up to its total.</summary>
    public const string ComponentsDoNotSum = "TEMPEST-CIP-009";

    /// <summary>Another cost record covers the same process, supplier and quantities.</summary>
    public const string OverlappingCostRecords = "TEMPEST-CIP-010";

    /// <summary>The record is contradicted by another and the disagreement is unresolved.</summary>
    public const string CostIsContradicted = "TEMPEST-CIP-011";

    /// <summary>The record names a contradicting record the library does not hold.</summary>
    public const string ContradictionMustResolve = "TEMPEST-CIP-012";

    /// <summary>Two cost records share one reference.</summary>
    public const string DuplicateCostReference = "TEMPEST-CIP-013";
}
