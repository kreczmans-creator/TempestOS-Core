using Tempest.Core.BusinessGovernance;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Costs;

/// <summary>What a cost figure is charged against.</summary>
/// <remarks>
/// The basis is not decoration: £40 "per part" and £40 "per batch" differ
/// by the batch size, and a library that records the number without the
/// basis has recorded something nobody can use.
/// </remarks>
public enum CostBasis
{
    /// <summary>Not stated. A figure with no basis cannot be applied to anything.</summary>
    Unspecified,

    /// <summary>Charged once per component.</summary>
    PerPart,

    /// <summary>Charged once for the whole batch, whatever its size.</summary>
    PerBatch,

    /// <summary>Charged by machine or labour hour.</summary>
    PerHour,

    /// <summary>Charged by mass.</summary>
    PerKilogram,

    /// <summary>Charged by area — plating, painting, coating.</summary>
    PerSquareMetre,

    /// <summary>Charged by length — bar, profile, extrusion.</summary>
    PerMetre,

    /// <summary>Charged once, for the order, regardless of quantity — a setup or carriage charge.</summary>
    PerOrder,

    /// <summary>Charged once, ever — tooling, a pattern, a fixture.</summary>
    OneOff
}

/// <summary>Which part of a total cost a component accounts for.</summary>
public enum CostComponentKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Raw material.</summary>
    Material,

    /// <summary>Direct labour.</summary>
    Labour,

    /// <summary>Machine time.</summary>
    Machine,

    /// <summary>Getting the job on — programming, fixturing, first-off.</summary>
    Setup,

    /// <summary>Tooling, patterns, dies and fixtures.</summary>
    Tooling,

    /// <summary>Finishing, plating, heat treatment or another secondary operation.</summary>
    Treatment,

    /// <summary>Inspection and certification.</summary>
    Inspection,

    /// <summary>Carriage and packing.</summary>
    Carriage,

    /// <summary>Recovered overhead, where a source separates it.</summary>
    Overhead,

    /// <summary>The supplier's margin, where a source separates it.</summary>
    Margin,

    /// <summary>Something else, described on the component.</summary>
    Other
}

/// <summary>
/// One part of a cost, where a source breaks the total down.
/// </summary>
/// <remarks>
/// Optional throughout. Most quoted prices arrive as a single number, and
/// inventing a breakdown for one would be manufacturing detail nobody
/// supplied. Where a source does break it down, the parts are recorded as
/// the source gave them and are never assumed to sum to the total —
/// suppliers routinely omit their own margin.
/// </remarks>
/// <param name="Kind">Which part of the total this is.</param>
/// <param name="Amount">What it is. Required.</param>
/// <param name="Description">What it covers, in the source's own words. <see langword="null"/> where the kind says it.</param>
public sealed record CostComponent(CostComponentKind Kind, CostFigure Amount, string? Description = null)
{
    /// <summary>What the component is.</summary>
    public CostFigure Amount { get; } = Amount ?? throw new ArgumentNullException(nameof(Amount));
}

/// <summary>
/// What the organisation knows about obtaining one process at one price.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not the Manufacturing Process Library.</b> `A7` answers
/// "what is investment casting?"; this answers "what does investment
/// casting cost from this supplier, at this quantity, in this currency,
/// as at this date, and who told us?". The two are linked by
/// <see cref="CommercialApplicability.ProcessRecordId"/> and neither
/// duplicates the other.
/// </para>
/// <para>
/// Every figure carries its context, because a price without a quantity
/// basis, a currency and a date is not a price. The applicability object
/// makes those structural rather than optional prose, and validation
/// treats a missing quantity basis as an error.
/// </para>
/// </remarks>
public sealed record ProcessCostRecord
{
    /// <summary>The reference the cost record is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What the figure is for, in plain terms. Required.</summary>
    public required string Description { get; init; }

