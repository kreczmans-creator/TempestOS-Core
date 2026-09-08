using CsCheck;
using Tempest.Core.Calculations;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations;

/// <summary>
/// Property-based tests (CsCheck) over this platform's own six engineering
/// calculation definitions — `WP 17.3A` (`ADR-0147`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Unit-change invariance.</b> Every calculation's own result is a
/// function of the physical quantities its input carries, never of which
/// unit a caller happened to state them in. Each property below builds one
/// input entirely in a randomly chosen unit per quantity field, builds a
/// second input describing the exact same physical values re-expressed in
/// a different randomly chosen unit of the same dimension, runs the same
/// calculation against both, and asserts every numeric result (base
/// values for quantities, raw values for plain doubles such as a margin
/// ratio) agrees within a relative tolerance of 1e-9 — the same tolerance
/// <see cref="BracketSectionCheckCalculationDefinition.AcceptanceRelativeTolerance"/>
/// already names for exactly this class of floating-point rounding.
/// </para>
/// <para>
/// <b>Monotonicity.</b> <see cref="BracketMargin_IsMonotonicallyDecreasing_AsLoadIncreases"/>
/// asserts the one qualitative property every reader of
/// <c>BracketSectionCheckCalculationDefinition</c>'s own formula
/// (<c>margin = (allowable / (F / A)) − 1</c>) expects without needing to
/// read the formula: heavier load, thinner margin, always, at every input
/// combination CsCheck manages to generate.
/// </para>
/// </remarks>
public class CalculationPropertyTests
{
    private const double RelativeTolerance = 1e-9;

    // ------------------------------------------------------------
    // Generators
    // ------------------------------------------------------------

    private static Gen<Unit<TDimension>> AnyUnit<TDimension>(IReadOnlyList<Unit<TDimension>> catalogue)
        where TDimension : IDimension =>
        Gen.Int[0, catalogue.Count - 1].Select(i => catalogue[i]);

    /// <summary>A quantity whose own <see cref="Quantity{TDimension}.BaseValue"/> is exactly <paramref name="baseValue"/>, expressed in <paramref name="unit"/>.</summary>
    private static Quantity<TDimension> At<TDimension>(double baseValue, Unit<TDimension> unit)
        where TDimension : IDimension =>
        new(unit.FromBase(baseValue), unit);

    private static void AssertClose(double expected, double actual, string what)
    {
        var scale = Math.Max(1.0, Math.Abs(expected));
        Assert.True(
            Math.Abs(actual - expected) <= RelativeTolerance * scale,
            $"{what}: expected {expected}, got {actual} (relative difference {Math.Abs(actual - expected) / scale:E}).");
    }

    // ------------------------------------------------------------
    // Bolt shear capacity
    // ------------------------------------------------------------

    [Fact]
    public void BoltShear_ResultInvariant_UnderInputUnitChange()
    {
        var gen = Gen.Select(
            Gen.Double[0.005, 0.05],   // diameter, metres
            Gen.Double[1e8, 6e8],      // ultimate shear strength, pascals
            Gen.Int[1, 2],             // shear planes
            Gen.Double[1.0, 4.0],      // safety factor
            AnyUnit(LengthUnits.All),
            AnyUnit(LengthUnits.All),
            AnyUnit(PressureUnits.All),
            AnyUnit(PressureUnits.All));

        gen.Sample(t =>
        {
            var (diameterBase, strengthBase, planes, safety, dFrom, dTo, sFrom, sTo) = t;
            var definition = new BoltShearCapacityCalculationDefinition();

            var baseline = definition.Calculate(
                new BoltShearCapacityInput(At(diameterBase, dFrom), At(strengthBase, sFrom), planes, safety),
                new CalculationContext());

            var alternate = definition.Calculate(
                new BoltShearCapacityInput(At(diameterBase, dFrom).ConvertTo(dTo), At(strengthBase, sFrom).ConvertTo(sTo), planes, safety),
                new CalculationContext());

            AssertClose(baseline.AllowableShearCapacity.BaseValue, alternate.AllowableShearCapacity.BaseValue, "AllowableShearCapacity");
        });
    }

    // ------------------------------------------------------------
    // Beam bending stress
    // ------------------------------------------------------------

