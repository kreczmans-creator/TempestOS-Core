using Tempest.App.Workspace;
using Tempest.App.Workspace.Requirements;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// End-to-end `WP 9.1A` Workspace integration test — the same composition
/// <c>Program.cs</c> itself performs (<see cref="RequirementsWorkspaceRegistration.Register"/>,
/// called after <see cref="WorkspaceManager.StartAsync"/> exactly as
/// `Program.cs` calls it), against the real, running <see cref="ITempestHost"/>,
/// the real <see cref="RequirementsWorkspaceSampleModule"/>'s own
/// representative graph (which itself depends on the real
/// <see cref="MechanicalProductStructureSampleModule"/>'s own graph for
/// cross-discipline allocation), and the real Command Framework — proving
/// navigation, tree drill-down, selection, the Property Inspector, and
/// every Requirements command together, mirroring
/// <c>MechanicalWorkspaceIntegrationTests</c>'s own identical shape.
/// </summary>
[Collection("Console output capture")]
public class RequirementsWorkspaceIntegrationTests
{
    private static async Task<(IWorkspace Workspace, WorkspaceManager Manager, ITempestHost Host)> StartAsync(string rootPath)
    {
        var host = new TempestHostBuilder(
            [typeof(MechanicalProductStructureSampleModule), typeof(RequirementsWorkspaceExplorerModule), typeof(RequirementsWorkspaceSampleModule)])
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
        // MechanicalWorkspaceIntegrationTests's own StartAsync already
        // works around (WorkspaceManager.StartAsync's own WaitForServicesAsync
        // only waits for ITempestHost.Services, not for Module Initialisation
        // to finish) — waiting on the seeded module's own HasRegistered flag.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (GetSeededModule(host) is not { HasRegistered: true } && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        var services = host.Services!;
        var requirementsService = (IRequirementsService)services.GetService(typeof(IRequirementsService));
        var commandDispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
        var commandRegistry = (ICommandRegistry)services.GetService(typeof(ICommandRegistry));

        RequirementsWorkspaceRegistration.Register(manager, requirementsService, commandDispatcher, commandRegistry);

        return (workspace, manager, host);
    }

    private static RequirementsWorkspaceSampleModule GetSeededModule(ITempestHost host) =>
        (RequirementsWorkspaceSampleModule)host.Services!.GetService(typeof(RequirementsWorkspaceSampleModule));

