using Tempest.Workspace;
using Tempest.Workspace.Verification;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Workspace.DashboardExport;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// Proves `ADR-0155`: project health is <see cref="EngineeringCockpit"/>'s
/// own health rollup scoped to a Project, computed by the same discipline
/// read-models over only the objects that Project owns
/// (<see cref="Tempest.Workspace.Projects.ProjectMembership"/>), reported
/// per Project as <see cref="CockpitProjectHealth"/> — while the
/// workspace-wide <see cref="EngineeringCockpit.Health"/> keeps meaning
/// exactly what it always did.
/// </summary>
public class ProjectHealthTests
{
    /// <summary>Three Projects: A owns a Failed activity and an overdue task (Blocked); B owns nothing (Unknown); C owns a Passed activity (Healthy). One Failed activity is standalone — in no Project at all.</summary>
    private static async Task<(Project A, Project B, Project C)> SeedAsync(ITempestHost host)
    {
        var domain = DashboardExportTestHost.Domain(host);
        var registry = new VerificationActivityFactoryRegistry(domain);
        var verification = DashboardExportTestHost.Verification(host);

        var a = await DashboardExportTestHost.CreateProjectAsync(host, "PH-A", "Blocked Project");
        var b = await DashboardExportTestHost.CreateProjectAsync(host, "PH-B", "Empty Project");
        var c = await DashboardExportTestHost.CreateProjectAsync(host, "PH-C", "Healthy Project");

        var failedInA = await registry.CreateAsync("Failed in A", "Verifies something in A.", Guid.NewGuid(), "Test", parentId: a.Id);
        await verification.RecordAsync(failedInA.Id, VerificationOutcome.Fail, "Test", new VerificationContext());

        var passedInC = await registry.CreateAsync("Passed in C", "Verifies something in C.", Guid.NewGuid(), "Test", parentId: c.Id);
        await verification.RecordAsync(passedInC.Id, VerificationOutcome.Pass, "Test", new VerificationContext());

        var standaloneFailed = await registry.CreateAsync("Standalone failure", "Belongs to no Project.", Guid.NewGuid(), "Test", parentId: null);
        await verification.RecordAsync(standaloneFailed.Id, VerificationOutcome.Fail, "Test", new VerificationContext());

        var overdueInA = await DashboardExportTestHost.CreateTaskAsync(host, "PH-TASK-1", "Overdue in A");
        await ((IHasParent)overdueInA).MoveAsync(a.Id);
        await overdueInA.SetDueDateAsync(DateTimeOffset.UtcNow.AddDays(-2));

        var overdueStandalone = await DashboardExportTestHost.CreateTaskAsync(host, "PH-TASK-2", "Overdue, no Project");
        await overdueStandalone.SetDueDateAsync(DateTimeOffset.UtcNow.AddDays(-2));

        return (a, b, c);
    }

