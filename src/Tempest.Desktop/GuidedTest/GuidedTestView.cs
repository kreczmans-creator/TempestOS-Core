using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Tempest.Calc.Library.ManualTesting;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.GuidedTest;

/// <summary>
/// Addendum C, C-5: the Guided test panel. It sits beside the worksheet form (Engineering → Modules → Calculations → Guided test): the run, the step
/// list, the shown step with its instructions, the example's expected outputs with the tolerance rule, and the result the tester records. All behaviour
/// is in <see cref="GuidedTestViewModel"/>; this class draws it and hosts the clipboard and the save picker.
/// </summary>
public sealed class GuidedTestView : UserControl
{
    public const string Heading = "Guided test";
    public const string StartCaption = "Start a new run";
    public const string ResumeCaption = "Resume the unfinished run";
    public const string FinishCaption = "Finish the run";
    public const string CopyCaption = "Copy results sheet";
    public const string SaveCaption = "Save results sheet…";
    public const string DeleteOlderCaption = "Delete older finished runs…";
    public const string OpenWorksheetCaption = "Open worksheet with these inputs";
    public const string CompareCaption = "Compare with the form";
    public const string RecordCaption = "Record result";
    public const string PreviousCaption = "Previous step";
    public const string NextCaption = "Next step";
    public const string NextNotRunCaption = "Next not run";

    /// <summary>The width of the panel beside the worksheet form.</summary>
    public const double PanelWidth = 440;

