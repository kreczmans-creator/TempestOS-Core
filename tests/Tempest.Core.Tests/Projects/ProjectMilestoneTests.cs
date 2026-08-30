using Tempest.App.Projects;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Projects;

/// <summary>
/// The project milestone register: transitive project membership, the two
/// routes work takes to a milestone, and the facts the surface is allowed to
/// state about a date.
/// </summary>
/// <remarks>
/// <para>
/// <c>Milestone</c> and <c>Deliverable</c> were real, durable, rehydratable
/// domain types that nothing in the product created and no surface showed —
/// the Timeline area was `Declared`, tracked by `TD-81`.
/// </para>
/// <para>
/// These tests are about the decisions that shape the feature: that a
/// milestone belongs to a project through <see cref="ProjectMembership"/>
/// rather than a field; that a deliverable reaches the project through its
/// milestone; that work reaches a milestone either directly or through a
/// deliverable and the register keeps the difference; and — most
/// importantly — that the register states only what the model knows, never
/// inventing a "milestone achieved" fact the domain does not hold.
/// </para>
/// </remarks>
public sealed class ProjectMilestoneTests
{
    private static readonly DateTimeOffset Today = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    // ================================================================
    // Membership, transitively and by parenting
    // ================================================================

    [Fact]
    public async Task AMilestoneJoinsAProjectThroughTheParentChain_NotAField()
    {
        var fixture = await MilestoneFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var milestone = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "Critical Design Review", Today.AddDays(30));

        var entry = Assert.Single(await fixture.Register.ListAsync(project.Id));
        Assert.Equal(milestone.Id, entry.ObjectId);

        // The rule this feature had to respect: membership is answered once,
        // by the parent chain.
        Assert.Null(milestone.GetType().GetProperty("ProjectId"));
        Assert.DoesNotContain("ProjectId", milestone.CaptureState().TypeState.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ADeliverableReachesTheProjectThroughItsMilestone()
    {
        var fixture = await MilestoneFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var milestone = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "CDR", Today.AddDays(30));

        var deliverable = await fixture.Workflow.CreateDeliverableAsync(project.Id, milestone.Id, "DEL-001", "Stress report");

        // Parented to the milestone, not to the project — so the structure
        // the Timeline shows is the structure the domain actually holds.
        Assert.Equal(milestone.Id, ((IHasParent)deliverable).ParentId);
        Assert.Equal(project.Id, await ProjectMembership.ResolveOwningProjectAsync(fixture.Domain.Repository, deliverable.Id));

        var entry = Assert.Single(await fixture.Register.ListAsync(project.Id));
        Assert.Equal(deliverable.Id, Assert.Single(entry.Deliverables).ObjectId);
    }

    [Fact]
    public async Task AMilestoneRaisedDeepInTheStructure_IsStillAProjectMilestone()
    {
        var fixture = await MilestoneFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var assembly = await fixture.CreatePartAsync("ASM-1", "Turbopump", project.Id);
        var part = await fixture.CreatePartAsync("PRT-1", "Impeller", assembly.Id);

        var milestone = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "Impeller sign-off", Today.AddDays(10));
        await ((IHasParent)milestone).MoveAsync(part.Id);

