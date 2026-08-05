using Tempest.App.Workspace;
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
/// End-to-end `WP 9.0A` Workspace integration test — the same composition
/// <c>Program.cs</c> itself performs (<see cref="MechanicalWorkspaceRegistration.Register"/>,
/// called after <see cref="WorkspaceManager.StartAsync"/> exactly as
/// `Program.cs` calls it), against the real, running <see cref="ITempestHost"/>,
/// the real <see cref="MechanicalProductStructureSampleModule"/>'s own
/// representative graph, and the real Command Framework — proving
/// navigation, tree drill-down, selection, the Property Inspector, and all
/// nine commands (six from `WP 9.0A`, three more from `WP 9.0B`) together,
/// the "Workspace integration tests" both Work Packages name explicitly.
/// </summary>
[Collection("Console output capture")]
public class MechanicalWorkspaceIntegrationTests
{
    private static async Task<(IWorkspace Workspace, WorkspaceManager Manager, ITempestHost Host)> StartAsync(string rootPath)
    {
        var host = new TempestHostBuilder([typeof(MechanicalWorkspaceExplorerModule), typeof(MechanicalProductStructureSampleModule)])
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

        // Disclosed, pre-existing platform timing characteristic (not
        // introduced by WP 9.0A): WorkspaceManager.StartAsync's own
        // WaitForServicesAsync only waits for ITempestHost.Services to
        // become non-null, which TempestHost sets *before* running module
        // InitialiseAsync (TempestHost.cs's own phase order: "Dependency
        // Injection Built" precedes "Module Initialisation"). No prior
        // Workspace test ever combined WorkspaceManager with a real,
        // data-seeding module, so no prior test observed this race. Waiting
        // on the seeded module's own HasRegistered flag here — exactly the
        // flag EngineeringDomainSampleModule already carries for the same
        // reason — is this test's own deterministic fix; it is not a
        // WorkspaceManager/TempestHost code change, which would be a larger,
        // separate platform concern.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (GetSeededModule(host) is not { HasRegistered: true } && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        var services = host.Services!;
        var domainContext = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        var commandDispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
        var commandRegistry = (ICommandRegistry)services.GetService(typeof(ICommandRegistry));
        var referenceIntegrityChecker = (IReferenceIntegrityChecker)services.GetService(typeof(IReferenceIntegrityChecker));

        MechanicalWorkspaceRegistration.Register(manager, domainContext, commandDispatcher, commandRegistry, referenceIntegrityChecker);

        return (workspace, manager, host);
    }

    private static MechanicalProductStructureSampleModule GetSeededModule(ITempestHost host) =>
        (MechanicalProductStructureSampleModule)host.Services!.GetService(typeof(MechanicalProductStructureSampleModule));

    [Fact]
    public async Task ProjectExplorer_MechanicalArea_RootIsTheSeededProject()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();

        var root = Assert.Single(roots);
        Assert.Equal(seeded.ProjectId, root.Id);
        Assert.Equal("Falcon Structural Assembly Project", root.Title);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ProjectExplorer_DrillIntoProject_FindsBothTopLevelAssemblies()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
        var children = await workspace.ProjectExplorer.GetChildrenAsync(seeded.ProjectId!.Value);

        Assert.Equal(2, children.Count);
        Assert.Contains(children, n => n.Id == seeded.WingAssemblyId);
        Assert.Contains(children, n => n.Id == seeded.EmpennageAssemblyId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ProjectExplorer_DeletedSeedPart_DoesNotAppearInTheTree()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
        var children = await workspace.ProjectExplorer.GetChildrenAsync(seeded.EmpennageAssemblyId!.Value);

        Assert.DoesNotContain(children, n => n.Id == seeded.DeletedPartId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_MechanicalObject_PropertyInspectorShowsRealFacets()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.ProjectId!.Value, "Project");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Name" && f.Value == "Falcon Structural Assembly Project");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Navigation_OpenSeededProject_ViewTitleIsTheRealDisplayName()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        var view = await workspace.Navigation.OpenAsync(seeded.ProjectId!.Value, "Project");

        Assert.Equal("Falcon Structural Assembly Project", view.Title);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task FullLifecycle_CreateRenameMoveCopyDuplicateDelete_AllSucceedAndExplorerReflectsEachStep()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);

        // Create, under the Wing Assembly.
        var createResult = await commandDispatcher.DispatchAsync(
            new CreateMechanicalObjectCommand("Part", "Integration Test Part", parentId: seeded.WingAssemblyId), default);
        Assert.True(createResult.Succeeded);

        var wingChildren = await workspace.ProjectExplorer.GetChildrenAsync(seeded.WingAssemblyId!.Value);
        var created = Assert.Single(wingChildren, n => n.Title == "Integration Test Part");

        // Rename.
        var renameResult = await commandDispatcher.DispatchAsync(
            new RenameMechanicalObjectCommand(created.Id, "Part", "Renamed Integration Test Part"), default);
        Assert.True(renameResult.Succeeded);

