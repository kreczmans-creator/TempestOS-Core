using CsCheck;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// The unit-change invariance every `WP 21.7A` module carries, in
/// <c>CalculationPropertyTests</c>' own pattern: the same physics stated
/// in different randomly chosen units of each dimension gives the same
/// base-unit answer, to one part in a billion.
/// </summary>
/// <remarks>
/// Each field is generated as a base value with two random units of its
/// dimension; the baseline input states the value in the first unit, the
/// alternate re-expresses that same quantity in the second. Ranges are
/// chosen inside every method's limits so both runs compute.
/// </remarks>
public class ModuleCalculationPropertyTests
{
    private const double RelativeTolerance = 1e-9;

    private static Gen<Unit<TDimension>> AnyUnit<TDimension>(IReadOnlyList<Unit<TDimension>> catalogue)
        where TDimension : IDimension =>
        Gen.Int[0, catalogue.Count - 1].Select(i => catalogue[i]);

    /// <summary>A base value in [lo, hi] with two random units of its dimension.</summary>
    private static Gen<(double Base, Unit<TDimension> From, Unit<TDimension> To)> Field<TDimension>(double lo, double hi, IReadOnlyList<Unit<TDimension>> units)
        where TDimension : IDimension =>
        Gen.Select(Gen.Double[lo, hi], AnyUnit(units), AnyUnit(units));

    /// <summary>The quantity in its first unit.</summary>
    private static Quantity<TDimension> A<TDimension>((double Base, Unit<TDimension> From, Unit<TDimension> To) f)
        where TDimension : IDimension =>
        new(f.From.FromBase(f.Base), f.From);

    /// <summary>The same quantity re-expressed in its second unit.</summary>
    private static Quantity<TDimension> B<TDimension>((double Base, Unit<TDimension> From, Unit<TDimension> To) f)
        where TDimension : IDimension =>
        A(f).ConvertTo(f.To);

    private static void AssertSame(double expected, double actual, string what)
    {
        var scale = Math.Max(1.0, Math.Abs(expected));
        Assert.True(
            Math.Abs(actual - expected) <= RelativeTolerance * scale,
            $"{what}: expected {expected}, got {actual} (relative difference {Math.Abs(actual - expected) / scale:E}).");
    }

    private static void AssertSame<TDimension>(Quantity<TDimension>? expected, Quantity<TDimension>? actual, string what)
        where TDimension : IDimension
    {
        Assert.True(expected.HasValue && actual.HasValue, $"{what}: one of the two runs did not compute it.");
        AssertSame(expected.Value.BaseValue, actual.Value.BaseValue, what);
    }

    private static void AssertSame(double? expected, double? actual, string what)
    {
        Assert.True(expected.HasValue && actual.HasValue, $"{what}: one of the two runs did not compute it.");
        AssertSame(expected.Value, actual.Value, what);
    }

