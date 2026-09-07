using Tempest.Core.Calculations;
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
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations;

// The whole chain, through the real host and the real file-backed store:
//
//   governed review -> release -> calculation -> persisted record ->
//   restart -> reload -> pinned revision -> traceability
//
// The reviewer here is a test principal, and that is not a fabrication: the
// review service records whoever is actually signed in, so in a test it
// honestly records the test principal. What it can no longer do is let a
// caller name somebody else. Releasing the shipped seed corpus remains a
// human action nobody has performed.
[Collection("Console output capture")]
public class GovernedBracketCheckTests
{
    private const string ReviewerId = "test-reviewer-01";

    private static readonly DateTimeOffset FixedNow = new(2026, 9, 7, 9, 0, 0, TimeSpan.Zero);

    // The bracket, in the units an engineer would state it in.
    private static GovernedBracketCheckRequest Request(string materialId, double loadNewtons = 12_000.0) =>
        new(materialId,
            new Quantity<Force>(loadNewtons, ForceUnits.Newton),
            new Quantity<Area>(60.0, AreaUnits.SquareMillimetre),
            new Quantity<Length>(150.0, LengthUnits.Millimetre),
            new Quantity<Mass>(0.050, MassUnits.Kilogram));

    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();

        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());
            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            await body(host);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static IMaterialCatalog Materials(ITempestHost host) =>
        (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;

    private static ICalculationEngine Engine(ITempestHost host) =>
        (ICalculationEngine)host.Services!.GetService(typeof(ICalculationEngine))!;

    private static (ReferenceReviewService Review, CurrentPrincipalAccessor Principals) SignedInReviewer()
    {
        var principals = new CurrentPrincipalAccessor();
        principals.SetCurrent(new PlatformPrincipal(new PlatformIdentity(ReviewerId, ReviewerId), []));

        return (new ReferenceReviewService(principals, new FixedTimeProvider(FixedNow)), principals);
    }

    private static async Task SeedAsync(ITempestHost host)
    {
        var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService))!;
        await seeder.ApplyAsync(Materials(host), MaterialSeed.Instance);
    }

    /// <summary>Signs a reviewer in, verifies the material against its source, and releases it.</summary>
    private static async Task<IReferenceRecord<MaterialDefinition>> ReviewAndReleaseAsync(
        ITempestHost host, string materialId)
    {
        var (review, _) = SignedInReviewer();

        await review.VerifyAsync(
            Materials(host),
            materialId,
            new ReferenceReviewStatement(
                "Aalco technical datasheet, mechanical and physical property tables",
                "Values compared field by field against the published datasheet."));

        return await review.ReleaseAsync(Materials(host), materialId, "Required for the bracket section check.");
    }

    // ---- The governed release path ----

    [Fact]
    public async Task AReviewRecordsTheSignedInPrincipal_NotANameAnyoneChose()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);

            var released = await ReviewAndReleaseAsync(host, MaterialSeed.Aluminium6082T6);

            Assert.Equal(ReferenceValidationState.Released, released.ValidationState);
            Assert.Equal(ReferenceVerificationStatus.VerifiedAgainstSource, released.Provenance.VerificationStatus);
            Assert.Equal(ReviewerId, released.Provenance.ReviewerPrincipalId);
            Assert.Equal(new DateOnly(2026, 9, 7), released.Provenance.VerificationDate);

            // What the reviewer said they checked against is on the record,
            // verbatim, so a later reader knows what was compared.
            Assert.Contains("Aalco technical datasheet", released.Provenance.Notes!, StringComparison.Ordinal);
            Assert.Contains($"principal '{ReviewerId}'", released.Provenance.Notes!, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task NobodySignedIn_MeansNoReview()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);

            var anonymous = new ReferenceReviewService(new CurrentPrincipalAccessor(), new FixedTimeProvider(FixedNow));

            var refused = await Assert.ThrowsAsync<ReferenceReviewException>(
                () => anonymous.VerifyAsync(
                    Materials(host),
                    MaterialSeed.Aluminium6082T6,
                    new ReferenceReviewStatement("Something")));

            Assert.Contains("no principal is signed in", refused.Reason, StringComparison.Ordinal);

            // And the record is untouched.
            var record = await Materials(host).FindAsync(MaterialSeed.Aluminium6082T6);
            Assert.Equal(ReferenceValidationState.Draft, record!.ValidationState);
            Assert.Null(record.Provenance.ReviewerPrincipalId);
        });
    }

    [Fact]
    public async Task ReleaseWithoutVerificationIsRefused()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            var (review, _) = SignedInReviewer();

            var refused = await Assert.ThrowsAsync<ReferenceReviewException>(
                () => review.ReleaseAsync(Materials(host), MaterialSeed.Aluminium6082T6, "Because I want to."));

            Assert.Contains("has not been verified", refused.Reason, StringComparison.Ordinal);
        });
    }

    // ---- The calculation, through governed data ----

    [Fact]
    public async Task TheGovernedCheckProducesTheHandCalculatedResult()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            await ReviewAndReleaseAsync(host, MaterialSeed.Aluminium6082T6);

            var service = new GovernedBracketCheckService(Materials(host), Engine(host));
            var check = await service.CheckAsync(Request(MaterialSeed.Aluminium6082T6));

            Assert.True(check.WasPerformed);
            Assert.Equal(BracketCheckRefusal.None, check.Refusal);

            var result = check.Result!;

            // The same numbers derived by hand in BracketSectionCheckTests,
            // now reached from the real 6082-T6 record's own 260 MPa and
            // 2.70 g/cm3 rather than from literals in a test.
            Assert.Equal(200.0, result.AppliedStress.ConvertTo(PressureUnits.Megapascal).Value, 1e-9);
            Assert.Equal(260.0, result.AllowableStress.ConvertTo(PressureUnits.Megapascal).Value, 1e-9);
            Assert.Equal(0.30, result.StressMargin, 1e-9);
            Assert.Equal(0.0243, result.EstimatedMass.ConvertTo(MassUnits.Kilogram).Value, 1e-9);
            Assert.Equal(BracketCheckOutcome.MeetsCriteria, result.Outcome);

            // And the record says which revision of which record it stood on.
            var material = await Materials(host).FindAsync(MaterialSeed.Aluminium6082T6);
            Assert.Equal(material!.RevisionNumber, result.MaterialPin.RevisionNumber);
            Assert.Equal("Materials", result.MaterialPin.Library);
        });
    }

    [Fact]
    public async Task TheCalculationRecordCarriesItsAssumptionsAndWorkingAndAuthor()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            await ReviewAndReleaseAsync(host, MaterialSeed.Aluminium6082T6);

            var check = await new GovernedBracketCheckService(Materials(host), Engine(host))
                .CheckAsync(Request(MaterialSeed.Aluminium6082T6));

            var record = check.Record!;

            Assert.Equal(BracketSectionCheckCalculationDefinition.Id, record.CalculationId);
            Assert.Equal(5, record.Assumptions.Count);
            Assert.Contains(record.IntermediateResults, i => i.Name == "Stress margin of safety");
            Assert.Contains(MaterialSeed.Aluminium6082T6, record.ReferencedMaterialIds);
            Assert.Equal(CalculationValidationOutcome.Valid, record.Validation.Outcome);
            Assert.False(string.IsNullOrWhiteSpace(record.ExecutedByPrincipalId));
            Assert.NotEqual(default, record.ExecutedAt);

            // The provenance of the data it used comes back with the check.
            Assert.Equal("Aalco Metals Limited", check.MaterialProvenance!.SourceOrganisation);
        });
    }

    [Fact]
    public async Task AnUnmetCriterionMakesTheRecordConditional_NotInvalidAndNotApproved()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            await ReviewAndReleaseAsync(host, MaterialSeed.Aluminium6082T6);

            var check = await new GovernedBracketCheckService(Materials(host), Engine(host))
                .CheckAsync(Request(MaterialSeed.Aluminium6082T6, loadNewtons: 18_000.0));

            Assert.True(check.WasPerformed);
            Assert.Equal(BracketCheckOutcome.DoesNotMeetCriteria, check.Result!.Outcome);
            Assert.Equal(CalculationValidationOutcome.Conditional, check.Record!.Validation.Outcome);

            var failed = Assert.Single(check.Record.Validation.ConstraintChecks, c => !c.IsSatisfied);
            Assert.Contains("Applied stress must not exceed", failed.Description, StringComparison.Ordinal);
        });
    }

    // ---- Persistence, restart and revision pinning ----

    [Fact]
    public async Task TheCalculationSurvivesARestartWithEverythingIntact()
    {
        using var temp = new TempDirectory();
        Guid recordId = Guid.Empty;
        int pinnedRevision = 0;

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            await ReviewAndReleaseAsync(host, MaterialSeed.Aluminium6082T6);

            var check = await new GovernedBracketCheckService(Materials(host), Engine(host))
                .CheckAsync(Request(MaterialSeed.Aluminium6082T6));

            recordId = check.Record!.Id;
            pinnedRevision = check.Result!.MaterialPin.RevisionNumber;
        });

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var reloaded = await Engine(host).FindRecordAsync<BracketSectionCheckResult>(recordId);

            Assert.NotNull(reloaded);
            Assert.Equal(BracketSectionCheckCalculationDefinition.Id, reloaded!.CalculationId);

            // Result, with its units.
            Assert.Equal(200.0, reloaded.Result.AppliedStress.ConvertTo(PressureUnits.Megapascal).Value, 1e-9);
            Assert.Equal("MPa", reloaded.Result.AppliedStress.Unit.Symbol);
            Assert.Equal(0.30, reloaded.Result.StressMargin, 1e-9);
            Assert.Equal(0.0243, reloaded.Result.EstimatedMass.ConvertTo(MassUnits.Kilogram).Value, 1e-9);
            Assert.Equal(BracketCheckOutcome.MeetsCriteria, reloaded.Result.Outcome);
            Assert.True(reloaded.Result.StressCriterionMet);

            // Reference identity and revision.
            Assert.Equal(MaterialSeed.Aluminium6082T6, reloaded.Result.MaterialPin.RecordId);
            Assert.Equal(pinnedRevision, reloaded.Result.MaterialPin.RevisionNumber);

            // Assumptions, working, author, timestamp, revision.
            Assert.Equal(5, reloaded.Assumptions.Count);
            Assert.Contains(reloaded.IntermediateResults, i => i.Name == "Estimated mass (kg)");
            Assert.Contains(MaterialSeed.Aluminium6082T6, reloaded.ReferencedMaterialIds);
            Assert.False(string.IsNullOrWhiteSpace(reloaded.ExecutedByPrincipalId));
            Assert.NotEqual(default, reloaded.ExecutedAt);
            Assert.Equal(1, reloaded.RevisionNumber);
            Assert.Equal(CalculationValidationOutcome.Valid, reloaded.Validation.Outcome);
        });
    }

    [Fact]
    public async Task RevisingTheMaterialDoesNotChangeTheHistoricalCalculation()
    {
        using var temp = new TempDirectory();
        Guid recordId = Guid.Empty;
        int pinnedRevision = 0;

        // 1-2. Calculate against revision N and persist it.
        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            await ReviewAndReleaseAsync(host, MaterialSeed.Aluminium6082T6);

            var check = await new GovernedBracketCheckService(Materials(host), Engine(host))
                .CheckAsync(Request(MaterialSeed.Aluminium6082T6));

            recordId = check.Record!.Id;
            pinnedRevision = check.Result!.MaterialPin.RevisionNumber;
            Assert.Equal(0.30, check.Result.StressMargin, 1e-9);
        });

        // 3. Supersede the released record with a corrected one.
        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var materials = Materials(host);
            var released = await materials.FindAsync(MaterialSeed.Aluminium6082T6);

            // A released record cannot be edited, so the correction is a new
            // record that supersedes it — exactly as the lifecycle intends.
            var corrected = released!.Definition.Properties.ToDictionary(p => p.Key, p => p.Value);
            corrected[MaterialPropertyNames.YieldStrength] = new ReferenceQuantityValue(
                new Quantity<Pressure>(240.0, PressureUnits.Megapascal),
                ReferenceValueOrigin.Standard,
                "Corrected on review against EN 755-2.");

            await materials.RegisterAsync(
                "mat-6082-t6-rev-b",
                released.Definition with { Designation = "6082 (rev B)", Properties = corrected },
                released.Provenance with
                {
                    VerificationStatus = ReferenceVerificationStatus.NotVerified,
                    ReviewerPrincipalId = null,
                    VerificationDate = null,
                });

            await materials.SupersedeAsync(
                MaterialSeed.Aluminium6082T6, "mat-6082-t6-rev-b", "Proof stress corrected to 240 MPa.");
        });

        // 4-6. Reload the old calculation. It still points at revision N and
        //      still reads the numbers it was performed with.
        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var reloaded = await Engine(host).FindRecordAsync<BracketSectionCheckResult>(recordId);

            Assert.Equal(pinnedRevision, reloaded!.Result.MaterialPin.RevisionNumber);
            Assert.Equal(260.0, reloaded.Result.AllowableStress.ConvertTo(PressureUnits.Megapascal).Value, 1e-9);
            Assert.Equal(0.30, reloaded.Result.StressMargin, 1e-9);

            // The pinned revision is still readable from the catalogue, so
            // the calculation can be reproduced from first principles and
            // not merely re-read from its own record.
            var asUsed = await Materials(host).GetRevisionAsync(
                MaterialSeed.Aluminium6082T6, reloaded.Result.MaterialPin.RevisionNumber);

            Assert.Equal(
                260e6,
                asUsed.Definition.Properties[MaterialPropertyNames.YieldStrength].CanonicalValue,
                0);

            // Meanwhile the live library has moved on.
            var superseded = await Materials(host).FindAsync(MaterialSeed.Aluminium6082T6);
            Assert.Equal(ReferenceValidationState.Superseded, superseded!.ValidationState);
            Assert.Equal("mat-6082-t6-rev-b", superseded.SupersededByRecordId);
        });
    }

    // ---- Refusals ----

    [Fact]
    public async Task ADraftMaterialIsRefused()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);

            var check = await new GovernedBracketCheckService(Materials(host), Engine(host))
                .CheckAsync(Request(MaterialSeed.Aluminium6082T6));

            Assert.False(check.WasPerformed);
            Assert.Equal(BracketCheckRefusal.MaterialNotReleased, check.Refusal);
            Assert.Contains("not Released", check.Reason!, StringComparison.Ordinal);
            Assert.Null(check.Record);

            // The refusal still says which record it was talking about.
            Assert.Equal(MaterialSeed.Aluminium6082T6, check.MaterialPin!.RecordId);
        });
    }

    [Fact]
    public async Task AMissingRequiredPropertyIsRefused_AndNothingIsSubstituted()
    {
        // 5083's source publishes an impossible density, and the population
        // phase omitted it rather than inventing 2.65 g/cm3. This is where
        // that decision earns its keep: the calculation refuses instead of
        // producing a mass nobody can stand behind.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            await ReviewAndReleaseAsync(host, MaterialSeed.Aluminium5083OH111);

            var check = await new GovernedBracketCheckService(Materials(host), Engine(host))
                .CheckAsync(Request(MaterialSeed.Aluminium5083OH111));

            Assert.False(check.WasPerformed);
            Assert.Equal(BracketCheckRefusal.RequiredPropertyMissing, check.Refusal);
            Assert.Contains("records no Density", check.Reason!, StringComparison.Ordinal);
            Assert.Contains("substituting a typical figure", check.Reason!, StringComparison.Ordinal);
            Assert.Null(check.Record);

            // The record really is still without a density.
            var material = await Materials(host).FindAsync(MaterialSeed.Aluminium5083OH111);
            Assert.False(material!.Definition.Properties.ContainsKey(MaterialPropertyNames.Density));
        });
    }

    [Fact]
    public async Task AMaterialThatDoesNotExistIsRefused()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);

            var check = await new GovernedBracketCheckService(Materials(host), Engine(host))
                .CheckAsync(Request("mat-unicorn"));

            Assert.Equal(BracketCheckRefusal.MaterialNotFound, check.Refusal);
            Assert.Null(check.MaterialPin);
        });
    }

    [Fact]
    public async Task ASupersededMaterialIsRefused()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            await ReviewAndReleaseAsync(host, MaterialSeed.Aluminium6082T6);
            var original = await Materials(host).FindAsync(MaterialSeed.Aluminium6082T6);

            await Materials(host).RegisterAsync(
                "mat-6082-t6-rev-b",
                original!.Definition with { Designation = "6082 (rev B)" },
                original.Provenance);

            await Materials(host).SupersedeAsync(
                MaterialSeed.Aluminium6082T6, "mat-6082-t6-rev-b", "Superseded.");

            var check = await new GovernedBracketCheckService(Materials(host), Engine(host))
                .CheckAsync(Request(MaterialSeed.Aluminium6082T6));

            Assert.Equal(BracketCheckRefusal.MaterialNotReleased, check.Refusal);
            Assert.Contains("Superseded", check.Reason!, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task APropertyOfTheWrongDimensionIsRefused()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);

            var record = await Materials(host).FindAsync(MaterialSeed.Aluminium6082T6);

            // A yield strength recorded as a length. Numerically usable,
            // physically meaningless.
            var broken = record!.Definition.Properties.ToDictionary(p => p.Key, p => p.Value);
            broken[MaterialPropertyNames.YieldStrength] = new ReferenceQuantityValue(
                new Quantity<Length>(260.0, LengthUnits.Millimetre),
                ReferenceValueOrigin.EngineeringReference,
                "FICTIONAL TEST FIXTURE: a deliberately wrong dimension.");

            await Materials(host).ReviseAsync(
                MaterialSeed.Aluminium6082T6,
                record.Definition with { Properties = broken },
                record.Provenance,
                "Test fixture: wrong dimension on yield strength.");

            await ReviewAndReleaseAsync(host, MaterialSeed.Aluminium6082T6);

            var check = await new GovernedBracketCheckService(Materials(host), Engine(host))
                .CheckAsync(Request(MaterialSeed.Aluminium6082T6));

            Assert.Equal(BracketCheckRefusal.PropertyDimensionWrong, check.Refusal);
            Assert.Contains("records YieldStrength as a Length", check.Reason!, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task AMalformedPersistedCalculationIsReported_NotReturnedAsMissing()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedAsync(host);
            await ReviewAndReleaseAsync(host, MaterialSeed.Aluminium6082T6);

            var check = await new GovernedBracketCheckService(Materials(host), Engine(host))
                .CheckAsync(Request(MaterialSeed.Aluminium6082T6));

            // Reading it back as the wrong result type is the same failure
            // shape as a corrupted document, and must not look like "no such
            // calculation" — a caller would move past that silently.
            var wrongType = await Assert.ThrowsAsync<CalculationException>(
                () => Engine(host).FindRecordAsync<BoltShearCapacityResult>(check.Record!.Id));

            Assert.Contains(check.Record.Id.ToString(), wrongType.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task AnUnknownCalculationRecordIdIsSimplyAbsent()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            Assert.Null(await Engine(host).FindRecordAsync<BracketSectionCheckResult>(Guid.NewGuid()));
        });
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
