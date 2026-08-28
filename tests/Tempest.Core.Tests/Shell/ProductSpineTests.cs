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
/// The Product Spine — `Module → Project → Workspace` as real, persisted,
/// testable application state (`TD-84`).
/// </summary>
/// <remarks>
/// Every test here runs against the <b>real</b> engineering domain
/// (`EngineeringDomainContext` over the real, persistent
/// `EngineeringDocumentStore` and `EngineeringObjectStateStore`) and the
/// <b>real</b> settings substrate — never a mock project or a stubbed
/// context. A project created here is the same `IProject` engineering
/// object the Engineering Workspace, Project Explorer and audit trail
/// already understand.
///
/// **`TD-85`.** Each `BuildSpineAsync` call is a fresh application
/// lifetime: a brand new in-memory object graph, over whatever durable
/// state the supplied persistence store already holds, with the real
/// `EngineeringObjectRehydrationService` run over it exactly as the
/// composition root runs it at startup. Sharing a persistence store
/// between two calls therefore models a genuine close-and-relaunch, and
/// the restart tests below prove the objects themselves come back — not
/// merely a summary of them.
/// </remarks>
public class ProductSpineTests
{
    private sealed record Spine(
        EngineeringDomainContext Domain,
        IProjectDirectory Directory,
        IProjectContext Context,
        IShellNavigator Navigator,
        ISettingsProvider Settings,
        IEventBus EventBus);

    private static async Task<Spine> BuildSpineAsync(ISettingsProvider? settings = null, IPersistenceStore? persistence = null)
    {
        var persistenceStore = persistence ?? new Materials.InMemoryPersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var store = new EngineeringDocumentStore(persistenceStore, principalAccessor);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);
        var domain = new EngineeringDomainContext(
            store, repository, relationshipRepository, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(relationshipDiscovery, repository), principalAccessor,
            new EngineeringObjectStateStore(persistenceStore));

        // The identical rehydration the composition root performs at
        // startup (`TD-85`) — same registry, same service, same order.
        var rehydrators = new EngineeringObjectRehydratorRegistry();
        MechanicalObjectFactoryRegistry.RegisterRehydrators(rehydrators, domain);
        await new EngineeringObjectRehydrationService(domain, rehydrators).RehydrateAsync();

        var eventBus = new EventBus();
        var settingsProvider = settings ?? new SettingsProvider(new Materials.InMemoryPersistenceStore(), new EventBus());

        var directory = new ProjectDirectory(domain);
        var context = new ProjectContext(directory, eventBus, settingsProvider);
        var navigator = new ShellNavigator(context, eventBus, settingsProvider);

