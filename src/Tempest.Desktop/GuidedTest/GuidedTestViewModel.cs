using System.Globalization;
using Tempest.Calc.Library.ManualTesting;
using Tempest.Core.Logging;

namespace Tempest.Desktop.GuidedTest;

/// <summary>One row of the step list: the step, its label and what has been recorded for it in the run.</summary>
public sealed record GuidedStepRow(string Id, string Label, string Result);

/// <summary>One expected output of the shown example beside what the form shows and how they compare.</summary>
public sealed record GuidedExpectedLine(string Symbol, string Expected, string Allowance, string? Shown, GuidedCompareOutcome? Outcome);

/// <summary>
/// Addendum C, C-5: the state behind the Guided test panel, with no UI in it. It walks the script a step at a time, opens the worksheet form on an
/// example's inputs (an unsaved scratch sheet), shows the expected outputs and the tolerance rule, can compare the form's current outputs with them
/// (and pre-ticks the result it would give, which the tester confirms), and records the tester's Pass, Fail, Blocked or Skipped with a note in a
/// test-run record. It holds no calculation, Evidence or issue service: nothing it does can change one (ADR-0175).
/// <para>
/// Threading: every method is called on the UI thread and continues on it; <see cref="Changed"/> is raised there.
/// </para>
/// </summary>
public sealed class GuidedTestViewModel
{
    /// <summary>The filter that lists every step.</summary>
    public const string AllSteps = "All steps";

    /// <summary>The filter that lists the steps with no result in the run.</summary>
    public const string NotYetRun = "Not yet run";

    /// <summary>The longest search text taken.</summary>
    public const int MaxSearchLength = 100;

    private static readonly (GuidedStepKind Kind, string Label)[] KindFilters =
    [
        (GuidedStepKind.Smoke, "4 · Smoke test"),
        (GuidedStepKind.Example, "5 · Worked examples"),
        (GuidedStepKind.Pack, "6 · Linked chains"),
        (GuidedStepKind.Import, "7 · Imports and records"),
        (GuidedStepKind.Lifting, "8 · Lifting"),
        (GuidedStepKind.Legacy, "9 · Legacy records"),
    ];

    private readonly ManualTestRunService _service;
    private readonly IGuidedWorksheetHost _worksheet;
    private readonly GuidedTestScript _script;
    private readonly ILogger? _logger;
    private ManualTestRun? _run;
    private ManualTestRun? _resumable;
    private GuidedTestStep? _current;
    private string _filter = AllSteps;
    private string _search = string.Empty;
    private string? _discardArmedFor;
    private GuidedAutoCompareResult? _comparison;
    private List<GuidedTestStep> _visible = [];

    /// <param name="service">The run records (and the script a run is started on).</param>
    /// <param name="worksheet">The worksheet form, as far as the guided test may touch it.</param>
    /// <param name="logger">The host's logger; one Information line per run started, resumed or finished (ids and counts only). Null logs nothing.</param>
    public GuidedTestViewModel(ManualTestRunService service, IGuidedWorksheetHost worksheet, ILogger? logger = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
        _script = service.Script;
        _logger = logger;
        ApplyFilter();
        _current = _visible.FirstOrDefault();
    }

    /// <summary>Raised (on the UI thread) whenever anything the panel shows has changed.</summary>
    public event Action? Changed;

    /// <summary>The script being walked.</summary>
    public GuidedTestScript Script => _script;

    /// <summary>The tolerance rule, in the words shown beside every expected value.</summary>
    public string Rule => GuidedTolerance.RuleText;

    /// <summary>What the last action did, or why it was refused, in one sentence.</summary>
    public string Status { get; private set; } = "Start a run to begin, or resume the unfinished one if there is one.";

    // ------------------------------------------------------------------ run

    /// <summary>The run being walked, or <see langword="null"/> before one is started or resumed.</summary>
    public ManualTestRun? Run => _run;

    /// <summary>The newest unfinished run on file that is not the one being walked: the one to offer to resume.</summary>
    public ManualTestRun? Resumable => _resumable;

    /// <summary>Whether results can be recorded now: a run is open, unfinished, and on the script this build carries.</summary>
    public bool CanRecord => _run is { IsFinished: false } run && string.Equals(run.ScriptVersion, _script.ScriptVersion, StringComparison.Ordinal);

    /// <summary>Whether the open run can be finished.</summary>
    public bool CanFinish => _run is { IsFinished: false };

