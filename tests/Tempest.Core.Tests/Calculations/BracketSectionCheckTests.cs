using Tempest.Core.Calculations;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations;

// Numerical verification of the bracket section check, against expected
// values derived by hand from first principles rather than by running the
// code and recording what it said.
//
// The derivations, in full, so a reviewer can repeat them without a
// calculator and without reading the implementation:
//
//   Geometry used throughout: A = 60 mm2 = 60e-6 m2 = 6.0e-5 m2
//                             L = 150 mm = 0.15 m
//
//   NOMINAL, 6082-T6 (allowable 260 MPa, density 2700 kg/m3):
//     sigma  = F / A       = 12 000 N / 6.0e-5 m2 = 2.0e8 Pa      = 200 MPa
//     margin = 260/200 - 1 = 1.3 - 1                              = 0.30
//     mass   = 2700 * 6.0e-5 * 0.15 = 2700 * 9.0e-6               = 0.0243 kg
//
//   BOUNDARY, margin exactly zero:
//     F such that sigma = 260 MPa: F = 2.6e8 * 6.0e-5             = 15 600 N
//     margin = 260/260 - 1                                        = 0.00
//
//   STRESS FAILURE:
//     F = 18 000 N -> sigma = 1.8e4 / 6.0e-5 = 3.0e8 Pa           = 300 MPa
//     margin = 260/300 - 1 = 0.8333... - 1                        = -0.133333...
//
//   S355J2 (allowable 355 MPa, density 7850 kg/m3), same geometry and load:
//     margin = 355/200 - 1 = 1.775 - 1                            = 0.775
//     mass   = 7850 * 9.0e-6                                      = 0.07065 kg
//
// Every one of those is checked below. None was read out of the code.
public class BracketSectionCheckTests
{
    private static readonly ReferencePin AluminiumPin = new("Materials", "mat-6082-t6", 3);

    private const double Tolerance = 1e-9;

    private static BracketSectionCheckInput Nominal(
        double loadNewtons = 12_000.0,
        double allowableMegapascals = 260.0,
        double densityKilogramsPerCubicMetre = 2700.0,
        double areaSquareMillimetres = 60.0,
        double lengthMillimetres = 150.0,
        double massLimitKilograms = 0.050) =>
        new(AluminiumPin,
            new Quantity<Pressure>(allowableMegapascals, PressureUnits.Megapascal),
            new Quantity<MassDensity>(densityKilogramsPerCubicMetre, MassDensityUnits.KilogramPerCubicMetre),
            new Quantity<Force>(loadNewtons, ForceUnits.Newton),
            new Quantity<Area>(areaSquareMillimetres, AreaUnits.SquareMillimetre),
            new Quantity<Length>(lengthMillimetres, LengthUnits.Millimetre),
            new Quantity<Mass>(massLimitKilograms, MassUnits.Kilogram));

    private static BracketSectionCheckResult Run(BracketSectionCheckInput input) =>
        new BracketSectionCheckCalculationDefinition().Calculate(input, new CalculationContext());

    // ---- Nominal ----

    [Fact]
    public void NominalCase_MatchesTheHandCalculation()
    {
        var result = Run(Nominal());

        Assert.Equal(200.0, result.AppliedStress.ConvertTo(PressureUnits.Megapascal).Value, Tolerance);
        Assert.Equal(0.30, result.StressMargin, Tolerance);
        Assert.Equal(0.0243, result.EstimatedMass.ConvertTo(MassUnits.Kilogram).Value, Tolerance);

        Assert.True(result.StressCriterionMet);
        Assert.True(result.MassCriterionMet);
        Assert.Equal(BracketCheckOutcome.MeetsCriteria, result.Outcome);
    }

    [Fact]
    public void TheResultCarriesTheAllowableItWasComparedAgainst_NotJustTheVerdict()
    {
        var result = Run(Nominal());

        Assert.Equal(260.0, result.AllowableStress.ConvertTo(PressureUnits.Megapascal).Value, Tolerance);
        Assert.Equal(0.050, result.MassLimit.ConvertTo(MassUnits.Kilogram).Value, Tolerance);
        Assert.Equal(AluminiumPin, result.MaterialPin);
    }

    // ---- Boundary ----