    private readonly GuidedTestViewModel _model;
    private readonly TextBlock _runText = Wrapped(string.Empty);
    private readonly TextBlock _tally = Wrapped(string.Empty);
    private readonly TextBlock _resumeText = Wrapped(string.Empty);
    private readonly Button _start = Command(StartCaption);
    private readonly Button _resume = Command(ResumeCaption);
    private readonly Button _finish = Command(FinishCaption);
    private readonly Button _copy = Command(CopyCaption);
    private readonly Button _save = Command(SaveCaption);
    private readonly Button _deleteOlder = Command(DeleteOlderCaption);
    private readonly ComboBox _filter = new() { MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _search = new() { Watermark = "Search steps", MaxLength = GuidedTestViewModel.MaxSearchLength, MinHeight = DesignTokens.MinControlSize };
    private readonly ListBox _steps = new() { MaxHeight = 210 };
    private readonly TextBlock _title = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _subtitle = Wrapped(string.Empty);
    private readonly StackPanel _instructions = new() { Spacing = 2 };
    private readonly TextBlock _passWhen = Wrapped(string.Empty);
    private readonly TextBlock _versionNote = Wrapped(string.Empty);
    private readonly Button _openWorksheet = Command(OpenWorksheetCaption);
    private readonly Expander _inputs = new() { Header = "Inputs loaded", IsExpanded = false, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel _inputRows = new() { Spacing = 1 };
    private readonly StackPanel _expectedPanel = new() { Spacing = 2 };
    private readonly StackPanel _expected = new() { Spacing = 1 };
    private readonly TextBlock _rule = Wrapped(string.Empty);
    private readonly Button _compare = Command(CompareCaption);
    private readonly StackPanel _notes = new() { Spacing = 2 };
    private readonly RadioButton _pass = new() { Content = "Pass", GroupName = "GuidedResult" };
    private readonly RadioButton _fail = new() { Content = "Fail", GroupName = "GuidedResult" };
    private readonly RadioButton _blocked = new() { Content = "Blocked", GroupName = "GuidedResult" };
    private readonly RadioButton _skipped = new() { Content = "Skipped", GroupName = "GuidedResult" };
    // The box is not cut at the 2,000 characters a recorded note can hold: a longer note is shown whole, counted below, and refused when recorded.
    private readonly TextBox _note = new() { Watermark = "Note (a recorded note holds up to 2,000 characters)", MaxLength = GuidedTestViewModel.MaxDraftNoteLength, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, MaxHeight = 140 };
    private readonly TextBlock _noteCounter = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
    private readonly TextBox _values = new() { Watermark = "Values shown", MaxLength = ManualTestRun.MaxValuesShownLength, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _record = Command(RecordCaption);
    private readonly Button _previous = Command(PreviousCaption);
    private readonly Button _next = Command(NextCaption);
    private readonly Button _nextNotRun = Command(NextNotRunCaption);
    private readonly TextBlock _recorded = Wrapped(string.Empty);
    private readonly TextBlock _status = Wrapped(string.Empty);
    private bool _rendering;
    private List<string> _shownRows = [];

    public GuidedTestView(GuidedTestViewModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        Width = PanelWidth;
        AutomationProperties.SetName(this, Heading);

        foreach (var (control, name) in new (Control, string)[]
        {
            (_runText, "Test run"), (_tally, "Run tally"), (_resumeText, "Unfinished run"), (_filter, "Step filter"), (_search, "Search steps"), (_steps, "Test steps"),
            (_title, "Step title"), (_subtitle, "Step details"), (_instructions, "Step instructions"), (_passWhen, "Step passes when"), (_versionNote, "Worksheet version note"),
            (_inputs, "Inputs loaded"), (_expected, "Expected outputs"), (_rule, "Tolerance rule"), (_notes, "Comparison notes"),
            (_pass, "Pass"), (_fail, "Fail"), (_blocked, "Blocked"), (_skipped, "Skipped"), (_note, "Result note"), (_noteCounter, "Note length"), (_values, "Values shown"),
            (_recorded, "Recorded result"), (_status, "Guided test status"),
        })
        {
            AutomationProperties.SetName(control, name);
        }

        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);

        var run = new StackPanel { Spacing = DesignTokens.SpaceXs };
        run.Children.Add(_runText);
        run.Children.Add(_tally);
        run.Children.Add(_resumeText);
        run.Children.Add(Row(_start, _resume, _finish));
        run.Children.Add(Row(_copy, _save, _deleteOlder));

        var list = new StackPanel { Spacing = DesignTokens.SpaceXs };
        list.Children.Add(_filter);
        list.Children.Add(_search);
        list.Children.Add(_steps);

        var detail = new StackPanel { Spacing = DesignTokens.SpaceXs };
        detail.Children.Add(_title);
        detail.Children.Add(_subtitle);
        detail.Children.Add(_instructions);
        detail.Children.Add(_passWhen);
        detail.Children.Add(_versionNote);
        detail.Children.Add(_openWorksheet);
        _inputs.Content = new ScrollViewer { Content = _inputRows, MaxHeight = 170 };
        detail.Children.Add(_inputs);

        _expectedPanel.Children.Add(Caption("Expected outputs"));
        _expectedPanel.Children.Add(_expected);
        _expectedPanel.Children.Add(_rule);
        _expectedPanel.Children.Add(_compare);
        _expectedPanel.Children.Add(_notes);
        detail.Children.Add(_expectedPanel);

        var outcomes = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var radio in new[] { _pass, _fail, _blocked, _skipped })
        {
            radio.Margin = new Thickness(0, 0, DesignTokens.SpaceMd, 0);
            outcomes.Children.Add(radio);
        }

        var result = new StackPanel { Spacing = DesignTokens.SpaceXs };
        result.Children.Add(outcomes);
        result.Children.Add(_note);
        result.Children.Add(_noteCounter);
        result.Children.Add(_values);
        result.Children.Add(Row(_record));
        result.Children.Add(_recorded);
        result.Children.Add(Row(_previous, _next, _nextNotRun));

        var stack = new StackPanel { Spacing = DesignTokens.SpaceSm, Margin = DesignTokens.PanelPadding };
        stack.Children.Add(new TextBlock { Text = Heading, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 4 });
        stack.Children.Add(Caption("Walks the owner test pack. Each worked example opens in the worksheet beside this panel with its inputs loaded; nothing here changes a calculation or Evidence."));
        stack.Children.Add(Section("Run", run));
        stack.Children.Add(Section("Steps", list));
        stack.Children.Add(Section("This step", detail));
        stack.Children.Add(Section("Your result", result));
        stack.Children.Add(_status);
        Content = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        // Every handler that does I/O goes through the model's guard: an exception out of an async button handler has nobody to catch it and ends the
        // application, so a failure (a store that cannot be read, a file that cannot be saved) becomes a sentence in the status line instead.
        _start.Click += async (_, _) => await _model.StartNewAsync().ConfigureAwait(true);
        _resume.Click += async (_, _) => await _model.ResumeAsync().ConfigureAwait(true);
        _finish.Click += async (_, _) => await _model.FinishAsync().ConfigureAwait(true);
        _copy.Click += async (_, _) => await _model.RunGuardedAsync(CopyAsync, "copy the results sheet").ConfigureAwait(true);
        _save.Click += async (_, _) => await _model.RunGuardedAsync(SaveAsync, "save the results sheet").ConfigureAwait(true);
        _deleteOlder.Click += async (_, _) => await _model.DeleteOlderRunsAsync().ConfigureAwait(true);
        _openWorksheet.Click += (_, _) => Guard(_model.OpenWorksheet, "open the worksheet");
        _compare.Click += (_, _) => Guard(_model.AutoCompare, "compare with the form");
        _record.Click += async (_, _) => await _model.RecordAsync().ConfigureAwait(true);
        _previous.Click += (_, _) => _model.Previous();
        _next.Click += (_, _) => _model.Next();
        _nextNotRun.Click += (_, _) => _model.NextNotRun();
        _filter.SelectionChanged += (_, _) => { if (!_rendering && _filter.SelectedItem is string filter) _model.SetFilter(filter); };
        _search.TextChanged += (_, _) => { if (!_rendering) _model.SetSearch(_search.Text); };
        _steps.SelectionChanged += (_, _) => { if (!_rendering && _steps.SelectedIndex is >= 0 and var index && index < _model.Rows.Count) _model.Select(_model.Rows[index].Id); };
        _note.TextChanged += (_, _) => { if (!_rendering) _model.SetDraftNote(_note.Text); };
        _values.TextChanged += (_, _) => { if (!_rendering) _model.SetDraftValues(_values.Text); };

        foreach (var (radio, outcome) in new[] { (_pass, ManualTestOutcome.Pass), (_fail, ManualTestOutcome.Fail), (_blocked, ManualTestOutcome.Blocked), (_skipped, ManualTestOutcome.Skipped) })
        {
            var chosen = outcome;
            radio.IsCheckedChanged += (_, _) => { if (!_rendering && radio.IsChecked == true) _model.SetDraftOutcome(chosen); };
        }

        _model.Changed += Render;
        _rule.Text = _model.Rule;
        Render();
    }

    /// <summary>The view model, for the coordinator and the tests.</summary>
    public GuidedTestViewModel Model => _model;

    // ------------------------------------------------------------------

    private void Render()
    {
        _rendering = true;

        try
        {
            var model = _model;
            _runText.Text = model.RunText;
            _tally.Text = model.TallyText;
            _tally.IsVisible = model.Run is not null;
            _resumeText.Text = model.ResumeText;
            _resumeText.IsVisible = model.Resumable is not null;
            _resume.IsVisible = model.Resumable is not null;
            _finish.IsEnabled = model.CanFinish;
            _copy.IsEnabled = _save.IsEnabled = model.Run is not null;
            _deleteOlder.IsVisible = model.CanDeleteOlderRuns;

            if (!model.Filters.SequenceEqual(_filter.Items.OfType<string>(), StringComparer.Ordinal))
                _filter.ItemsSource = model.Filters.ToList();

            _filter.SelectedItem = model.Filter;

            // The search is trimmed by the model; writing it back while the person is typing would eat the space between two words.
            if (!string.Equals((_search.Text ?? string.Empty).Trim(), model.Search, StringComparison.Ordinal))
                _search.Text = model.Search;

            // A keystroke in the note raises Changed too; the list is only rebuilt when its rows are different.
            var rows = model.Rows.Select(r => $"{r.Result,-8}  {r.Label}").ToList();

            if (!rows.SequenceEqual(_shownRows, StringComparer.Ordinal))
            {
                _shownRows = rows;
                _steps.ItemsSource = rows;
            }

            _steps.SelectedIndex = model.CurrentIndex;

            RenderStep(model);
            RenderResult(model);
            _status.Text = model.Status;
        }
        finally
        {
            _rendering = false;
        }
    }

    private void RenderStep(GuidedTestViewModel model)
    {
        var step = model.Current;
        _title.Text = step?.Title ?? "No step matches the filter.";
        _subtitle.Text = step is null ? string.Empty : Describe(step);
        _instructions.Children.Clear();

        foreach (var line in step?.Instructions ?? [])
            _instructions.Children.Add(Wrapped(line));

        _passWhen.Text = step?.PassWhen is { } pass ? "Pass when: " + pass : string.Empty;
        _passWhen.IsVisible = _passWhen.Text.Length > 0;
        _versionNote.Text = model.VersionNote;
        _versionNote.IsVisible = model.VersionNote.Length > 0;

        var example = model.CurrentExample;
        _openWorksheet.IsVisible = example is not null;
        _expectedPanel.IsVisible = example is not null;
        _inputs.IsVisible = example is not null;
        _inputRows.Children.Clear();

        foreach (var input in example?.Inputs ?? [])
            _inputRows.Children.Add(Mono($"{input.Symbol} = {input.Value}{(input.Unit.Length > 0 ? " " + input.Unit : string.Empty)}"));

        _expected.Children.Clear();

        foreach (var line in model.ExpectedLines)
            _expected.Children.Add(ExpectedRow(line));

        _compare.IsVisible = example?.TemplateId is not null;
        _notes.Children.Clear();

        foreach (var note in model.ComparisonNotes)
            _notes.Children.Add(Wrapped(note));
    }

    private void RenderResult(GuidedTestViewModel model)
    {
        _pass.IsChecked = model.DraftOutcome == ManualTestOutcome.Pass;
        _fail.IsChecked = model.DraftOutcome == ManualTestOutcome.Fail;
        _blocked.IsChecked = model.DraftOutcome == ManualTestOutcome.Blocked;
        _skipped.IsChecked = model.DraftOutcome == ManualTestOutcome.Skipped;

        if (!string.Equals(_note.Text ?? string.Empty, model.DraftNote, StringComparison.Ordinal))
            _note.Text = model.DraftNote;

        if (!string.Equals(_values.Text ?? string.Empty, model.DraftValues, StringComparison.Ordinal))
            _values.Text = model.DraftValues;

        var canRecord = model.CanRecord && model.Current is not null;
        _record.IsEnabled = canRecord;
        _noteCounter.Text = model.NoteCounterText;
        _noteCounter.Foreground = model.NoteTooLong ? Brushes.IndianRed : null;

        foreach (var control in new Control[] { _pass, _fail, _blocked, _skipped, _note, _values })
            control.IsEnabled = canRecord;

        _recorded.Text = model.Recorded is { } recorded
            ? $"Recorded: {recorded.Outcome} at {recorded.RecordedUtc.UtcDateTime:yyyy-MM-dd HH:mm} UTC."
            : model.Run is null ? "Start or resume a run to record results." : "No result recorded for this step yet.";
        _previous.IsEnabled = _next.IsEnabled = _nextNotRun.IsEnabled = model.Rows.Count > 0;
    }

    private static string Describe(GuidedTestStep step) => step.Kind == GuidedStepKind.Example
        ? $"Runbook section {step.Section} · {step.Discipline} · {step.TemplateId}@{step.TemplateVersion} · example {step.ExampleId}"
        : $"Runbook section {step.Section} · {step.Kind}";

    /// <summary>One expected output over two lines: the symbol, the expected value and its allowance and the verdict; then, after a comparison, what the form shows.</summary>
    private static Control ExpectedRow(GuidedExpectedLine line)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXs) };
        var verdict = new TextBlock
        {
            Text = line.Outcome switch
            {
                GuidedCompareOutcome.Match => "agrees",
                GuidedCompareOutcome.Mismatch => "differs",
                GuidedCompareOutcome.NotComparable => "your call",
                _ => string.Empty,
            },
            Foreground = line.Outcome switch
            {
                GuidedCompareOutcome.Match => Brushes.SeaGreen,
                GuidedCompareOutcome.Mismatch => Brushes.IndianRed,
                _ => Brushes.DarkOrange,
            },
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeCaption,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var expected = Mono($"{line.Expected}   ({line.Allowance})");
        AutomationProperties.SetName(row, $"Expected {line.Symbol}");

        var cells = new List<(Control Cell, int Column, int RowIndex)> { (Mono(line.Symbol), 0, 0), (expected, 1, 0), (verdict, 2, 0) };

        if (line.Shown is not null)
            cells.Add((Caption($"form shows {line.Shown}{(line.Applied is null ? string.Empty : $"; allowance applied {line.Applied}")}"), 1, 1));

        foreach (var (cell, column, rowIndex) in cells)
        {
            Grid.SetColumn(cell, column);
            Grid.SetRow(cell, rowIndex);
            row.Children.Add(cell);
        }

        return row;
    }

    private void Guard(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _model.ReportFailure(what, failure);
        }
    }

    private async Task CopyAsync()
    {
        var sheet = _model.ExportMarkdown();
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

        if (sheet is null || clipboard is null)
        {
            _model.ReportStatus(sheet is null ? "Start or resume a run first." : "No clipboard is available here.");
            return;
        }

        await clipboard.SetTextAsync(sheet).ConfigureAwait(true);
        _model.ReportStatus("The results sheet is on the clipboard.");
    }

    private async Task SaveAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (_model.ExportMarkdown() is null || storage is null || !storage.CanSave)
        {
            _model.ReportStatus(_model.ExportMarkdown() is null ? "Start or resume a run first." : "No file picker is available here; copy the sheet instead.");
            return;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the guided test results sheet",
            SuggestedFileName = _model.SuggestedFileName,
            DefaultExtension = "md",
            FileTypeChoices = [new FilePickerFileType("Markdown") { Patterns = ["*.md"] }],
        }).ConfigureAwait(true);

        if (file is null)
            return;

        // A file on disk is written through a temporary file and moved over the target, so a failure part-way leaves the earlier file whole; a place the
        // picker gives no path for is written through its own stream.
        await _model.SaveResultsAsync(async sheet =>
        {
            if (file.TryGetLocalPath() is { Length: > 0 } path)
            {
                await ManualTestMarkdown.WriteFileAsync(path, sheet).ConfigureAwait(true);
                return;
            }

            await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
            await stream.WriteAsync(new UTF8Encoding(false).GetBytes(sheet)).ConfigureAwait(true);
        }, file.Name).ConfigureAwait(true);
    }

    // ------------------------------------------------------------------

    private static Button Command(string caption)
    {
        var button = new Button { Content = caption, MinHeight = DesignTokens.MinControlSize, Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceXs) };
        AutomationProperties.SetName(button, caption);
        return button;
    }

    private static WrapPanel Row(params Control[] controls)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var control in controls)
            row.Children.Add(control);

        return row;
    }

    private static Expander Section(string title, Control content) => new() { Header = title, IsExpanded = true, Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };

    private static TextBlock Wrapped(string text) => new() { Text = text, FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };

    private static TextBlock Caption(string text) => new() { Text = text, FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };

    private static TextBlock Mono(string text) => new() { Text = text, FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
}