    /// <summary>Who ran it, on which build, since when, for the header.</summary>
    public string RunText => _run is not { } run
        ? "No run is open."
        : $"Run by {run.Tester} on {run.Build}, started {Stamp(run.StartedUtc)}{(run.FinishedUtc is { } done ? $", finished {Stamp(done)}" : ", not finished")}. Script {run.ScriptVersion}.";

    /// <summary>How the open run stands: <c>12 pass, 1 fail, 0 blocked, 2 skipped, 94 not run</c>.</summary>
    public string TallyText => _run is { } run ? ManualTestTally.Of(run, _script).ToString() : string.Empty;

    /// <summary>The offer to resume, in words (empty when there is none).</summary>
    public string ResumeText => _resumable is { } run
        ? $"An unfinished run by {run.Tester}, started {Stamp(run.StartedUtc)} on {run.Build}, can be resumed ({ManualTestTally.Of(run, _script)})."
        : string.Empty;

    /// <summary>Looks for an unfinished run on file (and re-reads the open one), so the panel can offer to resume it. Called each time the screen is shown.</summary>
    public async Task RefreshAsync()
    {
        try
        {
            if (_run is { } open && await _service.FindAsync(open.Id).ConfigureAwait(true) is { } reread)
                _run = reread;

            var unfinished = await _service.FindUnfinishedAsync().ConfigureAwait(true);
            _resumable = unfinished is not null && unfinished.Id != _run?.Id ? unfinished : null;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            Status = $"The test runs on file could not be read: {failure.Message}";
        }

        LoadDraft();
        Changed?.Invoke();
    }

    /// <summary>Starts a new run for the person signed in.</summary>
    public async Task StartNewAsync()
    {
        try
        {
            _run = await _service.StartAsync().ConfigureAwait(true);
            _resumable = null;
            _logger?.Information($"Guided test: run {_run.Id:N} started on {_run.Build} by {_run.Tester}.");
            Status = $"Started a new run on {_run.Build}. Open a step and work down the list.";
            ApplyFilter();
            SelectFirstNotRun();
        }
        catch (ManualTestRefusedException refusal)
        {
            Status = refusal.Message;
        }

        Changed?.Invoke();
    }

    /// <summary>Resumes the unfinished run offered by <see cref="Resumable"/>, going to its first step without a result.</summary>
    public async Task ResumeAsync()
    {
        if (_resumable is not { } run)
        {
            Status = "There is no unfinished run to resume.";
            Changed?.Invoke();
            return;
        }

        _run = await _service.FindAsync(run.Id).ConfigureAwait(true) ?? run;
        _resumable = null;
        _logger?.Information($"Guided test: run {_run.Id:N} resumed ({ManualTestTally.Of(_run, _script)}).");
        Status = CanRecord
            ? $"Resumed the run started {Stamp(_run.StartedUtc)}. {ManualTestTally.Of(_run, _script)}."
            : $"That run was started on script {_run.ScriptVersion} and this build carries {_script.ScriptVersion}, so nothing more can be added to it. Finish it, then start a new run.";
        ApplyFilter();
        SelectFirstNotRun();
        Changed?.Invoke();
    }

    /// <summary>Finishes the open run: it is dated and read-only from then on.</summary>
    public async Task FinishAsync()
    {
        if (_run is not { IsFinished: false } run)
        {
            Status = "There is no unfinished run open to finish.";
            Changed?.Invoke();
            return;
        }

        try
        {
            _run = await _service.FinishAsync(run.Id).ConfigureAwait(true);
            _logger?.Information($"Guided test: run {_run.Id:N} finished ({ManualTestTally.Of(_run, _script)}).");
            Status = $"Run finished {Stamp(_run.FinishedUtc!.Value)}: {ManualTestTally.Of(_run, _script)}. Copy or save the results sheet to send it back.";
        }
        catch (ManualTestRefusedException refusal)
        {
            Status = refusal.Message;
        }

        Changed?.Invoke();
    }

    /// <summary>The open run as the markdown results sheet, or <see langword="null"/> when no run is open.</summary>
    public string? ExportMarkdown() => _run is { } run ? _service.ExportMarkdown(run) : null;

    /// <summary>A file name for the saved results sheet: the commit and the date, for example <c>tempestos-guided-test-1a2b3c4-20261026.md</c>.</summary>
    public string SuggestedFileName => _run is { } run
        ? $"tempestos-guided-test-{Safe(run.BuildCommit)}-{run.StartedUtc.UtcDateTime:yyyyMMdd}.md"
        : "tempestos-guided-test.md";

