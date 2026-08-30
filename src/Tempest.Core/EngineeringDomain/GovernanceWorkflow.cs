namespace Tempest.Core.EngineeringDomain;

/// <summary>Where a risk stands.</summary>
/// <remarks>
/// <para>
/// A family-specific state in the sense the platform already defines
/// (<see cref="IFamilySpecificState"/>), for the same reason
/// <see cref="TaskWorkState"/> is one: the canonical lifecycle is a
/// <em>document release</em> lifecycle, and a risk does not get released.
/// Every value maps to a canonical equivalent through
/// <see cref="RiskStatuses.For"/>, so anything reasoning across the whole
/// domain still gets one answer per Kind.
/// </para>
/// <para>
/// <b><see cref="Accepted"/> is not <see cref="Closed"/>.</b> An accepted
/// risk is still live — the team has decided to carry it rather than spend
/// on mitigating it, and it must keep appearing on the register. Collapsing
/// the two would quietly delete exactly the risks a reviewer most needs to
/// see, which is the failure mode a risk register exists to prevent.
/// </para>
/// </remarks>
public enum RiskStatus
{
    /// <summary>Identified, nothing done about it yet.</summary>
    Open,

    /// <summary>Being actively worked down.</summary>
    Mitigating,

    /// <summary>Consciously carried rather than mitigated. Still live.</summary>
    Accepted,

    /// <summary>No longer a risk — it went away, or it happened and is now an issue.</summary>
    Closed,
}

/// <summary>Where an issue stands.</summary>
/// <remarks>
/// Deliberately close to <see cref="TaskWorkState"/> without being it. An
/// issue is something that has gone wrong and is being dealt with, not a
/// planned piece of work: it has no <c>Blocked</c>, because an issue nobody
/// can progress is still an open issue, and it distinguishes
/// <see cref="Resolved"/> from <see cref="Closed"/>, because "we fixed it"
/// and "we agree it is finished" are different moments and reviewers care
/// about the gap between them.
/// </remarks>
public enum IssueStatus
{
    /// <summary>Raised, not yet being worked.</summary>
    Open,

    /// <summary>Being worked on now.</summary>
    Investigating,

    /// <summary>Fixed, awaiting confirmation.</summary>
    Resolved,

    /// <summary>Confirmed finished.</summary>
    Closed,
}

/// <summary>Where a decision stands.</summary>
/// <remarks>
/// <para>
/// This is the one of the three that maps almost exactly onto the canonical
/// lifecycle, and the mapping is used rather than worked around:
/// <see cref="Proposed"/> is genuinely <see cref="LifecycleState.InReview"/>,
/// <see cref="Accepted"/> is <see cref="LifecycleState.Approved"/>, and
/// <see cref="Superseded"/> is <see cref="LifecycleState.Superseded"/>.
/// </para>
/// <para>
/// A superseded decision is kept, never deleted. The record of what was
/// decided, and later un-decided, is the point of recording decisions at
/// all.
/// </para>
/// </remarks>
public enum DecisionStatus
{
    /// <summary>Put forward, not yet decided.</summary>
    Proposed,

    /// <summary>Decided, and in force.</summary>
    Accepted,

    /// <summary>Considered and turned down.</summary>
    Rejected,

    /// <summary>Was in force, replaced by a later decision.</summary>
    Superseded,
}

/// <summary>One risk status, and the canonical lifecycle state it corresponds to.</summary>
/// <param name="Status">The risk-family state.</param>
/// <param name="Name">Its display name.</param>
/// <param name="CanonicalEquivalent">The platform-wide <see cref="LifecycleState"/> it maps to.</param>
public sealed record RiskStatusDescriptor(RiskStatus Status, string Name, LifecycleState CanonicalEquivalent)
    : IFamilySpecificState;

/// <summary>One issue status, and the canonical lifecycle state it corresponds to.</summary>
/// <param name="Status">The issue-family state.</param>
/// <param name="Name">Its display name.</param>
/// <param name="CanonicalEquivalent">The platform-wide <see cref="LifecycleState"/> it maps to.</param>
public sealed record IssueStatusDescriptor(IssueStatus Status, string Name, LifecycleState CanonicalEquivalent)
    : IFamilySpecificState;

/// <summary>One decision status, and the canonical lifecycle state it corresponds to.</summary>
/// <param name="Status">The decision-family state.</param>
/// <param name="Name">Its display name.</param>
/// <param name="CanonicalEquivalent">The platform-wide <see cref="LifecycleState"/> it maps to.</param>
public sealed record DecisionStatusDescriptor(DecisionStatus Status, string Name, LifecycleState CanonicalEquivalent)
    : IFamilySpecificState;

