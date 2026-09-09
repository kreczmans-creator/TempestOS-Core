using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.ReferenceData;

// The shared Group A value types and vocabularies: provenance, lifecycle,
// sourced values and ranges, standard citation, comparison cells, and the
// quantity codec the two dictionary-valued libraries need.
public class ReferenceDataModelTests
{
    // ----------------------------------------------------------------
    // Provenance
    // ----------------------------------------------------------------

    [Fact]
    public void Provenance_Unknown_IsHonestlyEmptyRatherThanGuessed()
    {
        var provenance = ReferenceProvenance.Unknown;

        Assert.Null(provenance.SourceOrganisation);
        Assert.Null(provenance.SourceDocument);
        Assert.Equal(ReferenceExtractionMethod.Unknown, provenance.ExtractionMethod);
        Assert.Equal(ReferenceVerificationStatus.NotVerified, provenance.VerificationStatus);
        Assert.False(provenance.IdentifiesASource);
        Assert.False(provenance.IsVerified);
    }

    [Fact]
    public void Provenance_AnImportedRecord_IsNotVerified()
    {
        var imported = ReferenceDataFixtures.Sourced() with { ExtractionMethod = ReferenceExtractionMethod.StructuredImport };

        Assert.True(imported.IdentifiesASource);
        Assert.False(imported.IsVerified);
    }

