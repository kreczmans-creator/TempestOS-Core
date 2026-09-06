using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Finance;

/// <summary>
/// What a financial figure actually is.
/// </summary>
/// <remarks>
/// <para>
/// <b>The single most important distinction in C5.</b> An actual happened.
/// A forecast is somebody's expectation. A budget is a limit somebody set.
/// An assumption is an input to a forecast that nobody has established.
/// Presenting all four as "the numbers" is how an organisation comes to
/// believe it has money it has only projected.
/// </para>
/// <para>
/// <see cref="Actual"/> is also the one kind `P07` cannot originate. An
/// actual comes from an accounting system, a bank statement or an invoice;
/// TempestOS records it with its source and does not compute it.
/// </para>
/// </remarks>
public enum FinancialFigureKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Something that happened, taken from an accounting record.</summary>
    Actual,

    /// <summary>A limit somebody set in advance.</summary>
    Budget,

    /// <summary>An expectation about a future period.</summary>
    Forecast,

    /// <summary>A commitment already made but not yet paid or invoiced.</summary>
    Committed,

    /// <summary>A figure carried forward from a previous plan for comparison.</summary>
    Baseline
}

/// <summary>What a financial figure is about.</summary>
public enum FinancialCategory
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Money coming in from client work.</summary>
    Revenue,

    /// <summary>The cost of delivering that work.</summary>
    CostOfDelivery,

    /// <summary>People — salaries, contractors, employer costs.</summary>
    StaffCost,

    /// <summary>Premises, insurance, software, professional fees and the rest of the standing cost base.</summary>
    Overhead,

    /// <summary>Equipment and other capital items.</summary>
    CapitalExpenditure,

    /// <summary>Travel and disbursements.</summary>
    Expenses,

    /// <summary>Money set aside for a known future obligation, tax among them.</summary>
    Provision,

    /// <summary>Cash held.</summary>
    CashPosition,

    /// <summary>Something else, described in the line.</summary>
    Other
}

/// <summary>
/// A period a financial figure describes.
/// </summary>
/// <remarks>
/// A named period rather than a pair of dates, because financial
/// comparison is between like periods and a label is what makes the
/// comparison legible. <see cref="Period"/> carries the dates, so a
/// figure can still be placed on a timeline.
/// </remarks>
/// <param name="Label">What the period is called — "FY26 Q1", "2026-04". Required.</param>
/// <param name="Period">The days it covers. Required.</param>
public sealed record FinancialPeriod(string Label, EffectivePeriod Period) : IComparable<FinancialPeriod>
{
    /// <summary>What the period is called.</summary>
    public string Label { get; } = string.IsNullOrWhiteSpace(Label)
        ? throw new ArgumentException("A financial period must be named, or figures cannot be compared like for like.", nameof(Label))
        : Label.Trim();

    /// <summary>The days it covers.</summary>
    /// <exception cref="ArgumentException"><paramref name="Period"/> is open-ended.</exception>
    public EffectivePeriod Period { get; } = Period is null
        ? throw new ArgumentNullException(nameof(Period))
        : Period.IsOpenEnded
            ? throw new ArgumentException(
                $"Financial period '{Label}' has no end date. An unbounded period cannot be totalled, compared or closed.",
                nameof(Period))
            : Period;

    /// <summary>Whether <paramref name="date"/> falls in the period.</summary>
    public bool Contains(DateOnly date) => Period.Contains(date);

    /// <summary>Whether the period has finished as at <paramref name="asAt"/>, and so should have actuals rather than forecasts.</summary>
    public bool HasClosedBy(DateOnly asAt) => Period.HasExpiredBy(asAt);