    /// <summary>What the figure is charged against. Required in substance — validation reports an unspecified basis.</summary>
    public CostBasis Basis { get; init; } = CostBasis.Unspecified;

    /// <summary>The figure itself. Required.</summary>
    public required CostFigure Cost { get; init; }

    /// <summary>Where and when the figure applies, and to what. Required.</summary>
    public required CommercialApplicability Applicability { get; init; }

    /// <summary>Where the figure came from.</summary>
    public CommercialSource Source { get; init; } = CommercialSource.Unrecorded;

    /// <summary>The least that will be charged however small the job. <see langword="null"/> where the source states none.</summary>
    public CostFigure? MinimumCharge { get; init; }

    /// <summary>A one-off setup charge, separate from the per-unit figure. <see langword="null"/> where the source states none.</summary>
    public CostFigure? SetupCost { get; init; }

    /// <summary>A one-off tooling charge. <see langword="null"/> where the source states none.</summary>
    public CostFigure? ToolingCost { get; init; }

    /// <summary>How the source breaks the figure down. Never <see langword="null"/>; empty is the ordinary case.</summary>
    public IReadOnlyList<CostComponent> Components { get; init; } = [];

    /// <summary>Other cost records this one contradicts, by reference. Never <see langword="null"/>.</summary>
    /// <remarks>
    /// Recorded rather than resolved. Two credible sources giving
    /// different prices for the same thing is a fact about the market as
    /// often as it is an error, and the disagreement is more useful on the
    /// record than silently averaged away.
    /// </remarks>
    public IReadOnlyList<string> ContradictedBy { get; init; } = [];

    /// <summary>Anything else about the figure. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The currency the figure is in. <see langword="null"/> for an unknown cost.</summary>
    public CurrencyCode? Currency => Cost.Currency;

    /// <summary>Whether the figure came from a named supplier rather than a market source.</summary>
    public bool IsSupplierSpecific => Applicability.IsSupplierSpecific;

    /// <summary>Whether another record of at least equal standing disagrees.</summary>
    public bool IsContradicted => ContradictedBy.Count > 0;

    /// <summary>Whether the record's own validity has run out as at <paramref name="asAt"/>.</summary>
    public bool IsStaleAt(DateOnly asAt) => Applicability.IsStaleAt(asAt);

    /// <summary>
    /// The total cost of <paramref name="quantity"/> units, including
    /// setup, tooling and any minimum charge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Returns unknown if any part is unknown</b>, rather than a
    /// number that is certainly too small. Exact decimal arithmetic
    /// throughout, with no rounding of its own.
    /// </para>
    /// <para>
    /// The basis decides how the figure scales: per-part multiplies,
    /// per-batch and per-order do not, and a basis this record cannot
    /// scale — per kilogram, per metre, per hour — returns
    /// <see langword="null"/> rather than guessing a mass, a length or a
    /// cycle time that nobody supplied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quantity"/> is less than one.</exception>
    /// <exception cref="CurrencyMismatchException">Setup, tooling or the minimum charge is in a different currency from the cost.</exception>
    public CostFigure? TotalFor(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);

        var scaled = Basis switch
        {
            CostBasis.PerPart => Cost * quantity,
            CostBasis.PerBatch or CostBasis.PerOrder or CostBasis.OneOff => Cost,
            _ => null,
        };

        if (scaled is null)
            return null;

        if (MinimumCharge is { } minimum
            && !scaled.IsUnknown
            && !minimum.IsUnknown
            && scaled.Lowest!.Value < minimum.Lowest!.Value)
            scaled = minimum;

        var extras = new[] { SetupCost, ToolingCost }.OfType<CostFigure>();

        return extras.Aggregate(scaled, (running, extra) => running + extra);
    }

    /// <summary>Whether this record applies to <paramref name="enquiry"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="enquiry"/> is <see langword="null"/>.</exception>
    public bool AppliesTo(CommercialEnquiry enquiry) => Applicability.AppliesTo(enquiry);

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
