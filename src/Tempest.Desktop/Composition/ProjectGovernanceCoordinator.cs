using Tempest.App.Projects;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// The open project's own risks, issues and decisions — raised, owned,
/// scored, moved and edited. Extracted from <c>MainWindow</c> by `WP-G`
/// (`TD-109`, audit finding `F-05`).
/// </summary>
/// <remarks>
/// <para>
/// A collaborator under `ADR-0103`, on the same terms as
/// <see cref="ProjectDeliveryCoordinator"/>: constructed once by
/// <c>MainWindow</c>, declaring only what it needs, never DI-registered,
/// never referencing <c>MainWindow</c> or a sibling back.
/// </para>
/// <para>
/// <b>One domain service, and only one.</b> Everything here goes through
/// <see cref="IProjectGovernanceService"/>. Risks, issues and decisions
/// share a surface, a lifecycle vocabulary and an editor
/// (<see cref="GovernanceFamily"/> selects which family an edit applies
/// to), so splitting them further would separate methods that read as one
/// another's siblings.
/// </para>
/// <para>
/// <b>Moved verbatim.</b> Prompts, messages, identifier schemes,
/// <see langword="try"/>/<see langword="catch"/> blocks, refreshes and
/// ordering are exactly as they were; the only edits were field access
/// becoming constructor-injected dependencies and <c>RecordHistory(…)</c>
/// becoming the injected delegate.
/// </para>
/// </remarks>
internal sealed class ProjectGovernanceCoordinator
{
    private readonly IProjectContext _projectContext;
    private readonly IProjectGovernanceService _projectGovernance;
    private readonly IProjectGovernanceRegister _projectGovernanceRegister;
    private readonly ProjectWorkspaceView _projectWorkspace;
    private readonly InputDialog _inputDialog;
    private readonly ToastHost _toastHost;
    private readonly Action<string> _recordHistory;

    /// <summary>Initialises a new instance of the <see cref="ProjectGovernanceCoordinator"/> class.</summary>
    public ProjectGovernanceCoordinator(
        IProjectContext projectContext,
        IProjectGovernanceService projectGovernance,
        IProjectGovernanceRegister projectGovernanceRegister,
        ProjectWorkspaceView projectWorkspace,
        InputDialog inputDialog,
        ToastHost toastHost,
        Action<string> recordHistory)
    {
        ArgumentNullException.ThrowIfNull(projectContext);
        ArgumentNullException.ThrowIfNull(projectGovernance);
        ArgumentNullException.ThrowIfNull(projectGovernanceRegister);
        ArgumentNullException.ThrowIfNull(projectWorkspace);
        ArgumentNullException.ThrowIfNull(inputDialog);
        ArgumentNullException.ThrowIfNull(toastHost);
        ArgumentNullException.ThrowIfNull(recordHistory);

        _projectContext = projectContext;
        _projectGovernance = projectGovernance;
        _projectGovernanceRegister = projectGovernanceRegister;
        _projectWorkspace = projectWorkspace;
        _inputDialog = inputDialog;
        _toastHost = toastHost;
        _recordHistory = recordHistory;
    }

