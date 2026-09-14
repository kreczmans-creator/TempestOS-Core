using Tempest.Core.BusinessGovernance;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Estimating;

/// <summary>Where a customer quotation has got to.</summary>
/// <remarks>
/// A second axis from <see cref="ReferenceValidationState"/>, on the same
/// reasoning `ADR-0129` gives for contract status in `P07`: a record can
/// be a perfectly good, released, validated record <em>of a draft
/// quotation</em>. Conflating the two would make it impossible to hold an
/// accurate record of something unfinished.
/// </remarks>
public enum QuotationStatus
{
    /// <summary>Being prepared. Nothing has left the building.</summary>
    Draft,

    /// <summary>Prepared and awaiting the authority that would let it be sent.</summary>
    AwaitingApproval,

    /// <summary>Sent to the customer.</summary>
    Issued,

    /// <summary>Withdrawn by the organisation before any answer.</summary>
    Withdrawn,

    /// <summary>Ran past its own validity without an answer.</summary>
    Lapsed,

    /// <summary>The customer declined it.</summary>
    Declined,

    /// <summary>The customer accepted it.</summary>
    Accepted,

    /// <summary>Replaced by a later revision of the same offer.</summary>
    Superseded
}

/// <summary>One priced line of an offer to a customer.</summary>
/// <param name="Reference">The line's own identifier within the quotation. Required.</param>
/// <param name="Description">What is being offered. Required.</param>
/// <param name="Quantity">How many.</param>
/// <param name="UnitPrice">The price of one, as offered. Required.</param>
/// <param name="LeadTime">The lead time offered for this line. <see langword="null"/> where none is.</param>
/// <param name="EstimateLineReference">The estimate line this price was built from. <see langword="null"/> where the price was set some other way.</param>
public sealed record QuotationLine(
    string Reference,
    string Description,
    decimal Quantity,
    Money UnitPrice,
    LeadTimeDuration? LeadTime = null,
    string? EstimateLineReference = null)
{
    /// <summary>The line's own identifier within the quotation.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A quotation line must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What is being offered.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A quotation line must say what is being offered.", nameof(Description))
        : Description.Trim();

    /// <summary>How many.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="Quantity"/> is not positive.</exception>
    public decimal Quantity { get; } = Quantity <= 0m
        ? throw new ArgumentOutOfRangeException(nameof(Quantity), Quantity, "A quotation line must offer a positive quantity.")
        : Quantity;

    /// <summary>The line total.</summary>
    public Money LineTotal => UnitPrice * Quantity;
}

/// <summary>
/// What the organisation offered a customer, and what it rests on.
/// </summary>
/// <remarks>
/// <para>
/// <b>The third of the four things D4 keeps apart.</b> A customer
/// quotation is an offer the organisation makes. It is not an estimate —
/// an estimate is what the work is thought to cost, a quotation is what
/// somebody is being asked to pay, and the difference between them is the
/// margin. It is not a supplier quote either: those are prices coming in,
/// this is a price going out (`ADR-0134`).
/// </para>
/// <para>
/// <b>TempestOS does not issue it.</b> Nothing on this type sends,
/// approves or commits anything. <see cref="Status"/> records where a
/// quotation has got to; moving it to <see cref="QuotationStatus.Issued"/>
/// is a statement that a person did so, and
/// <see cref="IssuedUnderAuthority"/> records which person and on what
/// basis. Issuing an offer binds the organisation, and that is an act of
/// commercial authority `P03` does not hold (`ADR-0135`).
/// </para>
/// </remarks>
public sealed record CustomerQuotation
{
    /// <summary>The reference the quotation is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>Who it is for. Required.</summary>
    public required string CustomerName { get; init; }

    /// <summary>What is being offered. Required.</summary>
    public required string Subject { get; init; }

    /// <summary>The currency it is stated in. Required.</summary>
    public required CurrencyCode Currency { get; init; }

    /// <summary>Where the quotation has got to.</summary>
    public QuotationStatus Status { get; init; } = QuotationStatus.Draft;

    /// <summary>The lines. Never <see langword="null"/>.</summary>
    public IReadOnlyList<QuotationLine> Lines { get; init; } = [];

    /// <summary>The customer's own enquiry or RFQ number. <see langword="null"/> where there was none.</summary>
    public string? CustomerEnquiryReference { get; init; }

