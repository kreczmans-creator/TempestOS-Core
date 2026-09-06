using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Finance;

/// <summary>
/// One named set of financial expectations, over a set of periods, resting
/// on a stated set of assumptions.
/// </summary>
/// <remarks>
/// <para>
/// Scenarios are how a business plans without pretending to know. The
/// conservative case and the stretch case are both legitimate views of the
/// same future; what is illegitimate is presenting either as the position.
/// A scenario carries its own name and its own assumptions so that a
/// reader always knows which future they are looking at.
/// </para>
/// <para>
/// A scenario is a governed record, so revising it produces a new revision
/// and a released one cannot be edited in place. A change in an assumption
/// therefore cannot silently rewrite a forecast somebody already acted on.
/// </para>
/// </remarks>
public sealed record FinancialScenario
{
    /// <summary>The reference the scenario is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What the scenario is called — "Planning case", "Conservative", "Two-hire scale case". Required.</summary>
    public required string Name { get; init; }

    /// <summary>What view of the future it represents, and why anybody would look at it. Required.</summary>
    public required string Purpose { get; init; }

    /// <summary>The currency every figure in the scenario is stated in. Required.</summary>
    public required CurrencyCode Currency { get; init; }

    /// <summary>The governance every `P07` record carries. Required.</summary>
    public required BusinessGovernanceFacts Governance { get; init; }

    /// <summary>The periods the scenario covers, in order. Never <see langword="null"/>.</summary>
    public IReadOnlyList<FinancialPeriod> Periods { get; init; } = [];

    /// <summary>The figures, keyed by the period label they belong to. Never <see langword="null"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<FinancialLine>> LinesByPeriod { get; init; } =
        new Dictionary<string, IReadOnlyList<FinancialLine>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The assumptions the scenario rests on, by reference. Never <see langword="null"/>.</summary>
    /// <remarks>
    /// References rather than copies, so that an assumption revised in one
    /// place is revised for every scenario that names it — and so that a
    /// scenario pinned to an assumption revision keeps the value it was
    /// built on.
    /// </remarks>
    public IReadOnlyList<string> AssumptionReferences { get; init; } = [];

    /// <summary>The exact assumption revisions the scenario was built on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AssumptionPins { get; init; } = [];

    /// <summary>Whether this is the scenario the organisation is actually planning against.</summary>
    /// <remarks>
    /// A statement of intent that needs a person behind it: a planning case
    /// is what budgets and hiring decisions get made against, so it is
    /// marked by <see cref="BusinessAuthorityKind.InternalApproval"/> on
    /// the record rather than by a flag anybody can set.
    /// </remarks>
    public bool IsPlanningCase => Governance.HasAuthority(BusinessAuthorityKind.InternalApproval);

    /// <summary>Anything else about the scenario. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The figures for <paramref name="periodLabel"/>. Empty where the scenario has none.</summary>
    public IReadOnlyList<FinancialLine> LinesFor(string periodLabel) =>
        LinesByPeriod.TryGetValue(periodLabel, out var lines) ? lines : [];

    /// <summary>
    /// The total of every line of <paramref name="kind"/> in
    /// <paramref name="periodLabel"/> for <paramref name="category"/>.
    /// </summary>
    /// <remarks>
    /// Exact decimal arithmetic, and deterministic: the same scenario
    /// revision always totals the same. Currency mismatches throw rather
    /// than converting.
    /// </remarks>
    /// <exception cref="CurrencyMismatchException">A line is stated in a currency other than the scenario's.</exception>
    public Money Total(string periodLabel, FinancialCategory category, FinancialFigureKind kind) =>
        Money.Sum(
            LinesFor(periodLabel).Where(l => l.Category == category && l.Kind == kind).Select(l => l.Amount),
            Currency);

