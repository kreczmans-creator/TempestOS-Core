using System.Text.Json.Nodes;
using Tempest.Workspace.Integration.DashboardExport;
using Tempest.Workspace.Mechanical;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

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
        var adapter = new ProgrammeHierarchyExportAdapter(DashboardExportTestHost.Domain(host));

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

        Assert.Equal(1, json["schemaVersion"]!.GetValue<int>());
        Assert.Empty(json["portfolios"]!.AsArray());
        Assert.Empty(json["programmes"]!.AsArray());
        Assert.Empty(json["projects"]!.AsArray());
        Assert.Equal(0, json["summary"]!["portfolioCount"]!.GetValue<int>());
        Assert.Equal(0, json["summary"]!["deletedCount"]!.GetValue<int>());

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
}
