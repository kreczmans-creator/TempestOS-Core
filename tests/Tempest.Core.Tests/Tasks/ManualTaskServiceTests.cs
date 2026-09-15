using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Core.Runtime;
using Tempest.Core.Tasks;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;

namespace Tempest.Core.Tests.Tasks;

/// <summary>
/// <see cref="TaskService"/> (`WP 19.5C`): the create/complete/delete acts
/// on a <see cref="ManualTask"/>, and — added here first, since no test
/// class for this service existed before it (`WP 19.10H`, `TD-179`) — the
/// archived-project guard those three acts now carry, matching the pattern
/// <c>Tempest.Core.Quotations.QuotationService</c> and its four sibling
/// Core services already established.
/// </summary>
/// <remarks>
/// Uses <c>QuotationTestHost</c> rather than a dedicated host: it already
/// registers <see cref="ITaskService"/> (<c>TasksReadModelTests</c> reads
/// it the same way) and every other host builder in this assembly wires
/// the Engineering Disciplines identically.
/// </remarks>
public sealed class ManualTaskServiceTests
{
    /// <summary>
    /// Backdates the project's own close so <c>ClosedOn</c> lands
    /// <paramref name="daysAgo"/> days in the past and the real,
    /// system-clock <see cref="ITaskService"/> under test reads the project
    /// as Archive without needing its own custom clock. Writes directly
    /// through <see cref="Project.SignOffAsync"/> — the internal mutator
    /// <see cref="IProjectLifecycleService.SignOffAsync"/> itself commits
    /// after deciding an act is permitted — rather than through that
    /// service: this file's own fixtures deliberately leave a live
    /// <see cref="ManualTask"/> open under the project so the
    /// archived-project guard can be tested against it afterwards, and
    /// since `WP 20.10E` that same open task is exactly what the service
    /// itself would refuse to sign off over (Product Owner finding D18) —
    /// a rule this file is not about.
    /// </summary>
    private static async Task CloseProjectAsync(ITempestHost host, Guid projectId, int daysAgo)
    {
        var domain = QuotationTestHost.Domain(host);
        var project = (Project)(await domain.Repository.FindAsync(projectId))!;
        var closedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-daysAgo));
        var signOff = new ProjectSignOff(domain.ResolveCurrentPrincipalId(), closedOn, "Closed for the archive test.");
        await project.SignOffAsync(signOff, closedOn);
    }

    [Fact]
    public async Task AnArchivedProject_RefusesANewTask()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);

        try
        {
            QuotationTestHost.SignIn(host);
            var projectId = await QuotationTestHost.CreateProjectAsync(host, "MTASK-ARCH-1");
            await CloseProjectAsync(host, projectId, 91);

            var service = (ITaskService)host.Services!.GetService(typeof(ITaskService));
            var result = await service.CreateAsync("Too late", projectId, dueDate: null);

            Assert.False(result.Succeeded);
            Assert.Equal(TaskRefusal.ProjectArchived, result.Refusal);
            Assert.Contains("archived", result.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Null(result.Task);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task AnArchivedProject_RefusesCompletingATask_AndTheStoreStaysUnchanged()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);

        try
        {
            QuotationTestHost.SignIn(host);
            var projectId = await QuotationTestHost.CreateProjectAsync(host, "MTASK-ARCH-2");

            var service = (ITaskService)host.Services!.GetService(typeof(ITaskService));
            var created = await service.CreateAsync("Balance the impeller", projectId, dueDate: null);
            Assert.True(created.Succeeded);

            await CloseProjectAsync(host, projectId, 91);

            var result = await service.CompleteAsync(created.Task!.Id);
            Assert.False(result.Succeeded);
            Assert.Equal(TaskRefusal.ProjectArchived, result.Refusal);
            Assert.False(result.Task!.Done);

            var reloaded = await QuotationTestHost.Domain(host).Repository.FindAsync(created.Task.Id) as ManualTask;
            Assert.False(reloaded!.Done);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task AnArchivedProject_RefusesDeletingATask_AndTheStoreStaysUnchanged()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);

        try
        {
            QuotationTestHost.SignIn(host);
            var projectId = await QuotationTestHost.CreateProjectAsync(host, "MTASK-ARCH-3");

            var service = (ITaskService)host.Services!.GetService(typeof(ITaskService));
            var created = await service.CreateAsync("Balance the impeller", projectId, dueDate: null);
            Assert.True(created.Succeeded);

            await CloseProjectAsync(host, projectId, 91);

            var result = await service.DeleteAsync(created.Task!.Id);
            Assert.False(result.Succeeded);
            Assert.Equal(TaskRefusal.ProjectArchived, result.Refusal);

            var reloaded = await QuotationTestHost.Domain(host).Repository.FindAsync(created.Task.Id);
            Assert.NotNull(reloaded);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task AProjectClosedTodayButNotYetArchived_StillAcceptsAWrite()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);

        try
        {
            QuotationTestHost.SignIn(host);
            var projectId = await QuotationTestHost.CreateProjectAsync(host, "MTASK-CLOSED");

            // Not backdated at all: closed today is Closed, not yet
            // Archive (`ProjectArchival.ArchiveAfterDays` = 90) — the
            // write still goes through.
            await CloseProjectAsync(host, projectId, 0);

            var service = (ITaskService)host.Services!.GetService(typeof(ITaskService));
            var result = await service.CreateAsync("Still open for writes", projectId, dueDate: null);

            Assert.True(result.Succeeded, result.Reason);
            Assert.NotNull(result.Task);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
