using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Pricing;

/// <summary>What a rate is charged against.</summary>
public enum PricingBasis
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Charged by the hour.</summary>
    Hourly,

    /// <summary>Charged by the half day.</summary>
    HalfDay,

    /// <summary>Charged by the day.</summary>
    Day,

    /// <summary>Charged by the week.</summary>
    Week,

    /// <summary>A single price for a defined piece of work.</summary>
    FixedPrice,

    /// <summary>A recurring fee for availability or a standing allocation.</summary>
    Retainer,

    /// <summary>Paid on reaching a defined milestone.</summary>
    Milestone,

    /// <summary>Charged per unit produced, tested or inspected.</summary>
    PerUnit,

    /// <summary>Recovered at cost, or at cost plus a stated uplift.</summary>
    Expenses,

    /// <summary>Travel, charged on its own basis.</summary>
    Travel
}

/// <summary>
/// Which number in a commercial conversation a figure is.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are four different facts and must never collapse into one.</b>
/// The list rate is what the organisation publishes. The quoted rate is
/// what it offered this client. The negotiated rate is what they agreed.
/// The realised rate is what actually came in once the work was done, the
/// overrun absorbed and the invoice discounted.
/// </para>
/// <para>
/// A business that only keeps one of these cannot answer the question that
/// matters — where does the margin actually go — and one that overwrites
/// the earlier numbers with the later ones cannot answer it retrospectively
/// either.
/// </para>
/// </remarks>
public enum RateKind
{
    /// <summary>What the organisation publishes.</summary>
    List,

    /// <summary>What was offered to a particular client for particular work.</summary>
    Quoted,

    /// <summary>What was agreed after negotiation.</summary>
    Negotiated,

    /// <summary>What the work actually earned once everything was accounted for.</summary>
    Realised
}

/// <summary>
/// One line of a rate card: a service, charged on a basis, at a rate.
/// </summary>
/// <remarks>
/// The entry is always a list rate — a rate card is the published
/// position, and the other three <see cref="RateKind"/> values belong to a
/// particular engagement rather than to the card.
/// <see cref="QuotedRate"/> is where those live.
/// </remarks>
/// <param name="ServiceCode">The service's own identifier within the card. Required.</param>
/// <param name="ServiceName">What the service is called. Required.</param>
/// <param name="Basis">What the rate is charged against.</param>
/// <param name="Rate">The rate itself. Required.</param>
/// <param name="MinimumCharge">The least that will be charged however small the job. <see langword="null"/> where there is no minimum.</param>
/// <param name="Grade">The grade or seniority the rate applies to. <see langword="null"/> where the service is not graded.</param>
/// <param name="Description">What is included, and what is not. <see langword="null"/> if the name says it.</param>
/// <param name="Conditions">Anything the rate depends on — a minimum booking, a location, a volume. Never <see langword="null"/>.</param>
public sealed record RateCardEntry(
    string ServiceCode,
    string ServiceName,
    PricingBasis Basis,
    Money Rate,
    Money? MinimumCharge = null,
    string? Grade = null,
    string? Description = null,
    IReadOnlyList<string>? Conditions = null)
{
    /// <summary>The service's own identifier within the card.</summary>
    public string ServiceCode { get; } = string.IsNullOrWhiteSpace(ServiceCode)
        ? throw new ArgumentException("A rate-card entry must carry its own service code, or nothing can quote it.", nameof(ServiceCode))
        : ServiceCode.Trim();

    /// <summary>What the service is called.</summary>
    public string ServiceName { get; } = string.IsNullOrWhiteSpace(ServiceName)
        ? throw new ArgumentException("A rate-card entry must name the service it prices.", nameof(ServiceName))
        : ServiceName.Trim();

    /// <summary>The rate itself.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="Rate"/> is negative.</exception>
    public Money Rate { get; } = Rate.IsNegative
        ? throw new ArgumentOutOfRangeException(nameof(Rate), Rate, "A published rate cannot be negative. A credit is not a rate.")
        : Rate;

    /// <summary>Anything the rate depends on.</summary>
    public IReadOnlyList<string> Conditions { get; init; } = Conditions ?? [];

    /// <summary>The case-insensitive key the entry is found by within its card.</summary>
    public string ServiceCodeKey => ServiceCode.ToUpperInvariant();

    /// <summary>
    /// The charge for <paramref name="units"/> of this service, before any
    /// minimum is applied.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="units"/> is negative.</exception>
    public Money ChargeFor(decimal units)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(units);

        return Rate * units;
    }

    /// <summary>
    /// The charge for <paramref name="units"/>, with any minimum applied.
    /// </summary>
    /// <remarks>
    /// Deterministic and exact: decimal arithmetic throughout, with no
    /// rounding of its own, so the caller decides how the result is
    /// presented.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="units"/> is negative.</exception>
    public Money ChargeWithMinimumFor(decimal units)
    {
        var charge = ChargeFor(units);

        return MinimumCharge is { } minimum && charge < minimum ? minimum : charge;
    }
}

