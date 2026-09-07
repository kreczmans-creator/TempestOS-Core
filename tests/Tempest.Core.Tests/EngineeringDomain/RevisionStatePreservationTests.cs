using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `WP 16.4B-R6` — a revision carries the <b>complete</b> durable state of
/// the object it revises, <c>TypeState</c> included.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these pin.</b> <c>CaptureState</c> captures a type's own
/// fields through <c>CaptureTypeState</c>; <c>RestoreState</c> restores the
/// base class's mutable fields and, by design, never reads
/// <c>TypeState</c> — a type's fields are read back only by that type's own
/// <see cref="IRehydratable{TSelf}.Rehydrate"/>. Rehydration pairs the two
/// halves correctly. <c>ReviseAsync</c> did not: it built the successor
/// from a plain construction closure and then called <c>RestoreState</c>,
/// so every type-specific field silently reverted to its construction-time
/// value — and was then written to disk by the successor's next mutation.
/// </para>
/// <para>
/// The user-visible form is ordinary: <c>ProjectTaskService.EditAsync</c>
/// revises a task whenever its <em>description</em> is edited, so editing
/// the description of an in-progress, high-priority task assigned to Alice
/// and due in December returned an unassigned, Todo, Normal task with no
/// due date. The same shape reached Issues, Risks and Decisions through
/// <c>ProjectGovernanceService</c> and <c>ProjectMilestoneService</c>.
/// </para>
/// <para>
/// The fix is not a copy of an object graph: the successor is now built by
/// the Kind's own <see cref="IRehydratable{TSelf}.Rehydrate"/> — the one
/// reader that exists for <c>TypeState</c> — given the state captured at
/// the instant of the revision. "Revise" and "restart" therefore
/// reconstruct an object through exactly the same code, which is why
/// <see cref="ReviseAsync_PreservesTypeState_ForEveryConcreteKind"/> can
/// assert it for all of them at once rather than type by type.
/// </para>
/// </remarks>
public sealed class RevisionStatePreservationTests
{
    private static readonly DateTimeOffset DueDate = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The board's own reproduction, made an assertion, on the type whose
    /// mutable type state a user actually edits.
    /// </summary>
    [Fact]
    public async Task ReviseAsync_KeepsATasksAssigneeWorkStatePriorityAndDueDate()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);

        var task = await CreateAsync<EngineeringTask>(
            context, "Task", (d, r) => new EngineeringTask(d, r, context, "TASK-1", "Fit the bracket", EngineeringObjectMetadata.Empty));

        await task.AssignAsync("alice");
        await task.ChangeWorkStateAsync(TaskWorkState.InProgress);
        await task.SetPriorityAsync(WorkPriority.High);
        await task.SetDueDateAsync(DueDate);

        var revised = (EngineeringTask)await task.ReviseAsync("A rewritten description.", "Description edited.");

        Assert.Equal("alice", revised.AssignedToPrincipalId);
        Assert.Equal(TaskWorkState.InProgress, revised.WorkState);
        Assert.Equal(WorkPriority.High, revised.Priority);
        Assert.Equal(DueDate, revised.DueDate);
    }

    /// <summary>
    /// And durably: the loss only became permanent at the successor's next
    /// ordinary write, so the assertion has to look at the record on disk
    /// after one, not only at the instance in memory.
    /// </summary>
    [Fact]
    public async Task AfterARevision_TheSuccessorsNextWriteDoesNotPersistARevertedTypeState()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);

        var task = await CreateAsync<EngineeringTask>(
            context, "Task", (d, r) => new EngineeringTask(d, r, context, "TASK-1", "Fit the bracket", EngineeringObjectMetadata.Empty));

        await task.AssignAsync("alice");
        await task.ChangeWorkStateAsync(TaskWorkState.InProgress);
        await task.SetPriorityAsync(WorkPriority.High);
        await task.SetDueDateAsync(DueDate);

        var revised = (EngineeringTask)await task.ReviseAsync("A rewritten description.", "Description edited.");

        // An ordinary, unrelated mutation on the live successor.
        await revised.RenameAsync("Fit the bracket (revised)");

        var state = await stateStore.FindAsync(task.Id);
        Assert.NotNull(state);
        Assert.Equal("alice", state.TypeState[nameof(EngineeringTask.AssignedToPrincipalId)]);
        Assert.Equal(nameof(TaskWorkState.InProgress), state.TypeState[nameof(EngineeringTask.WorkState)]);
        Assert.Equal(nameof(WorkPriority.High), state.TypeState[nameof(EngineeringTask.Priority)]);
        Assert.Equal(DueDate.ToString("O"), state.TypeState[nameof(EngineeringTask.DueDate)]);
    }

    /// <summary>
    /// The governance families lose the same way — status, priority and
    /// ownership are exactly the fields a risk register exists to hold.
    /// </summary>
    [Fact]
    public async Task ReviseAsync_KeepsTheGovernanceFamiliesOwnMutableState()
    {
        var stateStore = new InMemoryObjectStateStore();
        var context = BuildContext(stateStore);

        var issue = await CreateAsync<Issue>(
            context, "Issue", (d, r) => new Issue(d, r, context, "ISS-1", "Leak", EngineeringObjectMetadata.Empty));
        await issue.ChangeStatusAsync(IssueStatus.Investigating);
        await issue.SetPriorityAsync(WorkPriority.Critical);
        await issue.AssignAsync("bob");

        var risk = await CreateAsync<Risk>(
            context, "Risk", (d, r) => new Risk(d, r, context, "RSK-1", "Corrosion", EngineeringObjectMetadata.Empty));
        await risk.ChangeStatusAsync(RiskStatus.Mitigating);
        await risk.ScoreAsync("Likely", "Severe");
        await risk.AssignOwnerAsync("carol");

        var decision = await CreateAsync<Decision>(
            context, "Decision", (d, r) => new Decision(d, r, context, "DEC-1", "Use titanium", EngineeringObjectMetadata.Empty, "Because it is lighter."));
        await decision.DecideAsync(DecisionStatus.Accepted, "dave", DueDate);

        var revisedIssue = (Issue)await issue.ReviseAsync("Rewritten.", null);
        var revisedRisk = (Risk)await risk.ReviseAsync("Rewritten.", null);
        var revisedDecision = (Decision)await decision.ReviseAsync("Rewritten.", null);

        Assert.Equal(IssueStatus.Investigating, revisedIssue.IssueStatus);
        Assert.Equal(WorkPriority.Critical, revisedIssue.Priority);
        Assert.Equal("bob", revisedIssue.AssignedToPrincipalId);

        Assert.Equal(RiskStatus.Mitigating, revisedRisk.RiskStatus);
        Assert.Equal("Likely", revisedRisk.Likelihood);
        Assert.Equal("Severe", revisedRisk.Severity);
        Assert.Equal("carol", revisedRisk.OwnedByPrincipalId);

        Assert.Equal(DecisionStatus.Accepted, revisedDecision.DecisionStatus);
        Assert.Equal("dave", revisedDecision.DecidedByPrincipalId);
    }

    /// <summary>
    /// The invariant itself, over <b>every</b> concrete Engineering Object
    /// type in the platform: revision preserves the complete captured
    /// state, and <c>TypeState</c> is part of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stated once, for all of them, rather than per type — which is only
    /// possible because the fix is a single reader, not 26 hand-written
    /// restore methods. Every entry in <see cref="EveryConcreteKind"/> is
    /// constructed with a distinctive, non-default value for each of its
    /// own type-specific fields, so a type whose
    /// <c>CaptureTypeState</c>/<c>Rehydrate</c> pair disagrees shows up
    /// here as a changed key rather than as two matching defaults.
    /// </para>
    /// <para>
    /// It deliberately includes the derived chains whose
    /// <c>CaptureTypeState</c> calls <c>base.CaptureTypeState</c> —
    /// <see cref="Drawing"/>/<see cref="CadModel"/>/<see cref="WorkInstruction"/>
    /// over <see cref="Document"/>, <see cref="SubAssembly"/> over
    /// <see cref="Assembly"/>, <see cref="Baseline"/>/<see cref="Release"/>
    /// over <see cref="Configuration"/>, <see cref="Hazard"/> over
    /// <see cref="Risk"/>, <see cref="EngineeringAction"/> over
    /// <see cref="EngineeringTask"/>, and <see cref="Test"/>/
    /// <see cref="Inspection"/> over <see cref="VerificationActivity"/> —
    /// because a partially-applied inheritance chain is exactly the shape
    /// that would pass a single-type test.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ReviseAsync_PreservesTypeState_ForEveryConcreteKind()
    {
        var failures = new List<string>();

        foreach (var (kind, create) in EveryConcreteKind())
        {
            var stateStore = new InMemoryObjectStateStore();
            var context = BuildContext(stateStore);

            var original = await create(context);
            await MutateAnyMutableTypeStateAsync(original);

            var before = original.CaptureState();
            var revised = (EngineeringObjectBase)await original.ReviseAsync("Revised content.", "Rev B.");
            var after = revised.CaptureState();

            foreach (var entry in before.TypeState)
            {
                if (!after.TypeState.TryGetValue(entry.Key, out var carried))
                    failures.Add($"{kind}: '{entry.Key}' was dropped entirely by the revision.");
                else if (carried != entry.Value)
                    failures.Add($"{kind}: '{entry.Key}' was '{entry.Value}' and came back '{carried ?? "<null>"}'.");
            }

            foreach (var key in after.TypeState.Keys.Where(k => !before.TypeState.ContainsKey(k)))
                failures.Add($"{kind}: '{key}' appeared from nowhere in the successor.");

            // The base half has to survive too — the same assertion
            // `CanonicalKindRoundTripTests` makes across a restart.
            if (before.DisplayName != after.DisplayName || before.Identifier != after.Identifier || before.Status != after.Status)
                failures.Add($"{kind}: base state changed across the revision.");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Every concrete Engineering Object type, each with distinctive
    /// type-specific values. Deliberately spelled out rather than
    /// discovered by reflection: a type added to the platform and forgotten
    /// here should be caught by the review that adds it, and a reflective
    /// list would silently cover a type with default values it cannot
    /// invent constructor arguments for.
    /// </summary>
    private static IReadOnlyList<(string Kind, Func<EngineeringDomainContext, Task<EngineeringObjectBase>> Create)> EveryConcreteKind()
    {
        var metadata = EngineeringObjectMetadata.Empty;
        var subject = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var child = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var members = new List<ConfigurationMember> { new(child, 3) };

        return
        [
            ("Document", c => Make<Document>(c, "Document", (d, r) => new Document(d, r, c, "DOC-1", "A document", metadata))),
            ("Drawing", c => Make<Drawing>(c, "Drawing", (d, r) => new Drawing(d, r, c, "DRW-1", "A drawing", metadata, "DRW-NUM-9"))),
            ("CadModel", c => Make<CadModel>(c, "CadModel", (d, r) => new CadModel(d, r, c, "CAD-1", "A model", metadata, "STEP"))),
            ("Simulation", c => Make<Simulation>(c, "Simulation", (d, r) => new Simulation(d, r, c, "A simulation", metadata, subject, "Thermal", ["AL-7075"]))),
            ("ExternalSystemLink", c => Make<ExternalSystemLink>(c, "ExternalSystemLink", (d, r) => new ExternalSystemLink(d, r, c, "A link", metadata, "PLM", "EXT-1"))),

            ("Assembly", c => Make<Assembly>(c, "Assembly", (d, r) => new Assembly(d, r, c, "ASM-1", "An assembly", metadata, [child]))),
            ("SubAssembly", c => Make<SubAssembly>(c, "SubAssembly", (d, r) => new SubAssembly(d, r, c, "SUB-1", "A sub-assembly", metadata, subject, [child]))),
            ("Part", c => Make<Part>(c, "Part", (d, r) => new Part(d, r, c, "PRT-1", "A part", metadata, "AL-7075"))),
            ("Component", c => Make<Component>(c, "Component", (d, r) => new Component(d, r, c, "CMP-1", "A component", metadata))),
            ("Configuration", c => Make<Core.EngineeringDomain.Configuration>(c, "Configuration", (d, r) => new Core.EngineeringDomain.Configuration(d, r, c, "CFG-1", "A configuration", metadata, members))),

            ("ChangeRequest", c => Make<ChangeRequest>(c, "ChangeRequest", (d, r) => new ChangeRequest(d, r, c, "CR-1", "A change request", metadata))),
            ("EngineeringChange", c => Make<EngineeringChange>(c, "EngineeringChange", (d, r) => new EngineeringChange(d, r, c, "EC-1", "A change", metadata, subject))),
            ("Baseline", c => Make<Baseline>(c, "Baseline", (d, r) => new Baseline(d, r, c, "BL-1", "A baseline", metadata, members))),
            ("Release", c => Make<Release>(c, "Release", (d, r) => new Release(d, r, c, "REL-1", "A release", metadata, members))),

            ("Calculation", c => Make<Calculation>(c, "Calculation", (d, r) => new Calculation(d, r, c, "CALC-1", "A calculation", metadata))),
            ("CalculationSet", c => Make<CalculationSet>(c, "CalculationSet", (d, r) => new CalculationSet(d, r, c, "CS-1", "A calculation set", metadata, [child]))),

            ("Task", c => Make<EngineeringTask>(c, "Task", (d, r) => new EngineeringTask(d, r, c, "TASK-1", "A task", metadata, "engineer"))),
            ("Action", c => Make<EngineeringAction>(c, "Action", (d, r) => new EngineeringAction(d, r, c, "ACT-1", "An action", metadata, subject, "engineer"))),
            ("Review", c => Make<Review>(c, "Review", (d, r) => new Review(d, r, c, "A review", metadata, ["reviewer"]))),
            ("Approval", c => Make<Approval>(c, "Approval", (d, r) => new Approval(d, r, c, "An approval", metadata, "approver", DueDate))),
            ("Milestone", c => Make<Milestone>(c, "Milestone", (d, r) => new Milestone(d, r, c, "MS-1", "A milestone", metadata, DueDate))),
            ("Deliverable", c => Make<Deliverable>(c, "Deliverable", (d, r) => new Deliverable(d, r, c, "DEL-1", "A deliverable", metadata, subject))),

            ("Issue", c => Make<Issue>(c, "Issue", (d, r) => new Issue(d, r, c, "ISS-1", "An issue", metadata))),
            ("Risk", c => Make<Risk>(c, "Risk", (d, r) => new Risk(d, r, c, "RSK-1", "A risk", metadata, "Low", "Medium"))),
            ("Hazard", c => Make<Hazard>(c, "Hazard", (d, r) => new Hazard(d, r, c, "HAZ-1", "A hazard", metadata, "Low", "High"))),
            ("Decision", c => Make<Decision>(c, "Decision", (d, r) => new Decision(d, r, c, "DEC-1", "A decision", metadata, "Because."))),
            ("Assumption", c => Make<Assumption>(c, "Assumption", (d, r) => new Assumption(d, r, c, "ASU-1", "An assumption", metadata))),

            ("VerificationActivity", c => Make<VerificationActivity>(c, "VerificationActivity", (d, r) => new VerificationActivity(d, r, c, "An activity", metadata, subject, "Analysis"))),
            ("Test", c => Make<Test>(c, "Test", (d, r) => new Test(d, r, c, "A test", metadata, subject, "Test"))),
            ("Inspection", c => Make<Inspection>(c, "Inspection", (d, r) => new Inspection(d, r, c, "An inspection", metadata, subject, "Inspect"))),
            ("Verification", c => Make<Core.EngineeringDomain.Verification>(c, "Verification", (d, r) => new Core.EngineeringDomain.Verification(d, r, c, metadata))),

            ("ManufacturingOperation", c => Make<ManufacturingOperation>(c, "ManufacturingOperation", (d, r) => new ManufacturingOperation(d, r, c, "OP-1", "An operation", metadata, subject))),
            ("WorkInstruction", c => Make<WorkInstruction>(c, "WorkInstruction", (d, r) => new WorkInstruction(d, r, c, "WI-1", "An instruction", metadata, subject))),

            ("Supplier", c => Make<Supplier>(c, "Supplier", (d, r) => new Supplier(d, r, c, "SUP-1", "A supplier", metadata))),
            ("PurchaseItem", c => Make<PurchaseItem>(c, "PurchaseItem", (d, r) => new PurchaseItem(d, r, c, "PI-1", "An item", metadata, subject, child))),

            ("Portfolio", c => Make<Portfolio>(c, "Portfolio", (d, r) => new Portfolio(d, r, c, "PF-1", "A portfolio", metadata, [child]))),
            ("Programme", c => Make<Programme>(c, "Programme", (d, r) => new Programme(d, r, c, "PG-1", "A programme", metadata, subject, [child]))),
            ("Project", c => Make<Project>(c, "Project", (d, r) => new Project(d, r, c, "PJ-1", "A project", metadata, subject))),
        ];
    }

    /// <summary>
    /// Moves every type that <em>has</em> mutable type state away from its
    /// construction-time values, so the assertion above cannot pass by two
    /// defaults matching. The types with no mutable type state fall
    /// through — their fields are immutable, and the point is that the same
    /// one reader carries both kinds.
    /// </summary>
    private static async Task MutateAnyMutableTypeStateAsync(EngineeringObjectBase instance)
    {
        switch (instance)
        {
            case EngineeringTask task: // and EngineeringAction
                await task.AssignAsync("alice");
                await task.ChangeWorkStateAsync(TaskWorkState.InProgress);
                await task.SetPriorityAsync(WorkPriority.High);
                await task.SetDueDateAsync(DueDate);
                break;

            case Issue issue:
                await issue.ChangeStatusAsync(IssueStatus.Investigating);
                await issue.SetPriorityAsync(WorkPriority.Critical);
                await issue.AssignAsync("bob");
                break;

            case Risk risk: // and Hazard
                await risk.ChangeStatusAsync(RiskStatus.Mitigating);
                await risk.ScoreAsync("Likely", "Severe");
                await risk.AssignOwnerAsync("carol");
                break;

            case Decision decision:
                await decision.SetRationaleAsync("A rewritten rationale.");
                await decision.DecideAsync(DecisionStatus.Accepted, "dave", DueDate);
                break;

            default:
                break;
        }
    }

    private static async Task<EngineeringObjectBase> Make<T>(
        EngineeringDomainContext context, string kind, Func<IEngineeringDocument, IDocumentRevision, T> ctor)
        where T : EngineeringObjectBase, IRehydratable<T> =>
        await CreateAsync(context, kind, ctor).ConfigureAwait(false);

    private static async Task<T> CreateAsync<T>(
        EngineeringDomainContext context, string kind, Func<IEngineeringDocument, IDocumentRevision, T> ctor)
        where T : EngineeringObjectBase, IRehydratable<T> =>
        (T)await new EngineeringObjectFactory<T>(kind, context, ctor).CreateAsync($"{kind} — for test purposes.").ConfigureAwait(false);

    private static EngineeringDomainContext BuildContext(IEngineeringObjectStateStore stateStore)
    {
        var principalAccessor = new CurrentPrincipalAccessor();
        var store = new InMemoryEngineeringDocumentStore(principalAccessor);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);

        return new EngineeringDomainContext(
            store, repository, relationshipRepository, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(relationshipDiscovery, repository), principalAccessor, stateStore);
    }

    private sealed class InMemoryObjectStateStore : IEngineeringObjectStateStore
    {
        private readonly Dictionary<Guid, EngineeringObjectState> _states = new();

        public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
        {
            lock (_states) { _states[state.Id] = state; }
            return Task.CompletedTask;
        }

        public Task<EngineeringObjectState?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_states) { return Task.FromResult(_states.TryGetValue(id, out var state) ? state : null); }
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_states) { _states.Remove(id); }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default)
        {
            lock (_states) { return Task.FromResult<IReadOnlyList<EngineeringObjectState>>(_states.Values.ToList()); }
        }
    }
}
