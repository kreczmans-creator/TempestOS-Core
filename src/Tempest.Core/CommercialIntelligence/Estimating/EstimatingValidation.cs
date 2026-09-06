using Tempest.Core.BusinessGovernance;
using Tempest.Core.CommercialIntelligence.Suppliers;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Estimating;

/// <summary>The diagnostic codes D4's validation services report.</summary>
public static class EstimatingValidationRules
{
    /// <summary>The estimate has no lines, so it estimates nothing.</summary>
    public const string EstimateHasNoLines = "TEMPEST-CIQ-001";

    /// <summary>A line is stated in a currency other than the estimate's own.</summary>
    public const string LineCurrencyMismatch = "TEMPEST-CIQ-002";

    /// <summary>A line has no price, so the estimate has no total.</summary>
    public const string LineIsUnpriced = "TEMPEST-CIQ-003";

    /// <summary>A line cannot be traced to a governed record at a known revision.</summary>
    public const string LineIsUntraceable = "TEMPEST-CIQ-004";

    /// <summary>No line can be traced to a governed record; the estimate is unsupported throughout.</summary>
    public const string EstimateIsWhollyUntraceable = "TEMPEST-CIQ-005";

    /// <summary>A line cites an assumption the estimate does not carry.</summary>
    public const string AssumptionReferenceUnresolved = "TEMPEST-CIQ-006";

    /// <summary>Two lines, assumptions or quote lines share one reference.</summary>
    public const string DuplicateLineReference = "TEMPEST-CIQ-007";

    /// <summary>Nobody is named as having prepared the estimate.</summary>
    public const string EstimateIsUnattributed = "TEMPEST-CIQ-008";

    /// <summary>The estimate does not say when it was prepared, so nobody can tell whether it is current.</summary>
    public const string PreparationDateMissing = "TEMPEST-CIQ-009";

    /// <summary>The estimate has run past its own stated validity.</summary>
    public const string EstimateHasExpired = "TEMPEST-CIQ-010";

    /// <summary>A source record the estimate pins has since been superseded.</summary>
    /// <remarks>
    /// A warning and never an error. The estimate is still an accurate
    /// record of what was estimated on the day; the finding says the
    /// library has moved on beneath it, which is a reason to redo the
    /// estimate rather than a defect in this one.
    /// </remarks>
    public const string PinnedSourceSuperseded = "TEMPEST-CIQ-011";

    /// <summary>A source record the estimate pins is no longer in the library at all.</summary>
    public const string PinnedSourceMissing = "TEMPEST-CIQ-012";

    /// <summary>The estimate carries a contingency line but states no assumptions.</summary>
    public const string ContingencyWithoutAssumptions = "TEMPEST-CIQ-013";

    /// <summary>An outcome is recorded against the estimate but nothing evidences it.</summary>
    public const string OutcomeIsUnevidenced = "TEMPEST-CIQ-014";

    /// <summary>The recorded outcome is in a currency the estimate is not stated in.</summary>
    public const string OutcomeCurrencyMismatch = "TEMPEST-CIQ-015";

    /// <summary>The quote does not say how firm it is, so what the supplier is bound by is unknown.</summary>
    public const string QuoteFirmnessNotStated = "TEMPEST-CIQ-016";

    /// <summary>The quote names no supplier the supplier database holds.</summary>
    public const string QuoteSupplierMustResolve = "TEMPEST-CIQ-017";

    /// <summary>The quote has no lines, so it offers nothing.</summary>
    public const string QuoteHasNoLines = "TEMPEST-CIQ-018";

    /// <summary>A firm quote states no period over which the supplier will hold the price.</summary>
    public const string FirmQuoteNeedsValidity = "TEMPEST-CIQ-019";

    /// <summary>The quotation document itself is not on file.</summary>
    public const string QuoteIsUnevidenced = "TEMPEST-CIQ-020";

    /// <summary>The quote carries no date, so its age cannot be judged.</summary>
    public const string QuoteDateMissing = "TEMPEST-CIQ-021";

    /// <summary>The quote has run past its own stated validity.</summary>
    public const string QuoteHasExpired = "TEMPEST-CIQ-022";

    /// <summary>The quote states neither its conditions nor its exclusions.</summary>
    public const string QuoteTermsNotStated = "TEMPEST-CIQ-023";

    /// <summary>The quotation says it has been issued but names nobody who issued it.</summary>
    public const string IssuedQuotationNeedsAuthority = "TEMPEST-CIQ-024";

    /// <summary>The quotation says it has been issued but carries no issue date.</summary>
    public const string IssuedQuotationNeedsDate = "TEMPEST-CIQ-025";

