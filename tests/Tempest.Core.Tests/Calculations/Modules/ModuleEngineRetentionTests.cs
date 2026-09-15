using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// The `WP 21.7A` modules on the `WP 21.3A` engine: intermediates are typed
/// and read back as the quantities they were recorded as, inputs are
/// retained on the record so a calculation re-runs unchanged or with a
/// changed parameter, and two runs compare field by field.
/// </summary>
public class ModuleEngineRetentionTests
{
    private static BeamDeflectionInput Example1() => new(
        SteelPin, BeamSupport.SimplySupported, BeamLoading.PointLoad,
        new Quantity<Force>(10, ForceUnits.Kilonewton),
        new Quantity<Length>(2, LengthUnits.Metre),
        new Quantity<Pressure>(210, PressureUnits.Gigapascal),
        new Quantity<SecondMomentOfArea>(2.0e6, SecondMomentOfAreaUnits.MillimetreToTheFourth),
        new Quantity<Length>(50, LengthUnits.Millimetre),
        new Quantity<Pressure>(165, PressureUnits.Megapascal),
        new Quantity<Length>(8, LengthUnits.Millimetre));

    [Fact]
    public async Task IntermediatesAreTyped_AndReadBackAsTheQuantitiesTheyWereRecordedAs()
    {
        var engine = BareEngine();

        var executed = await engine.ExecuteAsync<BeamDeflectionInput, BeamDeflectionResult>(BeamDeflectionCalculationDefinition.Id, Example1());
        var readBack = (await engine.FindRecordAsync<BeamDeflectionResult>(executed.Id))!;

        // In memory, straight from the definition.
        var moment = executed.IntermediateResults.Single(i => i.Name == "Maximum bending moment");
        Assert.Equal(typeof(Quantity<Torque>).FullName, moment.ValueTypeName);
        Assert.Equal(5000.0, moment.As<Quantity<Torque>>().BaseValue, 1e-9);

        // After the round trip through the store: the same typed value, not a
        // JSON fragment, and a wrong type is refused rather than coerced.
        var stored = readBack.IntermediateResults.Single(i => i.Name == "Maximum bending moment");
        Assert.Equal(5000.0, stored.As<Quantity<Torque>>().BaseValue, 1e-9);
        Assert.Throws<CalculationReadbackException>(() => stored.As<Quantity<Force>>());

        var ratio = readBack.IntermediateResults.Single(i => i.Name == "Span-to-depth ratio");
        Assert.Equal(20.0, ratio.As<double>(), 1e-9);
    }

    [Fact]
    public async Task TheInputIsRetained_AndARecordReRunsUnchanged_WithItsPredecessorNamed()
    {
        var engine = BareEngine();

        var first = await engine.ExecuteAsync<BeamDeflectionInput, BeamDeflectionResult>(BeamDeflectionCalculationDefinition.Id, Example1());

        Assert.NotNull(first.Input);
        Assert.Equal(typeof(BeamDeflectionInput).FullName, first.InputTypeName);
        Assert.Null(first.PredecessorRecordId);

        var again = await engine.ReRunAsync<BeamDeflectionInput, BeamDeflectionResult>(first.Id);

        Assert.NotEqual(first.Id, again.Id);
        Assert.Equal(first.Id, again.PredecessorRecordId);
        Assert.Equal(first.Result.MaximumDeflection!.Value.BaseValue, again.Result.MaximumDeflection!.Value.BaseValue, 1e-12);
        Assert.Equal(first.Result.Outcome, again.Result.Outcome);

        // The retained input survives the store as the record type it was.
        var stored = (await engine.FindRecordAsync<BeamDeflectionResult>(first.Id))!;
        Assert.NotNull(stored.Input);
        Assert.Equal(typeof(BeamDeflectionInput).FullName, stored.InputTypeName);
    }

