using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Tests.Materials;
using Tempest.Core.UnitsAndQuantities;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Material properties reach the modules through the catalogue, pinned to
/// the released record they came from — never typed in. The reader refuses
/// a missing record, an unreleased one, an absent property and a property of
/// the wrong dimension, each by name.
/// </summary>
public class ModuleMaterialResolutionTests
{
    private const string ReviewerId = "test-reviewer-21-7a";

    private static ReferenceReviewService SignedInReviewer()
    {
        var principals = new CurrentPrincipalAccessor();
        principals.SetCurrent(new PlatformPrincipal(new PlatformIdentity(ReviewerId, ReviewerId), []));
        return new ReferenceReviewService(principals);
    }

    private static async Task<IReferenceRecord<MaterialDefinition>> ReleaseAsync(IMaterialCatalog materials, string recordId)
    {
        var review = SignedInReviewer();
        await review.VerifyAsync(materials, recordId, new ReferenceReviewStatement("Fixture materials handbook, Table 3", "Compared field by field."));
        return await review.ReleaseAsync(materials, recordId, "Required for a WP 21.7A module test.");
    }

    // ---- The governed path ----

    [Fact]
    public async Task AReleasedRecordsProperties_AreReadWithTheirPin_AndTheModuleRecordsTheMaterial()
    {
        var materials = MaterialFixtures.BuildCatalog();
        await materials.RegisterAsync("mat-fx-steel", MaterialFixtures.Steel(), MaterialFixtures.Sourced());
        var released = await ReleaseAsync(materials, "mat-fx-steel");

        var modulus = await MaterialPropertyReader.ReadAsync<Pressure>(materials, "mat-fx-steel", MaterialPropertyNames.YoungsModulus);
        var yield = await MaterialPropertyReader.ReadAsync<Pressure>(materials, "mat-fx-steel", MaterialPropertyNames.YieldStrength);

        Assert.True(modulus.Succeeded, modulus.Reason);
        Assert.True(yield.Succeeded, yield.Reason);
        Assert.Equal(200e9, modulus.Value!.Value.BaseValue, 1e-3);
        Assert.Equal(300e6, yield.Value!.Value.BaseValue, 1e-3);
        Assert.Equal("mat-fx-steel", modulus.Pin!.RecordId);
        Assert.Equal(released.RevisionNumber, modulus.Pin.RevisionNumber);
        Assert.Equal(modulus.Pin, yield.Pin);

        // The pin travels into the calculation and out onto its record.
        var input = new ColumnBucklingInput(
            modulus.Pin,
            new Quantity<Length>(3, LengthUnits.Metre),
            new Quantity<Area>(2000, AreaUnits.SquareMillimetre),
            new Quantity<SecondMomentOfArea>(2.0e6, SecondMomentOfAreaUnits.MillimetreToTheFourth),
            modulus.Value.Value,
            yield.Value.Value,
            5.5,
            new Quantity<Force>(150, ForceUnits.Kilonewton));

        var (executed, readBack) = await RoundTripAsync<ColumnBucklingInput, ColumnBucklingResult>(ColumnBucklingCalculationDefinition.Id, input);

        Assert.Equal(EngineeringCheckOutcome.MeetsCriteria, executed.Result.Outcome);
        Assert.Contains("mat-fx-steel", executed.ReferencedMaterialIds);
        Assert.Contains("mat-fx-steel", readBack.ReferencedMaterialIds);
        Assert.Contains(executed.IntermediateResults, i => i.Name == "Material reference" && i.Value.ToString()!.Contains("mat-fx-steel", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheSeededS355J2Record_DrivesTheThermalAndBeamModulesOnceReleased()
    {
        var materials = MaterialFixtures.BuildCatalog();
        await new ReferenceSeedService().ApplyAsync(materials, MaterialSeed.Instance);
        await ReleaseAsync(materials, MaterialSeed.S355J2);

        var modulus = await MaterialPropertyReader.ReadAsync<Pressure>(materials, MaterialSeed.S355J2, MaterialPropertyNames.YoungsModulus);
        var alpha = await MaterialPropertyReader.ReadAsync<ThermalExpansion>(materials, MaterialSeed.S355J2, MaterialPropertyNames.ThermalExpansionCoefficient);
        var yield = await MaterialPropertyReader.ReadAsync<Pressure>(materials, MaterialSeed.S355J2, MaterialPropertyNames.YieldStrength);

        Assert.True(modulus.Succeeded, modulus.Reason);
        Assert.True(alpha.Succeeded, alpha.Reason);
        Assert.True(yield.Succeeded, yield.Reason);

        // A 2 m S355J2 bar restrained rigidly through +60 K: E alpha dT with
        // the seeded 210 GPa and 12e-6 /K is 151.2 MPa, under the seeded
        // 355 MPa yield.
        var thermal = new ThermalExpansionStressCalculationDefinition().Calculate(
            new ThermalExpansionStressInput(
                modulus.Pin!,
                new Quantity<Length>(2, LengthUnits.Metre),
                new Quantity<Area>(500, AreaUnits.SquareMillimetre),
                modulus.Value!.Value,
                alpha.Value!.Value,
                new Quantity<TemperatureDelta>(60, TemperatureDeltaUnits.Kelvin),
                new Quantity<Length>(0, LengthUnits.Millimetre),
                null,
                yield.Value!.Value),
            new CalculationContext());

        Assert.Equal(-151.2, thermal.Stress.ConvertTo(PressureUnits.Megapascal).Value, 1e-9);
        Assert.Equal(EngineeringCheckOutcome.MeetsCriteria, thermal.Outcome);

        var beam = new BeamDeflectionCalculationDefinition().Calculate(
            new BeamDeflectionInput(
                modulus.Pin!,
                BeamSupport.SimplySupported,
                BeamLoading.PointLoad,
                new Quantity<Force>(10, ForceUnits.Kilonewton),
                new Quantity<Length>(2, LengthUnits.Metre),
                modulus.Value.Value,
                new Quantity<SecondMomentOfArea>(2.0e6, SecondMomentOfAreaUnits.MillimetreToTheFourth),
                new Quantity<Length>(50, LengthUnits.Millimetre),
                yield.Value.Value,
                new Quantity<Length>(8, LengthUnits.Millimetre)),
            new CalculationContext());

        // Example 1 of the specification at 210 GPa rather than the vector's
        // own 210 GPa: identical, 3.9683 mm.
        Assert.Equal(3.9683, beam.MaximumDeflection!.Value.ConvertTo(LengthUnits.Millimetre).Value, 1e-4);
    }

    // ---- The refusals ----

    [Fact]
    public async Task ARecordNobodyHasReleased_IsRefused()
    {
        var materials = MaterialFixtures.BuildCatalog();
        var registered = await materials.RegisterAsync("mat-fx-steel", MaterialFixtures.Steel(), MaterialFixtures.Sourced());
        Assert.NotEqual(ReferenceValidationState.Released, registered.ValidationState);

        var reading = await MaterialPropertyReader.ReadAsync<Pressure>(materials, "mat-fx-steel", MaterialPropertyNames.YoungsModulus);

        Assert.False(reading.Succeeded);
        Assert.Equal(ReferencePropertyRefusal.RecordNotReleased, reading.Refusal);
        Assert.Contains("not Released", reading.Reason, StringComparison.Ordinal);
        Assert.Null(reading.Value);
        Assert.NotNull(reading.Pin);
    }

    [Fact]
    public async Task ARecordThatDoesNotExist_IsRefused()
    {
        var materials = MaterialFixtures.BuildCatalog();

        var reading = await MaterialPropertyReader.ReadAsync<Pressure>(materials, "mat-nobody", MaterialPropertyNames.YoungsModulus);

        Assert.Equal(ReferencePropertyRefusal.RecordNotFound, reading.Refusal);
        Assert.Contains("mat-nobody", reading.Reason, StringComparison.Ordinal);
        Assert.Null(reading.Pin);
    }

    [Fact]
    public async Task APropertyTheRecordDoesNotCarry_IsRefused_NeverSubstituted()
    {
        var materials = MaterialFixtures.BuildCatalog();
        await materials.RegisterAsync("mat-fx-steel", MaterialFixtures.Steel(), MaterialFixtures.Sourced());
        await ReleaseAsync(materials, "mat-fx-steel");

        var reading = await MaterialPropertyReader.ReadAsync<ThermalExpansion>(materials, "mat-fx-steel", MaterialPropertyNames.ThermalExpansionCoefficient);

        Assert.Equal(ReferencePropertyRefusal.RequiredPropertyMissing, reading.Refusal);
        Assert.Contains(MaterialPropertyNames.ThermalExpansionCoefficient, reading.Reason, StringComparison.Ordinal);
        Assert.Null(reading.Value);
    }

    [Fact]
    public async Task APropertyOfTheWrongDimension_IsRefused_NotCoerced()
    {
        var materials = MaterialFixtures.BuildCatalog();
        await materials.RegisterAsync("mat-fx-steel", MaterialFixtures.Steel(), MaterialFixtures.Sourced());
        await ReleaseAsync(materials, "mat-fx-steel");

        var reading = await MaterialPropertyReader.ReadAsync<MassDensity>(materials, "mat-fx-steel", MaterialPropertyNames.YoungsModulus);

        Assert.Equal(ReferencePropertyRefusal.PropertyDimensionWrong, reading.Refusal);
        Assert.Contains(nameof(MassDensity), reading.Reason, StringComparison.Ordinal);
        Assert.Null(reading.Value);
    }

    [Fact]
    public async Task ABlankRecordIdOrPropertyName_IsAnArgumentError()
    {
        var materials = MaterialFixtures.BuildCatalog();

        await Assert.ThrowsAsync<ArgumentException>(() => MaterialPropertyReader.ReadAsync<Pressure>(materials, " ", MaterialPropertyNames.YoungsModulus));
        await Assert.ThrowsAsync<ArgumentException>(() => MaterialPropertyReader.ReadAsync<Pressure>(materials, "mat-fx-steel", ""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => MaterialPropertyReader.ReadAsync<Pressure>(null!, "mat-fx-steel", MaterialPropertyNames.YoungsModulus));
    }
}