    /// <summary>The quotation has no lines, so it offers nothing.</summary>
    public const string QuotationHasNoLines = "TEMPEST-CIQ-026";

    /// <summary>The quotation cites no estimate, so what it rests on is unrecorded.</summary>
    public const string QuotationCitesNoEstimate = "TEMPEST-CIQ-027";

    /// <summary>The quotation states no period over which the offer stands.</summary>
    public const string QuotationNeedsValidity = "TEMPEST-CIQ-028";

    /// <summary>A supplier quote the quotation depends on expires before the offer does.</summary>
    /// <remarks>
    /// The margin exposure nobody notices: the organisation holds a price
    /// open for ninety days on the strength of a supplier quote that
    /// lapses in thirty.
    /// </remarks>
    public const string SupportingQuoteExpiresFirst = "TEMPEST-CIQ-029";

    /// <summary>The offered total is at or below what the work is estimated to cost.</summary>
    public const string QuotationIsAtOrBelowCost = "TEMPEST-CIQ-030";
}

/// <summary>Governance of the estimate library itself.</summary>
public interface ICostEstimateValidationService : IReferenceValidationService<CostEstimate>
{
}

/// <summary>The concrete <see cref="ICostEstimateValidationService"/> implementation.</summary>
/// <remarks>
/// The findings are about what an estimate can be relied on for. An
/// untraceable estimate may be perfectly accurate and still cannot be
/// defended; an estimate whose pinned sources have been superseded is a
/// faithful record of a stale view. Neither is a wrong number, and the
/// service never changes one.
/// </remarks>
public sealed class CostEstimateValidationService : ReferenceValidationService<CostEstimate>, ICostEstimateValidationService
{
    private readonly IReferenceDataCatalog<CostEstimate> _estimates;
    private readonly IReadOnlyDictionary<string, IReferencePinResolver> _pinResolvers;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="CostEstimateValidationService"/> class.</summary>
    /// <param name="catalog">The estimate library whose records this service validates.</param>
    /// <param name="pinResolvers">Resolvers for the libraries an estimate may pin, keyed by library name. Optional; pins into libraries with no resolver are not checked.</param>
    /// <param name="timeProvider">The clock staleness checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public CostEstimateValidationService(
        ICostEstimateCatalog catalog,
        IEnumerable<IReferencePinResolver>? pinResolvers = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _estimates = catalog;
        _pinResolvers = (pinResolvers ?? []).ToDictionary(r => r.LibraryName, StringComparer.Ordinal);
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        CostEstimate definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Estimate '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (definition.Lines.Count == 0)
            errors.Add(Diagnostic(
                EstimatingValidationRules.EstimateHasNoLines,
                $"{subject} carries no lines, so it estimates nothing."));

        EvaluateDuplicateReferences(
            definition.Lines.Select(l => l.Reference),
            $"{subject} has two lines sharing the reference",
            errors);

        EvaluateDuplicateReferences(
            definition.Assumptions.Select(a => a.Reference),
            $"{subject} has two assumptions sharing the reference",
            errors);