/// <summary>
/// A published, effective-dated set of rates.
/// </summary>
/// <remarks>
/// <para>
/// <b>A rate card is versioned and dated, and a later card never rewrites
/// an earlier commercial decision.</b> A quotation names the card revision
/// it was priced from; changing the card afterwards changes the price of
/// future work and nothing else. The shared reference-data lifecycle
/// enforces the first half of that — a released card cannot be edited in
/// place — and <see cref="ReferencePin"/> the second.
/// </para>
/// <para>
/// No rates ship with TempestOS. What an organisation charges is its own
/// commercial position, arrived at from its cost base and its market, and
/// a plausible-looking default rate would be a fabricated business fact.
/// </para>
/// </remarks>
public sealed record RateCard
{
    /// <summary>The identifier the card is known by. Required.</summary>
    public required string Code { get; init; }

    /// <summary>What the card is called. Required.</summary>
    public required string Name { get; init; }

    /// <summary>When the card applies. Required — a rate with no period is a rate nobody can date a quotation against.</summary>
    public required EffectivePeriod EffectivePeriod { get; init; }

    /// <summary>The currency every rate on the card is stated in. Required.</summary>
    /// <remarks>
    /// One currency per card, deliberately. A card mixing currencies cannot
    /// be totalled, compared or uplifted without an exchange rate, and a
    /// second card is the honest way to price in a second currency.
    /// </remarks>
    public required CurrencyCode Currency { get; init; }

    /// <summary>The governance every `P07` record carries. Required.</summary>
    public required BusinessGovernanceFacts Governance { get; init; }

    /// <summary>The rates. Never <see langword="null"/>.</summary>
    public IReadOnlyList<RateCardEntry> Entries { get; init; } = [];

    /// <summary>Who the card applies to — a client segment, a sector, a region. <see langword="null"/> for the general card.</summary>
    public string? AppliesTo { get; init; }

    /// <summary>Whether the rates are stated inclusive or exclusive of tax, in the card's own words. <see langword="null"/> if it does not say.</summary>
    /// <remarks>
    /// Recorded as the card's own statement. What tax is actually due is an
    /// accounting determination, and `P07` does not compute one.
    /// </remarks>
    public string? TaxTreatment { get; init; }

    /// <summary>How expenses are recovered. <see langword="null"/> where the card does not say.</summary>
    public string? ExpensesPolicy { get; init; }

    /// <summary>Anything else about the card. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether the card is the approved commercial position, or merely a proposal somebody drafted.</summary>
    /// <remarks>
    /// A published price binds the organisation to whoever it is shown to.
    /// The card is approved when a named person exercised
    /// <see cref="BusinessAuthorityKind.InternalApproval"/> — never because
    /// the record reached Released, which says the record is accurate, not
    /// that the prices are the ones the organisation intends to charge.
    /// </remarks>
    public bool IsApproved => Governance.HasAuthority(BusinessAuthorityKind.InternalApproval);

