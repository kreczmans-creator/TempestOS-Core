using System.Diagnostics;
using Tempest.Workspace.Mechanical;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Tests.Persistence;
using Xunit.Abstractions;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `TD-88`/`WP 21.5B` Scope #4's own benchmark — not timing-asserted (a
/// wall-clock number is never a pass/fail gate in this suite, only a
/// recorded fact: the machine this happens to run on is not a claim about
/// every machine) — over a generated estate of 10,000 objects: startup to
/// <see cref="EngineeringObjectRehydrationService.IndexBuilt"/>, startup to
/// first project open, and the first read of an un-materialised object.
/// The numbers this records are quoted in the Work Package's own report
/// and in the release notes row, against `WP 20.1C2`'s own measured ~190 ms
/// per thousand objects for the fully-eager predecessor.
/// </summary>
/// <remarks>
/// <para>
/// The estate's own bulk Kind is <c>Assembly</c> — unenforced by
/// <see cref="BusinessIdentifierScope.EnforcedKinds"/> — deliberately: an
/// enforced Kind (<c>Part</c>, most directly) would have its own business
/// identifier rebuilt eagerly during <c>RehydrateAsync</c> itself (this
/// Work Package's own disclosed kill switch), which would materialise the
/// whole estate as a side effect and make "first read of an
/// un-materialised object" measure nothing real.
/// </para>
/// <para>
/// <b>Read this benchmark's "RehydrateAsync returning" figure with its own
/// disclosed context, not as a bare regression against `WP 20.1C2`'s ~190
/// ms/1000.</b> That figure is dominated by
/// <c>EngineeringObjectRehydrationService.RebuildRelationshipsAsync</c>'s
/// own per-object <c>GetReferencesAsync</c> call — unchanged by this Work
/// Package, deliberately kept fully eager for the whole estate (see that
/// method's own remarks) because it is id-keyed, not materialisation. Under
/// <see cref="InMemoryQueryablePersistenceStore"/>'s own <c>ListKeysAsync(collection)</c>
/// (a scan of every key this whole test double holds, filtered by
/// collection — its own documented trade-off, never claimed to model the
/// real <c>SqlitePersistenceStore</c>'s indexed query), that per-object
/// call's cost grows with total store size, so 10× the objects costs
/// noticeably more than 10× the time — a test-double characteristic
/// present identically in the pre-`WP 21.5B` eager code path, not something
/// this Work Package introduced or could remove without touching
/// relationship rebuilding, which is outside this brief's scope. The
/// figures this Work Package's own change actually moves are the other
/// three: index availability, project-scoped materialisation, and
/// per-object read cost.
/// </para>
/// </remarks>
public sealed class RehydrationLazyMaterialisationBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public RehydrationLazyMaterialisationBenchmarkTests(ITestOutputHelper output) => _output = output;

    private static (EngineeringDomainContext Domain, EngineeringObjectRehydrationService Service) NewLifetime(
        InMemoryQueryablePersistenceStore persistence)
    {
        var principal = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(persistence, principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);
        var stateStore = new EngineeringObjectStateStore(persistence);

        var domain = new EngineeringDomainContext(
            persistence, documentStore, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, stateStore);

        var rehydrators = new EngineeringObjectRehydratorRegistry();
        MechanicalObjectFactoryRegistry.RegisterRehydrators(rehydrators, domain);

        return (domain, new EngineeringObjectRehydrationService(domain, rehydrators));
    }

    [Fact]
    public async Task TenThousandObjectEstate_MeasuresIndexBuilt_FirstProjectOpen_AndFirstLazyRead()
    {
        const int ProjectMemberCount = 300;
        const int StandaloneCount = 9699; // + 1 Project + ProjectMemberCount members = 10,000 objects total.

        var persistence = new InMemoryQueryablePersistenceStore();
        var (seedDomain, _) = NewLifetime(persistence);

        var projectFactory = new EngineeringObjectFactory<Project>(
            MechanicalObjectFactoryRegistry.Project, seedDomain,
            (doc, rev) => new Project(doc, rev, seedDomain, "PRJ-BENCH", "Benchmark Project", EngineeringObjectMetadata.Empty));
        var project = (Project)await projectFactory.CreateAsync("The one project this benchmark opens.");

        for (var i = 0; i < ProjectMemberCount; i++)
        {
            var index = i;
            var factory = new EngineeringObjectFactory<Assembly>(
                MechanicalObjectFactoryRegistry.Assembly, seedDomain,
                (doc, rev) => new Assembly(doc, rev, seedDomain, $"ASM-MEM-{index}", $"Project Assembly {index}", EngineeringObjectMetadata.Empty));
            var member = (Assembly)await factory.CreateAsync($"Project member {index}.");
            await member.MoveAsync(project.Id);
        }

        Guid unrelatedId = default;
        for (var i = 0; i < StandaloneCount; i++)
        {
            var index = i;
            var factory = new EngineeringObjectFactory<Assembly>(
                MechanicalObjectFactoryRegistry.Assembly, seedDomain,
                (doc, rev) => new Assembly(doc, rev, seedDomain, $"ASM-STD-{index}", $"Standalone Assembly {index}", EngineeringObjectMetadata.Empty));
            var standalone = (Assembly)await factory.CreateAsync($"Standalone object {index}, outside the benchmark project.");
            unrelatedId = standalone.Id;
        }

        var totalCreated = 1 + ProjectMemberCount + StandaloneCount;
        Assert.Equal(10_000, totalCreated);

        // ============================================================
        // The measured pass: a fresh lifetime, as if the process had
        // just restarted onto the estate seeded above.
        // ============================================================
        var (domain, service) = NewLifetime(persistence);

        var stopwatch = Stopwatch.StartNew();
        TimeSpan toIndexBuilt = default;
        service.IndexBuilt += _ => toIndexBuilt = stopwatch.Elapsed;

        var result = await service.RehydrateAsync();
        var toRehydrateReturned = stopwatch.Elapsed;

        Assert.Equal(totalCreated, result.ObjectCount);
        Assert.True(result.IsComplete);

        var toFirstProjectOpen = await TimeAsync(() => domain.Repository.MaterialiseSubtreeAsync(project.Id));

        var toFirstLazyRead = await TimeAsync(async () =>
        {
            var found = await domain.Repository.FindAsync(unrelatedId);
            Assert.NotNull(found);
        });

        // The read above is now materialised and in the identity map — a
        // second ask costs whatever a dictionary lookup costs, not a
        // second reconstruction.
        var toSecondReadOfTheSameObject = await TimeAsync(async () => await domain.Repository.FindAsync(unrelatedId));

        _output.WriteLine($"Estate size: {totalCreated:N0} objects ({ProjectMemberCount:N0} under the opened project, {StandaloneCount:N0} standalone, 1 project).");
        _output.WriteLine($"Startup to IndexBuilt: {toIndexBuilt.TotalMilliseconds:N1} ms.");
        _output.WriteLine($"Startup to RehydrateAsync returning (lazy registration of the whole estate): {toRehydrateReturned.TotalMilliseconds:N1} ms " +
            $"({toRehydrateReturned.TotalMilliseconds / (totalCreated / 1000.0):N1} ms per thousand objects — WP 20.1C2's own eager predecessor measured ~190 ms per thousand).");
        _output.WriteLine($"First project open (MaterialiseSubtreeAsync, {ProjectMemberCount:N0}-object subtree): {toFirstProjectOpen.TotalMilliseconds:N1} ms.");
        _output.WriteLine($"First read of one un-materialised standalone object (FindAsync): {toFirstLazyRead.TotalMilliseconds:N3} ms.");
        _output.WriteLine($"Second read of the same, now-materialised object (identity map): {toSecondReadOfTheSameObject.TotalMilliseconds:N3} ms.");
    }

    private static async Task<TimeSpan> TimeAsync(Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        await action().ConfigureAwait(false);
        return stopwatch.Elapsed;
    }
}
