using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.CommercialIntelligence;

/// <summary>What a lead-time figure is counted in.</summary>
/// <remarks>
/// <b>A working day is not a duration.</b> How much elapsed time five
/// working days represent depends on a calendar, a country, a shift
/// pattern and a shutdown schedule, none of which TempestOS holds. It is
/// therefore kept as its own unit and never converted, for the same
/// reason `ADR-0130` refuses to convert between currencies.
/// </remarks>
public enum LeadTimeUnit
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Elapsed hours.</summary>
    Hour,

    /// <summary>Calendar days, including weekends and holidays.</summary>
    CalendarDay,

    /// <summary>Days the supplier actually works. Not convertible to elapsed time without a calendar.</summary>
    WorkingDay,

    /// <summary>Calendar weeks.</summary>
    Week,

    /// <summary>Calendar months.</summary>
    Month
}

/// <summary>
/// A lead time, in the unit the source stated it in.
/// </summary>
/// <remarks>
/// <para>
/// Structured rather than a string, so that "2–3 weeks" can be compared,
/// filtered and validated. A human-readable note may sit alongside it on
/// the record; it does not replace it.
/// </para>
/// <para>
/// <see cref="ToElapsed"/> converts to a real
/// <see cref="Quantity{TDimension}"/> where the unit is a calendar one
/// and returns <see langword="null"/> for working days — the honest
/// answer, and the one that stops a working-day figure being silently
/// compared against a calendar one.
/// </para>
/// </remarks>
/// <param name="Amount">How many units. Required.</param>
/// <param name="Unit">What the units are.</param>
public sealed record LeadTimeDuration(decimal Amount, LeadTimeUnit Unit) : IComparable<LeadTimeDuration>
{
    /// <summary>How many units.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="Amount"/> is negative.</exception>
    public decimal Amount { get; } = Amount < 0m
        ? throw new ArgumentOutOfRangeException(nameof(Amount), Amount, "A lead time cannot be negative. Work does not arrive before it is ordered.")
        : Amount;

    /// <summary>A lead time in working days.</summary>
    public static LeadTimeDuration WorkingDays(decimal amount) => new(amount, LeadTimeUnit.WorkingDay);

    /// <summary>A lead time in calendar weeks.</summary>
    public static LeadTimeDuration Weeks(decimal amount) => new(amount, LeadTimeUnit.Week);

    /// <summary>A lead time in calendar days.</summary>
    public static LeadTimeDuration CalendarDays(decimal amount) => new(amount, LeadTimeUnit.CalendarDay);

    /// <summary>Whether the unit counts working time rather than elapsed time.</summary>
    public bool IsWorkingTime => Unit == LeadTimeUnit.WorkingDay;

    /// <summary>Whether the figure states a unit at all.</summary>
    public bool IsSpecified => Unit != LeadTimeUnit.Unspecified;

    /// <summary>
    /// The figure as elapsed time, where its unit is a calendar one.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> for <see cref="LeadTimeUnit.WorkingDay"/>
    /// and <see cref="LeadTimeUnit.Unspecified"/>. A month is taken as 30
    /// calendar days for this purpose, which is a stated approximation
    /// rather than a fact about any particular month, and is why a month
    /// is a poor unit to record a lead time in.
    /// </remarks>
    public Quantity<Duration>? ToElapsed() => Unit switch
    {
        LeadTimeUnit.Hour => new Quantity<Duration>((double)Amount, DurationUnits.Hour),
        LeadTimeUnit.CalendarDay => new Quantity<Duration>((double)Amount, DurationUnits.Day),
        LeadTimeUnit.Week => new Quantity<Duration>((double)Amount, DurationUnits.Week),
        LeadTimeUnit.Month => new Quantity<Duration>((double)Amount * 30.0, DurationUnits.Day),
        _ => null,
    };

    /// <summary>
    /// Whether this figure can be compared against <paramref name="other"/>
    /// at all.
    /// </summary>
    /// <remarks>
    /// Two calendar-based figures are comparable however they are
    /// expressed. Two working-day figures are comparable with each other.
    /// A working-day figure and a calendar figure are not, and this
    /// returns <see langword="false"/> rather than guessing a five-day
    /// week.
    /// </remarks>
    public bool IsComparableWith(LeadTimeDuration? other) =>
        other is not null
        && IsSpecified
        && other.IsSpecified
        && IsWorkingTime == other.IsWorkingTime;

