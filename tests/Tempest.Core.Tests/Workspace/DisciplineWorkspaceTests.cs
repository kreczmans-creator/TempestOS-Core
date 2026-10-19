using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Disciplines;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `ADR-0163`: Structural, Electrical and Building Services / HVAC, built
/// on the shared <see cref="DisciplineObject"/> shape — their Explorer
/// folders, the full create → attribute → status round trip through the
/// real Command Framework, and survival across a restart.
/// </summary>
public class DisciplineWorkspaceTests
{
    private static async Task<(IWorkspace Workspace, WorkspaceManager Manager, ICommandDispatcher Dispatcher, ICommandRegistry Registry, EngineeringDomainContext Domain)> StartAsync(string rootPath)
    {
        var host = new TempestHostBuilder(
        [
            typeof(Tempest.Workspace.Mechanical.MechanicalWorkspaceExplorerModule),
            typeof(StructuralWorkspaceExplorerModule),
            typeof(ElectricalWorkspaceExplorerModule),
            typeof(BuildingServicesWorkspaceExplorerModule),
        ])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);
        var workspace = await manager.StartAsync();

        var services = host.Services!;
        var domain = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        var dispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
        var registry = (ICommandRegistry)services.GetService(typeof(ICommandRegistry));

        // The production composition: every discipline, every rehydrator,
        // then the persisted objects brought back.
        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host);
        await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);

        return (workspace, manager, dispatcher, registry, domain);
    }

    public static TheoryData<string> Disciplines() => ["Structural", "Electrical", "Building Services"];

    [Theory]
    [MemberData(nameof(Disciplines))]
    public async Task ExplorerArea_ListsOneEmptyFolderPerKind(string category)
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _, _, _) = await StartAsync(temp.Path);
        var definition = DisciplineCatalogue.All.Single(d => d.Category == category);

        await workspace.Navigation.SwitchAreaAsync(definition.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();

        Assert.Equal(definition.Kinds.Select(k => k.Folder), roots.Select(r => r.Title));
        Assert.All(roots, r => Assert.False(r.HasChildren));

        await manager.ShutdownAsync();
    }

    [Theory]
    [MemberData(nameof(Disciplines))]
    public async Task EveryDiscipline_RegistersItsFullCommandSetUnderItsOwnCategory(string category)
    {
        using var temp = new TempDirectory();
        var (_, manager, _, registry, _) = await StartAsync(temp.Path);
        var definition = DisciplineCatalogue.All.Single(d => d.Category == category);

        foreach (var verb in new[] { "create", "rename", "edit", "delete", "move", "request-review", "approve", "release", "set-attribute" })
        {
            var descriptor = Assert.Single(registry.Items, d => d.Id == definition.CommandId(verb));
            Assert.Equal(category, descriptor.Category);
        }

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task CreateThenSetAttribute_ListsUnderItsFolderAndShowsTheValueInProperties()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, dispatcher, _, _) = await StartAsync(temp.Path);

        var created = await dispatcher.DispatchAsync(new CreateDisciplineObjectCommand("StructuralMember", "Ground floor beam B1"), CancellationToken.None);
        Assert.True(created.Succeeded, created.Message);
        var id = created.SubjectId!.Value;

        var set = await dispatcher.DispatchAsync(new SetDisciplineAttributeCommand(id, "StructuralMember", "Section", "UB 305x165x40"), CancellationToken.None);
        Assert.True(set.Succeeded, set.Message);

        await workspace.Navigation.SwitchAreaAsync(DisciplineCatalogue.Structural.NavigationItemId);
        var members = await workspace.ProjectExplorer.GetChildrenAsync(DisciplineCatalogue.Structural.Kinds[0].FolderNodeId);
        Assert.Equal("Ground floor beam B1", Assert.Single(members).Title);

        await workspace.Selection.SelectAsync(id, "StructuralMember");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f is { Name: "Section", Value: "UB 305x165x40" });
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f is { Name: "Grade", Value: "(not recorded)" });

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SetAttribute_RefusesAnAttributeTheKindDoesNotDeclare()
    {
        using var temp = new TempDirectory();
        var (_, manager, dispatcher, _, _) = await StartAsync(temp.Path);

        var created = await dispatcher.DispatchAsync(new CreateDisciplineObjectCommand("ElectricalCircuit", "Lighting L1"), CancellationToken.None);
        var result = await dispatcher.DispatchAsync(
            new SetDisciplineAttributeCommand(created.SubjectId!.Value, "ElectricalCircuit", "Section", "x"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("Rating (A)", result.Message);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Create_RefusesAKindNoDisciplineOffers()
    {
        using var temp = new TempDirectory();
        var (_, manager, dispatcher, _, _) = await StartAsync(temp.Path);

        var result = await dispatcher.DispatchAsync(new CreateDisciplineObjectCommand("Part", "Not mine"), CancellationToken.None);

        Assert.False(result.Succeeded);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task AttributesAndStatus_SurviveARestart()
    {
        using var temp = new TempDirectory();
        Guid id;
        {
            var (_, manager, dispatcher, _, _) = await StartAsync(temp.Path);
            var created = await dispatcher.DispatchAsync(new CreateDisciplineObjectCommand("BuildingServicesZone", "Level 2 office"), CancellationToken.None);
            id = created.SubjectId!.Value;
            await dispatcher.DispatchAsync(new SetDisciplineAttributeCommand(id, "BuildingServicesZone", "Floor area (m²)", "420"), CancellationToken.None);
            await dispatcher.DispatchAsync(new SetDisciplineAttributeCommand(id, "BuildingServicesZone", "Occupancy", "35"), CancellationToken.None);
            await dispatcher.DispatchAsync(new SetDisciplineAttributeCommand(id, "BuildingServicesZone", "Occupancy", ""), CancellationToken.None);
            foreach (var status in new[] { LifecycleState.InReview, LifecycleState.Approved, LifecycleState.Released })
            {
                var transitioned = await dispatcher.DispatchAsync(new SetDisciplineObjectStatusCommand(id, "BuildingServicesZone", status), CancellationToken.None);
                Assert.True(transitioned.Succeeded, transitioned.Message);
            }
            await manager.ShutdownAsync();
        }

        {
            var (_, manager, _, _, domain) = await StartAsync(temp.Path);
            var zone = Assert.IsAssignableFrom<IDisciplineObject>(await domain.Repository.FindAsync(id));

            Assert.Equal("BuildingServicesZone", zone.Kind);
            Assert.Equal("420", zone.Attributes["Floor area (m²)"]);
            Assert.False(zone.Attributes.ContainsKey("Occupancy"));
            Assert.Equal(LifecycleState.Released, zone.Status);
            await manager.ShutdownAsync();
        }
    }
}
