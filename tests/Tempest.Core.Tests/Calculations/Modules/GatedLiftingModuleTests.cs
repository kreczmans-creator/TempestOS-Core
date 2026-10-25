using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// `ADR-0173`, review M8: the older lifting-lug and pin-joint module (<c>calc.lifting-lug-pin-joint</c>) is a lifting calculation like S8 and is
/// withheld with it. The Engineering Calculators service that refused it (<c>ModuleSwitchedOff</c>) was retired in v0.27.0 (ADR-0167 Amendment 1), so
/// these checks, which ran against that service, now run against the rule it applied and the engine's choke point (the execution tests are in
/// <see cref="GatedEngineExecutionTests"/>): every other module is unaffected.
/// </summary>
public class GatedLiftingModuleTests
{
    private static readonly string LugId = LiftingLugPinJointCalculationDefinition.Id;

    [Fact]
    public void Only_The_Lifting_Module_Names_A_Feature()
    {
        Assert.Equal(CalculationModuleDescriptors.LiftingFeature, CalculationModuleDescriptors.FeatureOf(LugId));
        Assert.Equal("lifting", CalculationModuleDescriptors.LiftingFeature);
        Assert.All(CalculationModuleDescriptors.All.Where(m => m.Id != LugId), m => Assert.Null(CalculationModuleDescriptors.FeatureOf(m.Id)));
    }

    [Fact]
    public void With_The_Feature_Off_The_Lifting_Module_Is_Refused_And_The_Reason_Says_Why()
    {
        var reason = CalculationFeatureGate.RefusalFor(LugId, feature => false);

        Assert.NotNull(reason);
        Assert.Contains("switched off", reason, StringComparison.Ordinal);
        Assert.Contains("lifting", reason, StringComparison.Ordinal);

        // Another module is unaffected, off or never stated.
        Assert.All(CalculationModuleDescriptors.All.Where(m => m.Id != LugId), m => Assert.Null(CalculationFeatureGate.RefusalFor(m.Id, feature => false)));
        Assert.All(CalculationModuleDescriptors.All.Where(m => m.Id != LugId), m => Assert.Null(CalculationFeatureGate.RefusalFor(m.Id, null)));
    }

    [Fact]
    public void With_The_Feature_On_The_Lifting_Module_Is_Available_As_Before()
    {
        Assert.Null(CalculationFeatureGate.RefusalFor(LugId, feature => feature == "lifting"));
    }

    [Fact]
    public void With_No_Gate_Stated_The_Lifting_Module_Is_Refused_Because_The_Default_Is_Deny()
    {
        // The retired service treated "no gate composed" as nothing gated (a tool or a test); the engine's rule is stricter, and is the one that remains.
        Assert.NotNull(CalculationFeatureGate.RefusalFor(LugId, null));
    }

    [Fact]
    public void A_Feature_That_Is_Not_The_Lifting_One_Does_Not_Open_The_Gate()
    {
        Assert.NotNull(CalculationFeatureGate.RefusalFor(LugId, feature => feature != "lifting"));
    }

    [Fact]
    public void The_Lifting_Lug_Worksheet_Of_The_Port_Is_Gated_Like_Its_Legacy_Module_And_S8()
    {
        Assert.Equal("lifting", CalculationFeatureGate.FeatureOf("mech.lifting-lug"));
        Assert.Contains("mech.lifting-lug", CalculationFeatureGate.GatedIds);
        Assert.Contains("struct.lifting", CalculationFeatureGate.GatedIds);
        Assert.Contains(LugId, CalculationFeatureGate.GatedIds);
        Assert.NotNull(CalculationFeatureGate.RefusalFor("mech.lifting-lug", _ => false));
        Assert.Null(CalculationFeatureGate.RefusalFor("mech.lifting-lug", feature => feature == "lifting"));
    }
}
