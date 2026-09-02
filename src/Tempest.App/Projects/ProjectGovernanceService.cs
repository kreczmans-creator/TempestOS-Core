using Tempest.App.Workspace;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Projects;

/// <summary>Creating and changing risks, issues and decisions inside a project.</summary>
public interface IProjectGovernanceService
{
    /// <summary>Raises a risk in <paramref name="projectId"/>.</summary>
    Task<Risk> CreateRiskAsync(
        Guid projectId,
        string identifier,
        string title,
        string? description = null,
        string? likelihood = null,
        string? severity = null,
        string? ownedByPrincipalId = null,
        bool isHazard = false,
        CancellationToken cancellationToken = default);

    /// <summary>Raises an issue in <paramref name="projectId"/>.</summary>
    Task<Issue> CreateIssueAsync(
        Guid projectId,
        string identifier,
        string title,
        string? description = null,
        WorkPriority priority = WorkPriority.Normal,
        string? assignedToPrincipalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Proposes a decision in <paramref name="projectId"/>.</summary>
    Task<Decision> CreateDecisionAsync(
        Guid projectId,
        string identifier,
        string title,
        string rationale,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>Moves <paramref name="riskId"/> to <paramref name="target"/>.</summary>
    Task ChangeRiskStatusAsync(Guid riskId, RiskStatus target, CancellationToken cancellationToken = default);

    /// <summary>Scores <paramref name="riskId"/>.</summary>
    Task ScoreRiskAsync(Guid riskId, string? likelihood, string? severity, CancellationToken cancellationToken = default);

    /// <summary>Gives <paramref name="riskId"/> an owner, or removes it when <see langword="null"/>.</summary>
    Task AssignRiskOwnerAsync(Guid riskId, string? principalId, CancellationToken cancellationToken = default);

    /// <summary>Gives <paramref name="riskId"/> to whoever is using the application right now.</summary>
    Task AssignRiskToCurrentPrincipalAsync(Guid riskId, CancellationToken cancellationToken = default);

    /// <summary>Retitles and/or rewrites <paramref name="riskId"/>.</summary>
    Task EditRiskAsync(Guid riskId, string? title = null, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>Records that <paramref name="riskId"/> materialised as <paramref name="issueId"/>.</summary>
    Task RecordRiskRealisedAsync(Guid riskId, Guid issueId, CancellationToken cancellationToken = default);

    /// <summary>Moves <paramref name="issueId"/> to <paramref name="target"/>.</summary>
    Task ChangeIssueStatusAsync(Guid issueId, IssueStatus target, CancellationToken cancellationToken = default);

    /// <summary>Sets <paramref name="issueId"/>'s priority.</summary>
    Task SetIssuePriorityAsync(Guid issueId, WorkPriority priority, CancellationToken cancellationToken = default);

    /// <summary>Assigns <paramref name="issueId"/>, or unassigns it when <see langword="null"/>.</summary>
    Task AssignIssueAsync(Guid issueId, string? principalId, CancellationToken cancellationToken = default);

    /// <summary>Assigns <paramref name="issueId"/> to whoever is using the application right now.</summary>
    Task AssignIssueToCurrentPrincipalAsync(Guid issueId, CancellationToken cancellationToken = default);

    /// <summary>Retitles and/or rewrites <paramref name="issueId"/>.</summary>
    Task EditIssueAsync(Guid issueId, string? title = null, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>Moves <paramref name="decisionId"/> to <paramref name="target"/>, recording the current principal as the decider.</summary>
    Task DecideAsync(Guid decisionId, DecisionStatus target, CancellationToken cancellationToken = default);

    /// <summary>Rewrites <paramref name="decisionId"/>'s rationale.</summary>
    Task SetDecisionRationaleAsync(Guid decisionId, string rationale, CancellationToken cancellationToken = default);

    /// <summary>Retitles and/or rewrites <paramref name="decisionId"/>.</summary>
    Task EditDecisionAsync(Guid decisionId, string? title = null, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>Records that <paramref name="decisionId"/> was taken about <paramref name="subjectId"/>.</summary>
    Task RecordDecisionAddressesAsync(Guid decisionId, Guid subjectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The risk, issue and decision lifecycles, as the Project Workspace
/// performs them.
/// </summary>
/// <remarks>
/// <para>
/// <b>No second governance model.</b> Every operation here is performed on
/// the real <see cref="Risk"/>, <see cref="Issue"/> or <see cref="Decision"/>
/// in the domain, through the same <see cref="EngineeringObjectFactory{T}"/>
/// every other Kind is created by and the same mutation-then-persist path
/// tasks already use. That is what makes a risk raised here survive a
/// restart (`TD-85`/`TD-104`) without this class knowing anything about
/// persistence.
/// </para>
/// <para>
/// <b>Project membership is set by parenting, not by a field.</b> Creating
/// a risk in a project moves it under that project, so
/// <see cref="ProjectMembership"/> resolves it exactly as it resolves every
/// other object — the third family to be scoped this way, after tasks and
/// documents, and by the same one definition of the phrase.
/// </para>
/// <para>
/// <b>Ownership comes from the principal boundary (`ADR-0116`).</b> The
/// <c>…ToCurrentPrincipal</c> operations read <c>ICurrentPrincipalAccessor</c>
/// through the domain context. No authentication is performed and no
/// permission is checked: ownership is a statement about who is dealing
/// with something, not about who is allowed to.
/// </para>
/// </remarks>
public sealed class ProjectGovernanceService : IProjectGovernanceService
{
    private readonly EngineeringDomainContext _context;
    private readonly Func<DateTimeOffset> _now;

    /// <summary>Initialises a new instance of the <see cref="ProjectGovernanceService"/> class.</summary>
    /// <param name="context">The engineering domain.</param>
    /// <param name="now">
    /// The clock a decision's own <see cref="Decision.DecidedAt"/> is stamped
    /// from. Injectable so a test can state the moment rather than depend on
    /// the instant it runs.
    /// </param>
    public ProjectGovernanceService(EngineeringDomainContext context, Func<DateTimeOffset>? now = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<Risk> CreateRiskAsync(
        Guid projectId,
        string identifier,
        string title,
        string? description = null,
        string? likelihood = null,
        string? severity = null,
        string? ownedByPrincipalId = null,
        bool isHazard = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var project = await RequireProjectAsync(projectId, cancellationToken).ConfigureAwait(false);

        // A Hazard is a Risk, so one operation raises either — the caller
        // says which, and the object gets the Kind that matches its type so
        // rehydration reconstructs the right one.
        var created = isHazard
            ? (Risk)await new EngineeringObjectFactory<Hazard>(
                    CanonicalObjectKinds.Hazard,
                    _context,
                    (document, revision) => new Hazard(
                        document, revision, _context, identifier.Trim(), title.Trim(),
                        EngineeringObjectMetadata.Empty, likelihood, severity, RiskStatus.Open, ownedByPrincipalId))
                .CreateAsync(description ?? DefaultDescription("Hazard", identifier, title), cancellationToken)
                .ConfigureAwait(false)
            : (Risk)await new EngineeringObjectFactory<Risk>(
                    CanonicalObjectKinds.Risk,
                    _context,
                    (document, revision) => new Risk(
                        document, revision, _context, identifier.Trim(), title.Trim(),
                        EngineeringObjectMetadata.Empty, likelihood, severity, RiskStatus.Open, ownedByPrincipalId))
                .CreateAsync(description ?? DefaultDescription("Risk", identifier, title), cancellationToken)
                .ConfigureAwait(false);

        await created.MoveAsync(project.Id, cancellationToken).ConfigureAwait(false);

        return created;
    }

    /// <inheritdoc />
    public async Task<Issue> CreateIssueAsync(
        Guid projectId,
        string identifier,
        string title,
        string? description = null,
        WorkPriority priority = WorkPriority.Normal,
        string? assignedToPrincipalId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var project = await RequireProjectAsync(projectId, cancellationToken).ConfigureAwait(false);

        var factory = new EngineeringObjectFactory<Issue>(
            CanonicalObjectKinds.Issue,
            _context,
            (document, revision) => new Issue(
                document, revision, _context, identifier.Trim(), title.Trim(),
                EngineeringObjectMetadata.Empty, IssueStatus.Open, priority, assignedToPrincipalId));

        var created = (Issue)await factory
            .CreateAsync(description ?? DefaultDescription("Issue", identifier, title), cancellationToken)
            .ConfigureAwait(false);

        await created.MoveAsync(project.Id, cancellationToken).ConfigureAwait(false);

        return created;
    }

    /// <inheritdoc />
    public async Task<Decision> CreateDecisionAsync(
        Guid projectId,
        string identifier,
        string title,
        string rationale,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);

        var project = await RequireProjectAsync(projectId, cancellationToken).ConfigureAwait(false);

        var factory = new EngineeringObjectFactory<Decision>(
            CanonicalObjectKinds.Decision,
            _context,
            (document, revision) => new Decision(
                document, revision, _context, identifier.Trim(), title.Trim(),
                EngineeringObjectMetadata.Empty, rationale.Trim(), DecisionStatus.Proposed));

        var created = (Decision)await factory
            .CreateAsync(description ?? DefaultDescription("Decision", identifier, title), cancellationToken)
            .ConfigureAwait(false);

        await created.MoveAsync(project.Id, cancellationToken).ConfigureAwait(false);

        return created;
    }

    /// <inheritdoc />
    public async Task ChangeRiskStatusAsync(Guid riskId, RiskStatus target, CancellationToken cancellationToken = default) =>
        await (await RequireRiskAsync(riskId, cancellationToken).ConfigureAwait(false))
            .ChangeStatusAsync(target, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task ScoreRiskAsync(Guid riskId, string? likelihood, string? severity, CancellationToken cancellationToken = default) =>
        await (await RequireRiskAsync(riskId, cancellationToken).ConfigureAwait(false))
            .ScoreAsync(likelihood, severity, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task AssignRiskOwnerAsync(Guid riskId, string? principalId, CancellationToken cancellationToken = default) =>
        await (await RequireRiskAsync(riskId, cancellationToken).ConfigureAwait(false))
            .AssignOwnerAsync(principalId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task AssignRiskToCurrentPrincipalAsync(Guid riskId, CancellationToken cancellationToken = default) =>
        AssignRiskOwnerAsync(riskId, _context.ResolveCurrentPrincipalId(), cancellationToken);

    /// <inheritdoc />
    public async Task EditRiskAsync(Guid riskId, string? title = null, string? description = null, CancellationToken cancellationToken = default) =>
        await EditAsync(await RequireRiskAsync(riskId, cancellationToken).ConfigureAwait(false),
            title, description, "Risk description edited.", cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RecordRiskRealisedAsync(Guid riskId, Guid issueId, CancellationToken cancellationToken = default)
    {
        var risk = await RequireRiskAsync(riskId, cancellationToken).ConfigureAwait(false);

        _ = await _context.Repository.FindAsync(issueId, cancellationToken).ConfigureAwait(false) as Issue
            ?? throw new GovernanceObjectNotFoundException(issueId, "Issue");

        await risk.RealisedAsAsync(issueId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ChangeIssueStatusAsync(Guid issueId, IssueStatus target, CancellationToken cancellationToken = default) =>
        await (await RequireIssueAsync(issueId, cancellationToken).ConfigureAwait(false))
            .ChangeStatusAsync(target, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task SetIssuePriorityAsync(Guid issueId, WorkPriority priority, CancellationToken cancellationToken = default) =>
        await (await RequireIssueAsync(issueId, cancellationToken).ConfigureAwait(false))
            .SetPriorityAsync(priority, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task AssignIssueAsync(Guid issueId, string? principalId, CancellationToken cancellationToken = default) =>
        await (await RequireIssueAsync(issueId, cancellationToken).ConfigureAwait(false))
            .AssignAsync(principalId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task AssignIssueToCurrentPrincipalAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        AssignIssueAsync(issueId, _context.ResolveCurrentPrincipalId(), cancellationToken);

    /// <inheritdoc />
    public async Task EditIssueAsync(Guid issueId, string? title = null, string? description = null, CancellationToken cancellationToken = default) =>
        await EditAsync(await RequireIssueAsync(issueId, cancellationToken).ConfigureAwait(false),
            title, description, "Issue description edited.", cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task DecideAsync(Guid decisionId, DecisionStatus target, CancellationToken cancellationToken = default) =>
        await (await RequireDecisionAsync(decisionId, cancellationToken).ConfigureAwait(false))
            .DecideAsync(target, _context.ResolveCurrentPrincipalId(), _now(), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task SetDecisionRationaleAsync(Guid decisionId, string rationale, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);

        await (await RequireDecisionAsync(decisionId, cancellationToken).ConfigureAwait(false))
            .SetRationaleAsync(rationale.Trim(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EditDecisionAsync(Guid decisionId, string? title = null, string? description = null, CancellationToken cancellationToken = default) =>
        await EditAsync(await RequireDecisionAsync(decisionId, cancellationToken).ConfigureAwait(false),
            title, description, "Decision description edited.", cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RecordDecisionAddressesAsync(Guid decisionId, Guid subjectId, CancellationToken cancellationToken = default)
    {
        var decision = await RequireDecisionAsync(decisionId, cancellationToken).ConfigureAwait(false);

        _ = await _context.Repository.FindAsync(subjectId, cancellationToken).ConfigureAwait(false)
            ?? throw new GovernanceObjectNotFoundException(subjectId, "object");

        await decision.AddressesAsync(subjectId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retitles and rewrites any governance object, since all three edit
    /// identically — a rename, then a revision.
    /// </summary>
    /// <remarks>
    /// A rewritten description is a new revision, not an overwrite: what a
    /// risk used to say is part of its history, and the platform already
    /// records that for every other object.
    /// </remarks>
    private static async Task EditAsync(
        EngineeringObjectBase target, string? title, string? description, string revisionNote, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(title))
            await target.RenameAsync(title.Trim(), cancellationToken).ConfigureAwait(false);

        if (description is not null)
            await target.ReviseAsync(description, revisionNote, cancellationToken).ConfigureAwait(false);
    }

    private static string DefaultDescription(string noun, string identifier, string title) =>
        $"{noun} {identifier.Trim()} — {title.Trim()}.";

    private async Task<IEngineeringObject> RequireProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false)
        ?? throw new ProjectNotFoundException(projectId);

    private async Task<Risk> RequireRiskAsync(Guid riskId, CancellationToken cancellationToken) =>
        await _context.Repository.FindAsync(riskId, cancellationToken).ConfigureAwait(false) as Risk
        ?? throw new GovernanceObjectNotFoundException(riskId, "Risk");

    private async Task<Issue> RequireIssueAsync(Guid issueId, CancellationToken cancellationToken) =>
        await _context.Repository.FindAsync(issueId, cancellationToken).ConfigureAwait(false) as Issue
        ?? throw new GovernanceObjectNotFoundException(issueId, "Issue");

    private async Task<Decision> RequireDecisionAsync(Guid decisionId, CancellationToken cancellationToken) =>
        await _context.Repository.FindAsync(decisionId, cancellationToken).ConfigureAwait(false) as Decision
        ?? throw new GovernanceObjectNotFoundException(decisionId, "Decision");
}

/// <summary>Thrown when a governance operation names an object that is not of the family it expects.</summary>
public sealed class GovernanceObjectNotFoundException : InvalidOperationException
{
    /// <summary>Initialises a new instance of the <see cref="GovernanceObjectNotFoundException"/> class.</summary>
    public GovernanceObjectNotFoundException(Guid objectId, string expectedFamily)
        : base($"No {expectedFamily} with Id '{objectId}' exists in this session.")
    {
        ObjectId = objectId;
        ExpectedFamily = expectedFamily;
    }

    /// <summary>The Id that did not resolve.</summary>
    public Guid ObjectId { get; }

    /// <summary>What the operation expected it to be.</summary>
    public string ExpectedFamily { get; }
}

/// <summary>Which of the three governance families an operation is about.</summary>
/// <remarks>
/// Exists so the shell can route one edit action to the right service call
/// without three near-identical handlers, and so the surface's own wording
/// ("Edit Risk", "Edit Decision") comes from one place.
/// </remarks>
public enum GovernanceFamily
{
    /// <summary>A risk or hazard.</summary>
    Risk,

    /// <summary>An issue.</summary>
    Issue,

    /// <summary>A decision.</summary>
    Decision,
}