    // ------------------------------------------------------------------ steps

    /// <summary>The filters offered: all steps, those not yet run, and one per section of the runbook.</summary>
    public IReadOnlyList<string> Filters => [AllSteps, NotYetRun, .. KindFilters.Where(f => _script.Steps.Any(s => s.Kind == f.Kind)).Select(f => f.Label)];

    /// <summary>The filter in use.</summary>
    public string Filter => _filter;

    /// <summary>The search text in use.</summary>
    public string Search => _search;

    /// <summary>The steps the filter and search let through, each with its result in the run.</summary>
    public IReadOnlyList<GuidedStepRow> Rows => [.. _visible.Select(s => new GuidedStepRow(s.Id, LabelOf(s), ResultText(s)))];

    /// <summary>Index of <see cref="Current"/> within <see cref="Rows"/>, or -1.</summary>
    public int CurrentIndex => _current is null ? -1 : _visible.IndexOf(_current);

    /// <summary>The step shown.</summary>
    public GuidedTestStep? Current => _current;

    /// <summary>The worked example the shown step opens (the step itself for an example; the example a smoke step pastes), or <see langword="null"/>.</summary>
    public GuidedTestStep? CurrentExample => _current is null ? null : _script.ExampleOf(_current);

    /// <summary>Chooses a filter (one of <see cref="Filters"/>); an unknown name is ignored.</summary>
    public void SetFilter(string filter)
    {
        if (!Filters.Contains(filter, StringComparer.Ordinal) || string.Equals(filter, _filter, StringComparison.Ordinal))
            return;

        _filter = filter;
        ApplyFilter();
        KeepCurrentOrFirst();
        Changed?.Invoke();
    }

    /// <summary>Narrows the list to steps whose title, template or discipline contains <paramref name="text"/> (capped at <see cref="MaxSearchLength"/> characters).</summary>
    public void SetSearch(string? text)
    {
        var search = (text ?? string.Empty).Trim();
        search = search.Length > MaxSearchLength ? search[..MaxSearchLength] : search;

        if (string.Equals(search, _search, StringComparison.Ordinal))
            return;

        _search = search;
        ApplyFilter();
        KeepCurrentOrFirst();
        Changed?.Invoke();
    }

    /// <summary>Shows the step <paramref name="stepId"/>, clearing the earlier comparison and loading any result already recorded for it.</summary>
    public void Select(string stepId)
    {
        if (_script.Find(stepId) is not { } step)
            return;

        // A step outside the filter is shown by widening it, so a jump never lands on a step the list does not show.
        if (!_visible.Contains(step))
        {
            _filter = AllSteps;
            _search = string.Empty;
            ApplyFilter();
        }

        MoveTo(step);
        Changed?.Invoke();
    }

    /// <summary>The next step in the list (stays on the last).</summary>
    public void Next() => Step(+1);

    /// <summary>The previous step in the list (stays on the first).</summary>
    public void Previous() => Step(-1);

    /// <summary>The next step with no result in the run, after the shown one, wrapping to the start; says so when every step has one.</summary>
    public void NextNotRun()
    {
        var start = Math.Max(0, CurrentIndex);
        var order = _visible.Skip(start + 1).Concat(_visible.Take(start + 1));
        var next = order.FirstOrDefault(s => _run?.ResultFor(s.Id) is null);

        if (next is null)
        {
            Status = "Every step in this list has a result.";
            Changed?.Invoke();
            return;
        }

        MoveTo(next);
        Changed?.Invoke();
    }

    // ------------------------------------------------------------------ the shown step

    /// <summary>A note when the example was written for another version of the worksheet than this build ships, else empty.</summary>
    public string VersionNote
    {
        get
        {
            if (CurrentExample is not { TemplateId: { } id, TemplateVersion: { } written })
                return string.Empty;

            var shipped = _worksheet.ShippedTemplateVersion(id);
            return shipped is null || string.Equals(shipped, written, StringComparison.Ordinal)
                ? string.Empty
                : $"This example was written for version {written} of the worksheet; this build has {shipped}, so values may differ.";
        }
    }