    /// <inheritdoc />
    public int CompareTo(FinancialPeriod? other) =>
        other is null ? 1 : Period.From.CompareTo(other.Period.From);

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>
/// Something a forecast rests on that nobody has established.
/// </summary>
/// <remarks>
/// <para>
/// Assumptions are first-class here for the same reason they are in a
/// trade study: a forecast that turns out wrong is usually a forecast
/// whose assumption turned out wrong, and unless the assumption was
/// written down nobody can say which one.
/// </para>
/// <para>
/// An assumption is a governed record in its own right, so that changing
/// it produces a new revision rather than quietly altering every forecast
/// that ever relied on it.
/// </para>
/// </remarks>
public sealed record FinancialAssumption
{
    /// <summary>The reference the assumption is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What is being assumed. Required.</summary>
    public required string Statement { get; init; }

    /// <summary>The governance every `P07` record carries. Required.</summary>
    public required BusinessGovernanceFacts Governance { get; init; }

    /// <summary>The value assumed, where the assumption is a figure. <see langword="null"/> where it is qualitative.</summary>
    public Money? AssumedAmount { get; init; }

    /// <summary>The value assumed, where it is a rate, a count or a proportion. <see langword="null"/> otherwise.</summary>
    public decimal? AssumedValue { get; init; }

    /// <summary>What the value is measured in — "days per month", "per cent", "engagements". <see langword="null"/> where it is a money figure.</summary>
    public string? Unit { get; init; }

    /// <summary>How firmly the assumption is established.</summary>
    public DeterminationState State { get; init; } = DeterminationState.Assumed;

    /// <summary>Where it came from — a signed contract, last year's actuals, somebody's judgement. Required in substance.</summary>
    public string? Source { get; init; }

    /// <summary>What would no longer hold if the assumption were wrong. <see langword="null"/> if not stated.</summary>
    public string? WouldInvalidate { get; init; }

    /// <summary>The period the assumption applies to. <see langword="null"/> where it applies generally.</summary>
    public EffectivePeriod? AppliesOver { get; init; }

    /// <summary>Whether the assumption rests on something stated.</summary>
    public bool HasStatedSource => !string.IsNullOrWhiteSpace(Source);

    /// <summary>Whether the assumption has been established rather than merely assumed.</summary>
    public bool IsEstablished => DeterminationStates.IsEstablished(State);

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
/// One figure, for one category, in one period.
/// </summary>
/// <param name="Category">What the figure is about.</param>
/// <param name="Kind">What the figure actually is — an actual, a forecast, a budget.</param>
/// <param name="Amount">The figure. Required.</param>
/// <param name="Description">What it covers. Required.</param>
/// <param name="Source">Where it came from. Required for an actual; strongly expected for anything else.</param>
/// <param name="AssumptionReferences">The assumptions it rests on. Never <see langword="null"/>.</param>
/// <param name="Evidence">What supports it. Never <see langword="null"/>.</param>
public sealed record FinancialLine(
    FinancialCategory Category,
    FinancialFigureKind Kind,
    Money Amount,
    string Description,
    string? Source = null,
    IReadOnlyList<string>? AssumptionReferences = null,
    IReadOnlyList<BusinessEvidence>? Evidence = null)
{
    /// <summary>What the figure covers.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A financial line must say what it covers.", nameof(Description))
        : Description.Trim();

    /// <summary>The assumptions it rests on.</summary>
    public IReadOnlyList<string> AssumptionReferences { get; init; } = AssumptionReferences ?? [];

    /// <summary>What supports it.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = Evidence ?? [];

    /// <summary>Whether the figure says where it came from.</summary>
    public bool HasStatedSource => !string.IsNullOrWhiteSpace(Source);

    /// <summary>
    /// Whether the line is an actual with nothing behind it.
    /// </summary>
    /// <remarks>
    /// An actual is a fact from somewhere else. One with no source and no
    /// evidence is a forecast that has been relabelled, and that is the
    /// error this property exists to catch.
    /// </remarks>
    public bool IsUnsupportedActual => Kind == FinancialFigureKind.Actual && !HasStatedSource && Evidence.Count == 0;

    /// <summary>Whether the line increases what the organisation has.</summary>
    public bool IsInflow => Category is FinancialCategory.Revenue or FinancialCategory.CashPosition;
}
