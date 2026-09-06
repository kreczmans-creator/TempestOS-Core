using Tempest.Core.Constants;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Constants;

// A6: the constants-specific half of the library. The shared machinery it
// sits on (registration, revision, lifecycle, supersession, hostile data)
// is tested once, in ReferenceDataCatalogTests, and is not restated here.
public class ConstantLibraryTests
{
    private static bool HasError(IValidationResult result, string code) => result.Errors.Any(d => d.Code == code);

    private static bool HasWarning(IValidationResult result, string code) => result.Warnings.Any(d => d.Code == code);

    // ----------------------------------------------------------------
    // Model
    // ----------------------------------------------------------------

    [Fact]
    public void ADefinition_DefaultsToNotRecordedRatherThanToExact()
    {
        var definition = new ConstantDefinition { Symbol = "fx", Name = "Fixture" };

        Assert.Null(definition.Value);
        Assert.Null(definition.Applicability);
        Assert.Equal(ConstantCategory.Unspecified, definition.Category);
        Assert.Equal(ConstantUncertaintyKind.NotRecorded, definition.Uncertainty.Kind);
        Assert.False(definition.Uncertainty.IsExact);
        Assert.Empty(definition.AlternativeSymbols);
    }

    [Fact]
    public void TheSymbolKeyIsCaseSignificant()
    {
        // Folding case would silently merge two different constants.
        Assert.NotEqual(ConstantDefinition.SymbolKeyFor("G"), ConstantDefinition.SymbolKeyFor("g"));
        Assert.Equal(ConstantDefinition.SymbolKeyFor(" fx_a "), ConstantDefinition.SymbolKeyFor("fx_a"));
        Assert.Throws<ArgumentException>(() => ConstantDefinition.SymbolKeyFor("  "));
    }

    [Fact]
    public void AValueIsAlwaysADimensionedQuantityEvenWhenDimensionless()
    {
        var mathematical = ConstantFixtures.Mathematical();

        Assert.Equal("Dimensionless", mathematical.Value!.DimensionName);
        Assert.Equal(2.5, mathematical.Value.CanonicalValue);
        Assert.IsType<Quantity<Dimensionless>>(mathematical.Value.Value);
    }

    [Fact]
    public void AValueOfAnyDimensionRoundTripsThroughTheStore()
    {
        // A constant's dimension varies from record to record, which is why
        // the value is boxed through the shared codec rather than declared
        // at a statically-known dimension.
        var value = ConstantFixtures.Quantity(ConstantFixtures.MetresPerSecondSquared(10));
        var decoded = ReferenceQuantityCodec.Decode(value.EncodedValue);

        Assert.Equal("Acceleration", value.DimensionName);
        Assert.Equal(value.Value, decoded);
    }

    [Theory]
    [InlineData(ConstantCategory.Mathematical, true, true)]
    [InlineData(ConstantCategory.ConventionalReference, false, true)]
    [InlineData(ConstantCategory.Universal, false, false)]
    public void CategoryTraits_SayWhereAConstantsAuthorityComesFrom(ConstantCategory category, bool dimensionless, bool exact)
    {
        Assert.Equal(dimensionless, ConstantCategories.IsAlwaysDimensionless(category));
        Assert.Equal(exact, ConstantCategories.IsExactByNature(category));
    }

    // ----------------------------------------------------------------
    // Catalogue and the released-constant seam
    // ----------------------------------------------------------------

    [Fact]
    public async Task AConstantIsFindableByItsSymbol()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        await catalog.RegisterAsync("con-0001", ConstantFixtures.Measured(), ConstantFixtures.SourcedProvenance());

