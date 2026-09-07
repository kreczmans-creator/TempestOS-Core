namespace Tempest.Core.BusinessGovernance;

/// <summary>
/// The window during which a business record is in force.
/// </summary>
/// <remarks>
/// <para>
/// Almost every `P07` record has one: a contract runs from execution to
/// expiry, a rate card applies from a date until it is replaced, an
/// insurance policy covers a period and stops, a forecast describes a
/// period. Modelling it once means "is this in force today?" and "do
/// these two overlap?" mean the same thing in all seven work packages.
/// </para>
/// <para>
/// The end is <see langword="null"/> for an open-ended period, which is
/// not the same as an unknown one. Where an end date exists but has not
/// been recorded, that is a gap in the record and the record should say
/// so — an open-ended rate card and a rate card whose expiry nobody
/// captured are different situations, and silently treating the second as
/// the first is how an expired policy comes to look current.
/// </para>
/// <para>
/// Dates are <see cref="DateOnly"/> because business effectivity is
/// day-grained: a policy that incepts on the 1st does so at whatever hour
/// its own wording says, and a timestamp here would imply a precision the
/// underlying documents do not have.
/// </para>
/// </remarks>
/// <param name="From">The first day the record is in force. Required.</param>
/// <param name="To">The last day it is in force, inclusive. <see langword="null"/> for open-ended.</param>
public sealed record EffectivePeriod(DateOnly From, DateOnly? To)
{
    /// <summary>The last day the record is in force, inclusive.</summary>
    /// <exception cref="ArgumentException"><paramref name="To"/> is before <paramref name="From"/>.</exception>
    public DateOnly? To { get; } = To is { } end && end < From
        ? throw new ArgumentException($"An effective period cannot end ({end:O}) before it starts ({From:O}).", nameof(To))
        : To;

    /// <summary>Whether the period has no recorded end.</summary>
    public bool IsOpenEnded => To is null;

    /// <summary>An open-ended period starting on <paramref name="from"/>.</summary>
    public static EffectivePeriod From_(DateOnly from) => new(from, null);

    /// <summary>Whether <paramref name="date"/> falls within the period, inclusive of both ends.</summary>
    public bool Contains(DateOnly date) => date >= From && (To is not { } end || date <= end);

    /// <summary>Whether the period has ended before <paramref name="asAt"/>.</summary>
    public bool HasExpiredBy(DateOnly asAt) => To is { } end && end < asAt;

    /// <summary>Whether the period has not yet started as at <paramref name="asAt"/>.</summary>
    public bool StartsAfter(DateOnly asAt) => From > asAt;

    /// <summary>
    /// Whether two periods share at least one day.
    /// </summary>
    /// <remarks>
    /// Used to detect the case a rate card must never be in: two revisions
    /// both claiming to be the applicable price on the same day.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public bool Overlaps(EffectivePeriod other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var startsBeforeOtherEnds = other.To is not { } otherEnd || From <= otherEnd;
        var endsAfterOtherStarts = To is not { } end || end >= other.From;

        return startsBeforeOtherEnds && endsAfterOtherStarts;
    }

    /// <summary>How many days the period covers, or <see langword="null"/> where it is open-ended.</summary>
    public int? DayCount => To is { } end ? end.DayNumber - From.DayNumber + 1 : null;

    /// <inheritdoc />
    public override string ToString() => To is { } end ? $"{From:O} to {end:O}" : $"{From:O} onwards";
}
