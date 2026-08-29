using Tempest.App.Projects;
using Tempest.App.Shell;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Settings;

namespace Tempest.Core.Tests.Shell;

/// <summary>
/// The project-centric convergence (`TD-89`): a real relationship between
/// <c>Global Application → Module → Project → Project Workspace →
/// Engineering Workspace → Engineering Objects</c>, <b>and</b> a standalone
/// engineering workflow that requires no project at all.
/// </summary>
/// <remarks>
/// Every test runs against the real spine over the real persistent stores.
/// A "lifetime" is a fresh object graph over whatever the shared
/// persistence store already holds, with the production rehydration step
/// run over it — so restart behaviour is genuine, not simulated.
/// </remarks>
public class ProductConvergenceTests
{
    private sealed record Spine(
        EngineeringDomainContext Domain,
        IProjectDirectory Directory,
        IProjectContext Context,
        IShellNavigator Navigator,
        IEngineeringScope Scope,
        ISettingsProvider Settings,
        IPersistenceStore Persistence);

    private static async Task<Spine> BuildAsync(ISettingsProvider? settings = null, IPersistenceStore? persistence = null)
    {
        var store = persistence ?? new Materials.InMemoryPersistenceStore();
        var principal = new CurrentPrincipalAccessor();
        var documents = new EngineeringDocumentStore(store, principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);

        var domain = new EngineeringDomainContext(
            documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, new EngineeringObjectStateStore(store));

        var rehydrators = new EngineeringObjectRehydratorRegistry();
        MechanicalObjectFactoryRegistry.RegisterRehydrators(rehydrators, domain);
        Tempest.App.Workspace.Calculations.CalculationObjectFactoryRegistry.RegisterRehydrators(rehydrators, domain);
        Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.RegisterRehydrators(rehydrators, domain);
        await new EngineeringObjectRehydrationService(domain, rehydrators).RehydrateAsync();

        var eventBus = new EventBus();
        var settingsProvider = settings ?? new SettingsProvider(new Materials.InMemoryPersistenceStore(), new EventBus());

        var directory = new ProjectDirectory(domain);
        var context = new ProjectContext(directory, eventBus, settingsProvider);
        var navigator = new ShellNavigator(context, eventBus, settingsProvider);
        var scope = new EngineeringScope(navigator, context, domain);

        return new Spine(domain, directory, context, navigator, scope, settingsProvider, store);
    }

    private static async Task<T> CreateAsync<T>(
        EngineeringDomainContext domain, string kind, Func<IEngineeringDocument, IDocumentRevision, T> ctor, Guid? parentId = null)
        where T : EngineeringObjectBase
    {
        var created = (T)await new EngineeringObjectFactory<T>(kind, domain, ctor).CreateAsync($"{kind} — test.");
        if (parentId is { } id)
            await ((IHasParent)created).MoveAsync(id);
        return created;
    }

    private static Task<Part> CreatePartAsync(EngineeringDomainContext domain, string identifier, string name, Guid? parentId = null) =>
        CreateAsync(domain, MechanicalObjectFactoryRegistry.Part,
            (d, r) => new Part(d, r, domain, identifier, name, EngineeringObjectMetadata.Empty), parentId);

    // ================================================================
    // Navigation model — every destination is real or declared
    // ================================================================

    [Fact]
    public void EveryDeclaredGlobalModule_HasADescriptor_AndEveryUnimplementedOneNamesWhatTracksIt()
    {
        foreach (var area in Enum.GetValues<ShellArea>())
        {
            var descriptor = ShellAreas.For(area);

            Assert.False(string.IsNullOrWhiteSpace(descriptor.Title));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Note));

