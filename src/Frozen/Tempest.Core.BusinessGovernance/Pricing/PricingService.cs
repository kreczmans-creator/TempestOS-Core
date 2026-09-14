using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Pricing;

/// <summary>Thrown when pricing is attempted against a card that cannot be used.</summary>
public sealed class RateCardUnusableException : ReferenceDataException
{
    /// <summary>Initialises a new instance of the <see cref="RateCardUnusableException"/> class.</summary>
    /// <param name="cardCode">The card that was asked for.</param>
    /// <param name="reason">Why it cannot be used.</param>
    public RateCardUnusableException(string cardCode, string reason)
        : base("BusinessRateCards", $"Rate card '{cardCode}' cannot be quoted from: {reason}")
    {
        CardCode = cardCode;
    }

    /// <summary>The card that was asked for.</summary>
    public string CardCode { get; }
}

/// <summary>One priced line of a quotation.</summary>
/// <param name="ServiceCode">The service priced.</param>
/// <param name="ServiceName">What the service is called, as the card names it.</param>
/// <param name="Basis">What the rate is charged against.</param>
/// <param name="Units">How many units were priced.</param>
/// <param name="ListRate">The published rate, as the card states it.</param>
/// <param name="LineTotal">The charge, with any minimum applied.</param>
/// <param name="MinimumApplied">Whether the card's minimum charge raised the line above rate times units.</param>
public sealed record QuotationLine(
    string ServiceCode,
    string ServiceName,
    PricingBasis Basis,
    decimal Units,
    Money ListRate,
    Money LineTotal,
    bool MinimumApplied);

/// <summary>
/// A priced quotation, tied to the exact rate-card revision it was priced
/// from.
/// </summary>
/// <remarks>
/// <b>A quotation is a calculation, not a commitment.</b> It states what
/// the published rates come to for the units asked about. What the
/// organisation actually offers is a commercial decision, recorded as a
/// <see cref="QuotedRate"/> of kind <see cref="RateKind.Quoted"/> by
/// whoever makes it.
/// </remarks>
/// <param name="RateCardPin">The exact card revision priced from.</param>
/// <param name="RateCardCode">The card's code, so the quotation reads without resolving the pin.</param>
/// <param name="PricedOn">The date the quotation was priced at, which decides which card applied.</param>
/// <param name="Currency">The currency every line is stated in.</param>
/// <param name="Lines">The priced lines, in the order they were asked for.</param>
/// <param name="Total">The sum of the lines.</param>
public sealed record RateCardQuotation(
    ReferencePin RateCardPin,
    string RateCardCode,
    DateOnly PricedOn,
    CurrencyCode Currency,
    IReadOnlyList<QuotationLine> Lines,
    Money Total)
{
    /// <summary>Whether any line was raised by the card's own minimum charge.</summary>
    public bool AnyMinimumApplied => Lines.Any(l => l.MinimumApplied);

    /// <summary>
    /// The list rates this quotation used, as
    /// <see cref="QuotedRate"/> records ready to sit alongside whatever is
    /// eventually quoted, negotiated and realised.
    /// </summary>
    public IReadOnlyList<QuotedRate> AsListRates() =>
        Lines.Select(l => new QuotedRate(
                l.ServiceCode,
                RateKind.List,
                l.Basis,
                l.ListRate,
                RateCardPin,
                PricedOn))
            .ToList();
}

/// <summary>How many units of one service a quotation should price.</summary>
/// <param name="ServiceCode">The service to price. Required.</param>
/// <param name="Units">How many units.</param>
public sealed record QuotationRequest(string ServiceCode, decimal Units)
{
    /// <summary>The service to price.</summary>
    public string ServiceCode { get; } = string.IsNullOrWhiteSpace(ServiceCode)
        ? throw new ArgumentException("A quotation line must name the service it prices.", nameof(ServiceCode))
        : ServiceCode.Trim();

    /// <summary>How many units.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="Units"/> is negative.</exception>
    public decimal Units { get; } = Units < 0m
        ? throw new ArgumentOutOfRangeException(nameof(Units), Units, "A quotation cannot price a negative quantity of work.")
        : Units;
}

