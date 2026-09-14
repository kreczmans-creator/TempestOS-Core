namespace Tempest.Core.BusinessGovernance;

/// <summary>
/// How firmly a business fact is established.
/// </summary>
/// <remarks>
/// <para>
/// The `P07` counterpart of `P02`'s eight-valued
/// <see cref="Tempest.Core.EngineeringIntelligence.AssessmentOutcome"/>,
/// and it exists for the same reason: a Boolean would let "nobody has
/// looked at this" read identically to "this was checked and is fine".
/// </para>
/// <para>
/// Business governance needs its own vocabulary rather than reusing the
/// engineering one, because the states differ. An engineering rule can
/// fail; a business fact is more often simply undetermined, disputed, or
/// waiting on somebody whose profession it is to determine it. That last
/// state — <see cref="ReviewRequired"/> — is the one that keeps this
/// platform out of legal and accounting practice: where a determination
/// belongs to a solicitor or an accountant, TempestOS records that it
/// belongs to them.
/// </para>
/// </remarks>
public enum DeterminationState
{
    /// <summary>Nobody has established this, and nothing is recorded. The honest default.</summary>
    NotDetermined,

    /// <summary>Established, and the record carries what establishes it.</summary>
    Recorded,

    /// <summary>Something is recorded, but it is a working assumption rather than an established fact.</summary>
    Assumed,

    /// <summary>
    /// The determination belongs to somebody outside this system — a
    /// solicitor, an accountant, an insurer, a client. TempestOS records
    /// that the question is open and whose it is; it does not answer it.
    /// </summary>
    ReviewRequired,

    /// <summary>The question genuinely does not arise for this record.</summary>
    NotApplicable,

    /// <summary>Two sources disagree, and the disagreement is itself the recorded state.</summary>
    Disputed
}

/// <summary>Reasoning over <see cref="DeterminationState"/>.</summary>
public static class DeterminationStates
{
    /// <summary>Every state, in the order a report should present them.</summary>
    public static IReadOnlyList<DeterminationState> All { get; } =
    [
        DeterminationState.Recorded,
        DeterminationState.Assumed,
        DeterminationState.NotApplicable,
        DeterminationState.NotDetermined,
        DeterminationState.ReviewRequired,
        DeterminationState.Disputed,
    ];

    /// <summary>
    /// Whether the fact is established well enough to be relied on.
    /// </summary>
    /// <remarks>
    /// True for <see cref="DeterminationState.Recorded"/> alone. An
    /// assumption is not a fact, and an open question is not an answer.
    /// </remarks>
    public static bool IsEstablished(DeterminationState state) => state == DeterminationState.Recorded;

    /// <summary>Whether the record is waiting on somebody.</summary>
    public static bool IsOutstanding(DeterminationState state) =>
        state is DeterminationState.NotDetermined or DeterminationState.ReviewRequired or DeterminationState.Disputed;

    /// <summary>
    /// The state of a set of determinations taken together — the weakest
    /// one, because a set is only as established as its least-established
    /// member.
    /// </summary>
    /// <remarks>
    /// An empty set is <see cref="DeterminationState.NotDetermined"/>:
    /// nothing was determined, which is exactly what an empty set means.
    /// A set in which everything is <see cref="DeterminationState.NotApplicable"/>
    /// stays that way rather than degrading.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="states"/> is <see langword="null"/>.</exception>
    public static DeterminationState Weakest(IEnumerable<DeterminationState> states)
    {
        ArgumentNullException.ThrowIfNull(states);

        var seen = false;
        var worst = DeterminationState.NotApplicable;

        foreach (var state in states)
        {
            seen = true;

            if (Rank(state) > Rank(worst))
                worst = state;
        }

        return seen ? worst : DeterminationState.NotDetermined;
    }

    /// <summary>How adverse a state is, for aggregation. Higher wins.</summary>
    public static int Rank(DeterminationState state) => state switch
    {
        DeterminationState.NotApplicable => 0,
        DeterminationState.Recorded => 1,
        DeterminationState.Assumed => 2,
        DeterminationState.NotDetermined => 3,
        DeterminationState.ReviewRequired => 4,
        DeterminationState.Disputed => 5,
        _ => 3,
    };
}
