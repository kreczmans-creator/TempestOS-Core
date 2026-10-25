using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

public sealed partial class MechanismCalculator
{
    public const string CamMode = "Cam";

    private static IReadOnlyList<string> CamModes => [];

    private static void AddCamInputs(List<CalcInputSpec> inputs)
    {
    }

    private static void AddCamRules(List<CalcApplicabilityRule> rules)
    {
    }

    private static void AddCamCases(List<CalcTestCase> cases)
    {
    }

    private static void RunCam(CalcRunContext c)
    {
        c.Block("missing", "mode: the cam is not available yet.");
    }
}
