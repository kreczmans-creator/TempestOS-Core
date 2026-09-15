using Tempest.Core.Calculations;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Tests.Population;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations;

// The complete demonstration, in one place, in the order an engineer would
// work through it:
//
//   governed reference -> release -> inputs -> calculation -> result ->
//   acceptance -> independent verification -> persisted artefacts ->
//   traceability to source
//
// The independent figures below were derived by hand, from first
// principles, and are stated here so the verification is genuinely
// independent of the implementation rather than a second call to it:
//
//   A = 60 mm2 = 6.0e-5 m2, L = 150 mm = 0.15 m, F = 12 kN
//   6082-T6: allowable 260 MPa, density 2700 kg/m3
//
//   sigma  = 12 000 / 6.0e-5 = 2.0e8 Pa = 200 MPa
//   margin = 260/200 - 1                = 0.30
//   mass   = 2700 * 6.0e-5 * 0.15       = 0.0243 kg
public class BracketEngineeringDemonstrationTests
{
    private const string ReviewerId = "test-reviewer-01";
    private const string CheckerId = "test-independent-checker-02";

    private const double IndependentMargin = 0.30;
    private const double IndependentMassKilograms = 0.0243;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Harness : SeedHarness
    {
        public CurrentPrincipalAccessor Principals { get; } = new();

        public ReferenceReviewService Review => new(
            Principals, new FixedTimeProvider(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero)));

        public CalculationEngine Engine { get; private set; } = null!;

        public GovernedBracketCheckService Check { get; private set; } = null!;

        public BracketEngineeringRecordService Records { get; private set; } = null!;

