using Tempest.Calc.Sheets;

namespace Tempest.Calc.Import.Scan;

/// <summary>Which scan or inspection software a table came from. It labels provenance and picks the header spellings offered for a new profile; it never decides how a file is read — the profile does.</summary>
public enum ScanSoftware
{
    Generic,
    ZeissInspect,
    GomInspect,
    PolyWorks,
    GeomagicControlX,
    MultiStageCsv,
}

/// <summary>How the tolerance columns of a table are meant: as deviations from the nominal (+0.10 / −0.10, the usual inspection-report form) or as absolute limits (USL / LSL).</summary>
public enum ScanToleranceMode
{
    DeviationsFromNominal,
    AbsoluteLimits,
}

/// <summary>The field separator of a table. <see cref="Auto"/> looks at the first line (comma, semicolon or tab); <see cref="Whitespace"/> splits a text report on runs of spaces and tabs.</summary>
public enum ScanDelimiter
{
    Auto,
    Comma,
    Semicolon,
    Tab,
    Whitespace,
}

/// <summary>What a statistic of a feature is pushed into when the target is a single input.</summary>
public enum ScanStat
{
    Mean,
    Sigma,
    Nominal,
    Min,
    Max,
}

/// <summary>Why a cell or a row was reported rather than used.</summary>
public enum ScanProblemKind
{
    /// <summary>A mapped cell was empty. Reported; the row is used if the rest of it carries a value.</summary>
    Blank,

    /// <summary>A cell that should be a number is not one (including NaN, Infinity and a number in the other decimal notation). The cell is treated as missing and reported.</summary>
    NotANumber,

    /// <summary>A number beyond any plausible scan value.</summary>
    OutOfRange,

    /// <summary>Actual, nominal and deviation of one row disagree beyond rounding; the actual value is used.</summary>
    Inconsistent,

    /// <summary>The tolerance of a row is unusable (reversed limits); the row is used without it.</summary>
    Tolerance,

    /// <summary>A row without a feature name; it is not used.</summary>
    NoFeature,

    /// <summary>A row without any value (neither actual, nor nominal with deviation); it is not used.</summary>
    NoValue,

    /// <summary>Text from the file held markup, a link or an invisible character and was reduced to plain text.</summary>
    Text,

    /// <summary>A row shorter than the columns the profile maps.</summary>
    ShortRow,

    /// <summary>A number was read with the profile's grouping mark removed (<c>1.500</c> as 1500 under a decimal-comma profile that groups with a point).</summary>
    Grouping,
}

/// <summary>
/// Which column of the file supplies which quantity. A column is named by its header text (matched as the profile says) or by
/// position as <c>#3</c> (the third column, counting from 1). Only <see cref="Feature"/> and one of <see cref="Actual"/> or
/// <see cref="Deviation"/> are required.
/// </summary>
public sealed record ScanColumns(
    string Feature,
    string? Nominal = null,
    string? Actual = null,
    string? Deviation = null,
    string? UpperTolerance = null,
    string? LowerTolerance = null,
    string? Serial = null,
    string? Stage = null)
{
    /// <summary>The (role, reference) pairs that are set, in a fixed order.</summary>
    public IEnumerable<(string Role, string Reference)> Set()
    {
        yield return ("feature", Feature);

        if (Nominal is not null)
            yield return ("nominal", Nominal);

        if (Actual is not null)
            yield return ("actual", Actual);

        if (Deviation is not null)
            yield return ("deviation", Deviation);

        if (UpperTolerance is not null)
            yield return ("upper tolerance", UpperTolerance);

        if (LowerTolerance is not null)
            yield return ("lower tolerance", LowerTolerance);

        if (Serial is not null)
            yield return ("serial", Serial);

        if (Stage is not null)
            yield return ("stage", Stage);
    }
}

/// <summary>One file of a scan import with the facts only the person knows: the stage and the part serial when the file holds one stage or one part and has no column for it.</summary>
public sealed record ScanFileInput(ImportedFile File, string? Stage = null, string? Serial = null);

