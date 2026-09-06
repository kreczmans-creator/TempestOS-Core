using System.Collections;
using System.Reflection;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `WP 16.4B-R6` made <c>_selfFactory</c> state-aware, so
/// <see cref="EngineeringObjectBase.ReviseAsync"/> builds its successor
/// through the Kind's own <see cref="IRehydratable{TSelf}.Rehydrate"/>
/// rather than through the construction closure. These are the questions
/// that change raises but does not answer by itself.
/// </summary>
/// <remarks>
/// <para>
/// The fix's own test asserts that the successor's captured
/// <c>TypeState</c> dictionary matches the predecessor's. That is
/// necessary and it is not sufficient, for one specific reason: a
/// type-specific value that <c>CaptureTypeState</c> never writes is
/// absent from <em>both</em> dictionaries, so the comparison passes while
/// the value itself is silently lost. Before R6 such a value survived a
/// revision anyway, because the construction closure still held it; after
/// R6 the closure is gone and the state record is the only channel. That
/// is a regression the fix's own shape of test cannot see, and
/// <see cref="ReviseAsync_PreservesEveryPublicProperty_ForEveryConcreteKind"/>
/// exists to see it — it compares the objects, not their serialisations.
/// </para>
/// <para>
/// The rest cover the round trip the brief asks for end to end —
/// <b>original → revise → successor → persisted → reloaded</b> over a real
/// <see cref="PersistenceStore"/> on disk — the rehydrate-then-revise
/// order that the R6 report calls "a second, quieter instance of the same
/// bug", and whether one reader now runs exactly once per revision rather
/// than twice.
/// </para>
/// </remarks>
public sealed class RevisionRehydrationEquivalenceTests : IDisposable
{
    private static readonly DateTimeOffset DueDate = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "tempest-r6c-rehydrate-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // ================================================================
    // The objects themselves, not their serialisations
    // ================================================================