        Assert.Equal(milestone.Id, Assert.Single(await fixture.Register.ListAsync(project.Id)).ObjectId);
    }

    [Fact]
    public async Task OneProjectsMilestones_NeverLeakIntoAnother()
    {
        var fixture = await MilestoneFixture.CreateAsync();
        var apollo = await fixture.CreateProjectAsync("P-1", "Apollo");
        var gemini = await fixture.CreateProjectAsync("P-2", "Gemini");

        await fixture.Workflow.CreateMilestoneAsync(apollo.Id, "MS-001", "Apollo CDR", Today.AddDays(30));

        Assert.Single(await fixture.Register.ListAsync(apollo.Id));
        Assert.Empty(await fixture.Register.ListAsync(gemini.Id));
    }

    // ================================================================
    // Both routes from work to a milestone
    // ================================================================

    [Fact]
    public async Task WorkReachesAMilestoneDirectly_AndThroughADeliverable_AndTheRegisterKeepsTheDifference()
    {
        var fixture = await MilestoneFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var milestone = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "CDR", Today.AddDays(30));
        var deliverable = await fixture.Workflow.CreateDeliverableAsync(project.Id, milestone.Id, "DEL-001", "Stress report");

        var direct = await fixture.Tasks.CreateAsync(project.Id, "TSK-001", "Book the review room");
        await fixture.Tasks.ContributeToAsync(direct.Id, milestone.Id);

        var indirect = await fixture.Tasks.CreateAsync(project.Id, "TSK-002", "Run the stress case");
        await fixture.Tasks.ContributeToAsync(indirect.Id, deliverable.Id);

        var entry = Assert.Single(await fixture.Register.ListAsync(project.Id));

        Assert.Equal(2, entry.Contributions.Count);

        // A deliverable already knows its own MilestoneId, so the second hop
        // is a read rather than a second link — but which route the work
        // took is kept, so the surface can show what a deliverable carries.
        var directEntry = entry.Contributions.Single(c => c.ObjectId == direct.Id);
        var indirectEntry = entry.Contributions.Single(c => c.ObjectId == indirect.Id);

        Assert.False(directEntry.IsIndirect);
        Assert.Null(directEntry.ViaDeliverableId);
        Assert.True(indirectEntry.IsIndirect);
        Assert.Equal(deliverable.Id, indirectEntry.ViaDeliverableId);
    }

    [Fact]
    public async Task AnActionContributesLikeATask_BecauseAnActionIsATask()
    {
        var fixture = await MilestoneFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var milestone = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "CDR", Today.AddDays(30));

        var action = await fixture.CreateActionAsync("ACT-001", "Close the review finding", project.Id);
        await action.ContributeToAsync(milestone.Id);

        // Hiding actions would understate the work a date is carrying.
        var entry = Assert.Single(await fixture.Register.ListAsync(project.Id));
        Assert.Equal(action.Id, Assert.Single(entry.Contributions).ObjectId);
    }

    [Fact]
    public async Task WorkContributingToAnotherProjectsMilestone_IsNotCounted()
    {
        var fixture = await MilestoneFixture.CreateAsync();
        var apollo = await fixture.CreateProjectAsync("P-1", "Apollo");
        var gemini = await fixture.CreateProjectAsync("P-2", "Gemini");

        var apolloMilestone = await fixture.Workflow.CreateMilestoneAsync(apollo.Id, "MS-001", "Apollo CDR", Today.AddDays(30));
        var geminiTask = await fixture.Tasks.CreateAsync(gemini.Id, "TSK-001", "Gemini work");
        await fixture.Tasks.ContributeToAsync(geminiTask.Id, apolloMilestone.Id);

        // The task is not a member of Apollo, so it is not read at all — the
        // register only ever walks the project's own membership.
        Assert.Empty(Assert.Single(await fixture.Register.ListAsync(apollo.Id)).Contributions);
    }

    // ================================================================
    // What the register may and may not claim about a date
    // ================================================================

    [Fact]
    public async Task APastDateWithOpenWork_IsReportedAsSuch_AndIsNotCalledMissed()
    {
        var fixture = await MilestoneFixture.CreateAsync(() => Today);
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var milestone = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "Slipped review", Today.AddDays(-5));

        var task = await fixture.Tasks.CreateAsync(project.Id, "TSK-001", "Finish the analysis");
        await fixture.Tasks.ContributeToAsync(task.Id, milestone.Id);

        var entry = Assert.Single(await fixture.Register.ListAsync(project.Id));

        Assert.True(entry.IsPast);
        Assert.True(entry.HasOutstandingWork);
        Assert.True(entry.IsPastWithOutstandingWork);
        Assert.False(entry.IsPastWithNothingLinked);
        Assert.Equal(1, entry.OpenContributionCount);
    }

    [Fact]
    public async Task APastDateWhoseWorkIsAllDone_IsNoLongerOutstanding()
    {
        var fixture = await MilestoneFixture.CreateAsync(() => Today);
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var milestone = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "Done review", Today.AddDays(-5));

        var task = await fixture.Tasks.CreateAsync(project.Id, "TSK-001", "Finish the analysis");
        await fixture.Tasks.ContributeToAsync(task.Id, milestone.Id);
        await fixture.Tasks.ChangeWorkStateAsync(task.Id, TaskWorkState.Done);

        var entry = Assert.Single(await fixture.Register.ListAsync(project.Id));

        // The date has still gone by — that is a fact and stays true. What
        // changed is that nothing is outstanding against it. The register
        // never claims the milestone was "achieved", because the domain
        // holds no such state to read.
        Assert.True(entry.IsPast);
        Assert.False(entry.HasOutstandingWork);
        Assert.False(entry.IsPastWithOutstandingWork);
        Assert.Equal(0, entry.OpenContributionCount);
    }

    [Fact]
    public async Task APastDateWithNothingLinked_IsItsOwnDistinctProblem()
    {
        var fixture = await MilestoneFixture.CreateAsync(() => Today);
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "A date nobody used", Today.AddDays(-5));

        var entry = Assert.Single(await fixture.Register.ListAsync(project.Id));

        // A date with nothing behind it is a different failure from a date
        // with work still running, and collapsing them would hide the one a
        // review most needs to see.
        Assert.True(entry.IsPastWithNothingLinked);
        Assert.False(entry.IsPastWithOutstandingWork);
        Assert.False(entry.HasLinkedWork);
    }

    [Fact]
    public async Task AFutureMilestone_IsNotPast()
    {
        var fixture = await MilestoneFixture.CreateAsync(() => Today);
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "Upcoming", Today.AddDays(30));

        var entry = Assert.Single(await fixture.Register.ListAsync(project.Id));

        Assert.False(entry.IsPast);
        Assert.False(entry.IsPastWithOutstandingWork);
        Assert.False(entry.IsPastWithNothingLinked);
    }

    [Fact]
    public async Task AMilestoneWithADeliverableButNoTasks_CountsAsHavingLinkedWork()
    {
        var fixture = await MilestoneFixture.CreateAsync(() => Today);
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var milestone = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "CDR", Today.AddDays(-1));
        await fixture.Workflow.CreateDeliverableAsync(project.Id, milestone.Id, "DEL-001", "Stress report");

        var entry = Assert.Single(await fixture.Register.ListAsync(project.Id));

        // Something is attached, so this is not the "date nobody used" case
        // — even though no task has been linked yet.
        Assert.True(entry.HasLinkedWork);
        Assert.False(entry.IsPastWithNothingLinked);
    }

    // ================================================================
    // Ordering: a timeline reads chronologically
    // ================================================================

    [Fact]
    public async Task MilestonesAreListedInDateOrder_EvenWhenOneIsOverdue()
    {
        var fixture = await MilestoneFixture.CreateAsync(() => Today);
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var later = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-003", "Later", Today.AddDays(60));
        var overdue = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "Overdue", Today.AddDays(-5));
        var soon = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-002", "Soon", Today.AddDays(10));

        var milestones = await fixture.Register.ListAsync(project.Id);

        // Chronological, deliberately: an overdue milestone keeps its place
        // in the sequence rather than being promoted, because the order a
        // schedule is read in is the order the dates fall.
        Assert.Equal([overdue.Id, soon.Id, later.Id], milestones.Select(m => m.ObjectId));
    }

    // ================================================================
    // The service creates real domain objects, and refuses bad targets
    // ================================================================

    [Fact]
    public async Task ADeliverableCannotBeAddedToSomethingThatIsNotAMilestone()
    {
        var fixture = await MilestoneFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var task = await fixture.Tasks.CreateAsync(project.Id, "TSK-001", "Not a milestone");

        await Assert.ThrowsAsync<MilestoneNotFoundException>(
            () => fixture.Workflow.CreateDeliverableAsync(project.Id, task.Id, "DEL-001", "Nope"));
    }

    [Fact]
    public async Task AMilestoneCannotBeSetInAProjectThatDoesNotExist()
    {
        var fixture = await MilestoneFixture.CreateAsync();

        await Assert.ThrowsAsync<ProjectNotFoundException>(
            () => fixture.Workflow.CreateMilestoneAsync(Guid.NewGuid(), "MS-001", "Nowhere", Today));
    }

    [Fact]
    public async Task TheTargetDateReachesTheStore_NotOnlyTheInstance()
    {
        var fixture = await MilestoneFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var milestone = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "CDR", Today.AddDays(30));

        Assert.Equal(Today.AddDays(30), (await fixture.LoadStoredStateAsync(milestone.Id)).TypeDate("TargetDate"));
    }

    [Fact]
    public async Task EditingAMilestoneRetitlesItAndAddsARevision()
    {
        var fixture = await MilestoneFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var milestone = await fixture.Workflow.CreateMilestoneAsync(project.Id, "MS-001", "Critcal Design Review", Today.AddDays(30));

        var revisionBefore = milestone.CurrentRevisionNumber;

        await fixture.Workflow.EditMilestoneAsync(milestone.Id, "Critical Design Review", "Full design package reviewed.");

        var entry = Assert.Single(await fixture.Register.ListAsync(project.Id));
        Assert.Equal("Critical Design Review", entry.DisplayName);
        Assert.Contains("design package", entry.Description, StringComparison.Ordinal);

        var reloaded = (EngineeringObjectBase)(await fixture.Domain.Repository.FindAsync(milestone.Id))!;
        Assert.True(reloaded.CurrentRevisionNumber > revisionBefore);
    }

    // ================================================================
    // Fixture
    // ================================================================

    private sealed class MilestoneFixture
    {
        private MilestoneFixture(
            EngineeringDomainContext domain, EngineeringObjectStateStore states, Func<DateTimeOffset> now)
        {
            Domain = domain;
            States = states;
            Register = new ProjectMilestoneRegister(domain, now);
            Workflow = new ProjectMilestoneService(domain);
            Tasks = new ProjectTaskService(domain);
        }

        private EngineeringObjectStateStore States { get; }

        public EngineeringDomainContext Domain { get; }

        public IProjectMilestoneRegister Register { get; }

        public IProjectMilestoneService Workflow { get; }

        public IProjectTaskService Tasks { get; }

        public async Task<EngineeringObjectState> LoadStoredStateAsync(Guid objectId) =>
            await States.FindAsync(objectId) ?? throw new InvalidOperationException($"Nothing is persisted for '{objectId}'.");

        public static Task<MilestoneFixture> CreateAsync(Func<DateTimeOffset>? now = null)
        {
            var root = Path.Combine(Path.GetTempPath(), "tempest-project-milestones-" + Guid.NewGuid().ToString("N"));
            var configuration = new ConfigurationBuilder()
                .AddSource(new MemoryConfigurationSource(
                [
                    new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, root),
                ]))
                .Build();

            var store = new PersistenceStore(configuration);
            var principal = new CurrentPrincipalAccessor();
            var documents = new EngineeringDocumentStore(store, principal);
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);
            var states = new EngineeringObjectStateStore(store);

            var domain = new EngineeringDomainContext(
                documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal,
                states, new AttachmentContentStore(store));

            return Task.FromResult(new MilestoneFixture(domain, states, now ?? (() => DateTimeOffset.UtcNow)));
        }

        public async Task<IProject> CreateProjectAsync(string identifier, string name)
        {
            var factory = new EngineeringObjectFactory<Project>(
                ProjectDirectory.ProjectKind, Domain,
                (d, r) => new Project(d, r, Domain, identifier, name, EngineeringObjectMetadata.Empty));

            return (Project)await factory.CreateAsync($"Project {identifier}.");
        }

        public async Task<Part> CreatePartAsync(string identifier, string name, Guid parentId)
        {
            var factory = new EngineeringObjectFactory<Part>(
                MechanicalObjectFactoryRegistry.Part, Domain,
                (d, r) => new Part(d, r, Domain, identifier, name, EngineeringObjectMetadata.Empty));

            var part = (Part)await factory.CreateAsync($"Part {identifier}.");
            await ((IHasParent)part).MoveAsync(parentId);
            return part;
        }

        public async Task<EngineeringAction> CreateActionAsync(string identifier, string name, Guid parentId)
        {
            var factory = new EngineeringObjectFactory<EngineeringAction>(
                CanonicalObjectKinds.Action, Domain,
                (d, r) => new EngineeringAction(d, r, Domain, identifier, name, EngineeringObjectMetadata.Empty, Guid.NewGuid()));

            var action = (EngineeringAction)await factory.CreateAsync($"Action {identifier}.");
            await ((IHasParent)action).MoveAsync(parentId);
            return action;
        }
    }
}
