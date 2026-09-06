using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;
using Tempest.Core.Standards;

namespace Tempest.Core.Tests.Standards;

// A2: the standards-specific half of the library. The shared machinery it
// sits on (registration, revision, lifecycle, supersession, hostile data)
// is tested once, in ReferenceDataCatalogTests, and is not restated here.
public class StandardLibraryTests
{
    private static bool HasError(IValidationResult result, string code) => result.Errors.Any(d => d.Code == code);

    private static bool HasWarning(IValidationResult result, string code) => result.Warnings.Any(d => d.Code == code);

    // ----------------------------------------------------------------
    // Model and taxonomy
    // ----------------------------------------------------------------

    [Fact]
    public void ADefinition_DefaultsEveryOptionalFieldToAbsent()
    {
        var definition = new StandardDefinition { Body = StandardFixtures.Body(), Designation = "FX-1" };

        Assert.Null(definition.Title);
        Assert.Null(definition.Edition);
        Assert.Null(definition.PartNumber);
        Assert.Null(definition.ScopeSummary);
        Assert.Null(definition.PublicationDate);
        Assert.Null(definition.EffectiveDate);
        Assert.Null(definition.WithdrawalDate);
        Assert.Null(definition.ConfirmationDate);
        Assert.Null(definition.Language);
        Assert.Equal(StandardClassification.Unspecified, definition.Classification);
        Assert.Equal(StandardPublicationStatus.Unknown, definition.PublicationStatus);
        Assert.Empty(definition.Disciplines);
        Assert.Empty(definition.Equivalences);
        Assert.Empty(definition.NormativeReferences);
        Assert.Empty(definition.ReplacesDesignations);
    }

    [Fact]
    public void ABody_RequiresACodeAndNormalisesItForMatching()
    {
        Assert.Throws<ArgumentException>(() => new StandardsBody("  "));
        Assert.Equal("TFX", new StandardsBody("  tfx  ").Code.ToUpperInvariant());
        Assert.Equal(new StandardsBody("tfx").CodeKey, new StandardsBody("TFX").CodeKey);
    }

    [Fact]
    public void TheDesignationKey_TreatsTwoEditionsOfOneStandardAsTwoDistinctRecords()
    {
        // The central identity decision in A2: an edition is not a
        // revision of one record, it is a second piece of reference data
        // that must be holdable alongside the first.
        var first = StandardFixtures.Dimensional("FX-100", "2018");
        var second = StandardFixtures.Dimensional("FX-100", "2026");

        Assert.NotEqual(first.DesignationKey, second.DesignationKey);
        Assert.Equal(first.DesignationKey, (first with { Designation = " fx-100 " }).DesignationKey);
    }

    [Fact]
    public void TheDesignationKey_CollapsesToBodyAndDesignationWhereNoEditionIsRecorded()
    {
        var undated = StandardFixtures.Dimensional("FX-100", edition: null);

        Assert.Equal(StandardDefinition.DesignationKeyFor("TFX", "FX-100"), undated.DesignationKey);
        Assert.Equal("TFX FX-100", undated.FullDesignation);
        Assert.Equal("TFX FX-100:2026", StandardFixtures.Dimensional().FullDesignation);
    }

    [Fact]
    public void TheDesignationKey_RefusesToBeBuiltFromNothing()
    {
        Assert.Throws<ArgumentException>(() => StandardDefinition.DesignationKeyFor("", "FX-1"));
        Assert.Throws<ArgumentException>(() => StandardDefinition.DesignationKeyFor("TFX", "  "));
    }

    [Fact]
    public void AnEquivalence_RequiresTheOtherStandardsDesignation() =>
        Assert.Throws<ArgumentException>(() => new StandardEquivalence("  "));

    [Theory]
    [InlineData(StandardClassification.Specification, true)]
    [InlineData(StandardClassification.DimensionalStandard, true)]
    [InlineData(StandardClassification.ManagementSystem, true)]
    [InlineData(StandardClassification.TestMethod, false)]
    [InlineData(StandardClassification.Terminology, false)]
    public void ClassificationTraits_KnowWhichStandardsStateConformityRequirements(StandardClassification classification, bool expected) =>
        Assert.Equal(expected, StandardClassificationTraits.StatesConformityRequirements(classification));