        var assumptionReferences = definition.Assumptions
            .Select(a => a.Reference)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var line in definition.Lines)
        {
            if (line.UnitCost.Currency is { } lineCurrency && lineCurrency != definition.Currency)
                errors.Add(Diagnostic(
                    EstimatingValidationRules.LineCurrencyMismatch,
                    $"{subject} line '{line.Reference}' is stated in {lineCurrency} but the estimate is in "
                    + $"{definition.Currency}. TempestOS does not convert currencies."));

            if (line.IsUnpriced)
                warnings.Add(Diagnostic(
                    EstimatingValidationRules.LineIsUnpriced,
                    $"{subject} line '{line.Reference}' is unpriced, so the estimate has no total."));

            if (!line.IsTraceable)
                warnings.Add(Diagnostic(
                    EstimatingValidationRules.LineIsUntraceable,
                    $"{subject} line '{line.Reference}' cites no source record, so its figure is somebody's judgement "
                    + "rather than a derivation."));

            foreach (var reference in line.AssumptionReferences.Where(r => !assumptionReferences.Contains(r)))
                errors.Add(Diagnostic(
                    EstimatingValidationRules.AssumptionReferenceUnresolved,
                    $"{subject} line '{line.Reference}' rests on assumption '{reference}', which the estimate does not state."));
        }

        if (definition.Lines.Count > 0 && definition.UntraceableLines.Count == definition.Lines.Count)
            warnings.Add(Diagnostic(
                EstimatingValidationRules.EstimateIsWhollyUntraceable,
                $"{subject} cites no source record on any line. Nothing in it can be reproduced."));

        if (string.IsNullOrWhiteSpace(definition.PreparedByPrincipalId))
            warnings.Add(Diagnostic(
                EstimatingValidationRules.EstimateIsUnattributed,
                $"{subject} names nobody who prepared it."));

        if (definition.PreparedOn is null)
            warnings.Add(Diagnostic(
                EstimatingValidationRules.PreparationDateMissing,
                $"{subject} carries no preparation date, so nobody can tell whether it is current."));

        if (definition.IsStaleAt(today))
            warnings.Add(Diagnostic(
                EstimatingValidationRules.EstimateHasExpired,
                $"{subject} ran past its own validity on {definition.Validity!.To:O}."));

        if (definition.Lines.Any(l => l.Kind == EstimateLineKind.Contingency) && definition.Assumptions.Count == 0)
            warnings.Add(Diagnostic(
                EstimatingValidationRules.ContingencyWithoutAssumptions,
                $"{subject} carries a contingency but states no assumptions. What the contingency covers is unrecorded."));

        if (definition.Outcome is { } outcome)
        {
            if (!outcome.IsEvidenced)
                warnings.Add(Diagnostic(
                    EstimatingValidationRules.OutcomeIsUnevidenced,
                    $"{subject} records an outcome of {outcome.ActualCost} with nothing evidencing it."));

            if (outcome.ActualCost.Currency != definition.Currency)
                errors.Add(Diagnostic(
                    EstimatingValidationRules.OutcomeCurrencyMismatch,
                    $"{subject} is stated in {definition.Currency} but its outcome is in {outcome.ActualCost.Currency}, "
                    + "so the two cannot be compared."));
        }

        await EvaluatePinsAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reports a reference appearing more than once in a collection that must key on it.</summary>
    internal static void EvaluateDuplicateReferences(
        IEnumerable<string> references,
        string message,
        List<IValidationDiagnostic> errors)
    {
        var duplicates = references
            .GroupBy(r => r, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(r => r, StringComparer.Ordinal);

        foreach (var duplicate in duplicates)
            errors.Add(Diagnostic(EstimatingValidationRules.DuplicateLineReference, $"{message} '{duplicate}'."));
    }

    private async Task EvaluatePinsAsync(
        CostEstimate definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        foreach (var pin in definition.AllPins)
        {
            if (!_pinResolvers.TryGetValue(pin.Library, out var resolver))
                continue;

            var state = await resolver.ResolveAsync(pin, cancellationToken).ConfigureAwait(false);

            switch (state)
            {
                case null:
                    warnings.Add(Diagnostic(
                        EstimatingValidationRules.PinnedSourceMissing,
                        $"{subject} rests on {pin.Library} record '{pin.RecordId}', which that library no longer holds."));
                    break;

                case ReferenceValidationState.Superseded:
                    warnings.Add(Diagnostic(
                        EstimatingValidationRules.PinnedSourceSuperseded,
                        $"{subject} rests on {pin.Library} record '{pin.RecordId}' revision {pin.RevisionNumber}, which has since "
                        + "been superseded. The estimate itself is unchanged and remains an accurate record of what was estimated."));
                    break;
            }
        }
    }
}

/// <summary>
/// Resolves a <see cref="ReferencePin"/> into one library, so an
/// estimate's sources can be checked without D4 taking a dependency on
/// every library an estimate might cite.
/// </summary>
public interface IReferencePinResolver
{
    /// <summary>The library this resolver answers for, matching <see cref="ReferencePin.Library"/>.</summary>
    string LibraryName { get; }

    /// <summary>The pinned record's current validation state, or <see langword="null"/> where the library no longer holds it.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="pin"/> is <see langword="null"/>.</exception>
    Task<ReferenceValidationState?> ResolveAsync(ReferencePin pin, CancellationToken cancellationToken = default);
}

/// <summary>An <see cref="IReferencePinResolver"/> over any reference-data catalogue.</summary>
/// <typeparam name="TDefinition">The library's own definition type.</typeparam>
public sealed class CatalogPinResolver<TDefinition> : IReferencePinResolver
    where TDefinition : class
{
    private readonly IReferenceDataCatalog<TDefinition> _catalog;

    /// <summary>Initialises a new instance of the <see cref="CatalogPinResolver{TDefinition}"/> class.</summary>
    /// <param name="catalog">The catalogue this resolver answers for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <see langword="null"/>.</exception>
    public CatalogPinResolver(IReferenceDataCatalog<TDefinition> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
    }

    /// <inheritdoc />
    public string LibraryName => _catalog.LibraryName;

    /// <inheritdoc />
    public async Task<ReferenceValidationState?> ResolveAsync(ReferencePin pin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pin);

        var record = await _catalog.FindAsync(pin.RecordId, cancellationToken).ConfigureAwait(false);

        return record?.ValidationState;
    }
}