    [Fact]
    public void AtExactlyTheAllowableStress_TheMarginIsZeroAndTheCriterionIsMet()
    {
        // 15 600 N over 60 mm2 is exactly 260 MPa.
        var result = Run(Nominal(loadNewtons: 15_600.0));

        Assert.Equal(260.0, result.AppliedStress.ConvertTo(PressureUnits.Megapascal).Value, 1e-9);
        // The margin is zero to within a rounding artefact, and the
        // artefact is real: 60 mm2 converts to 5.9999999999999995e-05 m2,
        // so the computed stress lands one ulp above 260 MPa and the raw
        // margin is about -1.1e-16. Reported as computed, not snapped.
        Assert.Equal(0.0, result.StressMargin, 1e-12);
        Assert.True(Math.Abs(result.StressMargin) < 1e-15);

        // At the limit, not over it. This is what
        // AcceptanceRelativeTolerance exists for: without it the bracket
        // would be failed by a hundred-trillionth of a percent.
        Assert.True(result.StressCriterionMet);
        Assert.Equal(BracketCheckOutcome.MeetsCriteria, result.Outcome);
    }

    [Fact]
    public void AtExactlyTheMassLimit_TheCriterionIsMet()
    {
        var result = Run(Nominal(massLimitKilograms: 0.0243));

        Assert.Equal(0.0243, result.EstimatedMass.ConvertTo(MassUnits.Kilogram).Value, Tolerance);
        Assert.True(result.MassCriterionMet);
        Assert.Equal(BracketCheckOutcome.MeetsCriteria, result.Outcome);
    }

    // ---- Acceptance failures ----

    [Fact]
    public void OverTheAllowableStress_TheMarginIsNegativeAndTheCheckDoesNotPass()
    {
        var result = Run(Nominal(loadNewtons: 18_000.0));

        Assert.Equal(300.0, result.AppliedStress.ConvertTo(PressureUnits.Megapascal).Value, Tolerance);
        Assert.Equal(-1.0 / 7.5, result.StressMargin, Tolerance);
        Assert.Equal(-0.13333333333333333, result.StressMargin, 1e-12);

        Assert.False(result.StressCriterionMet);
        Assert.Equal(BracketCheckOutcome.DoesNotMeetCriteria, result.Outcome);
    }

    [Fact]
    public void OverTheMassLimit_TheCheckDoesNotPassEvenThoughTheStressDoes()
    {
        var result = Run(Nominal(massLimitKilograms: 0.020));

        Assert.True(result.StressCriterionMet);
        Assert.False(result.MassCriterionMet);
        Assert.Equal(BracketCheckOutcome.DoesNotMeetCriteria, result.Outcome);
    }

    [Fact]
    public void SteelPassesOnStrengthAndFailsOnMass()
    {
        // The same conclusion the integration phase's material selection
        // reached, now reached numerically: S355J2 is far stronger than the
        // bracket needs and nearly three times too heavy.
        var result = Run(Nominal(allowableMegapascals: 355.0, densityKilogramsPerCubicMetre: 7850.0));

        Assert.Equal(0.775, result.StressMargin, Tolerance);
        Assert.Equal(0.07065, result.EstimatedMass.ConvertTo(MassUnits.Kilogram).Value, Tolerance);

        Assert.True(result.StressCriterionMet);
        Assert.False(result.MassCriterionMet);
        Assert.Equal(BracketCheckOutcome.DoesNotMeetCriteria, result.Outcome);
    }

    // ---- Dimensional correctness ----