    [Fact]
    public void ClassificationTraits_RefuseToSpeakForAnUnclassifiedStandard()
    {
        // "Not known to apply" is not "known not to apply" — the same
        // conservative rule A1 and A4 apply to their own families.
        Assert.False(StandardClassificationTraits.IsApplicabilityKnown(StandardClassification.Unspecified));
        Assert.False(StandardClassificationTraits.IsApplicabilityKnown(StandardClassification.Other));
        Assert.True(StandardClassificationTraits.IsApplicabilityKnown(StandardClassification.TestMethod));
    }

    // ----------------------------------------------------------------
    // Publisher status is not record validation state
    // ----------------------------------------------------------------

    [Fact]
    public async Task AWithdrawnStandard_CanBeHeldInAFullyReleasedRecord()
    {
        // The distinction A2 exists to preserve. An accurate, verified,
        // released record of a standard its publisher withdrew is exactly
        // what a legacy design review needs; the two axes are independent.
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-0001", StandardFixtures.Withdrawn(), StandardFixtures.Verified());

        var released = await StandardFixtures.ReleaseAsync(catalog, "std-0001");

        Assert.Equal(ReferenceValidationState.Released, released.ValidationState);
        Assert.Equal(StandardPublicationStatus.Withdrawn, released.Definition.PublicationStatus);
    }

    [Fact]
    public async Task ACurrentStandard_CanSitInADraftRecordNobodyHasChecked()
    {
        var catalog = StandardFixtures.BuildCatalog();
        var record = await catalog.RegisterAsync("std-0001", StandardFixtures.Dimensional(), StandardFixtures.Sourced());

        Assert.Equal(ReferenceValidationState.Draft, record.ValidationState);
        Assert.Equal(StandardPublicationStatus.Current, record.Definition.PublicationStatus);
    }

    [Fact]
    public void PublicationStatuses_AnswerOnlyWhatThePublisherSaid()
    {
        Assert.False(StandardPublicationStatuses.IsKnown(StandardPublicationStatus.Unknown));
        Assert.True(StandardPublicationStatuses.IsCurrent(StandardPublicationStatus.Amended));
        Assert.True(StandardPublicationStatuses.IsNoLongerInForce(StandardPublicationStatus.Obsolete));
        Assert.False(StandardPublicationStatuses.IsNoLongerInForce(StandardPublicationStatus.Draft));
        Assert.Equal(
            Enum.GetValues<StandardPublicationStatus>().Length,
            StandardPublicationStatuses.All.Count);
    }

    // ----------------------------------------------------------------
    // Catalogue
    // ----------------------------------------------------------------

    [Fact]
    public async Task TwoEditionsOfOneStandard_AreBothRegisterableAndBothFindable()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-2018", StandardFixtures.Dimensional("FX-100", "2018"), StandardFixtures.Sourced());
        await catalog.RegisterAsync("std-2026", StandardFixtures.Dimensional("FX-100", "2026"), StandardFixtures.Sourced());

        var older = await catalog.FindByDesignationAsync("TFX", "FX-100", "2018");
        var newer = await catalog.FindByDesignationAsync("TFX", "FX-100", "2026");
        var editions = await catalog.FindEditionsAsync("tfx", " fx-100 ");