/// <summary>Governance of the supplier-quote library itself.</summary>
public interface ISupplierQuoteValidationService : IReferenceValidationService<SupplierQuote>
{
}

/// <summary>The concrete <see cref="ISupplierQuoteValidationService"/> implementation.</summary>
public sealed class SupplierQuoteValidationService : ReferenceValidationService<SupplierQuote>, ISupplierQuoteValidationService
{
    private readonly ISupplierCatalog? _suppliers;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="SupplierQuoteValidationService"/> class.</summary>
    /// <param name="catalog">The quote library whose records this service validates.</param>
    /// <param name="suppliers">The supplier database, for confirming that the quoting supplier exists. Optional.</param>
    /// <param name="timeProvider">The clock expiry checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public SupplierQuoteValidationService(
        ISupplierQuoteCatalog catalog,
        ISupplierCatalog? suppliers = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _suppliers = suppliers;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        SupplierQuote definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Supplier quote '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (definition.Lines.Count == 0)
            errors.Add(Diagnostic(
                EstimatingValidationRules.QuoteHasNoLines,
                $"{subject} carries no lines, so it offers nothing."));

        CostEstimateValidationService.EvaluateDuplicateReferences(
            definition.Lines.Select(l => l.Reference),
            $"{subject} has two lines sharing the reference",
            errors);

        foreach (var line in definition.Lines.Where(l => l.UnitPrice.Currency != definition.Currency))
            errors.Add(Diagnostic(
                EstimatingValidationRules.LineCurrencyMismatch,
                $"{subject} line '{line.Reference}' is stated in {line.UnitPrice.Currency} but the quote is in "
                + $"{definition.Currency}. TempestOS does not convert currencies."));

        if (definition.Firmness == QuoteFirmness.Unspecified)
            warnings.Add(Diagnostic(
                EstimatingValidationRules.QuoteFirmnessNotStated,
                $"{subject} does not say how firm it is, so what the supplier is bound by is unknown."));

        if (definition.Firmness is QuoteFirmness.Firm or QuoteFirmness.FirmAgainstSpecification && definition.Validity is null)
            warnings.Add(Diagnostic(
                EstimatingValidationRules.FirmQuoteNeedsValidity,
                $"{subject} is recorded as firm but states no period over which the supplier will hold the price."));

        if (!definition.IsEvidenced)
            warnings.Add(Diagnostic(
                EstimatingValidationRules.QuoteIsUnevidenced,
                $"{subject} has no quotation document on file, so it is a recollection of a price rather than a record of one."));

        if (definition.QuotedOn is null)
            warnings.Add(Diagnostic(
                EstimatingValidationRules.QuoteDateMissing,
                $"{subject} carries no date, so its age cannot be judged."));

        if (definition.IsExpiredAt(today))
            warnings.Add(Diagnostic(
                EstimatingValidationRules.QuoteHasExpired,
                $"{subject} ran past its own validity on {definition.Validity!.To:O}."));

        if (definition.Conditions.Count == 0 && definition.Exclusions.Count == 0)
            warnings.Add(Diagnostic(
                EstimatingValidationRules.QuoteTermsNotStated,
                $"{subject} states neither conditions nor exclusions. Suppliers rarely attach none."));

        if (_suppliers is not null)
        {
            var supplier = await _suppliers.FindAsync(definition.SupplierRecordId, cancellationToken).ConfigureAwait(false);

            if (supplier is null)
                warnings.Add(Diagnostic(
                    EstimatingValidationRules.QuoteSupplierMustResolve,
                    $"{subject} names supplier '{definition.SupplierRecordId}', which the supplier database does not hold."));
        }
    }
}

/// <summary>Governance of the customer-quotation library itself.</summary>
public interface ICustomerQuotationValidationService : IReferenceValidationService<CustomerQuotation>
{
}