        Assert.Equal("con-0001", (await catalog.FindBySymbolAsync(" fx_a "))!.Id);
        Assert.Null(await catalog.FindBySymbolAsync("FX_A"));
    }

    [Fact]
    public async Task RegisteringTwoConstantsUnderOneSymbol_IsRefused()
    {
        // A calculation asking for a symbol must get exactly one answer.
        var catalog = ConstantFixtures.BuildCatalog();
        await catalog.RegisterAsync("con-0001", ConstantFixtures.Measured(), ConstantFixtures.SourcedProvenance());

        var exception = await Assert.ThrowsAsync<DuplicateReferenceKeyException>(
            () => catalog.RegisterAsync(
                "con-0002",
                ConstantFixtures.Measured() with { Name = "A different fixture constant" },
                ConstantFixtures.SourcedProvenance()));

        Assert.Equal("con-0001", exception.ExistingRecordId);
        Assert.Equal("Constants", exception.Library);
        Assert.Contains("fx_a", exception.Message);
    }

    [Fact]
    public async Task TheReleasedSeam_HandsBackNothingUntilTheConstantIsReleased()
    {
        // The property the seam exists for: a calculation can never consume
        // a value nobody has finished verifying.
        var catalog = ConstantFixtures.BuildCatalog();
        IReleasedConstantSource source = catalog;

        await catalog.RegisterAsync("con-0001", ConstantFixtures.Measured(), ConstantFixtures.VerifiedProvenance());
        Assert.Null(await source.FindReleasedAsync("fx_a"));

        await catalog.SetValidationStateAsync("con-0001", ReferenceValidationState.Checked, "Checked.");
        Assert.Null(await source.FindReleasedAsync("fx_a"));

        await catalog.SetValidationStateAsync("con-0001", ReferenceValidationState.Validated, "Rules pass.");
        Assert.Null(await source.FindReleasedAsync("fx_a"));

        await catalog.SetValidationStateAsync("con-0001", ReferenceValidationState.Released, "Released.");
        Assert.NotNull(await source.FindReleasedAsync("fx_a"));
    }

    [Fact]
    public async Task TheReleasedSeam_ReportsAnUnreleasedConstantExactlyAsItReportsAMissingOne()
    {
        // Not "there is a value here you may not use" — that invites using
        // it anyway.
        var catalog = ConstantFixtures.BuildCatalog();
        IReleasedConstantSource source = catalog;

        await catalog.RegisterAsync("con-0001", ConstantFixtures.Measured(), ConstantFixtures.SourcedProvenance());

        Assert.Null(await source.FindReleasedAsync("fx_a"));
        Assert.Null(await source.FindReleasedAsync("fx_never_registered"));
    }

    [Fact]
    public async Task TheReleasedSeam_CarriesTheRecordAndRevisionThatProducedTheValue()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        await catalog.RegisterAsync("con-0001", ConstantFixtures.Measured(), ConstantFixtures.VerifiedProvenance());
        await catalog.ReviseAsync(
            "con-0001",
            ConstantFixtures.Measured() with { Value = ConstantFixtures.Quantity(ConstantFixtures.Pascals(1001)) },
            ConstantFixtures.VerifiedProvenance(),
            "Value corrected against source.");
        var released = await ConstantFixtures.ReleaseAsync(catalog, "con-0001");

        var constant = await ((IReleasedConstantSource)catalog).FindReleasedAsync("fx_a");

        Assert.Equal("con-0001", constant!.RecordId);
        Assert.Equal(released.RevisionNumber, constant.RevisionNumber);
        Assert.Equal(1001, constant.Value.CanonicalValue);
        Assert.Equal("fx_a", constant.Symbol);
    }

    [Fact]
    public async Task ASupersededConstantStopsBeingHandedToCalculations()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        IReleasedConstantSource source = catalog;

        await catalog.RegisterAsync("con-old", ConstantFixtures.Measured(), ConstantFixtures.VerifiedProvenance());
        await ConstantFixtures.ReleaseAsync(catalog, "con-old");
        await catalog.RegisterAsync("con-new", ConstantFixtures.Measured("fx_a2"), ConstantFixtures.VerifiedProvenance());
        await catalog.SupersedeAsync("con-old", "con-new", "Replaced by the 2026 adjustment.");

        // The superseded record is still readable — its history is itself
        // engineering data — but it is no longer a value to calculate with.
        Assert.Null(await source.FindReleasedAsync("fx_a"));
        Assert.NotNull(await catalog.FindBySymbolAsync("fx_a"));
    }

    [Fact]
    public async Task TheReleasedSeam_RefusesAnEmptySymbol() =>
        await Assert.ThrowsAsync<ArgumentException>(
            () => ((IReleasedConstantSource)ConstantFixtures.BuildCatalog()).FindReleasedAsync("  "));

    // ----------------------------------------------------------------
    // Search
    // ----------------------------------------------------------------

    [Fact]
    public async Task Search_MatchesOnCategoryDimensionAndUncertaintyKind()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        await catalog.RegisterAsync("con-measured", ConstantFixtures.Measured(), ConstantFixtures.SourcedProvenance());
        await catalog.RegisterAsync("con-maths", ConstantFixtures.Mathematical(), ConstantFixtures.SourcedProvenance());
        await catalog.RegisterAsync("con-conventional", ConstantFixtures.Conventional(), ConstantFixtures.SourcedProvenance());

        Assert.Equal(
            ["con-maths"],
            (await catalog.SearchAsync(new ConstantQuery { Categories = [ConstantCategory.Mathematical] })).Select(c => c.Id));
        Assert.Equal(
            ["con-conventional"],
            (await catalog.SearchAsync(new ConstantQuery { DimensionName = "Acceleration" })).Select(c => c.Id));
        Assert.Equal(
            ["con-conventional", "con-maths"],
            (await catalog.SearchAsync(new ConstantQuery { UncertaintyKinds = [ConstantUncertaintyKind.Exact] })).Select(c => c.Id).Order());
        Assert.Equal(
            ["con-conventional"],
            (await catalog.SearchAsync(new ConstantQuery { ApplicabilityContains = "fixture convention" })).Select(c => c.Id));
    }

    [Fact]
    public async Task Search_OnASymbolIsCaseSensitiveAndCoversAlternativeSymbols()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        await catalog.RegisterAsync(
            "con-0001",
            ConstantFixtures.Measured() with { AlternativeSymbols = ["fx_alpha"] },
            ConstantFixtures.SourcedProvenance());

        Assert.Single(await catalog.SearchAsync(new ConstantQuery { SymbolContains = "fx_a" }));
        Assert.Single(await catalog.SearchAsync(new ConstantQuery { SymbolContains = "fx_alpha" }));
        Assert.Empty(await catalog.SearchAsync(new ConstantQuery { SymbolContains = "FX_A" }));
    }

    // ----------------------------------------------------------------
    // Comparison
    // ----------------------------------------------------------------

    [Fact]
    public async Task Comparison_LaysTwoEditionsOfOneConstantSideBySide()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        await catalog.RegisterAsync("con-a-2018", ConstantFixtures.Measured("fx_a"), ConstantFixtures.SourcedProvenance());
        await catalog.RegisterAsync(
            "con-b-2026",
            ConstantFixtures.Measured("fx_a2") with { Value = ConstantFixtures.Quantity(ConstantFixtures.Pascals(1001)) },
            ConstantFixtures.SourcedProvenance());

        var comparison = ConstantComparer.Compare(await catalog.ListAsync());
        var values = comparison.Row(ConstantComparisonProperties.Value)!;

        Assert.True(comparison.IsSingleFamily);
        Assert.Equal([1000, 1001], values.Cells.Select(c => c.CanonicalValue));
    }

    [Fact]
    public async Task Comparison_OffersNoCanonicalValueWhereTheDimensionsDiffer()
    {
        // Ordering values of different dimensions by their base-unit
        // magnitudes would be arithmetic on numbers that are not
        // comparable.
        var catalog = ConstantFixtures.BuildCatalog();
        await catalog.RegisterAsync("con-a", ConstantFixtures.Measured(), ConstantFixtures.SourcedProvenance());
        await catalog.RegisterAsync("con-b", ConstantFixtures.Conventional(), ConstantFixtures.SourcedProvenance());

        var comparison = ConstantComparer.Compare(await catalog.ListAsync());

        Assert.False(comparison.IsSingleFamily);
        Assert.All(comparison.Row(ConstantComparisonProperties.Value)!.Cells, cell => Assert.Null(cell.CanonicalValue));
    }

    [Fact]
    public async Task Comparison_ReportsAnExactConstantsUncertaintyAsNotApplicable()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        await catalog.RegisterAsync("con-a-measured", ConstantFixtures.Measured(), ConstantFixtures.SourcedProvenance());
        await catalog.RegisterAsync(
            "con-b-exact",
            ConstantFixtures.Measured("fx_b") with { Uncertainty = ConstantUncertainty.Exact },
            ConstantFixtures.SourcedProvenance());

        var cells = ConstantComparer.Compare(await catalog.ListAsync())
            .Row(ConstantComparisonProperties.AbsoluteUncertainty)!.Cells;

        Assert.Equal(ReferencePropertyAvailability.Recorded, cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, cells[1].Availability);
    }

    // ----------------------------------------------------------------
    // Validation
    // ----------------------------------------------------------------

    [Fact]
    public async Task EveryCoherentFixture_PassesEveryRule()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        await catalog.RegisterAsync("con-measured", ConstantFixtures.Measured(), ConstantFixtures.VerifiedProvenance());
        await catalog.RegisterAsync("con-maths", ConstantFixtures.Mathematical(), ConstantFixtures.VerifiedProvenance());
        await catalog.RegisterAsync("con-conventional", ConstantFixtures.Conventional(), ConstantFixtures.VerifiedProvenance());

        var report = await validator.ValidateLibraryAsync();

        Assert.Equal(3, report.RecordsExamined);
        Assert.True(
            report.Findings.Count == 0,
            string.Join("; ", report.Findings.SelectMany(f => f.Result.Errors.Concat(f.Result.Warnings)).Select(d => $"{d.Code}: {d.Message}")));
    }

    [Fact]
    public async Task AConstantWithNoValue_IsNotAConstant()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            new ConstantDefinition { Symbol = "fx", Name = "Fixture" },
            ConstantFixtures.SourcedProvenance());

        Assert.True(HasError(result, ConstantValidationRules.ValueMustBeRecorded));
    }

    [Fact]
    public async Task NotRecordedIsNeverReadAsExact()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Measured() with { Uncertainty = ConstantUncertainty.NotRecorded },
            ConstantFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ConstantValidationRules.UncertaintyShouldBeRecorded));
    }

    [Fact]
    public async Task AnExactConstantIsStillAskedToSayItIsExact()
    {
        // "Exact" is a claim the record should make, not one the reader
        // should infer from the category.
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Mathematical() with { Uncertainty = ConstantUncertainty.NotRecorded },
            ConstantFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ConstantValidationRules.UncertaintyShouldBeRecorded));
        Assert.Contains("normally exact", result.Warnings.Single(d => d.Code == ConstantValidationRules.UncertaintyShouldBeRecorded).Message);
    }

    [Fact]
    public async Task AnExactConstantCarryingAnUncertaintyFigure_IsAnError()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Mathematical() with
            {
                Uncertainty = new ConstantUncertainty(ConstantUncertaintyKind.Exact, Relative: 0.001),
            },
            ConstantFixtures.SourcedProvenance());

        Assert.True(HasError(result, ConstantValidationRules.ExactConstantCarriesUncertainty));
    }

    [Fact]
    public async Task AnUncertaintyInTheWrongDimension_IsAnError()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Measured() with
            {
                Uncertainty = new ConstantUncertainty(
                    ConstantUncertaintyKind.Standard,
                    Absolute: ConstantFixtures.Quantity(ConstantFixtures.MetresPerSecondSquared(0.5))),
            },
            ConstantFixtures.SourcedProvenance());

        Assert.True(HasError(result, ConstantValidationRules.UncertaintyDimensionMismatch));
    }

    [Fact]
    public async Task ANegativeUncertainty_IsAnError()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Measured() with
            {
                Uncertainty = new ConstantUncertainty(ConstantUncertaintyKind.Standard, Relative: -0.001),
            },
            ConstantFixtures.SourcedProvenance());

        Assert.True(HasError(result, ConstantValidationRules.UncertaintyMustNotBeNegative));
    }

    [Fact]
    public async Task ARelativeUncertaintyOfOneOrMore_IsCaughtAsALikelyPercentageMistake()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Measured() with
            {
                Uncertainty = new ConstantUncertainty(ConstantUncertaintyKind.Standard, Relative: 5.0),
            },
            ConstantFixtures.SourcedProvenance());

        Assert.True(HasError(result, ConstantValidationRules.RelativeUncertaintyImplausible));
    }

    [Fact]
    public async Task AnExpandedUncertaintyWithNoCoverageFactor_IsWarnedAbout()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        var missing = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Measured() with
            {
                Uncertainty = new ConstantUncertainty(ConstantUncertaintyKind.Expanded, Relative: 0.0005),
            },
            ConstantFixtures.SourcedProvenance());

        var nonPositive = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Measured() with
            {
                Uncertainty = new ConstantUncertainty(ConstantUncertaintyKind.Expanded, Relative: 0.0005, CoverageFactor: 0),
            },
            ConstantFixtures.SourcedProvenance());

        Assert.True(HasWarning(missing, ConstantValidationRules.ExpandedUncertaintyNeedsCoverageFactor));
        Assert.True(HasError(nonPositive, ConstantValidationRules.CoverageFactorMustBePositive));
    }

    [Fact]
    public async Task AMathematicalConstantWithADimension_IsAnError()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Mathematical() with { Value = ConstantFixtures.Quantity(ConstantFixtures.Pascals(2.5)) },
            ConstantFixtures.SourcedProvenance());

        Assert.True(HasError(result, ConstantValidationRules.MathematicalConstantMustBeDimensionless));
    }

    [Fact]
    public async Task AConventionalValueWithNoStatementOfWhereItApplies_IsWarnedAbout()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Conventional() with { Applicability = null },
            ConstantFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ConstantValidationRules.ApplicabilityShouldBeRecorded));
    }

    [Fact]
    public async Task AValueTempestOSDerivedForItself_IsFlaggedAsNotAPublishedConstant()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Measured() with
            {
                Value = ConstantFixtures.Quantity(ConstantFixtures.Pascals(1000), ReferenceValueOrigin.DerivedByTempestOS),
            },
            ConstantFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ReferenceValidationRules.DerivedValuePresent));
    }

    [Fact]
    public async Task ASymbolShadowedByAnotherRecordsAlternativeSymbol_IsWarnedAbout()
    {
        // The index cannot catch this: an alternative symbol is not a key,
        // but a reader looking the symbol up finds two claims to it.
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        await catalog.RegisterAsync("con-primary", ConstantFixtures.Measured("fx_a"), ConstantFixtures.VerifiedProvenance());
        await catalog.RegisterAsync(
            "con-claimant",
            ConstantFixtures.Measured("fx_b") with { AlternativeSymbols = ["fx_a"] },
            ConstantFixtures.VerifiedProvenance());

        Assert.True(HasWarning(await validator.ValidateAsync("con-primary"), ConstantValidationRules.SymbolCollidesWithAnAlternative));
    }

    [Fact]
    public async Task ACategorisedOtherConstant_MustCarryTheSourcesOwnWording()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        var validator = new ConstantValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Measured() with { Category = ConstantCategory.Other },
            ConstantFixtures.SourcedProvenance());

        var repaired = await validator.ValidateDefinitionAsync(
            ConstantFixtures.Measured() with
            {
                Category = ConstantCategory.Other,
                SourceClassification = "The source called it a 'fixture engineering datum'.",
                Applicability = "Fixture use only.",
            },
            ConstantFixtures.SourcedProvenance());

        Assert.True(HasError(result, ConstantValidationRules.OtherCategoryNeedsSourceClassification));
        Assert.False(HasError(repaired, ConstantValidationRules.OtherCategoryNeedsSourceClassification));
    }

    // ----------------------------------------------------------------
    // Lifecycle
    // ----------------------------------------------------------------

    [Fact]
    public async Task EveryConstantRecordIsBackedByOneDocumentOfTheConstantKind()
    {
        var catalog = ConstantFixtures.BuildCatalog(out var documentStore, out _);
        var record = await catalog.RegisterAsync("con-0001", ConstantFixtures.Measured(), ConstantFixtures.SourcedProvenance());

        Assert.Equal(ConstantCatalog.ConstantDocumentKind, (await documentStore.FindAsync(record.UnderlyingDocumentId))!.Kind);
    }

    [Fact]
    public async Task AReleasedConstantIsImmutableSoTheValueACalculationCitedCanAlwaysBeReadBack()
    {
        var catalog = ConstantFixtures.BuildCatalog();
        await catalog.RegisterAsync("con-0001", ConstantFixtures.Measured(), ConstantFixtures.VerifiedProvenance());
        await ConstantFixtures.ReleaseAsync(catalog, "con-0001");

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(
            () => catalog.ReviseAsync(
                "con-0001",
                ConstantFixtures.Measured() with { Value = ConstantFixtures.Quantity(ConstantFixtures.Pascals(9999)) },
                ConstantFixtures.VerifiedProvenance(),
                "Refused."));

        Assert.Equal(1000, (await catalog.FindBySymbolAsync("fx_a"))!.Definition.Value!.CanonicalValue);
    }
}
