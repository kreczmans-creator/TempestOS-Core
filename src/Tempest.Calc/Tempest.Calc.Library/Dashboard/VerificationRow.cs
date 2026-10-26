namespace Tempest.Calc.Library.Dashboard;

/// <summary>Whether a row is a worksheet calculator (a template) or a chain (a calculation pack template).</summary>
public enum VerificationRowKind
{
    Calculator,
    Chain,
}

/// <summary>Whether the known-answer cases of a row have been run and how they came out.</summary>
public enum KatRunState
{
    /// <summary>No results for this version of the template (or the chain has no worked example).</summary>
    NotRun,

    /// <summary>Every case passed in the last run.</summary>
    Passed,

    /// <summary>At least one case failed in the last run.</summary>
    Failed,
}

/// <summary>The status filter of the dashboard. <see cref="All"/> lists every row; each other value lists the rows that have that condition.</summary>
public enum VerificationStatusFilter
{
    All,

    /// <summary>A known-answer case failed in the last run.</summary>
    Failing,

    /// <summary>At least one Track P finding is not fixed (still open, or waiting for the owner).</summary>
    OpenDefects,

    /// <summary>At least one known-answer case is Unconfirmed or from a Secondary source.</summary>
    UnconfirmedOrSecondary,

    /// <summary>At least one record kind the template draws on has no record entered.</summary>
    MissingRecords,

    /// <summary>The known-answer cases have no results for this version.</summary>
    NotRun,

    /// <summary>The owner has not run the manual test.</summary>
    NotManuallyTested,

    /// <summary>None of failing, open defects, unconfirmed or secondary, missing records or not run.</summary>
    Clean,
}

/// <summary>The outcome of the owner's manual test of a row, from the guided test run records (Addendum C, C-5).</summary>
public enum ManualTestOutcome
{
    NotTested,
    Passed,
    Failed,
    Partial,
}

/// <summary>The manual-test status of one row: the outcome, when it was recorded and an optional note.</summary>
public sealed record ManualTestStatus(ManualTestOutcome Outcome, DateTimeOffset? At = null, string? Note = null)
{
    /// <summary>The status of a row nobody has tested.</summary>
    public static ManualTestStatus NotTested { get; } = new(ManualTestOutcome.NotTested);

    /// <summary>How the dashboard words it.</summary>
    public string Label => Outcome switch
    {
        ManualTestOutcome.Passed => "Passed",
        ManualTestOutcome.Failed => "Failed",
        ManualTestOutcome.Partial => "Partly passed",
        _ => "Not tested",
    };
}

/// <summary>One Track P finding on a row, with where it stands.</summary>
public sealed record VerificationFinding(string Id, string Severity, VerificationDefectStatus? Defect)
{
    /// <summary>Whether it counts as an open defect: listed in the known-defects document and not Fixed (and not a non-defect).</summary>
    public bool IsOpen => Defect is VerificationDefectStatus.StillOpen or VerificationDefectStatus.OwnerDecision;

    /// <summary>How the dashboard words the status.</summary>
    public string StatusLabel => Defect switch
    {
        VerificationDefectStatus.Fixed => "Fixed",
        VerificationDefectStatus.StillOpen => "Still open",
        VerificationDefectStatus.OwnerDecision => "Owner decision",
        VerificationDefectStatus.NotADefect => "Not a defect",
        _ => "Not triaged",
    };
}

