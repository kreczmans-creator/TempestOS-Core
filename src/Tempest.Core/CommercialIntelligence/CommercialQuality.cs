namespace Tempest.Core.CommercialIntelligence;

/// <summary>
/// How much weight a commercial record can bear.
/// </summary>
/// <remarks>
/// <para>
/// Commercial intelligence fails differently from engineering reference
/// data. A material's yield strength is either recorded correctly or it
/// is not. A cost can be recorded perfectly and still be worthless —
/// because it is four years old, because nobody wrote down what quantity
/// it applied to, or because two sources disagree. None of those is a
/// malformed record, and a Boolean "valid" would report all three as
/// fine.
/// </para>
/// <para>
/// The states are therefore about <i>usability</i>, not correctness, and
/// they are ordered: a record can only be as good as its weakest
/// attribute.
/// </para>
/// </remarks>
public enum CommercialQuality
{
    /// <summary>
    /// The record is malformed and cannot be used at all — a cost with no
    /// currency, a lead time of minus three days. A defect, not a
    /// limitation.
    /// </summary>
    Invalid,

    /// <summary>
    /// Something a reader needs is missing — the quantity the price
    /// applied to, the supplier it came from, the date it was observed.
    /// The record may well be true; nobody can tell what it is true of.
    /// </summary>
    Incomplete,

    /// <summary>
    /// Complete, and nobody has checked it against its source. The
    /// ordinary state of an imported or transcribed figure.
    /// </summary>
    Unverified,

    /// <summary>
    /// Complete and checked against its source, but its own validity
    /// period has passed. Still evidence of what was true then; not
    /// evidence of what is true now.
    /// </summary>
    Stale,

    /// <summary>
    /// Complete, checked, and within its validity period. The only state
    /// a current commercial decision should rest on.
    /// </summary>
    Verified,

    /// <summary>
    /// The record does not apply to the question being asked — a cost for
    /// a different process, a lead time for a different region. Not a
    /// defect, and not usable here.
    /// </summary>
    NotApplicable,

    /// <summary>
    /// Another record of at least equal standing says something
    /// different. The disagreement is itself the finding, and resolving it
    /// needs a person.
    /// </summary>
    Contradicted
}

/// <summary>Reasoning over <see cref="CommercialQuality"/>.</summary>
public static class CommercialQualities
{
    /// <summary>Every state, in the order a report should present them — worst first.</summary>
    public static IReadOnlyList<CommercialQuality> WorstFirst { get; } =
    [
        CommercialQuality.Invalid,
        CommercialQuality.Contradicted,
        CommercialQuality.Incomplete,
        CommercialQuality.Stale,
        CommercialQuality.Unverified,
        CommercialQuality.Verified,
        CommercialQuality.NotApplicable,
    ];

    /// <summary>
    /// Whether a current commercial decision may rest on the record.
    /// </summary>
    /// <remarks>
    /// True for <see cref="CommercialQuality.Verified"/> alone.
    /// <see cref="CommercialQuality.Unverified"/> is deliberately excluded:
    /// most commercial data starts there, and a system that treated it as
    /// decision-grade would make the verification step pointless.
    /// </remarks>
    public static bool IsDecisionGrade(CommercialQuality quality) => quality == CommercialQuality.Verified;

    /// <summary>Whether the record can be used at all, for anything.</summary>
    public static bool IsUsable(CommercialQuality quality) =>
        quality is CommercialQuality.Verified or CommercialQuality.Unverified or CommercialQuality.Stale;

    /// <summary>Whether the record needs somebody's attention before it can be relied on.</summary>
    public static bool NeedsAttention(CommercialQuality quality) =>
        quality is CommercialQuality.Invalid or CommercialQuality.Incomplete or CommercialQuality.Contradicted;

    /// <summary>
    /// How poor a state is, for ordering and aggregation. Higher is worse.
    /// </summary>
    /// <remarks>
    /// <see cref="CommercialQuality.NotApplicable"/> ranks lowest because
    /// it is not a defect: a record that does not apply is correctly
    /// excluded rather than counted against the set it was never part of.
    /// </remarks>
    public static int Rank(CommercialQuality quality) => quality switch
    {
        CommercialQuality.NotApplicable => 0,
        CommercialQuality.Verified => 1,
        CommercialQuality.Unverified => 2,
        CommercialQuality.Stale => 3,
        CommercialQuality.Incomplete => 4,
        CommercialQuality.Contradicted => 5,
        CommercialQuality.Invalid => 6,
        _ => 4,
    };

    /// <summary>
    /// The quality of a set of records taken together — the worst of
    /// them.
    /// </summary>
    /// <remarks>
    /// An estimate is only as sound as its weakest input, and an empty set
    /// is <see cref="CommercialQuality.Incomplete"/>: nothing supports the
    /// answer, which is a gap rather than a clean sheet.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="qualities"/> is <see langword="null"/>.</exception>
    public static CommercialQuality Weakest(IEnumerable<CommercialQuality> qualities)
    {
        ArgumentNullException.ThrowIfNull(qualities);

        var seen = false;
        var worst = CommercialQuality.NotApplicable;

        foreach (var quality in qualities)
        {
            seen = true;

            if (Rank(quality) > Rank(worst))
                worst = quality;
        }

        return seen ? worst : CommercialQuality.Incomplete;
    }
}
