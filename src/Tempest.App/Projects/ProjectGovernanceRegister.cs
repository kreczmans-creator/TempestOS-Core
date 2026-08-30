using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Projects;

/// <summary>One risk belonging to a project, as the Risks surface shows it.</summary>
/// <param name="ObjectId">The risk's own identity.</param>
/// <param name="Kind">Risk or Hazard — a Hazard is a safety risk, and is listed alongside.</param>
/// <param name="Identifier">Its business identifier, where it has one.</param>
/// <param name="DisplayName">Its title.</param>
/// <param name="Description">Its current revision content — what the risk actually says.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="Likelihood">How likely, in the team's own scale.</param>
/// <param name="Severity">How bad, in the team's own scale.</param>
/// <param name="OwnedByPrincipalId">Who owns it, or <see langword="null"/> when nobody does.</param>
/// <param name="RealisedAsIssueId">The issue this risk became, where it materialised.</param>
public sealed record ProjectRiskEntry(
    Guid ObjectId,
    string Kind,
    string? Identifier,
    string DisplayName,
    string Description,
    RiskStatus Status,
    string? Likelihood,
    string? Severity,
    string? OwnedByPrincipalId,
    Guid? RealisedAsIssueId)
{
    /// <summary>Whether this risk is still live.</summary>
    public bool IsLive => RiskStatuses.IsLive(Status);

    /// <summary>Whether anybody owns this risk.</summary>
    public bool IsUnowned => OwnedByPrincipalId is null;

    /// <summary>Whether this risk has been scored on both axes.</summary>
    /// <remarks>
    /// Both, deliberately. A risk scored on one axis only cannot be ranked
    /// against any other, so for a register's purposes it is unscored.
    /// </remarks>
    public bool IsScored => !string.IsNullOrWhiteSpace(Likelihood) && !string.IsNullOrWhiteSpace(Severity);
}

/// <summary>One issue belonging to a project, as the Issues surface shows it.</summary>
/// <param name="ObjectId">The issue's own identity.</param>
/// <param name="Identifier">Its business identifier, where it has one.</param>
/// <param name="DisplayName">Its title.</param>
/// <param name="Description">Its current revision content.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="Priority">How urgent it is.</param>
/// <param name="AssignedToPrincipalId">Who owns it, or <see langword="null"/> when nobody does.</param>
/// <param name="RaisedByRiskId">The risk that materialised as this issue, where one did.</param>
public sealed record ProjectIssueEntry(
    Guid ObjectId,
    string? Identifier,
    string DisplayName,
    string Description,
    IssueStatus Status,
    WorkPriority Priority,
    string? AssignedToPrincipalId,
    Guid? RaisedByRiskId)
{
    /// <summary>Whether this issue still needs attention.</summary>
    public bool IsOpen => IssueStatuses.IsOpen(Status);

    /// <summary>Whether anybody owns this issue.</summary>
    public bool IsUnassigned => AssignedToPrincipalId is null;
}

/// <summary>One decision belonging to a project, as the Decisions surface shows it.</summary>
/// <param name="ObjectId">The decision's own identity.</param>
/// <param name="Identifier">Its business identifier, where it has one.</param>
/// <param name="DisplayName">Its title.</param>
/// <param name="Description">Its current revision content.</param>
/// <param name="Rationale">Why it was taken.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="DecidedByPrincipalId">Who decided, or <see langword="null"/> when still proposed.</param>
/// <param name="DecidedAt">When it was decided, or <see langword="null"/>.</param>
/// <param name="AddressesObjectIds">What it was taken about — risks, issues or requirements.</param>
public sealed record ProjectDecisionEntry(
    Guid ObjectId,
    string? Identifier,
    string DisplayName,
    string Description,
    string Rationale,
    DecisionStatus Status,
    string? DecidedByPrincipalId,
    DateTimeOffset? DecidedAt,
    IReadOnlyList<Guid> AddressesObjectIds)
{
    /// <summary>Whether this decision is currently in force.</summary>
    public bool IsInForce => DecisionStatuses.IsInForce(Status);

    /// <summary>Whether this decision is still waiting to be taken.</summary>
    public bool IsAwaitingDecision => DecisionStatuses.IsAwaitingDecision(Status);
}

