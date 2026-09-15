using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// The canonical comparison (`TD-29`) of two module runs reads as an
/// engineer would write it: an outcome as spaced words, a utilisation to
/// six figures, a quantity with its unit — the strings the Compare with
/// previous table shows verbatim (`WP 21.7C`).
/// </summary>
public class ModuleComparisonDisplayTests
{
    private static BeamDeflectionInput Beam(double limitMillimetres) => new(
        SteelPin,
        BeamSupport.SimplySupported,
        BeamLoading.PointLoad,
        new Quantity<Force>(10, ForceUnits.Kilonewton),
        new Quantity<Length>(2000, LengthUnits.Millimetre),
        new Quantity<Pressure>(210, PressureUnits.Gigapascal),
        new Quantity<SecondMomentOfArea>(2_000_000, SecondMomentOfAreaUnits.MillimetreToTheFourth),
        new Quantity<Length>(50, LengthUnits.Millimetre),
        new Quantity<Pressure>(355, PressureUnits.Megapascal),
        new Quantity<Length>(limitMillimetres, LengthUnits.Millimetre));

    [Fact]
    public async Task AChangedDeflectionLimit_ComparesAsWords_Figures_AndQuantitiesWithUnits()
    {
        var engine = BareEngine();
        var meets = await engine.ExecuteAsync<BeamDeflectionInput, BeamDeflectionResult>(BeamDeflectionCalculationDefinition.Id, Beam(8));
        var fails = await engine.ExecuteAsync<BeamDeflectionInput, BeamDeflectionResult>(BeamDeflectionCalculationDefinition.Id, Beam(3));

        var comparison = await engine.CompareAsync<BeamDeflectionInput, BeamDeflectionResult>(meets.Id, fails.Id);

        var limit = Assert.Single(comparison.InputChanges);
        Assert.Equal(nameof(BeamDeflectionInput.DeflectionLimit), limit.FieldName);
        Assert.Equal("8 mm", limit.OldDisplay);
        Assert.Equal("3 mm", limit.NewDisplay);

        Assert.Contains(comparison.ResultChanges, d => d.FieldName == nameof(BeamDeflectionResult.Outcome) && d.OldDisplay == "Meets criteria" && d.NewDisplay == "Does not meet criteria");
        Assert.Contains(comparison.ResultChanges, d => d.FieldName == nameof(BeamDeflectionResult.DeflectionUtilisation) && d.OldDisplay == "0.496032" && d.NewDisplay == "1.32275");
        Assert.Contains(comparison.ResultChanges, d => d.FieldName == nameof(BeamDeflectionResult.DeflectionCriterionMet) && d.OldDisplay == "True" && d.NewDisplay == "False");
        Assert.DoesNotContain(comparison.ResultChanges, d => d.FieldName == nameof(BeamDeflectionResult.MaximumDeflection));
        Assert.DoesNotContain(comparison.ResultChanges, d => (d.OldDisplay ?? string.Empty).Length > 12 && double.TryParse(d.OldDisplay, out _));
    }

    [Fact]
    public async Task TheConstraintLines_ShowTheValueInTheFormsUnit_LiveAndReadBack()
    {
        var engine = BareEngine();
        var executed = await engine.ExecuteAsync<BeamDeflectionInput, BeamDeflectionResult>(BeamDeflectionCalculationDefinition.Id, Beam(8));
        var readBack = (await engine.FindRecordAsync<BeamDeflectionResult>(executed.Id))!;

        foreach (var record in new[] { executed, readBack })
        {
            Assert.Contains(record.Validation.ConstraintChecks, c => c.Description == "Young's modulus must be positive." && c.Detail == "Received 210 GPa.");
            Assert.Contains(record.Validation.ConstraintChecks, c => c.Description == "Span must be positive." && c.Detail == "Received 2000 mm.");
            Assert.Contains(record.Validation.ConstraintChecks, c => c.Description == "Load must be positive." && c.Detail == "Received 10 kN.");
            Assert.Contains(record.Validation.ConstraintChecks, c => c.Description == "Second moment of area must be positive." && c.Detail == "Received 2000000 mm^4.");
            Assert.DoesNotContain(record.Validation.ConstraintChecks, c => (c.Detail ?? string.Empty).EndsWith(" Pa.", StringComparison.Ordinal) || (c.Detail ?? string.Empty).EndsWith(" m.", StringComparison.Ordinal));
        }
    }
}
