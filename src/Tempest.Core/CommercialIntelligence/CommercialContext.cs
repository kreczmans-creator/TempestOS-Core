using Tempest.Core.BusinessGovernance;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence;

/// <summary>
/// Where a commercial figure applies.
/// </summary>
/// <remarks>
/// <para>
/// A price obtained in one country is not a price in another, and a lead
/// time from a supplier three hundred miles away is not a lead time from
/// one overseas. Geography is recorded as free-standing text plus an
/// optional ISO country code, deliberately: TempestOS ships no
/// geographical hierarchy, and inventing one would be a second registry
/// nobody maintains.
/// </para>
/// <para>
/// An unstated scope means the record does not say where it applies —
/// which is a gap, not a claim of universality.
/// </para>
/// </remarks>
/// <param name="CountryCode">An ISO 3166-1 alpha-2 country code, upper-cased. <see langword="null"/> where not stated.</param>
/// <param name="Description">Where the figure applies, in the source's own words — "UK mainland", "EU excluding islands". <see langword="null"/> where not stated.</param>
public sealed record GeographicScope(string? CountryCode = null, string? Description = null)
{
    /// <summary>An ISO 3166-1 alpha-2 country code.</summary>
    /// <exception cref="ArgumentException"><paramref name="CountryCode"/> is present and is not two ASCII letters.</exception>
    public string? CountryCode { get; } = Normalise(CountryCode);

    /// <summary>A scope nobody stated.</summary>
    public static GeographicScope Unstated { get; } = new();

    /// <summary>Whether the record says anything at all about where it applies.</summary>
    public bool IsStated => CountryCode is not null || !string.IsNullOrWhiteSpace(Description);

