using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Calc.Engine;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// `v0.26.0` (ADR-0165, §3.8a): the one form every worksheet calculator is
/// entered through. Left: the catalogue (discipline → template, with
/// version and hash). Right: the generated inputs table (value, unit picker,
/// provenance, tooltip with symbol and description, inline validation), the
/// results panel headed by the governing check's PASS/FAIL bar, "Show
/// working" revealing formula → substitution → result exactly as printed,
/// applicability notices, and the lifecycle actions. Every input change
/// recalculates at once; while a recalculation is pending the results carry
/// a visible stale marker. An issued instance is read-only under an ISSUED
/// watermark. The view raises events and renders what it is given; the
/// coordinator talks to the service.
/// </summary>
public sealed class CalcSheetView : UserControl
{
    public const string Heading = "Calculation worksheets";
    public const string CatalogueAutomationName = "Worksheet catalogue";
    public const string SaveCaption = "Save as calculation";
    public const string CheckCaption = "Record check";
    public const string IssueCaption = "Issue";
    public const string ReviseCaption = "Revise";
    public const string ShowWorkingCaption = "Show working";
    public const string ExportCsvCaption = "Export inputs (CSV)";
    public const string StaleMarker = "Recalculating…";
    public const string PickGuidance = "Choose a worksheet from the catalogue on the left. Its inputs, with their units, sources and limits, appear here.";

    private static readonly string[] ProvenanceKinds = ["manual", "drawing", "spec", "assumption", "evidence", "reference"];

