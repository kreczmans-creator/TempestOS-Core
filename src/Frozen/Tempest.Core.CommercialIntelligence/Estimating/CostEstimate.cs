using Tempest.Core.BusinessGovernance;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Estimating;

/// <summary>What part of a job an estimate line covers.</summary>
public enum EstimateLineKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Raw or bought-in material.</summary>
    Material,

    /// <summary>A manufacturing operation.</summary>
    Process,

    /// <summary>Tooling, patterns or fixtures.</summary>
    Tooling,

    /// <summary>The organisation's own engineering time.</summary>
    Labour,

    /// <summary>Something bought in whole — a subcontracted assembly, a proprietary component.</summary>
    ExternalCost,

    /// <summary>Carriage, packing and duty.</summary>
    Carriage,

    /// <summary>Inspection, testing and certification.</summary>
    Inspection,

    /// <summary>An allowance for what the estimate cannot yet see.</summary>
    Contingency
}

/// <summary>
/// Something the estimate takes to be true that nobody has established.
/// </summary>
/// <remarks>
/// Recorded because an estimate that turns out wrong is usually an
/// estimate whose assumption turned out wrong, and unless the assumption
/// was written down nobody can say which one. The same discipline `P02`
/// applies to trade studies and `P07` to forecasts.
/// </remarks>
/// <param name="Reference">The assumption's own identifier within the estimate. Required.</param>
/// <param name="Statement">What is being assumed. Required.</param>
/// <param name="Basis">Why it is reasonable. <see langword="null"/> where nobody said.</param>
/// <param name="WouldInvalidate">What would no longer hold if it were wrong. <see langword="null"/> if not stated.</param>
public sealed record EstimateAssumption(string Reference, string Statement, string? Basis = null, string? WouldInvalidate = null)
{
    /// <summary>The assumption's own identifier within the estimate.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("An estimate assumption must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What is being assumed.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("An estimate assumption must say what is being assumed.", nameof(Statement))
        : Statement.Trim();
}

/// <summary>
/// One line of a cost estimate, and the exact records it was derived
/// from.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SourcePins"/> is what makes an estimate reproducible. A
/// line derived from a cost record at revision 3 keeps that pin, so
/// re-reading the estimate two years later resolves the figure that was
/// actually used — not whatever the cost library says today.
/// </para>
/// <para>
/// A line with no pins is legitimate and is reported: it is somebody's
/// judgement rather than a derivation, and an estimate built entirely
/// from such lines is a guess with a spreadsheet's authority.
/// </para>
/// </remarks>
/// <param name="Reference">The line's own identifier within the estimate. Required.</param>
/// <param name="Kind">What part of the job it covers.</param>
/// <param name="Description">What the line is for. Required.</param>
/// <param name="Quantity">How many units the line covers.</param>
/// <param name="UnitCost">The cost of one unit, however well known. Required.</param>
/// <param name="SourcePins">The exact `P03` record revisions the figure came from. Never <see langword="null"/>.</param>
/// <param name="AssumptionReferences">The estimate assumptions this line rests on. Never <see langword="null"/>.</param>
/// <param name="LeadTime">The lead time this line implies. <see langword="null"/> where it implies none.</param>
public sealed record EstimateLine(
    string Reference,
    EstimateLineKind Kind,
    string Description,
    decimal Quantity,
    CostFigure UnitCost,
    IReadOnlyList<ReferencePin>? SourcePins = null,
    IReadOnlyList<string>? AssumptionReferences = null,
    LeadTimeDuration? LeadTime = null)
{
    /// <summary>The line's own identifier within the estimate.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("An estimate line must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What the line is for.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("An estimate line must say what it is for.", nameof(Description))
        : Description.Trim();

    /// <summary>How many units the line covers.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="Quantity"/> is negative.</exception>
    public decimal Quantity { get; } = Quantity < 0m
        ? throw new ArgumentOutOfRangeException(nameof(Quantity), Quantity, "An estimate line cannot cover a negative quantity.")
        : Quantity;

    /// <summary>The cost of one unit.</summary>
    public CostFigure UnitCost { get; } = UnitCost ?? throw new ArgumentNullException(nameof(UnitCost));

    /// <summary>The exact `P03` record revisions the figure came from.</summary>
    public IReadOnlyList<ReferencePin> SourcePins { get; init; } = SourcePins ?? [];

    /// <summary>The estimate assumptions this line rests on.</summary>
    public IReadOnlyList<string> AssumptionReferences { get; init; } = AssumptionReferences ?? [];

    /// <summary>The line total — unit cost times quantity, unknown if the unit cost is.</summary>
    public CostFigure LineTotal => UnitCost * Quantity;

    /// <summary>Whether the figure can be traced to a governed record at a known revision.</summary>
    public bool IsTraceable => SourcePins.Count > 0;

    /// <summary>Whether the line is unpriced, and so makes the whole estimate unknown.</summary>
    public bool IsUnpriced => UnitCost.IsUnknown;
}

