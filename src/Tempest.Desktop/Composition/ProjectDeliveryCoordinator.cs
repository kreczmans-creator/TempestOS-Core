using System.Globalization;
using Tempest.App.Projects;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// The open project's own tasks, milestones and deliverables — created,
/// assigned, dated and edited. Extracted from <c>MainWindow</c> by `WP-G`
/// (`TD-109`, audit finding `F-05`).
/// </summary>
/// <remarks>
/// <para>
/// A collaborator under `ADR-0103`: constructed once by <c>MainWindow</c>
/// (the composition root), declaring only the dependencies it actually
/// needs, never DI-registered, never referencing <c>MainWindow</c> or a
/// sibling collaborator back. The same shape
/// <see cref="WorkspaceViewCoordinator"/> and
/// <see cref="UndoRedoCoordinator"/> already use.
/// </para>
/// <para>
/// <b>Split from <see cref="ProjectGovernanceCoordinator"/> along the
/// domain service, not by size.</b> Not one method here touches
/// <c>IProjectGovernanceService</c>, and not one method there touches
/// <c>IProjectTaskService</c> or <c>IProjectMilestoneService</c> — the two
/// halves were already disjoint inside <c>MainWindow</c>, which is what
/// made the seam obvious. A single 533-line collaborator would have been
/// the largest file in this folder and would have relocated the god object
/// rather than decomposed it.
/// </para>
/// <para>
/// <b>Moved verbatim.</b> Every prompt, message, identifier scheme,
/// <see langword="try"/>/<see langword="catch"/>, refresh and ordering is
/// exactly as it was in <c>MainWindow</c>; the only edits were field access
/// becoming constructor-injected dependencies and <c>RecordHistory(…)</c>
/// becoming the injected delegate. This reporting family deliberately does
/// <i>not</i> use <see cref="ActionOutcomeReporter"/> — it carries no
/// <see cref="ActionOutcome"/> and refreshes no dependent surfaces, which
/// is `TD-111`'s recorded reason for leaving it alone.
/// </para>
/// </remarks>
internal sealed class ProjectDeliveryCoordinator
{
    private readonly IProjectContext _projectContext;
    private readonly IProjectTaskService _projectTasks;
    private readonly IProjectTaskRegister _projectTaskRegister;
    private readonly IProjectMilestoneService _projectMilestones;
    private readonly IProjectMilestoneRegister _projectMilestoneRegister;
    private readonly ProjectWorkspaceView _projectWorkspace;
    private readonly InputDialog _inputDialog;
    private readonly ToastHost _toastHost;
    private readonly Action<string> _recordHistory;

    /// <summary>Initialises a new instance of the <see cref="ProjectDeliveryCoordinator"/> class.</summary>
    public ProjectDeliveryCoordinator(
        IProjectContext projectContext,
        IProjectTaskService projectTasks,
        IProjectTaskRegister projectTaskRegister,
        IProjectMilestoneService projectMilestones,
        IProjectMilestoneRegister projectMilestoneRegister,
        ProjectWorkspaceView projectWorkspace,
        InputDialog inputDialog,
        ToastHost toastHost,
        Action<string> recordHistory)
    {
        ArgumentNullException.ThrowIfNull(projectContext);
        ArgumentNullException.ThrowIfNull(projectTasks);
        ArgumentNullException.ThrowIfNull(projectTaskRegister);
        ArgumentNullException.ThrowIfNull(projectMilestones);
        ArgumentNullException.ThrowIfNull(projectMilestoneRegister);
        ArgumentNullException.ThrowIfNull(projectWorkspace);
        ArgumentNullException.ThrowIfNull(inputDialog);
        ArgumentNullException.ThrowIfNull(toastHost);
        ArgumentNullException.ThrowIfNull(recordHistory);

        _projectContext = projectContext;
        _projectTasks = projectTasks;
        _projectTaskRegister = projectTaskRegister;
        _projectMilestones = projectMilestones;
        _projectMilestoneRegister = projectMilestoneRegister;
        _projectWorkspace = projectWorkspace;
        _inputDialog = inputDialog;
        _toastHost = toastHost;
        _recordHistory = recordHistory;
    }