    /// <summary>
    /// For every concrete Kind: revise it, and compare <b>every public
    /// property the Kind itself declares</b> between predecessor and
    /// successor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately reflective, where
    /// <see cref="RevisionStatePreservationTests"/> is deliberately
    /// explicit — the two answer different questions. That one asks "did
    /// the state record survive the trip"; this one asks "did the object".
    /// A property that <c>CaptureTypeState</c> forgets to write is
    /// invisible to a state-record comparison and glaring here, and after
    /// R6 that gap is a real data-loss path rather than a cosmetic one,
    /// because the construction closure that used to paper over it has
    /// been removed.
    /// </para>
    /// <para>
    /// Properties declared on <see cref="EngineeringObjectBase"/> itself
    /// are excluded: they are the base half of the record, restored by
    /// <c>RestoreState</c>, and some of them (the revision number, the
    /// content) are <em>supposed</em> to differ across a revision. What is
    /// left is exactly the type-specific half, which must not.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ReviseAsync_PreservesEveryPublicProperty_ForEveryConcreteKind()
    {
        var failures = new List<string>();

        foreach (var (kind, create) in EveryConcreteKind())
        {
            var context = BuildInMemoryContext();
            var original = await create(context);
            await MutateAnyMutableTypeStateAsync(original);

            var successor = (EngineeringObjectBase)await original.ReviseAsync("Revised content.", "Rev B.");

            if (successor.GetType() != original.GetType())
            {
                failures.Add($"{kind}: the successor came back as '{successor.GetType().Name}', not '{original.GetType().Name}'.");
                continue;
            }

            foreach (var property in TypeSpecificProperties(original.GetType()))
            {
                var before = Describe(property.GetValue(original));
                var after = Describe(property.GetValue(successor));

                if (!string.Equals(before, after, StringComparison.Ordinal))
                    failures.Add($"{kind}.{property.Name}: was '{before}' and came back '{after}'.");
            }
        }

        Assert.True(
            failures.Count == 0,
            "Revising an object changed type-specific state that a revision has no business changing:" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// The same comparison for a Kind whose <c>CaptureTypeState</c> calls
    /// <c>base.CaptureTypeState</c> — the chains the brief names
    /// explicitly, where a break would drop only the inherited half and
    /// leave the derived half looking correct.
    /// </summary>
    [Fact]
    public async Task ReviseAsync_PreservesBothHalvesOfABaseCallingCaptureTypeStateChain()
    {
        var context = BuildInMemoryContext();

        var drawing = (Drawing)await CreateAsync<Drawing>(
            context, "Drawing", (d, r) => new Drawing(d, r, context, "DRW-1", "A drawing", EngineeringObjectMetadata.Empty, "DRW-NUM-9"));
        var revisedDrawing = (Drawing)await drawing.ReviseAsync("Revised.", "Rev B.");
        Assert.Equal("DRW-NUM-9", revisedDrawing.DrawingNumber);

        var subAssembly = (SubAssembly)await CreateAsync<SubAssembly>(
            context, "SubAssembly", (d, r) => new SubAssembly(d, r, context, "SUB-1", "A sub-assembly", EngineeringObjectMetadata.Empty, Guid.Parse("11111111-1111-1111-1111-111111111111"), [Guid.Parse("22222222-2222-2222-2222-222222222222")]));
        var revisedSubAssembly = (SubAssembly)await subAssembly.ReviseAsync("Revised.", "Rev B.");
        Assert.Equal(subAssembly.ParentAssemblyId, revisedSubAssembly.ParentAssemblyId);
        Assert.Equal(subAssembly.ChildIds, revisedSubAssembly.ChildIds);

        var hazard = (Hazard)await CreateAsync<Hazard>(
            context, "Hazard", (d, r) => new Hazard(d, r, context, "HAZ-1", "A hazard", EngineeringObjectMetadata.Empty, "Low", "High"));
        await hazard.ScoreAsync("Likely", "Severe");
        await hazard.AssignOwnerAsync("carol");
        var revisedHazard = (Hazard)await hazard.ReviseAsync("Revised.", "Rev B.");
        Assert.Equal("Likely", revisedHazard.Likelihood);
        Assert.Equal("Severe", revisedHazard.Severity);
        Assert.Equal("carol", revisedHazard.OwnedByPrincipalId);

        var baseline = (Baseline)await CreateAsync<Baseline>(
            context, "Baseline", (d, r) => new Baseline(d, r, context, "BL-1", "A baseline", EngineeringObjectMetadata.Empty, [new ConfigurationMember(Guid.Parse("33333333-3333-3333-3333-333333333333"), 4)]));
        var revisedBaseline = (Baseline)await baseline.ReviseAsync("Revised.", "Rev B.");
        Assert.Equal(baseline.MemberRevisions.Count, revisedBaseline.MemberRevisions.Count);
        Assert.Equal(baseline.MemberRevisions[0].ObjectId, revisedBaseline.MemberRevisions[0].ObjectId);
        Assert.Equal(baseline.MemberRevisions[0].RevisionNumber, revisedBaseline.MemberRevisions[0].RevisionNumber);
    }

    // ================================================================
    // The full durable round trip
    // ================================================================

    /// <summary>
    /// <b>original → revise → successor → persisted → reloaded.</b> The
    /// whole chain the brief asks for, over a real
    /// <see cref="PersistenceStore"/> on disk and a second lifetime that
    /// shares nothing with the first but the files.
    /// </summary>
    /// <remarks>
    /// The intermediate assertions matter as much as the final one: an
    /// implementation that kept the successor correct in memory and wrote
    /// a reverted record, or wrote a correct record that the rehydrator
    /// then could not read back, would each pass a shorter version of this
    /// test.
    /// </remarks>
    [Fact]
    public async Task ARevisedObjectsMutatedTypeState_SurvivesTheSuccessorsWriteAndARestart()
    {
        Guid id;

        // ---- FIRST LIFETIME ------------------------------------------
        {
            var lifetime = BuildDiskLifetime();

            var task = (EngineeringTask)await CreateAsync<EngineeringTask>(
                lifetime.Context, CanonicalObjectKinds.Task,
                (d, r) => new EngineeringTask(d, r, lifetime.Context, "TASK-1", "Fit the bracket", EngineeringObjectMetadata.Empty, "engineer"));

            id = task.Id;

            await task.AssignAsync("alice");
            await task.ChangeWorkStateAsync(TaskWorkState.InProgress);
            await task.SetPriorityAsync(WorkPriority.High);
            await task.SetDueDateAsync(DueDate);
            await task.TransitionAsync(LifecycleState.InReview);

            var successor = (EngineeringTask)await task.ReviseAsync("A revised description.", "Rev B.");

            // In memory, immediately after the hand-off.
            Assert.Equal("alice", successor.AssignedToPrincipalId);
            Assert.Equal(TaskWorkState.InProgress, successor.WorkState);
            Assert.Equal(WorkPriority.High, successor.Priority);
            Assert.Equal(DueDate, successor.DueDate);
            Assert.Equal(LifecycleState.InReview, successor.Status);

            // And durably, once the successor writes its own snapshot —
            // which is the write that used to persist the reverted values.
            await successor.RenameAsync("Fit the bracket (rev B)");
        }

        // ---- SECOND LIFETIME -----------------------------------------
        {
            var lifetime = BuildDiskLifetime();
            var result = await new EngineeringObjectRehydrationService(lifetime.Context, lifetime.Rehydrators).RehydrateAsync();

            Assert.True(result.IsComplete, "Rehydration did not come back complete.");

            var reloaded = Assert.IsType<EngineeringTask>(await lifetime.Context.Repository.FindAsync(id));

            Assert.Equal("alice", reloaded.AssignedToPrincipalId);
            Assert.Equal(TaskWorkState.InProgress, reloaded.WorkState);
            Assert.Equal(WorkPriority.High, reloaded.Priority);
            Assert.Equal(DueDate, reloaded.DueDate);
            Assert.Equal(LifecycleState.InReview, reloaded.Status);
            Assert.Equal("Fit the bracket (rev B)", reloaded.DisplayName);
            Assert.Equal(2, reloaded.CurrentRevisionNumber);
        }
    }

    /// <summary>
    /// <b>Rehydrate, mutate, then revise.</b> The half of the same defect
    /// that lives in <see cref="EngineeringObjectRehydrator{T}"/> rather
    /// than in <see cref="EngineeringObjectFactory{T}"/>, and the one no
    /// board named.
    /// </summary>
    /// <remarks>
    /// Before `WP 16.4B-R6` the rehydrator attached a self-factory that
    /// closed over the state <em>as it stood at rehydration</em>. So an
    /// object recovered at start-up, edited during the session and then
    /// revised had its type-specific fields silently rolled back to their
    /// on-disk values from the moment the process started — a different
    /// and quieter failure than the factory's, with the same cause and the
    /// same consequence one write later. Restarting a second time is what
    /// makes it durable, so this test does.
    /// </remarks>
    [Fact]
    public async Task AnObjectRehydratedThenMutatedThenRevised_KeepsTheMutationMadeAfterRehydration()
    {
        Guid id;

        {
            var lifetime = BuildDiskLifetime();
            var task = (EngineeringTask)await CreateAsync<EngineeringTask>(
                lifetime.Context, CanonicalObjectKinds.Task,
                (d, r) => new EngineeringTask(d, r, lifetime.Context, "TASK-1", "Fit the bracket", EngineeringObjectMetadata.Empty, "engineer"));

            id = task.Id;
            await task.AssignAsync("alice");
        }

        {
            var lifetime = BuildDiskLifetime();
            await new EngineeringObjectRehydrationService(lifetime.Context, lifetime.Rehydrators).RehydrateAsync();

            var reloaded = Assert.IsType<EngineeringTask>(await lifetime.Context.Repository.FindAsync(id));
            Assert.Equal("alice", reloaded.AssignedToPrincipalId);

            // Edited in this session, after rehydration.
            await reloaded.AssignAsync("bob");
            await reloaded.SetPriorityAsync(WorkPriority.Critical);

            var successor = (EngineeringTask)await reloaded.ReviseAsync("A revised description.", "Rev B.");

            Assert.Equal("bob", successor.AssignedToPrincipalId);
            Assert.Equal(WorkPriority.Critical, successor.Priority);

            await successor.RenameAsync("Fit the bracket (rev B)");
        }

        {
            var lifetime = BuildDiskLifetime();
            await new EngineeringObjectRehydrationService(lifetime.Context, lifetime.Rehydrators).RehydrateAsync();

            var reloaded = Assert.IsType<EngineeringTask>(await lifetime.Context.Repository.FindAsync(id));
            Assert.Equal("bob", reloaded.AssignedToPrincipalId);
            Assert.Equal(WorkPriority.Critical, reloaded.Priority);
        }
    }

    // ================================================================
    // Applied once, by one reader
    // ================================================================

    /// <summary>
    /// The Kind's own state reader runs <b>exactly once</b> per revision,
    /// and the type state it reads is applied once — not twice, and not
    /// once by the reader and again by <c>RestoreState</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two failure shapes are covered by one fixture. The first is the
    /// count: a successor built by calling the reader twice (or by calling
    /// it and then a second restorer) is a real hazard now that the reader
    /// is on the revision path, and it is invisible to any value-equality
    /// assertion when the reader happens to be idempotent. The second is a
    /// reader that is <em>not</em> idempotent — here a list-valued property
    /// — where a double application shows up as duplicated entries rather
    /// than as a wrong count.
    /// </para>
    /// <para>
    /// The fixture also asserts the negative that makes the R6 design
    /// intelligible: <c>RestoreState</c> must <b>not</b> read
    /// <c>TypeState</c>. Its counter stays at zero for the type-state keys
    /// precisely because there is only one reader.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ARevisionRunsTheKindsStateReaderExactlyOnce_AndAppliesTypeStateOnce()
    {
        var context = BuildInMemoryContext();

        var original = (CountingFixture)await CreateAsync<CountingFixture>(
            context, CountingFixture.KindName,
            (d, r) => new CountingFixture(d, r, context, "CF-1", "Counting", EngineeringObjectMetadata.Empty, ["alpha", "beta"]));

        var readsBefore = CountingFixture.RehydrateCallCount;

        var successor = (CountingFixture)await original.ReviseAsync("Revised content.", "Rev B.");

        Assert.Equal(1, CountingFixture.RehydrateCallCount - readsBefore);
        Assert.Equal(["alpha", "beta"], successor.ReaderNotes);

        // A second revision, of the successor, runs it exactly once more —
        // so the count is per revision, not per object graph.
        var third = (CountingFixture)await successor.ReviseAsync("Revised again.", "Rev C.");
        Assert.Equal(2, CountingFixture.RehydrateCallCount - readsBefore);
        Assert.Equal(["alpha", "beta"], third.ReaderNotes);
    }

    /// <summary>
    /// A rehydrated object is not a second-class one: it can still revise
    /// itself, its successor is correctly typed, and that successor can
    /// revise again.
    /// </summary>
    /// <remarks>
    /// A regression pin, and labelled as one — it passed before
    /// `WP 16.4B-R6` too. It is here because R6 changed both suppliers of
    /// the self-factory at once, and "the rehydrated object can no longer
    /// revise at all" is the loudest way that change could have gone
    /// wrong.
    /// </remarks>
    [Fact]
    public async Task ARehydratedObject_CanStillReviseAndItsSuccessorIsCorrectlyTyped()
    {
        Guid id;

        {
            var lifetime = BuildDiskLifetime();
            var part = await CreateAsync<Part>(
                lifetime.Context, MechanicalObjectFactoryRegistry.Part,
                (d, r) => new Part(d, r, lifetime.Context, "PRT-1", "Bracket", EngineeringObjectMetadata.Empty, "AL-7075"));
            id = part.Id;
        }

        {
            var lifetime = BuildDiskLifetime();
            await new EngineeringObjectRehydrationService(lifetime.Context, lifetime.Rehydrators).RehydrateAsync();

            var reloaded = Assert.IsType<Part>(await lifetime.Context.Repository.FindAsync(id));
            Assert.Equal("AL-7075", reloaded.MaterialId);

            var successor = Assert.IsType<Part>(await reloaded.ReviseAsync("Revised.", "Rev B."));
            Assert.Equal("AL-7075", successor.MaterialId);
            Assert.Equal(2, successor.CurrentRevisionNumber);

            var third = Assert.IsType<Part>(await successor.ReviseAsync("Revised again.", "Rev C."));
            Assert.Equal("AL-7075", third.MaterialId);
            Assert.Equal(3, third.CurrentRevisionNumber);
        }
    }

    // ================================================================
    // Helpers
    // ================================================================

    /// <summary>
    /// Every public instance property the concrete type or its
    /// intermediate bases declare — everything below
    /// <see cref="EngineeringObjectBase"/>, which is the base half of the
    /// record and is restored separately.
    /// </summary>
    private static IEnumerable<PropertyInfo> TypeSpecificProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => p.CanRead)
            .Where(p => p.DeclaringType is not null
                && p.DeclaringType != typeof(EngineeringObjectBase)
                && p.DeclaringType != typeof(object))
            .OrderBy(p => p.Name, StringComparer.Ordinal);

    /// <summary>A stable, comparable rendering of a property value, sequences included.</summary>
    private static string Describe(object? value) =>
        value switch
        {
            null => "<null>",
            string s => s,
            IEnumerable sequence => "[" + string.Join(", ", sequence.Cast<object?>().Select(Describe)) + "]",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "<null>",
        };

    private static EngineeringDomainContext BuildInMemoryContext()
    {
        var principal = new CurrentPrincipalAccessor();
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);

        return new EngineeringDomainContext(
            new InMemoryEngineeringDocumentStore(principal), repository, relationships,
            new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, new InMemoryObjectStateStore());
    }

