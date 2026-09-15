using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.Tests.Materials;
using Tempest.Core.UnitsAndQuantities;
using Tempest.Workspace.Engineering;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// The calculator workbench (`WP 21.7B`): the catalogue by category, a form
/// turned into an input record and run, every kind of typing mistake named
/// by input, a refusal shown as the outcome, and material properties filled
/// from a released record rather than typed.
/// </summary>
public class CalculationModuleWorkbenchTests
{
    private static readonly ReleasedMaterialOption Steel = new("mat-s355j2", "S355J2", SteelPin);

    private static CalculationModuleDescriptor Module(string id) => CalculationModuleDescriptors.For(id)!;

    private static CalculationModuleWorkbench Workbench() => new(MaterialFixtures.BuildCatalog(), BareEngine());

    private static CalculationFormField F(string name, string text, string? unit = null) => new(name, text, unit);

    /// <summary>Example 1 of the beam specification, as typed into the form.</summary>
    private static List<CalculationFormField> BeamExample1() =>
    [
        new("Support", Choice: nameof(BeamSupport.SimplySupported)),
        new("Loading", Choice: nameof(BeamLoading.PointLoad)),
        F("Load", "10", "kN"),
        F("Span", "2000", "mm"),
        F("YoungsModulus", "210", "GPa"),
        F("SecondMomentOfArea", "2000000", "mm^4"),
        F("ExtremeFibreDistance", "50", "mm"),
        F("AllowableBendingStress", "165", "MPa"),
        F("DeflectionLimit", "8", "mm"),
    ];

