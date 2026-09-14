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
/// `WP 17.9.3`, closing hazard H8 of the design-freeze review: before this,
/// any signed-in principal could verify and release any reference record.
/// Now the acts require <see cref="ReferenceReviewService.VerifyPermission"/>
/// and <see cref="ReferenceReviewService.ReleasePermission"/>, checked
/// through the platform's one authorisation point.
/// </summary>
public class ReferenceReviewPermissionTests
{
    private const string ReviewerId = "permission-test-reviewer";

    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
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

    private static CurrentPrincipalAccessor SignedInWith(params Permission[] permissions)
    {
        var principals = new CurrentPrincipalAccessor();
        principals.SetCurrent(new PlatformPrincipal(new PlatformIdentity(ReviewerId, ReviewerId), permissions));
        return principals;
    }

    [Fact]
    public void TheSessionPrincipal_HoldsBothReviewPermissions_ByDefault()
    {
        Assert.Contains(ReferenceReviewService.VerifyPermission, ApplicationPermissions.LocalSession);
        Assert.Contains(ReferenceReviewService.ReleasePermission, ApplicationPermissions.LocalSession);
    }

    [Fact]
    public async Task APrincipalWithoutTheVerifyPermission_IsRefused_AndTheRecordStaysDraft()
    {
        using var temp = new TempDirectory();
        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            var review = new ReferenceReviewService(SignedInWith(), permissions: new PermissionEvaluator());

            var refusal = await Assert.ThrowsAsync<ReferenceReviewException>(() => review.VerifyAsync(
                Materials(host), MaterialSeed.Aluminium6082T6, new ReferenceReviewStatement("Aalco technical datasheet")));

            Assert.Contains(ReferenceReviewService.VerifyPermission.Key, refusal.Message, StringComparison.Ordinal);
            var record = await Materials(host).FindAsync(MaterialSeed.Aluminium6082T6);
            Assert.Equal(ReferenceValidationState.Draft, record!.ValidationState);
        });
    }

    [Fact]
    public async Task APrincipalWhoMayVerifyButNotRelease_VerifiesAndIsThenRefused()
    {
        using var temp = new TempDirectory();
        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            var review = new ReferenceReviewService(
                SignedInWith(ReferenceReviewService.VerifyPermission), permissions: new PermissionEvaluator());

            var verified = await review.VerifyAsync(
                Materials(host), MaterialSeed.Aluminium6082T6, new ReferenceReviewStatement("Aalco technical datasheet"));
            Assert.Equal(ReferenceValidationState.Checked, verified.ValidationState);

            var refusal = await Assert.ThrowsAsync<ReferenceReviewException>(() => review.ReleaseAsync(
                Materials(host), MaterialSeed.Aluminium6082T6, "Required for the bracket section check."));

            Assert.Contains(ReferenceReviewService.ReleasePermission.Key, refusal.Message, StringComparison.Ordinal);
            var record = await Materials(host).FindAsync(MaterialSeed.Aluminium6082T6);
            Assert.Equal(ReferenceValidationState.Checked, record!.ValidationState);
        });
    }

    [Fact]
    public async Task APrincipalWithBothPermissions_VerifiesAndReleases()
    {
        using var temp = new TempDirectory();
        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            var review = new ReferenceReviewService(
                SignedInWith(ReferenceReviewService.VerifyPermission, ReferenceReviewService.ReleasePermission),
                permissions: new PermissionEvaluator());

            await review.VerifyAsync(Materials(host), MaterialSeed.Aluminium6082T6, new ReferenceReviewStatement("Aalco technical datasheet"));
            var released = await review.ReleaseAsync(Materials(host), MaterialSeed.Aluminium6082T6, "Required for the bracket section check.");

            Assert.Equal(ReferenceValidationState.Released, released.ValidationState);
            Assert.Equal(ReviewerId, released.Provenance.ReviewerPrincipalId);
        });
    }

    [Fact]
    public async Task WithoutAnEvaluator_TheServiceBehavesAsBefore_GatedOnlyOnBeingSignedIn()
    {
        using var temp = new TempDirectory();
        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            var review = new ReferenceReviewService(SignedInWith());

            var verified = await review.VerifyAsync(
                Materials(host), MaterialSeed.Aluminium6082T6, new ReferenceReviewStatement("Aalco technical datasheet"));

            Assert.Equal(ReferenceValidationState.Checked, verified.ValidationState);
        });
    }
}