    [Fact]
    public async Task PrimeAsync_NoProjects_ReportsHonestlyEmpty()
    {
        using var temp = new TempDirectory();
        var (_, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var cockpit = await DashboardExportTestHost.Cockpit(manager);

        Assert.Empty(cockpit.ProjectHealth);
        Assert.Null(cockpit.GetProjectHealth(Guid.NewGuid()));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ProjectHealth_IsTheCockpitRollupScopedToEachProject()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var (a, b, c) = await SeedAsync(host);

        var cockpit = await DashboardExportTestHost.Cockpit(manager);

        Assert.Equal(3, cockpit.ProjectHealth.Count);

        var healthA = cockpit.GetProjectHealth(a.Id)!;
        Assert.Equal("PH-A", healthA.Identifier);
        Assert.Equal("PH-A Blocked Project", healthA.Label);
        Assert.Equal(LifecycleState.Draft, healthA.Status);
        Assert.Equal(EngineeringHealthStatus.Blocked, healthA.VerificationStatus);
        Assert.Equal(EngineeringHealthStatus.Unknown, healthA.RequirementsStatus);
        Assert.Equal(EngineeringHealthStatus.Unknown, healthA.CalculationStatus);
        Assert.Equal(EngineeringHealthStatus.Unknown, healthA.DocumentationStatus);
        Assert.Equal(EngineeringHealthStatus.Unknown, healthA.ManufacturingStatus);
        Assert.Equal(EngineeringHealthStatus.Blocked, healthA.Health);
        Assert.Equal("0/1 healthy (1/5 disciplines reporting)", healthA.HealthScoreDisplay);
        Assert.Equal(1, healthA.BlockedItemCount);
        Assert.Equal(1, healthA.OverdueActionCount);

        var healthB = cockpit.GetProjectHealth(b.Id)!;
        Assert.Equal(EngineeringHealthStatus.Unknown, healthB.Health);
        Assert.Equal(EngineeringHealthStatus.Unknown, healthB.VerificationStatus);
        Assert.Equal("— (no Engineering data yet)", healthB.HealthScoreDisplay);
        Assert.Equal(0, healthB.BlockedItemCount);
        Assert.Equal(0, healthB.OverdueActionCount);

        var healthC = cockpit.GetProjectHealth(c.Id)!;
        Assert.Equal(EngineeringHealthStatus.Healthy, healthC.Health);
        Assert.Equal(EngineeringHealthStatus.Healthy, healthC.VerificationStatus);
        Assert.Equal("1/1 healthy (1/5 disciplines reporting)", healthC.HealthScoreDisplay);
        Assert.Equal(0, healthC.BlockedItemCount);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task WorkspaceHealth_StillRollsUpEveryLiveObject_ProjectOrNot()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        await SeedAsync(host);

        var cockpit = await DashboardExportTestHost.Cockpit(manager);

        // The workspace-wide figures are the same rule over every live
        // object — two Failed activities (one in A, one standalone) make the
        // discipline Blocked, and the overall rollup follows, exactly as
        // WP 8.1C always defined it.
        Assert.Equal(EngineeringHealthStatus.Blocked, cockpit.VerificationStatus);
        Assert.Equal(EngineeringHealthStatus.Blocked, cockpit.Health);
        Assert.Equal("0/1 healthy (1/5 disciplines reporting)", cockpit.HealthScoreDisplay);
        Assert.Equal(2, cockpit.BlockedItems.Count);
        Assert.Equal(2, cockpit.OverdueActions.Count);

        // No Project's own figures can exceed the workspace's, and the
        // workspace count is at least the sum over Projects (the standalone
        // remainder is the difference).
        Assert.Equal(1, cockpit.ProjectHealth.Sum(p => p.BlockedItemCount));
        Assert.Equal(1, cockpit.ProjectHealth.Sum(p => p.OverdueActionCount));
        Assert.All(cockpit.ProjectHealth, p => Assert.True(p.BlockedItemCount <= cockpit.BlockedItems.Count));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ProjectHealth_FollowsTheObject_WhenItIsMovedBetweenProjects()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var (a, b, _) = await SeedAsync(host);
        var domain = DashboardExportTestHost.Domain(host);

        var cockpit = await DashboardExportTestHost.Cockpit(manager);
        Assert.Equal(EngineeringHealthStatus.Blocked, cockpit.GetProjectHealth(a.Id)!.Health);
        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.GetProjectHealth(b.Id)!.Health);

        // Membership is the parent chain (ProjectMembership) — moving the
        // failed activity from A to B moves the Blocked signal with it.
        var failedInA = (await domain.Repository.ListByKindAsync(VerificationActivityFactoryRegistry.SupportedKind))
            .OfType<IHasParent>()
            .Single(o => o.ParentId == a.Id);
        await failedInA.MoveAsync(b.Id);

        await cockpit.PrimeAsync();
        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.GetProjectHealth(a.Id)!.Health);
        Assert.Equal(EngineeringHealthStatus.Blocked, cockpit.GetProjectHealth(b.Id)!.Health);
        Assert.Equal(EngineeringHealthStatus.Blocked, cockpit.Health);

        await manager.ShutdownAsync();
    }
}