    /// <summary>Raises a risk in the open project, prompting for its title.</summary>
    /// <remarks>
    /// The identifier is derived from how many risks the project already
    /// has, matching how tasks and projects suggest theirs. It is a
    /// suggestion, not an identity scheme — the risk's own Guid is its
    /// identity.
    /// </remarks>
    public async Task CreateProjectRiskAsync(CancellationToken cancellationToken = default)
    {
        if (_projectContext.Current is not { } project)
            return;

        var title = await _inputDialog.PromptAsync(
            "Raise Risk",
            $"What might go wrong in {project.Label}?",
            validate: value => value.Length > 200 ? "Title is too long (200 characters max)." : null).ConfigureAwait(true);

        if (title is null)
            return;

        var existing = await _projectGovernanceRegister.ListRisksAsync(project.Id, cancellationToken).ConfigureAwait(true);
        var identifier = $"RSK-{existing.Count + 1:D3}";

        try
        {
            await _projectGovernance.CreateRiskAsync(project.Id, identifier, title, cancellationToken: cancellationToken).ConfigureAwait(true);
            _toastHost.Show($"Raised {identifier} — {title}.", FeedbackSeverity.Success);
            _recordHistory($"Raised risk {identifier} in {project.Label}.");
        }
        catch (ProjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Raises an issue in the open project, prompting for its title.</summary>
    public async Task CreateProjectIssueAsync(CancellationToken cancellationToken = default)
    {
        if (_projectContext.Current is not { } project)
            return;

        var title = await _inputDialog.PromptAsync(
            "Raise Issue",
            $"What has gone wrong in {project.Label}?",
            validate: value => value.Length > 200 ? "Title is too long (200 characters max)." : null).ConfigureAwait(true);

        if (title is null)
            return;

        var existing = await _projectGovernanceRegister.ListIssuesAsync(project.Id, cancellationToken).ConfigureAwait(true);
        var identifier = $"ISS-{existing.Count + 1:D3}";

        try
        {
            await _projectGovernance.CreateIssueAsync(project.Id, identifier, title, cancellationToken: cancellationToken).ConfigureAwait(true);
            _toastHost.Show($"Raised {identifier} — {title}.", FeedbackSeverity.Success);
            _recordHistory($"Raised issue {identifier} in {project.Label}.");
        }
        catch (ProjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Proposes a decision in the open project, prompting for its title and rationale.</summary>
    /// <remarks>
    /// The rationale is prompted for rather than defaulted, because a
    /// decision log whose reasons are auto-filled records nothing worth
    /// keeping. Cancelling the second prompt abandons the decision.
    /// </remarks>
    public async Task CreateProjectDecisionAsync(CancellationToken cancellationToken = default)
    {
        if (_projectContext.Current is not { } project)
            return;

        var title = await _inputDialog.PromptAsync(
            "Propose Decision",
            $"What is being decided in {project.Label}?",
            validate: value => value.Length > 200 ? "Title is too long (200 characters max)." : null).ConfigureAwait(true);

        if (title is null)
            return;

        var rationale = await _inputDialog.PromptAsync(
            "Decision Rationale",
            "Why is this the right call?",
            validate: value => value.Length > 1000 ? "Rationale is too long (1000 characters max)." : null).ConfigureAwait(true);

        if (rationale is null)
            return;

        var existing = await _projectGovernanceRegister.ListDecisionsAsync(project.Id, cancellationToken).ConfigureAwait(true);
        var identifier = $"DEC-{existing.Count + 1:D3}";

        try
        {
            await _projectGovernance.CreateDecisionAsync(project.Id, identifier, title, rationale, cancellationToken: cancellationToken).ConfigureAwait(true);
            _toastHost.Show($"Proposed {identifier} — {title}.", FeedbackSeverity.Success);
            _recordHistory($"Proposed decision {identifier} in {project.Label}.");
        }
        catch (ProjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Moves a risk to <paramref name="target"/>, reporting a refused transition rather than swallowing it.</summary>
    public async Task ChangeProjectRiskStatusAsync(Guid riskId, RiskStatus target, CancellationToken cancellationToken = default)
    {
        try
        {
            await _projectGovernance.ChangeRiskStatusAsync(riskId, target, cancellationToken).ConfigureAwait(true);
            _toastHost.Show($"Risk moved to {ProjectRisksView.Describe(target)}.", FeedbackSeverity.Success);
            _recordHistory($"Risk moved to {ProjectRisksView.Describe(target)}.");
        }
        catch (InvalidRiskStatusTransitionException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }
        catch (GovernanceObjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Moves an issue to <paramref name="target"/>.</summary>
    public async Task ChangeProjectIssueStatusAsync(Guid issueId, IssueStatus target, CancellationToken cancellationToken = default)
    {
        try
        {
            await _projectGovernance.ChangeIssueStatusAsync(issueId, target, cancellationToken).ConfigureAwait(true);
            _toastHost.Show($"Issue moved to {ProjectRisksView.Describe(target)}.", FeedbackSeverity.Success);
            _recordHistory($"Issue moved to {ProjectRisksView.Describe(target)}.");
        }
        catch (InvalidIssueStatusTransitionException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }
        catch (GovernanceObjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Moves a decision to <paramref name="target"/>, recording the current principal as the decider (`ADR-0116`).</summary>
    public async Task DecideProjectDecisionAsync(Guid decisionId, DecisionStatus target, CancellationToken cancellationToken = default)
    {
        try
        {
            await _projectGovernance.DecideAsync(decisionId, target, cancellationToken).ConfigureAwait(true);
            _toastHost.Show($"Decision moved to {ProjectRisksView.Describe(target)}.", FeedbackSeverity.Success);
            _recordHistory($"Decision moved to {ProjectRisksView.Describe(target)}.");
        }
        catch (InvalidDecisionStatusTransitionException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }
        catch (GovernanceObjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Gives a risk to whoever is using the application right now (`ADR-0116`).</summary>
    public async Task OwnProjectRiskAsync(Guid riskId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _projectGovernance.AssignRiskToCurrentPrincipalAsync(riskId, cancellationToken).ConfigureAwait(true);
            _toastHost.Show("Risk assigned to you.", FeedbackSeverity.Success);
        }
        catch (GovernanceObjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Assigns an issue to whoever is using the application right now (`ADR-0116`).</summary>
    public async Task AssignProjectIssueToMeAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _projectGovernance.AssignIssueToCurrentPrincipalAsync(issueId, cancellationToken).ConfigureAwait(true);
            _toastHost.Show("Issue assigned to you.", FeedbackSeverity.Success);
        }
        catch (GovernanceObjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Scores a risk, prompting for likelihood and then severity.</summary>
    /// <remarks>
    /// Both axes are prompted for together, because the domain sets them
    /// together: a risk carrying a fresh severity against a stale likelihood
    /// is worse than one that is honestly unscored.
    /// </remarks>
    public async Task ScoreProjectRiskAsync(Guid riskId, CancellationToken cancellationToken = default)
    {
        var likelihood = await _inputDialog.PromptAsync(
            "Score Risk",
            "How likely is it? (your team's own scale)",
            validate: value => value.Length > 60 ? "Too long (60 characters max)." : null).ConfigureAwait(true);

        if (likelihood is null)
            return;

        var severity = await _inputDialog.PromptAsync(
            "Score Risk",
            "How bad would it be? (your team's own scale)",
            validate: value => value.Length > 60 ? "Too long (60 characters max)." : null).ConfigureAwait(true);

        if (severity is null)
            return;

        try
        {
            await _projectGovernance.ScoreRiskAsync(riskId, likelihood, severity, cancellationToken).ConfigureAwait(true);
            _toastHost.Show("Risk scored.", FeedbackSeverity.Success);
        }
        catch (GovernanceObjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Retitles a risk, issue or decision.</summary>
    public async Task EditProjectGovernanceObjectAsync(Guid objectId, GovernanceFamily family, CancellationToken cancellationToken = default)
    {
        var title = await _inputDialog.PromptAsync(
            $"Edit {family}",
            "What should it be called?",
            validate: value => value.Length > 200 ? "Title is too long (200 characters max)." : null).ConfigureAwait(true);

        if (title is null)
            return;

        try
        {
            switch (family)
            {
                case GovernanceFamily.Risk:
                    await _projectGovernance.EditRiskAsync(objectId, title, cancellationToken: cancellationToken).ConfigureAwait(true);
                    break;
                case GovernanceFamily.Issue:
                    await _projectGovernance.EditIssueAsync(objectId, title, cancellationToken: cancellationToken).ConfigureAwait(true);
                    break;
                default:
                    await _projectGovernance.EditDecisionAsync(objectId, title, cancellationToken: cancellationToken).ConfigureAwait(true);
                    break;
            }

            _toastHost.Show($"{family} renamed.", FeedbackSeverity.Success);
        }
        catch (GovernanceObjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }
}
