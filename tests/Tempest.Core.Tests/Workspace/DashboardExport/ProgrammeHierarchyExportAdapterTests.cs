using System.Text.Json.Nodes;
using Tempest.Workspace;
using Tempest.Workspace.Integration.DashboardExport;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Verification;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Workspace.DashboardExport;

/// <summary>
/// Proves <see cref="ProgrammeHierarchyExportAdapter"/> against a real,
/// running <see cref="EngineeringDomainContext"/> — Portfolio/Programme
/// (no Cockpit equivalent, checked directly), and Project, whose live set
/// and per-status counts must exactly match a freshly-constructed
/// <see cref="MechanicalCockpitReadModel"/>'s own already-computed
/// <c>LiveProjects</c> (the product owner's Cockpit-reuse requirement,
/// checked directly rather than assumed).
/// </summary>
public class ProgrammeHierarchyExportAdapterTests
{
    private static async Task<JsonNode> ExportAsync(ITempestHost host)
    {
        var adapter = new ProgrammeHierarchyExportAdapter(
            DashboardExportTestHost.Domain(host),
            DashboardExportTestHost.Requirements(host),
            DashboardExportTestHost.RequirementValidation(host),
            DashboardExportTestHost.NavigationProvider(host),
            DashboardExportTestHost.CommandRegistry(host));

        using var stream = new MemoryStream();
        await adapter.ExportAsync(stream);
        stream.Position = 0;

        return JsonNode.Parse(stream) ?? throw new InvalidOperationException("Export produced no JSON.");
    }

    [Fact]
    public async Task ExportAsync_NoData_ReportsHonestZeroes()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var json = await ExportAsync(host);

        Assert.Equal(2, json["schemaVersion"]!.GetValue<int>());
        Assert.Empty(json["portfolios"]!.AsArray());
        Assert.Empty(json["programmes"]!.AsArray());
        Assert.Empty(json["projects"]!.AsArray());
        Assert.Equal(0, json["summary"]!["portfolioCount"]!.GetValue<int>());
        Assert.Equal(0, json["summary"]!["deletedCount"]!.GetValue<int>());
        foreach (var word in new[] { "healthy", "attention", "blocked", "unknown" })
            Assert.Equal(0, json["summary"]!["byHealth"]![word]!.GetValue<int>());
        foreach (var word in new[] { "open", "overdue", "blocked" })
            Assert.Equal(0, json["summary"]!["tasks"]![word]!.GetValue<int>());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_FullHierarchy_ReportsIdentityOwnershipAndLinkage()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var portfolio = await DashboardExportTestHost.CreatePortfolioAsync(host, "PORT-1", "Test Portfolio");
        var programme = await DashboardExportTestHost.CreateProgrammeAsync(host, "PROG-1", "Test Programme", portfolio.Id);
        var project = await DashboardExportTestHost.CreateProjectAsync(host, "PROJ-1", "Test Project", programme.Id);

        var json = await ExportAsync(host);

        var portfolioEntry = json["portfolios"]!.AsArray().Single()!;
        Assert.Equal(portfolio.Id.ToString(), portfolioEntry["id"]!.GetValue<string>());
        Assert.Equal("PORT-1", portfolioEntry["identifier"]!.GetValue<string>());
        Assert.Equal("Test Portfolio", portfolioEntry["name"]!.GetValue<string>());
        Assert.Equal("draft", portfolioEntry["status"]!.GetValue<string>());

        var programmeEntry = json["programmes"]!.AsArray().Single()!;
        Assert.Equal(portfolio.Id.ToString(), programmeEntry["portfolioId"]!.GetValue<string>());

        var projectEntry = json["projects"]!.AsArray().Single()!;
        Assert.Equal(project.Id.ToString(), projectEntry["id"]!.GetValue<string>());
        Assert.Equal(programme.Id.ToString(), projectEntry["programmeId"]!.GetValue<string>());
        Assert.False(projectEntry["isDeleted"]!.GetValue<bool>());
        Assert.True(DateTimeOffset.TryParse(projectEntry["lastUpdate"]!.GetValue<string>(), out _));