    private sealed record Lifetime(EngineeringDomainContext Context, IEngineeringObjectRehydratorRegistry Rehydrators);

    private Lifetime BuildDiskLifetime()
    {
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, _root),
            ]))
            .Build();

        var persistence = new PersistenceStore(configuration);
        var principal = new CurrentPrincipalAccessor();
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);

        var context = new EngineeringDomainContext(
            new EngineeringDocumentStore(persistence, principal), repository, relationships,
            new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, new EngineeringObjectStateStore(persistence));

        var rehydrators = new EngineeringObjectRehydratorRegistry();
        CanonicalObjectKinds.RegisterRehydrators(rehydrators, context);
        MechanicalObjectFactoryRegistry.RegisterRehydrators(rehydrators, context);

        return new Lifetime(context, rehydrators);
    }

    private static async Task<T> CreateAsync<T>(
        EngineeringDomainContext context, string kind, Func<IEngineeringDocument, IDocumentRevision, T> ctor)
        where T : EngineeringObjectBase, IRehydratable<T> =>
        (T)await new EngineeringObjectFactory<T>(kind, context, ctor).CreateAsync($"{kind} — for test purposes.").ConfigureAwait(false);

    /// <summary>
    /// The same enumeration <see cref="RevisionStatePreservationTests"/>
    /// uses, repeated rather than shared: these two files answer different
    /// questions about the same list, and a shared fixture would let a
    /// change to one silently change the other's coverage.
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

            ("Assembly", c => Make<Core.EngineeringDomain.Assembly>(c, "Assembly", (d, r) => new Core.EngineeringDomain.Assembly(d, r, c, "ASM-1", "An assembly", metadata, [child]))),
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

    private static async Task<EngineeringObjectBase> Make<T>(
        EngineeringDomainContext context, string kind, Func<IEngineeringDocument, IDocumentRevision, T> ctor)
        where T : EngineeringObjectBase, IRehydratable<T> =>
        await CreateAsync(context, kind, ctor).ConfigureAwait(false);

    private static async Task MutateAnyMutableTypeStateAsync(EngineeringObjectBase instance)
    {
        switch (instance)
        {
            case EngineeringTask task:
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

            case Risk risk:
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

    /// <summary>
    /// A Kind that counts how often its own state reader runs, and whose
    /// only type state is a list — so a value applied twice shows up as
    /// duplicated entries rather than as an equal value.
    /// </summary>
    private sealed class CountingFixture : EngineeringObjectBase, IRehydratable<CountingFixture>
    {
        public const string KindName = "CountingFixture";

        private static int _rehydrateCallCount;

        public CountingFixture(
            IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
            string? identifier, string displayName, EngineeringObjectMetadata metadata, IReadOnlyList<string> readerNotes)
            : base(document, currentRevision, context, identifier, displayName, metadata) =>
            ReaderNotes = readerNotes;

        public static int RehydrateCallCount => Volatile.Read(ref _rehydrateCallCount);

        public IReadOnlyList<string> ReaderNotes { get; }

        protected override void CaptureTypeState(IDictionary<string, string?> state) =>
            WriteList(state, nameof(ReaderNotes), ReaderNotes);

        static CountingFixture IRehydratable<CountingFixture>.Rehydrate(
            IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state)
        {
            Interlocked.Increment(ref _rehydrateCallCount);
            return new CountingFixture(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeList(nameof(ReaderNotes)));
        }
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