        Assert.Equal("std-2018", older!.Id);
        Assert.Equal("std-2026", newer!.Id);
        Assert.Equal(["std-2018", "std-2026"], editions.Select(e => e.Id));
    }

    [Fact]
    public async Task LookingUpWithoutAnEdition_FindsTheUndatedRecordRatherThanGuessingTheLatest()
    {
        // A2 has no authority to decide which edition a caller meant.
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-dated", StandardFixtures.Dimensional("FX-100", "2026"), StandardFixtures.Sourced());

        Assert.Null(await catalog.FindByDesignationAsync("TFX", "FX-100"));

        await catalog.RegisterAsync("std-undated", StandardFixtures.Dimensional("FX-100", edition: null), StandardFixtures.Sourced());

        Assert.Equal("std-undated", (await catalog.FindByDesignationAsync("TFX", "FX-100"))!.Id);
    }

    [Fact]
    public async Task RegisteringTheSameBodyDesignationAndEditionTwice_IsRefused()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-0001", StandardFixtures.Dimensional(), StandardFixtures.Sourced());

        var exception = await Assert.ThrowsAsync<DuplicateReferenceKeyException>(
            () => catalog.RegisterAsync("std-0002", StandardFixtures.Dimensional(), StandardFixtures.Sourced()));

        Assert.Equal("std-0001", exception.ExistingRecordId);
        Assert.Equal("Standards", exception.Library);
        Assert.Contains("TFX FX-100:2026", exception.Message);
    }

    [Fact]
    public async Task TheSameDesignationFromADifferentBody_IsADifferentStandard()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-0001", StandardFixtures.Dimensional(), StandardFixtures.Sourced());
        await catalog.RegisterAsync(
            "std-0002",
            StandardFixtures.Dimensional() with { Body = new StandardsBody("FXN", "Fixture National Body", StandardsBodyKind.National) },
            StandardFixtures.Sourced());

        Assert.Equal(2, (await catalog.ListAsync()).Count);
    }

    [Fact]
    public async Task ReviseAsync_MovesTheDesignationIndexWithTheRecord()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-0001", StandardFixtures.Dimensional("FX-100"), StandardFixtures.Sourced());

        await catalog.ReviseAsync(
            "std-0001",
            StandardFixtures.Dimensional("FX-101"),
            StandardFixtures.Sourced(),
            "Designation mis-transcribed.");

        Assert.Null(await catalog.FindByDesignationAsync("TFX", "FX-100", "2026"));
        Assert.Equal("std-0001", (await catalog.FindByDesignationAsync("TFX", "FX-101", "2026"))!.Id);
    }

    // ----------------------------------------------------------------
    // The IStandardResolver seam
    // ----------------------------------------------------------------

    [Fact]
    public async Task TheCatalogue_IsTheStandardResolverEveryOtherLibraryCitesThrough()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-0001", StandardFixtures.Dimensional(), StandardFixtures.Sourced());

        IStandardResolver resolver = catalog;

        Assert.True(await resolver.ExistsAsync("std-0001"));
        Assert.False(await resolver.ExistsAsync("std-9999"));
    }

    [Fact]
    public async Task TheResolver_ReportsADraftRecordAsPresent()
    {
        // Existence and release are different questions. A citation of an
        // unchecked record is a governance observation for the citing
        // library to make with the record in hand, not a reason to report
        // the standard as absent.
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-0001", StandardFixtures.Dimensional(), ReferenceProvenance.Unknown);

        Assert.True(await ((IStandardResolver)catalog).ExistsAsync("std-0001"));
    }

    [Fact]
    public async Task TheResolver_RefusesAnEmptyStandardId() =>
        await Assert.ThrowsAsync<ArgumentException>(
            () => ((IStandardResolver)StandardFixtures.BuildCatalog()).ExistsAsync("  "));

    // ----------------------------------------------------------------
    // Search
    // ----------------------------------------------------------------

    [Fact]
    public async Task Search_FiltersOnPublisherStatusAndRecordStateIndependently()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-current-draft", StandardFixtures.Dimensional("FX-100"), StandardFixtures.Sourced());
        await catalog.RegisterAsync("std-withdrawn-released", StandardFixtures.Withdrawn("FX-200"), StandardFixtures.Verified());
        await StandardFixtures.ReleaseAsync(catalog, "std-withdrawn-released");

        var publisherCurrent = await catalog.SearchAsync(new StandardQuery
        {
            PublicationStatuses = [StandardPublicationStatus.Current],
        });
        var recordReleased = await catalog.SearchAsync(new StandardQuery
        {
            ValidationStates = [ReferenceValidationState.Released],
        });

        Assert.Equal(["std-current-draft"], publisherCurrent.Select(s => s.Id));
        Assert.Equal(["std-withdrawn-released"], recordReleased.Select(s => s.Id));
    }

    [Fact]
    public async Task Search_MatchesOnBodyClassificationDisciplineAndText()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-dim", StandardFixtures.Dimensional("FX-100"), StandardFixtures.Sourced());
        await catalog.RegisterAsync("std-test", StandardFixtures.TestMethod("FX-200"), StandardFixtures.Sourced());
        await catalog.RegisterAsync("std-term", StandardFixtures.Terminology("FX-300"), StandardFixtures.Sourced());

        Assert.Equal(3, (await catalog.SearchAsync(new StandardQuery { BodyCode = " tfx " })).Count);
        Assert.Empty(await catalog.SearchAsync(new StandardQuery { BodyCode = "OTHER" }));
        Assert.Equal(
            ["std-test"],
            (await catalog.SearchAsync(new StandardQuery { Classifications = [StandardClassification.TestMethod] })).Select(s => s.Id));
        Assert.Equal(
            ["std-dim"],
            (await catalog.SearchAsync(new StandardQuery { Disciplines = [StandardDiscipline.Metrology] })).Select(s => s.Id));
        Assert.Equal(
            ["std-term"],
            (await catalog.SearchAsync(new StandardQuery { TitleContains = "terminology" })).Select(s => s.Id));
        Assert.Equal(
            ["std-dim", "std-term", "std-test"],
            (await catalog.SearchAsync(new StandardQuery { ScopeSummaryContains = "invented" })).Select(s => s.Id).Order());
    }

    [Fact]
    public async Task Search_NeverTreatsAnUnrecordedPublicationDateAsAMatch()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-dated", StandardFixtures.Dimensional(), StandardFixtures.Sourced());
        await catalog.RegisterAsync(
            "std-undated",
            StandardFixtures.Dimensional("FX-200") with { PublicationDate = null, EffectiveDate = null },
            StandardFixtures.Sourced());

        var matched = await catalog.SearchAsync(new StandardQuery { PublishedOnOrAfter = new DateOnly(2000, 1, 1) });

        Assert.Equal(["std-dated"], matched.Select(s => s.Id));
    }

    [Fact]
    public async Task Search_FindsAStandardByWhatItReferencesReplacesOrIsEquivalentTo()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync(
            "std-0001",
            StandardFixtures.Dimensional() with
            {
                Equivalences = [new StandardEquivalence("FX-900", StandardEquivalenceKind.Identical, Body: "FXN", Origin: ReferenceValueOrigin.Standard)],
                NormativeReferences = [new StandardReference("FX-800", Body: "TFX")],
                ReplacesDesignations = ["FX-050"],
            },
            StandardFixtures.Sourced());
        await catalog.RegisterAsync("std-0002", StandardFixtures.TestMethod(), StandardFixtures.Sourced());

        Assert.Single(await catalog.SearchAsync(new StandardQuery { EquivalentToDesignationContaining = "fx-900" }));
        Assert.Single(await catalog.SearchAsync(new StandardQuery { NormativelyReferencesDesignationContaining = "fx-800" }));
        Assert.Single(await catalog.SearchAsync(new StandardQuery { ReplacesDesignationContaining = "fx-050" }));
    }

    [Fact]
    public async Task Search_CombinesEveryCriterionWithAnd()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-0001", StandardFixtures.Dimensional(), StandardFixtures.Sourced());

        Assert.Empty(await catalog.SearchAsync(new StandardQuery
        {
            BodyCode = "TFX",
            Classifications = [StandardClassification.Terminology],
        }));
    }

    // ----------------------------------------------------------------
    // Comparison
    // ----------------------------------------------------------------

    [Fact]
    public async Task Comparison_KeepsPublisherStatusAndRecordStateAsSeparateRows()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-0001", StandardFixtures.Withdrawn(), StandardFixtures.Verified());
        await StandardFixtures.ReleaseAsync(catalog, "std-0001");

        var comparison = StandardComparer.Compare(await catalog.ListAsync());

        Assert.Equal("Withdrawn", comparison.Row(StandardComparisonProperties.PublicationStatus)!.Cells[0].Display);
        Assert.Equal("Released", comparison.Row(StandardComparisonProperties.RecordValidationState)!.Cells[0].Display);
    }

    [Fact]
    public async Task Comparison_ReportsAWithdrawalDateAsNotApplicableForAStandardStillInForce()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-current", StandardFixtures.Dimensional("FX-100"), StandardFixtures.Sourced());
        await catalog.RegisterAsync(
            "std-withdrawn-undated",
            StandardFixtures.Withdrawn("FX-200") with { WithdrawalDate = null },
            StandardFixtures.Sourced());

        var comparison = StandardComparer.Compare(await catalog.ListAsync());
        var cells = comparison.Row(StandardComparisonProperties.WithdrawalDate)!.Cells;

        // Nothing to record versus nobody recorded it — the distinction the
        // whole comparison capability exists to preserve.
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotRecorded, cells[1].Availability);
    }

    [Fact]
    public async Task Comparison_OrdersDatesByTheirOwnCanonicalValue()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-2018", StandardFixtures.Withdrawn("FX-100", "2018"), StandardFixtures.Sourced());
        await catalog.RegisterAsync("std-2026", StandardFixtures.Dimensional("FX-100", "2026"), StandardFixtures.Sourced());

        var comparison = StandardComparer.Compare(await catalog.ListAsync());
        var published = comparison.Row(StandardComparisonProperties.PublicationDate)!;

        Assert.True(published.Cells[0].CanonicalValue < published.Cells[1].CanonicalValue);
    }

    [Fact]
    public async Task Comparison_ReportsAZeroCountAsRecorded()
    {
        // "This record lists no equivalences" is a fact the record states,
        // unlike a value nobody supplied.
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-0001", StandardFixtures.Dimensional(), StandardFixtures.Sourced());

        var cell = StandardComparer.Compare(await catalog.ListAsync())
            .Row(StandardComparisonProperties.EquivalenceCount)!.Cells[0];

        Assert.Equal(ReferencePropertyAvailability.Recorded, cell.Availability);
        Assert.Equal(0, cell.CanonicalValue);
    }

    [Fact]
    public async Task Comparison_FlagsThatStandardsOfDifferentKindsAreBeingCompared()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-dim", StandardFixtures.Dimensional("FX-100"), StandardFixtures.Sourced());
        await catalog.RegisterAsync("std-test", StandardFixtures.TestMethod("FX-200"), StandardFixtures.Sourced());

        Assert.False(StandardComparer.Compare(await catalog.ListAsync()).IsSingleFamily);
    }

    // ----------------------------------------------------------------
    // Validation
    // ----------------------------------------------------------------

    [Fact]
    public async Task ACompleteFixtureStandard_PassesEveryRule()
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);
        await catalog.RegisterAsync("std-0001", StandardFixtures.Dimensional(), StandardFixtures.Verified());

        var result = await validator.ValidateAsync("std-0001");

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ARecordMissingItsBibliographicIdentity_IsWarnedAboutRatherThanRefused()
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            new StandardDefinition { Body = StandardFixtures.Body(), Designation = "FX-1" },
            StandardFixtures.Sourced());

        Assert.True(HasWarning(result, StandardValidationRules.TitleShouldBeRecorded));
        Assert.True(HasWarning(result, StandardValidationRules.EditionShouldBeRecorded));
        Assert.True(HasWarning(result, StandardValidationRules.ClassificationShouldBeStated));
        Assert.True(HasWarning(result, StandardValidationRules.DisciplineShouldBeRecorded));
        Assert.True(HasWarning(result, StandardValidationRules.PublicationStatusShouldBeStated));
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AStandardClassifiedOther_MustRecordTheSourcesOwnWording(bool classificationOther, bool bodyOther)
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);

        var definition = StandardFixtures.Dimensional() with
        {
            Classification = classificationOther ? StandardClassification.Other : StandardClassification.DimensionalStandard,
            Body = bodyOther ? StandardFixtures.Body(StandardsBodyKind.Other) : StandardFixtures.Body(),
        };

        var result = await validator.ValidateDefinitionAsync(definition, StandardFixtures.Sourced());
        var repaired = await validator.ValidateDefinitionAsync(
            definition with { SourceClassification = "The source called it a 'fixture practice note'." },
            StandardFixtures.Sourced());

        Assert.True(HasError(result, StandardValidationRules.OtherClassificationNeedsSourceClassification));
        Assert.False(HasError(repaired, StandardValidationRules.OtherClassificationNeedsSourceClassification));
    }

    [Fact]
    public async Task AStandardBothCurrentAndWithdrawn_IsAnError()
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            StandardFixtures.Dimensional() with { WithdrawalDate = new DateOnly(2026, 6, 1) },
            StandardFixtures.Sourced());

        Assert.True(HasError(result, StandardValidationRules.CurrentStandardHasWithdrawalDate));
    }

    [Fact]
    public async Task AStandardOutOfForceWithoutAWithdrawalDate_IsWarnedAbout()
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            StandardFixtures.Withdrawn() with { WithdrawalDate = null },
            StandardFixtures.Sourced());

        Assert.True(HasWarning(result, StandardValidationRules.WithdrawalDateShouldBeRecorded));
    }

    [Theory]
    [InlineData("effective")]
    [InlineData("withdrawn")]
    [InlineData("confirmed")]
    public async Task ADateThatPrecedesPublication_IsAnError(string field)
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);
        var before = new DateOnly(2000, 1, 1);

        var definition = StandardFixtures.Dimensional() with { PublicationStatus = StandardPublicationStatus.Superseded };
        definition = field switch
        {
            "effective" => definition with { EffectiveDate = before },
            "withdrawn" => definition with { WithdrawalDate = before },
            _ => definition with { ConfirmationDate = before },
        };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition, StandardFixtures.Sourced()), StandardValidationRules.DatesOutOfOrder));
    }

    [Fact]
    public async Task AWithdrawalBeforeTheStandardTookEffect_IsAnError()
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            StandardFixtures.Dimensional() with
            {
                PublicationStatus = StandardPublicationStatus.Withdrawn,
                PublicationDate = new DateOnly(2026, 1, 1),
                EffectiveDate = new DateOnly(2026, 6, 1),
                WithdrawalDate = new DateOnly(2026, 3, 1),
            },
            StandardFixtures.Sourced());

        Assert.True(HasError(result, StandardValidationRules.DatesOutOfOrder));
    }

    [Fact]
    public async Task ALongScopeSummary_IsFlaggedAsPossiblyReproducedStandardText()
    {
        // A2 registers standards; it never reproduces them. The rule is a
        // heuristic and so is reported as a warning, never as a claim.
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            StandardFixtures.Dimensional() with
            {
                ScopeSummary = new string('x', StandardValidationService.ScopeSummaryLengthWarningThreshold + 1),
            },
            StandardFixtures.Sourced());

        Assert.True(HasWarning(result, StandardValidationRules.ScopeSummaryMayBeReproducedText));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task AStandardThatNamesItself_IsAnError()
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);

        var asEquivalent = await validator.ValidateDefinitionAsync(
            StandardFixtures.Dimensional() with
            {
                Equivalences = [new StandardEquivalence("FX-100", Body: "TFX", Origin: ReferenceValueOrigin.Standard)],
            },
            StandardFixtures.Sourced());

        var asReference = await validator.ValidateDefinitionAsync(
            StandardFixtures.Dimensional() with { NormativeReferences = [new StandardReference("FX-100", Body: "TFX")] },
            StandardFixtures.Sourced());

        var asReplaced = await validator.ValidateDefinitionAsync(
            StandardFixtures.Dimensional() with { ReplacesDesignations = ["FX-100"] },
            StandardFixtures.Sourced());

        Assert.True(HasError(asEquivalent, StandardValidationRules.SelfReference));
        Assert.True(HasError(asReference, StandardValidationRules.SelfReference));
        Assert.True(HasError(asReplaced, StandardValidationRules.SelfReference));
    }

    [Fact]
    public async Task AnEquivalenceTempestOSDerivedForItself_IsFlaggedAsNotSourceData()
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);

        var derived = await validator.ValidateDefinitionAsync(
            StandardFixtures.Dimensional() with
            {
                Equivalences = [new StandardEquivalence("FX-900", StandardEquivalenceKind.Identical, Body: "FXN", Origin: ReferenceValueOrigin.DerivedByTempestOS)],
            },
            StandardFixtures.Sourced());

        var unattributed = await validator.ValidateDefinitionAsync(
            StandardFixtures.Dimensional() with
            {
                Equivalences = [new StandardEquivalence("FX-900", StandardEquivalenceKind.Identical, Body: "FXN")],
            },
            StandardFixtures.Sourced());

        Assert.True(HasWarning(derived, ReferenceValidationRules.DerivedValuePresent));
        Assert.True(HasWarning(unattributed, StandardValidationRules.EquivalenceOriginShouldBeRecorded));
    }

    [Fact]
    public async Task AnUnresolvableNormativeReference_IsAWarningAgainstTheRegisterNotAnErrorInTheRecord()
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);

        await catalog.RegisterAsync(
            "std-0001",
            StandardFixtures.Dimensional() with
            {
                NormativeReferences = [new StandardReference("FX-800", StandardId: "std-missing", Body: "TFX")],
            },
            StandardFixtures.Sourced());

        var result = await validator.ValidateAsync("std-0001");

        Assert.True(HasWarning(result, ReferenceValidationRules.StandardReferenceUnresolved));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ASupersededRecordStillDescribingItsStandardAsCurrent_IsWarnedAbout()
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);

        await catalog.RegisterAsync("std-2018", StandardFixtures.Dimensional("FX-100", "2018"), StandardFixtures.Verified());
        await catalog.RegisterAsync("std-2026", StandardFixtures.Dimensional("FX-100", "2026"), StandardFixtures.Verified());
        await StandardFixtures.ReleaseAsync(catalog, "std-2018");
        await catalog.SupersedeAsync("std-2018", "std-2026", "Replaced by the 2026 edition.");

        var result = await validator.ValidateAsync("std-2018");

        Assert.True(HasWarning(result, StandardValidationRules.SupersededRecordStillMarkedCurrent));
    }

    [Fact]
    public async Task TheLibraryReport_CountsEveryRecordAndReportsOnlyTheOnesWithFindings()
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);

        await catalog.RegisterAsync("std-good", StandardFixtures.Dimensional("FX-100"), StandardFixtures.Verified());
        await catalog.RegisterAsync(
            "std-bare",
            new StandardDefinition { Body = StandardFixtures.Body(), Designation = "FX-200" },
            StandardFixtures.Verified());

        var report = await validator.ValidateLibraryAsync();

        Assert.Equal("Standards", report.Library);
        Assert.Equal(2, report.RecordsExamined);
        Assert.Equal(["std-bare"], report.Findings.Select(f => f.RecordId));
    }

    // ----------------------------------------------------------------
    // Lifecycle, end to end
    // ----------------------------------------------------------------

    [Fact]
    public async Task AnEditionSupersedingAnother_LeavesBothReadableAndTheOlderOneTraceable()
    {
        var catalog = StandardFixtures.BuildCatalog();
        var validator = new StandardValidationService(catalog);

        await catalog.RegisterAsync("std-2018", StandardFixtures.Dimensional("FX-100", "2018"), StandardFixtures.Verified());
        Assert.True((await validator.ValidateAsync("std-2018")).IsValid);
        await StandardFixtures.ReleaseAsync(catalog, "std-2018");

        // A released record is immutable: the new edition is a new record.
        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(
            () => catalog.ReviseAsync("std-2018", StandardFixtures.Dimensional("FX-100", "2026"), StandardFixtures.Verified(), "Refused."));

        await catalog.RegisterAsync(
            "std-2026",
            StandardFixtures.Dimensional("FX-100", "2026") with { ReplacesDesignations = ["FX-100:2018"] },
            StandardFixtures.Verified());
        await catalog.SupersedeAsync("std-2018", "std-2026", "Replaced by the 2026 edition.");

        var older = await catalog.FindAsync("std-2018");

        Assert.Equal(ReferenceValidationState.Superseded, older!.ValidationState);
        Assert.Equal("std-2026", older.SupersededByRecordId);
        Assert.Equal("2018", older.Definition.Edition);
        Assert.Equal(2, (await catalog.FindEditionsAsync("TFX", "FX-100")).Count);
    }

    [Fact]
    public async Task ARecordCannotBeReleasedOnProvenanceThatDoesNotSupportIt()
    {
        var catalog = StandardFixtures.BuildCatalog();
        await catalog.RegisterAsync("std-0001", StandardFixtures.Dimensional(), StandardFixtures.Sourced());
        await catalog.SetValidationStateAsync("std-0001", ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync("std-0001", ReferenceValidationState.Validated, "Rules pass.");

        await Assert.ThrowsAsync<ReferenceProvenanceIncompleteException>(
            () => catalog.SetValidationStateAsync("std-0001", ReferenceValidationState.Released, "Released."));
    }

    [Fact]
    public async Task EveryStandardRecordIsBackedByOneDocumentOfTheStandardsKind()
    {
        var catalog = StandardFixtures.BuildCatalog(out var documentStore, out _);
        var record = await catalog.RegisterAsync("std-0001", StandardFixtures.Dimensional(), StandardFixtures.Sourced());

        var document = await documentStore.FindAsync(record.UnderlyingDocumentId);

        Assert.Equal(StandardCatalog.StandardDocumentKind, document!.Kind);
    }
}