/// <summary>
/// One row of the verification dashboard (Addendum C, C-4): a worksheet calculator or a chain with everything the owner needs to judge
/// how far it can be trusted. Built read-only by <see cref="VerificationDashboardService"/>; nothing here changes a calculation, its
/// Evidence status or an issued record.
/// </summary>
/// <param name="Kind">Calculator or chain.</param>
/// <param name="Id">The template id, or the pack template id of a chain.</param>
/// <param name="Title">The title shown on the worksheet or the chain.</param>
/// <param name="Discipline">The discipline's label; "Chains" for a chain.</param>
/// <param name="Version">The template's version; empty for a chain, which has none (see <see cref="Steps"/>).</param>
/// <param name="KatCount">A calculator's known-answer cases; a chain's issued worked examples.</param>
/// <param name="KatPassed">How many of them passed in the last run.</param>
/// <param name="KatState">Not run, passed or failed.</param>
/// <param name="KatNote">Why a row is not run ("results are for 1.1.0"), or empty.</param>
/// <param name="WorkedExamples">The worked examples under <c>docs/worked-examples</c>.</param>
/// <param name="Unconfirmed">Known-answer cases whose reference value could not be confirmed against the source.</param>
/// <param name="Secondary">Known-answer cases taken from a secondary source. For a chain: the sums over its distinct calculators.</param>
/// <param name="RederivationResult">Track P's result, in words ("Match, with findings", "Not re-derived").</param>
/// <param name="RederivationFindings">Track P's findings of Medium or above: how many of each.</param>
/// <param name="RederivationVerdict">The verdict line of the report, clipped; empty when there is no report.</param>
/// <param name="RecordKinds">The titles of the record kinds the template (or the chain's calculators) can draw on, material kinds left out.</param>
/// <param name="MissingRecordKinds">The titles of those with no record entered.</param>
/// <param name="Findings">Every Track P finding of the row.</param>
/// <param name="ManualTest">The owner's manual-test status.</param>
/// <param name="Steps">A chain's calculators as <c>id@version</c>; empty for a calculator.</param>
public sealed record VerificationRow(
    VerificationRowKind Kind,
    string Id,
    string Title,
    string Discipline,
    string Version,
    int KatCount,
    int KatPassed,
    KatRunState KatState,
    string KatNote,
    int WorkedExamples,
    int Unconfirmed,
    int Secondary,
    string RederivationResult,
    VerificationSeverityCounts RederivationFindings,
    string RederivationVerdict,
    IReadOnlyList<string> RecordKinds,
    IReadOnlyList<string> MissingRecordKinds,
    IReadOnlyList<VerificationFinding> Findings,
    ManualTestStatus ManualTest,
    IReadOnlyList<string> Steps)
{
    /// <summary>The findings that are open defects (not Fixed), worst first.</summary>
    public IReadOnlyList<VerificationFinding> OpenDefects { get; } = [.. Findings.Where(f => f.IsOpen).OrderBy(f => SeverityRank(f.Severity)).ThenBy(f => f.Id, StringComparer.Ordinal)];

    /// <summary>The number of open defects.</summary>
    public int OpenDefectCount => OpenDefects.Count;

    /// <summary>Whether a known-answer case failed.</summary>
    public bool IsFailing => KatState == KatRunState.Failed;

    /// <summary>Whether any case is Unconfirmed or Secondary.</summary>
    public bool HasUnconfirmedOrSecondary => Unconfirmed + Secondary > 0;

    /// <summary>Whether a record kind has no record.</summary>
    public bool HasMissingRecords => MissingRecordKinds.Count > 0;

    /// <summary>Whether nothing needs attention (the manual test is not counted).</summary>
    public bool IsClean => !IsFailing && OpenDefectCount == 0 && !HasUnconfirmedOrSecondary && !HasMissingRecords && KatState != KatRunState.NotRun;

    /// <summary>The one word that sums the row up, worst condition first.</summary>
    public string Headline =>
        IsFailing ? "Failing"
        : OpenDefectCount > 0 ? "Open defects"
        : KatState == KatRunState.NotRun ? "Not run"
        : HasMissingRecords ? "Missing records"
        : HasUnconfirmedOrSecondary ? "Unconfirmed"
        : "Clean";

    /// <summary>The known-answer column in words: "3 of 3 pass", "1 of 4 pass, 3 fail", "not run".</summary>
    public string KatText => KatState switch
    {
        KatRunState.NotRun => KatNote.Length > 0 ? $"not run ({KatNote})" : "not run",
        KatRunState.Failed => $"{KatPassed} of {KatCount} pass, {KatCount - KatPassed} fail",
        _ => $"{KatPassed} of {KatCount} pass",
    };

    /// <summary>Whether the row has the condition <paramref name="status"/> names.</summary>
    public bool Matches(VerificationStatusFilter status) => status switch
    {
        VerificationStatusFilter.All => true,
        VerificationStatusFilter.Failing => IsFailing,
        VerificationStatusFilter.OpenDefects => OpenDefectCount > 0,
        VerificationStatusFilter.UnconfirmedOrSecondary => HasUnconfirmedOrSecondary,
        VerificationStatusFilter.MissingRecords => HasMissingRecords,
        VerificationStatusFilter.NotRun => KatState == KatRunState.NotRun,
        VerificationStatusFilter.NotManuallyTested => ManualTest.Outcome == ManualTestOutcome.NotTested,
        VerificationStatusFilter.Clean => IsClean,
        _ => false,
    };

    /// <summary>Critical 0, High 1, Medium 2, Low 3, Info 4, anything else 5.</summary>
    public static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 0,
        "High" => 1,
        "Medium" => 2,
        "Low" => 3,
        "Info" => 4,
        _ => 5,
    };
}

/// <summary>How many Track P findings of each severity from Medium up a row has (Low, Info and rejected findings are not counted).</summary>
public sealed record VerificationSeverityCounts(int Critical, int High, int Medium)
{
    /// <summary>No findings of Medium or above.</summary>
    public static VerificationSeverityCounts None { get; } = new(0, 0, 0);

    /// <summary>The total.</summary>
    public int Total => Critical + High + Medium;

    /// <summary>"1 High, 5 Medium", or "none at Medium or above".</summary>
    public string Text
    {
        get
        {
            var parts = new List<string>();

            if (Critical > 0)
                parts.Add($"{Critical} Critical");

            if (High > 0)
                parts.Add($"{High} High");

            if (Medium > 0)
                parts.Add($"{Medium} Medium");

            return parts.Count == 0 ? "none at Medium or above" : string.Join(", ", parts);
        }
    }
}