/// <summary>
/// The organisation's own view of what a piece of work will cost.
/// </summary>
/// <remarks>
/// <para>
/// <b>An estimate is not a quote.</b> It is derived from reference
/// information the organisation holds; nobody outside has offered
/// anything and nobody is bound. `SupplierQuote` records what a supplier
/// offered, `CustomerQuotation` records what the organisation offered,
/// and neither is this. The three are separate types precisely so that
/// one cannot be read as another (`ADR-0134`).
/// </para>
/// <para>
/// The estimate is a governed record, and its lines pin the exact cost
/// and lead-time revisions they were built from, so a historical estimate
/// keeps saying what it said when it was made even after every source
/// beneath it has been superseded.
/// </para>
/// </remarks>
public sealed record CostEstimate
{
    /// <summary>The reference the estimate is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What is being estimated. Required.</summary>
    public required string Subject { get; init; }

    /// <summary>The currency every figure in the estimate is stated in. Required.</summary>
    public required CurrencyCode Currency { get; init; }

    /// <summary>How many of the subject the estimate covers.</summary>
    public int Quantity { get; init; } = 1;

    /// <summary>The lines. Never <see langword="null"/>.</summary>
    public IReadOnlyList<EstimateLine> Lines { get; init; } = [];

    /// <summary>What the estimate takes to be true. Never <see langword="null"/>.</summary>
    public IReadOnlyList<EstimateAssumption> Assumptions { get; init; } = [];

    /// <summary>Who prepared it. Required in substance — validation reports an unattributed estimate.</summary>
    public string? PreparedByPrincipalId { get; init; }

    /// <summary>When it was prepared.</summary>
    public DateOnly? PreparedOn { get; init; }

    /// <summary>How long the estimate is meant to hold. <see langword="null"/> where nobody said.</summary>
    public EffectivePeriod? Validity { get; init; }

    /// <summary>What the estimate excludes. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Exclusions { get; init; } = [];

    /// <summary>What actually happened, once it has. <see langword="null"/> until then.</summary>
    public RealisedOutcome? Outcome { get; init; }

    /// <summary>Anything else about the estimate. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>
    /// The estimate total.
    /// </summary>
    /// <remarks>
    /// Unknown if any line is unpriced, rather than a total that is
    /// certainly too small. Exact decimal arithmetic throughout.
    /// </remarks>
    /// <exception cref="CurrencyMismatchException">A line is stated in a currency other than the estimate's.</exception>
    public CostFigure Total => CostFigure.Sum(Lines.Select(l => l.LineTotal), Currency);

    /// <summary>The total for one unit of the subject. Unknown if the estimate is.</summary>
    public CostFigure PerUnit => Quantity > 0 ? Total * (1m / Quantity) : CostFigure.Unknown;

    /// <summary>Lines nobody has priced.</summary>
    public IReadOnlyList<EstimateLine> UnpricedLines => Lines.Where(l => l.IsUnpriced).ToList();

