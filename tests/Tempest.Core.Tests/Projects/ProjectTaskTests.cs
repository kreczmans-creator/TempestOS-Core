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
/// The task family: its own work state, its project membership, and the
/// register and service the Tasks surface is built on.
/// </summary>
/// <remarks>
/// <para>
/// Tasks were the last project area still marked <c>Declared</c>:
/// <c>EngineeringTask</c>, <c>EngineeringAction</c>, <c>Milestone</c> and
/// <c>Deliverable</c> were real, durable, rehydratable domain types that
/// nothing created, assigned, dated or reported on (`TD-81`).
/// </para>
/// <para>
/// These tests are about the two decisions that shape the feature: that a
/// task's status is its own family state rather than the canonical
/// document lifecycle (so it can be reopened), and that a task belongs to a
/// project through <see cref="ProjectMembership"/> rather than through a
/// field of its own.
/// </para>
/// </remarks>
public sealed class ProjectTaskTests : IDisposable
{
    private static readonly DateTimeOffset Today = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly List<string> _fixtureRoots = [];

    /// <summary>
    /// Creates a <see cref="TaskFixture"/> and remembers its isolated
    /// persistence root for <see cref="Dispose"/> — closes the Core-side
    /// leak <c>TD-120</c> (Technical Debt Register.md) left open, see
    /// <see cref="ProjectFixtureRoot"/>.
    /// </summary>
    private async Task<TaskFixture> CreateFixtureAsync()
    {
        var fixture = await TaskFixture.CreateAsync();
        _fixtureRoots.Add(fixture.Root);
        return fixture;
    }

    /// <summary>Deletes every persistence root this instance's own test created — xUnit constructs a fresh instance per test, so this runs once per test, not once per class.</summary>
    public void Dispose()
    {
        foreach (var root in _fixtureRoots)
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    // ================================================================
    // The work state is the task family's own, and it can be reopened
    // ================================================================

    [Fact]
    public void EveryWorkState_MapsToACanonicalLifecycleState()
    {
        // IFamilySpecificState's whole point: a family may have its own
        // vocabulary, but something reasoning across the domain must still
        // get one answer for every Kind.
        foreach (var state in Enum.GetValues<TaskWorkState>())
        {
            var descriptor = TaskWorkStates.For(state);

            Assert.Equal(state, descriptor.State);
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Name));
            Assert.IsAssignableFrom<IFamilySpecificState>(descriptor);
        }