    [Fact]
    public void BeamDeflection_ResultInvariant_UnderInputUnitChange()
    {
        var gen = Gen.Select(
            Field(100, 50_000, ForceUnits.All),                         // load, N
            Field(0.5, 6.0, LengthUnits.All),                            // span, m
            Field(7e10, 2.1e11, PressureUnits.All),                      // modulus, Pa
            Field(1e-7, 1e-4, SecondMomentOfAreaUnits.All),              // I, m^4
            Field(0.005, 0.02, LengthUnits.All),                         // c, m (span/(2c) >= 12.5)
            Field(1e8, 5e8, PressureUnits.All),                          // allowable, Pa
            Field(0.001, 0.05, LengthUnits.All),                         // limit, m
            Gen.Int[0, 3]);                                              // case

        gen.Sample(t =>
        {
            var (load, span, modulus, inertia, fibre, allowable, limit, which) = t;
            var support = which < 2 ? BeamSupport.SimplySupported : BeamSupport.Cantilever;
            var loading = which % 2 == 0 ? BeamLoading.PointLoad : BeamLoading.UniformlyDistributed;
            var definition = new BeamDeflectionCalculationDefinition();

            var baseline = definition.Calculate(new BeamDeflectionInput(SteelPin, support, loading, A(load), A(span), A(modulus), A(inertia), A(fibre), A(allowable), A(limit)), new CalculationContext());
            var alternate = definition.Calculate(new BeamDeflectionInput(SteelPin, support, loading, B(load), B(span), B(modulus), B(inertia), B(fibre), B(allowable), B(limit)), new CalculationContext());

            Assert.Equal(baseline.Outcome, alternate.Outcome);
            AssertSame(baseline.MaximumMoment, alternate.MaximumMoment, "MaximumMoment");
            AssertSame(baseline.MaximumBendingStress, alternate.MaximumBendingStress, "MaximumBendingStress");
            AssertSame(baseline.MaximumDeflection, alternate.MaximumDeflection, "MaximumDeflection");
            AssertSame(baseline.StressUtilisation, alternate.StressUtilisation, "StressUtilisation");
        });
    }

    [Fact]
    public void BoltedJointPreload_ResultInvariant_UnderInputUnitChange()
    {
        var gen = Gen.Select(
            Gen.Double[0.3, 0.8],                                        // preload as a fraction of proof
            Field(0, 60_000, ForceUnits.All),                            // external load, N
            Field(1e7, 1e9, StiffnessUnits.All),                         // bolt stiffness, N/m
            Field(1e7, 5e9, StiffnessUnits.All),                         // member stiffness, N/m
            Field(5e-5, 3e-4, AreaUnits.All),                            // stress area, m^2
            Field(4e8, 1e9, PressureUnits.All),                          // proof strength, Pa
            AnyUnit(ForceUnits.All), AnyUnit(ForceUnits.All));           // preload units

        gen.Sample(t =>
        {
            var (fraction, external, bolt, member, area, proof, preloadFrom, preloadTo) = t;
            var preload = (fraction * proof.Base * area.Base, preloadFrom, preloadTo);
            var definition = new BoltedJointPreloadCalculationDefinition();

            var baseline = definition.Calculate(new BoltedJointPreloadInput(null, FastenerGrade, A(preload), A(external), A(bolt), A(member), A(area), A(proof)), new CalculationContext());
            var alternate = definition.Calculate(new BoltedJointPreloadInput(null, FastenerGrade, B(preload), B(external), B(bolt), B(member), B(area), B(proof)), new CalculationContext());

            Assert.Equal(baseline.Outcome, alternate.Outcome);
            Assert.Equal(baseline.IsSeparated, alternate.IsSeparated);
            AssertSame(baseline.JointConstant, alternate.JointConstant, "JointConstant");
            AssertSame(baseline.BoltLoad, alternate.BoltLoad, "BoltLoad");
            AssertSame(baseline.ClampLoad, alternate.ClampLoad, "ClampLoad");
            AssertSame(baseline.BoltStress, alternate.BoltStress, "BoltStress");
        });
    }