        wingChildren = await workspace.ProjectExplorer.GetChildrenAsync(seeded.WingAssemblyId!.Value);
        Assert.Contains(wingChildren, n => n.Id == created.Id && n.Title == "Renamed Integration Test Part");

        // Move, to the Empennage Assembly.
        var moveResult = await commandDispatcher.DispatchAsync(
            new MoveMechanicalObjectCommand(created.Id, "Part", seeded.EmpennageAssemblyId), default);
        Assert.True(moveResult.Succeeded);

        wingChildren = await workspace.ProjectExplorer.GetChildrenAsync(seeded.WingAssemblyId!.Value);
        Assert.DoesNotContain(wingChildren, n => n.Id == created.Id);
        var empennageChildren = await workspace.ProjectExplorer.GetChildrenAsync(seeded.EmpennageAssemblyId!.Value);
        Assert.Contains(empennageChildren, n => n.Id == created.Id);

        // Copy, back into the Wing Assembly.
        var copyResult = await commandDispatcher.DispatchAsync(
            new CopyMechanicalObjectCommand(created.Id, "Part", seeded.WingAssemblyId), default);
        Assert.True(copyResult.Succeeded);

        wingChildren = await workspace.ProjectExplorer.GetChildrenAsync(seeded.WingAssemblyId!.Value);
        var copy = Assert.Single(wingChildren, n => n.Title == "Renamed Integration Test Part (Copy)");

        // Duplicate, staying in place (Wing Assembly).
        var duplicateResult = await commandDispatcher.DispatchAsync(
            new DuplicateMechanicalObjectCommand(copy.Id, "Part"), default);
        Assert.True(duplicateResult.Succeeded);

        wingChildren = await workspace.ProjectExplorer.GetChildrenAsync(seeded.WingAssemblyId!.Value);
        Assert.Contains(wingChildren, n => n.Title == "Renamed Integration Test Part (Copy) (Copy)");

        // Delete, the original moved Part (now in the Empennage Assembly, no children of its own).
        var deleteResult = await commandDispatcher.DispatchAsync(
            new DeleteMechanicalObjectCommand(created.Id, "Part"), default);
        Assert.True(deleteResult.Succeeded);

        empennageChildren = await workspace.ProjectExplorer.GetChildrenAsync(seeded.EmpennageAssemblyId!.Value);
        Assert.DoesNotContain(empennageChildren, n => n.Id == created.Id);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task CommandRegistry_ListsAllNineMechanicalCommands()
    {
        // WP 9.0A shipped six (Create/Rename/Delete/Move/Copy/Duplicate);
        // WP 9.0B adds three more (Set BOM Line, Compare Baselines,
        // Validate Configuration).
        using var temp = new TempDirectory();
        var (_, manager, host) = await StartAsync(temp.Path);
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

        var mechanicalCommands = commandRegistry.Items.Where(d => d.Category == "Mechanical").ToList();

        Assert.Equal(9, mechanicalCommands.Count);

        await manager.ShutdownAsync();
    }

    // ---- WP 9.0B: BOM / Configuration Management, against the real seeded graph ----

    [Fact]
    public async Task ProjectExplorer_SeededPartsShowBomAwareTitles()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
        var wingChildren = await workspace.ProjectExplorer.GetChildrenAsync(seeded.WingAssemblyId!.Value);

        Assert.Contains(wingChildren, n => n.Title.StartsWith("0010 ×4 Wing Skin Panel"));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_SeededPart_PropertyInspectorShowsRealBomFacets()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.SparWebPlateId!.Value, "Part");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Item Number" && f.Value == "0020");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Baseline");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SetBomLineCommand_AgainstSeededPart_UpdatesTheRealFacet()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

        var result = await commandDispatcher.DispatchAsync(
            new SetBomLineCommand(seeded.SparCapPartId!.Value, "Part", 3m, "EA", itemNumber: "0031"), default);

        Assert.True(result.Succeeded);
        await workspace.Selection.SelectAsync(seeded.SparCapPartId!.Value, "Part");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Quantity" && f.Value == "3");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task CompareBaselinesCommand_SeededBaselineAndRelease_ReportsAddedAndRevisionChanged()
    {
        using var temp = new TempDirectory();
        var (_, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

        var result = await commandDispatcher.DispatchAsync(
            new CompareBaselinesCommand(seeded.BaselineId!.Value, seeded.ReleaseId!.Value), default);

        Assert.True(result.Succeeded);
        Assert.Contains("1 added", result.Message);
        Assert.Contains("1 revision-changed", result.Message);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ValidateConfigurationCommand_SeededRelease_IsConsistent()
    {
        using var temp = new TempDirectory();
        var (_, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

        var result = await commandDispatcher.DispatchAsync(
            new ValidateConfigurationCommand(seeded.ReleaseId!.Value, "Release"), default);

        Assert.True(result.Succeeded);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_SeededRelease_PropertyInspectorShowsReleasedStatus()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.ReleaseId!.Value, "Release");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Status" && f.Value == "Released");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Configuration Members" && f.Value == "3");

        await manager.ShutdownAsync();
    }
}
