using Tempest.Workspace.Projects;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Settings;

namespace Tempest.Core.Tests.Shell;

/// <summary>
/// `TD-176`: two overlapping <see cref="ProjectContext.RefreshAsync"/>
/// calls, reproduced deterministically against a controllable
/// <see cref="IProjectDirectory"/> rather than by racing the real one —
/// the shape the New Project + quotation journey exposed (several
/// fire-and-forget renders in flight from the same redirect burst, each
/// starting with <c>ProjectContext.RefreshAsync</c>).
/// </summary>
/// <remarks>
/// Checked by reverting: with the generation token removed from
/// <see cref="ProjectContext.RefreshAsync"/> (the pre-`WP 19.10B` shape,
/// where a "not found" read unconditionally closed the context), this
/// test's own final assertion fails — <c>Current</c> comes back
/// <see langword="null"/> instead of naming the project a later,
/// already-completed refresh had confirmed open.
/// </remarks>
public sealed class ProjectContextRefreshRaceTests
{
    [Fact]
    public async Task RefreshAsync_StaleOverlappingRefresh_DoesNotCloseAContextALaterRefreshAlreadyConfirmedOpen()
    {
        var project = new ProjectSummary(Guid.NewGuid(), "P-0001", "Race Project", LifecycleState.Draft, null);
        var directory = new GatedProjectDirectory(project);
        var eventBus = new EventBus();
        var settings = new SettingsProvider(new Tempest.Core.Tests.Settings.InMemoryPersistenceStore(), new EventBus());
        var context = new ProjectContext(directory, eventBus, settings);

        await context.OpenAsync(project.Id);
        Assert.Equal(project.Id, context.Current?.Id);

        // The first refresh below is *started* first, but its own read of
        // the directory is gated to resolve *last*, and to find nothing —
        // the losing side of the race. The second is started second, but
        // resolves first, and does find the project — the winning side,
        // exactly as `ProjectWorkspaceView.RefreshAsync`'s own overlapping
        // fire-and-forget calls raced in the real journey.
        var firstFind = directory.GateNextFind();
        var secondFind = directory.GateNextFind();

        var firstRefresh = context.RefreshAsync();
        var secondRefresh = context.RefreshAsync();

        secondFind.SetResult(project);
        await secondRefresh;
        Assert.NotNull(context.Current);
        Assert.Equal(project.Id, context.Current!.Id);

        firstFind.SetResult(null);
        await firstRefresh;

        // The stale, losing "not found" must not close a context a later,
        // already-completed refresh has already confirmed open — this is
        // `TD-176`: reverting the generation token in
        // `ProjectContext.RefreshAsync` makes this assertion fail, with
        // `Current` coming back null.
        Assert.NotNull(context.Current);
        Assert.Equal(project.Id, context.Current!.Id);
    }

    /// <summary>
    /// A controllable <see cref="IProjectDirectory"/> test double: each
    /// call to <see cref="GateNextFind"/> reserves the next
    /// <see cref="IProjectDirectory.FindAsync"/> call's own result, so a
    /// test can decide exactly when, and in what order, two overlapping
    /// reads resolve. A <see cref="FindAsync"/> call made once every
    /// gate has been consumed falls back to a plain, immediate lookup —
    /// what <see cref="ProjectContext.OpenAsync"/>'s own initial read
    /// above uses.
    /// </summary>
    private sealed class GatedProjectDirectory : IProjectDirectory
    {
        private readonly ProjectSummary _project;
        private readonly Queue<TaskCompletionSource<ProjectSummary?>> _gatedFinds = new();

        public GatedProjectDirectory(ProjectSummary project) => _project = project;

        public TaskCompletionSource<ProjectSummary?> GateNextFind()
        {
            var gate = new TaskCompletionSource<ProjectSummary?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _gatedFinds.Enqueue(gate);
            return gate;
        }

        public Task<ProjectSummary?> FindAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            _gatedFinds.Count > 0
                ? _gatedFinds.Dequeue().Task
                : Task.FromResult(projectId == _project.Id ? _project : null);

        public Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectSummary>>([_project]);

        public Task<ProjectSummary> CreateAsync(string identifier, string displayName, string? description = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not needed for this focused ProjectContext race test.");

        public Task<IReadOnlyList<Guid>> ListProjectContentsAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
