using System.Text.Json;
using Tempest.App.Projects;
using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Settings;

namespace Tempest.App.Shell;

/// <summary>
/// The concrete <see cref="IShellNavigator"/> — owns the current
/// <see cref="ShellLocation"/>, keeps <see cref="IProjectContext"/> in step
/// with it, publishes every move on the existing <see cref="IEventBus"/>,
/// and persists the location through <see cref="ISettingsProvider"/>.
/// </summary>
/// <remarks>
/// The one rule this class exists to enforce: <b>a project-scoped location
/// and the current project can never disagree.</b> Both are updated in the
/// same operation, so there is no window in which the shell believes it is
/// inside a project the context has not opened.
/// </remarks>
public sealed class ShellNavigator : IShellNavigator
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> the current location is stored under.</summary>
    public const string SettingKey = "Workspace.ShellLocation";

    private readonly IProjectContext _projectContext;
    private readonly IEventBus _eventBus;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="ShellNavigator"/> class at <see cref="ShellLocation.Home"/>.</summary>
    /// <exception cref="ArgumentNullException">Any required parameter is <see langword="null"/>.</exception>
    public ShellNavigator(IProjectContext projectContext, IEventBus eventBus, ISettingsProvider settingsProvider, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(projectContext);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(settingsProvider);

        _projectContext = projectContext;
        _eventBus = eventBus;
        _settingsProvider = settingsProvider;
        _logger = logger;

        try
        {
            _settingsProvider.RegisterDefinition(new SettingDefinition(SettingKey, "Shell Location", string.Empty));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Idempotent across restarts, as elsewhere.
        }
    }

    /// <inheritdoc />
    public ShellLocation Current { get; private set; } = ShellLocation.Home;

    /// <inheritdoc />
    public Task GoHomeAsync(CancellationToken cancellationToken = default) =>
        MoveToAsync(ShellLocation.Home, cancellationToken);

    /// <inheritdoc />
    public Task GoToProjectsAsync(CancellationToken cancellationToken = default) =>
        MoveToAsync(ShellLocation.Projects, cancellationToken);

    /// <inheritdoc />
    public async Task OpenProjectAsync(Guid projectId, ProjectArea area = ProjectArea.Overview, CancellationToken cancellationToken = default)
    {
        // Open first: if the project does not exist this throws before the
        // location moves, so a failed open can never leave the shell
        // pointing into a project that was never opened.
        await _projectContext.OpenAsync(projectId, cancellationToken).ConfigureAwait(false);
        await MoveToAsync(ShellLocation.ForProject(projectId, area), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task GoToProjectAreaAsync(ProjectArea area, CancellationToken cancellationToken = default)
    {
        var projectId = RequireOpenProject();

        return area == ProjectArea.Engineering
            ? MoveToAsync(ShellLocation.ForEngineering(projectId), cancellationToken)
            : MoveToAsync(ShellLocation.ForProject(projectId, area), cancellationToken);
    }

    /// <inheritdoc />
    public Task GoToEngineeringAsync(CancellationToken cancellationToken = default) =>
        MoveToAsync(ShellLocation.ForEngineering(RequireOpenProject()), cancellationToken);

    /// <inheritdoc />
    public Task ReturnToProjectAsync(CancellationToken cancellationToken = default) =>
        MoveToAsync(ShellLocation.ForProject(RequireOpenProject()), cancellationToken);

    /// <inheritdoc />
    public async Task CloseProjectAsync(CancellationToken cancellationToken = default)
    {
        await _projectContext.CloseAsync(cancellationToken).ConfigureAwait(false);
        await MoveToAsync(ShellLocation.Projects, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(new ShellLocationDto(Current.Area, Current.ProjectId, Current.ProjectArea));
        await _settingsProvider.SetValueAsync(SettingKey, json, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await _settingsProvider.GetValueAsync(SettingKey, cancellationToken).ConfigureAwait(false);

        ShellLocationDto? dto;
        try
        {
            dto = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<ShellLocationDto>(json);
        }
        catch (JsonException)
        {
            // Corrupted state degrades to Home rather than failing startup
            // (`TD-60`'s own established contract).
            dto = null;
        }

        if (dto is null)
            return;

        var restored = new ShellLocation(dto.Area, dto.ProjectId, dto.ProjectArea);

        if (!restored.IsProjectScoped)
        {
            Current = restored;
            return;
        }

        // A project-scoped location is only restorable if its project still
        // exists — otherwise the shell would claim to be inside a project
        // the context could not open.
        if (dto.ProjectId is not { } projectId)
        {
            Current = ShellLocation.Home;
            return;
        }

        try
        {
            await _projectContext.OpenAsync(projectId, cancellationToken).ConfigureAwait(false);
            Current = restored;
        }
        catch (ProjectNotFoundException)
        {
            _logger?.Warning($"Last location referenced project '{projectId}', which no longer exists — starting at Home.");
            Current = ShellLocation.Home;
        }
    }

    private Guid RequireOpenProject() =>
        _projectContext.Current?.Id
        ?? throw new InvalidOperationException("No project is open. Open a project before navigating to a project-scoped area.");

    private async Task MoveToAsync(ShellLocation destination, CancellationToken cancellationToken)
    {
        if (Current == destination)
            return;

        var previous = Current;
        Current = destination;

        await _eventBus.PublishAsync(new ShellLocationChangedEvent(previous, destination), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The plain, JSON-serializable shape this navigator persists.</summary>
    private sealed record ShellLocationDto(ShellArea Area, Guid? ProjectId, ProjectArea? ProjectArea);
}