    [Fact]
    public async Task ProjectExplorer_RequirementsArea_RootsAreBothCollectionsAndTheRootGroup()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(RequirementsWorkspaceExplorerModule.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();

        Assert.Equal(3, roots.Count);
        Assert.Contains(roots, n => n.Id == seeded.StructuralCollectionId);
        Assert.Contains(roots, n => n.Id == seeded.AvionicsCollectionId);
        Assert.Contains(roots, n => n.Id == seeded.AircraftRootGroupId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ProjectExplorer_DrillIntoAircraftRoot_FindsWingAndAvionicsGroups()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(RequirementsWorkspaceExplorerModule.NavigationItemId);
        var children = await workspace.ProjectExplorer.GetChildrenAsync(seeded.AircraftRootGroupId!.Value);

        Assert.Equal(2, children.Count);
        Assert.Contains(children, n => n.Id == seeded.WingGroupId);
        Assert.Contains(children, n => n.Id == seeded.AvionicsGroupId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ProjectExplorer_AvionicsGroup_ReflectsTheMoveViaMoveGroupAsync()
    {
        // Direct proof that MoveGroupAsync's own storage-model fix (WP
        // 9.1A) is reflected in the tree, not just in the Domain layer: the
        // seeded module creates "Avionics Requirements" as a root group,
        // then moves it - the tree must show it under Aircraft Requirements,
        // never as a second root.
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(RequirementsWorkspaceExplorerModule.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();

        Assert.DoesNotContain(roots, n => n.Id == seeded.AvionicsGroupId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ProjectExplorer_DrillIntoWingGroup_FindsSparGroupAndDirectlyGroupedRequirements()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(RequirementsWorkspaceExplorerModule.NavigationItemId);
        var children = await workspace.ProjectExplorer.GetChildrenAsync(seeded.WingGroupId!.Value);

        Assert.Contains(children, n => n.Id == seeded.SparGroupId);
        Assert.Contains(children, n => n.NodeType == ProjectExplorerNodeType.Object);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ProjectExplorer_DrillIntoStructuralCollection_FindsAllSixMembers()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(RequirementsWorkspaceExplorerModule.NavigationItemId);
        var children = await workspace.ProjectExplorer.GetChildrenAsync(seeded.StructuralCollectionId!.Value);

        Assert.Equal(6, children.Count);
        Assert.All(children, n => Assert.Equal(RequirementsService.RequirementDocumentKind, n.Kind));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_AllocatedRequirement_PropertyInspectorShowsAllocation()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        var mechanical = (MechanicalProductStructureSampleModule)host.Services!.GetService(typeof(MechanicalProductStructureSampleModule));
        var allocated = await requirementsService.FindByIdentifierAsync("REQ-STR-004");

        await workspace.Selection.SelectAsync(allocated!.Id, RequirementsService.RequirementDocumentKind);

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Status" && f.Value == "Allocated");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Allocated To" && f.Value.Contains(mechanical.WingAssemblyId!.Value.ToString()));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_VerifiedRequirement_PropertyInspectorShowsVerificationCoverage()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        var verified = await requirementsService.FindByIdentifierAsync("REQ-STR-005");

        await workspace.Selection.SelectAsync(verified!.Id, RequirementsService.RequirementDocumentKind);

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Verification Coverage" && f.Value.StartsWith("Verified"));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_SeededCollection_PropertyInspectorShowsMemberCount()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.StructuralCollectionId!.Value, RequirementsService.RequirementCollectionDocumentKind);

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Members" && f.Value == "6");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Navigation_OpenSeededRequirement_ViewTitleIncludesIdentifier()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        var requirement = await requirementsService.FindByIdentifierAsync("REQ-STR-001");

        var view = await workspace.Navigation.OpenAsync(requirement!.Id, RequirementsService.RequirementDocumentKind);

        Assert.Contains("REQ-STR-001", view.Title);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task CommandRegistry_ListsAllEighteenRequirementsCommands()
    {
        using var temp = new TempDirectory();
        var (_, manager, host) = await StartAsync(temp.Path);
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

        var requirementsCommands = commandRegistry.Items.Where(d => d.Category == "Requirements").ToList();

        Assert.Equal(18, requirementsCommands.Count);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task FullLifecycle_CreateReviseSetStatusMoveDelete_AllSucceedAndExplorerReflectsEachStep()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

        await workspace.Navigation.SwitchAreaAsync(RequirementsWorkspaceExplorerModule.NavigationItemId);

        // Create.
        var createResult = await commandDispatcher.DispatchAsync(
            new CreateRequirementCommand("REQ-IT-001", "Integration test requirement."), default);
        Assert.True(createResult.Succeeded);

        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        var created = await requirementsService.FindByIdentifierAsync("REQ-IT-001");
        Assert.NotNull(created);

        // Move into Wing Requirements.
        var moveResult = await commandDispatcher.DispatchAsync(
            new MoveRequirementCommand(created!.Id, seeded.WingGroupId), default);
        Assert.True(moveResult.Succeeded);

        var wingChildren = await workspace.ProjectExplorer.GetChildrenAsync(seeded.WingGroupId!.Value);
        Assert.Contains(wingChildren, n => n.Id == created.Id);

        // Revise.
        var reviseResult = await commandDispatcher.DispatchAsync(
            new ReviseRequirementCommand(created.Id, "Integration test requirement, revised.", "Integration test revision."), default);
        Assert.True(reviseResult.Succeeded);

        // Set Status.
        var statusResult = await commandDispatcher.DispatchAsync(
            new SetRequirementStatusCommand(created.Id, RequirementStatus.Reviewed), default);
        Assert.True(statusResult.Succeeded);

        // Set Owner / Priority.
        var ownerResult = await commandDispatcher.DispatchAsync(new SetRequirementOwnerCommand(created.Id, "integration-tester"), default);
        Assert.True(ownerResult.Succeeded);
        var priorityResult = await commandDispatcher.DispatchAsync(new SetRequirementPriorityCommand(created.Id, RequirementPriority.Critical), default);
        Assert.True(priorityResult.Succeeded);

        // Duplicate.
        var duplicateResult = await commandDispatcher.DispatchAsync(new DuplicateRequirementCommand(created.Id, "REQ-IT-001-DUP"), default);
        Assert.True(duplicateResult.Succeeded);
        var duplicate = await requirementsService.FindByIdentifierAsync("REQ-IT-001-DUP");
        Assert.NotNull(duplicate);
        Assert.Equal(RequirementPriority.Critical, duplicate!.Priority);

        // Delete, the original.
        var deleteResult = await commandDispatcher.DispatchAsync(new DeleteRequirementCommand(created.Id), default);
        Assert.True(deleteResult.Succeeded);

        wingChildren = await workspace.ProjectExplorer.GetChildrenAsync(seeded.WingGroupId!.Value);
        Assert.DoesNotContain(wingChildren, n => n.Id == created.Id);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task LinkRequirementCommand_RecordsARealRelationship()
    {
        using var temp = new TempDirectory();
        var (_, manager, host) = await StartAsync(temp.Path);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
        var first = await requirementsService.FindByIdentifierAsync("REQ-AV-001");
        var second = await requirementsService.FindByIdentifierAsync("REQ-AV-003");

        var result = await commandDispatcher.DispatchAsync(
            new LinkRequirementCommand(second!.Id, first!.Id, RequirementRelationshipKinds.References), default);

        Assert.True(result.Succeeded);
        var relationships = await requirementsService.GetRelationshipsAsync(second.Id);
        Assert.Contains(relationships, r => r.TargetDocumentId == first.Id && r.RelationshipKind == RequirementRelationshipKinds.References);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task AddRequirementToCollectionCommand_AddsARealMember()
    {
        using var temp = new TempDirectory();
        var (_, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
        var requirement = await requirementsService.CreateAsync("REQ-IT-002", "Another integration test requirement.");

        var result = await commandDispatcher.DispatchAsync(
            new AddRequirementToCollectionCommand(requirement.Id, seeded.AvionicsCollectionId!.Value), default);

        Assert.True(result.Succeeded);
        var collection = await requirementsService.FindCollectionAsync(seeded.AvionicsCollectionId!.Value);
        Assert.Contains(requirement.Id, collection!.MemberRequirementIds);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task BulkSetRequirementStatusCommand_AppliesToEveryRequirementInTheSet()
    {
        using var temp = new TempDirectory();
        var (_, manager, host) = await StartAsync(temp.Path);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
        var first = await requirementsService.CreateAsync("REQ-BULK-001", "Bulk one.");
        var second = await requirementsService.CreateAsync("REQ-BULK-002", "Bulk two.");

        var result = await commandDispatcher.DispatchAsync(
            new BulkSetRequirementStatusCommand([first.Id, second.Id], RequirementStatus.Reviewed), default);

        Assert.True(result.Succeeded);
        Assert.Equal(RequirementStatus.Reviewed, (await requirementsService.FindAsync(first.Id))!.Status);
        Assert.Equal(RequirementStatus.Reviewed, (await requirementsService.FindAsync(second.Id))!.Status);

        await manager.ShutdownAsync();
    }

    // ---- Engineering Cockpit KPIs (WP 9.1A), against the real seeded graph ----

    [Fact]
    public async Task Cockpit_RequirementsKpiCards_ReflectTheRealSeededGraph()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        var cards = cockpit.RequirementsKpiCards.ToDictionary(c => c.Label, c => c.Value);

        // 9 live (10 created, 1 soft-deleted).
        Assert.Equal("9", cards["Total Requirements"]);
        Assert.NotEqual("0", cards["Outstanding Actions"]);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Cockpit_AttentionItems_ReportsRequirementsManagementIsLive()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Contains(cockpit.AttentionItems, item => item.Title == "Requirements Management is live");

        await manager.ShutdownAsync();
    }
}