/// <summary>The risk family's own state vocabulary, and its mapping onto the canonical lifecycle.</summary>
public static class RiskStatuses
{
    private static readonly IReadOnlyList<RiskStatusDescriptor> Descriptors =
    [
        new(RiskStatus.Open, "Open", LifecycleState.Draft),
        new(RiskStatus.Mitigating, "Mitigating", LifecycleState.Draft),
        new(RiskStatus.Accepted, "Accepted", LifecycleState.Approved),
        new(RiskStatus.Closed, "Closed", LifecycleState.Archived),
    ];

    /// <summary>Every risk status, in register order.</summary>
    public static IReadOnlyList<RiskStatusDescriptor> All => Descriptors;

    /// <summary>The descriptor for <paramref name="status"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is not a declared status.</exception>
    public static RiskStatusDescriptor For(RiskStatus status) =>
        Descriptors.FirstOrDefault(d => d.Status == status)
        ?? throw new ArgumentOutOfRangeException(nameof(status), status, "No descriptor is declared for this risk status.");

    /// <summary>Whether <paramref name="status"/> means the risk is still live.</summary>
    /// <remarks>
    /// <see cref="RiskStatus.Accepted"/> counts as live, deliberately — see
    /// <see cref="RiskStatus"/>. Only <see cref="RiskStatus.Closed"/> takes
    /// a risk off the register.
    /// </remarks>
    public static bool IsLive(RiskStatus status) => status is not RiskStatus.Closed;
}

/// <summary>The issue family's own state vocabulary, and its mapping onto the canonical lifecycle.</summary>
public static class IssueStatuses
{
    private static readonly IReadOnlyList<IssueStatusDescriptor> Descriptors =
    [
        new(IssueStatus.Open, "Open", LifecycleState.Draft),
        new(IssueStatus.Investigating, "Investigating", LifecycleState.Draft),
        new(IssueStatus.Resolved, "Resolved", LifecycleState.Released),
        new(IssueStatus.Closed, "Closed", LifecycleState.Archived),
    ];

    /// <summary>Every issue status, in register order.</summary>
    public static IReadOnlyList<IssueStatusDescriptor> All => Descriptors;

    /// <summary>The descriptor for <paramref name="status"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is not a declared status.</exception>
    public static IssueStatusDescriptor For(IssueStatus status) =>
        Descriptors.FirstOrDefault(d => d.Status == status)
        ?? throw new ArgumentOutOfRangeException(nameof(status), status, "No descriptor is declared for this issue status.");

    /// <summary>Whether <paramref name="status"/> means the issue still needs attention.</summary>
    /// <remarks>
    /// <see cref="IssueStatus.Resolved"/> counts as open: a fix nobody has
    /// confirmed is still somebody's problem, and an open-issue count that
    /// dropped the moment a fix was claimed would flatter every report in
    /// the product.
    /// </remarks>
    public static bool IsOpen(IssueStatus status) => status is not IssueStatus.Closed;
}

/// <summary>The decision family's own state vocabulary, and its mapping onto the canonical lifecycle.</summary>
public static class DecisionStatuses
{
    private static readonly IReadOnlyList<DecisionStatusDescriptor> Descriptors =
    [
        new(DecisionStatus.Proposed, "Proposed", LifecycleState.InReview),
        new(DecisionStatus.Accepted, "Accepted", LifecycleState.Approved),
        new(DecisionStatus.Rejected, "Rejected", LifecycleState.Cancelled),
        new(DecisionStatus.Superseded, "Superseded", LifecycleState.Superseded),
    ];

    /// <summary>Every decision status, in register order.</summary>
    public static IReadOnlyList<DecisionStatusDescriptor> All => Descriptors;

    /// <summary>The descriptor for <paramref name="status"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is not a declared status.</exception>
    public static DecisionStatusDescriptor For(DecisionStatus status) =>
        Descriptors.FirstOrDefault(d => d.Status == status)
        ?? throw new ArgumentOutOfRangeException(nameof(status), status, "No descriptor is declared for this decision status.");

    /// <summary>Whether <paramref name="status"/> means the decision is currently in force.</summary>
    public static bool IsInForce(DecisionStatus status) => status is DecisionStatus.Accepted;

