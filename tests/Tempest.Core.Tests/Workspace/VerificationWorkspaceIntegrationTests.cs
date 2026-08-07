using Tempest.App.Workspace;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Verification;
using Tempest.Samples;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// End-to-end `WP 9.3A` Workspace integration test — the same composition
/// <c>Program.cs</c> itself performs (<see cref="VerificationWorkspaceRegistration.Register"/>,
/// called after <see cref="WorkspaceManager.StartAsync"/> exactly as
/// `Program.cs` calls it), against the real, running <see cref="ITempestHost"/>,
/// the real <see cref="EngineeringVerificationWorkspaceSampleModule"/>'s
/// own representative graph (which itself depends on the real
/// <see cref="EngineeringDomainSampleModule"/>'s,
/// <see cref="MechanicalProductStructureSampleModule"/>'s,
/// <see cref="RequirementsWorkspaceSampleModule"/>'s,
/// <see cref="EngineeringCalculationsWorkspaceSampleModule"/>'s, and
/// <see cref="EngineeringDocumentsWorkspaceSampleModule"/>'s own graphs
/// for cross-discipline Digital Thread links), and the real Command
/// Framework — mirroring <c>DocumentsWorkspaceIntegrationTests</c>'s own
/// identical shape.
/// </summary>
[Collection("Console output capture")]
public class VerificationWorkspaceIntegrationTests
{
    private static async Task<(IWorkspace Workspace, WorkspaceManager Manager, ITempestHost Host)> StartAsync(string rootPath)
    {
        var host = new TempestHostBuilder(
        [
            typeof(EngineeringDomainSampleModule),
            typeof(MechanicalProductStructureSampleModule),
            typeof(RequirementsWorkspaceExplorerModule),
            typeof(RequirementsWorkspaceSampleModule),
            typeof(CalculationsWorkspaceExplorerModule),
            typeof(EngineeringCalculationsWorkspaceSampleModule),
            typeof(DocumentsWorkspaceExplorerModule),
            typeof(EngineeringDocumentsWorkspaceSampleModule),
            typeof(VerificationWorkspaceExplorerModule),
            typeof(EngineeringVerificationWorkspaceSampleModule),
        ])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        var originalOut = Console.Out;
        IWorkspace workspace;
        try
        {
            Console.SetOut(new StringWriter());
            workspace = await manager.StartAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Same disclosed, pre-existing platform timing characteristic
        // DocumentsWorkspaceIntegrationTests's own StartAsync already
        // works around — waiting on the seeded module's own HasRegistered
        // flag (the last module in ordinal Id order).
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (GetSeededModule(host) is not { HasRegistered: true } && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        var services = host.Services!;
        var domainContext = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        var verificationService = (IVerificationService)services.GetService(typeof(IVerificationService));
        var commandDispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
        var commandRegistry = (ICommandRegistry)services.GetService(typeof(ICommandRegistry));

        VerificationWorkspaceRegistration.Register(manager, domainContext, verificationService, commandDispatcher, commandRegistry);

        return (workspace, manager, host);
    }

    private static EngineeringVerificationWorkspaceSampleModule GetSeededModule(ITempestHost host) =>
        (EngineeringVerificationWorkspaceSampleModule)host.Services!.GetService(typeof(EngineeringVerificationWorkspaceSampleModule));

    [Fact]
    public async Task ProjectExplorer_VerificationArea_RootsIncludeOneCategoryNodePerLabel()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);

        await workspace.Navigation.SwitchAreaAsync(VerificationWorkspaceExplorerModule.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();

        Assert.Equal(VerificationMethodCategory.Labels.Count, roots.Count);
        Assert.Contains(roots, n => n.Title == "Inspection" && n.HasChildren);
        Assert.Contains(roots, n => n.Title == "Analysis" && n.HasChildren);
        Assert.Contains(roots, n => n.Title == "Test" && n.HasChildren);
        Assert.Contains(roots, n => n.Title == "Demonstration" && n.HasChildren);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_InspectionActivity_PropertyInspectorShowsInReviewStatusAndVerifiesLink()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);
        var mechanical = (MechanicalProductStructureSampleModule)host.Services!.GetService(typeof(MechanicalProductStructureSampleModule));

        await workspace.Selection.SelectAsync(seeded.InspectionActivityId!.Value, "VerificationActivity");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Status" && f.Value == "InReview");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Method" && f.Value == "Inspection");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Result History" && f.Value == "Never recorded");
        Assert.Contains(
            workspace.PropertyInspector.CurrentFacets,
            f => f.Name == "Verifies (Digital Thread)" && f.Value.Contains(mechanical.SharedFastenerComponentId!.Value.ToString()));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_AnalysisActivity_PropertyInspectorShowsPassOutcomeAndLinkedCalculationAndMaterial()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.AnalysisActivityId!.Value, "VerificationActivity");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Latest Outcome" && f.Value == "Pass");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Referenced Materials" && f.Value.Contains(MaterialsSampleModule.SampleMaterialId));
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Based On Calculation Record(s)");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_TestActivity_PropertyInspectorShowsFailOutcomeAndLinkedDocument()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.TestActivityId!.Value, "VerificationActivity");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Latest Outcome" && f.Value == "Fail");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Referenced Document(s)");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task CommandRegistry_ListsAllElevenVerificationCommands()
    {
        using var temp = new TempDirectory();
        var (_, manager, host) = await StartAsync(temp.Path);
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

        var verificationCommands = commandRegistry.Items.Where(d => d.Category == "Verification").ToList();

        Assert.Equal(11, verificationCommands.Count);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task FullLifecycle_CreateRecordResultSetStatusDelete_AllSucceed()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
        var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

        await workspace.Navigation.SwitchAreaAsync(VerificationWorkspaceExplorerModule.NavigationItemId);

        // Create.
        var createResult = await commandDispatcher.DispatchAsync(
            new CreateVerificationActivityCommand("Integration Test Activity", Guid.NewGuid(), "Analysis"), default);
        Assert.True(createResult.Succeeded);
        var created = (await domainContext.Repository.ListByKindAsync("VerificationActivity"))
            .Single(a => ((IHasBusinessIdentifier)a).DisplayName == "Integration Test Activity");

        // Record Result.
        var recordResult = await commandDispatcher.DispatchAsync(
            new RecordVerificationResultCommand(created.Id, "VerificationActivity", VerificationOutcome.Pass, "Analysis"), default);
        Assert.True(recordResult.Succeeded);
        Assert.Single(await VerificationRecordReader.GetResultHistoryAsync(domainContext, created.Id));

        // Set Status (Request Review -> Approve).
        var reviewResult = await commandDispatcher.DispatchAsync(
            new SetVerificationActivityStatusCommand(created.Id, "VerificationActivity", LifecycleState.InReview), default);
        Assert.True(reviewResult.Succeeded);
        var approveResult = await commandDispatcher.DispatchAsync(
            new SetVerificationActivityStatusCommand(created.Id, "VerificationActivity", LifecycleState.Approved), default);
        Assert.True(approveResult.Succeeded);

        // Delete.
        var deleteResult = await commandDispatcher.DispatchAsync(new DeleteVerificationActivityCommand(created.Id, "VerificationActivity"), default);
        Assert.True(deleteResult.Succeeded);

        await manager.ShutdownAsync();
    }

    // ---- Engineering Cockpit KPIs (WP 9.3A), against the real seeded graph ----

    [Fact]
    public async Task Cockpit_VerificationKpiCards_ReflectTheRealSeededGraph()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        var cards = cockpit.VerificationKpiCards.ToDictionary(c => c.Label, c => c.Value);

        // Four live Activities: Inspection (InReview, no record - In
        // Progress/Outstanding), Analysis (Approved, Pass), Test (InReview,
        // Fail - Outstanding), Demonstration (Draft, no record - Planned).
        Assert.Equal("2", cards["Total Verification Records"]);
        Assert.Equal("1", cards["Planned"]);
        Assert.Equal("1", cards["In Progress"]);
        Assert.Equal("1", cards["Passed"]);
        Assert.Equal("1", cards["Failed"]);
        Assert.Equal("0", cards["Conditional"]);
        Assert.Equal("2", cards["Outstanding"]);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Cockpit_AttentionItems_ReportsVerificationIsLive()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Contains(cockpit.AttentionItems, item => item.Title == "Verification is live");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Cockpit_VerificationStatus_IsBlockedBecauseOfTheFailOutcome()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Equal(EngineeringHealthStatus.Blocked, cockpit.VerificationStatus);

        await manager.ShutdownAsync();
    }
}