    [Fact]
    public void BoltGroupEccentricShear_ResultInvariant_UnderInputUnitChange()
    {
        // Three distinct bolts on a 10 mm grid: x y x y x y, in metres.
        var positions = Gen.Int[-20, 20].Array[6]
            .Where(a => (a[0] != a[2] || a[1] != a[3]) && (a[0] != a[4] || a[1] != a[5]) && (a[2] != a[4] || a[3] != a[5]))
            .Select(a => a.Select(i => i * 0.01).ToArray());

        var gen = Gen.Select(
            positions,
            Field(-5e4, 5e4, ForceUnits.All),                            // load x, N
            Field(-5e4, 5e4, ForceUnits.All),                            // load y, N
            Field(-0.5, 0.5, LengthUnits.All),                           // load point x, m
            Field(-0.5, 0.5, LengthUnits.All),                           // load point y, m
            Field(1e4, 1e5, ForceUnits.All),                             // allowable, N
            AnyUnit(LengthUnits.All), AnyUnit(LengthUnits.All));         // coordinate units

        gen.Sample(t =>
        {
            var (coordinates, loadX, loadY, pointX, pointY, allowable, coordFrom, coordTo) = t;
            var definition = new BoltGroupEccentricShearCalculationDefinition();

            List<BoltPosition> Bolts(Func<(double, Unit<Length>, Unit<Length>), Quantity<Length>> express) =>
            [
                new(express((coordinates[0], coordFrom, coordTo)), express((coordinates[1], coordFrom, coordTo))),
                new(express((coordinates[2], coordFrom, coordTo)), express((coordinates[3], coordFrom, coordTo))),
                new(express((coordinates[4], coordFrom, coordTo)), express((coordinates[5], coordFrom, coordTo))),
            ];

            var baseline = definition.Calculate(new BoltGroupEccentricShearInput(FastenerGrade, Bolts(A), A(loadX), A(loadY), A(pointX), A(pointY), A(allowable)), new CalculationContext());
            var alternate = definition.Calculate(new BoltGroupEccentricShearInput(FastenerGrade, Bolts(B), B(loadX), B(loadY), B(pointX), B(pointY), B(allowable)), new CalculationContext());

            Assert.Equal(baseline.Outcome, alternate.Outcome);
            AssertSame(baseline.MomentAboutCentroid.BaseValue, alternate.MomentAboutCentroid.BaseValue, "MomentAboutCentroid");
            AssertSame(baseline.UnitPolarMoment.BaseValue, alternate.UnitPolarMoment.BaseValue, "UnitPolarMoment");
            AssertSame(baseline.GoverningBoltForce, alternate.GoverningBoltForce, "GoverningBoltForce");
            Assert.Equal(baseline.GoverningBoltIndex, alternate.GoverningBoltIndex);
        });
    }

    [Fact]
    public void FilletWeldThroatStress_ResultInvariant_UnderInputUnitChange()
    {
        var gen = Gen.Select(
            Field(-1e5, 1e5, ForceUnits.All),                            // parallel, N
            Field(-1e5, 1e5, ForceUnits.All),                            // transverse, N
            Field(-1e5, 1e5, ForceUnits.All),                            // normal, N
            Field(0.1, 0.5, LengthUnits.All),                            // length, m (>= 6a and 30 mm)
            Field(0.003, 0.012, LengthUnits.All),                        // throat, m
            Field(3.6e8, 6e8, PressureUnits.All),                        // ultimate, Pa
            Gen.Double[0.8, 1.0],                                        // correlation
            Gen.Double[1.0, 1.3]);                                       // partial

        gen.Sample(t =>
        {
            var (parallel, transverse, normal, length, throat, ultimate, correlation, partial) = t;
            var definition = new FilletWeldThroatStressCalculationDefinition();

            var baseline = definition.Calculate(new FilletWeldThroatStressInput(SteelPin, A(parallel), A(transverse), A(normal), A(length), A(throat), A(ultimate), correlation, partial), new CalculationContext());
            var alternate = definition.Calculate(new FilletWeldThroatStressInput(SteelPin, B(parallel), B(transverse), B(normal), B(length), B(throat), B(ultimate), correlation, partial), new CalculationContext());

            Assert.Equal(baseline.Outcome, alternate.Outcome);
            AssertSame(baseline.ThroatStress, alternate.ThroatStress, "ThroatStress");
            AssertSame(baseline.DesignShearStrength, alternate.DesignShearStrength, "DesignShearStrength");
            AssertSame(baseline.RequiredThroat, alternate.RequiredThroat, "RequiredThroat");
            AssertSame(baseline.Utilisation, alternate.Utilisation, "Utilisation");
        });
    }