    /// <summary>The total of every line of <paramref name="kind"/> in <paramref name="periodLabel"/>, whatever its category.</summary>
    /// <exception cref="CurrencyMismatchException">A line is stated in a currency other than the scenario's.</exception>
    public Money TotalOfKind(string periodLabel, FinancialFigureKind kind) =>
        Money.Sum(LinesFor(periodLabel).Where(l => l.Kind == kind).Select(l => l.Amount), Currency);

    /// <summary>
    /// Revenue less every cost category, for <paramref name="periodLabel"/>
    /// and <paramref name="kind"/>.
    /// </summary>
    /// <remarks>
    /// A planning figure, not an accounting one. It totals the categories
    /// this scenario happens to carry; it applies no accounting standard,
    /// recognises nothing, and is not a profit figure anybody should file.
    /// </remarks>
    /// <exception cref="CurrencyMismatchException">A line is stated in a currency other than the scenario's.</exception>
    public Money IndicativeMargin(string periodLabel, FinancialFigureKind kind)
    {
        var lines = LinesFor(periodLabel).Where(l => l.Kind == kind).ToList();
        var revenue = Money.Sum(lines.Where(l => l.Category == FinancialCategory.Revenue).Select(l => l.Amount), Currency);
        var costs = Money.Sum(
            lines.Where(l => l.Category is FinancialCategory.CostOfDelivery or FinancialCategory.StaffCost
                or FinancialCategory.Overhead or FinancialCategory.Expenses).Select(l => l.Amount),
            Currency);

        return revenue - costs;
    }

    /// <summary>Whether every figure in the scenario is stated in the scenario's own currency.</summary>
    public bool IsCurrencyConsistent =>
        LinesByPeriod.Values.SelectMany(l => l).All(l => l.Amount.Currency == Currency);

    /// <summary>Whether every period the lines are keyed to is actually declared.</summary>
    public bool ArePeriodsDeclared =>
        LinesByPeriod.Keys.All(k => Periods.Any(p => string.Equals(p.Label, k, StringComparison.OrdinalIgnoreCase)));

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
/// The difference between what was expected and what happened, for one
/// category in one period.
/// </summary>
/// <remarks>
/// Computed, never stored: a variance derived from the figures cannot
/// drift away from them. The comparison is refused rather than converted
/// where the two figures are in different currencies.
/// </remarks>
/// <param name="PeriodLabel">The period compared.</param>
/// <param name="Category">The category compared.</param>
/// <param name="Expected">What was expected — the forecast or budget.</param>
/// <param name="ExpectedKind">Which of those it was.</param>
/// <param name="Actual">What happened.</param>
/// <param name="Variance">Actual less expected. Positive means more than expected, whatever that means for the category.</param>
public sealed record FinancialVariance(
    string PeriodLabel,
    FinancialCategory Category,
    Money Expected,
    FinancialFigureKind ExpectedKind,
    Money Actual,
    Money Variance)
{
    /// <summary>
    /// The variance as a proportion of what was expected.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> where the expectation was zero: a percentage
    /// against nothing is not a number, and reporting "infinite" or
    /// "100 per cent" would be worse than reporting nothing.
    /// </remarks>
    public decimal? VarianceProportion => Expected.IsZero ? null : Variance.Amount / Expected.Amount;

    /// <summary>
    /// Whether the variance is bad for the organisation.
    /// </summary>
    /// <remarks>
    /// Direction depends on the category: less revenue than expected is
    /// adverse, and so is more cost. Treating every negative number as bad
    /// is how an under-spend gets reported as a problem.
    /// </remarks>
    public bool IsAdverse => Category switch
    {
        FinancialCategory.Revenue or FinancialCategory.CashPosition => Variance.Amount < 0m,
        _ => Variance.Amount > 0m,
    };

    /// <summary>Whether the variance exceeds <paramref name="threshold"/> of the expectation, either way.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="threshold"/> is negative.</exception>
    public bool ExceedsProportion(decimal threshold)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);

        return VarianceProportion is { } proportion && Math.Abs(proportion) > threshold;
    }
}
