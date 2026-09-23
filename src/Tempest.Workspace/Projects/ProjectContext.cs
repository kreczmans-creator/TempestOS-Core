using System.Text.Json;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Settings;

namespace Tempest.Workspace.Projects;

/// <summary>
/// The concrete <see cref="IProjectContext"/> — holds the current project,
/// publishes every change through the existing <see cref="IEventBus"/>,
/// and persists the last-open project through
/// <see cref="ISettingsProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Introduces no new pub/sub or persistence mechanism: the event bus and
/// the settings substrate are the ones `WorkspaceState`/`SelectionService`
/// already established (`ADR-0064`). Only the project's own <b>Id</b> is
/// persisted — never a copy of its name or status, which would be a
/// second, drifting source of truth for data the domain already owns.
/// </para>
/// <para>
/// <b>Project-scoped eager materialisation (`TD-88`, `WP 21.5B` Scope #2).</b>
/// Whichever project becomes <see cref="Current"/> — through
/// <see cref="OpenAsync"/> or through <see cref="LoadAsync"/> restoring the
/// last-open one — has its own subtree materialised eagerly, through
/// <paramref name="repository"/>'s own <c>MaterialiseSubtreeAsync</c>,
/// before this method returns: "the objects a user is about to touch"
/// (the brief's own phrase) is exactly what opening a project reveals.
/// <see cref="CloseAsync"/> releases nothing — this Work Package ships no
/// eviction — so a project visited once stays materialised for the rest of
/// the session even after it is no longer current.
/// </para>
/// </remarks>
public sealed class ProjectContext : IProjectContext
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> the last-open project is stored under.</summary>
    public const string SettingKey = "Workspace.CurrentProject";

    private readonly IProjectDirectory _directory;
    private readonly IEventBus _eventBus;
    private readonly ISettingsProvider _settingsProvider;
    private readonly SettingsDocument<CurrentProjectDto> _document;
    private readonly IEngineeringObjectRepository? _repository;
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="ProjectContext"/> class with no project open.</summary>
    /// <param name="directory">Where a project's own summary is read from.</param>
    /// <param name="eventBus">Where <see cref="ProjectContextChangedEvent"/> is published.</param>
    /// <param name="settingsProvider">Where the last-open project id is persisted.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <param name="repository">
    /// The engineering object repository whose <c>MaterialiseSubtreeAsync</c>
    /// this context calls when a project becomes current (`TD-88`,
    /// `WP 21.5B` Scope #2). <see langword="null"/> — the default — is a
    /// legitimate no-op: a caller with no Engineering Domain composed (most
    /// tests of this class alone) simply gets no eager materialisation,
    /// exactly as if this Work Package had not shipped.
    /// </param>
    /// <exception cref="ArgumentNullException">Any required parameter is <see langword="null"/>.</exception>
    public ProjectContext(
        IProjectDirectory directory, IEventBus eventBus, ISettingsProvider settingsProvider, ILogger? logger = null,
        IEngineeringObjectRepository? repository = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(settingsProvider);

        _directory = directory;
        _eventBus = eventBus;
        _settingsProvider = settingsProvider;
        _logger = logger;
        _repository = repository;

        _document = new SettingsDocument<CurrentProjectDto>(settingsProvider, SettingKey, "Current Project", logger);
    }

    /// <inheritdoc />
    public ProjectSummary? Current { get; private set; }

    /// <inheritdoc />
    public bool HasProject => Current is not null;

    /// <inheritdoc />
    public async Task OpenAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _directory.FindAsync(projectId, cancellationToken).ConfigureAwait(false)
            ?? throw new ProjectNotFoundException(projectId);

        if (Current?.Id == project.Id)
        {
            // Already open — refresh the snapshot in place rather than
            // publishing a change nothing actually changed.
            Current = project;
            return;
        }

        await SetCurrentAsync(project, cancellationToken).ConfigureAwait(false);
        _logger?.Information($"Project opened: '{project.Label}' ({project.Id}).");
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Current is null)
            return;

        await SetCurrentAsync(null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>`TD-176`.</b> Two callers overlap around the New Project +
    /// quotation journey: <c>ProjectBrowserView.CreateAsync</c>'s own
    /// "open right up" (<c>ProjectOpened</c>, wired fire-and-forget to
    /// <c>MainWindow.RenderCurrentModuleAsync</c> in
    /// <c>MainWindowComposer.Wire.cs</c>) and, separately, every other
    /// fire-and-forget <c>ProjectWorkspaceView.RefreshAsync</c> reachable
    /// from the same redirect chain — each begins with this method, and
    /// nothing before this fix stopped two of them running at once
    /// against the same <see cref="Current"/>. Before this fix, whichever
    /// call's own <see cref="IProjectDirectory.FindAsync"/> happened to
    /// return <em>last</em> decided <see cref="Current"/> outright — so a
    /// call that read the project before it was visible to that
    /// particular caller (a transient "not found", not a real deletion)
    /// could close a context a second, later call had already opened
    /// correctly, moments after opening it.
    /// <para>
    /// The fix: a generation token, bumped at the start of every call.
    /// Only the call that is still the <em>latest</em> one started when
    /// its own read comes back is allowed to act on that read — an
    /// overlapping call that finishes after a newer one has already
    /// moved on discards its own result instead of undoing the newer
    /// one's work, whether that discarded result was a project or
    /// nothing at all. The same check also refuses to act once
    /// <see cref="Current"/> no longer names the project this call went
    /// looking for (an <see cref="OpenAsync"/> or <see cref="CloseAsync"/>
    /// ran while this call's own read was in flight) — reaching the
    /// "not found" branch therefore means a genuine deletion, never a
    /// losing race against a just-created project's own write.
    /// </para>
    /// </remarks>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (Current is null)
            return;

        var wantId = Current.Id;
        var generation = Interlocked.Increment(ref _refreshGeneration);

        var refreshed = await _directory.FindAsync(wantId, cancellationToken).ConfigureAwait(false);

        // Stale: a later `RefreshAsync` call, or an `OpenAsync`/
        // `CloseAsync`, has already moved `Current` on since this call's
        // own read started. This call's result — found or not — must not
        // overwrite what the newer one already decided.
        if (generation != _refreshGeneration || Current?.Id != wantId)
            return;

        if (refreshed is null)
        {
            // The open project has genuinely been deleted — close rather
            // than keep serving a snapshot of something that no longer
            // exists.
            await SetCurrentAsync(null, cancellationToken).ConfigureAwait(false);
            return;
        }

        Current = refreshed;
    }

    /// <summary>
    /// Bumped at the start of every <see cref="RefreshAsync"/> call
    /// (`TD-176`) — the mechanism that lets a call recognise its own read
    /// as stale once a later call has already run.
    /// </summary>
    private int _refreshGeneration;

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _document.SaveAsync(new CurrentProjectDto(Current?.Id), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var dto = await _document.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (dto?.ProjectId is not { } projectId)
            return;

        // The saved project may have been deleted between sessions.
        var project = await _directory.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            _logger?.Warning($"Last-open project '{projectId}' no longer exists — starting with no project open.");
            return;
        }

        await SetCurrentAsync(project, cancellationToken).ConfigureAwait(false);
    }

    private async Task SetCurrentAsync(ProjectSummary? project, CancellationToken cancellationToken)
    {
        var previous = Current;
        Current = project;

        // `TD-88`/`WP 21.5B` Scope #2: materialised before the change is
        // announced, so a subscriber that renders on
        // `ProjectContextChangedEvent` (every project-scoped view) finds
        // the subtree already live rather than paying per-node lazy loads
        // during its own first render. A `null` project (closing) or no
        // repository composed (most tests of this class alone) is a no-op.
        if (project is not null && _repository is not null)
            await _repository.MaterialiseSubtreeAsync(project.Id, cancellationToken).ConfigureAwait(false);

        await _eventBus.PublishAsync(new ProjectContextChangedEvent(previous, project), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The plain, JSON-serializable shape this context persists — the Id only, never a copy of domain-owned data.</summary>
    private sealed record CurrentProjectDto(Guid? ProjectId);
}