    [Fact]
    public void Provenance_MarkedVerifiedWithoutAReviewerOrDate_IsNotTreatedAsVerified()
    {
        var claimed = ReferenceDataFixtures.Sourced() with { VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource };
        var noDate = claimed with { ReviewerPrincipalId = "reviewer-1" };

        Assert.False(claimed.IsVerified);
        Assert.False(noDate.IsVerified);
        Assert.True(ReferenceDataFixtures.Verified().IsVerified);
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
    public void PermittedTransitions(ReferenceValidationState from, ReferenceValidationState to) =>
        Assert.True(ReferenceValidationStates.IsPermitted(from, to));

    [Theory]
    [InlineData(ReferenceValidationState.Draft, ReferenceValidationState.Released)]
    [InlineData(ReferenceValidationState.Draft, ReferenceValidationState.Validated)]
    [InlineData(ReferenceValidationState.Checked, ReferenceValidationState.Released)]
    [InlineData(ReferenceValidationState.Released, ReferenceValidationState.Draft)]
    [InlineData(ReferenceValidationState.Superseded, ReferenceValidationState.Released)]
    [InlineData(ReferenceValidationState.Draft, ReferenceValidationState.Draft)]
    public void RejectedTransitions(ReferenceValidationState from, ReferenceValidationState to) =>
        Assert.False(ReferenceValidationStates.IsPermitted(from, to));

    [Fact]
    public void SupersededIsTerminal() =>
        Assert.Empty(ReferenceValidationStates.GetPermittedTargets(ReferenceValidationState.Superseded));

    [Theory]
    [InlineData(ReferenceValidationState.Draft, LifecycleState.Draft)]
    [InlineData(ReferenceValidationState.Checked, LifecycleState.InReview)]
    [InlineData(ReferenceValidationState.Validated, LifecycleState.Approved)]
    [InlineData(ReferenceValidationState.Released, LifecycleState.Released)]
    [InlineData(ReferenceValidationState.Superseded, LifecycleState.Superseded)]
    public void StatesMapOntoThePlatformsOwnCanonicalVocabulary(ReferenceValidationState state, LifecycleState expected) =>
        Assert.Equal(expected, ReferenceValidationStates.CanonicalEquivalent(state));

    [Theory]
    [InlineData(ReferenceValidationState.Draft, true)]
    [InlineData(ReferenceValidationState.Validated, true)]
    [InlineData(ReferenceValidationState.Released, false)]
    [InlineData(ReferenceValidationState.Superseded, false)]
    public void ReleasedAndSupersededRecordsAreNotRevisable(ReferenceValidationState state, bool expected) =>
        Assert.Equal(expected, ReferenceValidationStates.IsRevisable(state));

    [Fact]
    public void ProvenanceShortfall_DescribesExactlyWhatIsMissing()
    {
        Assert.Null(ReferenceValidationStates.DescribeProvenanceShortfall(ReferenceProvenance.Unknown, ReferenceValidationState.Draft));
        Assert.Contains("source", ReferenceValidationStates.DescribeProvenanceShortfall(ReferenceProvenance.Unknown, ReferenceValidationState.Checked)!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(ReferenceValidationStates.DescribeProvenanceShortfall(ReferenceDataFixtures.Sourced(), ReferenceValidationState.Checked));
        Assert.Contains("verified", ReferenceValidationStates.DescribeProvenanceShortfall(ReferenceDataFixtures.Sourced(), ReferenceValidationState.Released)!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(ReferenceValidationStates.DescribeProvenanceShortfall(ReferenceDataFixtures.Verified(), ReferenceValidationState.Released));
    }

    // ----------------------------------------------------------------
    // Sourced values and ranges
    // ----------------------------------------------------------------

    [Fact]
    public void AValue_KeepsTheUnitTheSourceQuotedAndStillOrdersCanonically()
    {
        var value = new ReferenceValue<Force>(new Quantity<Force>(4.6, ForceUnits.Kilonewton), ReferenceValueOrigin.ManufacturerCatalogue);

        Assert.Equal("kN", value.Value.Unit.Symbol);
        Assert.Equal(4600.0, value.CanonicalValue, 9);
        Assert.False(value.IsDerived);
    }

    [Fact]
    public void ADerivedValue_IsDistinguishableFromASourcedOne()
    {
        var derived = new ReferenceValue<Force>(new Quantity<Force>(1, ForceUnits.Newton), ReferenceValueOrigin.DerivedByTempestOS);

        Assert.True(derived.IsDerived);
    }

    [Fact]
    public void ARange_WithBothEnds_ContainsWhatItShould()
    {
        var range = new ReferenceRange<Length>(
            new Quantity<Length>(10, LengthUnits.Millimetre),
            new Quantity<Length>(20, LengthUnits.Millimetre),
            ReferenceValueOrigin.Standard);

        Assert.True(range.IsRecorded);
        Assert.False(range.IsInverted);
        Assert.True(range.Contains(new Quantity<Length>(15, LengthUnits.Millimetre)));
        Assert.True(range.Contains(new Quantity<Length>(10, LengthUnits.Millimetre)));
        Assert.False(range.Contains(new Quantity<Length>(25, LengthUnits.Millimetre)));
    }

    [Fact]
    public void ARange_ComparesAcrossUnits()
    {
        var range = new ReferenceRange<Length>(
            new Quantity<Length>(10, LengthUnits.Millimetre),
            new Quantity<Length>(50, LengthUnits.Millimetre),
            ReferenceValueOrigin.Standard);

        Assert.True(range.Contains(new Quantity<Length>(1, LengthUnits.Inch)));
        Assert.False(range.Contains(new Quantity<Length>(3, LengthUnits.Inch)));
    }

    [Fact]
    public void AnOpenEndedRange_IsGenuinelyOpenNotZeroBounded()
    {
        // "up to 20 mm" must not silently mean "0 to 20 mm": a negative or
        // very small value is inside it, because the source set no floor.
        var maximumOnly = new ReferenceRange<Length>(null, new Quantity<Length>(20, LengthUnits.Millimetre), ReferenceValueOrigin.Standard);

        Assert.True(maximumOnly.IsRecorded);
        Assert.True(maximumOnly.Contains(new Quantity<Length>(0.001, LengthUnits.Millimetre)));
        Assert.False(maximumOnly.Contains(new Quantity<Length>(21, LengthUnits.Millimetre)));
    }

    [Fact]
    public void ARangeWithNeitherEnd_IsNotRecordedAndConstrainsNothing()
    {
        var empty = new ReferenceRange<Length>(null, null, ReferenceValueOrigin.Unknown);

        Assert.False(empty.IsRecorded);
        Assert.True(empty.Contains(new Quantity<Length>(1000, LengthUnits.Metre)));
    }

    [Fact]
    public void AnInvertedRange_IsDetected()
    {
        var inverted = new ReferenceRange<Length>(
            new Quantity<Length>(20, LengthUnits.Millimetre),
            new Quantity<Length>(10, LengthUnits.Millimetre),
            ReferenceValueOrigin.Standard);

        Assert.True(inverted.IsInverted);
    }

    // ----------------------------------------------------------------
    // Standard citation
    // ----------------------------------------------------------------

    [Fact]
    public void AStandardReference_RequiresADesignationAndReportsWhetherItResolves()
    {
        Assert.Throws<ArgumentException>(() => new StandardReference("  "));

        var unresolved = new StandardReference("Fixture standard 1");
        var resolved = new StandardReference("Fixture standard 1", StandardId: "std-1");

        Assert.False(unresolved.IsResolved);
        Assert.True(resolved.IsResolved);
    }

    // ----------------------------------------------------------------
    // Comparison primitives
    // ----------------------------------------------------------------

    [Fact]
    public void ComparisonCells_DistinguishNotRecordedFromNotApplicable()
    {
        Assert.Equal(ReferencePropertyAvailability.NotRecorded, ReferenceComparisonCell.Text(null).Availability);
        Assert.Equal(ReferencePropertyAvailability.NotRecorded, ReferenceComparisonCell.Text("   ").Availability);
        Assert.Equal(ReferencePropertyAvailability.Recorded, ReferenceComparisonCell.Text("x").Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, ReferenceComparisonCell.Applicable("x", applies: false, applicabilityKnown: true).Availability);
    }

    [Fact]
    public void AnUnclassifiedFamilysOwnConservativeFalse_IsNotReadAsNotApplicable()
    {
        // applicabilityKnown false means "we cannot say", so the cell must
        // fall through to the ordinary recorded/not-recorded answer.
        Assert.Equal(
            ReferencePropertyAvailability.Recorded,
            ReferenceComparisonCell.Applicable("x", applies: false, applicabilityKnown: false).Availability);
    }

    [Fact]
    public void ARangeCell_DisplaysBothEndsAndOrdersByItsLowerEnd()
    {
        var cell = ReferenceComparer.Ranged(new ReferenceRange<Length>(
            new Quantity<Length>(10, LengthUnits.Millimetre),
            new Quantity<Length>(20, LengthUnits.Millimetre),
            ReferenceValueOrigin.Standard));

        Assert.Equal("10 mm to 20 mm", cell.Display);
        Assert.Equal(0.010, cell.CanonicalValue!.Value, 12);
    }

    [Fact]
    public void AnOpenEndedRangeCell_SaysSo()
    {
        Assert.Equal(
            "up to 20 mm",
            ReferenceComparer.Ranged(new ReferenceRange<Length>(null, new Quantity<Length>(20, LengthUnits.Millimetre), ReferenceValueOrigin.Standard)).Display);
        Assert.Equal(
            "10 mm or more",
            ReferenceComparer.Ranged(new ReferenceRange<Length>(new Quantity<Length>(10, LengthUnits.Millimetre), null, ReferenceValueOrigin.Standard)).Display);
    }

    [Fact]
    public void Compare_RejectsAnEmptyOrNullCandidateSet()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ReferenceComparer.Compare<WidgetDefinition>(null!, ["X"], (_, _) => ReferenceComparisonCell.NotRecorded));
        Assert.Throws<ArgumentException>(() =>
            ReferenceComparer.Compare<WidgetDefinition>([], ["X"], (_, _) => ReferenceComparisonCell.NotRecorded));
    }

    // ----------------------------------------------------------------
    // Quantity codec
    // ----------------------------------------------------------------

    [Fact]
    public void Codec_RoundTripsEveryDimensionThisFrameworkDefines()
    {
        object[] values =
        [
            new Quantity<Length>(5, LengthUnits.Millimetre),
            new Quantity<Mass>(2, MassUnits.Kilogram),
            new Quantity<Duration>(3, DurationUnits.Hour),
            new Quantity<Force>(4, ForceUnits.Kilonewton),
            new Quantity<Pressure>(210, PressureUnits.Megapascal),
            new Quantity<Area>(7, AreaUnits.SquareMetre),
            new Quantity<Volume>(8, VolumeUnits.CubicMetre),
            new Quantity<RotationalSpeed>(3000, RotationalSpeedUnits.RevolutionPerMinute),
            new Quantity<PlaneAngle>(15, PlaneAngleUnits.Degree),
            new Quantity<Temperature>(150, TemperatureUnits.DegreeCelsius),
            new Quantity<MassDensity>(7.85, MassDensityUnits.GramPerCubicCentimetre),
            new Quantity<Stiffness>(12, StiffnessUnits.NewtonPerMillimetre),
            new Quantity<Torque>(40, TorqueUnits.NewtonMetre),
            new Quantity<ThermalConductivity>(50, ThermalConductivityUnits.WattPerMetreKelvin),
            new Quantity<ThermalExpansion>(11.7, ThermalExpansionUnits.MicrometrePerMetreKelvin),
            new Quantity<SpecificHeatCapacity>(460, SpecificHeatCapacityUnits.JoulePerKilogramKelvin),
            new Quantity<Acceleration>(1, AccelerationUnits.StandardGravity),
            new Quantity<Energy>(27, EnergyUnits.Joule),
            new Quantity<Velocity>(120, VelocityUnits.MetrePerMinute),
            new Quantity<Dimensionless>(0.3, DimensionlessUnits.One),
        ];

        foreach (var value in values)
        {
            Assert.True(ReferenceQuantityCodec.IsSupported(value));
            Assert.Equal(value, ReferenceQuantityCodec.Decode(ReferenceQuantityCodec.Encode(value)));
        }
    }

    [Fact]
    public void Codec_RoundTripsAnAffineUnitWithItsOffsetIntact()
    {
        var original = new Quantity<Temperature>(150, TemperatureUnits.DegreeCelsius);

        var encoded = ReferenceQuantityCodec.Encode(original);
        var decoded = (Quantity<Temperature>)ReferenceQuantityCodec.Decode(encoded);

        Assert.Equal(273.15, encoded.UnitToBaseOffset, 9);
        Assert.Equal(423.15, decoded.ConvertTo(TemperatureUnits.Kelvin).Value, 9);
    }

    [Fact]
    public void Codec_CanonicalValue_HonoursTheOffset()
    {
        Assert.Equal(423.15, ReferenceQuantityCodec.CanonicalValueOf(new Quantity<Temperature>(150, TemperatureUnits.DegreeCelsius)), 9);
        Assert.Equal(0.005, ReferenceQuantityCodec.CanonicalValueOf(new Quantity<Length>(5, LengthUnits.Millimetre)), 12);
    }

    [Fact]
    public void Codec_RefusesAnUnsupportedValue()
    {
        Assert.False(ReferenceQuantityCodec.IsSupported("not a quantity"));
        Assert.Null(ReferenceQuantityCodec.DimensionNameOf(42));
        Assert.Throws<ReferenceDataException>(() => ReferenceQuantityCodec.Encode("not a quantity"));
    }

    [Fact]
    public void Codec_RefusesAStoredDimensionThisBuildDoesNotRecognise()
    {
        // Enum-as-string and name-keyed dimensions mean a record written by
        // a later version is reported, never silently reinterpreted.
        var exception = Assert.Throws<ReferenceDataException>(
            () => ReferenceQuantityCodec.Decode(new EncodedQuantity("Luminance", 1, "cd/m2", 1)));

        Assert.Contains("Luminance", exception.Message, StringComparison.Ordinal);
    }
}