    [Fact]
    public void BeamBending_ResultInvariant_UnderInputUnitChange()
    {
        var gen = Gen.Select(
            Gen.Double[10, 5000],       // applied load, newtons
            Gen.Double[0.05, 2.0],      // cantilever length, metres
            Gen.Double[0.005, 0.2],     // section width, metres
            Gen.Double[0.005, 0.2],     // section height, metres
            Gen.Double[1e8, 6e8]);      // allowable bending stress, pascals

        // Split across two 5-tuples: Gen.Select's tuple-producing overload
        // supports at most eight generators at once.
        var unitsGenA = Gen.Select(
            AnyUnit(ForceUnits.All), AnyUnit(ForceUnits.All),
            AnyUnit(LengthUnits.All), AnyUnit(LengthUnits.All),
            AnyUnit(LengthUnits.All));
        var unitsGenB = Gen.Select(
            AnyUnit(LengthUnits.All),
            AnyUnit(LengthUnits.All), AnyUnit(LengthUnits.All),
            AnyUnit(PressureUnits.All), AnyUnit(PressureUnits.All));

        Gen.Select(gen, unitsGenA, unitsGenB).Sample(sample =>
        {
            var (values, unitsA, unitsB) = sample;
            var (loadBase, lengthBase, widthBase, heightBase, stressBase) = values;
            var (loadFrom, loadTo, lengthFrom, lengthTo, widthFrom) = unitsA;
            var (widthTo, heightFrom, heightTo, stressFrom, stressTo) = unitsB;

            var definition = new BeamBendingStressCalculationDefinition();

            var baseline = definition.Calculate(
                new BeamBendingStressInput(
                    At(loadBase, loadFrom), At(lengthBase, lengthFrom), At(widthBase, widthFrom), At(heightBase, heightFrom), At(stressBase, stressFrom)),
                new CalculationContext());

            var alternate = definition.Calculate(
                new BeamBendingStressInput(
                    At(loadBase, loadFrom).ConvertTo(loadTo),
                    At(lengthBase, lengthFrom).ConvertTo(lengthTo),
                    At(widthBase, widthFrom).ConvertTo(widthTo),
                    At(heightBase, heightFrom).ConvertTo(heightTo),
                    At(stressBase, stressFrom).ConvertTo(stressTo)),
                new CalculationContext());

            AssertClose(baseline.BendingStress.BaseValue, alternate.BendingStress.BaseValue, "BendingStress");
            AssertClose(baseline.MarginOfSafety, alternate.MarginOfSafety, "MarginOfSafety");
        });
    }

    // ------------------------------------------------------------
    // Bearing load capacity
    // ------------------------------------------------------------

    [Fact]
    public void BearingLoad_ResultInvariant_UnderInputUnitChange()
    {
        var values = Gen.Select(
            Gen.Double[0.005, 0.05],   // hole diameter, metres
            Gen.Double[0.002, 0.05],   // plate thickness, metres
            Gen.Double[1e8, 6e8],      // bearing strength, pascals
            Gen.Double[1.0, 4.0]);     // safety factor
        var units = Gen.Select(
            AnyUnit(LengthUnits.All), AnyUnit(LengthUnits.All),
            AnyUnit(LengthUnits.All), AnyUnit(LengthUnits.All),
            AnyUnit(PressureUnits.All), AnyUnit(PressureUnits.All));

        Gen.Select(values, units).Sample(sample =>
        {
            var ((diameterBase, thicknessBase, strengthBase, safety), (dFrom, dTo, tFrom, tTo, sFrom, sTo)) = sample;
            var definition = new BearingLoadCapacityCalculationDefinition();

            var baseline = definition.Calculate(
                new BearingLoadCapacityInput(At(diameterBase, dFrom), At(thicknessBase, tFrom), At(strengthBase, sFrom), safety),
                new CalculationContext());

            var alternate = definition.Calculate(
                new BearingLoadCapacityInput(
                    At(diameterBase, dFrom).ConvertTo(dTo), At(thicknessBase, tFrom).ConvertTo(tTo), At(strengthBase, sFrom).ConvertTo(sTo), safety),
                new CalculationContext());

            AssertClose(baseline.AllowableBearingCapacity.BaseValue, alternate.AllowableBearingCapacity.BaseValue, "AllowableBearingCapacity");
        });
    }