/// <summary>
/// Prices work against the rate card that applied on a stated date.
/// </summary>
/// <remarks>
/// <b>Nothing here sets a price or approves one.</b> The service reads a
/// released, approved card, applies its published rates and minimums
/// exactly, and records which revision it read. What to charge a
/// particular client is a commercial decision a person makes.
/// </remarks>
public interface IPricingService
{
    /// <summary>
    /// Prices <paramref name="requests"/> against the card that applied on
    /// <paramref name="pricedOn"/>.
    /// </summary>
    /// <param name="cardCode">The card to price from.</param>
    /// <param name="pricedOn">The date deciding whether the card applied.</param>
    /// <param name="requests">What to price.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <exception cref="ArgumentException"><paramref name="cardCode"/> is blank, or the card does not price a requested service.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="requests"/> is <see langword="null"/>.</exception>
    /// <exception cref="ReferenceRecordNotFoundException">No card is registered under <paramref name="cardCode"/>.</exception>
    /// <exception cref="RateCardUnusableException">The card is not released, not approved, or did not apply on that date.</exception>
    Task<RateCardQuotation> QuoteAsync(
        string cardCode,
        DateOnly pricedOn,
        IReadOnlyList<QuotationRequest> requests,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-prices <paramref name="requests"/> against the exact card
    /// revision <paramref name="cardPin"/> names, whatever the card says
    /// today.
    /// </summary>
    /// <remarks>
    /// The method that keeps an old quotation readable. Re-running a
    /// two-year-old quote against today's card would silently answer a
    /// different question.
    /// </remarks>
    /// <param name="cardPin">The card revision to reproduce.</param>
    /// <param name="pricedOn">The date the original quotation was priced at.</param>
    /// <param name="requests">What to price.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cardPin"/> or <paramref name="requests"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="cardPin"/> does not name the rate-card library.</exception>
    Task<RateCardQuotation> ReproduceAsync(
        ReferencePin cardPin,
        DateOnly pricedOn,
        IReadOnlyList<QuotationRequest> requests,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IPricingService"/> implementation.</summary>
public sealed class PricingService : IPricingService
{
    private readonly IRateCardCatalog _cards;

    /// <summary>Initialises a new instance of the <see cref="PricingService"/> class.</summary>
    /// <param name="cards">The rate-card library.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cards"/> is <see langword="null"/>.</exception>
    public PricingService(IRateCardCatalog cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        _cards = cards;
    }

    /// <inheritdoc />
    public async Task<RateCardQuotation> QuoteAsync(
        string cardCode,
        DateOnly pricedOn,
        IReadOnlyList<QuotationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardCode);
        ArgumentNullException.ThrowIfNull(requests);

        var record = await _cards.FindByCodeAsync(cardCode, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(_cards.LibraryName, cardCode);

        var card = record.Definition;

        if (record.ValidationState != ReferenceValidationState.Released)
            throw new RateCardUnusableException(card.Code,
                $"the record is {record.ValidationState}, not Released. A price nobody has finished checking must not be quoted.");

        if (!card.IsApproved)
            throw new RateCardUnusableException(card.Code,
                "no named person has approved it. A published price binds the organisation to whoever it is shown to.");

        if (!card.AppliesOn(pricedOn))
            throw new RateCardUnusableException(card.Code,
                $"it applies {card.EffectivePeriod} and does not cover {pricedOn:O}.");

        return Price(record, pricedOn, requests);
    }

    /// <inheritdoc />
    public async Task<RateCardQuotation> ReproduceAsync(
        ReferencePin cardPin,
        DateOnly pricedOn,
        IReadOnlyList<QuotationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cardPin);
        ArgumentNullException.ThrowIfNull(requests);

        if (!string.Equals(cardPin.Library, _cards.LibraryName, StringComparison.Ordinal))
            throw new ArgumentException(
                $"Pin {cardPin} names library '{cardPin.Library}', and this service can only reproduce rate-card pins.",
                nameof(cardPin));

        var record = await _cards
            .GetRevisionAsync(cardPin.RecordId, cardPin.RevisionNumber, cancellationToken)
            .ConfigureAwait(false);

        // Deliberately no released-and-approved check here. Reproducing a
        // historical quotation must give the answer it gave at the time,
        // even if the card has since been superseded — which is the whole
        // point of pinning it.
        return Price(record, pricedOn, requests);
    }

    private static RateCardQuotation Price(
        IReferenceRecord<RateCard> record,
        DateOnly pricedOn,
        IReadOnlyList<QuotationRequest> requests)
    {
        var card = record.Definition;
        var lines = new List<QuotationLine>(requests.Count);

        foreach (var request in requests)
        {
            var entry = card.FindEntry(request.ServiceCode)
                ?? throw new ArgumentException(
                    $"Rate card '{card.Code}' does not price service '{request.ServiceCode}'.",
                    nameof(requests));

            var straight = entry.ChargeFor(request.Units);
            var charged = entry.ChargeWithMinimumFor(request.Units);

            lines.Add(new QuotationLine(
                entry.ServiceCode,
                entry.ServiceName,
                entry.Basis,
                request.Units,
                entry.Rate,
                charged,
                MinimumApplied: charged != straight));
        }

        return new RateCardQuotation(
            ReferencePin.For("BusinessRateCards", record),
            card.Code,
            pricedOn,
            card.Currency,
            lines,
            Money.Sum(lines.Select(l => l.LineTotal), card.Currency));
    }
}