    [Fact]
    public void LiftingLugPinJoint_ResultInvariant_UnderInputUnitChange()
    {
        var geometry = Gen.Select(
            Field(1e4, 2e5, ForceUnits.All),                             // load, N
            Field(0.008, 0.04, LengthUnits.All),                         // lug thickness, m
            Field(0.02, 0.05, LengthUnits.All),                          // hole, m
            Gen.Double[0.9, 0.999],                                      // pin/hole (strictly inside, so no rounding can push the pin past the hole)
            Gen.Double[2.0, 4.0],                                        // width/hole
            Field(0.02, 0.08, LengthUnits.All),                          // edge distance, m
            Field(0.006, 0.02, LengthUnits.All),                         // cheek plate, m
            Field(0, 0.004, LengthUnits.All));                           // clearance, m
        var allowables = Gen.Select(
            Field(1e8, 3e8, PressureUnits.All), Field(1.5e8, 4e8, PressureUnits.All), Field(6e7, 2e8, PressureUnits.All),
            Field(1.5e8, 5e8, PressureUnits.All), Field(1e8, 3e8, PressureUnits.All),
            AnyUnit(LengthUnits.All), AnyUnit(LengthUnits.All));

        Gen.Select(geometry, allowables).Sample(sample =>
        {
            var ((load, thickness, hole, pinRatio, widthRatio, edge, cheek, clearance), (tension, bearing, shear, pinBending, pinShear, derivedFrom, derivedTo)) = sample;
            var pin = (pinRatio * hole.Base, derivedFrom, derivedTo);
            var width = (widthRatio * hole.Base, derivedFrom, derivedTo);
            var definition = new LiftingLugPinJointCalculationDefinition();

            var baseline = definition.Calculate(new LiftingLugPinJointInput(SteelPin, PinSteelPin, A(load), A(thickness), A(width), A(hole), A(pin), A(edge), A(cheek), A(clearance), A(tension), A(bearing), A(shear), A(pinBending), A(pinShear)), new CalculationContext());
            var alternate = definition.Calculate(new LiftingLugPinJointInput(SteelPin, PinSteelPin, B(load), B(thickness), B(width), B(hole), B(pin), B(edge), B(cheek), B(clearance), B(tension), B(bearing), B(shear), B(pinBending), B(pinShear)), new CalculationContext());

            Assert.Equal(baseline.Outcome, alternate.Outcome);
            Assert.Equal(baseline.GoverningCheck, alternate.GoverningCheck);
            AssertSame(baseline.NetSectionStress, alternate.NetSectionStress, "NetSectionStress");
            AssertSame(baseline.BearingStress, alternate.BearingStress, "BearingStress");
            AssertSame(baseline.TearOutStress, alternate.TearOutStress, "TearOutStress");
            AssertSame(baseline.PinShearStress, alternate.PinShearStress, "PinShearStress");
            AssertSame(baseline.PinBendingStress, alternate.PinBendingStress, "PinBendingStress");
            AssertSame(baseline.GoverningUtilisation, alternate.GoverningUtilisation, "GoverningUtilisation");
        });
    }