    /// <summary>Lines that cannot be traced to a governed record.</summary>
    public IReadOnlyList<EstimateLine> UntraceableLines => Lines.Where(l => !l.IsTraceable).ToList();

    /// <summary>Whether every line can be traced to a governed record at a known revision.</summary>
    public bool IsFullyTraceable => Lines.Count > 0 && Lines.All(l => l.IsTraceable);

    /// <summary>Whether the estimate can produce a total at all.</summary>
    public bool IsPriced => Lines.Count > 0 && UnpricedLines.Count == 0;

    /// <summary>Whether the estimate has run past its own validity as at <paramref name="asAt"/>.</summary>
    public bool IsStaleAt(DateOnly asAt) => Validity?.HasExpiredBy(asAt) ?? false;

    /// <summary>Every record revision the estimate rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        Lines.SelectMany(l => l.SourcePins)
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionNumber)
            .ToList();

    /// <summary>The longest lead time any line implies. <see langword="null"/> where no line implies one.</summary>
    /// <remarks>
    /// Only lines whose lead times are comparable with each other are
    /// considered, and where they are not, the method returns
    /// <see langword="null"/> rather than picking one — a working-day
    /// figure and a calendar-week figure have no longest.
    /// </remarks>
    public LeadTimeDuration? LongestLeadTime
    {
        get
        {
            var stated = Lines.Select(l => l.LeadTime).OfType<LeadTimeDuration>().ToList();

            if (stated.Count == 0)
                return null;

            return stated.All(d => d.IsComparableWith(stated[0]))
                ? stated.OrderByDescending(d => d, Comparer<LeadTimeDuration>.Default).First()
                : null;
        }
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

/// <summary>
/// What a piece of work actually cost and how long it actually took.
/// </summary>
/// <remarks>
/// <b>The fourth of the four things D4 keeps apart.</b> An actual is not
/// an estimate, a supplier quote or a customer quotation; it is the only
/// one of the four that cannot be wrong about what happened. Recorded
/// against the estimate it tests, so that "how good are our estimates?"
/// becomes answerable — which it never is when actuals live in an
/// accounting system and estimates live in a spreadsheet.
/// </remarks>
/// <param name="ActualCost">What was actually paid. Required.</param>
/// <param name="RecordedOn">When the outcome was recorded.</param>
/// <param name="ActualLeadTime">How long it actually took. <see langword="null"/> where not measured.</param>
/// <param name="Evidence">What shows it — an invoice, a delivery note. Never <see langword="null"/>.</param>
/// <param name="Commentary">Why it differed, in the recorder's own words. <see langword="null"/> if nothing.</param>
public sealed record RealisedOutcome(
    Money ActualCost,
    DateOnly RecordedOn,
    LeadTimeDuration? ActualLeadTime = null,
    IReadOnlyList<BusinessEvidence>? Evidence = null,
    string? Commentary = null)
{
    /// <summary>What shows the outcome.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = Evidence ?? [];

    /// <summary>Whether anything at all evidences it.</summary>
    /// <remarks>
    /// An unevidenced actual is somebody's recollection of what was paid,
    /// and it is the one kind of figure that ought never to be.
    /// </remarks>
    public bool IsEvidenced => Evidence.Count > 0;

    /// <summary>
    /// How far the estimate was out, as a proportion of what was
    /// estimated.
    /// </summary>
    /// <remarks>
    /// Positive means the work cost more than estimated.
    /// <see langword="null"/> where the estimate was unpriced, in a
    /// different currency, or zero — a percentage against nothing is not
    /// a number.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="estimated"/> is <see langword="null"/>.</exception>
    public decimal? VarianceFrom(CostFigure estimated)
    {
        ArgumentNullException.ThrowIfNull(estimated);

        if (estimated.IsUnknown || estimated.Lowest!.Value.Currency != ActualCost.Currency)
            return null;

        var baseline = estimated.Lowest!.Value.Amount;

        return baseline == 0m ? null : (ActualCost.Amount - baseline) / baseline;
    }
}
