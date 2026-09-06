using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Every canonical Kind this Work Package took into production, driven
/// through the journey that actually matters: <b>create → persist → a new
/// host lifetime → rehydrate</b>.
/// </summary>
/// <remarks>
/// <para>
/// A registration test proves the map has an entry. This proves the object
/// comes back — with its identity, its kind, its business identifier and
/// its parent intact — over a <em>real</em> <see cref="PersistenceStore"/>
/// on disk, in a second set of repositories that share nothing with the
/// first but the files.
/// </para>
/// <para>
/// Twelve of these Kinds previously came back only because
/// <c>Tempest.Samples</c> registered them; nine came back not at all. The
/// second lifetime here registers <b>only</b> the production path.
/// </para>
/// </remarks>
public sealed class CanonicalKindRoundTripTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tempest-roundtrip-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // ================================================================
    // Every kind, one at a time
    // ================================================================

    public static TheoryData<string> EveryCanonicalKind()
    {
        var data = new TheoryData<string>();
        foreach (var kind in CanonicalObjectKinds.All)
            data.Add(kind);
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryCanonicalKind))]
    public async Task ACanonicalObject_SurvivesARestart_WithItsOwnState(string kind)
    {
        Guid id;
        Type type;
        EngineeringObjectState before;

        // ---- FIRST LIFETIME ------------------------------------------
        {
            var first = BuildLifetime();
            var created = await CreateAsync(first, kind, $"{kind}-001", $"A {kind}");

            id = created.Id;
            type = created.GetType();
            before = ((EngineeringObjectBase)created).CaptureState();

            Assert.Equal(kind, created.Kind);
        }

        // ---- SECOND LIFETIME: production registration only -----------
        {
            var second = BuildLifetime();
            var result = await RehydrateAsync(second);

            Assert.Empty(result.UnknownKinds);
            Assert.True(result.IsComplete, $"Rehydration was incomplete for '{kind}'.");

            var recovered = await second.Context.Repository.FindAsync(id);

            Assert.NotNull(recovered);
            Assert.Equal(type, recovered!.GetType());

            // Comparing the whole captured state, rather than a couple of
            // properties, is what makes this a round-trip test: TypeState is
            // each concrete type's own contribution, so an omission in any
            // one type's rehydration constructor shows up here as a missing
            // or changed key rather than passing unnoticed.
            AssertSameState(before, ((EngineeringObjectBase)recovered).CaptureState());
        }
    }

    private static void AssertSameState(EngineeringObjectState before, EngineeringObjectState after)
    {
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.Kind, after.Kind);
        Assert.Equal(before.Identifier, after.Identifier);
        Assert.Equal(before.DisplayName, after.DisplayName);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.ParentId, after.ParentId);
        Assert.Equal(before.IsDeleted, after.IsDeleted);
        Assert.Equal(before.BomLine, after.BomLine);
        Assert.Equal(before.Metadata, after.Metadata);
        Assert.Equal(before.History.Count, after.History.Count);
        Assert.Equal(before.Attachments.Count, after.Attachments.Count);

        // Every type-specific key the original wrote must come back with the
        // same value, and no key may appear from nowhere.
        Assert.Equal(
            before.TypeState.OrderBy(e => e.Key, StringComparer.Ordinal).ToList(),
            after.TypeState.OrderBy(e => e.Key, StringComparer.Ordinal).ToList());
    }

    // ================================================================
    // Nesting and relationships
    // ================================================================

    [Fact]
    public async Task ACanonicalObjectNestedInAProject_KeepsItsParentAcrossARestart()
    {
        Guid projectId;
        Guid partId;
        Guid riskId;

        // Project → Part → Risk. The Risk is a canonical Kind that used to
        // be sample-only, hanging three levels down a real structure.
        {
            var first = BuildLifetime();

            var project = await CreateAsync(first, MechanicalObjectFactoryRegistry.Project, "P-1", "Apollo");
            var part = await CreateAsync(first, MechanicalObjectFactoryRegistry.Part, "PRT-1", "Impeller");
            var risk = await CreateAsync(first, CanonicalObjectKinds.Risk, "RSK-1", "Cavitation");

            projectId = project.Id;
            partId = part.Id;
            riskId = risk.Id;

            await ((IHasParent)part).MoveAsync(projectId);
            await ((IHasParent)risk).MoveAsync(partId);
        }

        {
            var second = BuildLifetime();
            var result = await RehydrateAsync(second);

            Assert.True(result.IsComplete);

            var risk = await second.Context.Repository.FindAsync(riskId);
            var part = await second.Context.Repository.FindAsync(partId);

            Assert.NotNull(risk);
            Assert.NotNull(part);

            // The parent edge is the one that makes project membership
            // work at all (`TD-102`), so it has to survive too.
            Assert.Equal(partId, ((IHasParent)risk!).ParentId);
            Assert.Equal(projectId, ((IHasParent)part!).ParentId);
        }
    }

    [Fact]
    public async Task EveryCanonicalKindTogether_ComesBackInOneRestart_WithNothingUnknown()
    {
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);

        {
            var first = BuildLifetime();
            foreach (var kind in CanonicalObjectKinds.All)
            {
                var created = await CreateAsync(first, kind, $"{kind}-ALL", $"All-kinds {kind}");
                ids[kind] = created.Id;
            }
        }

        {
            var second = BuildLifetime();
            var result = await RehydrateAsync(second);

            Assert.Empty(result.UnknownKinds);
            Assert.Empty(result.FailedObjectIds);
            Assert.Empty(result.OrphanedStateIds);
            Assert.True(result.IsComplete);

            foreach (var (kind, id) in ids)
            {
                var recovered = await second.Context.Repository.FindAsync(id);
                Assert.True(recovered is not null, $"'{kind}' did not come back.");
                Assert.Equal(kind, recovered!.Kind);
            }
        }
    }

    [Fact]
    public async Task AKindWithNoRehydrator_IsReportedAsUnknown_AndNeverSilentlyDropped()
    {
        // The diagnostic requirement. An object whose Kind nothing can
        // rebuild must be named in the result — recovery continues for
        // everything else, but the loss is stated, not swallowed.
        Guid orphanId;

        {
            var first = BuildLifetime();
            var orphan = await CreateAsync(first, "AKindNoDisciplineOwns", "ORPH-1", "Orphan");
            orphanId = orphan.Id;
        }

        {
            var second = BuildLifetime();
            var result = await RehydrateAsync(second);

            Assert.Contains("AKindNoDisciplineOwns", result.UnknownKinds);
            Assert.False(result.IsComplete);
            Assert.Null(await second.Context.Repository.FindAsync(orphanId));
        }
    }

    // ================================================================
    // Fixtures
    // ================================================================

    private sealed record Lifetime(EngineeringDomainContext Context, IEngineeringObjectRehydratorRegistry Rehydrators);

    /// <summary>A fresh set of repositories over the same files — a new process, as far as the object graph is concerned.</summary>
    private Lifetime BuildLifetime()
    {
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, _root),
            ]))
            .Build();

        var store = new PersistenceStore(configuration);
        var principal = new CurrentPrincipalAccessor();
        var documents = new EngineeringDocumentStore(store, principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);

        var context = new EngineeringDomainContext(
            documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, new EngineeringObjectStateStore(store));

        var rehydrators = new EngineeringObjectRehydratorRegistry();
        CanonicalObjectKinds.RegisterRehydrators(rehydrators, context);
        MechanicalObjectFactoryRegistry.RegisterRehydrators(rehydrators, context);

        return new Lifetime(context, rehydrators);
    }

    private static Task<EngineeringRehydrationResult> RehydrateAsync(Lifetime lifetime) =>
        new EngineeringObjectRehydrationService(lifetime.Context, lifetime.Rehydrators).RehydrateAsync();

    /// <summary>
    /// Creates one object of <paramref name="kind"/> through the same
    /// <see cref="EngineeringObjectFactory{T}"/> every discipline uses.
    /// </summary>
    /// <remarks>
    /// The constructor arguments differ per type, so the mapping is
    /// explicit. Every one goes through the real factory, so what lands on
    /// disk is what the product would write.
    /// </remarks>
    private static async Task<IEngineeringObject> CreateAsync(Lifetime lifetime, string kind, string identifier, string name)
    {
        var context = lifetime.Context;
        var metadata = EngineeringObjectMetadata.Empty;
        var reason = $"Round-trip {kind}.";

        return kind switch
        {
            CanonicalObjectKinds.Portfolio => await Make<Portfolio>((d, r) => new Portfolio(d, r, context, identifier, name, metadata)),
            CanonicalObjectKinds.Programme => await Make<Programme>((d, r) => new Programme(d, r, context, identifier, name, metadata)),

            CanonicalObjectKinds.Risk => await Make<Risk>((d, r) => new Risk(d, r, context, identifier, name, metadata, "Low", "Medium")),
            CanonicalObjectKinds.Hazard => await Make<Hazard>((d, r) => new Hazard(d, r, context, identifier, name, metadata, "Low", "High")),
            CanonicalObjectKinds.Issue => await Make<Issue>((d, r) => new Issue(d, r, context, identifier, name, metadata)),
            CanonicalObjectKinds.Decision => await Make<Decision>((d, r) => new Decision(d, r, context, identifier, name, metadata, "Because the test says so.")),
            CanonicalObjectKinds.Assumption => await Make<Assumption>((d, r) => new Assumption(d, r, context, identifier, name, metadata)),

            CanonicalObjectKinds.Task => await Make<EngineeringTask>((d, r) => new EngineeringTask(d, r, context, identifier, name, metadata, "engineer")),
            CanonicalObjectKinds.Action => await Make<EngineeringAction>((d, r) => new EngineeringAction(d, r, context, identifier, name, metadata, Guid.NewGuid(), "engineer")),
            CanonicalObjectKinds.Milestone => await Make<Milestone>((d, r) => new Milestone(d, r, context, identifier, name, metadata, DateTimeOffset.UtcNow.AddMonths(3))),
            CanonicalObjectKinds.Deliverable => await Make<Deliverable>((d, r) => new Deliverable(d, r, context, identifier, name, metadata, Guid.NewGuid())),

            CanonicalObjectKinds.ChangeRequest => await Make<ChangeRequest>((d, r) => new ChangeRequest(d, r, context, identifier, name, metadata)),
            CanonicalObjectKinds.EngineeringChange => await Make<EngineeringChange>((d, r) => new EngineeringChange(d, r, context, identifier, name, metadata, Guid.NewGuid())),
            CanonicalObjectKinds.Approval => await Make<Approval>((d, r) => new Approval(d, r, context, name, metadata, "approver", DateTimeOffset.UtcNow)),
            CanonicalObjectKinds.Review => await Make<Review>((d, r) => new Review(d, r, context, name, metadata, ["reviewer"])),

            CanonicalObjectKinds.Supplier => await Make<Supplier>((d, r) => new Supplier(d, r, context, identifier, name, metadata)),
            CanonicalObjectKinds.PurchaseItem => await Make<PurchaseItem>((d, r) => new PurchaseItem(d, r, context, identifier, name, metadata, Guid.NewGuid())),
            CanonicalObjectKinds.ExternalSystemLink => await Make<ExternalSystemLink>((d, r) => new ExternalSystemLink(d, r, context, name, metadata, "PLM", "EXT-1")),

            CanonicalObjectKinds.Simulation => await Make<Simulation>((d, r) => new Simulation(d, r, context, name, metadata, Guid.NewGuid(), "Thermal")),
            CanonicalObjectKinds.Test => await Make<Test>((d, r) => new Test(d, r, context, name, metadata, Guid.NewGuid(), "Test")),
            CanonicalObjectKinds.Verification => await Make<Core.EngineeringDomain.Verification>((d, r) => new Core.EngineeringDomain.Verification(d, r, context, metadata)),

            MechanicalObjectFactoryRegistry.Project =>
                await Make<Project>((d, r) => new Project(d, r, context, identifier, name, metadata)),
            MechanicalObjectFactoryRegistry.Part =>
                await Make<Part>((d, r) => new Part(d, r, context, identifier, name, metadata)),

            // Any other kind is deliberately unregistered — the unknown-kind
            // case this suite also has to prove.
            _ => await Make<Part>((d, r) => new Part(d, r, context, identifier, name, metadata), kind),
        };

        // `WP 16.4B-R6`: `EngineeringObjectFactory<T>` now requires the
        // Kind's own `IRehydratable<T>` reader, because that is what
        // `ReviseAsync` builds a successor with. Every canonical Kind
        // already implements it — this constraint only restates that.
        async Task<IEngineeringObject> Make<T>(Func<IEngineeringDocument, IDocumentRevision, T> ctor, string? kindOverride = null)
            where T : EngineeringObjectBase, IRehydratable<T> =>
            await new EngineeringObjectFactory<T>(kindOverride ?? kind, context, ctor).CreateAsync(reason);
    }
}