        Assert.Equal(1, json["summary"]!["portfolioCount"]!.GetValue<int>());
        Assert.Equal(1, json["summary"]!["programmeCount"]!.GetValue<int>());
        Assert.Equal(1, json["summary"]!["projectCount"]!.GetValue<int>());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_Projects_MatchesMechanicalCockpitReadModelLiveProjectsExactly()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var live1 = await DashboardExportTestHost.CreateProjectAsync(host, "PROJ-LIVE-1", "Live Project One");
        var live2 = await DashboardExportTestHost.CreateProjectAsync(host, "PROJ-LIVE-2", "Live Project Two");
        await live2.TransitionAsync(LifecycleState.InReview);

        var deleted = await DashboardExportTestHost.CreateProjectAsync(host, "PROJ-DELETED", "Deleted Project");
        await deleted.DeleteAsync();

        // The exact same read model the desktop Cockpit's Mechanical
        // discipline card constructs — this test's own oracle.
        var cockpit = new MechanicalCockpitReadModel(DashboardExportTestHost.Domain(host));
        await cockpit.LoadAsync();

        var json = await ExportAsync(host);
        var projectIds = json["projects"]!.AsArray().Select(p => p!["id"]!.GetValue<string>()).ToList();

        Assert.Equal(cockpit.LiveProjects.Count, projectIds.Count);
        Assert.Equal(cockpit.LiveProjects.Select(p => p.DisplayName).OrderBy(n => n), json["projects"]!.AsArray().Select(p => p!["name"]!.GetValue<string>()).OrderBy(n => n));

        Assert.Contains(live1.Id.ToString(), projectIds);
        Assert.Contains(live2.Id.ToString(), projectIds);
        Assert.DoesNotContain(deleted.Id.ToString(), projectIds);

        Assert.Equal(1, json["summary"]!["byStatus"]!["draft"]!.GetValue<int>());
        Assert.Equal(1, json["summary"]!["byStatus"]!["inReview"]!.GetValue<int>());
        Assert.Equal(1, json["summary"]!["deletedCount"]!.GetValue<int>());
        Assert.Equal(2, json["summary"]!["projectCount"]!.GetValue<int>());