    [Fact]
    public void TheSamePhysicalInputsInDifferentUnitsGiveTheSameAnswer()
    {
        // Units are not decorative. 12 kN is 12 000 N and 6.0e-5 m2 is
        // 60 mm2, so the two statements of the same problem must agree
        // exactly, not approximately.
        var inMillimetresAndNewtons = Run(Nominal());

        var inMetresAndKilonewtons = Run(new BracketSectionCheckInput(
            AluminiumPin,
            new Quantity<Pressure>(260e6, PressureUnits.Pascal),
            new Quantity<MassDensity>(2.70, MassDensityUnits.GramPerCubicCentimetre),
            new Quantity<Force>(12.0, ForceUnits.Kilonewton),
            new Quantity<Area>(6.0e-5, AreaUnits.SquareMetre),
            new Quantity<Length>(0.15, LengthUnits.Metre),
            new Quantity<Mass>(50.0, MassUnits.Gram)));

        Assert.Equal(
            inMillimetresAndNewtons.AppliedStress.BaseValue,
            inMetresAndKilonewtons.AppliedStress.BaseValue,
            1e-6);
        Assert.Equal(inMillimetresAndNewtons.StressMargin, inMetresAndKilonewtons.StressMargin, 1e-12);
        Assert.Equal(
            inMillimetresAndNewtons.EstimatedMass.BaseValue,
            inMetresAndKilonewtons.EstimatedMass.BaseValue,
            1e-12);
        Assert.Equal(inMillimetresAndNewtons.Outcome, inMetresAndKilonewtons.Outcome);
    }

    [Fact]
    public void TheResultKeepsItsDimensionsRatherThanReturningBareNumbers()
    {
        var result = Run(Nominal());

        // A stress is a Pressure, a mass is a Mass. The margin is the only
        // dimensionless thing here, and it is dimensionless because a ratio
        // of two stresses genuinely is.
        Assert.IsType<Quantity<Pressure>>(result.AppliedStress);
        Assert.IsType<Quantity<Pressure>>(result.AllowableStress);
        Assert.IsType<Quantity<Mass>>(result.EstimatedMass);
        Assert.IsType<Quantity<Mass>>(result.MassLimit);

        Assert.Equal("MPa", result.AppliedStress.Unit.Symbol);
        Assert.Equal("kg", result.EstimatedMass.Unit.Symbol);
    }

    [Fact]
    public void ADimensionallyInvalidInputCannotBeExpressedAtAll()
    {
        // Not a runtime check — the type system refuses it. Passing an area
        // where a force belongs does not compile, which is why there is no
        // test asserting that it throws. What can be asserted is that the
        // input record's own parameters are dimensioned, so no caller can
        // hand over a bare double.
        var parameters = typeof(BracketSectionCheckInput).GetConstructors().Single().GetParameters();

        Assert.Equal(typeof(Quantity<Pressure>), parameters.Single(p => p.Name == "AllowableStress").ParameterType);
        Assert.Equal(typeof(Quantity<MassDensity>), parameters.Single(p => p.Name == "Density").ParameterType);
        Assert.Equal(typeof(Quantity<Force>), parameters.Single(p => p.Name == "AppliedLoad").ParameterType);
        Assert.Equal(typeof(Quantity<Area>), parameters.Single(p => p.Name == "SectionArea").ParameterType);
        Assert.Equal(typeof(Quantity<Length>), parameters.Single(p => p.Name == "MemberLength").ParameterType);
        Assert.Equal(typeof(Quantity<Mass>), parameters.Single(p => p.Name == "MassLimit").ParameterType);
    }

    // ---- Invalid input ----

    [Theory]
    [InlineData(0.0, 60.0, 150.0, 260.0, 2700.0, 0.05, "Applied load")]
    [InlineData(-12_000.0, 60.0, 150.0, 260.0, 2700.0, 0.05, "Applied load")]
    [InlineData(12_000.0, 0.0, 150.0, 260.0, 2700.0, 0.05, "Section area")]
    [InlineData(12_000.0, -60.0, 150.0, 260.0, 2700.0, 0.05, "Section area")]
    [InlineData(12_000.0, 60.0, 0.0, 260.0, 2700.0, 0.05, "Member length")]
    [InlineData(12_000.0, 60.0, 150.0, 0.0, 2700.0, 0.05, "Allowable stress")]
    [InlineData(12_000.0, 60.0, 150.0, -260.0, 2700.0, 0.05, "Allowable stress")]
    [InlineData(12_000.0, 60.0, 150.0, 260.0, 0.0, 0.05, "Density")]
    [InlineData(12_000.0, 60.0, 150.0, 260.0, 2700.0, 0.0, "Mass limit")]
    [InlineData(12_000.0, 60.0, 150.0, 260.0, 2700.0, -0.05, "Mass limit")]
    public void InvalidGeometryOrLoadingIsRefused(
        double load, double area, double length, double allowable, double density, double massLimit, string expected)
    {
        var input = Nominal(
            loadNewtons: load,
            allowableMegapascals: allowable,
            densityKilogramsPerCubicMetre: density,
            areaSquareMillimetres: area,
            lengthMillimetres: length,
            massLimitKilograms: massLimit);

        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(input));