    private readonly TreeView _catalogue = new() { MinHeight = 200 };
    private readonly TextBlock _title = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 4, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _watermark = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeTitle, Foreground = Brushes.IndianRed, IsVisible = false };
    private readonly TextBlock _reference = Caption(string.Empty);
    private readonly TextBlock _scope = Caption(PickGuidance);
    private readonly TextBox _nameBox = Input(string.Empty, "What to call this calculation");
    private readonly StackPanel _form = new() { Spacing = DesignTokens.SpaceSm };
    private readonly TextBlock _stale = new() { Text = StaleMarker, FontSize = DesignTokens.FontSizeCaption, Foreground = Brushes.DarkOrange, IsVisible = false };
    private readonly TextBlock _governing = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _utilisation = new() { Minimum = 0, Maximum = 1.5, Height = 10, IsVisible = false };
    private readonly Button _governingLink = new() { Content = "Go to step", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly StackPanel _results = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _checks = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _notices = new() { Spacing = DesignTokens.SpaceXs };
    private readonly ToggleButton _showWorking = new() { Content = ShowWorkingCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly StackPanel _working = new() { Spacing = DesignTokens.SpaceXs, IsVisible = false };
    private readonly TextBlock _problems = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, MaxWidth = 640, Foreground = Brushes.IndianRed };
    private readonly TextBlock _status = Caption(string.Empty);
    private readonly Button _saveButton = new() { Content = SaveCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _checkButton = new() { Content = CheckCaption, MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _issueButton = new() { Content = IssueCaption, MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _reviseButton = new() { Content = ReviseCaption, MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _exportButton = new() { Content = ExportCsvCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Dictionary<string, FieldControls> _fields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StackPanel> _stepRows = new(StringComparer.Ordinal);
    private bool _loadingForm;
    private string? _governingStep;

    private sealed record FieldControls(CalcInputSpec Spec, TextBox? Text, ComboBox? Unit, ComboBox? Choice, CheckBox? Flag, ComboBox Provenance, TextBox ProvenanceRef, TextBlock Problem, Grid Row);

    public CalcSheetView()
    {
        AutomationProperties.SetName(this, Heading);
        AutomationProperties.SetName(_catalogue, CatalogueAutomationName);
        AutomationProperties.SetName(_nameBox, "Calculation name");
        AutomationProperties.SetName(_governing, "Governing check");
        AutomationProperties.SetLiveSetting(_governing, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_stale, "Stale marker");
        AutomationProperties.SetName(_watermark, "Status watermark");
        AutomationProperties.SetName(_problems, "Input problems");
        AutomationProperties.SetLiveSetting(_problems, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_status, "Worksheet status");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_saveButton, SaveCaption);
        AutomationProperties.SetName(_checkButton, CheckCaption);
        AutomationProperties.SetName(_issueButton, IssueCaption);
        AutomationProperties.SetName(_reviseButton, ReviseCaption);
        AutomationProperties.SetName(_showWorking, ShowWorkingCaption);
        AutomationProperties.SetName(_exportButton, ExportCsvCaption);
        AutomationProperties.SetName(_working, "Working");
        AutomationProperties.SetName(_results, "Results");

        _catalogue.SelectionChanged += (_, _) => OnCatalogueSelectionChanged();
        _saveButton.Click += (_, _) => SaveRequested?.Invoke();
        _checkButton.Click += (_, _) => CheckRequested?.Invoke();
        _issueButton.Click += (_, _) => IssueRequested?.Invoke();
        _reviseButton.Click += (_, _) => ReviseRequested?.Invoke();
        _exportButton.Click += (_, _) => ExportRequested?.Invoke();
        _showWorking.IsCheckedChanged += (_, _) => _working.IsVisible = _showWorking.IsChecked == true;
        _governingLink.Click += (_, _) => RevealGoverningStep();

        Content = BuildLayout();
    }

    public event Action<CalcCatalogueEntry>? TemplateSelected;

    /// <summary>Raised on every input change with the current form; the coordinator answers with <see cref="ShowRun"/>.</summary>
    public event Action<CalcInputSet>? InputsChanged;

    public event Action? SaveRequested;

    public event Action? CheckRequested;

    public event Action? IssueRequested;

    public event Action? ReviseRequested;

    public event Action? ExportRequested;

    public CalcCatalogueEntry? SelectedTemplate { get; private set; }

    public CalcInstance? CurrentInstance { get; private set; }

    public CalcRunResult? LastRun { get; private set; }

    public IReadOnlyList<CalcCatalogueEntry> Catalogue { get; private set; } = [];

    public string? CalculationName => string.IsNullOrWhiteSpace(_nameBox.Text) ? null : _nameBox.Text;

    public bool IsReadOnly { get; private set; }

    public bool IsWorkingShown => _working.IsVisible;

    public bool IsStale => _stale.IsVisible;

    public void ShowCatalogue(IReadOnlyList<CalcCatalogueEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Catalogue = entries;
        _catalogue.Items.Clear();

        foreach (var group in entries.GroupBy(e => e.Template.Discipline).OrderBy(g => g.Key))
        {
            var node = new TreeViewItem { Header = DisciplineLabel(group.Key), IsExpanded = true };
            AutomationProperties.SetName(node, DisciplineLabel(group.Key));

            foreach (var entry in group.OrderBy(e => e.Template.Title, StringComparer.Ordinal))
            {
                var leaf = new TreeViewItem { Header = $"{entry.Template.Title}  ·  v{entry.Template.Version} {entry.ShortHash[..8]}", Tag = entry };
                AutomationProperties.SetName(leaf, entry.Template.Title);
                ToolTip.SetTip(leaf, $"{entry.Reference} — hash {entry.ShortHash}\n{entry.Template.Scope}");
                node.Items.Add(leaf);
            }

            _catalogue.Items.Add(node);
        }

        if (entries.Count == 0)
            _scope.Text = "No worksheets are registered in this build.";
    }

    public void SelectTemplate(string reference)
    {
        foreach (var node in _catalogue.Items.OfType<TreeViewItem>())
        {
            foreach (var leaf in node.Items.OfType<TreeViewItem>())
            {
                if (leaf.Tag is CalcCatalogueEntry e && (e.Reference == reference || e.Template.Id == reference))
                {
                    _catalogue.SelectedItem = leaf;
                    return;
                }
            }
        }
    }

    /// <summary>Builds the form for a template, seeding it from an instance (its inputs) or the template's defaults.</summary>
    public void ShowTemplate(CalcCatalogueEntry entry, CalcInstance? instance = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        SelectedTemplate = entry;
        CurrentInstance = instance;
        IsReadOnly = instance is { IsImmutable: true };
        _loadingForm = true;

        _title.Text = entry.Template.Title;
        _reference.Text = $"{entry.Reference}  ·  hash {entry.ShortHash}  ·  {entry.Template.ClauseRefs.Count} reference(s)";
        _scope.Text = entry.Template.Scope;
        _nameBox.Text = instance?.Name ?? string.Empty;
        _nameBox.IsEnabled = !IsReadOnly;
        _watermark.Text = instance is null ? string.Empty : instance.Status.ToString().ToUpperInvariant();
        _watermark.IsVisible = instance is not null;
        _watermark.Foreground = instance?.Status == CalcInstanceStatus.Issued ? Brushes.SeaGreen : instance?.Status == CalcInstanceStatus.Superseded ? Brushes.Gray : Brushes.DarkOrange;
        _form.Children.Clear();
        _fields.Clear();
        _problems.Text = string.Empty;
        _status.Text = IsReadOnly ? $"This calculation is {instance!.Status}; it is read-only. Revise it to start a new revision." : string.Empty;

        var seed = instance is null ? CalcTemplateHasher.DefaultInputs(entry.Template) : new CalcInputSet(instance.Inputs);
        string? lastGroup = null;

        foreach (var spec in entry.Template.Inputs)
        {
            if (spec.Kind is CalcInputKind.Table or CalcInputKind.Vector)
                continue;

            if (spec.Group is { Length: > 0 } g && g != lastGroup)
            {
                _form.Children.Add(new TextBlock { Text = g, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody, Margin = DesignTokens.SectionMargin });
                lastGroup = g;
            }

            _form.Children.Add(BuildField(spec, seed.Find(spec.Symbol)));
        }

        _loadingForm = false;
        UpdateActions();
        RaiseInputsChanged();
    }

    /// <summary>Shows a run: governing bar, results, checks, notices, working. Clears the stale marker.</summary>
    public void ShowRun(CalcRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        LastRun = result;
        _stale.IsVisible = false;
        _results.Children.Clear();
        _checks.Children.Clear();
        _notices.Children.Clear();
        _working.Children.Clear();
        _stepRows.Clear();
        _governingStep = null;

        foreach (var notice in result.Notices.Where(n => n.RuleId.StartsWith("input.", StringComparison.Ordinal)))
        {
            var symbol = notice.RuleId["input.".Length..];
            var key = symbol.Contains('[', StringComparison.Ordinal) ? symbol[..symbol.IndexOf('[', StringComparison.Ordinal)] : symbol;

            if (_fields.TryGetValue(key, out var field))
            {
                field.Problem.Text = notice.Message;
                field.Problem.Foreground = notice.Severity == CalcNoticeSeverity.Block ? Brushes.IndianRed : Brushes.DarkOrange;
                field.Problem.IsVisible = true;
            }
        }

        if (result.Governing is { } governing)
        {
            _governing.Text = $"{(governing.Passed ? "PASS" : "FAIL")} — {governing.Name}: utilisation {FormatUtilisation(governing.Utilisation)}";
            _governing.Foreground = governing.Passed ? Brushes.SeaGreen : Brushes.IndianRed;
            _utilisation.Value = double.IsFinite(governing.Utilisation) ? Math.Min(governing.Utilisation, 1.5) : 1.5;
            _utilisation.Foreground = governing.Passed ? Brushes.SeaGreen : Brushes.IndianRed;
            _utilisation.IsVisible = true;
            _governingStep = governing.StepSymbol;
            _governingLink.IsVisible = governing.StepSymbol is not null;
        }
        else if (result.IsBlocked)
        {
            _governing.Text = "Blocked — " + result.Blocks[0].Message;
            _governing.Foreground = Brushes.IndianRed;
            _utilisation.IsVisible = false;
            _governingLink.IsVisible = false;
        }
        else
        {
            _governing.Text = result.Outputs.Count == 0 ? string.Empty : "No pass/fail check in this calculation.";
            _governing.Foreground = Brushes.Gray;
            _utilisation.IsVisible = false;
            _governingLink.IsVisible = false;
        }

        foreach (var o in result.Outputs)
            _results.Children.Add(Readout(o.Symbol, o.ValueText, o.Description));

        foreach (var c in result.Checks.OrderByDescending(c => c.Utilisation))
            _checks.Children.Add(Readout(c.Passed ? "PASS" : "FAIL", $"{c.Name}: {c.DemandText} vs {c.CapacityText} (u = {FormatUtilisation(c.Utilisation)})", c.Clause));

        foreach (var n in result.Blocks)
            _notices.Children.Add(new TextBlock { Text = $"Block — {n.Message}", Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody });

        foreach (var n in result.Warnings)
            _notices.Children.Add(new TextBlock { Text = $"Warning — {n.Message}", Foreground = Brushes.DarkOrange, TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody });

        foreach (var s in result.Steps.Where(s => s.Kind != CalcStepKind.Input))
        {
            var row = new StackPanel { Spacing = 0, Margin = new Thickness(0, DesignTokens.SpaceXs) };
            row.Children.Add(Mono($"[{s.Ordinal}] {s.Symbol} = {s.Formula}{(s.Clause is { Length: > 0 } ? $"    « {s.Clause}" : string.Empty)}"));

            if (!string.Equals(s.Substitution, s.Formula, StringComparison.Ordinal))
                row.Children.Add(Mono($"      = {s.Substitution}"));

            row.Children.Add(Mono($"      = {s.ResultText}"));

            if (s.Description is { Length: > 0 })
                row.Children.Add(Caption("        " + s.Description));

            AutomationProperties.SetName(row, $"Step {s.Symbol}");
            _working.Children.Add(row);
            _stepRows[s.Symbol] = row;
        }

        _saveButton.IsEnabled = !IsReadOnly && SelectedTemplate is not null && !result.IsBlocked;
        _exportButton.IsEnabled = SelectedTemplate is not null;
        UpdateActions();
    }

    public void ShowInstance(CalcInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        CurrentInstance = instance;
        IsReadOnly = instance.IsImmutable;
        _nameBox.Text = instance.Name;
        _nameBox.IsEnabled = !IsReadOnly;
        _watermark.Text = instance.Status.ToString().ToUpperInvariant();
        _watermark.IsVisible = true;
        _watermark.Foreground = instance.Status == CalcInstanceStatus.Issued ? Brushes.SeaGreen : instance.Status == CalcInstanceStatus.Superseded ? Brushes.Gray : Brushes.DarkOrange;

        foreach (var f in _fields.Values)
            SetEnabled(f, !IsReadOnly);

        UpdateActions();
    }

    public void ShowStatus(string message) => _status.Text = message;

    public void ShowProblems(IEnumerable<string> problems) => _problems.Text = string.Join(Environment.NewLine, problems);

    /// <summary>Reads the form into an input set, every value as typed with its unit and provenance. Unparseable numbers become NaN so validation names them.</summary>
    public CalcInputSet ReadForm()
    {
        var set = new CalcInputSet();

        foreach (var (symbol, f) in _fields)
        {
            var provenance = ReadProvenance(f);

            switch (f.Spec.Kind)
            {
                case CalcInputKind.Quantity:
                case CalcInputKind.Number:
                {
                    var text = f.Text!.Text?.Trim() ?? string.Empty;

                    if (text.Length == 0)
                    {
                        if (!f.Spec.IsOptional && f.Spec.Default is null)
                            set.Set(new CalcInputValue(symbol, Number: double.NaN, Unit: f.Unit?.SelectedItem as string, Provenance: provenance));

                        break;
                    }

                    var number = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : double.NaN;
                    set.Set(new CalcInputValue(symbol, Number: number, Unit: f.Unit?.SelectedItem as string, Provenance: provenance));
                    break;
                }

                case CalcInputKind.Choice:
                    set.Set(new CalcInputValue(symbol, Text: f.Choice!.SelectedItem as string, Provenance: provenance));
                    break;

                case CalcInputKind.Text:
                    set.Set(new CalcInputValue(symbol, Text: f.Text!.Text ?? string.Empty, Provenance: provenance));
                    break;

                case CalcInputKind.Boolean:
                    set.Set(new CalcInputValue(symbol, Flag: f.Flag!.IsChecked == true, Provenance: provenance));
                    break;
            }
        }

        return set;
    }

    /// <summary>Sets one field as a test or an import would: the value text, the unit, the choice or the flag.</summary>
    public void SetField(string symbol, string? text = null, string? unit = null, string? choice = null, bool? flag = null, string? provenance = null, string? reference = null)
    {
        if (!_fields.TryGetValue(symbol, out var f))
            return;

        _loadingForm = true;

        if (text is not null && f.Text is not null)
            f.Text.Text = text;

        if (choice is not null && f.Choice is not null)
            f.Choice.SelectedItem = choice;

        if (flag is not null && f.Flag is not null)
            f.Flag.IsChecked = flag;

        if (provenance is not null)
            f.Provenance.SelectedItem = provenance;

        if (reference is not null)
            f.ProvenanceRef.Text = reference;

        _loadingForm = false;

        // A unit change converts the value and raises the change itself.
        if (unit is not null && f.Unit is not null && !string.Equals(f.Unit.SelectedItem as string, unit, StringComparison.Ordinal))
        {
            f.Unit.SelectedItem = unit;
            return;
        }

        RaiseInputsChanged();
    }

    public Control? FieldControl(string symbol) => _fields.TryGetValue(symbol, out var f) ? (Control?)f.Text ?? f.Choice ?? (Control?)f.Flag : null;

    public ComboBox? UnitPicker(string symbol) => _fields.TryGetValue(symbol, out var f) ? f.Unit : null;

    public string? FieldProblem(string symbol) => _fields.TryGetValue(symbol, out var f) && f.Problem.IsVisible ? f.Problem.Text : null;

    public string GoverningText => _governing.Text ?? string.Empty;

    public IReadOnlyList<string> WorkingLines => _working.Children.OfType<StackPanel>().SelectMany(p => p.Children.OfType<TextBlock>()).Select(t => t.Text ?? string.Empty).ToList();

    public void ToggleWorking(bool shown) => _showWorking.IsChecked = shown;

    // ------------------------------------------------------------------

    private void OnCatalogueSelectionChanged()
    {
        if (_catalogue.SelectedItem is TreeViewItem { Tag: CalcCatalogueEntry entry })
        {
            ShowTemplate(entry);
            TemplateSelected?.Invoke(entry);
        }
    }

    private void RaiseInputsChanged()
    {
        if (_loadingForm || SelectedTemplate is null)
            return;

        foreach (var f in _fields.Values)
            f.Problem.IsVisible = false;

        _stale.IsVisible = true;
        InputsChanged?.Invoke(ReadForm());
    }

    private void UpdateActions()
    {
        var instance = CurrentInstance;
        _saveButton.Content = instance is null ? SaveCaption : "Save changes";
        _saveButton.IsEnabled = !IsReadOnly && SelectedTemplate is not null && LastRun is { IsBlocked: false };
        _checkButton.IsVisible = instance is { Status: CalcInstanceStatus.Draft };
        _issueButton.IsVisible = instance is { Status: CalcInstanceStatus.Checked };
        _reviseButton.IsVisible = instance is { Status: CalcInstanceStatus.Issued };
    }

    private void RevealGoverningStep()
    {
        if (_governingStep is null || !_stepRows.TryGetValue(_governingStep, out var row))
            return;

        _showWorking.IsChecked = true;
        row.Background = new SolidColorBrush(Color.FromArgb(40, 64, 162, 206));
        row.BringIntoView();
    }

    private Control BuildField(CalcInputSpec spec, CalcInputValue? seed)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("200,160,90,120,*") };
        var label = new TextBlock { Text = $"{spec.Symbol} — {spec.Description}", FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        var tip = $"{spec.Symbol}: {spec.Description}\nValid range: {spec.RangeText}{(spec.IsOptional ? "\nOptional" : string.Empty)}";
        ToolTip.SetTip(label, tip);
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        TextBox? text = null;
        ComboBox? unit = null;
        ComboBox? choice = null;
        CheckBox? flag = null;

        switch (spec.Kind)
        {
            case CalcInputKind.Quantity:
            case CalcInputKind.Number:
            case CalcInputKind.Text:
            {
                var initial = seed?.Number is { } n ? n.ToString("R", CultureInfo.InvariantCulture) : seed?.Text ?? (spec.Default is { } d ? d.ToString("R", CultureInfo.InvariantCulture) : string.Empty);
                text = Input(initial, spec.Kind == CalcInputKind.Text ? "text" : "number");
                text.TextChanged += (_, _) => RaiseInputsChanged();
                AutomationProperties.SetName(text, spec.Symbol);
                ToolTip.SetTip(text, tip);
                Grid.SetColumn(text, 1);
                row.Children.Add(text);

                if (spec.Kind == CalcInputKind.Quantity)
                {
                    unit = new ComboBox { MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch };
                    var units = UnitChoices(spec);
                    unit.ItemsSource = units;
                    unit.SelectedItem = seed?.Unit is { } u && units.Contains(u) ? u : spec.DefaultUnit;
                    unit.Tag = unit.SelectedItem;
                    unit.SelectionChanged += (_, _) => OnUnitChanged(spec.Symbol);
                    AutomationProperties.SetName(unit, $"{spec.Symbol} unit");
                    Grid.SetColumn(unit, 2);
                    row.Children.Add(unit);
                }

                break;
            }

            case CalcInputKind.Choice:
            {
                choice = new ComboBox { MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = spec.Choices ?? [] };
                choice.SelectedItem = seed?.Text ?? spec.DefaultChoice;
                choice.SelectionChanged += (_, _) => RaiseInputsChanged();
                AutomationProperties.SetName(choice, spec.Symbol);
                ToolTip.SetTip(choice, tip);
                Grid.SetColumn(choice, 1);
                Grid.SetColumnSpan(choice, 2);
                row.Children.Add(choice);
                break;
            }

            case CalcInputKind.Boolean:
            {
                flag = new CheckBox { IsChecked = seed?.Flag ?? (spec.Default is { } d && d != 0), MinHeight = DesignTokens.MinControlSize };
                flag.IsCheckedChanged += (_, _) => RaiseInputsChanged();
                AutomationProperties.SetName(flag, spec.Symbol);
                ToolTip.SetTip(flag, tip);
                Grid.SetColumn(flag, 1);
                row.Children.Add(flag);
                break;
            }
        }

        var provenance = new ComboBox { MinHeight = DesignTokens.MinControlSize, ItemsSource = ProvenanceKinds, HorizontalAlignment = HorizontalAlignment.Stretch };
        provenance.SelectedItem = ProvenanceLabel(seed?.Provenance?.Kind ?? CalcProvenanceKind.Manual);
        provenance.SelectionChanged += (_, _) => RaiseInputsChanged();
        AutomationProperties.SetName(provenance, $"{spec.Symbol} source");
        ToolTip.SetTip(provenance, "Where this value comes from. A source other than 'manual' is needed to issue.");
        Grid.SetColumn(provenance, 3);
        row.Children.Add(provenance);

        var reference = Input(seed?.Provenance?.Reference ?? string.Empty, "reference / revision");
        reference.TextChanged += (_, _) => RaiseInputsChanged();
        AutomationProperties.SetName(reference, $"{spec.Symbol} source reference");
        Grid.SetColumn(reference, 4);
        row.Children.Add(reference);

        var problem = new TextBlock { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, IsVisible = false, Foreground = Brushes.DarkOrange };
        AutomationProperties.SetName(problem, $"{spec.Symbol} problem");
        var block = new StackPanel { Spacing = 0 };
        block.Children.Add(row);
        block.Children.Add(problem);

        var controls = new FieldControls(spec, text, unit, choice, flag, provenance, reference, problem, row);
        _fields[spec.Symbol] = controls;
        SetEnabled(controls, !IsReadOnly);
        return block;
    }

    /// <summary>A unit change converts the value so the physical quantity is unchanged (§3.8a: values convert on change; the engine stores SI).</summary>
    private void OnUnitChanged(string symbol)
    {
        if (_loadingForm || !_fields.TryGetValue(symbol, out var f) || f.Text is null || f.Unit is null)
            return;

        var previous = f.Unit.Tag as string;
        var next = f.Unit.SelectedItem as string;
        f.Unit.Tag = next;

        if (previous is null || next is null || previous == next)
        {
            RaiseInputsChanged();
            return;
        }

        if (double.TryParse(f.Text.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(previous, out var from, out _)
            && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(next, out var to, out _)
            && from.Dimension == to.Dimension)
        {
            _loadingForm = true;
            f.Text.Text = Tempest.Calc.Engine.Values.CalcValue.FormatNumber(to.FromSi(from.ToSi(value)), 8);
            _loadingForm = false;
        }

        RaiseInputsChanged();
    }

    private static IReadOnlyList<string> UnitChoices(CalcInputSpec spec)
    {
        var dimension = spec.Dimension;
        var candidates = new List<string> { spec.DefaultUnit };

        foreach (var symbol in CommonUnits)
        {
            if (symbol != spec.DefaultUnit && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(symbol, out var u, out _) && u.Dimension == dimension)
                candidates.Add(symbol);
        }

        return candidates;
    }

    private static readonly string[] CommonUnits =
    [
        "mm", "cm", "m", "in", "ft", "µm", "mm^2", "cm^2", "m^2", "in^2", "mm^3", "m^3", "L", "mm^4", "cm^4", "m^4", "in^4", "mm^3", "cm^3",
        "N", "kN", "MN", "lbf", "kip", "Pa", "kPa", "MPa", "GPa", "bar", "psi", "ksi", "N/mm^2", "N*m", "kN*m", "lbf*ft", "kg", "g", "t", "lb",
        "s", "min", "h", "K", "degC", "degF", "W", "kW", "hp", "J", "kJ", "kWh", "A", "V", "kV", "Ω", "mΩ", "Hz", "rpm", "rad/s", "m/s", "mm/s", "m/s^2",
        "kg/m^3", "Pa*s", "mPa*s", "cP", "m^2/s", "cSt", "L/s", "L/min", "m^3/h", "m^3/s", "cfm", "gpm", "kg/s", "kg/h", "W/(m*K)", "W/(m^2*K)", "J/(kg*K)",
        "kg*m^2", "N/m", "kN/m", "N/mm", "deg", "rad", "%", "1/K", "mm/m", "Ω/km", "mV/A/m", "kJ/kg", "g/kg",
    ];

    private static CalcProvenance ReadProvenance(FieldControls f)
    {
        var kind = (f.Provenance.SelectedItem as string) switch
        {
            "drawing" => CalcProvenanceKind.Drawing,
            "spec" => CalcProvenanceKind.Specification,
            "assumption" => CalcProvenanceKind.Assumption,
            "evidence" => CalcProvenanceKind.Evidence,
            "reference" => CalcProvenanceKind.Reference,
            _ => CalcProvenanceKind.Manual,
        };

        var reference = f.ProvenanceRef.Text?.Trim();
        return new CalcProvenance(kind, string.IsNullOrEmpty(reference) ? null : reference);
    }

    private static string ProvenanceLabel(CalcProvenanceKind kind) => kind switch
    {
        CalcProvenanceKind.Drawing => "drawing",
        CalcProvenanceKind.Specification => "spec",
        CalcProvenanceKind.Assumption => "assumption",
        CalcProvenanceKind.Evidence => "evidence",
        CalcProvenanceKind.Reference => "reference",
        _ => "manual",
    };

    private static void SetEnabled(FieldControls f, bool enabled)
    {
        if (f.Text is not null)
            f.Text.IsReadOnly = !enabled;

        if (f.Unit is not null)
            f.Unit.IsEnabled = enabled;

        if (f.Choice is not null)
            f.Choice.IsEnabled = enabled;

        if (f.Flag is not null)
            f.Flag.IsEnabled = enabled;

        f.Provenance.IsEnabled = enabled;
        f.ProvenanceRef.IsReadOnly = !enabled;
    }

    private static string FormatUtilisation(double u) => double.IsInfinity(u) ? "—" : Tempest.Calc.Engine.Values.CalcValue.FormatNumber(u, 3);

    private static string DisciplineLabel(CalcDiscipline d) => d == CalcDiscipline.BuildingServices ? "Building Services / HVAC" : d.ToString();

    private Control BuildLayout()
    {
        var left = new StackPanel { Spacing = DesignTokens.SpaceSm, Margin = DesignTokens.PanelPadding };
        left.Children.Add(new TextBlock { Text = Heading, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading });
        left.Children.Add(_catalogue);

        var right = new StackPanel { Spacing = DesignTokens.SpaceSm, Margin = DesignTokens.PanelPadding };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceLg };
        head.Children.Add(_title);
        head.Children.Add(_watermark);
        right.Children.Add(head);
        right.Children.Add(_reference);
        right.Children.Add(_scope);
        right.Children.Add(LabelledRow("Calculation name", _nameBox));
        right.Children.Add(Section("Inputs", _form));
        right.Children.Add(_problems);

        var resultsHead = new StackPanel { Spacing = DesignTokens.SpaceXs };
        var governingRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceLg };
        governingRow.Children.Add(_governing);
        governingRow.Children.Add(_governingLink);
        governingRow.Children.Add(_stale);
        resultsHead.Children.Add(governingRow);
        resultsHead.Children.Add(_utilisation);
        resultsHead.Children.Add(_results);
        resultsHead.Children.Add(_checks);
        resultsHead.Children.Add(_notices);
        resultsHead.Children.Add(_showWorking);
        resultsHead.Children.Add(_working);
        right.Children.Add(Section("Results", resultsHead));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        actions.Children.Add(_saveButton);
        actions.Children.Add(_checkButton);
        actions.Children.Add(_issueButton);
        actions.Children.Add(_reviseButton);
        actions.Children.Add(_exportButton);
        right.Children.Add(actions);
        right.Children.Add(_status);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*") };
        var leftScroll = new ScrollViewer { Content = left };
        var rightScroll = new ScrollViewer { Content = right };
        AutomationProperties.SetName(leftScroll, "Worksheets left column");
        AutomationProperties.SetName(rightScroll, "Worksheets right column");
        Grid.SetColumn(leftScroll, 0);
        Grid.SetColumn(rightScroll, 1);
        grid.Children.Add(leftScroll);
        grid.Children.Add(rightScroll);
        KeyboardNavigation.SetTabNavigation(right, KeyboardNavigationMode.Continue);
        return grid;
    }

    private static Expander Section(string title, Control content) => new() { Header = title, IsExpanded = true, Margin = DesignTokens.SectionMargin, Content = content };

    private static TextBlock Caption(string text) => new() { Text = text, FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, MaxWidth = 760, Opacity = 0.8 };

    private static TextBlock Mono(string text) => new() { Text = text, FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, MaxWidth = 760 };

    private static Control LabelledRow(string label, Control control)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*") };
        var text = new TextBlock { Text = label, Opacity = 0.8, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(control, 1);
        row.Children.Add(text);
        row.Children.Add(control);
        return row;
    }

    private static Control Readout(string label, string value, string? note)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("120,220,*") };
        var name = new TextBlock { Text = label, Opacity = 0.8, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Top };
        var read = new TextBlock { Text = value, FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
        var desc = Caption(note ?? string.Empty);
        AutomationProperties.SetName(read, $"Result {label}");
        Grid.SetColumn(name, 0);
        Grid.SetColumn(read, 1);
        Grid.SetColumn(desc, 2);
        row.Children.Add(name);
        row.Children.Add(read);
        row.Children.Add(desc);
        return row;
    }

    private static TextBox Input(string initial, string? watermark = null) => new() { Text = initial, Watermark = watermark, FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch };
}