    /// <summary>Whether <paramref name="status"/> still awaits a decision.</summary>
    public static bool IsAwaitingDecision(DecisionStatus status) => status is DecisionStatus.Proposed;
}

/// <summary>Which risk status may follow which.</summary>
/// <remarks>
/// Mirrors <see cref="TaskWorkStateTransitions"/>'s dictionary shape and its
/// "same-to-same is never permitted" rule. Nothing here is terminal: a
/// closed risk can be reopened, because risks recur, and a register that
/// could not say so would force a user to raise a duplicate.
/// </remarks>
public static class RiskStatusTransitions
{
    private static readonly IReadOnlyDictionary<RiskStatus, IReadOnlyList<RiskStatus>> Permitted =
        new Dictionary<RiskStatus, IReadOnlyList<RiskStatus>>
        {
            [RiskStatus.Open] = [RiskStatus.Mitigating, RiskStatus.Accepted, RiskStatus.Closed],
            [RiskStatus.Mitigating] = [RiskStatus.Open, RiskStatus.Accepted, RiskStatus.Closed],
            [RiskStatus.Accepted] = [RiskStatus.Open, RiskStatus.Mitigating, RiskStatus.Closed],
            [RiskStatus.Closed] = [RiskStatus.Open, RiskStatus.Mitigating],
        };

    /// <summary>Whether a risk may move from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static bool IsPermitted(RiskStatus from, RiskStatus to) =>
        from != to && Permitted.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Every status a risk in <paramref name="from"/> may move to.</summary>
    public static IReadOnlyList<RiskStatus> GetPermittedTargets(RiskStatus from) =>
        Permitted.TryGetValue(from, out var targets) ? targets : [];
}

/// <summary>Which issue status may follow which.</summary>
/// <remarks>
/// A resolved or closed issue can be reopened — the same reasoning that
/// gave the task family its own table. An issue that came back is the same
/// issue, and recording it as a new one loses the history that it recurred.
/// </remarks>
public static class IssueStatusTransitions
{
    private static readonly IReadOnlyDictionary<IssueStatus, IReadOnlyList<IssueStatus>> Permitted =
        new Dictionary<IssueStatus, IReadOnlyList<IssueStatus>>
        {
            [IssueStatus.Open] = [IssueStatus.Investigating, IssueStatus.Resolved, IssueStatus.Closed],
            [IssueStatus.Investigating] = [IssueStatus.Open, IssueStatus.Resolved, IssueStatus.Closed],
            [IssueStatus.Resolved] = [IssueStatus.Open, IssueStatus.Investigating, IssueStatus.Closed],
            [IssueStatus.Closed] = [IssueStatus.Open, IssueStatus.Investigating],
        };

    /// <summary>Whether an issue may move from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static bool IsPermitted(IssueStatus from, IssueStatus to) =>
        from != to && Permitted.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Every status an issue in <paramref name="from"/> may move to.</summary>
    public static IReadOnlyList<IssueStatus> GetPermittedTargets(IssueStatus from) =>
        Permitted.TryGetValue(from, out var targets) ? targets : [];
}

/// <summary>Which decision status may follow which.</summary>
/// <remarks>
/// <para>
/// The one of the three tables with a genuinely terminal state.
/// <see cref="DecisionStatus.Superseded"/> has no outward transitions,
/// because a decision that was replaced is a matter of record: bringing it
/// back would rewrite what the project decided and when. The replacement is
/// a new decision, which is what supersession means.
/// </para>
/// <para>
/// A rejected decision may be proposed again, because reconsidering is
/// ordinary and the alternative is raising a near-duplicate that hides the
/// first refusal.
/// </para>
/// </remarks>
public static class DecisionStatusTransitions
{
    private static readonly IReadOnlyDictionary<DecisionStatus, IReadOnlyList<DecisionStatus>> Permitted =
        new Dictionary<DecisionStatus, IReadOnlyList<DecisionStatus>>
        {
            [DecisionStatus.Proposed] = [DecisionStatus.Accepted, DecisionStatus.Rejected],
            [DecisionStatus.Accepted] = [DecisionStatus.Superseded],
            [DecisionStatus.Rejected] = [DecisionStatus.Proposed],
            [DecisionStatus.Superseded] = [],
        };

    /// <summary>Whether a decision may move from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static bool IsPermitted(DecisionStatus from, DecisionStatus to) =>
        from != to && Permitted.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Every status a decision in <paramref name="from"/> may move to.</summary>
    public static IReadOnlyList<DecisionStatus> GetPermittedTargets(DecisionStatus from) =>
        Permitted.TryGetValue(from, out var targets) ? targets : [];
}