    /// <summary>Creates a task in the open project, prompting for its title.</summary>
    /// <remarks>
    /// The identifier is derived from the count of tasks already in the
    /// project, matching how <c>ProjectBrowserView</c> suggests a project
    /// identifier. It is a suggestion the user never has to think about,
    /// not an identity scheme — the task's own Guid is its identity.
    /// </remarks>
    public async Task CreateProjectTaskAsync(CancellationToken cancellationToken = default)
    {
        if (_projectContext.Current is not { } project)
            return;

        var title = await _inputDialog.PromptAsync(
            "New Task",
            $"What needs doing in {project.Label}?",
            validate: value => value.Length > 200 ? "Title is too long (200 characters max)." : null).ConfigureAwait(true);

        if (title is null)
            return;

        var existing = await _projectTaskRegister.ListAsync(project.Id, cancellationToken).ConfigureAwait(true);
        var identifier = $"TSK-{existing.Count + 1:D3}";

        try
        {
            await _projectTasks.CreateAsync(project.Id, identifier, title, cancellationToken: cancellationToken).ConfigureAwait(true);
            _toastHost.Show($"Created {identifier} — {title}.", FeedbackSeverity.Success);
            _recordHistory($"Created task {identifier} in {project.Label}.");
        }
        catch (ProjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Sets a milestone in the open project, prompting for its title and target date.</summary>
    /// <remarks>
    /// The date is typed rather than picked, and parsed strictly as
    /// <c>yyyy-MM-dd</c>: a milestone whose date was silently reinterpreted
    /// by the machine's own locale would be worse than one the user had to
    /// retype. A date that will not parse is refused by the dialog's own
    /// validation rather than being guessed at.
    /// </remarks>
    public async Task CreateProjectMilestoneAsync(CancellationToken cancellationToken = default)
    {
        if (_projectContext.Current is not { } project)
            return;

        var title = await _inputDialog.PromptAsync(
            "Set Milestone",
            $"What is {project.Label} working to?",
            validate: value => value.Length > 200 ? "Title is too long (200 characters max)." : null).ConfigureAwait(true);

        if (title is null)
            return;

        var typedDate = await _inputDialog.PromptAsync(
            "Milestone Target Date",
            "When is it due? (yyyy-MM-dd)",
            validate: value => ParseTargetDate(value) is null ? "Enter a date as yyyy-MM-dd, for example 2026-11-30." : null).ConfigureAwait(true);

        if (typedDate is null)
            return;

        if (ParseTargetDate(typedDate) is not { } targetDate)
            return;

        var existing = await _projectMilestoneRegister.ListAsync(project.Id, cancellationToken).ConfigureAwait(true);
        var identifier = $"MS-{existing.Count + 1:D3}";

        try
        {
            await _projectMilestones.CreateMilestoneAsync(project.Id, identifier, title, targetDate, cancellationToken: cancellationToken).ConfigureAwait(true);
            _toastHost.Show($"Set {identifier} — {title}.", FeedbackSeverity.Success);
            _recordHistory($"Set milestone {identifier} in {project.Label}.");
        }
        catch (ProjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Adds a deliverable due against a milestone.</summary>
    public async Task AddProjectDeliverableAsync(Guid milestoneId, CancellationToken cancellationToken = default)
    {
        if (_projectContext.Current is not { } project)
            return;

        var title = await _inputDialog.PromptAsync(
            "Add Deliverable",
            "What has to be delivered for this milestone?",
            validate: value => value.Length > 200 ? "Title is too long (200 characters max)." : null).ConfigureAwait(true);

        if (title is null)
            return;

        var existing = await _projectMilestoneRegister.ListAsync(project.Id, cancellationToken).ConfigureAwait(true);
        var identifier = $"DEL-{existing.Sum(m => m.Deliverables.Count) + 1:D3}";

        try
        {
            await _projectMilestones.CreateDeliverableAsync(project.Id, milestoneId, identifier, title, cancellationToken: cancellationToken).ConfigureAwait(true);
            _toastHost.Show($"Added {identifier} — {title}.", FeedbackSeverity.Success);
            _recordHistory($"Added deliverable {identifier} in {project.Label}.");
        }
        catch (MilestoneNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }
        catch (ProjectNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Retitles a milestone.</summary>
    public async Task EditProjectMilestoneAsync(Guid milestoneId, CancellationToken cancellationToken = default)
    {
        var title = await _inputDialog.PromptAsync(
            "Edit Milestone",
            "What should it be called?",
            validate: value => value.Length > 200 ? "Title is too long (200 characters max)." : null).ConfigureAwait(true);

        if (title is null)
            return;

        try
        {
            await _projectMilestones.EditMilestoneAsync(milestoneId, title, cancellationToken: cancellationToken).ConfigureAwait(true);
            _toastHost.Show("Milestone renamed.", FeedbackSeverity.Success);
        }
        catch (MilestoneNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Parses a typed milestone date, or <see langword="null"/> when it is not a valid <c>yyyy-MM-dd</c>.</summary>
    /// <remarks>
    /// Invariant culture and an exact format, deliberately. The date a
    /// project commits to must mean the same thing on every machine that
    /// opens the file.
    /// </remarks>
    internal static DateTimeOffset? ParseTargetDate(string? value) =>
        DateTime.TryParseExact(
            value?.Trim(),
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsed)
            ? new DateTimeOffset(parsed, TimeSpan.Zero)
            : null;

    /// <summary>Assigns a task to whoever is using the application right now (`ADR-0116`).</summary>
    public async Task AssignProjectTaskToMeAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _projectTasks.AssignToCurrentPrincipalAsync(taskId, cancellationToken).ConfigureAwait(true);
            _toastHost.Show("Task assigned to you.", FeedbackSeverity.Success);
        }
        catch (TaskNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Moves a task to <paramref name="target"/>, reporting a refused transition rather than swallowing it.</summary>
    public async Task ChangeProjectTaskWorkStateAsync(Guid taskId, TaskWorkState target, CancellationToken cancellationToken = default)
    {
        try
        {
            await _projectTasks.ChangeWorkStateAsync(taskId, target, cancellationToken).ConfigureAwait(true);
            _toastHost.Show($"Task moved to {ProjectTasksView.Describe(target)}.", FeedbackSeverity.Success);
            _recordHistory($"Task moved to {ProjectTasksView.Describe(target)}.");
        }
        catch (InvalidTaskWorkStateTransitionException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }
        catch (TaskNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Retitles a task.</summary>
    public async Task EditProjectTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var title = await _inputDialog.PromptAsync(
            "Edit Task",
            "New title:",
            validate: value => value.Length > 200 ? "Title is too long (200 characters max)." : null).ConfigureAwait(true);

        if (title is null)
            return;

        try
        {
            await _projectTasks.EditAsync(taskId, title, cancellationToken: cancellationToken).ConfigureAwait(true);
            _toastHost.Show("Task updated.", FeedbackSeverity.Success);
        }
        catch (TaskNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Sets or clears a task's due date.</summary>
    /// <remarks>
    /// An empty answer clears the date rather than being rejected: "this
    /// no longer has a deadline" is a real edit, and a dialog that can
    /// only ever add a date leaves the user unable to undo a mistake.
    /// </remarks>
    public async Task ChangeProjectTaskDueDateAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var answer = await _inputDialog.PromptAsync(
            "Due Date",
            "Due date (yyyy-MM-dd), or blank to clear:",
            validate: value =>
                string.IsNullOrWhiteSpace(value) || DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, out _)
                    ? null
                    : "Enter a date as yyyy-MM-dd, or leave it blank.").ConfigureAwait(true);

        if (answer is null)
            return;

        DateTimeOffset? dueDate = string.IsNullOrWhiteSpace(answer)
            ? null
            : DateTimeOffset.Parse(answer, CultureInfo.InvariantCulture);

        try
        {
            await _projectTasks.SetDueDateAsync(taskId, dueDate, cancellationToken).ConfigureAwait(true);
            _toastHost.Show(dueDate is null ? "Due date cleared." : $"Due {dueDate:yyyy-MM-dd}.", FeedbackSeverity.Success);
        }
        catch (TaskNotFoundException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
        }

        await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
    }
}
