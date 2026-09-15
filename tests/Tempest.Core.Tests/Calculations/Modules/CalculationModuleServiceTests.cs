using Tempest.Core.Bearings;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Tests.Bearings;
using Tempest.Core.Tests.Fasteners;
using Tempest.Core.Tests.Materials;
using Tempest.Core.UnitsAndQuantities;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// The governed entry point of every calculation module (`WP 21.7C`,
/// closing `WP 21.7B`): a form-less caller names a module and its inputs,
/// references by released-record id, and gets the identical record a form
/// would have written — or the refusal, in the service's own terms. One
/// material per reference input, fastener and bearing records from their
/// own released libraries, every kind of typing mistake named by input, a
/// refusal shown as the outcome.
/// </summary>
public class CalculationModuleServiceTests
{
    private const string ReviewerId = "reviewer-21-7c";
    private const string FixtureBolt = "fst-fx-bolt";
    private const string FixtureBearing = "brg-fx-6000";
    private const string FixtureSteel = "mat-fx-steel";

    private sealed record Libraries(CalculationModuleService Service, MaterialCatalog Materials, FastenerCatalog Fasteners, BearingCatalog Bearings);

    private static Libraries Build() =>
        BuildWith(MaterialFixtures.BuildCatalog(), FastenerFixtures.BuildCatalog(), BearingFixtures.BuildCatalog());

    private static Libraries BuildWith(MaterialCatalog materials, FastenerCatalog fasteners, BearingCatalog bearings) =>
        new(new CalculationModuleService(materials, fasteners, bearings, BareEngine()), materials, fasteners, bearings);

    private static CalculationModuleDescriptor Module(string id) => CalculationModuleDescriptors.For(id)!;

    private static CalculationFormField F(string name, string text, string? unit = null) => new(name, text, unit);

    private static CalculationFormField Pin(string name, string recordId) => new(name, RecordId: recordId);

    private static Task<CalculationModuleOutcome> RunAsync(Libraries libraries, string id, IReadOnlyList<CalculationFormField> fields) =>
        libraries.Service.RunAsync(new CalculationModuleRequest(id, fields));