        return new Spine(domain, directory, context, navigator, settingsProvider, eventBus);
    }

    // ----------------------------------------------------------------
    // Projects are real domain objects, not a second model
    // ----------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ProducesARealEngineeringObject_VisibleToTheDomainItself()
    {
        var spine = await BuildSpineAsync();

        var project = await spine.Directory.CreateAsync("P-0027", "Apollo Pump Redesign");

        // The product shell created it; the *domain* can see it, because it
        // is the same object — not a parallel record.
        var fromDomain = await spine.Domain.Repository.FindAsync(project.Id);
        var typed = Assert.IsAssignableFrom<IProject>(fromDomain);
        Assert.Equal("P-0027", typed.Identifier);
        Assert.Equal("Apollo Pump Redesign", typed.DisplayName);

        // And it carries the full engineering-object capability set.
        Assert.IsAssignableFrom<IHasLifecycle>(typed);
        Assert.IsAssignableFrom<ITraceable>(typed);
        Assert.IsAssignableFrom<IHasRelationships>(typed);
    }

    [Fact]
    public async Task ListAsync_ReturnsEveryProject_OrderedByIdentifier()
    {
        var spine = await BuildSpineAsync();
        await spine.Directory.CreateAsync("P-0031", "Data Centre Cooling");
        await spine.Directory.CreateAsync("P-0011", "Hydraulic Manifold");
        await spine.Directory.CreateAsync("P-0027", "Apollo Pump Redesign");

        var projects = await spine.Directory.ListAsync();

        Assert.Equal(["P-0011", "P-0027", "P-0031"], projects.Select(p => p.Identifier));
    }

    [Fact]
    public async Task CreateAsync_DuplicateIdentifier_IsRefused()
    {
        var spine = await BuildSpineAsync();
        await spine.Directory.CreateAsync("P-0027", "Apollo Pump Redesign");

        await Assert.ThrowsAsync<DuplicateProjectIdentifierException>(
            () => spine.Directory.CreateAsync("P-0027", "A Different Project"));
    }

    [Fact]
    public async Task Label_MatchesTheMockUpsOwnForm()
    {
        var spine = await BuildSpineAsync();
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo Pump Redesign");

        Assert.Equal("P-0027 Apollo Pump Redesign", project.Label);
    }

    // ----------------------------------------------------------------
    // Project context is real state, with an event, not UI decoration
    // ----------------------------------------------------------------

    [Fact]
    public async Task OpeningAProject_MakesItCurrent_AndPublishesTheChangeOnce()
    {
        var spine = await BuildSpineAsync();
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo Pump Redesign");

        var observed = new List<ProjectContextChangedEvent>();
        spine.EventBus.Subscribe(new CapturingHandler<ProjectContextChangedEvent>(observed.Add));

        Assert.False(spine.Context.HasProject);
        await spine.Context.OpenAsync(project.Id);

        Assert.True(spine.Context.HasProject);
        Assert.Equal(project.Id, spine.Context.Current!.Id);
        var change = Assert.Single(observed);
        Assert.Null(change.Previous);
        Assert.Equal(project.Id, change.Current!.Id);
    }

    [Fact]
    public async Task ReopeningTheSameProject_PublishesNoSpuriousChange()
    {
        var spine = await BuildSpineAsync();
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo");
        await spine.Context.OpenAsync(project.Id);

        var observed = new List<ProjectContextChangedEvent>();
        spine.EventBus.Subscribe(new CapturingHandler<ProjectContextChangedEvent>(observed.Add));

        await spine.Context.OpenAsync(project.Id);

        Assert.Empty(observed);
    }

    [Fact]
    public async Task SwitchingProject_CarriesBothEndsOfTheMove()
    {
        var spine = await BuildSpineAsync();
        var first = await spine.Directory.CreateAsync("P-0011", "Hydraulic Manifold");
        var second = await spine.Directory.CreateAsync("P-0027", "Apollo");
        await spine.Context.OpenAsync(first.Id);

        var observed = new List<ProjectContextChangedEvent>();
        spine.EventBus.Subscribe(new CapturingHandler<ProjectContextChangedEvent>(observed.Add));

        await spine.Context.OpenAsync(second.Id);

        var change = Assert.Single(observed);
        Assert.Equal(first.Id, change.Previous!.Id);
        Assert.Equal(second.Id, change.Current!.Id);
        Assert.Equal(second.Id, spine.Context.Current!.Id);
    }

    [Fact]
    public async Task OpeningAProjectThatDoesNotExist_IsRefused_AndLeavesTheContextUntouched()
    {
        var spine = await BuildSpineAsync();
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo");
        await spine.Context.OpenAsync(project.Id);

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => spine.Context.OpenAsync(Guid.NewGuid()));

        Assert.Equal(project.Id, spine.Context.Current!.Id);
    }

    // ----------------------------------------------------------------
    // Navigation: Module -> Project -> Workspace, explicit and testable
    // ----------------------------------------------------------------

    [Fact]
    public async Task TheApplicationStartsAtHome_WithNoProject()
    {
        var spine = await BuildSpineAsync();

        Assert.Equal(ShellArea.Home, spine.Navigator.Current.Area);
        Assert.False(spine.Navigator.Current.IsProjectScoped);
        Assert.False(spine.Context.HasProject);
    }

    [Fact]
    public async Task OpeningAProject_MovesIntoItsWorkspace_AndOpensTheContextInTheSameMove()
    {
        var spine = await BuildSpineAsync();
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo");

        await spine.Navigator.OpenProjectAsync(project.Id);

        Assert.Equal(ShellArea.ProjectWorkspace, spine.Navigator.Current.Area);
        Assert.Equal(project.Id, spine.Navigator.Current.ProjectId);
        Assert.Equal(ProjectArea.Overview, spine.Navigator.Current.ProjectArea);

        // The invariant: a project-scoped location and the current project
        // can never disagree.
        Assert.Equal(project.Id, spine.Context.Current!.Id);
    }

    [Fact]
    public async Task EngineeringCannotBeEnteredWithoutAProject()
    {
        var spine = await BuildSpineAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => spine.Navigator.GoToEngineeringAsync());

        Assert.Equal(ShellArea.Home, spine.Navigator.Current.Area);
    }

    [Fact]
    public async Task EngineeringIsEnteredFromTheProject_AndCarriesItsScope()
    {
        var spine = await BuildSpineAsync();
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo");
        await spine.Navigator.OpenProjectAsync(project.Id);

        await spine.Navigator.GoToEngineeringAsync();

        Assert.Equal(ShellArea.Engineering, spine.Navigator.Current.Area);
        Assert.Equal(project.Id, spine.Navigator.Current.ProjectId);
        Assert.True(spine.Navigator.Current.IsProjectScoped);
    }

    [Fact]
    public async Task ReturningFromEngineering_KeepsTheProjectContext()
    {
        var spine = await BuildSpineAsync();
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo");
        await spine.Navigator.OpenProjectAsync(project.Id);
        await spine.Navigator.GoToEngineeringAsync();

        await spine.Navigator.ReturnToProjectAsync();

        Assert.Equal(ShellArea.ProjectWorkspace, spine.Navigator.Current.Area);
        Assert.Equal(project.Id, spine.Navigator.Current.ProjectId);
        Assert.Equal(project.Id, spine.Context.Current!.Id);
    }

    [Fact]
    public async Task GoingHomeFromAProject_KeepsTheProjectOpen_SoTheUserCanReturn()
    {
        var spine = await BuildSpineAsync();
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo");
        await spine.Navigator.OpenProjectAsync(project.Id);

        await spine.Navigator.GoHomeAsync();

        Assert.Equal(ShellArea.Home, spine.Navigator.Current.Area);
        Assert.True(spine.Context.HasProject);
    }

    [Fact]
    public async Task ClosingTheProject_ClearsTheContext_AndReturnsToTheBrowser()
    {
        var spine = await BuildSpineAsync();
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo");
        await spine.Navigator.OpenProjectAsync(project.Id);

        await spine.Navigator.CloseProjectAsync();

        Assert.Equal(ShellArea.Projects, spine.Navigator.Current.Area);
        Assert.False(spine.Context.HasProject);
    }

    [Fact]
    public async Task EveryNavigation_PublishesTheMoveOnTheEventBus()
    {
        var spine = await BuildSpineAsync();
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo");

        var moves = new List<ShellLocationChangedEvent>();
        spine.EventBus.Subscribe(new CapturingHandler<ShellLocationChangedEvent>(moves.Add));

        await spine.Navigator.GoToProjectsAsync();
        await spine.Navigator.OpenProjectAsync(project.Id);
        await spine.Navigator.GoToEngineeringAsync();
        await spine.Navigator.ReturnToProjectAsync();

        Assert.Equal(4, moves.Count);
        Assert.Equal(
            [ShellArea.Projects, ShellArea.ProjectWorkspace, ShellArea.Engineering, ShellArea.ProjectWorkspace],
            moves.Select(m => m.Current.Area));
    }

    [Fact]
    public async Task NavigatingNowhere_PublishesNothing()
    {
        var spine = await BuildSpineAsync();
        await spine.Navigator.GoToProjectsAsync();

        var moves = new List<ShellLocationChangedEvent>();
        spine.EventBus.Subscribe(new CapturingHandler<ShellLocationChangedEvent>(moves.Add));

        await spine.Navigator.GoToProjectsAsync();

        Assert.Empty(moves);
    }

    // ----------------------------------------------------------------
    // Restart recovery — the Definition of Done's own closing step
    // ----------------------------------------------------------------

    [Fact]
    public async Task AfterRestart_TheProjectAndLocationAreBothRecovered()
    {
        // One settings store and one persistence store stand for one
        // machine's durable state across two application lifetimes. The
        // domain object graph is deliberately NOT shared — the second
        // lifetime starts with an empty repository and rebuilds it from
        // the durable store, exactly as a relaunch does (`TD-85`).
        var settings = new SettingsProvider(new Materials.InMemoryPersistenceStore(), new EventBus());
        var persistence = new Materials.InMemoryPersistenceStore();

        var first = await BuildSpineAsync(settings, persistence);
        var project = await first.Directory.CreateAsync("P-0027", "Apollo Pump Redesign");
        await first.Navigator.OpenProjectAsync(project.Id, ProjectArea.Requirements);
        await first.Context.SaveAsync();
        await first.Navigator.SaveAsync();

        // A second lifetime: fresh domain, same durable state.
        var second = await BuildSpineAsync(settings, persistence);
        await second.Navigator.LoadAsync();

        Assert.Equal(ShellArea.ProjectWorkspace, second.Navigator.Current.Area);
        Assert.Equal(project.Id, second.Navigator.Current.ProjectId);
        Assert.Equal(ProjectArea.Requirements, second.Navigator.Current.ProjectArea);

        Assert.True(second.Context.HasProject);
        Assert.Equal("P-0027 Apollo Pump Redesign", second.Context.Current!.Label);
    }

    [Fact]
    public async Task AfterRestart_EveryProjectIsStillListed()
    {
        var persistence = new Materials.InMemoryPersistenceStore();
        var first = await BuildSpineAsync(persistence: persistence);
        await first.Directory.CreateAsync("P-0011", "Hydraulic Manifold");
        await first.Directory.CreateAsync("P-0027", "Apollo Pump Redesign");

        var second = await BuildSpineAsync(persistence: persistence);
        var projects = await second.Directory.ListAsync();

        Assert.Equal(["P-0011", "P-0027"], projects.Select(p => p.Identifier));
    }

    [Fact]
    public async Task AfterRestart_AgainstThePersistedProject_TheContextAndLocationBothReturn()
    {
        var settings = new SettingsProvider(new Materials.InMemoryPersistenceStore(), new EventBus());
        var spine = await BuildSpineAsync(settings);
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo Pump Redesign");

        await spine.Navigator.OpenProjectAsync(project.Id, ProjectArea.Requirements);
        await spine.Context.SaveAsync();
        await spine.Navigator.SaveAsync();

        // A new shell over the same domain and the same settings — the
        // real restart shape once the domain is itself durable.
        var reopened = new ShellNavigator(
            new ProjectContext(spine.Directory, spine.EventBus, settings), spine.EventBus, settings);
        await reopened.LoadAsync();

        Assert.Equal(ShellArea.ProjectWorkspace, reopened.Current.Area);
        Assert.Equal(project.Id, reopened.Current.ProjectId);
        Assert.Equal(ProjectArea.Requirements, reopened.Current.ProjectArea);
    }

    [Fact]
    public async Task AfterRestart_WhenTheSavedProjectHasBeenDeleted_ItDegradesToHome_NeverThrows()
    {
        var settings = new SettingsProvider(new Materials.InMemoryPersistenceStore(), new EventBus());
        var spine = await BuildSpineAsync(settings);
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo");
        await spine.Navigator.OpenProjectAsync(project.Id);
        await spine.Navigator.SaveAsync();

        // A different domain: the saved project no longer resolves.
        var afterDeletion = await BuildSpineAsync(settings);

        var exception = await Record.ExceptionAsync(() => afterDeletion.Navigator.LoadAsync());

        Assert.Null(exception);
        Assert.Equal(ShellArea.Home, afterDeletion.Navigator.Current.Area);
        Assert.False(afterDeletion.Context.HasProject);
    }

    [Fact]
    public async Task AfterRestart_ACorruptedSavedLocation_DegradesToHome_NeverThrows()
    {
        var settings = new SettingsProvider(new Materials.InMemoryPersistenceStore(), new EventBus());
        var spine = await BuildSpineAsync(settings);
        await settings.SetValueAsync(ShellNavigator.SettingKey, "{{{not json");
        await settings.SetValueAsync(ProjectContext.SettingKey, "{{{not json");

        Assert.Null(await Record.ExceptionAsync(() => spine.Navigator.LoadAsync()));
        Assert.Null(await Record.ExceptionAsync(() => spine.Context.LoadAsync()));

        Assert.Equal(ShellArea.Home, spine.Navigator.Current.Area);
        Assert.False(spine.Context.HasProject);
    }

    [Fact]
    public async Task ANonProjectLocation_IsRecoveredWithoutNeedingAProject()
    {
        var settings = new SettingsProvider(new Materials.InMemoryPersistenceStore(), new EventBus());
        var spine = await BuildSpineAsync(settings);
        await spine.Navigator.GoToProjectsAsync();
        await spine.Navigator.SaveAsync();

        var reopened = await BuildSpineAsync(settings);
        await reopened.Navigator.LoadAsync();

        Assert.Equal(ShellArea.Projects, reopened.Navigator.Current.Area);
    }

    [Fact]
    public async Task RefreshAsync_PicksUpARenamedProject_WithoutReopening()
    {
        var spine = await BuildSpineAsync();
        var project = await spine.Directory.CreateAsync("P-0027", "Apollo");
        await spine.Context.OpenAsync(project.Id);

        var domainObject = (IRenamable)(await spine.Domain.Repository.FindAsync(project.Id))!;
        await domainObject.RenameAsync("Apollo Pump Redesign");

        await spine.Context.RefreshAsync();

        Assert.Equal("Apollo Pump Redesign", spine.Context.Current!.DisplayName);
    }

    private sealed class CapturingHandler<TEvent>(Action<TEvent> capture) : IEventHandler<TEvent> where TEvent : IEvent
    {
        public Task HandleAsync(TEvent @event, CancellationToken cancellationToken)
        {
            capture(@event);
            return Task.CompletedTask;
        }
    }
}