        Assert.Contains(expected, refused.Message, StringComparison.Ordinal);
    }

    // ---- Determinism ----

    [Fact]
    public void TheSameInputsAlwaysGiveBitIdenticalResults()
    {
        var first = Run(Nominal());
        var second = Run(Nominal());
        var third = new BracketSectionCheckCalculationDefinition().Calculate(Nominal(), new CalculationContext());

        Assert.Equal(first, second);
        Assert.Equal(first, third);
        Assert.Equal(first.StressMargin, third.StressMargin);
        Assert.Equal(first.EstimatedMass.BaseValue, third.EstimatedMass.BaseValue);
    }

    // ---- What the calculation records about itself ----

    [Fact]
    public void TheCalculationRecordsItsWorkingAndItsAcceptanceChecks()
    {
        var context = new CalculationContext();
        new BracketSectionCheckCalculationDefinition().Calculate(Nominal(), context);

        Assert.Contains(context.IntermediateResults, i => i.Name == "Applied stress (MPa)");
        Assert.Contains(context.IntermediateResults, i => i.Name == "Stress margin of safety");
        Assert.Contains(context.IntermediateResults, i => i.Name == "Estimated mass (kg)");
        Assert.Contains(context.ReferencedMaterialIds, id => id == "mat-6082-t6");

        var acceptance = context.ConstraintChecks
            .Where(c => c.Description.StartsWith("Applied stress must not", StringComparison.Ordinal)
                || c.Description.StartsWith("Estimated mass must not", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, acceptance.Count);
        Assert.All(acceptance, c => Assert.True(c.IsSatisfied));
        Assert.All(acceptance, c => Assert.False(string.IsNullOrWhiteSpace(c.Detail)));
    }

    [Fact]
    public void AnUnmetAcceptanceCriterionIsRecordedAsAnUnsatisfiedConstraint()
    {
        var context = new CalculationContext();
        new BracketSectionCheckCalculationDefinition().Calculate(Nominal(loadNewtons: 18_000.0), context);

        var stressCheck = context.ConstraintChecks
            .Single(c => c.Description.StartsWith("Applied stress must not", StringComparison.Ordinal));

        Assert.False(stressCheck.IsSatisfied);
        Assert.Contains("300", stressCheck.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDeclaredAssumptionsAndLimitsTravelWithTheCalculation()
    {
        var metadata = new BracketSectionCheckCalculationDefinition().Metadata;

        Assert.Equal(5, metadata.Assumptions.Count);
        Assert.All(metadata.Assumptions, a => Assert.False(string.IsNullOrWhiteSpace(a.Justification)));

        // The limits of a first-order hand calculation are stated on the
        // calculation itself, so nobody has to know to ask.
        Assert.Contains(metadata.Constraints, c =>
            c.Description.Contains("bending", StringComparison.OrdinalIgnoreCase)
            && c.Description.Contains("fatigue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheAcceptanceToleranceReachesRoundingErrorAndNothingElse()
    {
        // The tolerance must be wide enough to absorb unit-conversion
        // rounding and narrow enough that it can never rescue a design.
        // Bracketed here so that widening it later breaks a test.
        Assert.Equal(1e-9, BracketSectionCheckCalculationDefinition.AcceptanceRelativeTolerance);

        // A hundredth of a percent over the allowable — still far below any
        // engineering significance, and still failed.
        var justOver = Run(Nominal(loadNewtons: 15_600.0 * 1.0001));
        Assert.False(justOver.StressCriterionMet);
        Assert.Equal(BracketCheckOutcome.DoesNotMeetCriteria, justOver.Outcome);

        // A ten-billionth over — inside the tolerance, and accepted.
        var withinRounding = Run(Nominal(loadNewtons: 15_600.0 * (1.0 + 1e-10)));
        Assert.True(withinRounding.StressCriterionMet);

        // The same for mass.
        var massJustOver = Run(Nominal(massLimitKilograms: 0.0243 / 1.0001));
        Assert.False(massJustOver.MassCriterionMet);
    }
}