/// <summary>Thrown when a risk is asked to move to a status it cannot reach from its current one.</summary>
public sealed class InvalidRiskStatusTransitionException : InvalidOperationException
{
    /// <summary>Initialises a new instance of the <see cref="InvalidRiskStatusTransitionException"/> class.</summary>
    public InvalidRiskStatusTransitionException(Guid riskId, RiskStatus from, RiskStatus to)
        : base($"Risk '{riskId}' cannot move from '{from}' to '{to}'. Permitted: " +
               $"{Describe(RiskStatusTransitions.GetPermittedTargets(from))}.")
    {
        RiskId = riskId;
        From = from;
        To = to;
    }

    /// <summary>The risk that refused the transition.</summary>
    public Guid RiskId { get; }

    /// <summary>The status it is in.</summary>
    public RiskStatus From { get; }

    /// <summary>The status it was asked to move to.</summary>
    public RiskStatus To { get; }

    private static string Describe(IReadOnlyList<RiskStatus> targets) =>
        targets.Count == 0 ? "nothing — this status is terminal" : string.Join(", ", targets);
}

/// <summary>Thrown when an issue is asked to move to a status it cannot reach from its current one.</summary>
public sealed class InvalidIssueStatusTransitionException : InvalidOperationException
{
    /// <summary>Initialises a new instance of the <see cref="InvalidIssueStatusTransitionException"/> class.</summary>
    public InvalidIssueStatusTransitionException(Guid issueId, IssueStatus from, IssueStatus to)
        : base($"Issue '{issueId}' cannot move from '{from}' to '{to}'. Permitted: " +
               $"{Describe(IssueStatusTransitions.GetPermittedTargets(from))}.")
    {
        IssueId = issueId;
        From = from;
        To = to;
    }

    /// <summary>The issue that refused the transition.</summary>
    public Guid IssueId { get; }

    /// <summary>The status it is in.</summary>
    public IssueStatus From { get; }

    /// <summary>The status it was asked to move to.</summary>
    public IssueStatus To { get; }

    private static string Describe(IReadOnlyList<IssueStatus> targets) =>
        targets.Count == 0 ? "nothing — this status is terminal" : string.Join(", ", targets);
}

/// <summary>Thrown when a decision is asked to move to a status it cannot reach from its current one.</summary>
public sealed class InvalidDecisionStatusTransitionException : InvalidOperationException
{
    /// <summary>Initialises a new instance of the <see cref="InvalidDecisionStatusTransitionException"/> class.</summary>
    public InvalidDecisionStatusTransitionException(Guid decisionId, DecisionStatus from, DecisionStatus to)
        : base($"Decision '{decisionId}' cannot move from '{from}' to '{to}'. Permitted: " +
               $"{Describe(DecisionStatusTransitions.GetPermittedTargets(from))}.")
    {
        DecisionId = decisionId;
        From = from;
        To = to;
    }

    /// <summary>The decision that refused the transition.</summary>
    public Guid DecisionId { get; }

    /// <summary>The status it is in.</summary>
    public DecisionStatus From { get; }

    /// <summary>The status it was asked to move to.</summary>
    public DecisionStatus To { get; }

    private static string Describe(IReadOnlyList<DecisionStatus> targets) =>
        targets.Count == 0 ? "nothing — this status is terminal" : string.Join(", ", targets);
}

/// <summary>
/// The relationship kinds the governance families write (`ADR-0105` — one
/// canonical declaring class per value).
/// </summary>
/// <remarks>
/// Mirrors <see cref="TaskRelationshipKinds"/>, which is the established
/// precedent for a family owning its own relationship vocabulary rather
/// than spelling literals at call sites.
/// </remarks>
public static class GovernanceRelationshipKinds
{
    /// <summary>A risk to the issue it became when it materialised.</summary>
    /// <remarks>
    /// The one link worth recording between these families. A risk that
    /// happened is the origin story of the issue, and losing it means
    /// nobody can later ask which of the risks the project identified
    /// actually came true.
    /// </remarks>
    public const string Realises = "realises";

    /// <summary>A decision to the risk, issue or requirement it was taken about.</summary>
    public const string Addresses = "addresses";

    /// <summary>A decision to the decision it replaced.</summary>
    public const string Supersedes = "supersedes";
}