    /// <summary>Example 1 of the beam specification, as typed into the form, with the seeded S355J2 picked.</summary>
    private static List<CalculationFormField> BeamExample1() =>
    [
        Pin("MaterialPin", MaterialSeed.S355J2),
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

    private static async Task<Libraries> WithSeededSteelAsync()
    {
        var libraries = Build();
        await new ReferenceSeedService().ApplyAsync(libraries.Materials, MaterialSeed.Instance);
        await ReleaseAsync(libraries.Materials, MaterialSeed.S355J2);
        return libraries;
    }

    // ---- Running from typed inputs ----

    [Fact]
    public async Task TheBeamExample_RunsOnTheSeededSteel_WithTheRecordsOwnValuesAndTheMaterialCited()
    {
        var libraries = await WithSeededSteelAsync();

        var outcome = await RunAsync(libraries, BeamDeflectionCalculationDefinition.Id, BeamExample1());

        Assert.True(outcome.WasPerformed, outcome.Reason);
        Assert.Equal(CalculationModuleRefusal.None, outcome.Refusal);
        var run = outcome.Run!;
        Assert.Equal("Meets its criteria", run.OutcomeSummary);
        Assert.False(run.IsRefused);
        Assert.NotEqual(Guid.Empty, run.RecordId);
        Assert.Contains(run.Results, r => r.Label == "Maximum deflection" && r.Display == "3.96825 mm");
        Assert.Contains(run.Results, r => r.Label == "Maximum moment" && r.Display == "5000 N.m");
        Assert.Contains(run.Working, w => w.Label == "Span-to-depth ratio" && w.Display == "20");
        Assert.Equal("Valid", run.ValidationOutcome);
        Assert.Contains(MaterialSeed.S355J2, run.ReferencedMaterialIds);

        // The record is the source: the seeded 355 MPa yield stands as the
        // allowable, not the 165 MPa typed, and the fill says so.
        var fill = Assert.Single(outcome.Fills);
        Assert.Equal("MaterialPin", fill.ReferenceInputName);
        Assert.Contains(fill.Fields, f => f.Name == "AllowableBendingStress" && f.Text == "355" && f.UnitSymbol == "MPa");
        Assert.Contains(run.Results, r => r.Label == "Maximum bending stress" && r.Display == "125 MPa");
        Assert.Contains(run.Results, r => r.Label == "Stress criterion met" && r.Display == "Yes");
    }

    [Fact]
    public async Task ATypingMistake_IsNamedByInput_AndNothingRuns()
    {
        var libraries = await WithSeededSteelAsync();
        var fields = BeamExample1();
        fields[4] = F("Span", "2x", "mm");
        fields[6] = F("SecondMomentOfArea", "2000000", "kg");

        var outcome = await RunAsync(libraries, BeamDeflectionCalculationDefinition.Id, fields);

        Assert.False(outcome.WasPerformed);
        Assert.Equal(CalculationModuleRefusal.InputIncomplete, outcome.Refusal);
        Assert.Null(outcome.Run);
        Assert.Contains(outcome.Problems, p => p.InputName == "Span" && p.Label == "Span L" && p.Problem.Contains("'2x' is not a number", StringComparison.Ordinal));
        Assert.Contains(outcome.Problems, p => p.InputName == "SecondMomentOfArea" && p.Problem.Contains("not a unit of SecondMomentOfArea", StringComparison.Ordinal));
        Assert.Equal(2, outcome.Problems.Count);
        Assert.Contains("Span L", outcome.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARequiredReferenceNotPicked_IsAProblem_AnOptionalOneIsNot()
    {
        var libraries = Build();

        var beam = await RunAsync(libraries, BeamDeflectionCalculationDefinition.Id, BeamExample1().Where(f => f.Name != "MaterialPin").ToList());
        Assert.Equal(CalculationModuleRefusal.InputIncomplete, beam.Refusal);
        Assert.Contains(beam.Problems, p => p.InputName == "MaterialPin" && p.Problem.Contains("pick a released", StringComparison.Ordinal));

        var fatigue = await RunAsync(
            libraries,
            FatigueMinerCalculationDefinition.Id,
            [F("CurveReference", "EN 1993-1-9 detail category 71"), F("ReferenceStressRange", "71", "MPa"), F("ReferenceCycles", "2000000"), F("Slope", "3"), new("Blocks", Rows: ["100 MPa, 100000"])]);

        Assert.True(fatigue.WasPerformed, fatigue.Reason);
        Assert.Empty(fatigue.Fills);
        Assert.Empty(fatigue.Run!.ReferencedMaterialIds);
    }

    [Fact]
    public async Task TheDefinitionsOwnRejection_ComesBackInItsWords_AsInputInvalid()
    {
        var libraries = await WithSeededSteelAsync();
        var fields = BeamExample1();
        fields[3] = F("Load", "-10", "kN");

        var outcome = await RunAsync(libraries, BeamDeflectionCalculationDefinition.Id, fields);

        Assert.False(outcome.WasPerformed);
        Assert.Equal(CalculationModuleRefusal.InputInvalid, outcome.Refusal);
        Assert.Empty(outcome.Problems);
        Assert.Contains("Load must be positive", outcome.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusal_IsTheOutcome_NotAnError()
    {
        var libraries = await WithSeededSteelAsync();
        var fields = BeamExample1();
        fields[4] = F("Span", "200", "mm");

        var outcome = await RunAsync(libraries, BeamDeflectionCalculationDefinition.Id, fields);

        Assert.True(outcome.WasPerformed, outcome.Reason);
        var run = outcome.Run!;
        Assert.True(run.IsRefused);
        Assert.Contains("refused", run.OutcomeSummary, StringComparison.Ordinal);
        Assert.Contains("span-to-depth", run.RefusalReason, StringComparison.Ordinal);
        Assert.Contains(run.Results, r => r.Label == "Maximum deflection" && r.Display == "—");
        Assert.Equal("Conditional", run.ValidationOutcome);
    }

    [Fact]
    public async Task AListInput_IsTypedOneRowPerLine_WithUnits_AndNoFastenerNeedBePicked()
    {
        var libraries = Build();
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

        var outcome = await RunAsync(libraries, BoltGroupEccentricShearCalculationDefinition.Id, fields);

        Assert.True(outcome.WasPerformed, outcome.Reason);
        Assert.Contains(outcome.Run!.Results, r => r.Label == "Governing bolt force" && r.Display == "18239.9 N");
        Assert.Contains(outcome.Run.Results, r => r.Label == "Governing bolt index" && r.Display == "0");
        Assert.DoesNotContain(outcome.Run.Working, w => w.Label == "Fastener reference");

        fields[1] = new("Bolts", Rows: ["75, 50"]);
        var noUnit = await RunAsync(libraries, BoltGroupEccentricShearCalculationDefinition.Id, fields);
        Assert.Equal(CalculationModuleRefusal.InputIncomplete, noUnit.Refusal);
        Assert.Contains(noUnit.Problems, p => p.InputName == "Bolts" && p.Problem.Contains("needs a unit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnOriginalDefinition_RunsFromTheSameRequest()
    {
        // Bolt shear: pi/4 x 20^2 x 2 planes x 400 MPa / 1.5 = 167 552 N.
        var outcome = await RunAsync(
            Build(),
            BoltShearCapacityCalculationDefinition.Id,
            [F("Diameter", "20", "mm"), F("UltimateShearStrength", "400", "MPa"), F("ShearPlanes", "2"), F("SafetyFactor", "1.5")]);

        Assert.True(outcome.WasPerformed, outcome.Reason);
        Assert.Equal("Computed", outcome.Run!.OutcomeSummary);
        Assert.Contains(outcome.Run.Results, r => r.Label == "Allowable shear capacity" && r.Display == "167552 N");
        Assert.Contains(outcome.Run.Working, w => w.Label == "Safety Factor" && w.Display == "1.5");
    }

    [Fact]
    public async Task AnUnknownModule_IsRefusedByName()
    {
        var outcome = await RunAsync(Build(), "calc.no-such-module", []);

        Assert.Equal(CalculationModuleRefusal.ModuleNotFound, outcome.Refusal);
        Assert.Contains("calc.no-such-module", outcome.Reason, StringComparison.Ordinal);
    }

    // ---- Materials: one record per reference input ----

    [Fact]
    public async Task TheLug_TakesItsOwnMaterialForTheLugAndAnotherForThePin_AndCitesBoth()
    {
        var libraries = await WithSeededSteelAsync();
        await libraries.Materials.RegisterAsync(FixtureSteel, MaterialFixtures.Steel(), MaterialFixtures.Sourced());
        await ReleaseAsync(libraries.Materials, FixtureSteel);

        var outcome = await RunAsync(
            libraries,
            LiftingLugPinJointCalculationDefinition.Id,
            [
                Pin("LugMaterialPin", MaterialSeed.S355J2),
                Pin("PinMaterialPin", FixtureSteel),
                F("Load", "50", "kN"), F("LugThickness", "20", "mm"), F("LugWidth", "100", "mm"), F("HoleDiameter", "32", "mm"), F("PinDiameter", "30", "mm"),
                F("EdgeDistance", "40", "mm"), F("CheekPlateThickness", "12", "mm"), F("Clearance", "2", "mm"),
                F("AllowableTensileStress", "150", "MPa"), F("AllowableBearingStress", "200", "MPa"), F("AllowableShearStress", "90", "MPa"),
                F("PinAllowableBendingStress", "250", "MPa"), F("PinAllowableShearStress", "150", "MPa"),
            ]);

        Assert.True(outcome.WasPerformed, outcome.Reason);
        Assert.Equal(2, outcome.Fills.Count);
        Assert.Contains(outcome.Fills, f => f.ReferenceInputName == "LugMaterialPin" && f.Record.RecordId == MaterialSeed.S355J2);
        Assert.Contains(outcome.Fills, f => f.ReferenceInputName == "PinMaterialPin" && f.Record.RecordId == FixtureSteel);
        Assert.Contains(MaterialSeed.S355J2, outcome.Run!.ReferencedMaterialIds);
        Assert.Contains(FixtureSteel, outcome.Run.ReferencedMaterialIds);

        // The same record for both is a valid pick, cited once per pin.
        var same = outcome.Fills.Select(f => f.Record.Pin).Distinct().Count();
        Assert.Equal(2, same);
    }

    [Fact]
    public async Task OnlyReleasedMaterialsAreOffered_AndAPickedOneFillsInTheFormsOwnUnits()
    {
        var libraries = Build();
        await libraries.Materials.RegisterAsync(FixtureSteel, MaterialFixtures.Steel(), MaterialFixtures.Sourced());
        await libraries.Materials.RegisterAsync("mat-fx-draft", MaterialFixtures.Steel("FX-DRAFT"), MaterialFixtures.Sourced());
        await ReleaseAsync(libraries.Materials, FixtureSteel);

        var offered = await libraries.Service.ListReleasedAsync(ReferenceLibrary.Materials);
        var option = Assert.Single(offered);
        Assert.Equal(ReferenceLibrary.Materials, option.Library);
        Assert.Equal(FixtureSteel, option.RecordId);
        Assert.Contains("FX-STEEL-1", option.Label, StringComparison.Ordinal);
        Assert.Equal(FixtureSteel, option.Pin.RecordId);

        var fill = await libraries.Service.FillFromRecordAsync(Module(ColumnBucklingCalculationDefinition.Id), "MaterialPin", FixtureSteel);
        Assert.Empty(fill.Problems);
        Assert.Contains(fill.Fields, f => f.Name == "YoungsModulus" && f.Text == "200" && f.UnitSymbol == "GPa");
        Assert.Contains(fill.Fields, f => f.Name == "YieldStrength" && f.Text == "300" && f.UnitSymbol == "MPa");
        Assert.Equal(option.Pin, fill.Record.Pin);

        // The fixture steel records no expansion coefficient: the thermal
        // module's own field is named as unfillable, never guessed, and a
        // run on it is refused as an incomplete record.
        var thermal = await libraries.Service.FillFromRecordAsync(Module(ThermalExpansionStressCalculationDefinition.Id), "MaterialPin", FixtureSteel);
        Assert.Contains(thermal.Fields, f => f.Name == "YoungsModulus");
        Assert.Contains(thermal.Problems, p => p.InputName == "ExpansionCoefficient" && p.Problem.Contains("records no", StringComparison.Ordinal));

        var refused = await RunAsync(
            libraries,
            ThermalExpansionStressCalculationDefinition.Id,
            [Pin("MaterialPin", FixtureSteel), F("Length", "2000", "mm"), F("Area", "1000", "mm²"), F("TemperatureChange", "60", "K"), F("RestraintStiffness", "0", "kN/mm")]);
        Assert.Equal(CalculationModuleRefusal.RecordIncomplete, refused.Refusal);
        Assert.Contains("Expansion coefficient", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreleasedOrUnknownRecord_IsRefused_NotRun()
    {
        var libraries = Build();
        await libraries.Materials.RegisterAsync("mat-fx-draft", MaterialFixtures.Steel("FX-DRAFT"), MaterialFixtures.Sourced());
        var fields = BeamExample1();

        fields[0] = Pin("MaterialPin", "mat-fx-draft");
        var unreleased = await RunAsync(libraries, BeamDeflectionCalculationDefinition.Id, fields);
        Assert.Equal(CalculationModuleRefusal.RecordNotReleased, unreleased.Refusal);
        Assert.Contains("not Released", unreleased.Reason, StringComparison.Ordinal);

        fields[0] = Pin("MaterialPin", "mat-nobody");
        var unknown = await RunAsync(libraries, BeamDeflectionCalculationDefinition.Id, fields);
        Assert.Equal(CalculationModuleRefusal.RecordNotFound, unknown.Refusal);
        Assert.Contains("mat-nobody", unknown.Reason, StringComparison.Ordinal);

        await Assert.ThrowsAsync<ArgumentException>(() => libraries.Service.FillFromRecordAsync(Module(BeamDeflectionCalculationDefinition.Id), "MaterialPin", "mat-nobody"));
        await Assert.ThrowsAsync<ArgumentException>(() => libraries.Service.FillFromRecordAsync(Module(BeamDeflectionCalculationDefinition.Id), "Span", "mat-fx-draft"));
    }

    // ---- Fasteners ----

    [Fact]
    public async Task AReleasedFastener_FillsTheBoltedJointsGradeAreaAndProofStrength_AndIsCitedOnTheRecord()
    {
        var libraries = Build();
        var bolt = FastenerFixtures.HexBolt();
        bolt = bolt with { Mechanical = bolt.Mechanical with { StressArea = FastenerFixtures.Sourced(new Quantity<Area>(84.3, AreaUnits.SquareMillimetre)) } };
        await libraries.Fasteners.RegisterAsync(FixtureBolt, bolt, FastenerFixtures.SourcedProvenance());
        await libraries.Fasteners.RegisterAsync("fst-fx-draft", FastenerFixtures.HexBolt("FX-BOLT-DRAFT"), FastenerFixtures.SourcedProvenance());
        await ReleaseAsync(libraries.Fasteners, FixtureBolt);

        var offered = await libraries.Service.ListReleasedAsync(ReferenceLibrary.Fasteners);
        var option = Assert.Single(offered);
        Assert.Equal(ReferenceLibrary.Fasteners, option.Library);
        Assert.Equal(FixtureBolt, option.RecordId);
        Assert.Contains("FX-BOLT-1", option.Label, StringComparison.Ordinal);
        Assert.Contains("FX-A", option.Label, StringComparison.Ordinal);

        var fill = await libraries.Service.FillFromRecordAsync(Module(BoltedJointPreloadCalculationDefinition.Id), "FastenerPin", FixtureBolt);
        Assert.Empty(fill.Problems);
        Assert.Contains(fill.Fields, f => f.Name == "FastenerGrade" && f.Text == "ISO 898-1 class FX-A, FX-BOLT-1");
        Assert.Contains(fill.Fields, f => f.Name == "TensileStressArea" && f.Text == "84.3" && f.UnitSymbol == "mm²");
        Assert.Contains(fill.Fields, f => f.Name == "ProofStrength" && f.Text == "500" && f.UnitSymbol == "MPa");

        // Example 1 of the specification on the fixture bolt: the record's
        // 500 MPa proof strength stands, not the 600 MPa typed.
        var outcome = await RunAsync(
            libraries,
            BoltedJointPreloadCalculationDefinition.Id,
            [
                Pin("FastenerPin", FixtureBolt),
                F("FastenerGrade", "typed and overridden"),
                F("Preload", "37935", "N"), F("ExternalLoad", "20", "kN"), F("BoltStiffness", "200", "kN/mm"), F("MemberStiffness", "600", "kN/mm"),
                F("TensileStressArea", "1", "mm²"), F("ProofStrength", "600", "MPa"),
            ]);

        Assert.True(outcome.WasPerformed, outcome.Reason);
        var run = outcome.Run!;
        Assert.Contains(run.Results, r => r.Label == "Bolt stress" && r.Display.StartsWith("509.31", StringComparison.Ordinal));
        Assert.Contains(run.Working, w => w.Label == "Fastener reference" && w.Display.Contains(FixtureBolt, StringComparison.Ordinal));
        Assert.Contains(run.Working, w => w.Label == "Fastener grade" && w.Display.Contains("FX-A", StringComparison.Ordinal));
        Assert.Equal(FixtureBolt, Assert.Single(outcome.Fills).Record.RecordId);
    }

    [Fact]
    public async Task AFastenerWithNoStressArea_CannotDriveTheBoltedJoint_AndTheMissingPropertyIsNamed()
    {
        var libraries = Build();
        await libraries.Fasteners.RegisterAsync(FixtureBolt, FastenerFixtures.HexBolt(), FastenerFixtures.SourcedProvenance());
        await ReleaseAsync(libraries.Fasteners, FixtureBolt);

        var fill = await libraries.Service.FillFromRecordAsync(Module(BoltedJointPreloadCalculationDefinition.Id), "FastenerPin", FixtureBolt);
        Assert.Contains(fill.Fields, f => f.Name == "ProofStrength" && f.Text == "500");
        var missing = Assert.Single(fill.Problems);
        Assert.Equal("TensileStressArea", missing.InputName);

        var outcome = await RunAsync(
            libraries,
            BoltedJointPreloadCalculationDefinition.Id,
            [Pin("FastenerPin", FixtureBolt), F("Preload", "37935", "N"), F("ExternalLoad", "20", "kN"), F("BoltStiffness", "200", "kN/mm"), F("MemberStiffness", "600", "kN/mm")]);

        Assert.Equal(CalculationModuleRefusal.RecordIncomplete, outcome.Refusal);
        Assert.Contains("Tensile stress area", outcome.Reason, StringComparison.Ordinal);
        Assert.Null(outcome.Run);

        // The bolt group needs only the grade from a fastener, so the same
        // record drives it, cited in the working.
        var group = await RunAsync(
            libraries,
            BoltGroupEccentricShearCalculationDefinition.Id,
            [
                Pin("FastenerPin", FixtureBolt),
                new("Bolts", Rows: ["75 mm, 50 mm", "75 mm, -50 mm", "-75 mm, 50 mm", "-75 mm, -50 mm"]),
                F("LoadX", "0", "kN"), F("LoadY", "-20", "kN"), F("LoadPointX", "250", "mm"), F("LoadPointY", "0", "mm"), F("AllowableShearPerBolt", "25", "kN"),
            ]);

        Assert.True(group.WasPerformed, group.Reason);
        Assert.Contains(group.Run!.Working, w => w.Label == "Fastener reference" && w.Display.Contains(FixtureBolt, StringComparison.Ordinal));
        Assert.Contains(group.Run.Results, r => r.Label == "Governing bolt force" && r.Display == "18239.9 N");
    }

    // ---- Bearings ----

    [Fact]
    public async Task AReleasedBearing_FillsTheDesignationTypeAndRating_AndIsCitedOnTheRecord()
    {
        var libraries = Build();
        await libraries.Bearings.RegisterAsync(FixtureBearing, BearingFixtures.DeepGrooveBall());
        await libraries.Bearings.RegisterAsync("brg-fx-draft", BearingFixtures.DeepGrooveBall("FX-6001"));
        await ReleaseAsync(libraries.Bearings, FixtureBearing);

        var offered = await libraries.Service.ListReleasedAsync(ReferenceLibrary.Bearings);
        var option = Assert.Single(offered);
        Assert.Equal(ReferenceLibrary.Bearings, option.Library);
        Assert.Equal(FixtureBearing, option.RecordId);
        Assert.Contains("FX-6000", option.Label, StringComparison.Ordinal);

        var fill = await libraries.Service.FillFromRecordAsync(Module(BearingRatingLifeCalculationDefinition.Id), "BearingPin", FixtureBearing);
        Assert.Empty(fill.Problems);
        Assert.Contains(fill.Fields, f => f.Name == "BearingDesignation" && f.Text == "FX-6000");
        Assert.Contains(fill.Fields, f => f.Name == "BearingType" && f.Choice == nameof(RollingBearingType.Ball));
        Assert.Contains(fill.Fields, f => f.Name == "BasicDynamicLoadRating" && f.Text == "4.6" && f.UnitSymbol == "kN");

        // C = 4.6 kN, P = 1 kN radial: the load ratio is 1/4.6 = 0.217391
        // and L10 = 4.6^3 = 97.336 million revolutions.
        var outcome = await RunAsync(
            libraries,
            BearingRatingLifeCalculationDefinition.Id,
            [
                Pin("BearingPin", FixtureBearing),
                new("BearingType", Choice: nameof(RollingBearingType.Roller)),
                F("BasicDynamicLoadRating", "99", "kN"),
                F("RadialLoad", "1", "kN"), F("AxialLoad", "0", "kN"), F("RadialFactor", "1"), F("AxialFactor", "0"),
                F("Speed", "1500", "r/min"), F("ReliabilityFactor", "1"), F("RequiredLife", "1000", "h"),
            ]);

        Assert.True(outcome.WasPerformed, outcome.Reason);
        var run = outcome.Run!;
        Assert.Equal("Meets its criteria", run.OutcomeSummary);
        Assert.Contains(run.Results, r => r.Label == "Load ratio" && r.Display == "0.217391");
        Assert.Contains(run.Results, r => r.Label == "Basic rating life million revolutions" && r.Display.StartsWith("97.336", StringComparison.Ordinal));
        Assert.Contains(run.Working, w => w.Label == "Bearing reference" && w.Display.Contains(FixtureBearing, StringComparison.Ordinal));
        Assert.Contains(run.Working, w => w.Label == "Bearing designation" && w.Display == "FX-6000");
    }

    [Fact]
    public async Task ABearingWithNoDynamicRating_CannotDriveTheLifeCalculation()
    {
        var libraries = Build();
        await libraries.Bearings.RegisterAsync(FixtureBearing, BearingFixtures.PlainBush());
        await ReleaseAsync(libraries.Bearings, FixtureBearing);

        var fill = await libraries.Service.FillFromRecordAsync(Module(BearingRatingLifeCalculationDefinition.Id), "BearingPin", FixtureBearing);
        Assert.Contains(fill.Fields, f => f.Name == "BearingDesignation" && f.Text == "FX-PB-1012");
        Assert.Contains(fill.Problems, p => p.InputName == "BasicDynamicLoadRating");
        Assert.Contains(fill.Problems, p => p.InputName == "BearingType");

        var outcome = await RunAsync(
            libraries,
            BearingRatingLifeCalculationDefinition.Id,
            [Pin("BearingPin", FixtureBearing), F("RadialLoad", "1", "kN"), F("AxialLoad", "0", "kN"), F("RadialFactor", "1"), F("AxialFactor", "0"), F("Speed", "1500", "r/min"), F("ReliabilityFactor", "1"), F("RequiredLife", "1000", "h")]);

        Assert.Equal(CalculationModuleRefusal.RecordIncomplete, outcome.Refusal);
        Assert.Contains("Dynamic load rating C", outcome.Reason, StringComparison.Ordinal);
    }

    // ---- Executing and presenting ----

    [Fact]
    public async Task PrepareBuildsWithoutRunning_ExecuteTakesOnlyTheModulesOwnInput_AndPresentReadsARecordBack()
    {
        var libraries = Build();
        var module = Module(BoltShearCapacityCalculationDefinition.Id);
        var fields = new[] { F("Diameter", "20", "mm"), F("UltimateShearStrength", "400", "MPa"), F("ShearPlanes", "2"), F("SafetyFactor", "1.5") };

        var prepared = await libraries.Service.PrepareAsync(new CalculationModuleRequest(module.Id, fields));
        Assert.True(prepared.Succeeded, prepared.Reason);
        var input = Assert.IsType<BoltShearCapacityInput>(prepared.Input);
        Assert.Equal(2, input.ShearPlanes);

        var run = await libraries.Service.ExecuteAsync(module, input);
        Assert.Contains(run.Results, r => r.Label == "Allowable shear capacity" && r.Display == "167552 N");

        var presented = await libraries.Service.PresentAsync(module, run.RecordId);
        Assert.NotNull(presented);
        Assert.Equal(run.RecordId, presented!.RecordId);
        Assert.Equal(run.Results, presented.Results);
        Assert.Null(await libraries.Service.PresentAsync(module, Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => libraries.Service.ExecuteAsync(Module(BeamDeflectionCalculationDefinition.Id), input));

        // A record read back from the store presents its working exactly
        // as the live run did: quantities with units, numbers to six
        // figures — never the JSON the store holds them as.
        Assert.Equal(run.Working, presented.Working);
        Assert.Equal(run.Checks, presented.Checks);
    }

    [Fact]
    public async Task ARecordReadBack_PresentsItsWorkingAsValues_NotAsTheStoredJson()
    {
        var libraries = await WithSeededSteelAsync();
        var module = Module(BeamDeflectionCalculationDefinition.Id);

        var live = (await RunAsync(libraries, BeamDeflectionCalculationDefinition.Id, BeamExample1())).Run!;
        var readBack = (await libraries.Service.PresentAsync(module, live.RecordId))!;

        Assert.Contains(readBack.Working, w => w.Label == "Maximum deflection" && w.Display == "3.96825 mm");
        Assert.Contains(readBack.Working, w => w.Label == "Span-to-depth ratio" && w.Display == "20");
        Assert.Contains(readBack.Working, w => w.Label == "Material reference" && w.Display.StartsWith("Materials/mat-s355j2@", StringComparison.Ordinal));
        Assert.Contains(readBack.Working, w => w.Label == "Case" && w.Display == "SimplySupported, PointLoad");
        Assert.DoesNotContain(readBack.Working, w => w.Display.Contains("{", StringComparison.Ordinal));
        Assert.Equal(live.Working, readBack.Working);
    }

    [Fact]
    public void LabelsAndValues_ReadAsAnEngineerWouldWriteThem()
    {
        Assert.Equal("Maximum bending stress", CalculationModuleForm.Humanise("MaximumBendingStress"));
        Assert.Equal("Load ratio", CalculationModuleForm.Humanise("LoadRatio"));
        Assert.Equal("125 MPa", CalculationModuleForm.Format(new Quantity<Pressure>(125, PressureUnits.Megapascal)));
        Assert.Equal("3.96825 mm", CalculationModuleForm.Format(new Quantity<Length>(3.968253968, LengthUnits.Millimetre)));
        Assert.Equal("—", CalculationModuleForm.Format(null));
        Assert.Equal("Yes", CalculationModuleForm.Format(true));
        Assert.Equal("Outside method limits", CalculationModuleForm.Format(EngineeringCheckOutcome.OutsideMethodLimits));
        Assert.Equal("1 N; 2 N", CalculationModuleForm.Format(new[] { new Quantity<Force>(1, ForceUnits.Newton), new Quantity<Force>(2, ForceUnits.Newton) }));
        Assert.Equal("Materials/mat-s355j2@1", CalculationModuleForm.Format(SteelPin));
    }

    private static async Task ReleaseAsync<TDefinition>(IReferenceDataCatalog<TDefinition> catalog, string recordId)
        where TDefinition : class
    {
        var principals = new CurrentPrincipalAccessor();
        principals.SetCurrent(new PlatformPrincipal(new PlatformIdentity(ReviewerId, ReviewerId), []));
        var review = new ReferenceReviewService(principals);
        await review.VerifyAsync(catalog, recordId, new ReferenceReviewStatement("Fixture handbook, Table 3"));
        await review.ReleaseAsync(catalog, recordId, "Required for a WP 21.7C service test.");
    }
}
