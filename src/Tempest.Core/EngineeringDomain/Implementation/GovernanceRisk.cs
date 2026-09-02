using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

/// <summary>Something that has gone wrong and is being dealt with.</summary>
/// <remarks>
/// Status, priority and ownership are held here, on the real domain object,
/// captured through <see cref="CaptureTypeState"/> and read back by the
/// production rehydration path (`TD-85`/`TD-104`) — the same shape
/// <see cref="EngineeringTask"/> established. There is no separate issue
/// store and no <c>ProjectId</c>: an issue belongs to a project because its
/// parent chain reaches one, exactly as every other object does.
/// </remarks>
public sealed class Issue : EngineeringObjectBase, IIssue, IRehydratable<Issue>
{
    private readonly object _issueLock = new();

    private IssueStatus _status;
    private WorkPriority _priority;
    private string? _assignedToPrincipalId;

    /// <summary>Initialises a new instance of the <see cref="Issue"/> class.</summary>
    public Issue(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        IssueStatus status = IssueStatus.Open, WorkPriority priority = WorkPriority.Normal,
        string? assignedToPrincipalId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        _status = status;
        _priority = priority;
        _assignedToPrincipalId = GovernanceState.Normalise(assignedToPrincipalId);
    }

    /// <summary>Where this issue stands.</summary>
    /// <remarks>
    /// Deliberately separate from <see cref="EngineeringObjectBase.Status"/>,
    /// which remains the canonical document lifecycle. Read
    /// <see cref="IssueStatuses.For"/> for the canonical equivalent where a
    /// cross-domain consumer needs one answer for every Kind.
    /// </remarks>
    public IssueStatus IssueStatus
    {
        get { lock (_issueLock) { return _status; } }
    }

    /// <summary>How urgent this issue is.</summary>
    public WorkPriority Priority
    {
        get { lock (_issueLock) { return _priority; } }
    }

    /// <summary>Who owns this issue, or <see langword="null"/> when nobody does.</summary>
    public string? AssignedToPrincipalId
    {
        get { lock (_issueLock) { return _assignedToPrincipalId; } }
    }

    /// <summary>Whether this issue still needs attention.</summary>
    public bool IsOpen
    {
        get { lock (_issueLock) { return IssueStatuses.IsOpen(_status); } }
    }

    /// <summary>Moves this issue to <paramref name="target"/>.</summary>
    /// <exception cref="InvalidIssueStatusTransitionException">The move is not permitted from the current status.</exception>
    public Task ChangeStatusAsync(IssueStatus target, CancellationToken cancellationToken = default)
    {
        lock (_issueLock)
        {
            if (!IssueStatusTransitions.IsPermitted(_status, target))
                throw new InvalidIssueStatusTransitionException(Id, _status, target);

            _status = target;
        }

        return PersistStateAsync(cancellationToken);
    }

    /// <summary>Sets this issue's priority.</summary>
    public Task SetPriorityAsync(WorkPriority priority, CancellationToken cancellationToken = default)
    {
        lock (_issueLock)
            _priority = priority;

        return PersistStateAsync(cancellationToken);
    }

    /// <summary>Assigns this issue to <paramref name="principalId"/>, or unassigns it when <see langword="null"/>.</summary>
    /// <remarks>
    /// Takes a principal id rather than resolving one itself, for the reason
    /// <see cref="EngineeringTask.AssignAsync"/> gives: an engineering object
    /// never knows who is signed in.
    /// </remarks>
    public Task AssignAsync(string? principalId, CancellationToken cancellationToken = default)
    {
        lock (_issueLock)
            _assignedToPrincipalId = GovernanceState.Normalise(principalId);

        return PersistStateAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(IssueStatus)] = this.IssueStatus.ToString();
        state[nameof(Priority)] = Priority.ToString();
        state[nameof(AssignedToPrincipalId)] = AssignedToPrincipalId;
    }

    static Issue IRehydratable<Issue>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            GovernanceState.Read(state, nameof(IssueStatus), IssueStatus.Open),
            GovernanceState.Read(state, nameof(Priority), WorkPriority.Normal),
            state.Type(nameof(AssignedToPrincipalId)));
}

