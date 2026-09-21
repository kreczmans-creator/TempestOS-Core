using Tempest.Core.BusinessGovernance.Contracts;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Quotations;

/// <summary>Where a sales quotation stands with the client it was sent to.</summary>
/// <remarks>
/// <para>
/// Deliberately separate from <see cref="ReferenceValidationState"/>, for
/// exactly the reason <see cref="ContractStatus"/> is (`ADR-0129`): the
/// record's own lifecycle asks "may work rely on this record?"; this asks
/// "what did the client say?". A quotation record can be Released as a
/// record while the quotation itself is still a draft nobody has sent.
/// </para>
/// <para>
/// The status moves one way: <see cref="Draft"/> to <see cref="Submitted"/>,
/// then to <see cref="Accepted"/> or <see cref="Declined"/>, and nowhere
/// else. A declined quotation is not re-drafted; a new one is issued.
/// <see cref="QuotationStatuses.CanMove"/> is the one statement of that
/// rule and <see cref="QuotationValidationService"/> reports a breach.
/// </para>
/// </remarks>
public enum QuotationStatus
{
    /// <summary>Being priced and written. Not sent to the client.</summary>
    Draft,

    /// <summary>Sent to the client, and awaiting their decision.</summary>
    Submitted,

    /// <summary>The client accepted it. Usually the point a contract is prepared from it.</summary>
    Accepted,

    /// <summary>The client declined it.</summary>
    Declined
}

/// <summary>Reasoning over <see cref="QuotationStatus"/>.</summary>
public static class QuotationStatuses
{
    /// <summary>Every status, in the order a report should present them.</summary>
    public static IReadOnlyList<QuotationStatus> All { get; } =
    [
        QuotationStatus.Draft, QuotationStatus.Submitted, QuotationStatus.Accepted, QuotationStatus.Declined,
    ];

    /// <summary>Whether the quotation is still waiting on the client — the ones a dashboard chases.</summary>
    public static bool IsOpen(QuotationStatus status) => status is QuotationStatus.Draft or QuotationStatus.Submitted;

    /// <summary>Whether the client has answered, either way.</summary>
    public static bool IsDecided(QuotationStatus status) => status is QuotationStatus.Accepted or QuotationStatus.Declined;

    /// <summary>Whether the quotation has been sent to the client at some point.</summary>
    public static bool HasBeenSubmitted(QuotationStatus status) => status != QuotationStatus.Draft;

    /// <summary>
    /// Whether a quotation recorded as <paramref name="from"/> may next be
    /// recorded as <paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// Staying put is always allowed — a revision that corrects a title
    /// does not move the status. Otherwise only Draft to Submitted, and
    /// Submitted to Accepted or Declined. Nothing moves backwards and
    /// nothing skips Submitted: a client cannot accept what was never sent.
    /// </remarks>
    public static bool CanMove(QuotationStatus from, QuotationStatus to) =>
        from == to
        || (from, to) is (QuotationStatus.Draft, QuotationStatus.Submitted)
            or (QuotationStatus.Submitted, QuotationStatus.Accepted)
            or (QuotationStatus.Submitted, QuotationStatus.Declined);
}

/// <summary>
/// A quotation the organisation has sent, or is preparing to send, to a
/// client: a stated price for stated work.
/// </summary>
/// <remarks>
/// <para>
/// The sales-side counterpart of <see cref="IssuedContract"/>, and the one
/// `P07` record the `WP 18.0C` freeze never held: `CommercialIntelligence`
/// records quotes <i>received from suppliers</i>, and
/// <see cref="Pricing.RateCardQuotation"/> is the arithmetic of pricing
/// work against a rate card, not the offer made. This is the offer made.
/// </para>
/// <para>
/// <b>Registering a quotation is not accepting one.</b> The record's
/// <see cref="Status"/> says what the client said; the dates say when. A
/// quotation that became a contract names it in
/// <see cref="IssuedContractReference"/>, and where it was priced from a
/// rate card the exact revision is pinned in <see cref="RateCardPin"/>,
/// for the same reason a contract pins its template: revising the card
/// afterwards changes nothing about a price already quoted.
/// </para>
/// </remarks>
public sealed record Quotation
{
    /// <summary>The reference the quotation is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>Who it was sent to. Required.</summary>
    public required ContractParty Client { get; init; }

    /// <summary>What was quoted for. Required.</summary>
    public required string Title { get; init; }

    /// <summary>The price quoted, in the currency it was quoted in. Required.</summary>
    public required Money Amount { get; init; }

    /// <summary>The governance every `P07` record carries. Required.</summary>
    public required BusinessGovernanceFacts Governance { get; init; }

    /// <summary>Where the quotation stands with the client.</summary>
    public QuotationStatus Status { get; init; } = QuotationStatus.Draft;

    /// <summary>When it was sent to the client. <see langword="null"/> while it is a draft.</summary>
    public DateOnly? SubmittedOn { get; init; }

    /// <summary>When somebody should chase it. <see langword="null"/> where no follow-up is planned.</summary>
    public DateOnly? FollowUpOn { get; init; }

    /// <summary>When the client accepted or declined it. <see langword="null"/> until they do.</summary>
    public DateOnly? DecidedOn { get; init; }

    /// <summary>
    /// The exact rate-card revision the price was worked out from, where
    /// it was. <see langword="null"/> for a price set by hand.
    /// </summary>
    public ReferencePin? RateCardPin { get; init; }

    /// <summary>
    /// The contract this quotation became, where it became one — an
    /// <see cref="IssuedContract.Reference"/>. <see langword="null"/> otherwise.
    /// </summary>
    public string? IssuedContractReference { get; init; }

    /// <summary>Anything else about the quotation. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether the quotation is still waiting on the client.</summary>
    public bool IsOpen => QuotationStatuses.IsOpen(Status);

    /// <summary>Whether it was priced from a rate card at a known revision.</summary>
    public bool IsFromRateCard => RateCardPin is not null;

    /// <summary>Whether it became a contract.</summary>
    public bool BecameContract => IssuedContractReference is not null;

    /// <summary>Whether the follow-up date has passed with the quotation still open, as at <paramref name="asAt"/>.</summary>
    public bool IsFollowUpOverdueAt(DateOnly asAt) => IsOpen && FollowUpOn is { } due && due <= asAt;

    /// <summary>Every reference-data revision this quotation rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        new[] { RateCardPin }
            .OfType<ReferencePin>()
            .Concat(Governance.Evidence.Select(e => e.Pin).OfType<ReferencePin>())
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionNumber)
            .ToList();

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
