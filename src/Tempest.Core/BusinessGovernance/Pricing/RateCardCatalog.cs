using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Pricing;

/// <summary>A deterministic filter over the rate-card library.</summary>
public sealed record RateCardQuery
{
    /// <summary>Matches any card whose code or name contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches cards applying on this date. <see langword="null"/> to match any.</summary>
    public DateOnly? ApplyingOn { get; init; }

    /// <summary>Matches cards stated in this currency. <see langword="null"/> to match any.</summary>
    public CurrencyCode? Currency { get; init; }

    /// <summary>Matches cards for this client segment, and the general card. <see langword="null"/> to match any.</summary>
    public string? AppliesTo { get; init; }

    /// <summary>Matches approved cards, unapproved cards, or either. <see langword="null"/> to match any.</summary>
    public bool? IsApproved { get; init; }

    /// <summary>Matches cards pricing this service. <see langword="null"/> to match any.</summary>
    public string? ServiceCode { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The library of published rate cards.</summary>
public interface IRateCardCatalog : IReferenceDataCatalog<RateCard>
{
    /// <summary>Returns the card registered under <paramref name="code"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<RateCard>?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Every registered card matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<RateCard>>> SearchAsync(RateCardQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every released, approved card that applies on <paramref name="date"/>
    /// — the cards a quotation could legitimately be priced from.
    /// </summary>
    /// <remarks>
    /// Returns a list rather than a single card on purpose. Two cards
    /// applying on the same day for the same segment is a governance
    /// failure the caller must see, not one this method should resolve by
    /// silently picking the newer.
    /// </remarks>
    Task<IReadOnlyList<IReferenceRecord<RateCard>>> FindApplicableAsync(
        DateOnly date,
        string? appliesTo = null,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IRateCardCatalog"/> implementation.</summary>
public sealed class RateCardCatalog : ReferenceDataCatalog<RateCard>, IRateCardCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every rate-card record's own backing document carries.</summary>
    public const string RateCardDocumentKind = "BusinessRateCard";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>rateCardId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessRateCards.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each card code to the <c>rateCardId</c> holding it.</summary>
    public const string CodeIndexCollection = "BusinessRateCards.CodeIndex";

    /// <summary>Initialises a new instance of the <see cref="RateCardCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own rate-card records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public RateCardCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "BusinessRateCards";

    /// <inheritdoc />
    public override string DocumentKind => RateCardDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => CodeIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<RateCard>?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(RateCard.CodeKeyFor(code), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<RateCard>>> SearchAsync(RateCardQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<RateCard>>> FindApplicableAsync(
        DateOnly date,
        string? appliesTo = null,
        CancellationToken cancellationToken = default) =>
        FilterAsync(
            record => record.ValidationState == ReferenceValidationState.Released
                      && record.Definition.IsApproved
                      && record.Definition.AppliesOn(date)
                      && SegmentMatches(record.Definition, appliesTo),
            cancellationToken);

    /// <inheritdoc />
    protected override string? GetSecondaryKey(RateCard definition) => definition.CodeKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(RateCard definition) => $"Rate card code '{definition.Code}'";

    private static bool SegmentMatches(RateCard card, string? appliesTo) =>
        appliesTo is null
        || card.AppliesTo is null
        || string.Equals(card.AppliesTo, appliesTo, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(IReferenceRecord<RateCard> record, RateCardQuery query)
    {
        var card = record.Definition;

        if (query.TextContains is { } text
            && !card.Code.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !card.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.ApplyingOn is { } date && !card.AppliesOn(date))
            return false;

        if (query.Currency is { } currency && card.Currency != currency)
            return false;

        if (query.AppliesTo is { } segment && !SegmentMatches(card, segment))
            return false;

        if (query.IsApproved is { } approved && card.IsApproved != approved)
            return false;

        if (query.ServiceCode is { } service && card.FindEntry(service) is null)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes C4's validation service reports.</summary>
public static class PricingValidationRules
{
    /// <summary>The card prices nothing.</summary>
    public const string CardMustHaveEntries = "TEMPEST-BGP-001";

    /// <summary>Two entries in one card share a service code.</summary>
    public const string DuplicateServiceCode = "TEMPEST-BGP-002";

    /// <summary>An entry does not say what its rate is charged against.</summary>
    public const string PricingBasisMustBeStated = "TEMPEST-BGP-003";

    /// <summary>A rate is stated in a currency other than the card's own.</summary>
    public const string CurrencyMustMatchCard = "TEMPEST-BGP-004";

    /// <summary>A rate is zero.</summary>
    public const string RateIsZero = "TEMPEST-BGP-005";

    /// <summary>A minimum charge is less than the rate it qualifies, so it can never apply.</summary>
    public const string MinimumChargeIsIneffective = "TEMPEST-BGP-006";

    /// <summary>Nobody has approved the card as the organisation's commercial position.</summary>
    public const string CardIsNotApproved = "TEMPEST-BGP-007";

    /// <summary>The card's effective period has ended.</summary>
    public const string CardHasExpired = "TEMPEST-BGP-008";

    /// <summary>The card has no end date, so nothing will prompt a price review.</summary>
    public const string CardIsOpenEnded = "TEMPEST-BGP-009";

    /// <summary>Another card for the same segment applies over the same days.</summary>
    public const string OverlappingCardPeriods = "TEMPEST-BGP-010";

    /// <summary>The card does not say whether its rates include tax.</summary>
    public const string TaxTreatmentNotStated = "TEMPEST-BGP-011";

    /// <summary>Two cards share one code.</summary>
    public const string DuplicateCardCode = "TEMPEST-BGP-012";
}

/// <summary>Governance of rate cards themselves.</summary>
public interface IRateCardValidationService : IReferenceValidationService<RateCard>
{
}

/// <summary>The concrete <see cref="IRateCardValidationService"/> implementation.</summary>
/// <remarks>
/// Nothing here has a view on whether a price is right — that is a
/// commercial judgement resting on a cost base this platform does not
/// hold. What it checks is that the card is usable: that it prices
/// something, in one currency, over a period that does not collide with
/// another card's, and that somebody approved it before it was shown to a
/// client.
/// </remarks>
public sealed class RateCardValidationService : ReferenceValidationService<RateCard>, IRateCardValidationService
{
    private readonly IRateCardCatalog _cards;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="RateCardValidationService"/> class.</summary>
    /// <param name="catalog">The rate-card library whose records this service validates.</param>
    /// <param name="timeProvider">The clock expiry checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public RateCardValidationService(IRateCardCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _cards = catalog;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        RateCard definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Rate card '{definition.Code}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        BusinessGovernanceValidator.Evaluate(subject, definition.Governance, today, errors, warnings, expectEvidence: false);
        BusinessGovernanceValidator.EvaluatePeriod(subject, definition.EffectivePeriod, today, warnings, expectAnEnd: true);

        if (definition.Entries.Count == 0)
            errors.Add(Diagnostic(
                PricingValidationRules.CardMustHaveEntries,
                $"{subject} prices nothing, so nothing can be quoted from it."));

        foreach (var duplicate in definition.Entries
                     .GroupBy(e => e.ServiceCodeKey, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.First().ServiceCode))
            errors.Add(Diagnostic(
                PricingValidationRules.DuplicateServiceCode,
                $"{subject} prices service '{duplicate}' more than once, so which rate applies is ambiguous."));

        foreach (var entry in definition.Entries)
        {
            if (entry.Basis == PricingBasis.Unspecified)
                errors.Add(Diagnostic(
                    PricingValidationRules.PricingBasisMustBeStated,
                    $"Service '{entry.ServiceCode}' in {subject} does not say what its rate is charged against. "
                    + $"{entry.Rate} per what?"));

            if (entry.Rate.Currency != definition.Currency)
                errors.Add(Diagnostic(
                    PricingValidationRules.CurrencyMustMatchCard,
                    $"Service '{entry.ServiceCode}' in {subject} is priced in {entry.Rate.Currency} on a card stated in "
                    + $"{definition.Currency}. A card cannot be totalled across currencies; price it on a second card."));

            if (entry.Rate.IsZero)
                warnings.Add(Diagnostic(
                    PricingValidationRules.RateIsZero,
                    $"Service '{entry.ServiceCode}' in {subject} is priced at zero. That may be deliberate; it is reported "
                    + "because a missing rate looks identical to a free one."));

            if (entry.MinimumCharge is { } minimum
                && minimum.Currency == entry.Rate.Currency
                && minimum <= entry.Rate)
                warnings.Add(Diagnostic(
                    PricingValidationRules.MinimumChargeIsIneffective,
                    $"Service '{entry.ServiceCode}' in {subject} has a minimum charge of {minimum} against a rate of "
                    + $"{entry.Rate}, so the minimum can never bite."));
        }

        if (!definition.IsApproved)
            warnings.Add(Diagnostic(
                PricingValidationRules.CardIsNotApproved,
                $"{subject} has not been approved by a named person. A published price binds the organisation to whoever it is "
                + "shown to; the record being accurate is not the same as the prices being the ones the organisation intends."));

        if (string.IsNullOrWhiteSpace(definition.TaxTreatment))
            warnings.Add(Diagnostic(
                PricingValidationRules.TaxTreatmentNotStated,
                $"{subject} does not say whether its rates include tax. What tax is actually due is an accounting "
                + "determination; whether the card says so is not."));

        await EvaluateOverlapAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    private async Task EvaluateOverlapAsync(
        RateCard definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var others = await _cards.ListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var other in others)
        {
            var card = other.Definition;

            if (string.Equals(card.CodeKey, definition.CodeKey, StringComparison.Ordinal))
                continue;

            if (other.ValidationState is ReferenceValidationState.Superseded or ReferenceValidationState.Draft)
                continue;

            if (!string.Equals(card.AppliesTo, definition.AppliesTo, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!card.EffectivePeriod.Overlaps(definition.EffectivePeriod))
                continue;

            warnings.Add(Diagnostic(
                PricingValidationRules.OverlappingCardPeriods,
                $"{subject} ({definition.EffectivePeriod}) overlaps rate card '{card.Code}' ({card.EffectivePeriod}) for the "
                + $"same segment. Two cards claiming to be the applicable price on one day is a question nobody can answer."));
        }
    }
}