        Assert.Equal(LifecycleState.Released, TaskWorkStates.For(TaskWorkState.Done).CanonicalEquivalent);
        Assert.Equal(LifecycleState.Cancelled, TaskWorkStates.For(TaskWorkState.Cancelled).CanonicalEquivalent);
        Assert.Equal(LifecycleState.Draft, TaskWorkStates.For(TaskWorkState.Todo).CanonicalEquivalent);
    }

    [Fact]
    public void FinishedWork_CanBeReopened_WhichTheCanonicalLifecycleForbids()
    {
        // The reason the task family needs a table of its own. The
        // canonical table is asserted here too, deliberately: this test
        // fails if someone "fixes" the engineering lifecycle to allow a
        // released drawing to become a draft again.
        Assert.True(TaskWorkStateTransitions.IsPermitted(TaskWorkState.Done, TaskWorkState.Todo));
        Assert.True(TaskWorkStateTransitions.IsPermitted(TaskWorkState.Done, TaskWorkState.InProgress));
        Assert.True(TaskWorkStateTransitions.IsPermitted(TaskWorkState.Cancelled, TaskWorkState.Todo));

        Assert.False(new LifecycleTransitionTable().IsPermitted(LifecycleState.Released, LifecycleState.Draft));
    }

    [Fact]
    public void AWorkState_NeverTransitionsToItself()
    {
        foreach (var state in Enum.GetValues<TaskWorkState>())
            Assert.False(TaskWorkStateTransitions.IsPermitted(state, state));
    }

    [Fact]
    public void OnlyUnfinishedWork_CountsAsOpen()
    {
        Assert.True(TaskWorkStates.IsOpen(TaskWorkState.Todo));
        Assert.True(TaskWorkStates.IsOpen(TaskWorkState.InProgress));
        Assert.True(TaskWorkStates.IsOpen(TaskWorkState.Blocked));

        // Cancelled counts as finished. An abandoned task is not
        // outstanding work, and counting it would make every open-task
        // figure in the product slowly become a lie.
        Assert.False(TaskWorkStates.IsOpen(TaskWorkState.Done));
        Assert.False(TaskWorkStates.IsOpen(TaskWorkState.Cancelled));
    }

    // ================================================================
    // The workflow
    // ================================================================

    [Fact]
    public async Task ATaskMovesThroughItsLifecycle_AndCanBeCompletedThenReopened()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Balance the impeller");
        Assert.Equal(TaskWorkState.Todo, task.WorkState);

        await fixture.Workflow.ChangeWorkStateAsync(task.Id, TaskWorkState.InProgress);
        Assert.Equal(TaskWorkState.InProgress, task.WorkState);

        await fixture.Workflow.ChangeWorkStateAsync(task.Id, TaskWorkState.Done);
        Assert.Equal(TaskWorkState.Done, task.WorkState);
        Assert.False(TaskWorkStates.IsOpen(task.WorkState));

        // Reopen.
        await fixture.Workflow.ChangeWorkStateAsync(task.Id, TaskWorkState.InProgress);
        Assert.Equal(TaskWorkState.InProgress, task.WorkState);
        Assert.True(TaskWorkStates.IsOpen(task.WorkState));
    }

    [Fact]
    public async Task AnImpossibleMove_IsRefusedByName_RatherThanSilentlyIgnored()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Balance the impeller");

        await fixture.Workflow.ChangeWorkStateAsync(task.Id, TaskWorkState.Done);

        var error = await Assert.ThrowsAsync<InvalidTaskWorkStateTransitionException>(
            () => fixture.Workflow.ChangeWorkStateAsync(task.Id, TaskWorkState.Blocked));

        Assert.Equal(TaskWorkState.Done, error.From);
        Assert.Equal(TaskWorkState.Blocked, error.To);
        Assert.Equal(TaskWorkState.Done, task.WorkState);
    }

    [Fact]
    public async Task AssigningToTheCurrentPrincipal_UsesTheBoundary_NotAHardCodedName()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Balance the impeller");

        Assert.Null(task.AssignedToPrincipalId);

        fixture.SignInAs("ada");
        await fixture.Workflow.AssignToCurrentPrincipalAsync(task.Id);
        Assert.Equal("ada", task.AssignedToPrincipalId);

        // A different principal at the boundary gives a different
        // assignee, with nothing in the domain changed.
        var second = await fixture.Workflow.CreateAsync(project.Id, "TSK-002", "Check the seal");
        fixture.SignInAs("grace");
        await fixture.Workflow.AssignToCurrentPrincipalAsync(second.Id);

        Assert.Equal("grace", second.AssignedToPrincipalId);
        Assert.Equal("ada", task.AssignedToPrincipalId);
    }

    [Fact]
    public async Task ATaskCanBeUnassigned_BecauseNobodyOwningItIsARealState()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Balance the impeller", assignedToPrincipalId: "ada");

        Assert.Equal("ada", task.AssignedToPrincipalId);

        await fixture.Workflow.AssignAsync(task.Id, null);
        Assert.Null(task.AssignedToPrincipalId);

        // Whitespace is not an assignee either.
        await fixture.Workflow.AssignAsync(task.Id, "   ");
        Assert.Null(task.AssignedToPrincipalId);
    }

    [Fact]
    public async Task ATaskIsOverdueOnlyWhileItIsStillOpen()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Late work", dueDate: Today.AddDays(-3));

        Assert.True(task.IsOverdue(Today));

        // Finishing late work does not leave it overdue for ever. The
        // question the user is asking is "what still needs chasing".
        await fixture.Workflow.ChangeWorkStateAsync(task.Id, TaskWorkState.Done);
        Assert.False(task.IsOverdue(Today));

        // And a task with no due date is never overdue.
        var undated = await fixture.Workflow.CreateAsync(project.Id, "TSK-002", "Someday");
        Assert.False(undated.IsOverdue(Today.AddYears(5)));
    }

    [Fact]
    public async Task EditingATask_RenamesIt_AndKeepsWhatItUsedToSay()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Balence the impeler", "Original wording.");

        await fixture.Workflow.EditAsync(task.Id, "Balance the impeller", "Corrected wording.");

        // Re-read rather than trusting the local reference: a revision is
        // a new instance of the same object (`TD-85`), and the repository
        // is what the register and the surface both read. A test that
        // asserted on the stale handle would pass while the product showed
        // the old wording, or fail while it showed the new one.
        var edited = (EngineeringTask)(await fixture.Domain.Repository.FindAsync(task.Id))!;

        Assert.Equal("Balance the impeller", edited.DisplayName);
        Assert.Equal("Corrected wording.", edited.Content);

        // The old wording is history, not a deletion.
        var revisions = await edited.GetRevisionHistoryAsync();
        Assert.Contains(revisions, r => r.Content == "Original wording.");

        // And the edit did not cost the task its place in the project.
        Assert.Contains(await fixture.Register.ListAsync(project.Id), e => e.ObjectId == task.Id);
    }

    // ================================================================
    // Project membership
    // ================================================================

    [Fact]
    public async Task ATaskCreatedInAProject_BelongsToThatProject_ByTheParentChain()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Balance the impeller");

        // Membership is the platform's one answer, not a field on the task.
        Assert.Equal(project.Id, await ProjectMembership.ResolveOwningProjectAsync(fixture.Domain.Repository, task.Id));
        Assert.Null(typeof(EngineeringTask).GetProperty("ProjectId"));
    }

    [Fact]
    public async Task ATaskNestedDeepInsideAProject_IsStillAProjectTask()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var assembly = await fixture.CreatePartAsync("ASM-1", "Pump", project.Id);
        var part = await fixture.CreatePartAsync("PRT-1", "Impeller", assembly.Id);

        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Balance the impeller");
        await ((IHasParent)task).MoveAsync(part.Id);

        // Three levels down, and still the project's task — the same
        // transitive rule documents already follow.
        var entries = await fixture.Register.ListAsync(project.Id);
        Assert.Contains(entries, e => e.ObjectId == task.Id);
    }

    [Fact]
    public async Task OneProjectsTasks_AreNeverAnotherProjectsTasks()
    {
        var fixture = await CreateFixtureAsync();
        var apollo = await fixture.CreateProjectAsync("P-1", "Apollo");
        var gemini = await fixture.CreateProjectAsync("P-2", "Gemini");

        var apolloTask = await fixture.Workflow.CreateAsync(apollo.Id, "TSK-001", "Apollo work");
        var geminiTask = await fixture.Workflow.CreateAsync(gemini.Id, "TSK-001", "Gemini work");

        var apolloEntries = await fixture.Register.ListAsync(apollo.Id);
        var geminiEntries = await fixture.Register.ListAsync(gemini.Id);

        Assert.Equal([apolloTask.Id], apolloEntries.Select(e => e.ObjectId));
        Assert.Equal([geminiTask.Id], geminiEntries.Select(e => e.ObjectId));
    }

    [Fact]
    public async Task AnEmptyProject_ReportsNoTasks_RatherThanEveryTask()
    {
        var fixture = await CreateFixtureAsync();
        var apollo = await fixture.CreateProjectAsync("P-1", "Apollo");
        var empty = await fixture.CreateProjectAsync("P-2", "Nothing yet");

        await fixture.Workflow.CreateAsync(apollo.Id, "TSK-001", "Apollo work");

        Assert.Empty(await fixture.Register.ListAsync(empty.Id));

        // The board still has every column, so an empty project shows the
        // shape of a board rather than nothing at all.
        var board = await fixture.Register.ListBoardAsync(empty.Id);
        Assert.Equal(TaskWorkStates.All.Count, board.Count);
        Assert.All(board, column => Assert.Empty(column.Entries));
    }

    [Fact]
    public async Task ATaskOutsideEveryProject_BelongsToNoProjectsRegister()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        // A standalone task — created through the factory directly, never
        // parented. Standalone work is a supported state (`TD-89`), so it
        // must not leak into a project's register.
        var standalone = await fixture.CreateStandaloneTaskAsync("TSK-999", "Standalone work");

        var entries = await fixture.Register.ListAsync(project.Id);
        Assert.DoesNotContain(entries, e => e.ObjectId == standalone.Id);
    }

    // ================================================================
    // The register
    // ================================================================

    [Fact]
    public async Task ActionsAreListedAlongsideTasks_BecauseAnActionIsATask()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Ordinary work");
        var action = await fixture.CreateActionAsync("ACT-001", "Raised in a review", project.Id);

        var entries = await fixture.Register.ListAsync(project.Id);

        Assert.Contains(entries, e => e.ObjectId == task.Id && e.Kind == CanonicalObjectKinds.Task);
        Assert.Contains(entries, e => e.ObjectId == action.Id && e.Kind == CanonicalObjectKinds.Action);
    }

    [Fact]
    public async Task TheRegisterOrdersWhatNeedsAttentionFirst()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var undated = await fixture.Workflow.CreateAsync(project.Id, "TSK-003", "Someday");
        var soon = await fixture.Workflow.CreateAsync(project.Id, "TSK-002", "Next week", dueDate: Today.AddDays(7));
        var overdue = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Late", dueDate: Today.AddDays(-2));

        var entries = await fixture.Register.ListAsync(project.Id);

        // Overdue first, then by due date, and an undated task sorts last
        // rather than jumping to the top on a null comparison.
        Assert.Equal([overdue.Id, soon.Id, undated.Id], entries.Select(e => e.ObjectId));
    }

    [Fact]
    public async Task TheBoardPutsEachTaskInItsOwnColumn_AndKeepsTheEmptyOnes()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var todo = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Not started");
        var doing = await fixture.Workflow.CreateAsync(project.Id, "TSK-002", "Underway");
        await fixture.Workflow.ChangeWorkStateAsync(doing.Id, TaskWorkState.InProgress);

        var board = await fixture.Register.ListBoardAsync(project.Id);

        Assert.Equal(TaskWorkStates.All.Count, board.Count);
        Assert.Equal([todo.Id], Column(board, TaskWorkState.Todo).Entries.Select(e => e.ObjectId));
        Assert.Equal([doing.Id], Column(board, TaskWorkState.InProgress).Entries.Select(e => e.ObjectId));
        Assert.Empty(Column(board, TaskWorkState.Blocked).Entries);
        Assert.Empty(Column(board, TaskWorkState.Done).Entries);
    }

    [Fact]
    public async Task ATaskLinkedToAMilestone_ReportsItAndItsTargetDate()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var milestone = await fixture.CreateMilestoneAsync("MS-1", "Design freeze", project.Id, Today.AddMonths(2));
        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Balance the impeller");

        await fixture.Workflow.ContributeToAsync(task.Id, milestone.Id);

        var entry = Assert.Single(await fixture.Register.ListAsync(project.Id), e => e.ObjectId == task.Id);

        Assert.NotNull(entry.ContributesTo);
        Assert.Equal(milestone.Id, entry.ContributesTo!.ObjectId);
        Assert.Equal("Design freeze", entry.ContributesTo.DisplayName);
        Assert.Equal(Today.AddMonths(2), entry.ContributesTo.TargetDate);
    }

    [Fact]
    public async Task ATaskLinkedToADeliverable_ReportsTheMilestoneDateItIsWorkingTo()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var milestone = await fixture.CreateMilestoneAsync("MS-1", "Design freeze", project.Id, Today.AddMonths(2));
        var deliverable = await fixture.CreateDeliverableAsync("DEL-1", "Impeller drawing pack", project.Id, milestone.Id);
        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Draft the pack");

        await fixture.Workflow.ContributeToAsync(task.Id, deliverable.Id);

        var entry = Assert.Single(await fixture.Register.ListAsync(project.Id), e => e.ObjectId == task.Id);

        // A deliverable has no date of its own, so the date shown is the
        // one the task is actually working to.
        Assert.Equal(deliverable.Id, entry.ContributesTo!.ObjectId);
        Assert.Equal(Today.AddMonths(2), entry.ContributesTo.TargetDate);
    }

    [Fact]
    public async Task ATaskCannotContributeToSomethingThatIsNotAMilestoneOrDeliverable()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var part = await fixture.CreatePartAsync("PRT-1", "Impeller", project.Id);
        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Balance the impeller");

        await Assert.ThrowsAsync<TaskTargetNotFoundException>(
            () => fixture.Workflow.ContributeToAsync(task.Id, part.Id));
    }

    [Fact]
    public async Task OperatingOnSomethingThatIsNotATask_FailsByName()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var part = await fixture.CreatePartAsync("PRT-1", "Impeller", project.Id);

        await Assert.ThrowsAsync<TaskNotFoundException>(() => fixture.Workflow.AssignAsync(part.Id, "ada"));
        await Assert.ThrowsAsync<TaskNotFoundException>(() => fixture.Workflow.ChangeWorkStateAsync(part.Id, TaskWorkState.Done));
        await Assert.ThrowsAsync<ProjectNotFoundException>(() => fixture.Workflow.CreateAsync(Guid.NewGuid(), "TSK-001", "Nowhere"));
    }

    // ================================================================
    // Persistence
    // ================================================================

    [Fact]
    public async Task EverythingATaskCarries_IsWrittenIntoItsPersistedState()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var task = await fixture.Workflow.CreateAsync(
            project.Id, "TSK-001", "Balance the impeller",
            priority: WorkPriority.Critical, dueDate: Today.AddDays(5), assignedToPrincipalId: "ada");

        await fixture.Workflow.ChangeWorkStateAsync(task.Id, TaskWorkState.Blocked);

        var state = task.CaptureState();

        Assert.Equal("ada", state.Type("AssignedToPrincipalId"));
        Assert.Equal(nameof(TaskWorkState.Blocked), state.Type("WorkState"));
        Assert.Equal(nameof(WorkPriority.Critical), state.Type("Priority"));
        Assert.Equal(Today.AddDays(5), state.TypeDate("DueDate"));

        // The parent edge is what makes it a project task, and it is part
        // of the same persisted record.
        Assert.Equal(project.Id, state.ParentId);
    }

    [Fact]
    public async Task EveryTaskEdit_ReachesTheStore_NotJustTheObjectInMemory()
    {
        // The mutation that found this: making ChangeWorkStateAsync skip
        // its own persist left every in-memory assertion passing. A test
        // that reads the object it just changed cannot tell the difference
        // between "saved" and "set on this instance" — so this one reads
        // what the store actually holds.
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var task = await fixture.Workflow.CreateAsync(project.Id, "TSK-001", "Balance the impeller");

        await fixture.Workflow.ChangeWorkStateAsync(task.Id, TaskWorkState.InProgress);
        Assert.Equal(nameof(TaskWorkState.InProgress), (await fixture.LoadStoredStateAsync(task.Id)).Type("WorkState"));

        await fixture.Workflow.AssignAsync(task.Id, "ada");
        Assert.Equal("ada", (await fixture.LoadStoredStateAsync(task.Id)).Type("AssignedToPrincipalId"));

        await fixture.Workflow.SetPriorityAsync(task.Id, WorkPriority.High);
        Assert.Equal(nameof(WorkPriority.High), (await fixture.LoadStoredStateAsync(task.Id)).Type("Priority"));

        await fixture.Workflow.SetDueDateAsync(task.Id, Today.AddDays(9));
        Assert.Equal(Today.AddDays(9), (await fixture.LoadStoredStateAsync(task.Id)).TypeDate("DueDate"));

        // Clearing a due date must reach the store too — otherwise the
        // date comes back on the next launch after the user removed it.
        await fixture.Workflow.SetDueDateAsync(task.Id, null);
        Assert.Null((await fixture.LoadStoredStateAsync(task.Id)).TypeDate("DueDate"));

        // And completing it.
        await fixture.Workflow.ChangeWorkStateAsync(task.Id, TaskWorkState.Done);
        Assert.Equal(nameof(TaskWorkState.Done), (await fixture.LoadStoredStateAsync(task.Id)).Type("WorkState"));
    }

    [Fact]
    public void ATaskRecordWrittenBeforeTheseFieldsExisted_ComesBackAsAnOrdinaryTodo()
    {
        // `TD-85`'s established rule: a missing field comes back visibly
        // empty rather than failing the whole rehydration. A task saved by
        // an older build has no WorkState key at all, and "a task nobody
        // ever gave a state" is honestly Todo at Normal priority.
        var legacy = new EngineeringObjectState(
            1, Guid.NewGuid(), CanonicalObjectKinds.Task, "TSK-OLD", "Old task",
            EngineeringObjectMetadata.Empty, LifecycleState.Draft, null, false,
            EngineeringObjectBomLineState.Default, [], [],
            new Dictionary<string, string?> { ["AssignedToPrincipalId"] = "ada" });

        Assert.Equal("ada", legacy.Type("AssignedToPrincipalId"));
        Assert.Null(legacy.Type("WorkState"));
        Assert.False(Enum.TryParse<TaskWorkState>(legacy.Type("WorkState"), out _));
    }

    // ================================================================
    // Fixture
    // ================================================================

    private static ProjectTaskBoardColumn Column(IReadOnlyList<ProjectTaskBoardColumn> board, TaskWorkState state) =>
        board.Single(c => c.State == state);

    private sealed class TaskFixture
    {
        private TaskFixture(EngineeringDomainContext domain, CurrentPrincipalAccessor principal, EngineeringObjectStateStore states, string root)
        {
            Domain = domain;
            Principal = principal;
            States = states;
            Register = new ProjectTaskRegister(domain, () => Today);
            Workflow = new ProjectTaskService(domain);
            Root = root;
        }

        private EngineeringObjectStateStore States { get; }

        /// <summary>This fixture's own isolated persistence root — the caller's own <c>Dispose</c> deletes it (`TD-120` Core-side closure).</summary>
        public string Root { get; }

        /// <summary>Reads back what the store actually holds for <paramref name="objectId"/>.</summary>
        public async Task<EngineeringObjectState> LoadStoredStateAsync(Guid objectId) =>
            await States.FindAsync(objectId) ?? throw new InvalidOperationException($"Nothing is persisted for '{objectId}'.");

        public EngineeringDomainContext Domain { get; }

        public CurrentPrincipalAccessor Principal { get; }

        public IProjectTaskRegister Register { get; }

        public IProjectTaskService Workflow { get; }

        public static Task<TaskFixture> CreateAsync()
        {
            var root = ProjectFixtureRoot.NewIsolatedRoot("tasks");
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

            return Task.FromResult(new TaskFixture(domain, principal, states, root));
        }

        public void SignInAs(string identityId) =>
            Principal.SetCurrent(new PlatformPrincipal(new PlatformIdentity(identityId, identityId), []));

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

        public async Task<EngineeringTask> CreateStandaloneTaskAsync(string identifier, string name)
        {
            var factory = new EngineeringObjectFactory<EngineeringTask>(
                CanonicalObjectKinds.Task, Domain,
                (d, r) => new EngineeringTask(d, r, Domain, identifier, name, EngineeringObjectMetadata.Empty));

            return (EngineeringTask)await factory.CreateAsync($"Task {identifier}.");
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

        public async Task<Milestone> CreateMilestoneAsync(string identifier, string name, Guid parentId, DateTimeOffset targetDate)
        {
            var factory = new EngineeringObjectFactory<Milestone>(
                CanonicalObjectKinds.Milestone, Domain,
                (d, r) => new Milestone(d, r, Domain, identifier, name, EngineeringObjectMetadata.Empty, targetDate));

            var milestone = (Milestone)await factory.CreateAsync($"Milestone {identifier}.");
            await ((IHasParent)milestone).MoveAsync(parentId);
            return milestone;
        }

        public async Task<Deliverable> CreateDeliverableAsync(string identifier, string name, Guid parentId, Guid milestoneId)
        {
            var factory = new EngineeringObjectFactory<Deliverable>(
                CanonicalObjectKinds.Deliverable, Domain,
                (d, r) => new Deliverable(d, r, Domain, identifier, name, EngineeringObjectMetadata.Empty, milestoneId));

            var deliverable = (Deliverable)await factory.CreateAsync($"Deliverable {identifier}.");
            await ((IHasParent)deliverable).MoveAsync(parentId);
            return deliverable;
        }
    }
}