    [Fact]
    public void ColumnBuckling_ResultInvariant_UnderInputUnitChange()
    {
        var gen = Gen.Select(
            Field(0.5, 3.0, LengthUnits.All),                            // effective length, m
            Field(1e-3, 5e-3, AreaUnits.All),                            // area, m^2
            Gen.Double[0.02, 0.1],                                       // radius of gyration, m (lambda <= 150)
            Field(1.9e11, 2.1e11, PressureUnits.All),                    // modulus, Pa
            Field(2.35e8, 4.6e8, PressureUnits.All),                     // yield, Pa
            Gen.Double[2.0, 8.0],                                        // Robertson constant
            Field(0, 1e6, ForceUnits.All),                               // applied load, N
            Gen.Select(AnyUnit(SecondMomentOfAreaUnits.All), AnyUnit(SecondMomentOfAreaUnits.All)));

        gen.Sample(t =>
        {
            var (length, area, radius, modulus, yield, robertson, load, inertiaUnits) = t;
            var inertia = (area.Base * radius * radius, inertiaUnits.Item1, inertiaUnits.Item2);
            var definition = new ColumnBucklingCalculationDefinition();

            var baseline = definition.Calculate(new ColumnBucklingInput(SteelPin, A(length), A(area), A(inertia), A(modulus), A(yield), robertson, A(load)), new CalculationContext());
            var alternate = definition.Calculate(new ColumnBucklingInput(SteelPin, B(length), B(area), B(inertia), B(modulus), B(yield), robertson, B(load)), new CalculationContext());

            Assert.Equal(baseline.Outcome, alternate.Outcome);
            AssertSame(baseline.Slenderness, alternate.Slenderness, "Slenderness");
            AssertSame(baseline.EulerStress, alternate.EulerStress, "EulerStress");
            AssertSame(baseline.PerryFactor, alternate.PerryFactor, "PerryFactor");
            AssertSame(baseline.CompressiveStrength, alternate.CompressiveStrength, "CompressiveStrength");
            AssertSame(baseline.CompressionResistance, alternate.CompressionResistance, "CompressionResistance");
        });
    }

    [Fact]
    public void ShaftCombinedStress_ResultInvariant_UnderInputUnitChange()
    {
        var gen = Gen.Select(
            Field(0.01, 0.1, LengthUnits.All),                           // diameter, m
            Field(10, 5000, TorqueUnits.All),                            // moment, N.m
            Field(10, 5000, TorqueUnits.All),                            // torque, N.m
            Field(2e8, 1e9, PressureUnits.All),                          // yield, Pa
            Gen.Double[1.0, 3.0], Gen.Double[1.0, 3.0], Gen.Double[1.0, 3.0]);

        gen.Sample(t =>
        {
            var (diameter, moment, torque, yield, kt, kts, required) = t;
            var definition = new ShaftCombinedStressCalculationDefinition();

            var baseline = definition.Calculate(new ShaftCombinedStressInput(SteelPin, A(diameter), A(moment), A(torque), A(yield), kt, kts, required), new CalculationContext());
            var alternate = definition.Calculate(new ShaftCombinedStressInput(SteelPin, B(diameter), B(moment), B(torque), B(yield), kt, kts, required), new CalculationContext());

            Assert.Equal(baseline.Outcome, alternate.Outcome);
            AssertSame(baseline.BendingStress.BaseValue, alternate.BendingStress.BaseValue, "BendingStress");
            AssertSame(baseline.TorsionalShearStress.BaseValue, alternate.TorsionalShearStress.BaseValue, "TorsionalShearStress");
            AssertSame(baseline.VonMisesStress.BaseValue, alternate.VonMisesStress.BaseValue, "VonMisesStress");
            AssertSame(baseline.TrescaSafetyFactor, alternate.TrescaSafetyFactor, "TrescaSafetyFactor");
        });
    }

