using Tempest.App.Workspace;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// End-to-end `WP 9.5A` Workspace integration test — the same composition
/// <c>Program.cs</c> itself performs (<see cref="ManufacturingWorkspaceRegistration.Register"/>,
/// called after <see cref="WorkspaceManager.StartAsync"/> exactly as
/// `Program.cs` calls it), against the real, running <see cref="ITempestHost"/>,
/// the real <see cref="EngineeringManufacturingWorkspaceSampleModule"/>'s own
/// representative graph (which itself depends on the real
/// <see cref="EngineeringDomainSampleModule"/>'s,
/// <see cref="MechanicalProductStructureSampleModule"/>'s,
/// <see cref="RequirementsWorkspaceSampleModule"/>'s,
/// <see cref="EngineeringCalculationsWorkspaceSampleModule"/>'s, and
/// <see cref="EngineeringDocumentsWorkspaceSampleModule"/>'s own graphs for
/// cross-discipline Digital Thread links), and the real Command
/// Framework — mirroring <c>VerificationWorkspaceIntegrationTests</c>'s own
/// identical shape. Also registers <see cref="MechanicalWorkspaceRegistration"/>
/// — needed for one dedicated test dispatching the pre-existing
/// <see cref="SetBomLineCommand"/> against a live Manufacturing object,
/// proving the "already works, zero new code" BOM finding empirically, not
/// just asserted in prose.
/// </summary>
[Collection("Console output capture")]
public class ManufacturingWorkspaceIntegrationTests
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
            typeof(ManufacturingWorkspaceExplorerModule),
            typeof(EngineeringManufacturingWorkspaceSampleModule),
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
        // DocumentsWorkspaceIntegrationTests/VerificationWorkspaceIntegrationTests's
        // own StartAsync already works around — waiting on the seeded
        // module's own HasRegistered flag (the last module in ordinal Id
        // order).
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (GetSeededModule(host) is not { HasRegistered: true } && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        var services = host.Services!;
        var domainContext = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        var commandDispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
        var commandRegistry = (ICommandRegistry)services.GetService(typeof(ICommandRegistry));
        var referenceIntegrityChecker = (IReferenceIntegrityChecker)services.GetService(typeof(IReferenceIntegrityChecker));

        // Mechanical must register before Manufacturing dispatches
        // SetBomLineCommand against a live Manufacturing object below —
        // mirrors Program.cs's own registration order.
        MechanicalWorkspaceRegistration.Register(manager, domainContext, commandDispatcher, commandRegistry, referenceIntegrityChecker);
        ManufacturingWorkspaceRegistration.Register(manager, domainContext, commandDispatcher, commandRegistry);

        return (workspace, manager, host);
    }

    private static EngineeringManufacturingWorkspaceSampleModule GetSeededModule(ITempestHost host) =>
        (EngineeringManufacturingWorkspaceSampleModule)host.Services!.GetService(typeof(EngineeringManufacturingWorkspaceSampleModule));

    [Fact]
    public async Task ProjectExplorer_ManufacturingArea_RootsIncludeOneCategoryNodePerLabel()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);

        await workspace.Navigation.SwitchAreaAsync(ManufacturingWorkspaceExplorerModule.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();

        Assert.Equal(ManufacturingCategory.Labels.Count, roots.Count);
        Assert.Contains(roots, n => n.Title == "Routings" && n.HasChildren);
        Assert.Contains(roots, n => n.Title == "Supplier Operations" && n.HasChildren);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_RoutingObject_PropertyInspectorShowsRealFacetsAndThreeSequencedSteps()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(ManufacturingWorkspaceExplorerModule.NavigationItemId);
        await workspace.Selection.SelectAsync(seeded.RoutingId!.Value, "ManufacturingOperation");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Classification" && f.Value == ManufacturingObjectFactoryRegistry.Routing);

        var children = await workspace.ProjectExplorer.GetChildrenAsync(seeded.RoutingId!.Value);
        Assert.Equal(3, children.Count);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_WingAssemblyStep_PropertyInspectorShowsReferencesDocumentedByAndVerifiedByLinks()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);
        var mechanical = (MechanicalProductStructureSampleModule)host.Services!.GetService(typeof(MechanicalProductStructureSampleModule));

        await workspace.Selection.SelectAsync(seeded.WingAssemblyOperationId!.Value, "ManufacturingOperation");

        Assert.Contains(
            workspace.PropertyInspector.CurrentFacets,
            f => f.Name == "References (Digital Thread)" && f.Value.Contains(mechanical.WingAssemblyId!.Value.ToString()));
        Assert.Contains(
            workspace.PropertyInspector.CurrentFacets,
            f => f.Name == "Documented By (Digital Thread)" && f.Value == seeded.WorkInstructionId!.Value.ToString());
        Assert.Contains(
            workspace.PropertyInspector.CurrentFacets,
            f => f.Name == "Verified By (Digital Thread)" && f.Value == seeded.InspectionId!.Value.ToString());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_SeededWorkInstruction_ViaReusedDocumentsFacetProvider_ShowsRealFacets()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.WorkInstructionId!.Value, "WorkInstruction");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Kind" && f.Value == "WorkInstruction");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Name");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_SeededInspection_ViaReusedVerificationFacetProvider_ShowsRecordedPassOutcome()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.InspectionId!.Value, "Inspection");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Latest Outcome" && f.Value == "Pass");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Referenced Document(s)");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task CommandRegistry_ListsAllTenManufacturingCommands()
    {
        using var temp = new TempDirectory();
        var (_, manager, host) = await StartAsync(temp.Path);
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

        var manufacturingCommands = commandRegistry.Items.Where(d => d.Category == "Manufacturing").ToList();

        Assert.Equal(10, manufacturingCommands.Count);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task FullLifecycle_CreateReviseSetStatusDelete_AllSucceed()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
        var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

        await workspace.Navigation.SwitchAreaAsync(ManufacturingWorkspaceExplorerModule.NavigationItemId);

        // Create.
        var createResult = await commandDispatcher.DispatchAsync(
            new CreateManufacturingObjectCommand("ManufacturingOperation", "Integration Test Operation", partId: Guid.NewGuid(), classification: "Operation"), default);
        Assert.True(createResult.Succeeded);
        var created = (await domainContext.Repository.ListByKindAsync("ManufacturingOperation"))
            .Single(o => ((IHasBusinessIdentifier)o).DisplayName == "Integration Test Operation");

        // Revise.
        var reviseResult = await commandDispatcher.DispatchAsync(
            new ReviseManufacturingObjectCommand(created.Id, "ManufacturingOperation", "Updated content."), default);
        Assert.True(reviseResult.Succeeded);

        // Set Status (Request Review -> Approve -> Release).
        var reviewResult = await commandDispatcher.DispatchAsync(
            new SetManufacturingObjectStatusCommand(created.Id, "ManufacturingOperation", LifecycleState.InReview), default);
        Assert.True(reviewResult.Succeeded);
        var approveResult = await commandDispatcher.DispatchAsync(
            new SetManufacturingObjectStatusCommand(created.Id, "ManufacturingOperation", LifecycleState.Approved), default);
        Assert.True(approveResult.Succeeded);
        var releaseResult = await commandDispatcher.DispatchAsync(
            new SetManufacturingObjectStatusCommand(created.Id, "ManufacturingOperation", LifecycleState.Released), default);
        Assert.True(releaseResult.Succeeded);

        // Delete (an already-Released object may still be soft-deleted — mirrors every prior discipline's own identical behaviour).
        var deleteResult = await commandDispatcher.DispatchAsync(new DeleteManufacturingObjectCommand(created.Id, "ManufacturingOperation"), default);
        Assert.True(deleteResult.Succeeded);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SetBomLineCommand_AgainstALiveManufacturingOperation_UpdatesTheRealFacet_ProvingZeroNewCode()
    {
        using var temp = new TempDirectory();
        var (_, manager, host) = await StartAsync(temp.Path);
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
        var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

        var createResult = await commandDispatcher.DispatchAsync(
            new CreateManufacturingObjectCommand("ManufacturingOperation", "BOM Test Operation", partId: Guid.NewGuid(), classification: "Operation"), default);
        Assert.True(createResult.Succeeded);
        var created = (await domainContext.Repository.ListByKindAsync("ManufacturingOperation"))
            .Single(o => ((IHasBusinessIdentifier)o).DisplayName == "BOM Test Operation");

        // Mechanical.SetBomLineCommand — never a Manufacturing-owned
        // command — dispatched directly against a real "ManufacturingOperation".
        var bomResult = await commandDispatcher.DispatchAsync(
            new SetBomLineCommand(created.Id, "ManufacturingOperation", 4m, "EA", findNumber: "10", itemNumber: "0010", referenceDesignator: "J1-J4"), default);
        Assert.True(bomResult.Succeeded);

        var facetProvider = new ManufacturingOperationPropertyFacetProvider("ManufacturingOperation", domainContext);
        var facets = await facetProvider.GetFacetsAsync(created.Id);

        Assert.Contains(facets, f => f.Name == "BOM Sequence (ItemNumber)" && f.Value == "0010");
        Assert.Contains(facets, f => f.Name == "BOM Quantity" && f.Value == "4 EA");

        await manager.ShutdownAsync();
    }

    // ---- Engineering Cockpit KPIs (WP 9.5A), against the real seeded graph ----

    [Fact]
    public async Task Cockpit_ManufacturingKpiCards_ReflectTheRealSeededGraph()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        var cards = cockpit.ManufacturingKpiCards.ToDictionary(c => c.Label, c => c.Value);

        // Seven live objects: Routing (Draft), three Operation steps (one
        // InReview, one Released, one Draft), one Supplier Operation
        // (Draft, manufacturedBy fulfilled), one Work Instruction, one
        // Inspection (recorded Pass).
        Assert.Equal("7", cards["Manufacturing Objects"]);
        Assert.Equal("33% (1/3)", cards["Manufacturing Readiness"]);
        Assert.Equal("1", cards["Released Items"]);
        Assert.Equal("2", cards["Open Operations"]);
        Assert.Equal("100% (1/1)", cards["Supplier Status"]);
        Assert.Equal("1 Passed / 0 Failed / 0 Conditional / 0 Pending", cards["Inspection Status"]);
        Assert.Equal("Attention", cards["Production Health"]);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Cockpit_AttentionItems_ReportsManufacturingIsLive()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Contains(cockpit.AttentionItems, item => item.Title == "Manufacturing is live");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Cockpit_ManufacturingStatus_IsAttentionBecauseOfOpenOperations()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Equal(EngineeringHealthStatus.Attention, cockpit.ManufacturingStatus);

        await manager.ShutdownAsync();
    }
}
