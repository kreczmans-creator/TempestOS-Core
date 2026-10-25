using System.Text.Json;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.EngineeringDomain;
using Tempest.Core.Tests.Persistence;
using Tempest.Workspace.Calculations;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// `ADR-0173` (S8 review, residual bypass): the lifting calculations are refused at the engine's one execution choke point while the lifting feature
/// is off, so no caller that runs a definition by id (the generic <c>calculations.execute</c> command, a plugin, a script, a re-run) can reach them.
/// With the feature on they run as before; every other calculation is unaffected either way.
/// </summary>
public class GatedEngineExecutionTests
{
    private static readonly string LiftingId = LiftingLugPinJointCalculationDefinition.Id;

    private static LiftingLugPinJointInput Input()
    {
        var v = ((IEnumerable<object[]>)LiftingLugPinJointVectors.WorkedExamples).Select(r => (LiftingLugPinJointVectors.Vector)r[0]).First();
        return new(
            SteelPin, PinSteelPin, v.Load, v.LugThickness, v.LugWidth, v.HoleDiameter, v.PinDiameter, v.EdgeDistance, v.CheekPlateThickness, v.Clearance,
            v.AllowableTensileStress, v.AllowableBearingStress, v.AllowableShearStress, v.PinAllowableBendingStress, v.PinAllowableShearStress);
    }

    private static CalculationEngine Engine(IEngineeringDocumentStore? store = null, Func<string, bool>? features = null)
    {
        var principals = new CurrentPrincipalAccessor();
        var engine = new CalculationEngine(store ?? new EngineeringDocumentStore(new InMemoryQueryablePersistenceStore(), principals), principals);
        ProductCalculationCatalogue.RegisterAll(engine);

        if (features is not null)
            engine.ConfigureFeatures(features);

        return engine;
    }

    [Fact]
    public async Task With_The_Lifting_Feature_Off_The_Engine_Refuses_The_Legacy_Definition_With_The_Catalogue_Wording()
    {
        var engine = Engine(features: _ => false);

        var refused = await Assert.ThrowsAsync<CalculationFeatureDisabledException>(() => engine.ExecuteAsync<LiftingLugPinJointInput, LiftingLugPinJointResult>(LiftingId, Input()));

        Assert.Equal(LiftingId, refused.CalculationId);
        Assert.Equal(CalculationFeatureGate.Refusal("Lifting lug and pin joint", LiftingId, "lifting"), refused.Message);
        Assert.Contains("switched off", refused.Message, StringComparison.Ordinal);
        Assert.Contains("HKLM\\SOFTWARE\\Policies\\TempestOS\\Features", refused.Message, StringComparison.Ordinal);
        Assert.Empty(await engine.ListRecordsAsync());
    }

    [Fact]
    public async Task An_Engine_That_Was_Never_Told_The_Features_Treats_Them_As_Off()
    {
        var engine = Engine();

        await Assert.ThrowsAsync<CalculationFeatureDisabledException>(() => engine.ExecuteAsync<LiftingLugPinJointInput, LiftingLugPinJointResult>(LiftingId, Input()));
    }

    [Fact]
    public async Task With_The_Lifting_Feature_On_The_Legacy_Definition_Runs()
    {
        var engine = Engine(features: feature => feature == CalculationFeatureGate.LiftingFeature);

        var record = await engine.ExecuteAsync<LiftingLugPinJointInput, LiftingLugPinJointResult>(LiftingId, Input());

        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.Equal(LiftingId, record.CalculationId);
    }

    [Fact]
    public async Task A_Feature_That_Is_Not_The_Lifting_One_Does_Not_Open_The_Gate()
    {
        var engine = Engine(features: feature => feature != CalculationFeatureGate.LiftingFeature);

        await Assert.ThrowsAsync<CalculationFeatureDisabledException>(() => engine.ExecuteAsync<LiftingLugPinJointInput, LiftingLugPinJointResult>(LiftingId, Input()));
    }