    /// <summary>The example's expected outputs and governing check, each with its allowance and, after a comparison, what the form shows.</summary>
    public IReadOnlyList<GuidedExpectedLine> ExpectedLines
    {
        get
        {
            if (CurrentExample is not { } example)
                return [];

            var lines = new List<GuidedExpectedLine>();
            var byName = _comparison?.Rows.ToDictionary(r => r.Symbol, StringComparer.Ordinal) ?? [];

            foreach (var expected in example.Expected ?? [])
            {
                var row = byName.GetValueOrDefault(expected.Symbol);
                lines.Add(new GuidedExpectedLine(expected.Symbol, Join(expected.Value, expected.Unit), GuidedTolerance.AllowanceText(expected.Value), row?.Shown, row?.Comparison.Outcome));
            }

            if (example.Governing is { } governing)
            {
                var outcome = byName.GetValueOrDefault("governing check");
                var utilisation = byName.GetValueOrDefault("governing utilisation");
                lines.Add(new GuidedExpectedLine("governing check", $"{governing.Outcome} — {governing.Name}", "exact text", outcome?.Shown, outcome?.Comparison.Outcome));
                lines.Add(new GuidedExpectedLine("governing utilisation", governing.Utilisation, GuidedTolerance.AllowanceText(governing.Utilisation), utilisation?.Shown, utilisation?.Comparison.Outcome));
            }

            return lines;
        }
    }

    /// <summary>The reasons for every line of the last comparison that does not match, one sentence each.</summary>
    public IReadOnlyList<string> ComparisonNotes => [.. _comparison?.Rows.Where(r => !r.Comparison.IsMatch).Select(r => r.Comparison.Detail) ?? []];

    /// <summary>What the auto-compare suggests (the result it pre-ticked), or <see langword="null"/>.</summary>
    public ManualTestOutcome? Suggested => _comparison?.Suggested;

    /// <summary>The result recorded for the shown step in the open run, if any.</summary>
    public ManualTestStepResult? Recorded => _current is null ? null : _run?.ResultFor(_current.Id);

    /// <summary>The result ticked (not yet recorded): the recorded one when the step is shown, or what an auto-compare pre-ticked, or the tester's choice.</summary>
    public ManualTestOutcome? DraftOutcome { get; private set; }

    /// <summary>The note being written for the shown step (at most <see cref="ManualTestRun.MaxNoteLength"/> characters can be recorded).</summary>
    public string DraftNote { get; private set; } = string.Empty;

    /// <summary>The values the form showed, as one line, for the record.</summary>
    public string DraftValues { get; private set; } = string.Empty;

    /// <summary>Ticks a result (the tester's own choice).</summary>
    public void SetDraftOutcome(ManualTestOutcome? outcome)
    {
        DraftOutcome = outcome;
        Changed?.Invoke();
    }

    /// <summary>Sets the note text being typed. It is not recorded until <see cref="RecordAsync"/>.</summary>
    public void SetDraftNote(string? note)
    {
        DraftNote = note ?? string.Empty;
        Changed?.Invoke();
    }

    /// <summary>Sets the values-shown text being typed or filled by a comparison.</summary>
    public void SetDraftValues(string? values)
    {
        DraftValues = values ?? string.Empty;
        Changed?.Invoke();
    }

    /// <summary>
    /// Opens the worksheet form on the shown example with its inputs loaded (an unsaved scratch sheet). Where the form holds unsaved edits the first
    /// call says so and the second call for the same step discards them.
    /// </summary>
    public void OpenWorksheet()
    {
        if (CurrentExample is not { TemplateId: { } templateId, InputsJson: { } inputs } example)
        {
            Status = "This step has no worksheet to open; follow its instructions.";
            Changed?.Invoke();
            return;
        }

        var discard = string.Equals(_discardArmedFor, _current!.Id, StringComparison.Ordinal);
        var result = _worksheet.Prepare(templateId, inputs, discard);
        _discardArmedFor = result.Outcome == GuidedPrepareOutcome.NeedsConfirmation ? _current.Id : null;
        _comparison = null;
        Status = result.Outcome == GuidedPrepareOutcome.Ready
            ? $"{result.Message} Run it, then compare its outputs with the expected values here{(example.Governing is null ? string.Empty : " and the governing check")}."
            : result.Message;
        Changed?.Invoke();
    }