        await manager.ShutdownAsync();
    }

    /// <summary>Schema v2 (`ADR-0151`): each Project's own health, blocked and overdue figures are <see cref="EngineeringCockpit.ProjectHealth"/>'s own, word for word, and <c>summary.byHealth</c> counts them.</summary>
    [Fact]
    public async Task ExportAsync_ProjectHealth_MatchesEngineeringCockpitProjectHealthWordForWord()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var blocked = await DashboardExportTestHost.CreateProjectAsync(host, "PROJ-BLOCKED", "Blocked Project");
        var empty = await DashboardExportTestHost.CreateProjectAsync(host, "PROJ-EMPTY", "Empty Project");

        var registry = new VerificationActivityFactoryRegistry(DashboardExportTestHost.Domain(host));
        var failed = await registry.CreateAsync("Failed Activity", "Verifies something.", Guid.NewGuid(), "Test", parentId: blocked.Id);
        await DashboardExportTestHost.Verification(host).RecordAsync(failed.Id, VerificationOutcome.Fail, "Test", new VerificationContext());

        var overdue = await DashboardExportTestHost.CreateTaskAsync(host, "TASK-1", "Overdue task");
        await ((IHasParent)overdue).MoveAsync(blocked.Id);
        await overdue.SetDueDateAsync(DateTimeOffset.UtcNow.AddDays(-3));

        var cockpit = await DashboardExportTestHost.Cockpit(manager);
        var json = await ExportAsync(host);

        Assert.Equal(ProgrammeHierarchyExportAdapter.CurrentSchemaVersion, json["schemaVersion"]!.GetValue<int>());

        var blockedEntry = json["projects"]!.AsArray().Single(p => p!["id"]!.GetValue<string>() == blocked.Id.ToString())!;
        Assert.Equal("blocked", blockedEntry["health"]!["overall"]!.GetValue<string>());
        Assert.Equal("blocked", blockedEntry["health"]!["byDiscipline"]!["verification"]!.GetValue<string>());
        Assert.Equal("unknown", blockedEntry["health"]!["byDiscipline"]!["requirements"]!.GetValue<string>());
        Assert.Equal("unknown", blockedEntry["health"]!["byDiscipline"]!["calculations"]!.GetValue<string>());
        Assert.Equal("unknown", blockedEntry["health"]!["byDiscipline"]!["documents"]!.GetValue<string>());
        Assert.Equal("unknown", blockedEntry["health"]!["byDiscipline"]!["manufacturing"]!.GetValue<string>());
        Assert.Equal("0/1 healthy (1/5 disciplines reporting)", blockedEntry["health"]!["score"]!.GetValue<string>());
        Assert.Equal(1, blockedEntry["blockedCount"]!.GetValue<int>());
        Assert.Equal(1, blockedEntry["overdueActionCount"]!.GetValue<int>());

        var emptyEntry = json["projects"]!.AsArray().Single(p => p!["id"]!.GetValue<string>() == empty.Id.ToString())!;
        Assert.Equal("unknown", emptyEntry["health"]!["overall"]!.GetValue<string>());
        Assert.Equal("— (no Engineering data yet)", emptyEntry["health"]!["score"]!.GetValue<string>());
        Assert.Equal(0, emptyEntry["blockedCount"]!.GetValue<int>());
        Assert.Equal(0, emptyEntry["overdueActionCount"]!.GetValue<int>());

        // Word for word against the desktop's own Cockpit — the oracle.
        foreach (var expected in cockpit.ProjectHealth)
        {
            var exported = json["projects"]!.AsArray().Single(p => p!["id"]!.GetValue<string>() == expected.ProjectId.ToString())!;
            Assert.Equal(expected.Health.ToString().ToLowerInvariant(), exported["health"]!["overall"]!.GetValue<string>());
            Assert.Equal(expected.HealthScoreDisplay, exported["health"]!["score"]!.GetValue<string>());
            Assert.Equal(expected.BlockedItemCount, exported["blockedCount"]!.GetValue<int>());
            Assert.Equal(expected.OverdueActionCount, exported["overdueActionCount"]!.GetValue<int>());
        }

        // Every v1 key is still present on the entry, untouched.
        foreach (var key in new[] { "id", "identifier", "name", "status", "owner", "discipline", "programmeId", "parentId", "isDeleted", "lastUpdate" })
            Assert.True(blockedEntry.AsObject().ContainsKey(key), $"v1 key '{key}' missing");

        Assert.Equal(1, json["summary"]!["byHealth"]!["blocked"]!.GetValue<int>());
        Assert.Equal(1, json["summary"]!["byHealth"]!["unknown"]!.GetValue<int>());
        Assert.Equal(0, json["summary"]!["byHealth"]!["healthy"]!.GetValue<int>());
        Assert.Equal(0, json["summary"]!["byHealth"]!["attention"]!.GetValue<int>());

        await manager.ShutdownAsync();
    }

    /// <summary>Schema v2 (additive): each Project's own open tasks, from the Project Workspace's own <see cref="Tempest.Workspace.Projects.ProjectTaskRegister"/>, in dashboard order, with the desktop's own words; <c>summary.tasks</c> totals them; and the Cockpit's <c>overdueActionCount</c> agrees with the exported task list.</summary>
    [Fact]
    public async Task ExportAsync_ProjectTasks_ExportsOpenTasksInDashboardOrderAndAgreesWithOverdueActionCount()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var project = await DashboardExportTestHost.CreateProjectAsync(host, "PROJ-TASKS", "Tasked Project");
        var milestone = await DashboardExportTestHost.CreateMilestoneAsync(host, "MS-1", "Design freeze", project.Id, DateTimeOffset.UtcNow.AddMonths(2));

        var overdue = await DashboardExportTestHost.CreateTaskAsync(host, "T-1", "Overdue work");
        await ((IHasParent)overdue).MoveAsync(project.Id);
        await overdue.SetDueDateAsync(DateTimeOffset.UtcNow.AddDays(-3));
        await overdue.AssignAsync("ada");
        await overdue.ContributeToAsync(milestone.Id);

        var blocked = await DashboardExportTestHost.CreateTaskAsync(host, "T-2", "Blocked work");
        await ((IHasParent)blocked).MoveAsync(project.Id);
        await blocked.ChangeWorkStateAsync(TaskWorkState.Blocked);
        await blocked.SetPriorityAsync(WorkPriority.Critical);

        var done = await DashboardExportTestHost.CreateTaskAsync(host, "T-3", "Finished work");
        await ((IHasParent)done).MoveAsync(project.Id);
        await done.SetDueDateAsync(DateTimeOffset.UtcNow.AddDays(-10));
        await done.ChangeWorkStateAsync(TaskWorkState.Done);

        var json = await ExportAsync(host);

        Assert.Equal(ProgrammeHierarchyExportAdapter.CurrentSchemaVersion, json["schemaVersion"]!.GetValue<int>());

        var entry = json["projects"]!.AsArray().Single(p => p!["id"]!.GetValue<string>() == project.Id.ToString())!;
        var tasks = entry["tasks"]!.AsArray();

        // Open only, overdue first, then Blocked — the Done task is not exported.
        Assert.Equal(2, tasks.Count);
        Assert.Equal([overdue.Id.ToString(), blocked.Id.ToString()], tasks.Select(t => t!["id"]!.GetValue<string>()).ToList());

        var first = tasks[0]!;
        Assert.Equal("T-1", first["identifier"]!.GetValue<string>());
        Assert.Equal("Overdue work", first["name"]!.GetValue<string>());
        Assert.Equal("todo", first["workState"]!.GetValue<string>());
        Assert.Equal("normal", first["priority"]!.GetValue<string>());
        Assert.Equal("ada", first["assignedTo"]!.GetValue<string>());
        Assert.Equal(DateTimeOffset.UtcNow.AddDays(-3).ToString("yyyy-MM-dd"), first["dueDate"]!.GetValue<string>());
        Assert.True(first["isOverdue"]!.GetValue<bool>());
        Assert.Equal("Milestone “Design freeze”", first["contributesTo"]!.GetValue<string>());

        var second = tasks[1]!;
        Assert.Equal("T-2", second["identifier"]!.GetValue<string>());
        Assert.Equal("blocked", second["workState"]!.GetValue<string>());
        Assert.Equal("critical", second["priority"]!.GetValue<string>());
        Assert.Null(second["assignedTo"]);
        Assert.Null(second["dueDate"]);
        Assert.False(second["isOverdue"]!.GetValue<bool>());
        Assert.Null(second["contributesTo"]);

        // Every task key is present on every entry, null or not.
        foreach (var task in tasks)
            foreach (var key in new[] { "id", "identifier", "name", "workState", "priority", "assignedTo", "dueDate", "isOverdue", "contributesTo" })
                Assert.True(task!.AsObject().ContainsKey(key), $"task key '{key}' missing");

        // The Cockpit's own overdueActionCount and the exported task list agree.
        Assert.Equal(tasks.Count(t => t!["isOverdue"]!.GetValue<bool>()), entry["overdueActionCount"]!.GetValue<int>());

        Assert.Equal(2, json["summary"]!["tasks"]!["open"]!.GetValue<int>());
        Assert.Equal(1, json["summary"]!["tasks"]!["overdue"]!.GetValue<int>());
        Assert.Equal(1, json["summary"]!["tasks"]!["blocked"]!.GetValue<int>());

        await manager.ShutdownAsync();
    }
}