/// <summary>The concrete <see cref="ICustomerQuotationValidationService"/> implementation.</summary>
/// <remarks>
/// The heaviest findings here are about authority. A quotation that says
/// it was issued but names nobody who issued it is a record of the
/// organisation having made an offer that nobody can be shown to have
/// made — exactly the gap `ADR-0135` exists to keep visible.
/// </remarks>
public sealed class CustomerQuotationValidationService : ReferenceValidationService<CustomerQuotation>, ICustomerQuotationValidationService
{
    private readonly ICostEstimateCatalog? _estimates;
    private readonly ISupplierQuoteCatalog? _supplierQuotes;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="CustomerQuotationValidationService"/> class.</summary>
    /// <param name="catalog">The quotation library whose records this service validates.</param>
    /// <param name="estimates">The estimate library, for reading what the offer rests on. Optional.</param>
    /// <param name="supplierQuotes">The supplier-quote library, for checking that supporting prices outlast the offer. Optional.</param>
    /// <param name="timeProvider">The clock expiry checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public CustomerQuotationValidationService(
        ICustomerQuotationCatalog catalog,
        ICostEstimateCatalog? estimates = null,
        ISupplierQuoteCatalog? supplierQuotes = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _estimates = estimates;
        _supplierQuotes = supplierQuotes;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        CustomerQuotation definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Customer quotation '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (definition.Lines.Count == 0)
            errors.Add(Diagnostic(
                EstimatingValidationRules.QuotationHasNoLines,
                $"{subject} carries no lines, so it offers nothing."));

        CostEstimateValidationService.EvaluateDuplicateReferences(
            definition.Lines.Select(l => l.Reference),
            $"{subject} has two lines sharing the reference",
            errors);

        foreach (var line in definition.Lines.Where(l => l.UnitPrice.Currency != definition.Currency))
            errors.Add(Diagnostic(
                EstimatingValidationRules.LineCurrencyMismatch,
                $"{subject} line '{line.Reference}' is stated in {line.UnitPrice.Currency} but the quotation is in "
                + $"{definition.Currency}. TempestOS does not convert currencies."));

        if (definition.HasBeenIssued && definition.IssuedUnderAuthority is null)
            errors.Add(Diagnostic(
                EstimatingValidationRules.IssuedQuotationNeedsAuthority,
                $"{subject} is recorded as having been issued but names nobody who issued it. Making an offer binds the "
                + "organisation, and TempestOS never records itself as having done so."));

        if (definition.HasBeenIssued && definition.IssuedOn is null)
            warnings.Add(Diagnostic(
                EstimatingValidationRules.IssuedQuotationNeedsDate,
                $"{subject} is recorded as having been issued but carries no issue date."));

        if (definition.EstimatePin is null)
            warnings.Add(Diagnostic(
                EstimatingValidationRules.QuotationCitesNoEstimate,
                $"{subject} cites no estimate, so what the price rests on is unrecorded."));

        if (definition.Validity is null)
            warnings.Add(Diagnostic(
                EstimatingValidationRules.QuotationNeedsValidity,
                $"{subject} states no period over which the offer stands, so it reads as open indefinitely."));

        await EvaluateSupportingQuotesAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluateMarginAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);

        _ = today;
    }

    private async Task EvaluateSupportingQuotesAsync(
        CustomerQuotation definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_supplierQuotes is null || definition.Validity is not { To: { } offerEnds })
            return;

        foreach (var pin in definition.SupportingQuotePins.Where(
                     p => string.Equals(p.Library, SupplierQuoteCatalog.SupplierQuoteLibraryName, StringComparison.Ordinal)))
        {
            var record = await _supplierQuotes.FindAsync(pin.RecordId, cancellationToken).ConfigureAwait(false);

            if (record?.Definition.Validity is not { To: { } quoteEnds } || quoteEnds >= offerEnds)
                continue;

            warnings.Add(Diagnostic(
                EstimatingValidationRules.SupportingQuoteExpiresFirst,
                $"{subject} stands until {offerEnds:O} but rests on supplier quote '{record.Definition.Reference}', which "
                + $"lapses on {quoteEnds:O}. The margin beyond that date is unsupported."));
        }
    }

    private async Task EvaluateMarginAsync(
        CustomerQuotation definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_estimates is null || definition.EstimatePin is not { } pin)
            return;

        var estimate = await _estimates.FindAsync(pin.RecordId, cancellationToken).ConfigureAwait(false);

        if (estimate is null)
            return;

        var margin = definition.MarginOver(estimate.Definition.Total);

        if (margin is <= 0m)
            warnings.Add(Diagnostic(
                EstimatingValidationRules.QuotationIsAtOrBelowCost,
                $"{subject} offers {definition.Total} against an estimated cost of {estimate.Definition.Total}. "
                + "Quoting at or below cost may be deliberate, and this finding does not say it is wrong — only that "
                + "somebody should have decided it."));
    }
}