/// <summary>Something that might go wrong, and what is being done about it.</summary>
/// <remarks>
/// <para>
/// <see cref="Likelihood"/> and <see cref="Severity"/> were already here and
/// stay as free text rather than becoming enums. They are the two axes a
/// team scores a risk on, and every organisation scores them on its own
/// scale — a 1-5, a High/Medium/Low, a probability band. Freezing one of
/// those into the platform would make the field wrong for everyone using a
/// different one, and this Work Package had no mandate to choose a scoring
/// scheme. What changed is that they can now be <em>set</em>: a risk whose
/// likelihood can never be revised after it is raised is not a risk
/// register.
/// </para>
/// <para>
/// Not sealed, because <see cref="Hazard"/> derives from it.
/// </para>
/// </remarks>
public class Risk : EngineeringObjectBase, IRisk, IRehydratable<Risk>
{
    private readonly object _riskLock = new();

    private RiskStatus _status;
    private string? _likelihood;
    private string? _severity;
    private string? _ownedByPrincipalId;

    /// <summary>Initialises a new instance of the <see cref="Risk"/> class.</summary>
    public Risk(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        string? likelihood = null, string? severity = null,
        RiskStatus status = RiskStatus.Open, string? ownedByPrincipalId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        _likelihood = likelihood;
        _severity = severity;
        _status = status;
        _ownedByPrincipalId = GovernanceState.Normalise(ownedByPrincipalId);
    }

    /// <summary>How likely this risk is thought to be, in the team's own scale.</summary>
    public string? Likelihood
    {
        get { lock (_riskLock) { return _likelihood; } }
    }

    /// <summary>How bad it would be, in the team's own scale.</summary>
    public string? Severity
    {
        get { lock (_riskLock) { return _severity; } }
    }

    /// <summary>Where this risk stands.</summary>
    public RiskStatus RiskStatus
    {
        get { lock (_riskLock) { return _status; } }
    }

    /// <summary>Who owns this risk, or <see langword="null"/> when nobody does.</summary>
    /// <remarks>
    /// An unowned risk is a real and reportable state, not a defect — it is
    /// exactly what a review meeting is looking for.
    /// </remarks>
    public string? OwnedByPrincipalId
    {
        get { lock (_riskLock) { return _ownedByPrincipalId; } }
    }

    /// <summary>Whether this risk is still live.</summary>
    public bool IsLive
    {
        get { lock (_riskLock) { return RiskStatuses.IsLive(_status); } }
    }

    /// <summary>Moves this risk to <paramref name="target"/>.</summary>
    /// <exception cref="InvalidRiskStatusTransitionException">The move is not permitted from the current status.</exception>
    public Task ChangeStatusAsync(RiskStatus target, CancellationToken cancellationToken = default)
    {
        lock (_riskLock)
        {
            if (!RiskStatusTransitions.IsPermitted(_status, target))
                throw new InvalidRiskStatusTransitionException(Id, _status, target);

            _status = target;
        }

        return PersistStateAsync(cancellationToken);
    }

    /// <summary>Sets or clears this risk's likelihood and severity scores.</summary>
    /// <remarks>
    /// Both together, because they are read together: a risk scored severe
    /// but with its likelihood left from a previous assessment is worse than
    /// one that is honestly unscored.
    /// </remarks>
    public Task ScoreAsync(string? likelihood, string? severity, CancellationToken cancellationToken = default)
    {
        lock (_riskLock)
        {
            _likelihood = GovernanceState.Normalise(likelihood);
            _severity = GovernanceState.Normalise(severity);
        }

        return PersistStateAsync(cancellationToken);
    }

    /// <summary>Gives this risk an owner, or removes its owner when <see langword="null"/>.</summary>
    public Task AssignOwnerAsync(string? principalId, CancellationToken cancellationToken = default)
    {
        lock (_riskLock)
            _ownedByPrincipalId = GovernanceState.Normalise(principalId);

        return PersistStateAsync(cancellationToken);
    }