    [Fact]
    public void BearingRatingLife_ResultInvariant_UnderInputUnitChange()
    {
        var gen = Gen.Select(
            Field(1e4, 2e5, ForceUnits.All),                             // rating, N
            Gen.Double[0.05, 0.3], Gen.Double[0, 0.1],                   // radial and axial as fractions of C
            Gen.Double[0.4, 1.0], Gen.Double[0, 1.5],                    // X, Y (P <= 0.45 C)
            Field(1, 100, RotationalSpeedUnits.All),                     // speed, r/s
            Gen.Double[0.2, 1.0],                                        // a1
            Field(0, 3.6e7, DurationUnits.All));                         // required life, s

        var loadUnits = Gen.Select(AnyUnit(ForceUnits.All), AnyUnit(ForceUnits.All), AnyUnit(ForceUnits.All), AnyUnit(ForceUnits.All), Gen.Bool);

        Gen.Select(gen, loadUnits).Sample(sample =>
        {
            var ((rating, radialFraction, axialFraction, x, y, speed, reliability, required), (radialFrom, radialTo, axialFrom, axialTo, isBall)) = sample;
            var radial = (radialFraction * rating.Base, radialFrom, radialTo);
            var axial = (axialFraction * rating.Base, axialFrom, axialTo);
            var type = isBall ? RollingBearingType.Ball : RollingBearingType.Roller;
            var definition = new BearingRatingLifeCalculationDefinition();

            var baseline = definition.Calculate(new BearingRatingLifeInput(null, "6208", type, A(rating), A(radial), A(axial), x, y, A(speed), reliability, A(required)), new CalculationContext());
            var alternate = definition.Calculate(new BearingRatingLifeInput(null, "6208", type, B(rating), B(radial), B(axial), x, y, B(speed), reliability, B(required)), new CalculationContext());

            Assert.Equal(baseline.Outcome, alternate.Outcome);
            AssertSame(baseline.LoadRatio, alternate.LoadRatio, "LoadRatio");
            AssertSame(baseline.BasicRatingLifeMillionRevolutions, alternate.BasicRatingLifeMillionRevolutions, "BasicRatingLifeMillionRevolutions");
            AssertSame(baseline.BasicRatingLife, alternate.BasicRatingLife, "BasicRatingLife");
            AssertSame(baseline.ModifiedRatingLife, alternate.ModifiedRatingLife, "ModifiedRatingLife");
        });
    }

    [Fact]
    public void ThickWalledCylinder_ResultInvariant_UnderInputUnitChange()
    {
        var gen = Gen.Select(
            Field(0.02, 0.2, LengthUnits.All),                           // inner radius, m
            Gen.Double[1.1, 3.0],                                        // b/a
            Field(1e5, 5e7, PressureUnits.All),                          // internal, Pa
            Field(0, 5e7, PressureUnits.All),                            // external, Pa
            Field(1e8, 5e8, PressureUnits.All),                          // allowable, Pa
            Gen.Bool,
            AnyUnit(LengthUnits.All), AnyUnit(LengthUnits.All));

        gen.Sample(t =>
        {
            var (inner, ratio, internalPressure, externalPressure, allowable, closed, outerFrom, outerTo) = t;
            var outer = (ratio * inner.Base, outerFrom, outerTo);
            var definition = new ThickWalledCylinderCalculationDefinition();

            var baseline = definition.Calculate(new ThickWalledCylinderInput(SteelPin, A(inner), A(outer), A(internalPressure), A(externalPressure), closed, A(allowable)), new CalculationContext());
            var alternate = definition.Calculate(new ThickWalledCylinderInput(SteelPin, B(inner), B(outer), B(internalPressure), B(externalPressure), closed, B(allowable)), new CalculationContext());

            Assert.Equal(baseline.Outcome, alternate.Outcome);
            Assert.Equal(baseline.MaximumVonMisesSurface, alternate.MaximumVonMisesSurface);
            AssertSame(baseline.BoreHoopStress.BaseValue, alternate.BoreHoopStress.BaseValue, "BoreHoopStress");
            AssertSame(baseline.OuterHoopStress.BaseValue, alternate.OuterHoopStress.BaseValue, "OuterHoopStress");
            AssertSame(baseline.AxialStress.BaseValue, alternate.AxialStress.BaseValue, "AxialStress");
            AssertSame(baseline.MaximumVonMisesStress.BaseValue, alternate.MaximumVonMisesStress.BaseValue, "MaximumVonMisesStress");
            AssertSame(baseline.ThinWallHoopEstimate.BaseValue, alternate.ThinWallHoopEstimate.BaseValue, "ThinWallHoopEstimate");
        });
    }