/// <summary>What the person tells the importer about the measurement; each is optional and goes into provenance.</summary>
public sealed record ScanSettings
{
    /// <summary>The scan limits: 5 MB, 1e6 rows, with a longer time budget than W1 because a table is large.</summary>
    public ImportLimits Limits { get; init; } = ImportLimits.Scan with { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>The most bytes all the files of one import may hold together (each file is also held to 5 MB).</summary>
    public long MaxTotalBytes { get; init; } = 20L * 1024 * 1024;

    /// <summary>The software version (ZEISS Inspect 2024, PolyWorks 2023 IR8 …), as entered; goes into provenance.</summary>
    public string? SoftwareVersion { get; init; }

    /// <summary>The alignment the deviations refer to (RPS, best-fit, datum A-B-C …), as entered by the person; goes into provenance.</summary>
    public string? Alignment { get; init; }

    /// <summary>Consecutive observations per subgroup for the within-subgroup σ (Cp, Cpk); 1 means no subgrouping, and then Cp = Pp and Cpk = Ppk.</summary>
    public int SubgroupSize { get; init; } = 1;

    /// <summary>Fewer parts than this and the capability figures carry a warning (the default is the customary 30).</summary>
    public int MinParts { get; init; } = 30;

    /// <summary>Leave the observations flagged as outliers out of the statistics. Default false: they are reported and kept.</summary>
    public bool ExcludeOutliers { get; init; }

    /// <summary>The significance level the normality verdict is read at.</summary>
    public double Alpha { get; init; } = 0.05;

    /// <summary>Overrides the profile's number format (for a file whose decimal mark the profile did not declare).</summary>
    public NumberProfile? Number { get; init; }
}

/// <summary>One file of an import as it was read.</summary>
public sealed record ScanFileInfo(string FileName, string Sha256, string? Stage, string? Serial, int Rows, int Observations);

/// <summary>One usable row: a feature of a part at a stage with its measured value, nominal and limits (in the profile's unit).</summary>
public readonly record struct ScanObservation(int File, int Line, string Feature, string Stage, string Serial, double? Nominal, double Value, double? Lsl, double? Usl, bool DeviationOnly);

/// <summary>A kind of reported problem in one column, with how often it happened and the first few places.</summary>
public sealed record ScanProblemGroup(ScanProblemKind Kind, string Column, int Count, IReadOnlyList<string> Examples);

/// <summary>Everything read from the files: the observations, what was reported, and the notices.</summary>
public sealed record ScanData(
    IReadOnlyList<ScanFileInfo> Files,
    IReadOnlyList<ScanObservation> Observations,
    IReadOnlyList<ScanProblemGroup> Problems,
    IReadOnlyList<CalcNotice> Notices,
    int RowsRead)
{
    public int ProblemCount => Problems.Sum(p => p.Count);
}

/// <summary>An observation flagged by the outlier rule. Flagged values are reported; they are left out of the statistics only when the person asks.</summary>
public sealed record ScanOutlier(int File, int Line, string Serial, double Value, string Rule, bool Far);

/// <summary>The verdict of the normality test.</summary>
public enum NormalityVerdict
{
    /// <summary>Too few parts, or no spread: the test is not run (said in the note).</summary>
    NotAssessed,

    /// <summary>The data do not contradict a normal distribution at the chosen level.</summary>
    Consistent,

    /// <summary>A normal distribution is rejected at the chosen level.</summary>
    Rejected,
}

/// <summary>The Anderson–Darling result: the statistic A², the modified A*², the approximate p-value and the verdict.</summary>
public sealed record ScanNormality(NormalityVerdict Verdict, double? A2, double? AStar2, double? PValue, string Note);

/// <summary>The statistics of one feature at one stage over N parts. Values are in the profile's unit.</summary>
public sealed record ScanFeatureStats(
    string Feature,
    string Stage,
    int Count,
    int Total,
    int Parts,
    bool Computable,
    double? Nominal,
    double? Lsl,
    double? Usl,
    double Mean,
    double Sigma,
    double SigmaWithin,
    double Min,
    double Max,
    double? Cp,
    double? Cpk,
    double? Pp,
    double? Ppk,
    ScanNormality Normality,
    IReadOnlyList<ScanOutlier> Outliers,
    bool OutliersExcluded,
    bool DeviationOnly,
    IReadOnlyList<int> Files,
    string SetSha256,
    string SerialSummary,
    IReadOnlyList<string> Warnings,
    double? WithinC4 = null)
{
    /// <summary>The stage as shown: "(none)" for a table without stages.</summary>
    public string StageLabel => Stage.Length == 0 ? "(none)" : Stage;

    /// <summary>
    /// How <see cref="SigmaWithin"/> was formed, for the output: pooled over the subgroups and divided by c4(d), d = Σ(nⱼ − 1) + 1 (<see cref="WithinC4"/> is that divisor),
    /// otherwise the overall σ (no subgrouping).
    /// </summary>
    public string WithinBasis => WithinC4 is { } c4
        ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"pooled over the subgroups and divided by c4 = {c4:0.#####} (unbiased)")
        : "the overall σ (no subgrouping)";
}