    /// <summary>Records that this risk materialised as <paramref name="issueId"/>.</summary>
    public Task RealisedAsAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        LinkAsync(issueId, GovernanceRelationshipKinds.Realises, cancellationToken);

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(Likelihood)] = Likelihood;
        state[nameof(Severity)] = Severity;
        state[nameof(RiskStatus)] = this.RiskStatus.ToString();
        state[nameof(OwnedByPrincipalId)] = OwnedByPrincipalId;
    }

    /// <summary>Reads a risk's own persisted state back, tolerating a record written before these fields existed.</summary>
    /// <remarks>
    /// An older record has no RiskStatus key at all. Falling back to
    /// <see cref="RiskStatus.Open"/> is the honest reading of "a risk nobody
    /// has closed", and matches `TD-85`'s rule that a missing field comes
    /// back visibly empty rather than failing the whole rehydration.
    /// </remarks>
    private protected static (string? Likelihood, string? Severity, RiskStatus Status, string? Owner) ReadRiskState(EngineeringObjectState state) =>
        (state.Type(nameof(Likelihood)),
         state.Type(nameof(Severity)),
         GovernanceState.Read(state, nameof(RiskStatus), RiskStatus.Open),
         state.Type(nameof(OwnedByPrincipalId)));

    static Risk IRehydratable<Risk>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state)
    {
        var (likelihood, severity, status, owner) = ReadRiskState(state);
        return new Risk(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            likelihood, severity, status, owner);
    }
}

/// <summary>A risk to safety specifically.</summary>
/// <remarks>
/// Derives from <see cref="Risk"/> and inherits its whole workflow, so a
/// hazard is scored, owned and closed by the same operations and appears on
/// the same register. A safety risk that needed a parallel workflow would be
/// a safety risk somebody forgot to review.
/// </remarks>
public sealed class Hazard : Risk, IHazard, IRehydratable<Hazard>
{
    /// <summary>Initialises a new instance of the <see cref="Hazard"/> class.</summary>
    public Hazard(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        string? likelihood = null, string? severity = null,
        RiskStatus status = RiskStatus.Open, string? ownedByPrincipalId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata, likelihood, severity, status, ownedByPrincipalId)
    {
    }

    static Hazard IRehydratable<Hazard>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state)
    {
        var (likelihood, severity, status, owner) = ReadRiskState(state);
        return new Hazard(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            likelihood, severity, status, owner);
    }
}

/// <summary>Something the project decided, and why.</summary>
/// <remarks>
/// <see cref="Rationale"/> is the reason the decision was taken and can be
/// revised — a rationale that turned out to be wrong is worth correcting,
/// and the object's own revision history records that it changed.
/// </remarks>
public sealed class Decision : EngineeringObjectBase, IDecision, IRehydratable<Decision>
{
    private readonly object _decisionLock = new();

    private DecisionStatus _status;
    private string _rationale;
    private string? _decidedByPrincipalId;
    private DateTimeOffset? _decidedAt;

    /// <summary>Initialises a new instance of the <see cref="Decision"/> class.</summary>
    public Decision(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string rationale,
        DecisionStatus status = DecisionStatus.Proposed, string? decidedByPrincipalId = null,
        DateTimeOffset? decidedAt = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        _rationale = rationale;
        _status = status;
        _decidedByPrincipalId = GovernanceState.Normalise(decidedByPrincipalId);
        _decidedAt = decidedAt;
    }

    /// <summary>Why this decision was taken.</summary>
    public string Rationale
    {
        get { lock (_decisionLock) { return _rationale; } }
    }

    /// <summary>Where this decision stands.</summary>
    public DecisionStatus DecisionStatus
    {
        get { lock (_decisionLock) { return _status; } }
    }

    /// <summary>Who decided, or <see langword="null"/> when it is still proposed.</summary>
    public string? DecidedByPrincipalId
    {
        get { lock (_decisionLock) { return _decidedByPrincipalId; } }
    }

    /// <summary>When it was decided, or <see langword="null"/> when it has not been.</summary>
    /// <remarks>
    /// Set by <see cref="DecideAsync"/> rather than by the caller, and never
    /// cleared once set: when a decision was taken is a matter of record, and
    /// a later supersession does not un-take it.
    /// </remarks>
    public DateTimeOffset? DecidedAt
    {
        get { lock (_decisionLock) { return _decidedAt; } }
    }

    /// <summary>Whether this decision is currently in force.</summary>
    public bool IsInForce
    {
        get { lock (_decisionLock) { return DecisionStatuses.IsInForce(_status); } }
    }

