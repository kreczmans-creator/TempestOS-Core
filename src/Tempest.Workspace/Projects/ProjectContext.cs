using System.Text.Json;
using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Settings;

namespace Tempest.App.Projects;

/// <summary>
/// The concrete <see cref="IProjectContext"/> — holds the current project,
/// publishes every change through the existing <see cref="IEventBus"/>,
/// and persists the last-open project through
/// <see cref="ISettingsProvider"/>.
/// </summary>
/// <remarks>
/// Introduces no new pub/sub or persistence mechanism: the event bus and
/// the settings substrate are the ones `WorkspaceState`/`SelectionService`
/// already established (`ADR-0064`). Only the project's own <b>Id</b> is
/// persisted — never a copy of its name or status, which would be a
/// second, drifting source of truth for data the domain already owns.
/// </remarks>
public sealed class ProjectContext : IProjectContext
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> the last-open project is stored under.</summary>
    public const string SettingKey = "Workspace.CurrentProject";

    private readonly IProjectDirectory _directory;
    private readonly IEventBus _eventBus;
    private readonly ISettingsProvider _settingsProvider;
    private readonly SettingsDocument<CurrentProjectDto> _document;
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="ProjectContext"/> class with no project open.</summary>
    /// <exception cref="ArgumentNullException">Any required parameter is <see langword="null"/>.</exception>
    public ProjectContext(IProjectDirectory directory, IEventBus eventBus, ISettingsProvider settingsProvider, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(settingsProvider);

        _directory = directory;
        _eventBus = eventBus;
        _settingsProvider = settingsProvider;
        _logger = logger;

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
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (Current is null)
            return;

        var refreshed = await _directory.FindAsync(Current.Id, cancellationToken).ConfigureAwait(false);
        if (refreshed is null)
        {
            // The open project has been deleted — close rather than keep
            // serving a snapshot of something that no longer exists.
            await SetCurrentAsync(null, cancellationToken).ConfigureAwait(false);
            return;
        }

        Current = refreshed;
    }

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

        await _eventBus.PublishAsync(new ProjectContextChangedEvent(previous, project), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The plain, JSON-serializable shape this context persists — the Id only, never a copy of domain-owned data.</summary>
    private sealed record CurrentProjectDto(Guid? ProjectId);
}
