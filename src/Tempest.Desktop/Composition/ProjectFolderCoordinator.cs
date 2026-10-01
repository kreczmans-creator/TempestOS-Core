using Avalonia.Threading;
using Tempest.Core.Events;
using Tempest.Core.Projects;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Generates the open project's own Windows Explorer folder whenever a
/// project becomes current — which is what happens straight after one is
/// created (every New Project path opens the project it made) and every
/// time one is opened, so a folder that is missing (a project created
/// before this existed, a folder deleted by hand, a different computer) is
/// put back (PO decision 2026-10-01).
/// </summary>
/// <remarks>
/// <para>
/// <b>One subscription, not one call per creation path.</b> Projects are
/// created from Home, from the Projects area and by the command palette,
/// and every one of those paths ends by opening the new project — after
/// its client has been set, so the folder is filed under the right
/// customer. Listening for <see cref="ProjectContextChangedEvent"/> covers
/// them all without touching any of them, and is idempotent:
/// <see cref="ProjectFolderService.Ensure"/> finds before it creates.
/// </para>
/// <para>
/// <b>Reported, never modal, never blocking.</b> The folder is generated
/// off the UI thread so opening a project never waits on a slow drive. A
/// created folder or a refusal is reported through the shell's own
/// <see cref="ActionOutcomeReporter"/> (status bar, toast, history); a
/// folder that already existed says nothing. "Unavailable" (no D: drive)
/// is reported once per session, and only when a root is actually
/// configured — a non-Windows session, or one with generation switched
/// off, stays silent.
/// </para>
/// </remarks>
internal sealed class ProjectFolderCoordinator : IEventHandler<ProjectContextChangedEvent>
{
    private readonly ProjectFolderLocator _locator;
    private readonly Func<string, ActionOutcome, Task> _report;
    private bool _reportedUnavailable;

    /// <summary>Initialises a new instance of the <see cref="ProjectFolderCoordinator"/> class.</summary>
    /// <param name="locator">Finds or creates each project's folder.</param>
    /// <param name="report">The shell's own report tail — <see cref="ActionOutcomeReporter.ReportAsync(string, ActionOutcome, Func{Task}?)"/>.</param>
    public ProjectFolderCoordinator(ProjectFolderLocator locator, Func<string, ActionOutcome, Task> report)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(report);

        _locator = locator;
        _report = report;
    }

    /// <inheritdoc />
    public Task HandleAsync(ProjectContextChangedEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (@event.Current is not { } current || @event.Previous?.Id == current.Id)
            return Task.CompletedTask;

        _ = EnsureAsync(current.Id);
        return Task.CompletedTask;
    }

    /// <summary>Ensures <paramref name="projectId"/>'s folder exists and reports the outcome — exposed so a caller (or a test) can await it directly.</summary>
    internal async Task<ProjectFolderOutcome?> EnsureAsync(Guid projectId)
    {
        ProjectFolderOutcome outcome;
        try
        {
            outcome = await Task.Run(() => _locator.EnsureAsync(projectId)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            outcome = new ProjectFolderOutcome(ProjectFolderStatus.Failed, $"Project folder could not be created: {ex.Message}", null, []);
        }

        (string Message, ActionOutcome Outcome)? report = outcome.Status switch
        {
            ProjectFolderStatus.Created => (outcome.Message, ActionOutcome.NoChange),
            ProjectFolderStatus.Failed => (outcome.Message, ActionOutcome.Failed),
            ProjectFolderStatus.Unavailable when !_reportedUnavailable && !string.IsNullOrWhiteSpace(_locator.Service.Options.Root) =>
                (outcome.Message, ActionOutcome.Failed),
            _ => null,
        };

        if (outcome.Status == ProjectFolderStatus.Unavailable && report is not null)
            _reportedUnavailable = true;

        if (report is { } r)
            await Dispatcher.UIThread.InvokeAsync(() => _report(r.Message, r.Outcome));

        return outcome;
    }
}
