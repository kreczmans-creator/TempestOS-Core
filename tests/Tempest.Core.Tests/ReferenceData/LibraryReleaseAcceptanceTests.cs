using Tempest.Core.Audit;
using Tempest.Core.Bearings;
using Tempest.Core.Configuration;
using Tempest.Core.Constants;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Runtime;
using Tempest.Core.Standards;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Runtime;

namespace Tempest.Core.Tests.ReferenceData;

/// <summary>
/// `WP 18.0B` acceptance test 3: one seeded record per library — Materials,
/// Fasteners, Bearings, Standards, Constants — released through the one
/// review flow, with an audit row for each act and its citation (where the
/// dataset holds one) surviving to the released revision.
/// </summary>
public class LibraryReleaseAcceptanceTests
{
    private const string ReviewerId = "library-release-reviewer";

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

    private static CurrentPrincipalAccessor SignedInWith(params Permission[] permissions)
    {
        var principals = new CurrentPrincipalAccessor();
        principals.SetCurrent(new PlatformPrincipal(new PlatformIdentity(ReviewerId, ReviewerId), permissions));
        return principals;
    }

    /// <summary>
    /// Seeds and releases one record from every one of the five libraries,
    /// asserting: it reaches Released; a `reference.verified` and a
    /// `reference.released` audit row exist for it, attributed to the
    /// reviewer; its citation, where the dataset held one, is still there
    /// on the released revision; and it now appears in <c>ListAsync</c>
    /// filtered to Released.
    /// </summary>
    [Fact]
    public async Task OneSeededRecordPerLibrary_ReleasesThroughTheReviewService_WithAuditRowsAndItsCitationIntact()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService))!;
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
            var fasteners = (IFastenerCatalog)host.Services!.GetService(typeof(IFastenerCatalog))!;
            var bearings = (IBearingCatalog)host.Services!.GetService(typeof(IBearingCatalog))!;
            var standards = (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog))!;
            var constants = (IConstantCatalog)host.Services!.GetService(typeof(IConstantCatalog))!;

            await seeder.ApplyAsync(materials, MaterialSeed.Instance);
            await seeder.ApplyAsync(fasteners, FastenerSeed.Instance);
            await seeder.ApplyAsync(bearings, BearingSeed.Instance);
            await seeder.ApplyAsync(standards, StandardSeed.Instance);
            await seeder.ApplyAsync(constants, ConstantSeed.Instance);

            var persistenceStore = (IPersistenceStore)host.Services!.GetService(typeof(IPersistenceStore))!;
            var queryableStore = (IQueryablePersistenceStore)host.Services!.GetService(typeof(IQueryablePersistenceStore))!;
            var principals = SignedInWith(
                ReferenceReviewService.VerifyPermission, ReferenceReviewService.ReleasePermission, AuditQuery.QueryPermission);
            var auditRecorder = new AuditRecorder(persistenceStore, principals);
            var auditQuery = new AuditQuery(queryableStore, principals, new PermissionEvaluator());
            var review = new ReferenceReviewService(principals, auditRecorder: auditRecorder);

            await ReleaseAndAssertAsync(review, auditQuery, materials, MaterialSeed.S355J2, expectCitation: true);
            await ReleaseAndAssertAsync(review, auditQuery, fasteners, "fst-m8-coarse", expectCitation: true);
            await ReleaseAndAssertAsync(review, auditQuery, bearings, BearingSeed.Rhd6205, expectCitation: true);
            await ReleaseAndAssertAsync(review, auditQuery, standards, StandardSeed.Iso15, expectCitation: true);
            await ReleaseAndAssertAsync(review, auditQuery, constants, "const-speed-of-light", expectCitation: true);
        });
    }

    private static async Task ReleaseAndAssertAsync<TDefinition>(
        ReferenceReviewService review,
        AuditQuery auditQuery,
        IReferenceDataCatalog<TDefinition> catalog,
        string recordId,
        bool expectCitation)
        where TDefinition : class
    {
        var beforeRelease = await catalog.FindAsync(recordId);
        Assert.NotNull(beforeRelease);
        Assert.Equal(ReferenceValidationState.Draft, beforeRelease!.ValidationState);
        if (expectCitation)
            Assert.NotNull(beforeRelease.Source);

        await review.VerifyAsync(catalog, recordId, new ReferenceReviewStatement($"Reviewed against {catalog.LibraryName}'s own cited source."));
        var released = await review.ReleaseAsync(catalog, recordId, "Required for the Libraries acceptance test.");

        Assert.Equal(ReferenceValidationState.Released, released.ValidationState);

        // The citation set at seeding survives verification and release —
        // ReferenceReviewService revises the record's provenance on both
        // acts, through the citation-unaware overload, which this Work
        // Package's own `ReferenceDataCatalog<TDefinition>` carries the
        // existing citation forward through rather than wiping.
        if (expectCitation)
            Assert.Equal(beforeRelease.Source, released.Source);
        else
            Assert.Null(released.Source);

        var verifyRows = await auditQuery.QueryAsync(new AuditQueryCriteria(actorId: ReviewerId));
        Assert.Contains(verifyRows, r => r.Action == ReferenceReviewService.ReferenceVerifiedActionName && r.Detail["Subject"] == recordId);
        Assert.Contains(verifyRows, r => r.Action == ReferenceReviewService.ReferenceReleasedActionName && r.Detail["Subject"] == recordId);

        var released2 = await catalog.ListAsync();
        Assert.Contains(released2, r => r.Id == recordId && r.ValidationState == ReferenceValidationState.Released);
    }

    /// <summary>
    /// A principal without <c>reference.release</c> is refused, exactly as
    /// <see cref="ReferenceReviewPermissionTests"/> already proves for
    /// Materials — repeated here against a second library, Standards, to
    /// show the refusal is the review service's own behaviour and not
    /// something particular to one catalogue.
    /// </summary>
    [Fact]
    public async Task APrincipalWithoutReleasePermission_IsRefused_ForAnyLibrary()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService))!;
            var standards = (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog))!;
            await seeder.ApplyAsync(standards, StandardSeed.Instance);

            var review = new ReferenceReviewService(
                SignedInWith(ReferenceReviewService.VerifyPermission), permissions: new PermissionEvaluator());

            await review.VerifyAsync(standards, StandardSeed.Iso15, new ReferenceReviewStatement("Reviewed against the standards index."));

            var refusal = await Assert.ThrowsAsync<ReferenceReviewException>(
                () => review.ReleaseAsync(standards, StandardSeed.Iso15, "Required for the acceptance test."));

            Assert.Contains(ReferenceReviewService.ReleasePermission.Key, refusal.Message, StringComparison.Ordinal);
            var record = await standards.FindAsync(StandardSeed.Iso15);
            Assert.Equal(ReferenceValidationState.Checked, record!.ValidationState);
        });
    }
}