    // ------------------------------------------------------------
    // Pressure vessel wall thickness
    // ------------------------------------------------------------

    [Fact]
    public void VesselWallThickness_ResultInvariant_UnderInputUnitChange()
    {
        var values = Gen.Select(
            Gen.Double[1e5, 5e6],      // internal pressure, pascals (kept well below allowable stress so S*E - 0.6P stays positive)
            Gen.Double[0.1, 2.0],      // inner radius, metres
            Gen.Double[1e8, 4e8],      // allowable stress, pascals
            Gen.Double[0.7, 1.0],      // joint efficiency
            Gen.Double[1.0, 3.0]);     // safety factor
        var units = Gen.Select(
            AnyUnit(PressureUnits.All), AnyUnit(PressureUnits.All),
            AnyUnit(LengthUnits.All), AnyUnit(LengthUnits.All),
            AnyUnit(PressureUnits.All), AnyUnit(PressureUnits.All));

        Gen.Select(values, units).Sample(sample =>
        {
            var ((pressureBase, radiusBase, stressBase, jointEfficiency, safety), (pFrom, pTo, rFrom, rTo, sFrom, sTo)) = sample;
            var definition = new PressureVesselWallThicknessCalculationDefinition();

            var baseline = definition.Calculate(
                new PressureVesselWallThicknessInput(At(pressureBase, pFrom), At(radiusBase, rFrom), At(stressBase, sFrom), jointEfficiency, safety),
                new CalculationContext());

            var alternate = definition.Calculate(
                new PressureVesselWallThicknessInput(
                    At(pressureBase, pFrom).ConvertTo(pTo), At(radiusBase, rFrom).ConvertTo(rTo), At(stressBase, sFrom).ConvertTo(sTo), jointEfficiency, safety),
                new CalculationContext());

            AssertClose(baseline.DesignThickness.BaseValue, alternate.DesignThickness.BaseValue, "DesignThickness");
        });
    }

    // ------------------------------------------------------------
    // Material selection margin
    // ------------------------------------------------------------

    [Fact]
    public void MaterialSelectionMargin_ResultInvariant_UnderInputUnitChange()
    {
        var gen = Gen.Select(
            Gen.Double[1e7, 5e8],   // material allowable stress, pascals
            Gen.Double[1e6, 4e8],   // applied stress, pascals
            AnyUnit(PressureUnits.All), AnyUnit(PressureUnits.All),
            AnyUnit(PressureUnits.All), AnyUnit(PressureUnits.All));

        gen.Sample(t =>
        {
            var (allowableBase, appliedBase, allowableFrom, allowableTo, appliedFrom, appliedTo) = t;
            var definition = new MaterialSelectionMarginCalculationDefinition();

            var baseline = definition.Calculate(
                new MaterialSelectionMarginInput("material-x", At(allowableBase, allowableFrom), At(appliedBase, appliedFrom)),
                new CalculationContext());

            var alternate = definition.Calculate(
                new MaterialSelectionMarginInput(
                    "material-x", At(allowableBase, allowableFrom).ConvertTo(allowableTo), At(appliedBase, appliedFrom).ConvertTo(appliedTo)),
                new CalculationContext());

            AssertClose(baseline.MarginRatio, alternate.MarginRatio, "MarginRatio");
        });
    }

    // ------------------------------------------------------------
    // Bracket section check
    // ------------------------------------------------------------

