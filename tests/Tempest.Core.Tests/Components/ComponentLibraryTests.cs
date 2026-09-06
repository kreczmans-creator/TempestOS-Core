using Tempest.Core.Components;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Components;

// A5: the component-specific half of the library. The shared machinery it
// sits on (registration, revision, lifecycle, supersession, hostile data)
// is tested once, in ReferenceDataCatalogTests, and is not restated here.
public class ComponentLibraryTests
{
    private static bool HasError(IValidationResult result, string code) => result.Errors.Any(d => d.Code == code);

    private static bool HasWarning(IValidationResult result, string code) => result.Warnings.Any(d => d.Code == code);

    // ----------------------------------------------------------------
    // Taxonomy and traits
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(ComponentFamily.CompressionSpring, ComponentGroup.Spring)]
    [InlineData(ComponentFamily.GasSpring, ComponentGroup.Spring)]
    [InlineData(ComponentFamily.HelicalGear, ComponentGroup.Gear)]
    [InlineData(ComponentFamily.GearRack, ComponentGroup.Gear)]
    [InlineData(ComponentFamily.RollerChain, ComponentGroup.DriveElement)]
    [InlineData(ComponentFamily.ShaftKey, ComponentGroup.ShaftElement)]
    [InlineData(ComponentFamily.RadialShaftSeal, ComponentGroup.Sealing)]
    [InlineData(ComponentFamily.BallScrew, ComponentGroup.MotionElement)]
    [InlineData(ComponentFamily.Other, ComponentGroup.Other)]
    [InlineData(ComponentFamily.Unspecified, ComponentGroup.Unspecified)]
    public void EveryFamilyBelongsToExactlyOneGroup(ComponentFamily family, ComponentGroup group) =>
        Assert.Equal(group, ComponentFamilyTraits.GroupOf(family));

    [Fact]
    public void TheGroupsPartitionTheTaxonomyWithNothingLeftOver()
    {
        var families = Enum.GetValues<ComponentFamily>();
        var grouped = Enum.GetValues<ComponentGroup>().SelectMany(ComponentFamilyTraits.FamiliesIn).ToList();

        Assert.Equal(families.Length, grouped.Count);
        Assert.Equal(families.Length, grouped.Distinct().Count());
    }

    [Fact]
    public void EachTypedDetailBelongsToExactlyOneGroup()
    {
        // The rule that makes one taxonomy over three kinds of component
        // safe: no family may carry two typed details.
        foreach (var family in Enum.GetValues<ComponentFamily>())
        {
            var applicable = new[]
            {
                ComponentFamilyTraits.HasSpringDetail(family),
                ComponentFamilyTraits.HasGearDetail(family),
                ComponentFamilyTraits.HasDriveElementDetail(family),
            }.Count(applies => applies);

            Assert.True(applicable <= 1, $"{family} claims {applicable} typed details.");
        }
    }

    [Fact]
    public void OnlyATorsionSpringHasATorquePerAngleRate()
    {
        Assert.True(ComponentFamilyTraits.HasTorsionalRate(ComponentFamily.TorsionSpring));

        foreach (var family in ComponentFamilyTraits.FamiliesIn(ComponentGroup.Spring).Where(f => f != ComponentFamily.TorsionSpring))
            Assert.False(ComponentFamilyTraits.HasTorsionalRate(family));
    }

    [Fact]
    public void FamilyTraits_RefuseToSpeakForAnUnclassifiedComponent()
    {
        Assert.False(ComponentFamilyTraits.IsApplicabilityKnown(ComponentFamily.Unspecified));
        Assert.False(ComponentFamilyTraits.IsApplicabilityKnown(ComponentFamily.Other));
        Assert.True(ComponentFamilyTraits.IsApplicabilityKnown(ComponentFamily.SpurGear));
    }

    [Fact]
    public void ADefinition_DefaultsEveryOptionalFieldToAbsent()
    {
        var definition = new ComponentDefinition { Family = ComponentFamily.ShaftCollar, Designation = "FX-1" };

        Assert.Null(definition.Spring);
        Assert.Null(definition.Gear);
        Assert.Null(definition.DriveElement);
        Assert.Null(definition.MaterialId);
        Assert.False(definition.Dimensions.IsRecorded);
        Assert.False(definition.Ratings.IsRecorded);
        Assert.Empty(definition.Standards);
        Assert.Equal(ComponentGroup.ShaftElement, definition.Group);
    }

    // ----------------------------------------------------------------
    // The torsional-stiffness dimension
    // ----------------------------------------------------------------

    [Fact]
    public void ATorsionalRateIsADimensionOfItsOwnAndConvertsWithinItself()
    {
        var perDegree = ComponentFixtures.NewtonMetresPerDegree(1);
        var perRadian = perDegree.ConvertTo(TorsionalStiffnessUnits.NewtonMetrePerRadian);

        Assert.Equal(180.0 / Math.PI, perRadian.Value, 9);
        Assert.Equal(perDegree.BaseValue, perRadian.BaseValue, 9);
    }

    [Fact]
    public void ATorsionalRateRoundTripsThroughTheSharedQuantityCodec()
    {
        var value = ComponentFixtures.NewtonMetresPerDegree(0.05);
        var decoded = ReferenceQuantityCodec.Decode(ReferenceQuantityCodec.Encode(value));

        Assert.Equal("TorsionalStiffness", ReferenceQuantityCodec.DimensionNameOf(value));
        Assert.Equal(value, Assert.IsType<Quantity<TorsionalStiffness>>(decoded));
    }

    // ----------------------------------------------------------------
    // Catalogue and search
    // ----------------------------------------------------------------

    [Fact]
    public async Task ComponentsOfEveryKindShareOneCatalogue()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        await catalog.RegisterAsync("cmp-spring", ComponentFixtures.CompressionSpring(), ComponentFixtures.SourcedProvenance());
        await catalog.RegisterAsync("cmp-gear", ComponentFixtures.SpurGear(), ComponentFixtures.SourcedProvenance());
        await catalog.RegisterAsync("cmp-pulley", ComponentFixtures.TimingPulley(), ComponentFixtures.SourcedProvenance());
        await catalog.RegisterAsync("cmp-coupling", ComponentFixtures.ShaftCoupling(), ComponentFixtures.SourcedProvenance());

        Assert.Equal(4, (await catalog.ListAsync()).Count);
        Assert.Equal("cmp-gear", (await catalog.FindByDesignationAsync(" fx-gear-1 ", "Fixture Components"))!.Id);
    }

    [Fact]
    public async Task Search_FindsEverySpringWithoutEnumeratingSixFamilies()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        await catalog.RegisterAsync("cmp-compression", ComponentFixtures.CompressionSpring(), ComponentFixtures.SourcedProvenance());
        await catalog.RegisterAsync("cmp-torsion", ComponentFixtures.TorsionSpring(), ComponentFixtures.SourcedProvenance());
        await catalog.RegisterAsync("cmp-gear", ComponentFixtures.SpurGear(), ComponentFixtures.SourcedProvenance());

        var springs = await catalog.SearchAsync(new ComponentQuery { Groups = [ComponentGroup.Spring] });

        Assert.Equal(["cmp-compression", "cmp-torsion"], springs.Select(c => c.Id).Order());
    }

    [Fact]
    public async Task Search_NeverMatchesAComponentThatDoesNotRecordTheValueBeingBounded()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        await catalog.RegisterAsync("cmp-spring", ComponentFixtures.CompressionSpring(), ComponentFixtures.SourcedProvenance());
        await catalog.RegisterAsync("cmp-gear", ComponentFixtures.SpurGear(), ComponentFixtures.SourcedProvenance());

        // A gear has no spring rate at all; it is not a candidate for a
        // rate bound, and an unrecorded value is never read as zero.
        Assert.Equal(
            ["cmp-spring"],
            (await catalog.SearchAsync(new ComponentQuery { SpringRateMinimum = ComponentFixtures.NewtonsPerMillimetre(0) })).Select(c => c.Id));
        Assert.Equal(
            ["cmp-gear"],
            (await catalog.SearchAsync(new ComponentQuery { NumberOfTeeth = 40 })).Select(c => c.Id));
    }

    [Fact]
    public async Task Search_MatchesGearAndDriveDetailAndRatings()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        await catalog.RegisterAsync("cmp-gear", ComponentFixtures.SpurGear(), ComponentFixtures.SourcedProvenance());
        await catalog.RegisterAsync("cmp-pulley", ComponentFixtures.TimingPulley(), ComponentFixtures.SourcedProvenance());
        await catalog.RegisterAsync("cmp-coupling", ComponentFixtures.ShaftCoupling(), ComponentFixtures.SourcedProvenance());

        Assert.Equal(
            ["cmp-gear"],
            (await catalog.SearchAsync(new ComponentQuery { ModuleMinimum = ComponentFixtures.Millimetres(1.5) })).Select(c => c.Id));
        Assert.Equal(
            ["cmp-pulley"],
            (await catalog.SearchAsync(new ComponentQuery { DriveProfileDesignation = "fx5m" })).Select(c => c.Id));
        Assert.Equal(
            ["cmp-coupling"],
            (await catalog.SearchAsync(new ComponentQuery { RatedTorqueMinimum = ComponentFixtures.NewtonMetres(50) })).Select(c => c.Id));
        Assert.Equal(
            ["cmp-coupling", "cmp-pulley"],
            (await catalog.SearchAsync(new ComponentQuery { MaximumSpeedMinimum = ComponentFixtures.RevolutionsPerMinute(5000) })).Select(c => c.Id).Order());
    }

    [Fact]
    public async Task Search_FiltersOnBoreAndValidationState()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        await catalog.RegisterAsync("cmp-gear", ComponentFixtures.SpurGear(), ComponentFixtures.VerifiedProvenance());
        await catalog.RegisterAsync("cmp-pulley", ComponentFixtures.TimingPulley(), ComponentFixtures.SourcedProvenance());
        await ComponentFixtures.ReleaseAsync(catalog, "cmp-gear");

        Assert.Equal(
            ["cmp-gear"],
            (await catalog.SearchAsync(new ComponentQuery { BoreDiameterMinimum = ComponentFixtures.Millimetres(14) })).Select(c => c.Id));
        Assert.Equal(
            ["cmp-gear"],
            (await catalog.SearchAsync(new ComponentQuery { ValidationStates = [ReferenceValidationState.Released] })).Select(c => c.Id));
    }

    // ----------------------------------------------------------------
    // Comparison
    // ----------------------------------------------------------------

    [Fact]
    public async Task Comparison_ReportsAToothCountOnASpringAsNotApplicable()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        await catalog.RegisterAsync("cmp-a-gear", ComponentFixtures.SpurGear(), ComponentFixtures.SourcedProvenance());
        await catalog.RegisterAsync("cmp-b-spring", ComponentFixtures.CompressionSpring(), ComponentFixtures.SourcedProvenance());

        var comparison = ComponentComparer.Compare(await catalog.ListAsync());
        var teeth = comparison.Row(ComponentComparisonProperties.NumberOfTeeth)!;
        var rate = comparison.Row(ComponentComparisonProperties.SpringRate)!;

        Assert.Equal(ReferencePropertyAvailability.Recorded, teeth.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, teeth.Cells[1].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, rate.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.Recorded, rate.Cells[1].Availability);
    }

    [Fact]
    public async Task Comparison_KeepsTheTwoFormsOfSpringRateInSeparateRows()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        await catalog.RegisterAsync("cmp-a-compression", ComponentFixtures.CompressionSpring(), ComponentFixtures.SourcedProvenance());
        await catalog.RegisterAsync("cmp-b-torsion", ComponentFixtures.TorsionSpring(), ComponentFixtures.SourcedProvenance());

        var comparison = ComponentComparer.Compare(await catalog.ListAsync());
        var linear = comparison.Row(ComponentComparisonProperties.SpringRate)!;
        var torsional = comparison.Row(ComponentComparisonProperties.TorsionalRate)!;

        Assert.Equal(ReferencePropertyAvailability.Recorded, linear.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, linear.Cells[1].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, torsional.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.Recorded, torsional.Cells[1].Availability);
    }

    [Fact]
    public async Task Comparison_ReportsANotApplicableDetailForAFamilyWithNoTypedDetailAtAll()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        await catalog.RegisterAsync("cmp-coupling", ComponentFixtures.ShaftCoupling(), ComponentFixtures.SourcedProvenance());

        var comparison = ComponentComparer.Compare(await catalog.ListAsync());

        Assert.Equal(ReferencePropertyAvailability.NotApplicable, comparison.Row(ComponentComparisonProperties.SpringRate)!.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, comparison.Row(ComponentComparisonProperties.NumberOfTeeth)!.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, comparison.Row(ComponentComparisonProperties.DriveProfile)!.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.Recorded, comparison.Row(ComponentComparisonProperties.RatedTorque)!.Cells[0].Availability);
    }

    [Fact]
    public async Task Comparison_OrdersDimensionedValuesByTheirCanonicalValue()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        await catalog.RegisterAsync("cmp-a", ComponentFixtures.SpurGear("FX-GEAR-20", 20), ComponentFixtures.SourcedProvenance());
        await catalog.RegisterAsync("cmp-b", ComponentFixtures.SpurGear("FX-GEAR-40", 40), ComponentFixtures.SourcedProvenance());

        var teeth = ComponentComparer.Compare(await catalog.ListAsync()).Row(ComponentComparisonProperties.NumberOfTeeth)!;

        Assert.Equal([20, 40], teeth.Cells.Select(c => c.CanonicalValue));
    }

    // ----------------------------------------------------------------
    // Validation — detail applicability
    // ----------------------------------------------------------------

    [Fact]
    public async Task EveryCoherentFixture_PassesEveryRule()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        await catalog.RegisterAsync("cmp-spring", ComponentFixtures.CompressionSpring(), ComponentFixtures.VerifiedProvenance());
        await catalog.RegisterAsync("cmp-torsion", ComponentFixtures.TorsionSpring(), ComponentFixtures.VerifiedProvenance());
        await catalog.RegisterAsync("cmp-gear", ComponentFixtures.SpurGear(), ComponentFixtures.VerifiedProvenance());
        await catalog.RegisterAsync("cmp-pulley", ComponentFixtures.TimingPulley(), ComponentFixtures.VerifiedProvenance());
        await catalog.RegisterAsync("cmp-coupling", ComponentFixtures.ShaftCoupling(), ComponentFixtures.VerifiedProvenance());

        var report = await validator.ValidateLibraryAsync();

        Assert.Equal(5, report.RecordsExamined);
        Assert.True(
            report.Findings.Count == 0,
            string.Join("; ", report.Findings.SelectMany(f => f.Result.Errors.Concat(f.Result.Warnings)).Select(d => $"{d.Code}: {d.Message}")));
    }

    [Fact]
    public async Task AGearDetailOnASpring_IsAModellingErrorNotADataGap()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.CompressionSpring() with { Spring = null, Gear = new GearDetail(NumberOfTeeth: 40) },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(result, ComponentValidationRules.DetailNotApplicableToFamily));
    }

    [Fact]
    public async Task TwoTypedDetailsAtOnce_IsAnError()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.SpurGear() with { Spring = new SpringDetail(Rate: ComponentFixtures.Sourced(ComponentFixtures.NewtonsPerMillimetre(5))) },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(result, ComponentValidationRules.MultipleDetailsRecorded));
    }

    [Fact]
    public async Task AFamilyWithATypedDetailButNoneRecorded_IsAGap()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.SpurGear() with { Gear = null },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ComponentValidationRules.DetailShouldBeRecordedForFamily));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task AFamilyWithNoTypedDetail_IsNeverAskedForOne()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(ComponentFixtures.ShaftCoupling(), ComponentFixtures.SourcedProvenance());

        Assert.False(HasWarning(result, ComponentValidationRules.DetailShouldBeRecordedForFamily));
    }

    // ----------------------------------------------------------------
    // Validation — springs
    // ----------------------------------------------------------------

    [Fact]
    public async Task ARateInTheWrongFormForItsFamily_IsAnError()
    {
        // The category error the units alone cannot catch, and the reason
        // TorsionalStiffness is a dimension of its own.
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var linearOnTorsion = await validator.ValidateDefinitionAsync(
            ComponentFixtures.TorsionSpring() with
            {
                Spring = ComponentFixtures.TorsionSpring().Spring! with
                {
                    TorsionalRate = null,
                    Rate = ComponentFixtures.Sourced(ComponentFixtures.NewtonsPerMillimetre(5)),
                },
            },
            ComponentFixtures.SourcedProvenance());

        var torsionalOnCompression = await validator.ValidateDefinitionAsync(
            ComponentFixtures.CompressionSpring() with
            {
                Spring = ComponentFixtures.CompressionSpring().Spring! with
                {
                    Rate = null,
                    TorsionalRate = ComponentFixtures.Sourced(ComponentFixtures.NewtonMetresPerDegree(0.05)),
                },
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(linearOnTorsion, ComponentValidationRules.SpringRateFormDoesNotMatchFamily));
        Assert.True(HasError(torsionalOnCompression, ComponentValidationRules.SpringRateFormDoesNotMatchFamily));
    }

    [Fact]
    public async Task ASpringWithNoTravel_IsAnError()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.CompressionSpring() with
            {
                Spring = ComponentFixtures.CompressionSpring().Spring! with
                {
                    SolidLength = ComponentFixtures.Sourced(ComponentFixtures.Millimetres(50)),
                },
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(result, ComponentValidationRules.SolidLengthNotShorterThanFreeLength));
    }

    [Fact]
    public async Task MoreActiveCoilsThanTotalCoils_IsAnError()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.CompressionSpring() with
            {
                Spring = ComponentFixtures.CompressionSpring().Spring! with
                {
                    ActiveCoils = ComponentFixtures.Sourced(ComponentFixtures.Count(12)),
                },
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(result, ComponentValidationRules.ActiveCoilsExceedTotalCoils));
    }

    [Fact]
    public async Task AWireDiameterThatDisagreesWithTheCoilDiameters_IsCaught()
    {
        // Outside minus inside is two wire diameters, exactly. A mismatch
        // means one of the three was mis-transcribed.
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.CompressionSpring() with
            {
                Spring = ComponentFixtures.CompressionSpring().Spring! with
                {
                    WireDiameter = ComponentFixtures.Sourced(ComponentFixtures.Millimetres(3)),
                },
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(result, ComponentValidationRules.WireDiameterInconsistentWithCoilDiameters));
    }

    [Fact]
    public async Task AHelicalSpringWithNoWindingDirection_IsWarnedAbout()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.CompressionSpring() with
            {
                Spring = ComponentFixtures.CompressionSpring().Spring! with { WindingDirection = SpringWindingDirection.Unspecified },
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ComponentValidationRules.HandednessShouldBeRecorded));
    }

    // ----------------------------------------------------------------
    // Validation — gears
    // ----------------------------------------------------------------

    [Fact]
    public async Task AnImpossiblePressureAngle_IsAnError()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.SpurGear() with
            {
                Gear = ComponentFixtures.SpurGear().Gear! with { PressureAngle = ComponentFixtures.Sourced(ComponentFixtures.Degrees(60)) },
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(result, ComponentValidationRules.PressureAngleOutOfRange));
    }

    [Fact]
    public async Task AHelixOnASpurGear_IsAnError()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var angled = await validator.ValidateDefinitionAsync(
            ComponentFixtures.SpurGear() with
            {
                Gear = ComponentFixtures.SpurGear().Gear! with { HelixAngle = ComponentFixtures.Sourced(ComponentFixtures.Degrees(15)) },
            },
            ComponentFixtures.SourcedProvenance());

        var handed = await validator.ValidateDefinitionAsync(
            ComponentFixtures.SpurGear() with
            {
                Gear = ComponentFixtures.SpurGear().Gear! with { HelixHand = GearHelixHand.RightHand },
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(angled, ComponentValidationRules.HelixAngleDoesNotMatchFamily));
        Assert.True(HasError(handed, ComponentValidationRules.HelixAngleDoesNotMatchFamily));
    }

    [Fact]
    public async Task AHelicalGearWithNoHelixHand_IsWarnedAbout()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.SpurGear() with
            {
                Family = ComponentFamily.HelicalGear,
                Gear = ComponentFixtures.SpurGear().Gear! with
                {
                    HelixAngle = ComponentFixtures.Sourced(ComponentFixtures.Degrees(15)),
                    HelixHand = GearHelixHand.Unspecified,
                },
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ComponentValidationRules.HandednessShouldBeRecorded));
    }

    [Fact]
    public async Task AnExternalGearWhoseTipsDoNotStandOutsideItsPitchCircle_IsAnError()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.SpurGear() with
            {
                Gear = ComponentFixtures.SpurGear().Gear! with { OutsideDiameter = ComponentFixtures.Sourced(ComponentFixtures.Millimetres(70)) },
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(result, ComponentValidationRules.OutsideDiameterNotGreaterThanPitchDiameter));
    }

    [Fact]
    public async Task AnInternalGear_IsNotHeldToTheExternalTipRule()
    {
        // An internal gear's teeth stand inside its reference cylinder,
        // which is why the rule is restricted rather than universal.
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.SpurGear() with
            {
                Family = ComponentFamily.InternalGear,
                Gear = ComponentFixtures.SpurGear().Gear! with
                {
                    HelixHand = GearHelixHand.None,
                    OutsideDiameter = ComponentFixtures.Sourced(ComponentFixtures.Millimetres(76)),
                },
            },
            ComponentFixtures.SourcedProvenance());

        Assert.False(HasError(result, ComponentValidationRules.OutsideDiameterNotGreaterThanPitchDiameter));
    }

    [Fact]
    public async Task ANonPositiveToothCount_IsAnError()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.SpurGear() with { Gear = ComponentFixtures.SpurGear().Gear! with { NumberOfTeeth = 0 } },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(result, ComponentValidationRules.ToothCountMustBePositive));
    }

    // ----------------------------------------------------------------
    // Validation — dimensions and ratings
    // ----------------------------------------------------------------

    [Fact]
    public async Task ABoreNoSmallerThanTheOutsideDiameter_IsAnError()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.ShaftCoupling() with
            {
                Dimensions = new ComponentDimensions(
                    BoreDiameter: ComponentFixtures.Sourced(ComponentFixtures.Millimetres(55)),
                    OutsideDiameter: ComponentFixtures.Sourced(ComponentFixtures.Millimetres(55))),
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(result, ComponentValidationRules.BoreNotSmallerThanOutsideDiameter));
    }

    [Fact]
    public async Task ABoreOnAFamilyThatHasNone_IsWarnedAbout()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            new ComponentDefinition
            {
                Family = ComponentFamily.ShaftKey,
                Designation = "FX-KEY-1",
                Dimensions = new ComponentDimensions(
                    BoreDiameter: ComponentFixtures.Sourced(ComponentFixtures.Millimetres(5)),
                    OutsideDiameter: ComponentFixtures.Sourced(ComponentFixtures.Millimetres(10))),
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ComponentValidationRules.BoreNotApplicableToFamily));
    }

    [Fact]
    public async Task ASpeedOrTorqueRatingOnAFamilyThatCarriesNeither_IsWarnedAbout()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.CompressionSpring() with
            {
                Ratings = new ComponentRatings(
                    MaximumSpeed: ComponentFixtures.Sourced(ComponentFixtures.RevolutionsPerMinute(1000)),
                    RatedTorque: ComponentFixtures.Sourced(ComponentFixtures.NewtonMetres(5))),
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ComponentValidationRules.SpeedRatingNotApplicableToFamily));
        Assert.True(HasWarning(result, ComponentValidationRules.TorqueRatingNotApplicableToFamily));
    }

    [Fact]
    public async Task ARatedTorqueAboveTheMaximumTorque_IsAnError()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.SpurGear() with
            {
                Ratings = ComponentFixtures.SpurGear().Ratings with { RatedTorque = ComponentFixtures.Sourced(ComponentFixtures.NewtonMetres(40)) },
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(result, ComponentValidationRules.RatedTorqueExceedsMaximumTorque));
    }

    [Fact]
    public async Task AnInvertedTemperatureRange_IsAnError()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.ShaftCoupling() with
            {
                Ratings = ComponentFixtures.ShaftCoupling().Ratings with
                {
                    OperatingTemperatureRange = new ReferenceRange<Temperature>(
                        new Quantity<Temperature>(120, TemperatureUnits.DegreeCelsius),
                        new Quantity<Temperature>(-30, TemperatureUnits.DegreeCelsius),
                        ReferenceValueOrigin.ManufacturerCatalogue),
                },
            },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(result, ReferenceValidationRules.RangeInverted));
    }

    [Fact]
    public async Task AComponentWithNoFamily_CannotBeInterpretedAndIsAnError()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            new ComponentDefinition { Family = ComponentFamily.Unspecified, Designation = "FX-1" },
            ComponentFixtures.SourcedProvenance());

        Assert.True(HasError(result, ComponentValidationRules.FamilyMustBeStated));
        Assert.True(HasWarning(result, ComponentValidationRules.NoEngineeringDataRecorded));
    }

    [Fact]
    public async Task AnUnclassifiedFamily_IsNeverToldItsDetailDoesNotApply()
    {
        // Conservative by construction: "not known to apply" must never be
        // reported as "known not to apply".
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ComponentFixtures.CompressionSpring() with
            {
                Family = ComponentFamily.Other,
                SourceClassification = "The source called it a 'fixture elastic element'.",
            },
            ComponentFixtures.SourcedProvenance());

        Assert.False(HasError(result, ComponentValidationRules.DetailNotApplicableToFamily));
        Assert.False(HasError(result, ComponentValidationRules.SpringRateFormDoesNotMatchFamily));
        Assert.Empty(result.Errors);
    }

    // ----------------------------------------------------------------
    // Integration with the rest of Group A
    // ----------------------------------------------------------------

    [Fact]
    public async Task ALinkedMaterialIsConfirmedAgainstTheCanonicalMaterialsCatalogue()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var materials = new MaterialCatalog(documentStore, persistenceStore);
        var components = new ComponentCatalog(documentStore, persistenceStore);
        var validator = new ComponentValidationService(components, materials);

        await materials.RegisterAsync(
            "mat-spring-steel",
            new MaterialDefinition { Name = "Fixture spring steel", Family = MaterialFamily.Steel },
            ComponentFixtures.SourcedProvenance());

        await components.RegisterAsync(
            "cmp-linked",
            ComponentFixtures.CompressionSpring() with { MaterialId = "mat-spring-steel" },
            ComponentFixtures.VerifiedProvenance());
        await components.RegisterAsync(
            "cmp-dangling",
            ComponentFixtures.CompressionSpring("FX-CSPR-2") with { MaterialId = "mat-missing" },
            ComponentFixtures.VerifiedProvenance());

        Assert.True((await validator.ValidateAsync("cmp-linked")).IsValid);
        Assert.True(HasWarning(await validator.ValidateAsync("cmp-dangling"), ReferenceValidationRules.MaterialReferenceUnresolved));
    }

    [Fact]
    public async Task EveryComponentRecordIsBackedByOneDocumentOfTheComponentKind()
    {
        var catalog = ComponentFixtures.BuildCatalog(out var documentStore, out _);
        var record = await catalog.RegisterAsync("cmp-0001", ComponentFixtures.SpurGear(), ComponentFixtures.SourcedProvenance());

        Assert.Equal(ComponentCatalog.ComponentDocumentKind, (await documentStore.FindAsync(record.UnderlyingDocumentId))!.Kind);
    }

    [Fact]
    public async Task AFullLifecycleFromDraftToSupersession_IsTraceableEndToEnd()
    {
        var catalog = ComponentFixtures.BuildCatalog();
        var validator = new ComponentValidationService(catalog);

        await catalog.RegisterAsync("cmp-0001", ComponentFixtures.SpurGear(), ComponentFixtures.VerifiedProvenance());
        Assert.True((await validator.ValidateAsync("cmp-0001")).IsValid);
        await ComponentFixtures.ReleaseAsync(catalog, "cmp-0001");

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(
            () => catalog.ReviseAsync("cmp-0001", ComponentFixtures.SpurGear(), ComponentFixtures.VerifiedProvenance(), "Refused."));

        await catalog.RegisterAsync("cmp-0002", ComponentFixtures.SpurGear("FX-GEAR-2"), ComponentFixtures.VerifiedProvenance());
        await catalog.SupersedeAsync("cmp-0001", "cmp-0002", "Superseded by fixture catalogue revision 2.");

        var superseded = await catalog.FindAsync("cmp-0001");

        Assert.Equal(ReferenceValidationState.Superseded, superseded!.ValidationState);
        Assert.Equal("cmp-0002", superseded.SupersededByRecordId);
        Assert.Equal(40, (await catalog.GetRevisionAsync("cmp-0001", 1)).Definition.Gear!.NumberOfTeeth);
    }
}