/// <summary>The risks, issues and decisions belonging to a project.</summary>
public interface IProjectGovernanceRegister
{
    /// <summary>Every risk and hazard in <paramref name="projectId"/>.</summary>
    Task<IReadOnlyList<ProjectRiskEntry>> ListRisksAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Every issue in <paramref name="projectId"/>.</summary>
    Task<IReadOnlyList<ProjectIssueEntry>> ListIssuesAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Every decision in <paramref name="projectId"/>.</summary>
    Task<IReadOnlyList<ProjectDecisionEntry>> ListDecisionsAsync(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The project's own risk, issue and decision register — the read model
/// behind the Project Workspace's Risks area.
/// </summary>
/// <remarks>
/// <para>
/// <b>Membership is <see cref="ProjectMembership"/>'s answer, not a second
/// one.</b> A risk belongs to a project when the durable
/// <see cref="IHasParent"/> chain from it reaches that project — so a risk
/// raised against a Part inside a Sub-Assembly inside an Assembly is a
/// project risk, exactly as a task or a document in the same position is.
/// No <c>ProjectId</c> field was added to any of these three models, for
/// the same reason none was added to tasks, documents or requirements: it
/// would be a competing answer to a question the platform already answers.
/// </para>
/// <para>
/// <b>A read model, not a store.</b> It holds no state, caches nothing and
/// creates no persistence. Everything it reports is read from the domain
/// objects themselves, which is why a risk edited anywhere in the product
/// shows correctly here without this class knowing the edit happened.
/// </para>
/// <para>
/// <b>Three lists, one class.</b> Risks, issues and decisions share one
/// surface and one project area, and they are read in one pass over the
/// same membership result — splitting them into three registers would walk
/// the same parent chains three times to answer one screen.
/// </para>
/// </remarks>
public sealed class ProjectGovernanceRegister : IProjectGovernanceRegister
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="ProjectGovernanceRegister"/> class.</summary>
    /// <param name="context">The engineering domain.</param>
    public ProjectGovernanceRegister(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectRiskEntry>> ListRisksAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var entries = new List<ProjectRiskEntry>();

        foreach (var member in await MembersAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Hazard derives from Risk, so this catches both — which is the
            // point: a safety risk that did not appear on the risk register
            // would be the most dangerous kind of omission.
            if (member is not Risk risk)
                continue;

            entries.Add(new ProjectRiskEntry(
                risk.Id,
                risk.Kind ?? string.Empty,
                risk.Identifier,
                risk.DisplayName,
                risk.Content,
                risk.RiskStatus,
                risk.Likelihood,
                risk.Severity,
                risk.OwnedByPrincipalId,
                await ResolveLinkAsync(risk, GovernanceRelationshipKinds.Realises, cancellationToken).ConfigureAwait(false)));
        }

        // Live risks first, then unscored ones above scored — an unscored
        // live risk is the thing a review meeting must not miss. Closed
        // risks sink to the bottom rather than disappearing, because a
        // register that hides what it closed cannot be audited.
        return
        [
            .. entries
                .OrderByDescending(e => e.IsLive)
                .ThenBy(e => e.IsScored)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.ObjectId),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectIssueEntry>> ListIssuesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var entries = new List<ProjectIssueEntry>();

        foreach (var member in await MembersAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (member is not Issue issue)
                continue;

            entries.Add(new ProjectIssueEntry(
                issue.Id,
                issue.Identifier,
                issue.DisplayName,
                issue.Content,
                issue.IssueStatus,
                issue.Priority,
                issue.AssignedToPrincipalId,
                await ResolveRaisedByRiskAsync(issue, cancellationToken).ConfigureAwait(false)));
        }

        // Open work first, most urgent first within it.
        return
        [
            .. entries
                .OrderByDescending(e => e.IsOpen)
                .ThenByDescending(e => e.Priority)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.ObjectId),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectDecisionEntry>> ListDecisionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var entries = new List<ProjectDecisionEntry>();

        foreach (var member in await MembersAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (member is not Decision decision)
                continue;

            entries.Add(new ProjectDecisionEntry(
                decision.Id,
                decision.Identifier,
                decision.DisplayName,
                decision.Content,
                decision.Rationale,
                decision.DecisionStatus,
                decision.DecidedByPrincipalId,
                decision.DecidedAt,
                await ResolveAddressesAsync(decision, cancellationToken).ConfigureAwait(false)));
        }

        // Decisions still waiting on someone come first — they are the ones
        // that block work. Then the most recently decided, because a
        // decision log is read newest-first.
        return
        [
            .. entries
                .OrderByDescending(e => e.IsAwaitingDecision)
                .ThenByDescending(e => e.DecidedAt ?? DateTimeOffset.MinValue)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.ObjectId),
        ];
    }

    private Task<IReadOnlyList<IEngineeringObject>> MembersAsync(Guid projectId, CancellationToken cancellationToken) =>
        ProjectMembership.ListProjectMembersAsync(_context.Repository, projectId, cancellationToken);

    /// <summary>The most recent outbound link of <paramref name="kind"/> from <paramref name="source"/>.</summary>
    private static async Task<Guid?> ResolveLinkAsync(EngineeringObjectBase source, string kind, CancellationToken cancellationToken)
    {
        var relationships = await source.GetRelationshipsAsync(cancellationToken).ConfigureAwait(false);

        return relationships
            .Where(r => string.Equals(r.RelationshipKind, kind, StringComparison.Ordinal))
            .Where(r => r.SourceId == source.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => (Guid?)r.TargetId)
            .FirstOrDefault();
    }

    /// <summary>
    /// The risk that materialised as <paramref name="issue"/>, read from the
    /// risk's own outbound link rather than from a field on the issue.
    /// </summary>
    /// <remarks>
    /// The relationship is written once, by the risk (<c>realises</c>), so
    /// the issue side has to be read as an <em>incoming</em> edge. An
    /// object's own <c>GetRelationshipsAsync</c> returns outgoing edges
    /// only, so this goes to the relationship repository directly — the
    /// first version filtered the issue's outgoing list for a link pointing
    /// at itself, which can never match and silently reported "no
    /// originating risk" for every issue. Storing the risk id on the issue
    /// as well would be a second answer to the same question.
    /// </remarks>
    private async Task<Guid?> ResolveRaisedByRiskAsync(Issue issue, CancellationToken cancellationToken)
    {
        var incoming = await _context.RelationshipRepository
            .GetIncomingAsync(issue.Id, cancellationToken)
            .ConfigureAwait(false);

        return incoming
            .Where(r => string.Equals(r.RelationshipKind, GovernanceRelationshipKinds.Realises, StringComparison.Ordinal))
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => (Guid?)r.SourceId)
            .FirstOrDefault();
    }

    private static async Task<IReadOnlyList<Guid>> ResolveAddressesAsync(Decision decision, CancellationToken cancellationToken)
    {
        var relationships = await decision.GetRelationshipsAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. relationships
                .Where(r => string.Equals(r.RelationshipKind, GovernanceRelationshipKinds.Addresses, StringComparison.Ordinal))
                .Where(r => r.SourceId == decision.Id)
                .OrderBy(r => r.CreatedAt)
                .Select(r => r.TargetId)
                .Distinct(),
        ];
    }
}