    /// <summary>Who approved it, where anybody has.</summary>
    public BusinessAuthorisation? Approval => Governance.FindAuthority(BusinessAuthorityKind.InternalApproval);

    /// <summary>Whether the card applies on <paramref name="date"/>.</summary>
    public bool AppliesOn(DateOnly date) => EffectivePeriod.Contains(date);

    /// <summary>Returns the entry for <paramref name="serviceCode"/>, or <see langword="null"/> if the card has none.</summary>
    public RateCardEntry? FindEntry(string serviceCode) =>
        Entries.FirstOrDefault(e => string.Equals(e.ServiceCode, serviceCode, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether every rate on the card is stated in the card's own currency.</summary>
    /// <remarks>
    /// Always true for a card built through the constructor's own
    /// validation path, and checked because a card assembled from a
    /// deserialised document has had no such path.
    /// </remarks>
    public bool IsCurrencyConsistent =>
        Entries.All(e => e.Rate.Currency == Currency && (e.MinimumCharge is not { } m || m.Currency == Currency));

    /// <summary>The case-insensitive key <see cref="Code"/> is indexed under.</summary>
    public string CodeKey => CodeKeyFor(Code);

    /// <summary>The case-insensitive key <paramref name="code"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty, or whitespace.</exception>
    public static string CodeKeyFor(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return code.Trim().ToUpperInvariant();
    }
}

/// <summary>
/// A rate as it stood at one point in a commercial conversation, tied to
/// the card revision it came from.
/// </summary>
/// <remarks>
/// <para>
/// The type that keeps list, quoted, negotiated and realised apart. Each
/// is recorded as its own <see cref="QuotedRate"/> against the same
/// service, so a later reader can see the whole sequence: what was
/// published, what was offered, what was agreed, and what came in.
/// </para>
/// <para>
/// <see cref="RateCardPin"/> is what makes an old quotation still
/// readable. Without it, re-reading a two-year-old quote against today's
/// card would silently answer a different question.
/// </para>
/// </remarks>
/// <param name="ServiceCode">The service priced. Required.</param>
/// <param name="Kind">Which number in the conversation this is.</param>
/// <param name="Basis">What it is charged against.</param>
/// <param name="Rate">The rate. Required.</param>
/// <param name="RateCardPin">The exact rate-card revision this came from. <see langword="null"/> for a rate priced outside any card — itself worth reporting.</param>
/// <param name="QuotedOn">When this figure was arrived at.</param>
/// <param name="ClientReference">Who it was for. <see langword="null"/> for a list rate.</param>
/// <param name="Justification">Why it differs from the list rate. Required for a negotiated rate; <see langword="null"/> otherwise.</param>
public sealed record QuotedRate(
    string ServiceCode,
    RateKind Kind,
    PricingBasis Basis,
    Money Rate,
    ReferencePin? RateCardPin = null,
    DateOnly? QuotedOn = null,
    string? ClientReference = null,
    string? Justification = null)
{
    /// <summary>The service priced.</summary>
    public string ServiceCode { get; } = string.IsNullOrWhiteSpace(ServiceCode)
        ? throw new ArgumentException("A quoted rate must name the service it prices.", nameof(ServiceCode))
        : ServiceCode.Trim();

    /// <summary>Whether the figure can be traced to a published card revision.</summary>
    public bool IsTraceable => RateCardPin is not null;

    /// <summary>
    /// The discount from <paramref name="listRate"/>, as a proportion.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> rather than a figure where the two
    /// are in different currencies or the list rate is zero: a percentage
    /// derived from an impossible comparison would be worse than no
    /// percentage.
    /// </remarks>
    public decimal? DiscountFrom(Money listRate)
    {
        if (listRate.Currency != Rate.Currency || listRate.IsZero)
            return null;

        return (listRate.Amount - Rate.Amount) / listRate.Amount;
    }
}
