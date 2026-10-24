using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Tests.Sheets;
using Tempest.Core.Bearings;
using Tempest.Core.Configuration;
using Tempest.Core.Constants;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.Standards;
using Tempest.Core.Tests.EngineeringDomain;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// Grey team (§8): a database written by the pre-calc code path (Evidence
/// created, checked and issued through the v0.25 `IssueAsync`, a real
/// SQLite file) is reopened by the v0.26.0 code: the issued record is still
/// Issued and immutable, the schema version is unchanged, a calculation
/// instance can be created in the same project, issued in one transaction,
/// and linked into the Calculation register beside the old record.
/// </summary>
public sealed class UpgradeTests
{
    private sealed record Host(SqlitePersistenceStore Store, EngineeringDomainContext Context, EvidenceService Evidence, CalcInstanceService Calc, CalcInstanceStore CalcStore);

    private static Host Open(string root)
    {
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource([new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, root)]))
            .Build();
        var store = new SqlitePersistenceStore(configuration);
        var principal = new CurrentPrincipalAccessor();
        var documents = new EngineeringDocumentStore(store, principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);
        var states = new EngineeringObjectStateStore(store);
        var context = new EngineeringDomainContext(store, documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(), new EvidenceComposer(discovery, repository), principal, states, new AttachmentContentStore(store));
        var evidence = new EvidenceService(context, new MaterialCatalog(documents, store), new FastenerCatalog(documents, store), new BearingCatalog(documents, store), new StandardCatalog(documents, store), new ConstantCatalog(documents, store), principal, configuration);
        var calcStore = new CalcInstanceStore(store);
        var calc = new CalcInstanceService(new CalcCatalogue([new DirectStressCalculator()]), calcStore, evidence, context, renderer: null, applicationVersion: "TempestOS upgrade test");
        return new Host(store, context, evidence, calc, calcStore);
    }

    /// <summary>What the product does at start-up: rebuild the live repositories from the persisted state of every known kind.</summary>
    private static async Task RehydrateAsync(EngineeringDomainContext context)
    {
        var rehydrators = new EngineeringObjectRehydratorRegistry();
        Tempest.Workspace.CanonicalObjectKinds.RegisterRehydrators(rehydrators, context);
        Tempest.Workspace.Mechanical.MechanicalObjectFactoryRegistry.RegisterRehydrators(rehydrators, context);
        Tempest.Workspace.Calculations.CalculationObjectFactoryRegistry.RegisterRehydrators(rehydrators, context);
        Tempest.Workspace.Documents.DocumentObjectFactoryRegistry.RegisterRehydrators(rehydrators, context);
        rehydrators.Register<Evidence>(Evidence.CanonicalKind, context);
        var result = await new EngineeringObjectRehydrationService(context, rehydrators).RehydrateAsync();
        Assert.True(result.IsComplete, $"Unknown kinds: {string.Join(", ", result.UnknownKinds)}; failed: {result.FailedObjectIds.Count}.");
    }

    [Fact]
    public async Task A_Database_With_Issued_Evidence_Opens_Unchanged_And_Accepts_New_Calculations()
    {
        var root = Path.Combine(Path.GetTempPath(), "tempest-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Guid projectId, oldEvidenceId;

        try
        {
            // Phase A: the old path — evidence issued with the two-step IssueAsync, as v0.25 did.
            {
                var old = Open(root);
                await using var _ = old.Store;
                var factory = new EngineeringObjectFactory<Project>("Project", old.Context, (doc, rev) => new Project(doc, rev, old.Context, "UPG-PRJ", "Upgrade project", EngineeringObjectMetadata.Empty));
                projectId = (await factory.CreateAsync("Upgrade project.")).Id;
                var created = await old.Evidence.CreateAsync(projectId, "Legacy bracket calculation", EvidenceClassification.Calculation);
                oldEvidenceId = created.Id;
                Assert.True((await old.Evidence.RecordCheckAsync(created.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
                var issued = await old.Evidence.IssueAsync(created.Id, "LEGACY-ISS-1", "A", "Client Co");
                Assert.True(issued.Succeeded, issued.Reason);
            }

            // Phase B: reopen with the v0.26.0 code.
            {
                var upgraded = Open(root);
                await using var _ = upgraded.Store;
                await RehydrateAsync(upgraded.Context);
                Assert.Equal(1, SqlitePersistenceStore.SchemaVersion);

                var legacy = (Evidence)(await upgraded.Context.Repository.FindAsync(oldEvidenceId))!;
                Assert.Equal(EvidenceStatus.Issued, legacy.Status);
                Assert.Equal("LEGACY-ISS-1", legacy.Issue!.IssueReference);
                var again = await upgraded.Evidence.IssueInOneTransactionAsync(oldEvidenceId, "LEGACY-ISS-2", "A", "Client Co");
                Assert.False(again.Succeeded);
                Assert.Equal("LEGACY-ISS-1", ((Evidence)(await upgraded.Context.Repository.FindAsync(oldEvidenceId))!).Issue!.IssueReference);

                var instance = await upgraded.Calc.CreateAsync("test.direct-stress", projectId, "New calc beside the legacy record", LibraryTestHost.ProvenancedInputs());
                Assert.NotNull(instance.RegisterObjectId);
                Assert.True((await upgraded.Calc.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
                var result = await upgraded.Calc.IssueAsync(instance.Id, "NEW-ISS-1", "Client Co");
                Assert.True(result.Succeeded, result.Reason);

                var stored = (await upgraded.CalcStore.FindInstanceAsync(instance.Id))!;
                Assert.Equal(CalcInstanceStatus.Issued, stored.Status);
                var register = await upgraded.Context.Repository.FindAsync(instance.RegisterObjectId!.Value) as Calculation;
                Assert.NotNull(register);
                Assert.Contains(await register!.GetRelationshipsAsync(), r => r.TargetId == instance.EvidenceId && r.RelationshipKind == CalcInstanceService.DocumentedByRelationship);
                Assert.Equal(EvidenceStatus.Issued, ((Evidence)(await upgraded.Context.Repository.FindAsync(oldEvidenceId))!).Status);
            }

            // Phase C: reopen once more — everything persisted, nothing depends on in-memory state.
            {
                var reopened = Open(root);
                await using var _ = reopened.Store;
                await RehydrateAsync(reopened.Context);
                Assert.Equal(EvidenceStatus.Issued, ((Evidence)(await reopened.Context.Repository.FindAsync(oldEvidenceId))!).Status);
                var instances = await reopened.CalcStore.ListInstancesAsync(projectId);
                Assert.Single(instances);
                Assert.Equal(CalcInstanceStatus.Issued, instances[0].Status);
            }
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }
}