    [Fact]
    public async Task A_Record_Made_While_The_Feature_Was_On_Cannot_Be_Re_Run_While_It_Is_Off()
    {
        var principals = new CurrentPrincipalAccessor();
        var store = new EngineeringDocumentStore(new InMemoryQueryablePersistenceStore(), principals);
        var on = new CalculationEngine(store, principals);
        ProductCalculationCatalogue.RegisterAll(on);
        on.ConfigureFeatures(_ => true);
        var record = await on.ExecuteAsync<LiftingLugPinJointInput, LiftingLugPinJointResult>(LiftingId, Input());

        var off = new CalculationEngine(store, principals);
        ProductCalculationCatalogue.RegisterAll(off);
        off.ConfigureFeatures(_ => false);

        await Assert.ThrowsAsync<CalculationFeatureDisabledException>(() => off.ReRunAsync<LiftingLugPinJointInput, LiftingLugPinJointResult>(record.Id));
        await Assert.ThrowsAsync<CalculationFeatureDisabledException>(() => off.ReRunAsync<LiftingLugPinJointInput, LiftingLugPinJointResult>(record.Id, Input()));

        // Reading it back is not running it.
        Assert.NotNull(await off.FindRecordAsync<LiftingLugPinJointResult>(record.Id));
    }

    [Fact]
    public async Task The_Worksheet_Lifting_Id_Is_Gated_Too_And_Reads_Like_The_Catalogue()
    {
        var engine = Engine(features: _ => false);

        var refused = await Assert.ThrowsAsync<CalculationFeatureDisabledException>(() => engine.ExecuteAsync<int, int>(CalculationFeatureGate.LiftingWorksheetId, 1));

        Assert.Equal(CalculationFeatureGate.RefusalFor(CalculationFeatureGate.LiftingWorksheetId, null), refused.Message);
        Assert.Contains("Lifting lugs and spreader beams", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_The_Lifting_Calculations_Are_Gated()
    {
        // Every other product calculation is unaffected, with the features off or never stated.
        Assert.All(ProductCalculationCatalogue.CalculationIds.Where(id => id != LiftingId), id => Assert.Null(CalculationFeatureGate.RefusalFor(id, null)));
        Assert.Equal(["calc.lifting-lug-pin-joint", "struct.lifting"], new[] { LiftingId, CalculationFeatureGate.LiftingWorksheetId }.Where(id => CalculationFeatureGate.FeatureOf(id) is not null).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_Features_Are_Fixed_Once_Stated()
    {
        var engine = Engine(features: _ => false);

        Assert.Throws<InvalidOperationException>(() => engine.ConfigureFeatures(_ => true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_Generic_Execute_Calculation_Command_Is_Refused_With_The_Feature_Off_And_Runs_With_It_On(bool featureOn)
    {
        var context = TestEngineeringDomain.NewContext();
        var factory = new EngineeringObjectFactory<Calculation>(
            "Calculation", context, (doc, rev) => new Calculation(doc, rev, context, "CALC-1", "Lug", EngineeringObjectMetadata.Empty));
        var target = (Calculation)await factory.CreateAsync("Lug — for test purposes.");

        var engine = new CalculationEngine(context.Store, context.CurrentPrincipalAccessor);
        ProductCalculationCatalogue.RegisterAll(engine);
        engine.ConfigureFeatures(feature => featureOn && feature == CalculationFeatureGate.LiftingFeature);
        var templates = new CalculationTemplateRegistry(engine, context);
        templates.Register<LiftingLugPinJointInput, LiftingLugPinJointResult>(LiftingId, new LiftingLugPinJointCalculationDefinition().Metadata);
        var handler = new ExecuteCalculationCommandHandler(templates);

        var result = await handler.HandleAsync(new ExecuteCalculationCommand(target.Id, "Calculation", LiftingId, JsonSerializer.Serialize(Input())), default);

        if (featureOn)
        {
            Assert.True(result.Succeeded, result.Message);
        }
        else
        {
            Assert.False(result.Succeeded);
            Assert.Equal(CalculationFeatureGate.Refusal("Lifting lug and pin joint", LiftingId, "lifting"), result.Message);
            Assert.Empty(await engine.ListRecordsAsync());
        }
    }
}