    /// <summary>
    /// Moves this decision to <paramref name="target"/>, recording who
    /// decided and when if this is the moment it was decided.
    /// </summary>
    /// <param name="target">The status to move to.</param>
    /// <param name="decidedByPrincipalId">Who is deciding. Recorded only when moving out of <see cref="DecisionStatus.Proposed"/>.</param>
    /// <param name="decidedAt">When the decision was taken. Recorded only when moving out of <see cref="DecisionStatus.Proposed"/>.</param>
    /// <param name="cancellationToken">Cancels the persist.</param>
    /// <exception cref="InvalidDecisionStatusTransitionException">The move is not permitted from the current status.</exception>
    public Task DecideAsync(
        DecisionStatus target,
        string? decidedByPrincipalId = null,
        DateTimeOffset? decidedAt = null,
        CancellationToken cancellationToken = default)
    {
        lock (_decisionLock)
        {
            if (!DecisionStatusTransitions.IsPermitted(_status, target))
                throw new InvalidDecisionStatusTransitionException(Id, _status, target);

            // Who decided is recorded at the moment of deciding, and only
            // then. Superseding a decision later does not change who took
            // the original one, and overwriting it would erase the record
            // this family exists to keep.
            if (_status is DecisionStatus.Proposed && target is DecisionStatus.Accepted or DecisionStatus.Rejected)
            {
                _decidedByPrincipalId = GovernanceState.Normalise(decidedByPrincipalId) ?? _decidedByPrincipalId;
                _decidedAt = decidedAt ?? _decidedAt;
            }

            _status = target;
        }

        return PersistStateAsync(cancellationToken);
    }

    /// <summary>Rewrites this decision's rationale.</summary>
    public Task SetRationaleAsync(string rationale, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rationale);

        lock (_decisionLock)
            _rationale = rationale;

        return PersistStateAsync(cancellationToken);
    }

    /// <summary>Records that this decision was taken about <paramref name="subjectId"/>.</summary>
    public Task AddressesAsync(Guid subjectId, CancellationToken cancellationToken = default) =>
        LinkAsync(subjectId, GovernanceRelationshipKinds.Addresses, cancellationToken);

    /// <summary>Records that this decision replaced <paramref name="supersededDecisionId"/>.</summary>
    public Task SupersedesAsync(Guid supersededDecisionId, CancellationToken cancellationToken = default) =>
        LinkAsync(supersededDecisionId, GovernanceRelationshipKinds.Supersedes, cancellationToken);

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(Rationale)] = Rationale;
        state[nameof(DecisionStatus)] = this.DecisionStatus.ToString();
        state[nameof(DecidedByPrincipalId)] = DecidedByPrincipalId;
        state[nameof(DecidedAt)] = DecidedAt?.ToString("O");
    }

    static Decision IRehydratable<Decision>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.Type(nameof(Rationale)) ?? string.Empty,
            GovernanceState.Read(state, nameof(DecisionStatus), DecisionStatus.Proposed),
            state.Type(nameof(DecidedByPrincipalId)),
            state.TypeDate(nameof(DecidedAt)));
}

/// <summary>Something taken to be true that the project has not proved.</summary>
public sealed class Assumption : EngineeringObjectBase, IAssumption, IRehydratable<Assumption>
{
    /// <summary>Initialises a new instance of the <see cref="Assumption"/> class.</summary>
    public Assumption(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }

    static Assumption IRehydratable<Assumption>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata);
}

/// <summary>Shared state-reading helpers for the governance families.</summary>
/// <remarks>
/// One place for the two rules these three types apply identically, rather
/// than three copies that can drift: an absent or unparseable enum key falls
/// back to the family's own starting state (`TD-85`), and a blank principal
/// id is stored as <see langword="null"/> so "unassigned" has exactly one
/// representation.
/// </remarks>
internal static class GovernanceState
{
    /// <summary>Reads an enum written by <c>CaptureTypeState</c>, falling back when the key is absent or unreadable.</summary>
    internal static TEnum Read<TEnum>(EngineeringObjectState state, string key, TEnum fallback)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(state.Type(key), out var value) && Enum.IsDefined(value) ? value : fallback;

    /// <summary>Reduces a blank or whitespace principal id to <see langword="null"/>.</summary>
    internal static string? Normalise(string? principalId) =>
        string.IsNullOrWhiteSpace(principalId) ? null : principalId.Trim();
}
