using Tempest.Core.Bearings;
using Tempest.Core.ReferenceData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Bearings;

// Model, taxonomy and vocabulary tests: the canonical Bearing object's own
// construction rules, the family taxonomy's own applicability model, and
// the "missing is never zero" discipline the whole library rests on.
public class BearingModelTests
{
    // ----------------------------------------------------------------
    // Identity
    // ----------------------------------------------------------------

    [Fact]
    public void BearingIdentity_ValidConstruction_KeepsEveryFieldAsGiven()
    {
        var identity = new BearingIdentity("TestFixture Bearings", "FX-6000", "FX-6000-2RS", "FX-60", "A");

        Assert.Equal("TestFixture Bearings", identity.Manufacturer);
        Assert.Equal("FX-6000", identity.ManufacturerPartNumber);
        Assert.Equal("FX-6000-2RS", identity.Designation);
        Assert.Equal("FX-60", identity.Series);
        Assert.Equal("A", identity.Variant);
        Assert.Empty(identity.EquivalentReferences);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BearingIdentity_WithoutManufacturer_Throws(string manufacturer)
    {
        Assert.Throws<ArgumentException>(() => new BearingIdentity(manufacturer, "FX-6000"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BearingIdentity_WithoutPartNumber_Throws(string partNumber)
    {
        Assert.Throws<ArgumentException>(() => new BearingIdentity("TestFixture Bearings", partNumber));
    }

    [Fact]
    public void BearingIdentity_PartNumberKey_IgnoresCaseAndSurroundingWhitespace()
    {
        var first = new BearingIdentity(" TestFixture Bearings ", " fx-6000 ");
        var second = new BearingIdentity("TESTFIXTURE BEARINGS", "FX-6000");

        Assert.Equal(second.PartNumberKey, first.PartNumberKey);
    }

    [Fact]
    public void BearingIdentity_PartNumberKey_DistinguishesManufacturersSharingAPartNumber()
    {
        var first = new BearingIdentity("TestFixture Bearings", "6000");
        var second = new BearingIdentity("Other Fixture Bearings", "6000");

        Assert.NotEqual(second.PartNumberKey, first.PartNumberKey);
    }

    [Fact]
    public void BearingEquivalentReference_WithoutAClaimant_Throws()
    {
        // An equivalence with nobody behind it would be TempestOS
        // asserting interchangeability on its own authority.
        Assert.Throws<ArgumentException>(() => new BearingEquivalentReference("Other Fixture Bearings", "6000", "  "));
    }

    [Fact]
    public void BearingEquivalentReference_RecordsTheClaimant()
    {
        var equivalent = new BearingEquivalentReference("Other Fixture Bearings", "6000", "Fixture cross-reference table");

        Assert.Equal("Fixture cross-reference table", equivalent.ClaimedBy);
    }

    // ----------------------------------------------------------------
    // Definition
    // ----------------------------------------------------------------

    [Fact]
    public void BearingDefinition_OptionalFields_DefaultToAbsentNeverToZero()
    {
        var definition = new BearingDefinition
        {
            Identity = BearingFixtures.Identity(),
            Family = BearingFamily.DeepGrooveBall,
            Geometry = new BearingGeometry(),
        };

        Assert.Null(definition.LoadRatings);
        Assert.Null(definition.Configuration);
        Assert.Null(definition.Construction);
        Assert.Null(definition.Lubrication);
        Assert.Null(definition.Mass);
        Assert.Null(definition.EffectiveDate);
        Assert.Null(definition.Notes);
        Assert.Empty(definition.SpeedRatings);
        Assert.Empty(definition.Standards);
        Assert.Empty(definition.ManufacturerAttributes);
    }

    [Fact]
    public void BearingDefinition_ManufacturerAttributes_AreKeptVerbatimRatherThanNormalisedAway()
    {
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            ManufacturerAttributes = new Dictionary<string, string> { ["Fixture cage code"] = "TN9" },
        };

        Assert.Equal("TN9", definition.ManufacturerAttributes["Fixture cage code"]);
    }

    [Fact]
    public void BearingGeometry_AdditionalDimensions_HoldFamilySpecificDimensionsWithoutAFixedSuperset()
    {
        var geometry = new BearingGeometry(
            Bore: BearingFixtures.Millimetres(10),
            OutsideDiameter: BearingFixtures.Millimetres(30),
            AdditionalDimensions: new Dictionary<string, Quantity<Length>>
            {
                ["da min"] = BearingFixtures.Millimetres(12),
                ["Da max"] = BearingFixtures.Millimetres(28),
            });

        Assert.Equal(2, geometry.AdditionalDimensions.Count);
        Assert.Equal(BearingFixtures.Millimetres(12), geometry.AdditionalDimensions["da min"]);
    }

    [Fact]
    public void BearingGeometry_ToMetres_ConvertsThroughTheUnitsFrameworkNotByHand()
    {
        Assert.Equal(0.01, BearingGeometry.ToMetres(BearingFixtures.Millimetres(10))!.Value, 12);
        Assert.Equal(0.0254, BearingGeometry.ToMetres(new Quantity<Length>(1.0, LengthUnits.Inch))!.Value, 12);
    }

    [Fact]
    public void BearingGeometry_ToMetres_ReportsAnUnrecordedDimensionAsAbsentNotAsZero()
    {
        Assert.Null(BearingGeometry.ToMetres(null));
    }

    // ----------------------------------------------------------------
    // Rated values: origin is never optional
    // ----------------------------------------------------------------

    [Fact]
    public void BearingRatedValue_CanonicalValue_ConvertsToTheDimensionsOwnBaseUnit()
    {
        var rating = new ReferenceValue<Force>(BearingFixtures.Kilonewtons(4.6), ReferenceValueOrigin.ManufacturerCatalogue);

        Assert.Equal(4600.0, rating.CanonicalValue, 9);
    }

    [Fact]
    public void BearingRatedValue_KeepsTheUnitTheSourceQuoted()
    {
        var rating = new ReferenceValue<Force>(BearingFixtures.Kilonewtons(4.6), ReferenceValueOrigin.ManufacturerCatalogue);

        Assert.Equal("kN", rating.Value.Unit.Symbol);
        Assert.Equal(4.6, rating.Value.Value);
    }

    [Fact]
    public void BearingRatedValue_DerivedValues_AreDistinguishableFromManufacturerData()
    {
        var manufacturer = new ReferenceValue<Force>(BearingFixtures.Kilonewtons(4.6), ReferenceValueOrigin.ManufacturerCatalogue);
        var derived = new ReferenceValue<Force>(BearingFixtures.Kilonewtons(4.6), ReferenceValueOrigin.DerivedByTempestOS);

        Assert.NotEqual(manufacturer.Origin, derived.Origin);
        Assert.Equal(ReferenceValueOrigin.DerivedByTempestOS, derived.Origin);
    }

    [Fact]
    public void BearingProvenance_Unknown_IsHonestlyEmptyRatherThanGuessed()
    {
        var provenance = ReferenceProvenance.Unknown;

        Assert.Null(provenance.SourceOrganisation);
        Assert.Null(provenance.SourceDocument);
        Assert.Null(provenance.ReviewerPrincipalId);
        Assert.Equal(ReferenceExtractionMethod.Unknown, provenance.ExtractionMethod);
        Assert.Equal(ReferenceVerificationStatus.NotVerified, provenance.VerificationStatus);
        Assert.False(provenance.IdentifiesASource);
        Assert.False(provenance.IsVerified);
    }

    [Fact]
    public void BearingProvenance_MarkedVerifiedWithoutAReviewer_IsNotTreatedAsVerified()
    {
        var provenance = BearingFixtures.SourcedProvenance() with
        {
            VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        };

        Assert.False(provenance.IsVerified);
    }

    [Fact]
    public void BearingProvenance_VerifiedByANamedReviewerOnADate_IsVerified()
    {
        Assert.True(BearingFixtures.VerifiedProvenance().IsVerified);
    }

    // ----------------------------------------------------------------
    // Taxonomy and type-aware applicability
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(BearingFamily.AngularContactBall)]
    [InlineData(BearingFamily.TaperedRoller)]
    [InlineData(BearingFamily.SphericalRoller)]
    [InlineData(BearingFamily.ThrustRoller)]
    public void BearingFamilyTraits_ContactAngleFamilies_HaveAContactAngle(BearingFamily family)
    {
        Assert.True(BearingFamilyTraits.HasContactAngle(family));
    }

    [Theory]
    [InlineData(BearingFamily.DeepGrooveBall)]
    [InlineData(BearingFamily.CylindricalRoller)]
    [InlineData(BearingFamily.NeedleRoller)]
    [InlineData(BearingFamily.Plain)]
    public void BearingFamilyTraits_OtherFamilies_HaveNoContactAngle(BearingFamily family)
    {
        Assert.False(BearingFamilyTraits.HasContactAngle(family));
    }

    [Fact]
    public void BearingFamilyTraits_PlainBearings_HaveNoRollingElementsCageOrClearance()
    {
        Assert.False(BearingFamilyTraits.HasRollingElements(BearingFamily.Plain));
        Assert.False(BearingFamilyTraits.HasCage(BearingFamily.Plain));
        Assert.False(BearingFamilyTraits.HasInternalClearance(BearingFamily.Plain));
        Assert.False(BearingFamilyTraits.HasRowConfiguration(BearingFamily.Plain));
    }

    [Theory]
    [InlineData(BearingFamily.ThrustBall)]
    [InlineData(BearingFamily.ThrustRoller)]
    public void BearingFamilyTraits_ThrustFamilies_AreIdentifiedAsSuch(BearingFamily family)
    {
        Assert.True(BearingFamilyTraits.IsThrustBearing(family));
    }

    [Theory]
    [InlineData(BearingFamily.Unspecified)]
    [InlineData(BearingFamily.Other)]
    public void BearingFamilyTraits_UnclassifiedFamilies_ReportApplicabilityAsUnknown(BearingFamily family)
    {
        // The conservative false the trait methods return for these two
        // must be read as "not known to apply", never "known not to apply"
        // — this is the flag that keeps a caller from doing the latter.
        Assert.False(BearingFamilyTraits.IsApplicabilityKnown(family));
    }

    [Fact]
    public void BearingFamilyTraits_EveryClassifiedFamily_IsCoveredByTheApplicabilityTable()
    {
        // Guards the taxonomy's own extensibility claim: adding a family
        // without giving it a traits row is caught here rather than
        // surfacing as a silently-wrong applicability answer later.
        var classified = Enum.GetValues<BearingFamily>()
            .Where(BearingFamilyTraits.IsApplicabilityKnown)
            .ToList();

        Assert.Equal(Enum.GetValues<BearingFamily>().Length - 2, classified.Count);

        foreach (var family in classified)
        {
            // Every classified family must answer consistently: a family
            // with no rolling elements can have neither cage, clearance,
            // nor rows.
            if (!BearingFamilyTraits.HasRollingElements(family))
            {
                Assert.False(BearingFamilyTraits.HasCage(family));
                Assert.False(BearingFamilyTraits.HasInternalClearance(family));
                Assert.False(BearingFamilyTraits.HasRowConfiguration(family));
            }
        }
    }

    // ----------------------------------------------------------------
    // Sealing: manufacturer terminology survives the mapping
    // ----------------------------------------------------------------

    [Fact]
    public void BearingSealingArrangement_KeepsTheManufacturersOwnDesignationAlongsideTheClassification()
    {
        var sealing = new BearingSealingArrangement(BearingSealingType.ContactSeal, "FX-2RS", SidesSealed: 2);

        Assert.Equal(BearingSealingType.ContactSeal, sealing.Type);
        Assert.Equal("FX-2RS", sealing.ManufacturerDesignation);
        Assert.Equal(2, sealing.SidesSealed);
    }

    [Fact]
    public void BearingSealingArrangement_AnUnmappableDesignation_StaysUnspecifiedRatherThanGuessed()
    {
        var sealing = new BearingSealingArrangement(BearingSealingType.Unspecified, "FX-QQ");

        Assert.Equal(BearingSealingType.Unspecified, sealing.Type);
        Assert.Equal("FX-QQ", sealing.ManufacturerDesignation);
    }

    // ----------------------------------------------------------------
    // Lifecycle vocabulary
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(ReferenceValidationState.Draft, ReferenceValidationState.Checked)]
    [InlineData(ReferenceValidationState.Checked, ReferenceValidationState.Validated)]
    [InlineData(ReferenceValidationState.Checked, ReferenceValidationState.Draft)]
    [InlineData(ReferenceValidationState.Validated, ReferenceValidationState.Released)]
    [InlineData(ReferenceValidationState.Validated, ReferenceValidationState.Checked)]
    [InlineData(ReferenceValidationState.Released, ReferenceValidationState.Superseded)]
    public void BearingValidationStates_PermittedTransitions(ReferenceValidationState from, ReferenceValidationState to)
    {
        Assert.True(ReferenceValidationStates.IsPermitted(from, to));
    }

    [Theory]
    [InlineData(ReferenceValidationState.Draft, ReferenceValidationState.Released)]
    [InlineData(ReferenceValidationState.Draft, ReferenceValidationState.Validated)]
    [InlineData(ReferenceValidationState.Checked, ReferenceValidationState.Released)]
    [InlineData(ReferenceValidationState.Released, ReferenceValidationState.Draft)]
    [InlineData(ReferenceValidationState.Released, ReferenceValidationState.Validated)]
    [InlineData(ReferenceValidationState.Superseded, ReferenceValidationState.Released)]
    [InlineData(ReferenceValidationState.Draft, ReferenceValidationState.Draft)]
    public void BearingValidationStates_RejectedTransitions(ReferenceValidationState from, ReferenceValidationState to)
    {
        Assert.False(ReferenceValidationStates.IsPermitted(from, to));
    }

    [Fact]
    public void BearingValidationStates_SupersededIsTerminal()
    {
        Assert.Empty(ReferenceValidationStates.GetPermittedTargets(ReferenceValidationState.Superseded));
    }

    [Theory]
    [InlineData(ReferenceValidationState.Draft, LifecycleState.Draft)]
    [InlineData(ReferenceValidationState.Checked, LifecycleState.InReview)]
    [InlineData(ReferenceValidationState.Validated, LifecycleState.Approved)]
    [InlineData(ReferenceValidationState.Released, LifecycleState.Released)]
    [InlineData(ReferenceValidationState.Superseded, LifecycleState.Superseded)]
    public void BearingValidationStates_MapOntoThePlatformsOwnCanonicalVocabulary(ReferenceValidationState state, LifecycleState expected)
    {
        // ADR-0074: a family-specific specialisation of the canonical
        // vocabulary, never a competing parallel state model.
        Assert.Equal(expected, ReferenceValidationStates.CanonicalEquivalent(state));
    }

    [Theory]
    [InlineData(ReferenceValidationState.Draft, true)]
    [InlineData(ReferenceValidationState.Checked, true)]
    [InlineData(ReferenceValidationState.Validated, true)]
    [InlineData(ReferenceValidationState.Released, false)]
    [InlineData(ReferenceValidationState.Superseded, false)]
    public void BearingValidationStates_ReleasedAndSupersededRecordsAreNotRevisable(ReferenceValidationState state, bool expected)
    {
        Assert.Equal(expected, ReferenceValidationStates.IsRevisable(state));
    }

    [Fact]
    public void BearingStandardReference_WithoutADesignation_Throws()
    {
        Assert.Throws<ArgumentException>(() => new StandardReference("  "));
    }

    [Fact]
    public void BearingConstruction_ReferencedMaterialIds_ListsOnlyWhatIsActuallyRecorded()
    {
        var construction = new BearingConstruction(
            RingMaterialId: "steel-100cr6",
            RollingElementMaterialId: null,
            CageMaterialId: "polyamide-66");

        Assert.Equal(["steel-100cr6", "polyamide-66"], construction.ReferencedMaterialIds);
    }

    [Fact]
    public void BearingConstruction_WithNoMaterialsRecorded_ReferencesNone()
    {
        Assert.Empty(new BearingConstruction().ReferencedMaterialIds);
    }
}