    /// <summary>The estimate the pricing was built from. <see langword="null"/> where the price was set some other way.</summary>
    /// <remarks>
    /// A pin rather than a reference, so the quotation keeps pointing at
    /// the estimate <em>as it was</em> when the price was set. Superseding
    /// the estimate afterwards must not silently change what the customer
    /// was told the offer rested on.
    /// </remarks>
    public ReferencePin? EstimatePin { get; init; }

    /// <summary>The supplier quotes the pricing depends on. Never <see langword="null"/>.</summary>
    /// <remarks>
    /// Recorded because a customer quotation that outlives the supplier
    /// quotes beneath it is a margin nobody has checked. `D5` reads these.
    /// </remarks>
    public IReadOnlyList<ReferencePin> SupportingQuotePins { get; init; } = [];

    /// <summary>When it was issued. <see langword="null"/> until it is.</summary>
    public DateOnly? IssuedOn { get; init; }

    /// <summary>How long the offer stands. <see langword="null"/> where nobody said.</summary>
    public EffectivePeriod? Validity { get; init; }

    /// <summary>The lead time offered overall. <see langword="null"/> where none is.</summary>
    public LeadTimeDuration? OfferedLeadTime { get; init; }

    /// <summary>The conditions the offer is subject to. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Conditions { get; init; } = [];

    /// <summary>What the offer excludes. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Exclusions { get; init; } = [];

    /// <summary>Payment terms offered. <see langword="null"/> where none are stated.</summary>
    public string? PaymentTerms { get; init; }

    /// <summary>Delivery terms offered. <see langword="null"/> where none are stated.</summary>
    public string? DeliveryTerms { get; init; }

    /// <summary>Who prepared it. <see langword="null"/> where unrecorded.</summary>
    public string? PreparedByPrincipalId { get; init; }

    /// <summary>
    /// The person who issued it and the authority they did so under.
    /// </summary>
    /// <remarks>
    /// Required in substance once <see cref="Status"/> reaches
    /// <see cref="QuotationStatus.Issued"/>, and the validation service
    /// says so. An offer nobody is named as having made is an offer the
    /// organisation cannot account for.
    /// </remarks>
    public BusinessAuthorisation? IssuedUnderAuthority { get; init; }

    /// <summary>The quotation document itself, and anything else supporting the record. Never <see langword="null"/>.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = [];

    /// <summary>The quotation this one replaces. <see langword="null"/> where it replaces none.</summary>
    public string? SupersedesReference { get; init; }

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The offered total, in the quotation's own currency.</summary>
    /// <exception cref="CurrencyMismatchException">A line is stated in a currency other than the quotation's.</exception>
    public Money Total => Lines.Aggregate(new Money(0m, Currency), (running, line) => running + line.LineTotal);

    /// <summary>Whether the offer has left the building.</summary>
    public bool HasBeenIssued => Status is not (QuotationStatus.Draft or QuotationStatus.AwaitingApproval);

    /// <summary>Whether the offer still stands as at <paramref name="asAt"/>.</summary>
    public bool IsOpenAt(DateOnly asAt) =>
        Status == QuotationStatus.Issued && (Validity is not { } validity || validity.Contains(asAt));

    /// <summary>Whether it has run past its own validity as at <paramref name="asAt"/>.</summary>
    public bool IsExpiredAt(DateOnly asAt) => Validity?.HasExpiredBy(asAt) ?? false;

    /// <summary>
    /// The margin over <paramref name="estimatedCost"/>, as a proportion
    /// of what is being offered.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> where the estimate is unpriced, in another
    /// currency, or the quotation totals zero. Computed from the
    /// estimate's <em>highest</em> figure, so a ranged estimate yields the
    /// margin that is actually safe rather than the flattering one.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="estimatedCost"/> is <see langword="null"/>.</exception>
    public decimal? MarginOver(CostFigure estimatedCost)
    {
        ArgumentNullException.ThrowIfNull(estimatedCost);

        if (estimatedCost.IsUnknown || estimatedCost.Highest!.Value.Currency != Currency)
            return null;

        var offered = Total.Amount;

        return offered == 0m ? null : (offered - estimatedCost.Highest!.Value.Amount) / offered;
    }

    /// <summary>The case-insensitive key <see cref="Reference"/> is indexed under.</summary>
    public string ReferenceKey => ReferenceKeyFor(Reference);

    /// <summary>The case-insensitive key <paramref name="reference"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public static string ReferenceKeyFor(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return reference.Trim().ToUpperInvariant();
    }
}
