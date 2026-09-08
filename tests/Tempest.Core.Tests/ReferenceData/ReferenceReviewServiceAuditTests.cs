using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

using Tempest.Core.Tests.Runtime;
namespace Tempest.Core.Tests.ReferenceData;

/// <summary>
/// Proves <see cref="ReferenceReviewService.VerifyAsync{TDefinition}"/> and
/// <see cref="ReferenceReviewService.ReleaseAsync{TDefinition}"/> each
/// record a real audit row, attributed to the reviewer's own
/// <see cref="IIdentity.Id"/> (`WP 17.2A`, ADR-0146).
/// </summary>
public class ReferenceReviewServiceAuditTests
{
    private const string ReviewerId = "audit-test-reviewer";

    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();

        var runTask = host.RunAsync();

        await RunningHostFixture.WaitUntilRunningAsync(host);

        await body(host);

        await host.StopAsync();
        await runTask;
    }

    private static IMaterialCatalog Materials(ITempestHost host) =>
        (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;

    private static async Task SeedAsync(ITempestHost host)
    {
        var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService))!;
        await seeder.ApplyAsync(Materials(host), MaterialSeed.Instance);
    }

    [Fact]
    public async Task VerifyAsync_RecordsAReferenceVerifiedAuditRow_AttributedToTheReviewersIdentityId()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);

            var principals = new CurrentPrincipalAccessor();
            principals.SetCurrent(new PlatformPrincipal(
                new PlatformIdentity(ReviewerId, ReviewerId),
                [AuditQuery.QueryPermission]));

            // Deliberately constructed directly over the host's own real
            // IPersistenceStore, with `principals` (not the host's own
            // ICurrentPrincipalAccessor, which nothing here establishes a
            // principal on) as the shared actor both the recorder and the
            // query resolve — the same "compose over already-resolved
            // Platform Services" shape WorkspaceHost itself uses.
            var persistenceStore = (IPersistenceStore)host.Services!.GetService(typeof(IPersistenceStore))!;
            var auditRecorder = new AuditRecorder(persistenceStore, principals);
            var auditQuery = new AuditQuery(persistenceStore, principals, new PermissionEvaluator());
            var review = new ReferenceReviewService(principals, auditRecorder: auditRecorder);

            var verified = await review.VerifyAsync(
                Materials(host),
                MaterialSeed.Aluminium6082T6,
                new ReferenceReviewStatement("Aalco technical datasheet"));

            var rows = await auditQuery.QueryAsync(new AuditQueryCriteria(actorId: ReviewerId));

            var row = Assert.Single(rows, r => r.Action == ReferenceReviewService.ReferenceVerifiedActionName);
            Assert.Equal(ReviewerId, row.ActorId);
            Assert.Equal(MaterialSeed.Aluminium6082T6, row.Detail["Subject"]);
            Assert.Equal(verified.RevisionNumber.ToString(), row.Detail["Revision"]);
        });
    }

    [Fact]
    public async Task ReleaseAsync_RecordsAReferenceReleasedAuditRow_AttributedToTheReleasersIdentityId()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);

            var principals = new CurrentPrincipalAccessor();
            principals.SetCurrent(new PlatformPrincipal(
                new PlatformIdentity(ReviewerId, ReviewerId),
                [AuditQuery.QueryPermission]));

            // Deliberately constructed directly over the host's own real
            // IPersistenceStore, with `principals` (not the host's own
            // ICurrentPrincipalAccessor, which nothing here establishes a
            // principal on) as the shared actor both the recorder and the
            // query resolve — the same "compose over already-resolved
            // Platform Services" shape WorkspaceHost itself uses.
            var persistenceStore = (IPersistenceStore)host.Services!.GetService(typeof(IPersistenceStore))!;
            var auditRecorder = new AuditRecorder(persistenceStore, principals);
            var auditQuery = new AuditQuery(persistenceStore, principals, new PermissionEvaluator());
            var review = new ReferenceReviewService(principals, auditRecorder: auditRecorder);

            await review.VerifyAsync(
                Materials(host),
                MaterialSeed.Aluminium6082T6,
                new ReferenceReviewStatement("Aalco technical datasheet"));

            var released = await review.ReleaseAsync(
                Materials(host), MaterialSeed.Aluminium6082T6, "Required for the bracket section check.");

            var rows = await auditQuery.QueryAsync(new AuditQueryCriteria(actorId: ReviewerId));

            var row = Assert.Single(rows, r => r.Action == ReferenceReviewService.ReferenceReleasedActionName);
            Assert.Equal(ReviewerId, row.ActorId);
            Assert.Equal(MaterialSeed.Aluminium6082T6, row.Detail["Subject"]);
            Assert.Equal(released.RevisionNumber.ToString(), row.Detail["Revision"]);
        });
    }

    [Fact]
    public async Task VerifyAsync_WithNoAuditRecorderSupplied_BehavesExactlyAsBefore()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);

            var principals = new CurrentPrincipalAccessor();
            principals.SetCurrent(new PlatformPrincipal(new PlatformIdentity(ReviewerId, ReviewerId), []));

            var review = new ReferenceReviewService(principals);

            var verified = await review.VerifyAsync(
                Materials(host),
                MaterialSeed.Aluminium6082T6,
                new ReferenceReviewStatement("Aalco technical datasheet"));

            Assert.Equal(ReferenceValidationState.Checked, verified.ValidationState);
        });
    }
}