    /// <summary>Whether this scope covers <paramref name="countryCode"/>.</summary>
    /// <remarks>
    /// An unstated scope covers nothing, and returns
    /// <see langword="false"/>. Treating "nobody said" as "everywhere" is
    /// how a UK price ends up in an estimate for work in Singapore.
    /// </remarks>
    public bool Covers(string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        return CountryCode is not null
               && string.Equals(CountryCode, Normalise(countryCode), StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override string ToString() => (CountryCode, Description) switch
    {
        (null, null) => "(scope not stated)",
        (not null, null) => CountryCode,
        (null, not null) => Description!,
        _ => $"{CountryCode} ({Description})",
    };

    private static string? Normalise(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var trimmed = code.Trim();

        if (trimmed.Length != 2 || !trimmed.All(char.IsAsciiLetter))
            throw new ArgumentException(
                $"'{code}' is not a two-letter country code. TempestOS validates the shape of a country code, not its existence: "
                + "no geographical registry ships with this platform.",
                nameof(code));

        return trimmed.ToUpperInvariant();
    }
}

/// <summary>
/// The context that decides whether a commercial figure applies to the
/// question being asked.
/// </summary>
/// <remarks>
/// Composed into every cost and lead-time record, because a figure
/// without its context is not usable and a library that stores the number
/// alone cannot tell one supplier's price from another's.
/// </remarks>
public sealed record CommercialApplicability
{
    /// <summary>The `A7` process the figure is about, by process record Id. <see langword="null"/> where the figure is not process-specific.</summary>
    /// <remarks>
    /// A reference to `P01`, never a copy of it. What CNC milling <i>is</i>
    /// belongs to the Manufacturing Process Library; what it costs from a
    /// particular supplier belongs here.
    /// </remarks>
    public string? ProcessRecordId { get; init; }

    /// <summary>The `A1` materials the figure is about, by material record Id. Never <see langword="null"/>; empty where it is not material-specific.</summary>
    public IReadOnlyList<string> MaterialRecordIds { get; init; } = [];

    /// <summary>The supplier the figure came from, by supplier reference. <see langword="null"/> for a market or published figure.</summary>
    public string? SupplierReference { get; init; }

    /// <summary>The quantities the figure applies to. <see langword="null"/> where nobody recorded a basis — a gap validation reports.</summary>
    public QuantityBand? Quantities { get; init; }

    /// <summary>Where the figure applies.</summary>
    public GeographicScope Geography { get; init; } = GeographicScope.Unstated;

    /// <summary>When the figure applies. <see langword="null"/> where nobody recorded a validity — a gap validation reports.</summary>
    public EffectivePeriod? Validity { get; init; }

    /// <summary>Anything else the figure depends on, in the source's own words. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Conditions { get; init; } = [];

    /// <summary>Whether the figure came from a named supplier rather than a market source.</summary>
    public bool IsSupplierSpecific => !string.IsNullOrWhiteSpace(SupplierReference);

    /// <summary>Whether the figure states what quantity it applies to.</summary>
    public bool HasQuantityBasis => Quantities is not null;

    /// <summary>Whether the figure states when it applies.</summary>
    public bool HasValidity => Validity is not null;

    /// <summary>Whether the figure's own validity has run out as at <paramref name="asAt"/>.</summary>
    public bool IsStaleAt(DateOnly asAt) => Validity?.HasExpiredBy(asAt) ?? false;

    /// <summary>
    /// Whether this figure applies to <paramref name="enquiry"/>.
    /// </summary>
    /// <remarks>
    /// <b>Absence never matches.</b> A figure with no quantity basis does
    /// not apply to a quantity of 500 — it applies to nothing anybody can
    /// name, and pretending otherwise is how an unqualified price reaches
    /// an estimate. Where the enquiry itself leaves a dimension open, that
    /// dimension is not tested.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="enquiry"/> is <see langword="null"/>.</exception>
    public bool AppliesTo(CommercialEnquiry enquiry)
    {
        ArgumentNullException.ThrowIfNull(enquiry);

        if (enquiry.ProcessRecordId is { } process
            && !string.Equals(ProcessRecordId, process, StringComparison.OrdinalIgnoreCase))
            return false;

        if (enquiry.MaterialRecordId is { } material
            && MaterialRecordIds.Count > 0
            && !MaterialRecordIds.Contains(material, StringComparer.OrdinalIgnoreCase))
            return false;

        if (enquiry.SupplierReference is { } supplier
            && !string.Equals(SupplierReference, supplier, StringComparison.OrdinalIgnoreCase))
            return false;

        if (enquiry.Quantity is { } quantity && (Quantities is null || !Quantities.Contains(quantity)))
            return false;

        if (enquiry.CountryCode is { } country && !Geography.Covers(country))
            return false;

        if (enquiry.AsAt is { } date && (Validity is null || !Validity.Contains(date)))
            return false;

        return true;
    }
}

/// <summary>
/// What a caller is asking for, when looking for applicable commercial
/// information.
/// </summary>
/// <remarks>
/// Every dimension is optional, and an omitted one is not tested rather
/// than matched against a default. Asking "what does anodising cost?"
/// without a quantity legitimately returns figures across all quantity
/// bands; it is the caller's job to notice that they asked a loose
/// question.
/// </remarks>
public sealed record CommercialEnquiry
{
    /// <summary>The `A7` process asked about. <see langword="null"/> to leave it open.</summary>
    public string? ProcessRecordId { get; init; }

    /// <summary>The `A1` material asked about. <see langword="null"/> to leave it open.</summary>
    public string? MaterialRecordId { get; init; }

    /// <summary>The supplier asked about. <see langword="null"/> to leave it open.</summary>
    public string? SupplierReference { get; init; }

    /// <summary>The quantity asked about. <see langword="null"/> to leave it open.</summary>
    public int? Quantity { get; init; }

    /// <summary>The country the work is for. <see langword="null"/> to leave it open.</summary>
    public string? CountryCode { get; init; }

    /// <summary>The date the answer must be valid on. <see langword="null"/> to leave it open.</summary>
    public DateOnly? AsAt { get; init; }
}

/// <summary>
/// The shared source information every `P03` record carries beyond the
/// provenance the reference-data layer already holds.
/// </summary>
/// <remarks>
/// <see cref="ReferenceProvenance"/> answers where a record came from and
/// whether anybody verified it, and is reused unchanged. What it does not
/// carry is the commercial-specific pair below: when the figure was
/// actually observed, as distinct from when the source document is dated,
/// and what evidence a reader could go and look at.
/// </remarks>
/// <param name="ObservedOn">When somebody actually saw this figure — an enquiry answered, a quote received, an invoice paid. <see langword="null"/> where not recorded.</param>
/// <param name="Evidence">What a reader could retrieve to check it. Never <see langword="null"/>.</param>
public sealed record CommercialSource(DateOnly? ObservedOn = null, IReadOnlyList<BusinessEvidence>? Evidence = null)
{
    /// <summary>What a reader could retrieve to check the figure.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = Evidence ?? [];

    /// <summary>A source nobody recorded.</summary>
    public static CommercialSource Unrecorded { get; } = new();

    /// <summary>Whether anything at all supports the figure.</summary>
    public bool HasEvidence => Evidence.Count > 0;

    /// <summary>Whether every piece of evidence can actually be retrieved.</summary>
    public bool AllEvidenceIsLocatable => Evidence.All(e => e.IsLocatable);

    /// <summary>How old the figure is, in days, as at <paramref name="asAt"/>. <see langword="null"/> where nobody recorded when it was observed.</summary>
    public int? AgeInDaysAt(DateOnly asAt) => ObservedOn is { } observed ? asAt.DayNumber - observed.DayNumber : null;

    /// <summary>Whether the figure was observed more than <paramref name="days"/> ago.</summary>
    /// <remarks>
    /// A figure with no observation date returns <see langword="true"/>:
    /// an undated price is at least as suspect as an old one, and treating
    /// it as fresh is the more dangerous of the two mistakes.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="days"/> is negative.</exception>
    public bool IsOlderThan(DateOnly asAt, int days)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);

        return AgeInDaysAt(asAt) is not { } age || age > days;
    }
}
