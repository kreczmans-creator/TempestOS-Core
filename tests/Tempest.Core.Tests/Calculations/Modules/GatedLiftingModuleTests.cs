using Tempest.Core.Calculations.Modules;
using Tempest.Core.Tests.Bearings;
using Tempest.Core.Tests.Fasteners;
using Tempest.Core.Tests.Materials;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// `ADR-0173`, review M8: the older lifting-lug and pin-joint module (`calc.lifting-lug-pin-joint`) is a lifting calculation like S8 and is
/// withheld with it: the Engineering Calculators surface does not offer it and the service refuses it while the installation's lifting feature
/// is off. Every other module is unaffected.
/// </summary>
public class GatedLiftingModuleTests
{
    private static CalculationModuleService Build(Func<string, bool>? featureEnabled) =>
        new(MaterialFixtures.BuildCatalog(), FastenerFixtures.BuildCatalog(), BearingFixtures.BuildCatalog(), BareEngine(), featureEnabled);

    private static readonly CalculationModuleRequest LugRequest = new(LiftingLugPinJointCalculationDefinition.Id, []);

    [Fact]
    public void Only_The_Lifting_Module_Names_A_Feature()
    {
        Assert.Equal(CalculationModuleDescriptors.LiftingFeature, CalculationModuleDescriptors.FeatureOf(LiftingLugPinJointCalculationDefinition.Id));
        Assert.Equal("lifting", CalculationModuleDescriptors.LiftingFeature);
        Assert.All(CalculationModuleDescriptors.All.Where(m => m.Id != LiftingLugPinJointCalculationDefinition.Id), m => Assert.Null(CalculationModuleDescriptors.FeatureOf(m.Id)));
    }

    [Fact]
    public async Task With_The_Feature_Off_The_Service_Refuses_The_Lifting_Module_And_Says_Why()
    {
        var service = Build(feature => false);
        var lug = CalculationModuleDescriptors.For(LiftingLugPinJointCalculationDefinition.Id)!;
        Assert.False(service.IsAvailable(lug));

        var prepared = await service.PrepareAsync(LugRequest);
        Assert.Equal(CalculationModuleRefusal.ModuleSwitchedOff, prepared.Refusal);
        Assert.Contains("switched off", prepared.Reason, StringComparison.Ordinal);
        Assert.Contains("lifting", prepared.Reason, StringComparison.Ordinal);

        var run = await service.RunAsync(LugRequest);
        Assert.Equal(CalculationModuleRefusal.ModuleSwitchedOff, run.Refusal);
        Assert.False(run.WasPerformed);

        // Another module is unaffected.
        Assert.All(CalculationModuleDescriptors.All.Where(m => m.Id != lug.Id), m => Assert.True(service.IsAvailable(m)));
        Assert.NotEqual(CalculationModuleRefusal.ModuleSwitchedOff, (await service.RunAsync(new CalculationModuleRequest("calc.no-such-module", []))).Refusal);
    }

    [Fact]
    public async Task With_The_Feature_On_Or_No_Gate_Composed_The_Lifting_Module_Is_Available_As_Before()
    {
        foreach (var gate in new Func<string, bool>?[] { feature => feature == "lifting", null })
        {
            var service = Build(gate);
            Assert.True(service.IsAvailable(CalculationModuleDescriptors.For(LiftingLugPinJointCalculationDefinition.Id)!));
            Assert.NotEqual(CalculationModuleRefusal.ModuleSwitchedOff, (await service.PrepareAsync(LugRequest)).Refusal);
        }
    }

    [Fact]
    public async Task A_Feature_That_Is_Not_The_Lifting_One_Does_Not_Open_The_Gate()
    {
        var service = Build(feature => feature != "lifting");
        Assert.Equal(CalculationModuleRefusal.ModuleSwitchedOff, (await service.PrepareAsync(LugRequest)).Refusal);
    }
}
