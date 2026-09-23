using Tempest.Workspace;
using Tempest.Workspace.Timesheets;
using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Projects;
using Tempest.Core.Timesheets;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP 19.0A` acceptance #3: <c>timesheet.record</c> resolves its own
/// parent to the open project via <c>CreationPlacement</c> and returns
/// the new entry's own subject id and Kind; the node provider lists it
/// under its own project and week; the facet provider names the
/// principal and the project, not a bare id (`ADR-0150`).
/// </summary>
public sealed class TimesheetWorkspaceTests : IAsyncLifetime
{
    private TempDirectory _temp = null!;
    private ITempestHost _host = null!;
    private WorkspaceManager _manager = null!;

    public async Task InitializeAsync()
    {
        _temp = new TempDirectory();
        (_host, _manager) = await ProjectCommercialTestHost.StartAsync(_temp.Path);
        ProjectCommercialTestHost.SignIn(_host);
    }

    public async Task DisposeAsync()
    {
        await _manager.ShutdownAsync();
        await _host.DisposeAsync();
        _temp.Dispose();
    }

    private static CommandParameterPrompt Answering(params (string Name, string Value)[] answers) =>
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
            answers.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal));

    private async Task<(Guid ProjectId, string RateCardId)> SetUpPricedProjectAsync()
    {
        var projectId = await ProjectCommercialTestHost.CreateProjectAsync(_host);

        var rateCards = ProjectCommercialTestHost.RateCards(_host);
        var card = Tests.BusinessGovernance.BusinessGovernanceFixtures.Card("TS-WS-CARD") with
        {
            Entries =
            [
                new Core.BusinessGovernance.Pricing.RateCardEntry(
                    "ENG-1", "Senior engineering", Core.BusinessGovernance.Pricing.PricingBasis.Hourly,
                    new Core.BusinessGovernance.Money(100m, Core.BusinessGovernance.CurrencyCode.Gbp), Grade: "Senior"),
            ],
        };
        await rateCards.RegisterAsync("TS-WS-CARD", card, Tests.BusinessGovernance.BusinessGovernanceFixtures.Verified());
        await Tests.BusinessGovernance.BusinessGovernanceFixtures.ReleaseAsync((Core.BusinessGovernance.Pricing.RateCardCatalog)rateCards, "TS-WS-CARD");

        var commercial = ProjectCommercialTestHost.ProjectCommercial(_host);
        await commercial.PinRateCardAsync(projectId, "TS-WS-CARD");

        return (projectId, "TS-WS-CARD");
    }

    [Fact]
    public async Task Record_WithTheProjectOpen_LandsUnderIt_AndReturnsTheEntryAsSubject()
    {
        var (projectId, _) = await SetUpPricedProjectAsync();
        var registry = (ICommandRegistry)_host.Services!.GetService(typeof(ICommandRegistry));

        var context = new CommandContext([], projectId);

        var invocation = await registry.InvokeAsync(
            "timesheet.record", context,
            Answering(("date", "2026-03-02"), ("hours", "4"), ("billable", "True"), ("grade", "Senior"), ("task", "Workspace test task")));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);
        Assert.NotNull(invocation.Result.SubjectId);
        Assert.Equal(TimesheetEntry.CanonicalKind, invocation.Result.SubjectKind);

        var domain = ProjectCommercialTestHost.Domain(_host);
        var created = (TimesheetEntry)(await domain.Repository.FindAsync(invocation.Result.SubjectId!.Value))!;
        Assert.Equal(projectId, created.ParentId);
        Assert.Equal(projectId, created.ProjectId);
    }

    [Fact]
    public async Task Record_WithNoProjectOpen_IsRefused_NotThrown()
    {
        var registry = (ICommandRegistry)_host.Services!.GetService(typeof(ICommandRegistry));

        var invocation = await registry.InvokeAsync(
            "timesheet.record", CommandContext.Empty,
            Answering(("date", "2026-03-02"), ("hours", "4"), ("billable", "True"), ("grade", "Senior"), ("task", "No project")));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.False(invocation.Result!.Succeeded);
    }

    [Fact]
    public async Task TheNodeProvider_ListsANewEntry_UnderItsOwnProjectAndWeek()
    {
        var (projectId, _) = await SetUpPricedProjectAsync();
        var timesheets = ProjectCommercialTestHost.Timesheets(_host);
        var entry = await timesheets.RecordAsync(projectId, new DateOnly(2026, 3, 4), 3m, billable: true, "Senior", "Listed entry");
        Assert.True(entry.Succeeded);

        var domain = ProjectCommercialTestHost.Domain(_host);
        var provider = new TimesheetEntryNodeProvider(TimesheetsWorkspaceRegistration.ExplorerAreaId, domain);

        var roots = await provider.GetRootNodesAsync();
        var projectNode = Assert.Single(roots, n => n.Id == projectId);
        Assert.True(projectNode.HasChildren);

        var weekGroups = await provider.GetChildrenAsync(projectId);
        var weekGroup = Assert.Single(weekGroups);
        Assert.Contains("2026-03-02", weekGroup.Title, StringComparison.Ordinal);

        var members = await provider.GetChildrenAsync(weekGroup.Id);
        var memberNode = Assert.Single(members, m => m.Id == entry.Entry!.Id);
        Assert.Contains("Listed entry", memberNode.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFacetProvider_NamesThePrincipalAndProject_NotBareIds()
    {
        var (projectId, _) = await SetUpPricedProjectAsync();
        var timesheets = ProjectCommercialTestHost.Timesheets(_host);
        var entry = await timesheets.RecordAsync(projectId, new DateOnly(2026, 3, 4), 3m, billable: true, "Senior", "Facet entry");
        Assert.True(entry.Succeeded);

        var domain = ProjectCommercialTestHost.Domain(_host);
        var principals = (IPrincipalDirectory)_host.Services!.GetService(typeof(IPrincipalDirectory));
        var provider = new TimesheetEntryPropertyFacetProvider(TimesheetEntry.CanonicalKind, domain, principals);

        var facets = await provider.GetFacetsAsync(entry.Entry!.Id);

        var principalFacet = Assert.Single(facets, f => f.Name == "Principal");
        Assert.Equal(principals.Describe(ProjectCommercialTestHost.PrincipalId), principalFacet.Value);

        var projectFacet = Assert.Single(facets, f => f.Name == "Project");
        Assert.DoesNotContain(projectId.ToString(), projectFacet.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Commercial Test Project", projectFacet.Value, StringComparison.Ordinal);
    }
}