    /// <summary>
    /// Reads the form's current outputs against the expected values by the stated rule, fills the values shown, and pre-ticks the result it gives. The
    /// tester confirms it, or changes it, by recording a result; nothing is recorded here.
    /// </summary>
    public void AutoCompare()
    {
        if (CurrentExample is not { TemplateId: { } templateId } example)
        {
            Status = "This step has no expected outputs to compare.";
            Changed?.Invoke();
            return;
        }

        if (_worksheet.ShownRun(templateId) is not { } run)
        {
            Status = "Open the worksheet for this step first; there is nothing in the form to compare.";
            Changed?.Invoke();
            return;
        }

        _comparison = GuidedAutoCompare.Compare(example, run);
        DraftValues = _comparison.ValuesShown;
        var differing = _comparison.Rows.Count(r => !r.Comparison.IsMatch);

        if (_comparison.Suggested is { } suggested)
        {
            DraftOutcome = suggested;
            Status = differing == 0
                ? $"All {_comparison.Rows.Count} lines agree with the expected values. Pass is ticked; record it to confirm."
                : $"{differing} of {_comparison.Rows.Count} lines differ (listed below). Fail is ticked; check them, then record the result you decide.";
        }
        else
        {
            DraftOutcome = null;
            Status = $"{differing} of {_comparison.Rows.Count} lines need your judgement (listed below). Nothing is ticked; choose the result and record it.";
        }

        Changed?.Invoke();
    }

    /// <summary>Records the ticked result with the note and the values shown for the shown step, in the open run.</summary>
    public async Task RecordAsync()
    {
        if (_current is not { } step)
        {
            Status = "Choose a step first.";
            Changed?.Invoke();
            return;
        }

        if (_run is null)
        {
            Status = "Start or resume a run before recording a result.";
            Changed?.Invoke();
            return;
        }

        if (DraftOutcome is not { } outcome)
        {
            Status = "Tick Pass, Fail, Blocked or Skipped first.";
            Changed?.Invoke();
            return;
        }

        try
        {
            _run = await _service.RecordAsync(_run.Id, step.Id, outcome, DraftNote, DraftValues).ConfigureAwait(true);
            Status = $"Recorded {outcome} for '{step.Title}'. {ManualTestTally.Of(_run, _script)}.";
        }
        catch (ManualTestRefusedException refusal)
        {
            Status = refusal.Message;
        }

        Changed?.Invoke();
    }

    // ------------------------------------------------------------------

    private void Step(int by)
    {
        if (_visible.Count == 0)
            return;

        var index = Math.Clamp(Math.Max(CurrentIndex, 0) + by, 0, _visible.Count - 1);
        MoveTo(_visible[index]);
        Changed?.Invoke();
    }

    private void SelectFirstNotRun()
    {
        var first = _visible.FirstOrDefault(s => _run?.ResultFor(s.Id) is null) ?? _visible.FirstOrDefault();

        if (first is not null)
            MoveTo(first);
    }

    private void KeepCurrentOrFirst()
    {
        if (_current is null || !_visible.Contains(_current))
        {
            _current = _visible.FirstOrDefault();
            LoadDraft();
        }
    }

    private void MoveTo(GuidedTestStep step)
    {
        _current = step;
        _comparison = null;
        _discardArmedFor = null;
        LoadDraft();
    }

    /// <summary>The draft starts from what is recorded for the step (its result, note and values), or empty.</summary>
    private void LoadDraft()
    {
        var recorded = Recorded;
        DraftOutcome = recorded?.Outcome;
        DraftNote = recorded?.Note ?? string.Empty;
        DraftValues = recorded?.ValuesShown ?? string.Empty;
    }

    private void ApplyFilter()
    {
        IEnumerable<GuidedTestStep> steps = _script.Steps;
        var kind = KindFilters.FirstOrDefault(f => string.Equals(f.Label, _filter, StringComparison.Ordinal));

        if (string.Equals(_filter, NotYetRun, StringComparison.Ordinal))
            steps = steps.Where(s => _run?.ResultFor(s.Id) is null);
        else if (kind.Label is not null)
            steps = steps.Where(s => s.Kind == kind.Kind);

        if (_search.Length > 0)
        {
            steps = steps.Where(s => Contains(s.Title, _search) || Contains(s.TemplateId, _search) || Contains(s.ExampleId, _search) || Contains(s.Discipline, _search));
        }

        _visible = [.. steps];
    }

    private static bool Contains(string? text, string search) => text is not null && text.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static string LabelOf(GuidedTestStep step) =>
        step.Kind == GuidedStepKind.Example ? $"{step.TemplateId} — {step.Title}" : step.Title;

    private string ResultText(GuidedTestStep step) => _run?.ResultFor(step.Id) is { } result ? result.Outcome.ToString() : "—";

    private static string Join(string value, string unit) => unit.Length == 0 ? value : $"{value} {unit}";

    private static string Stamp(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";

    private static string Safe(string text) => new string([.. text.Where(c => char.IsAsciiLetterOrDigit(c))]) is { Length: > 0 } safe ? safe : "build";
}