    [Fact]
    public void TheCatalogue_ListsAllSixteenProductCalculations_GroupedByCategory()
    {
        var groups = CalculationModuleWorkbench.Catalogue();

        Assert.Equal(16, groups.Sum(g => g.Modules.Count));
        Assert.All(groups, g => Assert.NotEmpty(g.Modules));
        Assert.Equal(groups.Count, groups.Select(g => g.Category).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(groups, g => g.Category == "Structural" && g.Modules.Any(m => m.Id == BeamDeflectionCalculationDefinition.Id) && g.Modules.Any(m => m.Id == BoltShearCapacityCalculationDefinition.Id));
        Assert.Contains(groups, g => g.Category == "Fatigue" && g.Modules.Single().Id == FatigueMinerCalculationDefinition.Id);
    }

    [Fact]
    public async Task AFormForTheBeamExample_BuildsTheInputAndRuns_WithTheWorkingAndTheMaterialOnTheRecord()
    {
        var module = Module(BeamDeflectionCalculationDefinition.Id);
        var workbench = Workbench();

        var attempt = await workbench.CalculateAsync(module, BeamExample1(), Steel);

        Assert.True(attempt.Succeeded, string.Join("; ", attempt.Problems) + attempt.Rejection);
        var run = attempt.Run!;
        Assert.Equal("Meets its criteria", run.OutcomeSummary);
        Assert.False(run.IsRefused);
        Assert.Null(run.RefusalReason);
        Assert.NotEqual(Guid.Empty, run.RecordId);
        Assert.Contains(run.Results, r => r.Label == "Maximum deflection" && r.Display == "3.96825 mm");
        Assert.Contains(run.Results, r => r.Label == "Maximum bending stress" && r.Display == "125 MPa");
        Assert.Contains(run.Results, r => r.Label == "Maximum moment" && r.Display == "5000 N.m");
        Assert.Contains(run.Results, r => r.Label == "Stress criterion met" && r.Display == "Yes");
        Assert.DoesNotContain(run.Results, r => r.Label == "Outcome");
        Assert.Contains(run.Working, w => w.Label == "Maximum bending moment");
        Assert.Contains(run.Working, w => w.Label == "Span-to-depth ratio" && w.Display == "20");
        Assert.Contains(run.Checks, c => c.IsSatisfied && c.Description.Contains("allowable bending stress", StringComparison.Ordinal));
        Assert.Equal("Valid", run.ValidationOutcome);
        Assert.Contains(SteelPin.RecordId, run.ReferencedMaterialIds);
    }

    [Fact]
    public void ATypingMistake_IsNamedByInput_AndNothingRuns()
    {
        var module = Module(BeamDeflectionCalculationDefinition.Id);
        var fields = BeamExample1();
        fields[3] = F("Span", "2x", "mm");
        fields[4] = F("YoungsModulus", "210", "kg");

        var build = CalculationModuleWorkbench.BuildInput(module, fields, Steel);

        Assert.False(build.Succeeded);
        Assert.Null(build.Input);
        Assert.Contains(build.Problems, p => p.InputName == "Span" && p.Label == "Span L" && p.Problem.Contains("'2x' is not a number", StringComparison.Ordinal));
        Assert.Contains(build.Problems, p => p.InputName == "YoungsModulus" && p.Problem.Contains("not a unit of Pressure", StringComparison.Ordinal));
        Assert.Equal(2, build.Problems.Count);
    }

    [Fact]
    public void ARequiredMaterialNotPicked_IsAProblem_AnOptionalOneIsNot()
    {
        var beam = CalculationModuleWorkbench.BuildInput(Module(BeamDeflectionCalculationDefinition.Id), BeamExample1(), material: null);
        Assert.Contains(beam.Problems, p => p.InputName == "MaterialPin" && p.Problem.Contains("pick a released material", StringComparison.Ordinal));

        var fatigue = CalculationModuleWorkbench.BuildInput(
            Module(FatigueMinerCalculationDefinition.Id),
            [F("CurveReference", "EN 1993-1-9 detail category 71"), F("ReferenceStressRange", "71", "MPa"), F("ReferenceCycles", "2000000"), F("Slope", "3"), new("Blocks", Rows: ["100 MPa, 100000"])],
            material: null);

        Assert.True(fatigue.Succeeded, string.Join("; ", fatigue.Problems));
        var input = Assert.IsType<FatigueMinerInput>(fatigue.Input);
        Assert.Null(input.MaterialPin);
        Assert.Null(input.EnduranceLimit);
        Assert.Single(input.Blocks);
    }

    [Fact]
    public async Task TheDefinitionsOwnRejection_ComesBackInItsWords()
    {
        var fields = BeamExample1();
        fields[2] = F("Load", "-10", "kN");

        var attempt = await Workbench().CalculateAsync(Module(BeamDeflectionCalculationDefinition.Id), fields, Steel);

        Assert.False(attempt.Succeeded);
        Assert.Empty(attempt.Problems);
        Assert.Contains("Load must be positive", attempt.Rejection, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusal_IsTheOutcome_NotAnError()
    {
        var fields = BeamExample1();
        fields[3] = F("Span", "200", "mm");

        var attempt = await Workbench().CalculateAsync(Module(BeamDeflectionCalculationDefinition.Id), fields, Steel);

        Assert.True(attempt.Succeeded);
        var run = attempt.Run!;
        Assert.True(run.IsRefused);
        Assert.Contains("refused", run.OutcomeSummary, StringComparison.Ordinal);
        Assert.Contains("span-to-depth", run.RefusalReason, StringComparison.Ordinal);
        Assert.Contains(run.Results, r => r.Label == "Maximum deflection" && r.Display == "—");
        Assert.Equal("Conditional", run.ValidationOutcome);
    }

    [Fact]
    public async Task AListInput_IsTypedOneRowPerLine_WithUnits()
    {
        var module = Module(BoltGroupEccentricShearCalculationDefinition.Id);
        var fields = new List<CalculationFormField>
        {
            F("FastenerGrade", "ISO 898-1 class 8.8 M16"),
            new("Bolts", Rows: ["75 mm, 50 mm", "75 mm, -50 mm", "", "-75 mm, 50 mm", "-75 mm, -50 mm"]),
            F("LoadX", "0", "kN"),
            F("LoadY", "-20", "kN"),
            F("LoadPointX", "250", "mm"),
            F("LoadPointY", "0", "mm"),
            F("AllowableShearPerBolt", "25", "kN"),
        };

        var attempt = await Workbench().CalculateAsync(module, fields, material: null);

        Assert.True(attempt.Succeeded, string.Join("; ", attempt.Problems) + attempt.Rejection);
        Assert.Contains(attempt.Run!.Results, r => r.Label == "Governing bolt force" && r.Display == "18239.9 N");
        Assert.Contains(attempt.Run.Results, r => r.Label == "Bolt forces" && r.Display.StartsWith("18239.9 N; 18239.9 N; 10095.7 N", StringComparison.Ordinal));
        Assert.Contains(attempt.Run.Results, r => r.Label == "Governing bolt index" && r.Display == "0");

        fields[1] = new("Bolts", Rows: ["75, 50"]);
        var build = CalculationModuleWorkbench.BuildInput(module, fields, null);
        Assert.Contains(build.Problems, p => p.InputName == "Bolts" && p.Problem.Contains("needs a unit", StringComparison.Ordinal));

        fields[1] = new("Bolts", Rows: ["75 mm"]);
        build = CalculationModuleWorkbench.BuildInput(module, fields, null);
        Assert.Contains(build.Problems, p => p.InputName == "Bolts" && p.Problem.Contains("expected 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnOriginalDefinition_RunsFromTheSameForm()
    {
        // Bolt shear: pi/4 x 20^2 x 2 planes x 400 MPa / 1.5 = 167 552 N.
        var attempt = await Workbench().CalculateAsync(
            Module(BoltShearCapacityCalculationDefinition.Id),
            [F("Diameter", "20", "mm"), F("UltimateShearStrength", "400", "MPa"), F("ShearPlanes", "2"), F("SafetyFactor", "1.5")],
            material: null);

        Assert.True(attempt.Succeeded, string.Join("; ", attempt.Problems) + attempt.Rejection);
        Assert.Equal("Computed", attempt.Run!.OutcomeSummary);
        Assert.Contains(attempt.Run.Results, r => r.Label == "Allowable shear capacity" && r.Display == "167552 N");
        Assert.Contains(attempt.Run.Working, w => w.Label == "Safety Factor" && w.Display == "1.5");

        var notWhole = CalculationModuleWorkbench.BuildInput(
            Module(BoltShearCapacityCalculationDefinition.Id),
            [F("Diameter", "20", "mm"), F("UltimateShearStrength", "400", "MPa"), F("ShearPlanes", "1.5"), F("SafetyFactor", "1.5")], null);
        Assert.Contains(notWhole.Problems, p => p.InputName == "ShearPlanes" && p.Problem.Contains("whole number", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnlyReleasedMaterialsAreOffered_AndAPickedOneFillsItsPropertiesInTheFormsOwnUnits()
    {
        var materials = MaterialFixtures.BuildCatalog();
        await materials.RegisterAsync("mat-fx-steel", MaterialFixtures.Steel(), MaterialFixtures.Sourced());
        await materials.RegisterAsync("mat-fx-draft", MaterialFixtures.Steel("FX-DRAFT"), MaterialFixtures.Sourced());
        await ReleaseAsync(materials, "mat-fx-steel");
        var workbench = new CalculationModuleWorkbench(materials, BareEngine());

        var offered = await workbench.ListReleasedMaterialsAsync();
        var option = Assert.Single(offered);
        Assert.Equal("mat-fx-steel", option.RecordId);
        Assert.Contains("FX-STEEL-1", option.Label, StringComparison.Ordinal);
        Assert.Equal("mat-fx-steel", option.Pin.RecordId);

        var fill = await workbench.FillFromMaterialAsync(Module(ColumnBucklingCalculationDefinition.Id), option.RecordId);
        Assert.Empty(fill.Problems);
        Assert.Contains(fill.Fields, f => f.Name == "YoungsModulus" && f.Text == "200" && f.UnitSymbol == "GPa");
        Assert.Contains(fill.Fields, f => f.Name == "YieldStrength" && f.Text == "300" && f.UnitSymbol == "MPa");
        Assert.Equal(option.Pin, fill.Material.Pin);

        // The fixture steel records no expansion coefficient: the thermal
        // module's own field is named as unfillable, never guessed.
        var thermal = await workbench.FillFromMaterialAsync(Module(ThermalExpansionStressCalculationDefinition.Id), option.RecordId);
        Assert.Contains(thermal.Fields, f => f.Name == "YoungsModulus");
        Assert.Contains(thermal.Problems, p => p.InputName == "ExpansionCoefficient" && p.Problem.Contains("records no", StringComparison.Ordinal));

        // And the run built on the fill cites the record it stood on.
        var fields = BeamExample1().Where(f => f.Name is not ("YoungsModulus" or "AllowableBendingStress")).ToList();
        fields.AddRange((await workbench.FillFromMaterialAsync(Module(BeamDeflectionCalculationDefinition.Id), option.RecordId)).Fields);
        var attempt = await workbench.CalculateAsync(Module(BeamDeflectionCalculationDefinition.Id), fields, option);
        Assert.True(attempt.Succeeded, string.Join("; ", attempt.Problems) + attempt.Rejection);
        Assert.Contains("mat-fx-steel", attempt.Run!.ReferencedMaterialIds);
        Assert.Contains(attempt.Run.Results, r => r.Label == "Maximum deflection" && r.Display == "4.16667 mm");
    }

    [Fact]
    public void LabelsAndValues_ReadAsAnEngineerWouldWriteThem()
    {
        Assert.Equal("Maximum bending stress", CalculationModuleWorkbench.Humanise("MaximumBendingStress"));
        Assert.Equal("Bore von mises stress", CalculationModuleWorkbench.Humanise("BoreVonMisesStress"));
        Assert.Equal("Load ratio", CalculationModuleWorkbench.Humanise("LoadRatio"));
        Assert.Equal("125 MPa", CalculationModuleWorkbench.Format(new Quantity<Pressure>(125, PressureUnits.Megapascal)));
        Assert.Equal("3.96825 mm", CalculationModuleWorkbench.Format(new Quantity<Length>(3.968253968, LengthUnits.Millimetre)));
        Assert.Equal("—", CalculationModuleWorkbench.Format(null));
        Assert.Equal("Yes", CalculationModuleWorkbench.Format(true));
        Assert.Equal("Outside method limits", CalculationModuleWorkbench.Format(EngineeringCheckOutcome.OutsideMethodLimits));
        Assert.Equal("1 N; 2 N", CalculationModuleWorkbench.Format(new[] { new Quantity<Force>(1, ForceUnits.Newton), new Quantity<Force>(2, ForceUnits.Newton) }));
        Assert.Equal("Materials/mat-s355j2@1", CalculationModuleWorkbench.Format(SteelPin));
    }

    private static async Task ReleaseAsync(IMaterialCatalog materials, string recordId)
    {
        var principals = new CurrentPrincipalAccessor();
        principals.SetCurrent(new PlatformPrincipal(new PlatformIdentity("reviewer-21-7b", "reviewer-21-7b"), []));
        var review = new ReferenceReviewService(principals);
        await review.VerifyAsync(materials, recordId, new ReferenceReviewStatement("Fixture materials handbook, Table 3"));
        await review.ReleaseAsync(materials, recordId, "Required for a WP 21.7B workbench test.");
    }
}