    [Fact]
    public async Task AReRunWithAChangedParameter_ComparesAgainstItsPredecessor_FieldByField()
    {
        var engine = BareEngine();

        var first = await engine.ExecuteAsync<BeamDeflectionInput, BeamDeflectionResult>(BeamDeflectionCalculationDefinition.Id, Example1());
        var heavier = Example1() with { Load = new Quantity<Force>(20, ForceUnits.Kilonewton) };
        var second = await engine.ReRunAsync<BeamDeflectionInput, BeamDeflectionResult>(first.Id, heavier);

        Assert.Equal(first.Id, second.PredecessorRecordId);
        Assert.Equal(2.0 * first.Result.MaximumDeflection!.Value.BaseValue, second.Result.MaximumDeflection!.Value.BaseValue, 1e-9);

        var comparison = await engine.CompareAsync<BeamDeflectionInput, BeamDeflectionResult>(first.Id, second.Id);

        Assert.Null(comparison.InputComparisonNote);
        Assert.Contains(comparison.InputChanges, d => d.FieldName == nameof(BeamDeflectionInput.Load));
        Assert.DoesNotContain(comparison.InputChanges, d => d.FieldName == nameof(BeamDeflectionInput.Span));
        Assert.Contains(comparison.ResultChanges, d => d.FieldName == nameof(BeamDeflectionResult.MaximumDeflection));
        Assert.Contains(comparison.ResultChanges, d => d.FieldName == nameof(BeamDeflectionResult.MaximumMoment));
        Assert.Contains(comparison.ResultChanges, d => d.FieldName == nameof(BeamDeflectionResult.Outcome));
        Assert.DoesNotContain(comparison.ResultChanges, d => d.FieldName == nameof(BeamDeflectionResult.SpanToDepthRatio));
    }

    [Fact]
    public async Task ARefusedRunRetainsItsInputToo_SoTheEngineerCanReRunInsideTheMethod()
    {
        var engine = BareEngine();
        var stubby = Example1() with { Span = new Quantity<Length>(200, LengthUnits.Millimetre) };

        var refused = await engine.ExecuteAsync<BeamDeflectionInput, BeamDeflectionResult>(BeamDeflectionCalculationDefinition.Id, stubby);
        Assert.Equal(EngineeringCheckOutcome.OutsideMethodLimits, refused.Result.Outcome);
        Assert.Equal(CalculationValidationOutcome.Conditional, refused.Validation.Outcome);

        var inside = await engine.ReRunAsync<BeamDeflectionInput, BeamDeflectionResult>(refused.Id, Example1());

        Assert.Equal(refused.Id, inside.PredecessorRecordId);
        Assert.Equal(EngineeringCheckOutcome.MeetsCriteria, inside.Result.Outcome);

        var comparison = await engine.CompareAsync<BeamDeflectionInput, BeamDeflectionResult>(refused.Id, inside.Id);
        Assert.Contains(comparison.ResultChanges, d => d.FieldName == nameof(BeamDeflectionResult.RefusalReason));
    }

    [Fact]
    public async Task ALargeBoltGroup_StaysInsideTheIntermediateBound_ByRecordingItsForcesAsOneList()
    {
        // The context refuses more than 200 intermediates. A 400-bolt group
        // records one list, not 400 entries, so the module cannot trip the
        // bound however large the group.
        var engine = BareEngine();
        var bolts = Enumerable.Range(0, 400)
            .Select(i => new BoltPosition(new Quantity<Length>(i % 20 * 50, LengthUnits.Millimetre), new Quantity<Length>(i / 20 * 50, LengthUnits.Millimetre)))
            .ToList();
        var input = new BoltGroupEccentricShearInput(
            FastenerGrade, bolts,
            new Quantity<Force>(0, ForceUnits.Newton), new Quantity<Force>(-500, ForceUnits.Kilonewton),
            new Quantity<Length>(2000, LengthUnits.Millimetre), new Quantity<Length>(0, LengthUnits.Millimetre),
            new Quantity<Force>(50, ForceUnits.Kilonewton));

        var record = await engine.ExecuteAsync<BoltGroupEccentricShearInput, BoltGroupEccentricShearResult>(BoltGroupEccentricShearCalculationDefinition.Id, input);

        Assert.Equal(400, record.Result.BoltForces!.Count);
        Assert.True(record.IntermediateResults.Count < CalculationContext.DefaultMaxIntermediateResultCount);

        var forces = record.IntermediateResults.Single(i => i.Name == "Bolt forces").As<Quantity<Force>[]>();
        Assert.Equal(400, forces.Length);
    }
}