    [Fact]
    public void BracketSectionCheck_ResultInvariant_UnderInputUnitChange()
    {
        var pin = new ReferencePin("Materials", "material-x", 1);

        var gen = Gen.Select(
            Gen.Double[1e8, 5e8],       // allowable stress, pascals
            Gen.Double[1500, 9000],     // density, kg/m3
            Gen.Double[100, 50_000],    // applied load, newtons
            Gen.Double[1e-5, 1e-2],     // section area, m2
            Gen.Double[0.01, 3.0],      // member length, metres
            Gen.Double[0.01, 1000]);    // mass limit, kg

        var unitsGenA = Gen.Select(
            AnyUnit(PressureUnits.All), AnyUnit(PressureUnits.All),
            AnyUnit(MassDensityUnits.All), AnyUnit(MassDensityUnits.All),
            AnyUnit(ForceUnits.All), AnyUnit(ForceUnits.All));
        var unitsGenB = Gen.Select(
            AnyUnit(AreaUnits.All), AnyUnit(AreaUnits.All),
            AnyUnit(LengthUnits.All), AnyUnit(LengthUnits.All),
            AnyUnit(MassUnits.All), AnyUnit(MassUnits.All));

        Gen.Select(gen, unitsGenA, unitsGenB).Sample(sample =>
        {
            var (values, unitsA, unitsB) = sample;
            var (stressBase, densityBase, loadBase, areaBase, lengthBase, massLimitBase) = values;
            var (stressFrom, stressTo, densityFrom, densityTo, loadFrom, loadTo) = unitsA;
            var (areaFrom, areaTo, lengthFrom, lengthTo, massFrom, massTo) = unitsB;

            var definition = new BracketSectionCheckCalculationDefinition();

            var baseline = definition.Calculate(
                new BracketSectionCheckInput(
                    pin, At(stressBase, stressFrom), At(densityBase, densityFrom), At(loadBase, loadFrom), At(areaBase, areaFrom), At(lengthBase, lengthFrom), At(massLimitBase, massFrom)),
                new CalculationContext());

            var alternate = definition.Calculate(
                new BracketSectionCheckInput(
                    pin,
                    At(stressBase, stressFrom).ConvertTo(stressTo),
                    At(densityBase, densityFrom).ConvertTo(densityTo),
                    At(loadBase, loadFrom).ConvertTo(loadTo),
                    At(areaBase, areaFrom).ConvertTo(areaTo),
                    At(lengthBase, lengthFrom).ConvertTo(lengthTo),
                    At(massLimitBase, massFrom).ConvertTo(massTo)),
                new CalculationContext());

            AssertClose(baseline.AppliedStress.BaseValue, alternate.AppliedStress.BaseValue, "AppliedStress");
            AssertClose(baseline.StressMargin, alternate.StressMargin, "StressMargin");
            AssertClose(baseline.EstimatedMass.BaseValue, alternate.EstimatedMass.BaseValue, "EstimatedMass");
            Assert.Equal(baseline.Outcome, alternate.Outcome);
        });
    }

    [Fact]
    public void BracketMargin_IsMonotonicallyDecreasing_AsLoadIncreases()
    {
        var pin = new ReferencePin("Materials", "material-x", 1);

        var gen = Gen.Select(
            Gen.Double[1e8, 5e8],     // allowable stress, pascals
            Gen.Double[1500, 9000],   // density, kg/m3
            Gen.Double[1e-5, 1e-2],   // section area, m2
            Gen.Double[0.01, 3.0],    // member length, metres
            Gen.Double[100, 5000],    // lower load, newtons
            Gen.Double[0.1, 50_000]); // strictly positive load increase, newtons

        gen.Sample(t =>
        {
            var (stressBase, densityBase, areaBase, lengthBase, lowerLoad, increase) = t;
            var higherLoad = lowerLoad + increase;
            var definition = new BracketSectionCheckCalculationDefinition();

            BracketSectionCheckResult Run(double load) => definition.Calculate(
                new BracketSectionCheckInput(
                    pin,
                    new Quantity<Pressure>(stressBase, PressureUnits.Pascal),
                    new Quantity<MassDensity>(densityBase, MassDensityUnits.KilogramPerCubicMetre),
                    new Quantity<Force>(load, ForceUnits.Newton),
                    new Quantity<Area>(areaBase, AreaUnits.SquareMetre),
                    new Quantity<Length>(lengthBase, LengthUnits.Metre),
                    new Quantity<Mass>(1_000_000, MassUnits.Kilogram)),
                new CalculationContext());

            var lowerMargin = Run(lowerLoad).StressMargin;
            var higherMargin = Run(higherLoad).StressMargin;

            Assert.True(
                higherMargin < lowerMargin,
                $"Expected the margin at the higher load ({higherLoad} N, margin {higherMargin}) to be strictly less than at the lower load ({lowerLoad} N, margin {lowerMargin}).");
        });
    }
}