    /// <summary>
    /// Orders two lead times, shortest first.
    /// </summary>
    /// <exception cref="ArgumentException">The two figures are not comparable — one counts working time and the other elapsed time.</exception>
    public int CompareTo(LeadTimeDuration? other)
    {
        if (other is null)
            return 1;

        if (!IsComparableWith(other))
            throw new ArgumentException(
                $"A lead time in {Unit} cannot be compared with one in {other.Unit}. Converting working days to elapsed time "
                + "needs a calendar, a country and a shift pattern, none of which TempestOS holds.",
                nameof(other));

        return IsWorkingTime
            ? Amount.CompareTo(other.Amount)
            : ToElapsed()!.Value.ConvertTo(DurationUnits.Day).Value
                .CompareTo(other.ToElapsed()!.Value.ConvertTo(DurationUnits.Day).Value);
    }

    /// <inheritdoc />
    public override string ToString() => Unit switch
    {
        LeadTimeUnit.Unspecified => $"{Amount} (unit not stated)",
        LeadTimeUnit.WorkingDay => $"{Amount} working day(s)",
        LeadTimeUnit.CalendarDay => $"{Amount} calendar day(s)",
        _ => $"{Amount} {Unit.ToString().ToLowerInvariant()}(s)",
    };
}

/// <summary>
/// Where a lead-time figure came from, and therefore what it commits
/// anybody to.
/// </summary>
/// <remarks>
/// <b>These must never collapse into one number.</b> A historical average
/// is not a supplier commitment. A quotation is not a delivery. An
/// estimate is nobody's promise. A commercial library that records "lead
/// time: 6 weeks" without saying which of these it is has recorded
/// something nobody can act on — and, worse, something a reader will
/// assume is the strongest of the five.
/// </remarks>
public enum LeadTimeKind
{
    /// <summary>Not stated. Reported, never assumed to be any of the others.</summary>
    Unspecified,

    /// <summary>TempestOS's own working figure, derived rather than sourced.</summary>
    Estimated,

    /// <summary>What a supplier publishes or generally says. Not offered against a specific order.</summary>
    Typical,

    /// <summary>What actually happened, averaged or observed across past orders.</summary>
    Historical,

    /// <summary>What a supplier offered in a specific quotation. Binding only within that quotation's own validity.</summary>
    Quoted,

    /// <summary>What a supplier contractually undertook to deliver in. The only kind that binds them.</summary>
    Committed,

    /// <summary>What one specific order actually took, measured.</summary>
    Actual
}

/// <summary>Reasoning over <see cref="LeadTimeKind"/>.</summary>
public static class LeadTimeKinds
{
    /// <summary>Every kind, weakest claim first.</summary>
    public static IReadOnlyList<LeadTimeKind> WeakestFirst { get; } =
    [
        LeadTimeKind.Unspecified, LeadTimeKind.Estimated, LeadTimeKind.Typical,
        LeadTimeKind.Historical, LeadTimeKind.Quoted, LeadTimeKind.Committed, LeadTimeKind.Actual,
    ];

    /// <summary>Whether the supplier is actually bound by the figure.</summary>
    /// <remarks>True for <see cref="LeadTimeKind.Committed"/> alone. A quotation binds only within its own validity, which the record carries separately.</remarks>
    public static bool IsSupplierCommitment(LeadTimeKind kind) => kind == LeadTimeKind.Committed;

    /// <summary>Whether the figure records something that happened rather than something expected.</summary>
    public static bool IsObserved(LeadTimeKind kind) => kind is LeadTimeKind.Actual or LeadTimeKind.Historical;

    /// <summary>Whether the figure is somebody's expectation rather than evidence or a promise.</summary>
    public static bool IsExpectation(LeadTimeKind kind) =>
        kind is LeadTimeKind.Estimated or LeadTimeKind.Typical or LeadTimeKind.Unspecified;

    /// <summary>
    /// How strong a claim the kind makes, for ordering. Higher is
    /// stronger.
    /// </summary>
    /// <remarks>
    /// <see cref="LeadTimeKind.Actual"/> ranks highest because a measured
    /// outcome is the only figure that cannot be wrong about what
    /// happened — though it says nothing on its own about what will
    /// happen next time, which is why the weaker kinds are still worth
    /// reading.
    /// </remarks>
    public static int Strength(LeadTimeKind kind) => kind switch
    {
        LeadTimeKind.Unspecified => 0,
        LeadTimeKind.Estimated => 1,
        LeadTimeKind.Typical => 2,
        LeadTimeKind.Historical => 3,
        LeadTimeKind.Quoted => 4,
        LeadTimeKind.Committed => 5,
        LeadTimeKind.Actual => 6,
        _ => 0,
    };
}
