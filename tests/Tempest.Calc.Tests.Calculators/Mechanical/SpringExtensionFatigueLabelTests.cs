using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// M7 1.3.1, mech.spring-R1: in extension mode the Goodman check works on the body stresses only, but the row read "Goodman fatigue against the required safety factor", so a pass read as the whole spring.
/// Hook bending usually governs the fatigue of an extension spring (ka-extension: body shear 358.3 MPa at working point 2, hook bending 693.0 MPa at the same force). The row now says "body only" and the
/// sheet warns that hook fatigue is not assessed; compression and torsion are unchanged.
/// </summary>
public sealed class SpringExtensionFatigueLabelTests
{
    private static readonly SpringCalculator Spring = new();

    private static CalcRunResult Run(string caseId, params CalcInputValue[] more) =>
        CalcRunner.Run(Spring, more.Aggregate(new CalcInputSet([.. Spring.Template.TestCases.Single(t => t.Id == caseId).Inputs]).With(CalcInputValue.Quantity("S_e", 300, "MPa")), (set, v) => set.With(v)));

    [Fact]
    public void The_Extension_Fatigue_Row_Says_Body_Only()
    {
        var r = Run("ka-extension");

        Assert.Contains(r.Checks, c => c.Name.StartsWith("Goodman fatigue of the body only", StringComparison.Ordinal));
        Assert.DoesNotContain(r.Checks, c => c.Name == "Goodman fatigue against the required safety factor");
    }

    [Fact]
    public void The_Extension_Sheet_Warns_That_Hook_Fatigue_Is_Not_Assessed()
    {
        var r = Run("ka-extension");

        var warning = Assert.Single(r.Warnings, w => w.RuleId == "hook-fatigue");
        Assert.Contains("hook", warning.Message, StringComparison.Ordinal);
        Assert.Contains("not assessed", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compression_Keeps_Its_Label_And_Raises_No_Hook_Warning()
    {
        var r = Run("ka-compression");

        Assert.Contains(r.Checks, c => c.Name == "Goodman fatigue against the required safety factor");
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "hook-fatigue");
    }
}
