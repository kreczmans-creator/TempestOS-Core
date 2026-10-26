namespace Tempest.Calc.Library.ManualTesting;

/// <summary>What the tester concluded about one step. Skipped means the step was not done; it is not a result and never counts towards a template's manual status.</summary>
public enum ManualTestOutcome
{
    /// <summary>The step did what the script says.</summary>
    Pass,

    /// <summary>The step did not do what the script says.</summary>
    Fail,

    /// <summary>The step could not be tried (something it needs is missing or broken).</summary>
    Blocked,

    /// <summary>The tester chose not to do the step in this run.</summary>
    Skipped,
}

/// <summary>The tester's result for one step of one run.</summary>
/// <param name="StepId">The script step.</param>
/// <param name="Outcome">Pass, Fail, Blocked or Skipped.</param>
/// <param name="Note">The tester's note, at most <see cref="ManualTestRun.MaxNoteLength"/> characters.</param>
/// <param name="ValuesShown">The values the form showed when the result was recorded (for example <c>P = 3.93 kN; L_10h = 4290.3 h</c>), at most <see cref="ManualTestRun.MaxValuesShownLength"/> characters.</param>
/// <param name="RecordedUtc">When the result was last recorded.</param>
public sealed record ManualTestStepResult(string StepId, ManualTestOutcome Outcome, string Note, string ValuesShown, DateTimeOffset RecordedUtc);

/// <summary>
/// One run of the guided test (Addendum C, C-5): who ran it, on which build, against which script, and what they concluded for each step.
/// It is a test-run record: it is stored in its own collection (<c>Manual.TestRuns</c>, ADR-0175) and is read by nothing that calculates, checks
/// or issues. A finished run is never changed.
/// </summary>
public sealed record ManualTestRun
{
    /// <summary>The longest note the tester may leave on a step.</summary>
    public const int MaxNoteLength = 2_000;

    /// <summary>The longest values-shown text kept for a step; a longer one the form produced is cut with a mark.</summary>
    public const int MaxValuesShownLength = 4_000;

    /// <summary>The longest tester name, build or script version kept.</summary>
    public const int MaxIdentityLength = 200;

    /// <summary>The most step results one run may hold (the script has far fewer steps; this caps a damaged record).</summary>
    public const int MaxResults = 1_000;

    /// <summary>The document version this build writes and reads.</summary>
    public const int CurrentSchema = 1;

    /// <summary>The document version (additive: a later build adds fields and reads this one unchanged).</summary>
    public int Schema { get; init; } = CurrentSchema;

    /// <summary>The run's id.</summary>
    public required Guid Id { get; init; }

    /// <summary>The script version the run started against (<see cref="GuidedTestScript.ScriptVersion"/>).</summary>
    public required string ScriptVersion { get; init; }

    /// <summary>When the run started, UTC.</summary>
    public required DateTimeOffset StartedUtc { get; init; }

    /// <summary>When the run was finished, UTC; <see langword="null"/> while it can still be resumed.</summary>
    public DateTimeOffset? FinishedUtc { get; init; }

    /// <summary>The build the tester ran, as the app's title bar shows it (for example <c>0.26.0 (1a2b3c4)</c>).</summary>
    public required string Build { get; init; }

    /// <summary>The commit of the build (the title bar's short commit), or <c>unknown</c> when the build carries none.</summary>
    public required string BuildCommit { get; init; }

    /// <summary>The tester: the person the app signs work off as (display name, or the sign-in id when there is none).</summary>
    public required string Tester { get; init; }

    /// <summary>The sign-in id of the tester.</summary>
    public string TesterId { get; init; } = string.Empty;

    /// <summary>The results recorded so far, at most one per step.</summary>
    public IReadOnlyList<ManualTestStepResult> Results { get; init; } = [];

    /// <summary>Whether the run is finished (and so read-only).</summary>
    public bool IsFinished => FinishedUtc is not null;

    /// <summary>The result for a step, or <see langword="null"/> when none is recorded.</summary>
    public ManualTestStepResult? ResultFor(string stepId) => Results.FirstOrDefault(r => string.Equals(r.StepId, stepId, StringComparison.Ordinal));
}

/// <summary>A counted summary of a run against a script.</summary>
public sealed record ManualTestTally(int Pass, int Fail, int Blocked, int Skipped, int NotRun)
{
    /// <summary>Counts the results of <paramref name="run"/> over the steps of <paramref name="script"/>; a result for a step the script no longer has is not counted.</summary>
    public static ManualTestTally Of(ManualTestRun run, GuidedTestScript script)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(script);
        int pass = 0, fail = 0, blocked = 0, skipped = 0, notRun = 0;

        foreach (var step in script.Steps)
        {
            switch (run.ResultFor(step.Id)?.Outcome)
            {
                case ManualTestOutcome.Pass: pass++; break;
                case ManualTestOutcome.Fail: fail++; break;
                case ManualTestOutcome.Blocked: blocked++; break;
                case ManualTestOutcome.Skipped: skipped++; break;
                default: notRun++; break;
            }
        }

        return new ManualTestTally(pass, fail, blocked, skipped, notRun);
    }

    /// <summary>One line for a status bar: <c>12 pass, 1 fail, 0 blocked, 2 skipped, 94 not run</c>.</summary>
    public override string ToString() => $"{Pass} pass, {Fail} fail, {Blocked} blocked, {Skipped} skipped, {NotRun} not run";
}