    [Fact]
    public void ThermalExpansionStress_ResultInvariant_UnderInputUnitChange()
    {
        var gen = Gen.Select(
            Field(0.1, 5.0, LengthUnits.All),                            // length, m
            Field(1e-5, 1e-2, AreaUnits.All),                            // area, m^2
            Field(5e10, 2.5e11, PressureUnits.All),                      // modulus, Pa
            Field(5e-6, 3e-5, ThermalExpansionUnits.All),                // alpha, 1/K
            Field(-100, 200, TemperatureDeltaUnits.All),                 // delta T, K
            Field(0, 0.002, LengthUnits.All),                            // gap, m
            Field(1e6, 1e9, StiffnessUnits.All),                         // restraint, N/m
            Field(5e7, 5e8, PressureUnits.All));                         // allowable, Pa

        gen.Sample(t =>
        {
            var (length, area, modulus, alpha, delta, gap, restraint, allowable) = t;
            var definition = new ThermalExpansionStressCalculationDefinition();

            var baseline = definition.Calculate(new ThermalExpansionStressInput(SteelPin, A(length), A(area), A(modulus), A(alpha), A(delta), A(gap), A(restraint), A(allowable)), new CalculationContext());
            var alternate = definition.Calculate(new ThermalExpansionStressInput(SteelPin, B(length), B(area), B(modulus), B(alpha), B(delta), B(gap), B(restraint), B(allowable)), new CalculationContext());

            Assert.Equal(baseline.Outcome, alternate.Outcome);
            AssertSame(baseline.FreeMovement.BaseValue, alternate.FreeMovement.BaseValue, "FreeMovement");
            AssertSame(baseline.RestraintForce.BaseValue, alternate.RestraintForce.BaseValue, "RestraintForce");
            AssertSame(baseline.Stress.BaseValue, alternate.Stress.BaseValue, "Stress");
            AssertSame(baseline.ActualMovement.BaseValue, alternate.ActualMovement.BaseValue, "ActualMovement");
        });
    }

    [Fact]
    public void FatigueMiner_ResultInvariant_UnderInputUnitChange()
    {
        var gen = Gen.Select(
            Field(5e7, 2e8, PressureUnits.All),                          // reference range, Pa
            Gen.Double[1e6, 5e6],                                        // reference cycles
            Gen.Double[3.0, 5.0],                                        // slope
            Gen.Double[0.3, 1.5], Gen.Double[0.3, 1.5],                  // block ranges as fractions of the reference
            Gen.Double[0, 1e6], Gen.Double[0, 1e6],                      // block cycles
            Gen.Select(AnyUnit(PressureUnits.All), AnyUnit(PressureUnits.All), AnyUnit(PressureUnits.All), AnyUnit(PressureUnits.All)));

        gen.Sample(t =>
        {
            var (reference, cycles, slope, fraction1, fraction2, cycles1, cycles2, units) = t;
            var block1 = (fraction1 * reference.Base, units.Item1, units.Item2);
            var block2 = (fraction2 * reference.Base, units.Item3, units.Item4);
            var definition = new FatigueMinerCalculationDefinition();

            var baseline = definition.Calculate(new FatigueMinerInput(null, "curve", A(reference), cycles, slope, null, [new(A(block1), cycles1), new(A(block2), cycles2)]), new CalculationContext());
            var alternate = definition.Calculate(new FatigueMinerInput(null, "curve", B(reference), cycles, slope, null, [new(B(block1), cycles1), new(B(block2), cycles2)]), new CalculationContext());

            Assert.Equal(baseline.Outcome, alternate.Outcome);
            AssertSame(baseline.TotalDamage, alternate.TotalDamage, "TotalDamage");
            AssertSame(baseline.BlockLives![0], alternate.BlockLives![0], "BlockLives[0]");
            AssertSame(baseline.BlockLives[1], alternate.BlockLives[1], "BlockLives[1]");
        });
    }
}