            if (descriptor.Availability == NavigationAvailability.Declared)
                Assert.False(string.IsNullOrWhiteSpace(descriptor.TrackedBy),
                    $"Module '{area}' is declared but not implemented, and must name the debt item that tracks it.");
        }
    }

    [Fact]
    public void EveryDeclaredProjectArea_HasADescriptor_AndEveryUnimplementedOneNamesWhatTracksIt()
    {
        foreach (var area in Enum.GetValues<ProjectArea>())
        {
            var descriptor = ProjectAreas.For(area);

            Assert.False(string.IsNullOrWhiteSpace(descriptor.Title));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Note));

            if (descriptor.Availability == NavigationAvailability.Declared)
                Assert.False(string.IsNullOrWhiteSpace(descriptor.TrackedBy),
                    $"Project area '{area}' is declared but not implemented, and must name the debt item that tracks it.");
        }
    }

    [Fact]
    public void TheProductsDesignedModuleAndAreaSets_AreBothPresent()
    {
        // The shell shows the product TempestOS is, not only the part that
        // is finished — with everything unfinished marked, never faked.
        Assert.Contains(ShellArea.Tasks, ShellAreas.All.Select(m => m.Area));
        Assert.Contains(ShellArea.Commercial, ShellAreas.All.Select(m => m.Area));
        Assert.Contains(ShellArea.Administration, ShellAreas.All.Select(m => m.Area));

        Assert.Contains(ProjectArea.Tasks, ProjectAreas.All.Select(a => a.Area));
        Assert.Contains(ProjectArea.Risks, ProjectAreas.All.Select(a => a.Area));
        Assert.Contains(ProjectArea.Timeline, ProjectAreas.All.Select(a => a.Area));
        Assert.Contains(ProjectArea.Reports, ProjectAreas.All.Select(a => a.Area));
        Assert.Contains(ProjectArea.Settings, ProjectAreas.All.Select(a => a.Area));

        // The rail never offers the project workspace: it is reached by
        // opening a project.
        Assert.DoesNotContain(ShellArea.ProjectWorkspace, ShellAreas.RailModules.Select(m => m.Area));
    }

    [Fact]
    public async Task ADeclaredModule_IsARealNavigationDestination_NotADeadButton()
    {
        var spine = await BuildAsync();

        await spine.Navigator.GoToModuleAsync(ShellArea.Tasks);

        Assert.Equal(ShellArea.Tasks, spine.Navigator.Current.Area);
        Assert.False(spine.Navigator.Current.IsProjectScoped);
        Assert.Equal(NavigationAvailability.Declared, ShellAreas.For(ShellArea.Tasks).Availability);
    }

    [Fact]
    public async Task NavigatingToAGlobalModule_NeverClosesTheOpenProject()
    {
        var spine = await BuildAsync();
        var project = await spine.Directory.CreateAsync("P-0001", "Apollo");
        await spine.Navigator.OpenProjectAsync(project.Id);

        await spine.Navigator.GoToModuleAsync(ShellArea.Knowledge);

        Assert.True(spine.Context.HasProject);
        Assert.Equal(project.Id, spine.Context.Current!.Id);
    }

    [Theory]
    [InlineData(ShellArea.ProjectWorkspace)]
    [InlineData(ShellArea.Engineering)]
    public async Task GoToModuleAsync_RefusesTheTwoAreasThatHaveTheirOwnScopedVerbs(ShellArea area)
    {
        var spine = await BuildAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => spine.Navigator.GoToModuleAsync(area));
    }

    // ================================================================
    // Project membership — one definition, transitive
    // ================================================================

    [Fact]
    public async Task ProjectContents_AreResolvedTransitively_NotOnlyDirectChildren()
    {
        var spine = await BuildAsync();
        var project = await spine.Directory.CreateAsync("P-0001", "Apollo");

        var assembly = await CreateAsync(spine.Domain, MechanicalObjectFactoryRegistry.Assembly,
            (d, r) => new Assembly(d, r, spine.Domain, "ASM-1", "Pump Head", EngineeringObjectMetadata.Empty), project.Id);
        var part = await CreatePartAsync(spine.Domain, "PN-1", "Impeller", assembly.Id);

        var contents = await spine.Directory.ListProjectContentsAsync(project.Id);

        // The Part is two levels down. It is still in the project.
        Assert.Contains(assembly.Id, contents);
        Assert.Contains(part.Id, contents);

        // The project is never a member of itself.
        Assert.DoesNotContain(project.Id, contents);
    }

    [Fact]
    public async Task ADeletedObject_IsNoLongerProjectContent()
    {
        var spine = await BuildAsync();
        var project = await spine.Directory.CreateAsync("P-0001", "Apollo");
        var part = await CreatePartAsync(spine.Domain, "PN-1", "Impeller", project.Id);

        Assert.Contains(part.Id, await spine.Directory.ListProjectContentsAsync(project.Id));

        await ((IDeletable)part).DeleteAsync();

        Assert.DoesNotContain(part.Id, await spine.Directory.ListProjectContentsAsync(project.Id));
    }

    [Fact]
    public async Task TwoProjects_NeverSeeEachOthersEngineeringObjects()
    {
        var spine = await BuildAsync();
        var apollo = await spine.Directory.CreateAsync("P-0001", "Apollo");
        var vulcan = await spine.Directory.CreateAsync("P-0002", "Vulcan");

        var apolloPart = await CreatePartAsync(spine.Domain, "PN-1", "Impeller", apollo.Id);
        var vulcanPart = await CreatePartAsync(spine.Domain, "PN-2", "Nozzle", vulcan.Id);

        var apolloContents = await spine.Directory.ListProjectContentsAsync(apollo.Id);
        var vulcanContents = await spine.Directory.ListProjectContentsAsync(vulcan.Id);

        Assert.Contains(apolloPart.Id, apolloContents);
        Assert.DoesNotContain(vulcanPart.Id, apolloContents);
        Assert.Contains(vulcanPart.Id, vulcanContents);
        Assert.DoesNotContain(apolloPart.Id, vulcanContents);
    }

    // ================================================================
    // Engineering scope — the two valid modes
    // ================================================================

    [Fact]
    public async Task EngineeringScope_InAProject_ListsThatProjectsObjectsOnly()
    {
        var spine = await BuildAsync();
        var project = await spine.Directory.CreateAsync("P-0001", "Apollo");
        var inProject = await CreatePartAsync(spine.Domain, "PN-1", "Impeller", project.Id);
        var standalone = await CreatePartAsync(spine.Domain, "PN-9", "Loose Part");

        await spine.Navigator.OpenProjectAsync(project.Id);
        await spine.Navigator.GoToEngineeringAsync();

        Assert.Equal(EngineeringScopeKind.Project, spine.Scope.Current.Kind);
        Assert.Equal(project.Id, spine.Scope.Current.ProjectId);
        Assert.Equal("P-0001 Apollo", spine.Scope.Current.Label);

        var objects = await spine.Scope.ListObjectsAsync();
        Assert.Contains(objects, o => o.Id == inProject.Id);
        Assert.DoesNotContain(objects, o => o.Id == standalone.Id);

        Assert.True(await spine.Scope.ContainsAsync(inProject.Id));
        Assert.False(await spine.Scope.ContainsAsync(standalone.Id));
    }

    [Fact]
    public async Task EngineeringScope_Standalone_ListsOnlyObjectsBelongingToNoProject()
    {
        var spine = await BuildAsync();
        var project = await spine.Directory.CreateAsync("P-0001", "Apollo");
        var inProject = await CreatePartAsync(spine.Domain, "PN-1", "Impeller", project.Id);
        var standalone = await CreatePartAsync(spine.Domain, "PN-9", "Loose Part");

        await spine.Navigator.GoToEngineeringAsync();

        Assert.Equal(EngineeringScopeKind.Standalone, spine.Scope.Current.Kind);
        Assert.Null(spine.Scope.Current.ProjectId);

        var objects = await spine.Scope.ListObjectsAsync();
        Assert.Contains(objects, o => o.Id == standalone.Id);
        Assert.DoesNotContain(objects, o => o.Id == inProject.Id);

        // A project is never standalone engineering work either.
        Assert.DoesNotContain(objects, o => o.Id == project.Id);
    }

    [Fact]
    public async Task MovingStandaloneWorkIntoAProject_ChangesItsScope_WithNoSecondMechanism()
    {
        var spine = await BuildAsync();
        var project = await spine.Directory.CreateAsync("P-0001", "Apollo");
        var part = await CreatePartAsync(spine.Domain, "PN-9", "Loose Part");

        Assert.DoesNotContain(part.Id, await spine.Directory.ListProjectContentsAsync(project.Id));

        // The existing structural edge is the only ownership mechanism.
        await ((IHasParent)part).MoveAsync(project.Id);

        Assert.Contains(part.Id, await spine.Directory.ListProjectContentsAsync(project.Id));
    }

    // ================================================================
    // Project lifecycle
    // ================================================================

    [Fact]
    public async Task SwitchingProjects_MovesBothTheContextAndTheLocationTogether()
    {
        var spine = await BuildAsync();
        var apollo = await spine.Directory.CreateAsync("P-0001", "Apollo");
        var vulcan = await spine.Directory.CreateAsync("P-0002", "Vulcan");

        await spine.Navigator.OpenProjectAsync(apollo.Id, ProjectArea.Requirements);
        Assert.Equal(apollo.Id, spine.Context.Current!.Id);

        await spine.Navigator.OpenProjectAsync(vulcan.Id);

        Assert.Equal(vulcan.Id, spine.Context.Current!.Id);
        Assert.Equal(vulcan.Id, spine.Navigator.Current.ProjectId);
        Assert.Equal(ProjectArea.Overview, spine.Navigator.Current.ProjectArea);
    }

    [Fact]
    public async Task ProjectDomainDataAndSessionState_AreSeparatelyDurable()
    {
        // Domain data lives in the engineering persistence store (`TD-85`);
        // session state lives in the settings substrate (`ADR-0064`). The
        // two are deliberately different mechanisms with different
        // lifetimes, and this proves they are genuinely independent.
        var domainStore = new Materials.InMemoryPersistenceStore();
        var settings = new SettingsProvider(new Materials.InMemoryPersistenceStore(), new EventBus());

        var first = await BuildAsync(settings, domainStore);
        var project = await first.Directory.CreateAsync("P-0001", "Apollo");
        await CreatePartAsync(first.Domain, "PN-1", "Impeller", project.Id);
        await first.Navigator.OpenProjectAsync(project.Id, ProjectArea.Requirements);
        await first.Context.SaveAsync();
        await first.Navigator.SaveAsync();

        // Same domain data, brand new session state: the work survives, the
        // session does not.
        var freshSession = await BuildAsync(
            new SettingsProvider(new Materials.InMemoryPersistenceStore(), new EventBus()), domainStore);

        Assert.Single(await freshSession.Directory.ListAsync());
        await freshSession.Navigator.LoadAsync();
        Assert.Equal(ShellArea.Home, freshSession.Navigator.Current.Area);
        Assert.False(freshSession.Context.HasProject);

        // Same session state, brand new domain data: the session cannot
        // restore a project that does not exist, and degrades honestly.
        var freshDomain = await BuildAsync(settings, new Materials.InMemoryPersistenceStore());
        await freshDomain.Navigator.LoadAsync();
        Assert.Equal(ShellArea.Home, freshDomain.Navigator.Current.Area);
        Assert.False(freshDomain.Context.HasProject);
    }

    // ================================================================
    // Restart: project scope and standalone scope both restore
    // ================================================================

    [Fact]
    public async Task AfterRestart_AProjectScopedEngineeringLocation_RestoresBothTheProjectAndTheScope()
    {
        var domainStore = new Materials.InMemoryPersistenceStore();
        var settings = new SettingsProvider(new Materials.InMemoryPersistenceStore(), new EventBus());

        var first = await BuildAsync(settings, domainStore);
        var project = await first.Directory.CreateAsync("P-0001", "Apollo");
        var part = await CreatePartAsync(first.Domain, "PN-1", "Impeller", project.Id);
        await first.Navigator.OpenProjectAsync(project.Id);
        await first.Navigator.GoToEngineeringAsync();
        await first.Context.SaveAsync();
        await first.Navigator.SaveAsync();

        var second = await BuildAsync(settings, domainStore);
        await second.Navigator.LoadAsync();

        Assert.Equal(ShellArea.Engineering, second.Navigator.Current.Area);
        Assert.Equal(project.Id, second.Navigator.Current.ProjectId);
        Assert.Equal(EngineeringScopeKind.Project, second.Scope.Current.Kind);

        var objects = await second.Scope.ListObjectsAsync();
        Assert.Contains(objects, o => o.Id == part.Id);
    }

    [Fact]
    public async Task AfterRestart_AStandaloneEngineeringLocation_RestoresAsStandalone_WithNoProject()
    {
        var domainStore = new Materials.InMemoryPersistenceStore();
        var settings = new SettingsProvider(new Materials.InMemoryPersistenceStore(), new EventBus());

        var first = await BuildAsync(settings, domainStore);
        var loose = await CreatePartAsync(first.Domain, "PN-9", "Loose Part");
        await first.Navigator.GoToStandaloneEngineeringAsync();
        await first.Context.SaveAsync();
        await first.Navigator.SaveAsync();

        var second = await BuildAsync(settings, domainStore);
        await second.Navigator.LoadAsync();

        Assert.Equal(ShellArea.Engineering, second.Navigator.Current.Area);
        Assert.True(second.Navigator.Current.IsStandaloneEngineering);
        Assert.False(second.Context.HasProject);

        var objects = await second.Scope.ListObjectsAsync();
        Assert.Contains(objects, o => o.Id == loose.Id);
    }
}
