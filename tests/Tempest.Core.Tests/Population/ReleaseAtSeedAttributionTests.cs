using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Runtime;

namespace Tempest.Core.Tests.Population;

// Colour review board v0.23.0, B3: release at seed is the seed process's
// work, so every audit row and every document revision it writes must name
// the seed identity — never the person who happened to be signed in when
// the app first started — and the person must be current again afterwards.
public class ReleaseAtSeedAttributionTests
{
    private const string PersonId = "S-1-5-21-person";

    [Fact]
    public async Task AuditActorAndRevisionAuthorAreTheSeedIdentityNotThePersonSignedIn()
    {
        using var temp = new TempDirectory();

        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, temp.Path),
            ]))
            .Build();

        var runTask = host.RunAsync();
        await RunningHostFixture.WaitUntilRunningAsync(host);

        try
        {
            var services = host.Services!;
            var session = (PrincipalSession)services.GetService(typeof(PrincipalSession))!;
            var principals = (ICurrentPrincipalAccessor)services.GetService(typeof(ICurrentPrincipalAccessor))!;
            var seeder = (ReferenceSeedService)services.GetService(typeof(ReferenceSeedService))!;
            var materials = (IMaterialCatalog)services.GetService(typeof(IMaterialCatalog))!;
            var documents = (IEngineeringDocumentStore)services.GetService(typeof(IEngineeringDocumentStore))!;
            var audit = (IAuditQuery)services.GetService(typeof(IAuditQuery))!;

            session.Establish(new PlatformPrincipal(new PlatformIdentity(PersonId, "A Person"), ApplicationPermissions.LocalSession));
            var startedAt = DateTimeOffset.UtcNow.AddSeconds(-1);

            var outcome = await seeder.ApplyAsync(materials, MaterialSeed.Instance);
            Assert.True(outcome.ReleasedCount > 0);

            // Restored afterwards: the person is current again.
            Assert.Equal(PersonId, principals.Current?.Identity.Id);

            var rows = await audit.QueryAsync(new AuditQueryCriteria(from: startedAt));
            Assert.NotEmpty(rows);
            Assert.All(rows, r => Assert.Equal(ReferenceSeedReleasePolicy.SeedPrincipalId, r.ActorId));

            foreach (var record in await materials.ListAsync())
            {
                var history = await documents.GetRevisionHistoryAsync(record.UnderlyingDocumentId);
                Assert.NotEmpty(history);
                Assert.All(history, r => Assert.Equal(ReferenceSeedReleasePolicy.SeedPrincipalId, r.AuthorPrincipalId));
            }

            // And the person's own writes after the pass are theirs again.
            var recorder = (IAuditRecorder)services.GetService(typeof(IAuditRecorder))!;
            await recorder.RecordAsync("test.after-seed");
            var after = await audit.QueryAsync(new AuditQueryCriteria(action: "test.after-seed"));
            Assert.Equal(PersonId, Assert.Single(after).ActorId);
        }
        finally
        {
            await host.StopAsync();
            await runTask;
        }
    }

    [Fact]
    public async Task ActingAsIsScopedToTheAsyncFlowAndRestoredOnDispose()
    {
        var accessor = new CurrentPrincipalAccessor();
        var person = new PlatformPrincipal(new PlatformIdentity(PersonId, PersonId), []);
        var seed = new PlatformPrincipal(new PlatformIdentity(ReferenceSeedReleasePolicy.SeedPrincipalId, "seed"), []);
        accessor.SetCurrent(person);

        var gate = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        string? seenElsewhere = null;

        // A flow started before the scope opens never sees it.
        var other = Task.Run(async () =>
        {
            await gate.Task;
            seenElsewhere = accessor.Current?.Identity.Id;
            release.SetResult();
        });

        using (accessor.BeginActingAs(seed))
        {
            Assert.Equal(ReferenceSeedReleasePolicy.SeedPrincipalId, accessor.Current?.Identity.Id);
            gate.SetResult();
            await release.Task;
        }

        await other;
        Assert.Equal(PersonId, seenElsewhere);
        Assert.Equal(PersonId, accessor.Current?.Identity.Id);
    }
}