        public async Task PrepareAsync()
        {
            Principals.SetCurrent(new PlatformPrincipal(new PlatformIdentity(ReviewerId, ReviewerId), []));

            Engine = new CalculationEngine((Tempest.Core.EngineeringData.IEngineeringDocumentStore)DocumentStore, Principals);
            Check = new GovernedBracketCheckService(Materials, Engine);
            Records = new BracketEngineeringRecordService(CalculationPacks, VerificationArtefacts);

            await SeedEverythingAsync();

            await Review.VerifyAsync(
                Materials,
                MaterialSeed.Aluminium6082T6,
                new ReferenceReviewStatement(
                    "Aalco technical datasheet — Aluminium Alloy - Commercial Alloy - 6082 - T6 Extrusions, "
                    + "mechanical properties (rod and bar 20 mm to 150 mm) and physical properties tables"));

            await Review.ReleaseAsync(
                Materials, MaterialSeed.Aluminium6082T6, "Required for the bracket section check.");
        }
    }

    private static GovernedBracketCheckRequest BracketRequest() =>
        new(MaterialSeed.Aluminium6082T6,
            new Quantity<Force>(12.0, ForceUnits.Kilonewton),
            new Quantity<Area>(60.0, AreaUnits.SquareMillimetre),
            new Quantity<Length>(150.0, LengthUnits.Millimetre),
            new Quantity<Mass>(50.0, MassUnits.Gram));

    [Fact]
    public async Task TheCompleteChain_FromGovernedReferenceToTraceableVerifiedResult()
    {
        var harness = new Harness();
        await harness.PrepareAsync();

        // --- 1. Governed reference, released by a signed-in reviewer ---
        var material = await harness.Materials.FindAsync(MaterialSeed.Aluminium6082T6);
        Assert.Equal(ReferenceValidationState.Released, material!.ValidationState);
        Assert.Equal(ReviewerId, material.Provenance.ReviewerPrincipalId);

        // --- 2. Calculation over that reference ---
        var check = await harness.Check.CheckAsync(BracketRequest());
        Assert.True(check.WasPerformed);

        var result = check.Result!;

        // --- 3. The numerical result, matching the hand calculation ---
        Assert.Equal(200.0, result.AppliedStress.ConvertTo(PressureUnits.Megapascal).Value, 1e-9);
        Assert.Equal(IndependentMargin, result.StressMargin, 1e-9);
        Assert.Equal(IndependentMassKilograms, result.EstimatedMass.ConvertTo(MassUnits.Kilogram).Value, 1e-9);

        // --- 4. Acceptance, as a finding and not an approval ---
        Assert.True(result.StressCriterionMet);
        Assert.True(result.MassCriterionMet);
        Assert.Equal(BracketCheckOutcome.MeetsCriteria, result.Outcome);
        Assert.DoesNotContain(
            "Approved",
            Enum.GetNames<BracketCheckOutcome>().Aggregate(string.Concat),
            StringComparison.OrdinalIgnoreCase);

        // --- 5. Independent verification ---
        var artefact = await harness.Records.RecordVerificationAsync(
            EngineeringAssetSeed.VerificationRecordId,
            check,
            new IndependentCheck(
                "Hand calculation from first principles: sigma = F/A = 12 000 N / 6.0e-5 m2 = 200 MPa; "
                + "margin = 260/200 - 1 = 0.30; mass = 2700 kg/m3 x 6.0e-5 m2 x 0.15 m = 0.0243 kg.",
                IndependentMargin,
                new Quantity<Mass>(IndependentMassKilograms, MassUnits.Kilogram)),
            CheckerId,
            new DateOnly(2026, 9, 7),
            calculationPackReference: "TDE-CPK-001");

        Assert.Equal(VerificationStanding.Passed, artefact.Definition.Result!.Standing);
        Assert.Equal(CheckerId, artefact.Definition.Result.PerformedByPrincipalId);
        Assert.Equal(check.Record!.Id, artefact.Definition.Result.VerificationRecordId);
        Assert.Contains("agrees with", artefact.Definition.Result.Summary, StringComparison.Ordinal);

        // The verifier is a different principal from the reviewer.
        Assert.NotEqual(material.Provenance.ReviewerPrincipalId, artefact.Definition.Result.PerformedByPrincipalId);

        // --- 6. Persisted engineering artefact ---
        var pack = await harness.Records.RecordCalculationAsync(
            EngineeringAssetSeed.CalculationPackRecordId, check);

        Assert.Contains(check.Record.Id, pack.Definition.ExecutionRecordIds);
        Assert.Contains(pack.Definition.Outputs, o => o.Reference == "OUT-MARGIN" && o.Value.StartsWith("0.3", StringComparison.Ordinal));
        Assert.Contains(pack.Definition.Outputs, o => o.Reference == "OUT-STRESS" && o.Value == "200 MPa");
        Assert.Contains(pack.Definition.Outputs, o => o.Reference == "OUT-MASS" && o.Value == "0.0243 kg");
        Assert.DoesNotContain(pack.Definition.Inputs, i => i.Value == "not established");
        Assert.DoesNotContain(pack.Definition.Limitations, l => l.StartsWith("No result.", StringComparison.Ordinal));

        // The acceptance comparison is recorded next to the value, and the
        // wording stops short of approval.
        var margin = pack.Definition.Outputs.Single(o => o.Reference == "OUT-MARGIN");
        Assert.False(string.IsNullOrWhiteSpace(margin.AcceptanceCriterion));
        Assert.Contains("not design approval", margin.Interpretation!, StringComparison.Ordinal);

        // --- 7. Traceability, from the result back to the source document ---
        var pin = result.MaterialPin;
        var asUsed = await harness.Materials.GetRevisionAsync(pin.RecordId, pin.RevisionNumber);

        Assert.Equal("Aalco Metals Limited", asUsed.Provenance.SourceOrganisation);
        Assert.Contains("6082 - T6 Extrusions", asUsed.Provenance.SourceDocument);
        Assert.Contains("20 mm to 150 mm band", asUsed.Provenance.SourceLocation);

        // And the number the calculation used really is the number on that
        // revision of that record.
        Assert.Equal(
            260e6,
            asUsed.Definition.Properties[MaterialPropertyNames.YieldStrength].CanonicalValue,
            0);

        // The pack input points at the same pin, so the chain is closed from
        // either end.
        var materialInput = pack.Definition.Inputs.Single(i => i.Reference == "IN-MATERIAL");
        Assert.Equal(pin, materialInput.SourcePin);

        // The density input states the material's own figure rather than
        // being reconstructed by dividing the mass back out.
        var densityInput = pack.Definition.Inputs.Single(i => i.Reference == "IN-DENSITY");
        Assert.Equal("2700 kg/m3", densityInput.Value);
        Assert.Equal(pin, densityInput.SourcePin);

        Assert.Equal("TDE-CPK-001", artefact.Definition.Result.CalculationPackReference);
    }

    [Fact]
    public async Task AnIndependentCheckThatDisagreesIsRecordedAsAFailure()
    {
        // The verification must be capable of failing, or it verifies
        // nothing. Here the independent checker gets a different margin —
        // as they would if they had used the wrong size band's proof stress.
        var harness = new Harness();
        await harness.PrepareAsync();

        var check = await harness.Check.CheckAsync(BracketRequest());

        var artefact = await harness.Records.RecordVerificationAsync(
            EngineeringAssetSeed.VerificationRecordId,
            check,
            new IndependentCheck(
                "Hand calculation using the 200 to 250 mm bar proof stress of 200 MPa by mistake: "
                + "margin = 200/200 - 1 = 0.00.",
                0.00,
                new Quantity<Mass>(IndependentMassKilograms, MassUnits.Kilogram)),
            CheckerId,
            new DateOnly(2026, 9, 7));

        Assert.Equal(VerificationStanding.Failed, artefact.Definition.Result!.Standing);
        Assert.Contains("DISAGREES", artefact.Definition.Result.Summary, StringComparison.Ordinal);
        Assert.Contains("differs", artefact.Definition.Result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusedCheckCannotBeWrittenIntoAnEngineeringRecord()
    {
        // 5083 has no density, so there is no result. Nothing may be written
        // into the pack or the verification artefact on the strength of it.
        var harness = new Harness();
        await harness.PrepareAsync();

        await harness.Review.VerifyAsync(
            harness.Materials,
            MaterialSeed.Aluminium5083OH111,
            new ReferenceReviewStatement("Aalco 5083 datasheet"));
        await harness.Review.ReleaseAsync(harness.Materials, MaterialSeed.Aluminium5083OH111, "For comparison.");

        var refused = await harness.Check.CheckAsync(
            BracketRequest() with { MaterialRecordId = MaterialSeed.Aluminium5083OH111 });

        Assert.Equal(BracketCheckRefusal.RequiredPropertyMissing, refused.Refusal);

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Records.RecordCalculationAsync(EngineeringAssetSeed.CalculationPackRecordId, refused));

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Records.RecordVerificationAsync(
                EngineeringAssetSeed.VerificationRecordId,
                refused,
                new IndependentCheck("Nothing to check.", 0, new Quantity<Mass>(0, MassUnits.Kilogram)),
                CheckerId,
                new DateOnly(2026, 9, 7)));

        // The seeded artefact is untouched: still NotPerformed.
        var artefact = await harness.VerificationArtefacts.FindAsync(EngineeringAssetSeed.VerificationRecordId);
        Assert.Null(artefact!.Definition.Result);
        Assert.Equal(VerificationStanding.NotPerformed, artefact.Definition.Standing);
    }

    // ---- Every input's own Dimension and SourceDescription, not only its value ----

    [Fact]
    public async Task TheInputsRecordTheirOwnDimensionAndSourceDescription()
    {
        var harness = new Harness();
        await harness.PrepareAsync();

        var check = await harness.Check.CheckAsync(BracketRequest());
        var pack = await harness.Records.RecordCalculationAsync(EngineeringAssetSeed.CalculationPackRecordId, check);

        var material = pack.Definition.Inputs.Single(i => i.Reference == "IN-MATERIAL");
        Assert.Equal(nameof(Pressure), material.Dimension);
        Assert.Contains("YieldStrength property", material.SourceDescription, StringComparison.Ordinal);

        var load = pack.Definition.Inputs.Single(i => i.Reference == "IN-LOAD");
        Assert.Equal(nameof(Force), load.Dimension);
        Assert.Equal("Supplied with the check request.", load.SourceDescription);

        var area = pack.Definition.Inputs.Single(i => i.Reference == "IN-AREA");
        Assert.Equal(nameof(Area), area.Dimension);

        var length = pack.Definition.Inputs.Single(i => i.Reference == "IN-LENGTH");
        Assert.Equal(nameof(Length), length.Dimension);

        var density = pack.Definition.Inputs.Single(i => i.Reference == "IN-DENSITY");
        Assert.Equal(nameof(MassDensity), density.Dimension);
        Assert.Contains("Density property", density.SourceDescription, StringComparison.Ordinal);

        // OUT-STRESS's own Dimension and Interpretation.
        var stressOutput = pack.Definition.Outputs.Single(o => o.Reference == "OUT-STRESS");
        Assert.Equal(nameof(Pressure), stressOutput.Dimension);
        Assert.Equal("Applied stress, F / A.", stressOutput.Interpretation);

        var massOutput = pack.Definition.Outputs.Single(o => o.Reference == "OUT-MASS");
        Assert.Equal(nameof(Mass), massOutput.Dimension);
        Assert.Equal("Meets the mass criterion.", massOutput.Interpretation);
    }

    // ---- The "does not meet" interpretation branch — the nominal
    //      scenario above only ever exercises "meets". ----

    [Fact]
    public async Task BothCriteriaUnmet_RecordsTheDoesNotMeetInterpretation_ForBothOutputs()
    {
        var harness = new Harness();
        await harness.PrepareAsync();

        // A far heavier load fails the stress criterion; a tiny mass limit
        // fails the mass criterion too, so both branches fire together.
        var failingRequest = BracketRequest() with
        {
            AppliedLoad = new Quantity<Force>(100.0, ForceUnits.Kilonewton),
            MassLimit = new Quantity<Mass>(0.001, MassUnits.Kilogram),
        };

        var check = await harness.Check.CheckAsync(failingRequest);
        Assert.True(check.WasPerformed);
        Assert.False(check.Result!.StressCriterionMet);
        Assert.False(check.Result.MassCriterionMet);

        var pack = await harness.Records.RecordCalculationAsync(EngineeringAssetSeed.CalculationPackRecordId, check);

        var margin = pack.Definition.Outputs.Single(o => o.Reference == "OUT-MARGIN");
        Assert.Equal("Does not meet the stress criterion.", margin.Interpretation);

        var mass = pack.Definition.Outputs.Single(o => o.Reference == "OUT-MASS");
        Assert.Equal("Does not meet the mass criterion.", mass.Interpretation);
        Assert.StartsWith("At or below 0.001 kg", mass.AcceptanceCriterion, StringComparison.Ordinal);
    }

    // ---- Independent disagreement on mass alone — the existing
    //      "disagrees" test disagrees on margin only, so the mass-specific
    //      half of the summary's own wording was never reached. ----

    [Fact]
    public async Task AnIndependentCheckThatAgreesOnMarginButDisagreesOnMass_RecordsBothWordingsSeparately()
    {
        var harness = new Harness();
        await harness.PrepareAsync();

        var check = await harness.Check.CheckAsync(BracketRequest());

        var artefact = await harness.Records.RecordVerificationAsync(
            EngineeringAssetSeed.VerificationRecordId,
            check,
            new IndependentCheck(
                "Hand calculation agreeing on margin but using the wrong length for mass.",
                IndependentMargin,
                new Quantity<Mass>(999.0, MassUnits.Kilogram)),
            CheckerId,
            new DateOnly(2026, 9, 7));

        Assert.Equal(VerificationStanding.Failed, artefact.Definition.Result!.Standing);
        Assert.Contains("DISAGREES", artefact.Definition.Result.Summary, StringComparison.Ordinal);
        Assert.Contains("(agrees)", artefact.Definition.Result.Summary, StringComparison.Ordinal);
        Assert.Contains("(differs)", artefact.Definition.Result.Summary, StringComparison.Ordinal);
    }

    // ---- The near-zero absolute-tolerance fallback in Agrees() — a
    //      relative comparison is meaningless once both figures are
    //      themselves within 1e-12 of zero. ----

    [Fact]
    public async Task WhenBothMarginsAreNearZero_TheComparisonFallsBackToAnAbsoluteOne()
    {
        var harness = new Harness();
        await harness.PrepareAsync();

        // The exact stress-allowable boundary load: the computed margin is
        // a rounding artefact a few ulps from zero (see
        // BracketSectionCheckTests.AtExactlyTheAllowableStress), well
        // inside the 1e-12 absolute-fallback threshold.
        var boundaryRequest = BracketRequest() with { AppliedLoad = new Quantity<Force>(15_600.0, ForceUnits.Newton) };

        var check = await harness.Check.CheckAsync(boundaryRequest);
        Assert.True(Math.Abs(check.Result!.StressMargin) < 1e-12);

        var artefact = await harness.Records.RecordVerificationAsync(
            EngineeringAssetSeed.VerificationRecordId,
            check,
            new IndependentCheck(
                "Hand calculation: margin is zero at the boundary.",
                0.0,
                new Quantity<Mass>(check.Result.EstimatedMass.ConvertTo(MassUnits.Kilogram).Value, MassUnits.Kilogram)),
            CheckerId,
            new DateOnly(2026, 9, 7));

        Assert.Equal(VerificationStanding.Passed, artefact.Definition.Result!.Standing);
    }

    // ---- Agrees()'s own scale: the LARGER of the two magnitudes, not the
    //      smaller — a near-zero independent figure must not pull a
    //      genuinely-different, normal-scale calculated one into the
    //      near-zero absolute fallback. ----

    [Fact]
    public async Task WhenOnlyTheIndependentFigureIsNearZero_TheComparisonStillUsesTheCalculatedFiguresScale()
    {
        var harness = new Harness();
        await harness.PrepareAsync();

        // 260 MPa (the governed 6082-T6 record's own allowable, fixed by
        // the seed data) divided by an applied stress a hair below it
        // gives a margin of a few hundred parts per billion — not near
        // zero by the 1e-12 threshold, but small.
        const double allowablePascals = 260_000_000.0;
        var appliedPascals = allowablePascals / 1.0000002;

        var request = BracketRequest() with
        {
            AppliedLoad = new Quantity<Force>(appliedPascals, ForceUnits.Newton),
            SectionArea = new Quantity<Area>(1.0, AreaUnits.SquareMetre),
        };

        var check = await harness.Check.CheckAsync(request);
        var calculatedMargin = check.Result!.StressMargin;
        Assert.True(Math.Abs(calculatedMargin) is > 1e-9 and < 1e-3, $"Test setup: margin was {calculatedMargin}.");

        // The independent figure is exactly zero — near zero by the
        // threshold — but the calculated one is not, so the scale Agrees()
        // compares against must be the calculated figure's own magnitude,
        // not zero. Using the smaller of the two (zero) would wrongly pull
        // this into the absolute fallback and call a genuine, order-of-
        // magnitude relative disagreement an agreement.
        // The mass side must agree independently of the margin side under
        // test here, so it is read back from the same result rather than
        // reused from the nominal scenario's own unrelated figure (the
        // 1 m2 section above gives a very different mass).
        var agreeingMass = check.Result.EstimatedMass.ConvertTo(MassUnits.Kilogram).Value;

        var artefact = await harness.Records.RecordVerificationAsync(
            EngineeringAssetSeed.VerificationRecordId,
            check,
            new IndependentCheck("Hand calculation: independently, the margin rounds to zero.", 0.0, new Quantity<Mass>(agreeingMass, MassUnits.Kilogram)),
            CheckerId,
            new DateOnly(2026, 9, 7));

        Assert.Equal(VerificationStanding.Failed, artefact.Definition.Result!.Standing);
    }

    // ---- Agrees()'s own relative comparison at normal scale must not be
    //      rejected by an absolute one — the near-zero fallback is for
    //      near-zero figures only. ----

    [Fact]
    public async Task ARelativeAgreement_AtNormalScale_IsNotRejectedByAnAbsoluteComparison()
    {
        var harness = new Harness();
        await harness.PrepareAsync();

        // Applied stress a thousandth of the allowable gives a margin
        // around 999 — comfortably "normal scale" (nowhere near the
        // 1e-12 near-zero threshold).
        const double allowablePascals = 260_000_000.0;
        var appliedPascals = allowablePascals / 1000.0;

        var request = BracketRequest() with
        {
            AppliedLoad = new Quantity<Force>(appliedPascals, ForceUnits.Newton),
            SectionArea = new Quantity<Area>(1.0, AreaUnits.SquareMetre),
        };

        var check = await harness.Check.CheckAsync(request);
        var calculatedMargin = check.Result!.StressMargin;
        Assert.True(calculatedMargin > 100.0, $"Test setup: margin was {calculatedMargin}.");

        // 0.0005 off a margin of about 999 is roughly five parts per
        // million relative — inside the default 1e-6 relative tolerance —
        // but an absolute comparison against that same 1e-6 tolerance
        // would reject it outright (0.0005 is nowhere near that small).
        var independentMargin = calculatedMargin + 0.0005;
        var agreeingMass = check.Result.EstimatedMass.ConvertTo(MassUnits.Kilogram).Value;

        var artefact = await harness.Records.RecordVerificationAsync(
            EngineeringAssetSeed.VerificationRecordId,
            check,
            new IndependentCheck("Hand calculation, agreeing to five significant figures.", independentMargin, new Quantity<Mass>(agreeingMass, MassUnits.Kilogram)),
            CheckerId,
            new DateOnly(2026, 9, 7));

        Assert.Equal(VerificationStanding.Passed, artefact.Definition.Result!.Standing);
    }

    // ---- Not-found — refusing to write into a record that does not exist ----

    [Fact]
    public async Task RecordCalculationAsync_UnknownPackRecordId_ThrowsReferenceRecordNotFoundException()
    {
        var harness = new Harness();
        await harness.PrepareAsync();

        var check = await harness.Check.CheckAsync(BracketRequest());

        await Assert.ThrowsAsync<ReferenceRecordNotFoundException>(
            () => harness.Records.RecordCalculationAsync("cpk-does-not-exist", check));
    }

    [Fact]
    public async Task RecordVerificationAsync_UnknownArtefactRecordId_ThrowsReferenceRecordNotFoundException()
    {
        var harness = new Harness();
        await harness.PrepareAsync();

        var check = await harness.Check.CheckAsync(BracketRequest());

        await Assert.ThrowsAsync<ReferenceRecordNotFoundException>(
            () => harness.Records.RecordVerificationAsync(
                "ver-does-not-exist",
                check,
                new IndependentCheck("Basis.", IndependentMargin, new Quantity<Mass>(IndependentMassKilograms, MassUnits.Kilogram)),
                CheckerId,
                new DateOnly(2026, 9, 7)));
    }

    // ---- Null / whitespace argument guards on both write paths ----

    [Fact]
    public async Task RecordCalculationAsync_NullOrWhitespacePackRecordId_ThrowsArgumentException()
    {
        var harness = new Harness();
        var records = new BracketEngineeringRecordService(harness.CalculationPacks, harness.VerificationArtefacts);
        var check = new GovernedBracketCheck(BracketCheckRefusal.None, null, null, null, null, BracketRequest());

        await Assert.ThrowsAsync<ArgumentException>(() => records.RecordCalculationAsync("", check));
        await Assert.ThrowsAsync<ArgumentException>(() => records.RecordCalculationAsync("   ", check));
    }

    [Fact]
    public async Task RecordCalculationAsync_NullCheck_ThrowsArgumentNullException()
    {
        var harness = new Harness();
        var records = new BracketEngineeringRecordService(harness.CalculationPacks, harness.VerificationArtefacts);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => records.RecordCalculationAsync(EngineeringAssetSeed.CalculationPackRecordId, null!));
    }

    [Fact]
    public async Task RecordVerificationAsync_NullArguments_EachThrowArgumentException()
    {
        var harness = new Harness();
        await harness.PrepareAsync();

        var check = await harness.Check.CheckAsync(BracketRequest());
        var independent = new IndependentCheck("Basis.", IndependentMargin, new Quantity<Mass>(IndependentMassKilograms, MassUnits.Kilogram));

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Records.RecordVerificationAsync("", check, independent, CheckerId, new DateOnly(2026, 9, 7)));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => harness.Records.RecordVerificationAsync(EngineeringAssetSeed.VerificationRecordId, null!, independent, CheckerId, new DateOnly(2026, 9, 7)));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => harness.Records.RecordVerificationAsync(EngineeringAssetSeed.VerificationRecordId, check, null!, CheckerId, new DateOnly(2026, 9, 7)));

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Records.RecordVerificationAsync(
                EngineeringAssetSeed.VerificationRecordId,
                check,
                independent with { Basis = "   " },
                CheckerId,
                new DateOnly(2026, 9, 7)));

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Records.RecordVerificationAsync(
                EngineeringAssetSeed.VerificationRecordId, check, independent, "   ", new DateOnly(2026, 9, 7)));
    }
}
