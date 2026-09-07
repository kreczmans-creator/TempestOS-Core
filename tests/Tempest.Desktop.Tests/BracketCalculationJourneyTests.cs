using Avalonia.Headless.XUnit;
using Tempest.Core.Calculations;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Desktop.Tests;

// What an engineer can actually do through TempestOS, run against the
// desktop application's own host:
//
//   1. see which reference data is held and whether it may be used
//   2. review and release a record, as themselves
//   3. supply the bracket's load and geometry
//   4. execute the check
//   5. read the result and the acceptance state
//   6. inspect the assumptions and the reference it stood on
//   7. retrieve the calculation again afterwards
//
// No view is added and no navigation is changed. This proves the
// application composition reaches the calculation, which is the claim worth
// making before anything is drawn on a screen.
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public class BracketCalculationJourneyTests
{
    private const string EngineerId = "desktop-test-engineer";

    private static GovernedBracketCheckRequest BracketRequest(double loadKilonewtons = 12.0) =>
        new(MaterialSeed.Aluminium6082T6,
            new Quantity<Force>(loadKilonewtons, ForceUnits.Kilonewton),
            new Quantity<Area>(60.0, AreaUnits.SquareMillimetre),
            new Quantity<Length>(150.0, LengthUnits.Millimetre),
            new Quantity<Mass>(50.0, MassUnits.Gram));

    private static void SignIn(WorkspaceHost host)
    {
        var principals = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
        ((CurrentPrincipalAccessor)principals).SetCurrent(
            new PlatformPrincipal(new PlatformIdentity(EngineerId, EngineerId), []));
    }

    private static async Task SeedAsync(WorkspaceHost host)
    {
        var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService));
        var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));
        await seeder.ApplyAsync(materials, MaterialSeed.Instance);
    }

    [AvaloniaFact]
    public async Task AnEngineerCanReviewReleaseCalculateAndRetrieveTheResult()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            SignIn(host);
            await SeedAsync(host);

            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));

            // 1. See what is held, and that it may not yet be used.
            var before = await host.ReferenceLibraries!.ListLibraryAsync("Materials");
            var entry = Assert.Single(before.Entries, e => e.RecordId == MaterialSeed.Aluminium6082T6);
            Assert.False(entry.IsUsableAsAuthoritative);

            // 2. The check refuses while the material is Draft.
            var refused = await host.BracketCheck!.CheckAsync(BracketRequest());
            Assert.Equal(BracketCheckRefusal.MaterialNotReleased, refused.Refusal);

            // 3. Review and release, as the signed-in engineer.
            await host.ReferenceReview!.VerifyAsync(
                materials,
                MaterialSeed.Aluminium6082T6,
                new ReferenceReviewStatement("Aalco 6082-T6 extrusions datasheet, mechanical and physical tables"));

            var released = await host.ReferenceReview.ReleaseAsync(
                materials, MaterialSeed.Aluminium6082T6, "Needed for the bracket check.");

            Assert.Equal(EngineerId, released.Provenance.ReviewerPrincipalId);

            // 4. The same library view now says it may be used.
            var after = await host.ReferenceLibraries!.ListLibraryAsync("Materials");
            var usable = Assert.Single(after.Entries, e => e.RecordId == MaterialSeed.Aluminium6082T6);
            Assert.True(usable.IsUsableAsAuthoritative);
            Assert.True(usable.IsVerified);
            Assert.Null(usable.UnusableReason);

            // 5. Execute, and read the result and the acceptance state.
            var check = await host.BracketCheck!.CheckAsync(BracketRequest());
            Assert.True(check.WasPerformed);

            Assert.Equal(200.0, check.Result!.AppliedStress.ConvertTo(PressureUnits.Megapascal).Value, 1e-9);
            Assert.Equal(0.30, check.Result.StressMargin, 1e-9);
            Assert.Equal(BracketCheckOutcome.MeetsCriteria, check.Result.Outcome);

            // 6. Inspect the assumptions and the reference it stood on.
            Assert.Equal(5, check.Record!.Assumptions.Count);
            Assert.Equal("Aalco Metals Limited", check.MaterialProvenance!.SourceOrganisation);
            Assert.Equal(MaterialSeed.Aluminium6082T6, check.Result.MaterialPin.RecordId);

            // 7. Retrieve it again, typed, through the same host.
            var engine = (ICalculationEngine)host.Services!.GetService(typeof(ICalculationEngine));
            var retrieved = await engine.FindRecordAsync<BracketSectionCheckResult>(check.Record.Id);

            Assert.NotNull(retrieved);
            Assert.Equal(0.30, retrieved!.Result.StressMargin, 1e-9);
            Assert.Equal(check.Result.MaterialPin, retrieved.Result.MaterialPin);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AnOverloadedBracketIsReportedAsNotMeetingItsCriteria_NotAsAnError()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            SignIn(host);
            await SeedAsync(host);

            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));
            await host.ReferenceReview!.VerifyAsync(
                materials, MaterialSeed.Aluminium6082T6, new ReferenceReviewStatement("Aalco datasheet"));
            await host.ReferenceReview.ReleaseAsync(materials, MaterialSeed.Aluminium6082T6, "For the check.");

            var check = await host.BracketCheck!.CheckAsync(BracketRequest(loadKilonewtons: 18.0));

            // The calculation succeeded; the bracket did not. Those are
            // different things and the application reports them differently.
            Assert.True(check.WasPerformed);
            Assert.Equal(BracketCheckRefusal.None, check.Refusal);
            Assert.Equal(BracketCheckOutcome.DoesNotMeetCriteria, check.Result!.Outcome);
            Assert.False(check.Result.StressCriterionMet);
            Assert.Equal(CalculationValidationOutcome.Conditional, check.Record!.Validation.Outcome);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AReviewPerformedThroughTheApplicationIsAttributedToTheSignedInEngineer()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            await SeedAsync(host);

            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));

            // Nobody signed in: the application refuses to record a review.
            var principals = (CurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
            principals.SetCurrent(null);

            await Assert.ThrowsAsync<ReferenceReviewException>(
                () => host.ReferenceReview!.VerifyAsync(
                    materials, MaterialSeed.Aluminium6082T6, new ReferenceReviewStatement("Anything")));

            var untouched = await materials.FindAsync(MaterialSeed.Aluminium6082T6);
            Assert.Equal(ReferenceValidationState.Draft, untouched!.ValidationState);
            Assert.Null(untouched.Provenance.ReviewerPrincipalId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
