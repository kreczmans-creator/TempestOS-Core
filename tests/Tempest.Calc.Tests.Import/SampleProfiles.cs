using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;

namespace Tempest.Calc.Tests.Import;

/// <summary>Scan profiles for the golden sample tables under <c>Samples/scan</c>. Each one states its own columns, as every real profile must.</summary>
internal static class SampleProfiles
{
    /// <summary><c>zeiss-groove.csv</c>: one row per feature per part, deviations from nominal, comma separated.</summary>
    public static ScanProfile Zeiss(params ScanFeatureEntry[] features) => new(
        "zeiss-groove", 1, "ZEISS Inspect groove table",
        new ScanColumns("Element", Nominal: "Nominal", Actual: "Actual", Deviation: "Deviation", UpperTolerance: "Upper tol.", LowerTolerance: "Lower tol.", Serial: "Serial"),
        features, ScanSoftware.ZeissInspect);

    /// <summary><c>gom-two-stage.csv</c>: semicolons, decimal commas, a stage column and a part column.</summary>
    public static ScanProfile GomTwoStage(params ScanFeatureEntry[] features) => new(
        "gom-two-stage", 2, "GOM Inspect two-stage bore table",
        new ScanColumns("Feature", Nominal: "Nominal", Actual: "Value", Deviation: "Dev", UpperTolerance: "Tol+", LowerTolerance: "Tol-", Serial: "Part", Stage: "Stage"),
        features, ScanSoftware.GomInspect, Delimiter: ScanDelimiter.Semicolon, Number: NumberProfile.Comma);

    /// <summary><c>polyworks-report.txt</c>: whitespace separated, two lines of preamble, absolute limits, columns by position.</summary>
    public static ScanProfile PolyWorks(params ScanFeatureEntry[] features) => new(
        "polyworks-pin", 1, "PolyWorks pin report",
        new ScanColumns("#1", Nominal: "#2", Actual: "#3", Deviation: "#4", UpperTolerance: "#5", LowerTolerance: "#6", Serial: "#7"),
        features, ScanSoftware.PolyWorks, Delimiter: ScanDelimiter.Whitespace, HeaderRow: 3, ToleranceMode: ScanToleranceMode.AbsoluteLimits);

    /// <summary>A small table for the problem cases: feature, nominal, actual, deviation, tolerances, serial.</summary>
    public static ScanProfile Plain(params ScanFeatureEntry[] features) => new(
        "plain", 1, "Plain table",
        new ScanColumns("Feature", Nominal: "Nominal", Actual: "Actual", Deviation: "Dev", UpperTolerance: "Up", LowerTolerance: "Lo", Serial: "Serial"),
        features);

    public static ScanFileInput Input(string text, string name = "t.csv", string? stage = null, string? serial = null) =>
        new(Samples.Text(text, name), stage, serial);
}
