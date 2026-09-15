using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.Calculations.Modules;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Engineering;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Engineering Calculators (`WP 21.7B`, completed by `WP 21.7C`):
/// every product calculation in a catalogue by category, a form generated
/// from the calculation's own descriptor with a released-record picker per
/// reference input, and the result with its working, its method reference,
/// and the record's own Re-run and Compare commands.
/// </summary>
/// <remarks>
/// <para>
/// <b>Generated, not written.</b> Nothing in this view names a
/// calculation or an input. The catalogue is <see cref="CalculationModuleDescriptors"/>
/// grouped by category, the form is one row per
/// <see cref="CalculationInputDescriptor"/> (a number with a unit picker,
/// a plain number, text, a choice, yes-or-no, rows, or a picker over the
/// released records of the input's own library), and the result is
/// whatever rows the workbench presents. A module registered tomorrow
/// appears here with no change to this file.
/// </para>
/// <para>
/// <b>Records are picked, never typed.</b> Each reference input has its
/// own picker over its own library; an input the descriptor marks as read
/// from that record is read-only on the form and filled from the picked
/// record, with the record's own revision pinned onto the calculation and
/// cited on its record. Nothing is offered that has not been released.
/// </para>
/// <para>
/// <b>This view decides nothing.</b> It collects text, raises intent and
/// renders what comes back. A refusal is shown as the outcome the
/// definition reported, not as an error; Re-run and Compare are the
/// canonical commands on the recorded Calculation, offered on its result.
/// </para>
/// </remarks>
public sealed class CalculationModulesView : UserControl
{
    /// <summary>The heading, and this surface's own automation name.</summary>
    public const string Heading = "Engineering Calculators";

    /// <summary>The caption on the button that runs the selected calculation.</summary>
    public const string CalculateCaption = "Calculate";

    /// <summary>The caption on the button that re-runs the recorded calculation with its retained input.</summary>
    public const string RerunCaption = "Re-run";

    /// <summary>The caption on the button that compares the recorded calculation with its previous run.</summary>
    public const string CompareCaption = "Compare with previous";

    /// <summary>The caption on the button that leaves the recorded calculation, so the next Calculate names a new one.</summary>
    public const string NewCalculationCaption = "Start a new calculation";

    /// <summary>Automation name of the catalogue tree.</summary>
    public const string CatalogueAutomationName = "Calculator catalogue";

    /// <summary>Automation name of the left column (catalogue and libraries).</summary>
    public const string LeftColumnAutomationName = "Engineering calculators left column";

    /// <summary>Automation name of the right column (about, inputs, results, working, comparison).</summary>
    public const string RightColumnAutomationName = "Engineering calculators right column";

    /// <summary>What the surface says before a calculation is chosen.</summary>
    public const string PickModuleGuidance = "Choose a calculation from the catalogue on the left. Its inputs, with their units and limits, appear here.";

    /// <summary>What a reference picker says when its library has nothing released.</summary>
    public const string NoReleasedRecordGuidance = "Nothing released in this library yet. Release a record under Reference data; only a released record can be picked.";

    private readonly TreeView _catalogue = new() { MinHeight = 200 };
    private readonly TextBlock _librariesNote = Caption(string.Empty);
    private readonly TextBlock _title = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 4, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _description = Caption(PickModuleGuidance);
    private readonly TextBlock _method = Caption(string.Empty);
    private readonly TextBlock _specification = Caption(string.Empty);
    private readonly StackPanel _form = new() { Spacing = DesignTokens.SpaceSm };
    private readonly TextBox _nameBox = Input(string.Empty, "What to call this calculation (optional)");
    private readonly Button _calculateButton = new() { Content = CalculateCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _rerunButton = new() { Content = RerunCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _compareButton = new() { Content = CompareCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _newButton = new() { Content = NewCalculationCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly TextBlock _problems = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, MaxWidth = 640, Foreground = Brushes.IndianRed };
    private readonly TextBlock _status = Caption(string.Empty);
    private readonly TextBlock _outcome = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _refusal = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, MaxWidth = 640 };
    private readonly StackPanel _resultPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _workingPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _checksPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _comparisonPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly TextBlock _comparisonNote = Caption(string.Empty);
    private readonly TextBlock _recordNote = Caption(string.Empty);
    private readonly Expander _resultsSection;
    private readonly Expander _workingSection;
    private readonly Expander _comparisonSection;

    private readonly Dictionary<ReferenceLibrary, IReadOnlyList<ReleasedRecordOption>> _released = new();
    private readonly Dictionary<string, FieldControls> _fields = new(StringComparer.Ordinal);
    private bool _replacingPickers;

    private sealed record FieldControls(CalculationInputDescriptor Descriptor, TextBox? Text, ComboBox? Unit, ComboBox? Choice, CheckBox? Flag, TextBox? Rows, ComboBox? Picker);

    /// <summary>Initialises a new instance of the <see cref="CalculationModulesView"/> class.</summary>
    public CalculationModulesView()
    {
        AutomationProperties.SetName(this, Heading);
        AutomationProperties.SetName(_catalogue, CatalogueAutomationName);
        AutomationProperties.SetName(_nameBox, "Calculation name");
        AutomationProperties.SetName(_calculateButton, CalculateCaption);
        AutomationProperties.SetName(_rerunButton, RerunCaption);
        AutomationProperties.SetName(_compareButton, CompareCaption);
        AutomationProperties.SetName(_newButton, NewCalculationCaption);
        AutomationProperties.SetName(_outcome, "Calculation outcome");
        AutomationProperties.SetLiveSetting(_outcome, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_problems, "Input problems");
        AutomationProperties.SetLiveSetting(_problems, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_status, "Calculator status");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);

        _catalogue.SelectionChanged += (_, _) => OnCatalogueSelectionChanged();
        _calculateButton.Click += (_, _) => CalculateRequested?.Invoke();
        _rerunButton.Click += (_, _) => RerunRequested?.Invoke();
        _compareButton.Click += (_, _) => CompareRequested?.Invoke();
        _newButton.Click += (_, _) =>
        {
            ClearResult();
            _status.Text = "Left the recorded calculation; the next Calculate records a new one.";
        };

        _resultsSection = Section("Results", BuildResultsPanel());
        _workingSection = Section("Working", BuildWorkingPanel());
        _comparisonSection = Section("Comparison with the previous run", BuildComparisonPanel());

        Content = BuildLayout();
        ClearResult();
    }

    /// <summary>Raised when a calculation is chosen from the catalogue.</summary>
    public event Action<CalculationModuleDescriptor>? ModuleSelected;

    /// <summary>Raised when the engineer asks for the selected calculation to run.</summary>
    public event Action? CalculateRequested;

    /// <summary>Raised when the engineer asks for the recorded calculation to be re-run.</summary>
    public event Action? RerunRequested;

    /// <summary>Raised when the engineer asks for the recorded calculation to be compared with its previous run.</summary>
    public event Action? CompareRequested;

    /// <summary>Raised when a released record is picked for a reference input.</summary>
    public event Action<string, ReleasedRecordOption>? ReferencePicked;

    /// <summary>The calculation the form is generated for, or <see langword="null"/> before one is chosen.</summary>
    public CalculationModuleDescriptor? SelectedModule { get; private set; }

    /// <summary>The catalogue as shown: every category and the calculations under it.</summary>
    public IReadOnlyList<CalculationModuleGroup> Catalogue { get; private set; } = [];

    /// <summary>The run shown, with the Calculation object it is recorded against, or <see langword="null"/>.</summary>
    public CalculationSurfaceRun? CurrentRun { get; private set; }

    /// <summary>The last run shown, or <see langword="null"/>.</summary>
    public CalculationModuleRun? LastRun => CurrentRun?.Run;

    /// <summary>The last comparison shown, or <see langword="null"/>.</summary>
    public CalculationSurfaceComparison? LastComparison { get; private set; }

    /// <summary>What the engineer typed as the calculation's name, or <see langword="null"/> for the default.</summary>
    public string? CalculationName => string.IsNullOrWhiteSpace(_nameBox.Text) ? null : _nameBox.Text;

    /// <summary>The released records the picker of <paramref name="library"/> offers.</summary>
    public IReadOnlyList<ReleasedRecordOption> Released(ReferenceLibrary library) => _released.TryGetValue(library, out var options) ? options : [];

    /// <summary>The record picked for <paramref name="inputName"/>, or <see langword="null"/>.</summary>
    public ReleasedRecordOption? PickedRecord(string inputName) =>
        _fields.TryGetValue(inputName, out var f) ? f.Picker?.SelectedItem as ReleasedRecordOption : null;

    /// <summary>Shows the catalogue, one expanded category node per group, each calculation a child node.</summary>
    public void ShowCatalogue(IReadOnlyList<CalculationModuleGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var selectedId = SelectedModule?.Id;
        Catalogue = groups;
        _catalogue.Items.Clear();

        foreach (var group in groups)
        {
            var node = new TreeViewItem { Header = group.Category, IsExpanded = true };
            AutomationProperties.SetName(node, $"Category {group.Category}");

            foreach (var module in group.Modules)
            {
                var leaf = new TreeViewItem { Header = module.Title, Tag = module };
                AutomationProperties.SetName(leaf, $"Calculator {module.Title}");
                ToolTip.SetTip(leaf, module.MethodReference);
                node.Items.Add(leaf);
            }

            _catalogue.Items.Add(node);
        }

        if (selectedId is not null)
            SelectModule(selectedId);
    }

    /// <summary>Selects the calculation with <paramref name="calculationId"/> in the catalogue, generating its form.</summary>
    /// <exception cref="ArgumentException">No shown catalogue entry has that id.</exception>
    public void SelectModule(string calculationId)
    {
        var leaf = _catalogue.Items.OfType<TreeViewItem>()
            .SelectMany(node => node.Items.OfType<TreeViewItem>())
            .FirstOrDefault(l => l.Tag is CalculationModuleDescriptor d && string.Equals(d.Id, calculationId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"The catalogue shows no calculation '{calculationId}'.", nameof(calculationId));

        _catalogue.SelectedItem = leaf;
        if (!ReferenceEquals(SelectedModule, leaf.Tag))
            OnCatalogueSelectionChanged();
    }

    /// <summary>Shows the released records of <paramref name="library"/>, re-offering them on every picker of that library and keeping each current pick where it is still offered.</summary>
    public void ShowReleased(ReferenceLibrary library, IReadOnlyList<ReleasedRecordOption> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        _released[library] = records;

        foreach (var field in _fields.Values.Where(f => f.Picker is not null && f.Descriptor.Library == library))
            Offer(field, records);

        _librariesNote.Text = string.Join("  ", Enum.GetValues<ReferenceLibrary>().Select(l => $"{l}: {Released(l).Count} released"));
    }

    /// <summary>Picks the released record <paramref name="recordId"/> for the reference input <paramref name="inputName"/>, as the engineer would from its picker.</summary>
    /// <exception cref="ArgumentException">The form shows no such reference input, or its picker offers no such record.</exception>
    public void PickRecord(string inputName, string recordId)
    {
        if (!_fields.TryGetValue(inputName, out var field) || field.Picker is null)
            throw new ArgumentException($"The form shows no reference input '{inputName}'.", nameof(inputName));

        field.Picker.SelectedItem = field.Picker.Items.OfType<ReleasedRecordOption>().FirstOrDefault(o => string.Equals(o.RecordId, recordId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"No released record '{recordId}' is offered for {inputName}.", nameof(recordId));
    }

    /// <summary>The form as typed, one field per input the form shows; a reference field carries its picked record id.</summary>
    public IReadOnlyList<CalculationFormField> ReadForm() =>
        _fields.Values.Select(f => new CalculationFormField(
            f.Descriptor.Name,
            f.Text?.Text,
            f.Unit?.SelectedItem as string,
            f.Choice?.SelectedItem as string,
            f.Flag?.IsChecked ?? false,
            f.Rows is { } rows ? (rows.Text ?? string.Empty).Split('\n').Select(r => r.TrimEnd('\r')).ToList() : null,
            (f.Picker?.SelectedItem as ReleasedRecordOption)?.RecordId)).ToList();

    /// <summary>Sets one field of the form, as a test or a fill would.</summary>
    /// <exception cref="ArgumentException">The form shows no input named <paramref name="inputName"/>.</exception>
    public void SetField(string inputName, string? text = null, string? unitSymbol = null, string? choice = null, bool? flag = null, IReadOnlyList<string>? rows = null)
    {
        if (!_fields.TryGetValue(inputName, out var field))
            throw new ArgumentException($"The form shows no input '{inputName}'.", nameof(inputName));

        if (text is not null && field.Text is { } box)
            box.Text = text;
        if (unitSymbol is not null && field.Unit is { } unit)
            unit.SelectedItem = unit.Items.OfType<string>().FirstOrDefault(s => s == unitSymbol) ?? throw new ArgumentException($"'{unitSymbol}' is not a unit the form offers for {inputName}.", nameof(unitSymbol));
        if (choice is not null && field.Choice is { } picker)
            picker.SelectedItem = picker.Items.OfType<string>().FirstOrDefault(s => s == choice) ?? throw new ArgumentException($"'{choice}' is not a choice the form offers for {inputName}.", nameof(choice));
        if (flag is { } value && field.Flag is { } check)
            check.IsChecked = value;
        if (rows is not null && field.Rows is { } lines)
            lines.Text = string.Join(Environment.NewLine, rows);
    }

    /// <summary>The control that takes <paramref name="inputName"/>'s value, for a test to assert placement of.</summary>
    public Control? FieldControl(string inputName) =>
        _fields.TryGetValue(inputName, out var f) ? (Control?)f.Text ?? (Control?)f.Choice ?? (Control?)f.Flag ?? (Control?)f.Rows ?? f.Picker : null;

    /// <summary>The unit picker of <paramref name="inputName"/>, or <see langword="null"/> where it has none.</summary>
    public ComboBox? UnitPicker(string inputName) => _fields.TryGetValue(inputName, out var f) ? f.Unit : null;

    /// <summary>The record picker of the reference input <paramref name="inputName"/>, or <see langword="null"/>.</summary>
    public ComboBox? RecordPicker(string inputName) => _fields.TryGetValue(inputName, out var f) ? f.Picker : null;

    /// <summary>Fills the inputs read from a picked record, and says what the record could not supply.</summary>
    public void ApplyFill(ReferenceFill fill)
    {
        ArgumentNullException.ThrowIfNull(fill);

        foreach (var field in fill.Fields)
            SetField(field.Name, field.Text, field.UnitSymbol, field.Choice);

        var reference = SelectedModule?.Inputs.FirstOrDefault(i => i.Name == fill.ReferenceInputName);
        _status.Text = fill.Problems.Count == 0
            ? $"{reference?.Label ?? fill.ReferenceInputName}: filled from {fill.Record.Label} ({fill.Record.Pin})."
            : $"{reference?.Label ?? fill.ReferenceInputName}: {fill.Record.Label} ({fill.Record.Pin}) cannot supply every input read from it. {string.Join(" ", fill.Problems.Select(p => $"{p.Label}: {p.Problem}."))}";
    }

    /// <summary>Shows what stopped the calculation: the form's problems by input, or the calculation's own rejection.</summary>
    public void ShowProblems(IReadOnlyList<CalculationFormProblem> problems, string? rejection)
    {
        ArgumentNullException.ThrowIfNull(problems);

        var lines = problems.Select(p => $"{p.Label}: {p.Problem}.").ToList();
        if (rejection is not null)
            lines.Add($"Rejected by the calculation: {rejection}");

        _problems.Text = string.Join(Environment.NewLine, lines);
        _problems.IsVisible = lines.Count > 0;
    }

    /// <summary>Shows a run: the outcome, the refusal where there is one, every result row, the working, the checks, and the record it is named as.</summary>
    public void ShowRun(CalculationSurfaceRun current)
    {
        ArgumentNullException.ThrowIfNull(current);

        CurrentRun = current;
        LastComparison = null;
        var run = current.Run;
        ShowProblems([], null);

        _outcome.Text = run.OutcomeSummary;
        _refusal.Text = run.RefusalReason ?? string.Empty;
        _refusal.IsVisible = run.RefusalReason is not null;

        _resultPanel.Children.Clear();
        foreach (var row in run.Results)
            _resultPanel.Children.Add(Readout(row.Label, row.Display));

        _workingPanel.Children.Clear();
        foreach (var row in run.Working)
            _workingPanel.Children.Add(Readout(row.Label, row.Display));

        _checksPanel.Children.Clear();
        foreach (var check in run.Checks)
            _checksPanel.Children.Add(Readout(check.IsSatisfied ? "Satisfied" : "Not satisfied", check.Detail is null ? check.Description : $"{check.Description} {check.Detail}"));

        var cited = run.ReferencedMaterialIds.Count == 0 ? "no material record" : string.Join(", ", run.ReferencedMaterialIds);
        var predecessor = run.PredecessorRecordId is { } p ? $" Re-run of {p}." : string.Empty;
        _recordNote.Text = $"Recorded as '{current.DisplayName}' (record {run.RecordId}, run {current.RunCount}) at {run.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC; validation {run.ValidationOutcome}; cites {cited}.{predecessor} Method: {run.Module.MethodReference}";

        _rerunButton.IsEnabled = true;
        _compareButton.IsEnabled = current.RunCount >= 2;
        _newButton.IsEnabled = true;
        _comparisonPanel.Children.Clear();
        _comparisonNote.Text = current.RunCount >= 2
            ? "Press Compare with previous to see what changed between the last two runs."
            : "Re-run once, or press Calculate again with a changed input, before comparing.";
        _resultsSection.IsVisible = true;
        _workingSection.IsVisible = true;
        _comparisonSection.IsVisible = true;
    }

    /// <summary>Shows a comparison as a table: section, field, before and after, with units.</summary>
    public void ShowComparison(CalculationSurfaceComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        LastComparison = comparison;
        _comparisonPanel.Children.Clear();

        if (!comparison.HasChanges)
        {
            _comparisonNote.Text = "Nothing differs between the last two runs: the identical input gave the identical result.";
            return;
        }

        _comparisonNote.Text = $"Records {comparison.Comparison.RecordIdA} (before) and {comparison.Comparison.RecordIdB} (after).";
        _comparisonPanel.Children.Add(ComparisonRow("Section", "Field", "Before", "After", header: true));
        foreach (var row in comparison.Rows)
            _comparisonPanel.Children.Add(ComparisonRow(row.Section, row.Field, row.Before, row.After, header: false));
    }

    /// <summary>A short message about the last action, in the surface's own words.</summary>
    public void ShowStatus(string text) => _status.Text = text;

    /// <summary>Clears any result shown, as when a different calculation is chosen.</summary>
    public void ClearResult()
    {
        CurrentRun = null;
        LastComparison = null;
        _outcome.Text = string.Empty;
        _refusal.Text = string.Empty;
        _refusal.IsVisible = false;
        _resultPanel.Children.Clear();
        _workingPanel.Children.Clear();
        _checksPanel.Children.Clear();
        _comparisonPanel.Children.Clear();
        _comparisonNote.Text = string.Empty;
        _recordNote.Text = string.Empty;
        _rerunButton.IsEnabled = false;
        _compareButton.IsEnabled = false;
        _newButton.IsEnabled = false;
        _resultsSection.IsVisible = false;
        _workingSection.IsVisible = false;
        _comparisonSection.IsVisible = false;
        ShowProblems([], null);
    }

    // ---- The generated form ----

    private void OnCatalogueSelectionChanged()
    {
        if (_catalogue.SelectedItem is not TreeViewItem { Tag: CalculationModuleDescriptor module })
            return;

        if (ReferenceEquals(SelectedModule, module))
            return;

        SelectedModule = module;
        BuildForm(module);
        ClearResult();
        _status.Text = string.Empty;
        ModuleSelected?.Invoke(module);
    }

    private void BuildForm(CalculationModuleDescriptor module)
    {
        _fields.Clear();
        _form.Children.Clear();

        _title.Text = module.Title;
        _description.Text = module.Metadata.Description ?? string.Empty;
        _method.Text = $"Method: {module.MethodReference}";
        _specification.Text = module.SpecificationPath is { } path ? $"Specification: {path}" : "Specification: the definition's own metadata (this calculation predates the written specifications).";

        foreach (var input in module.Inputs)
            _form.Children.Add(BuildRow(input));

        _calculateButton.IsEnabled = true;
    }

    private Control BuildRow(CalculationInputDescriptor input)
    {
        switch (input.Kind)
        {
            case CalculationInputKind.Quantity:
            {
                var box = Input(string.Empty, input.Limits);
                var units = new ComboBox
                {
                    ItemsSource = CalculationInputUnits.SymbolsOf(input.DimensionName!),
                    SelectedItem = input.DefaultUnitSymbol,
                    FontSize = DesignTokens.FontSizeBody,
                    MinHeight = DesignTokens.MinControlSize,
                    MinWidth = 110,
                };
                AutomationProperties.SetName(box, input.Label);
                AutomationProperties.SetName(units, $"{input.Label} unit");
                ToolTip.SetTip(box, input.Description);

                if (input.IsSourced)
                {
                    box.IsReadOnly = true;
                    box.Watermark = $"from the record picked for {LabelOf(input.SourceInputName!)}";
                }

                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,120") };
                Grid.SetColumn(box, 0);
                Grid.SetColumn(units, 1);
                row.Children.Add(box);
                row.Children.Add(units);
                _fields[input.Name] = new FieldControls(input, box, units, null, null, null, null);
                return LabelledRow(input.Label, row);
            }

            case CalculationInputKind.Number:
            case CalculationInputKind.Text:
            {
                var box = Input(string.Empty, input.Limits);
                AutomationProperties.SetName(box, input.Label);
                ToolTip.SetTip(box, input.Description);

                if (input.IsSourced)
                    box.Watermark = $"{input.Limits}; filled from the record picked for {LabelOf(input.SourceInputName!)}, or typed";

                _fields[input.Name] = new FieldControls(input, box, null, null, null, null, null);
                return LabelledRow(input.Label, box);
            }

            case CalculationInputKind.Choice:
            {
                var picker = new ComboBox
                {
                    ItemsSource = input.Choices ?? [],
                    SelectedItem = input.Choices?.FirstOrDefault(),
                    FontSize = DesignTokens.FontSizeBody,
                    MinHeight = DesignTokens.MinControlSize,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                AutomationProperties.SetName(picker, input.Label);
                ToolTip.SetTip(picker, input.Description);
                _fields[input.Name] = new FieldControls(input, null, null, picker, null, null, null);
                return LabelledRow(input.Label, picker);
            }

            case CalculationInputKind.Boolean:
            {
                var check = new CheckBox { Content = input.Description, FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
                AutomationProperties.SetName(check, input.Label);
                _fields[input.Name] = new FieldControls(input, null, null, null, check, null, null);
                return LabelledRow(input.Label, check);
            }

            case CalculationInputKind.List:
            {
                var rows = new TextBox
                {
                    AcceptsReturn = true,
                    MinHeight = 96,
                    Watermark = input.Description,
                    FontSize = DesignTokens.FontSizeBody,
                    FontFamily = DesignTokens.MonoFont,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                AutomationProperties.SetName(rows, input.Label);
                ToolTip.SetTip(rows, $"{input.Description} {input.Limits}.");
                _fields[input.Name] = new FieldControls(input, null, null, null, null, rows, null);
                return LabelledRow(input.Label, rows);
            }

            case CalculationInputKind.Reference:
            {
                var picker = new ComboBox
                {
                    FontSize = DesignTokens.FontSizeBody,
                    MinHeight = DesignTokens.MinControlSize,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    PlaceholderText = input.IsOptional ? $"Choose a released {input.Library} record, or leave empty" : $"Choose a released {input.Library} record",
                };
                AutomationProperties.SetName(picker, input.Label);
                ToolTip.SetTip(picker, input.Description);
                picker.SelectionChanged += (_, _) =>
                {
                    if (!_replacingPickers && picker.SelectedItem is ReleasedRecordOption picked)
                        ReferencePicked?.Invoke(input.Name, picked);
                };

                var field = new FieldControls(input, null, null, null, null, null, picker);
                _fields[input.Name] = field;
                Offer(field, Released(input.Library ?? ReferenceLibrary.Materials));

                var column = new StackPanel { Spacing = DesignTokens.SpaceXs };
                column.Children.Add(picker);
                column.Children.Add(Caption(input.Description));
                return LabelledRow(input.Label, column);
            }

            default:
                return Caption($"{input.Label}: this form cannot show a {input.Kind} input.");
        }
    }

    private void Offer(FieldControls field, IReadOnlyList<ReleasedRecordOption> records)
    {
        var picker = field.Picker!;
        var keep = (picker.SelectedItem as ReleasedRecordOption)?.RecordId;

        _replacingPickers = true;
        try
        {
            picker.ItemsSource = records;
            picker.SelectedItem = keep is null ? null : records.FirstOrDefault(r => r.RecordId == keep);
        }
        finally
        {
            _replacingPickers = false;
        }

        picker.IsEnabled = records.Count > 0;
        ToolTip.SetTip(picker, records.Count == 0 ? NoReleasedRecordGuidance : field.Descriptor.Description);
    }

    private string LabelOf(string inputName) => SelectedModule?.Inputs.FirstOrDefault(i => i.Name == inputName)?.Label ?? inputName;

    // ---- Layout ----

    private Control BuildLayout()
    {
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("360,*"), Margin = DesignTokens.PagePadding };

        var left = new StackPanel { Spacing = DesignTokens.SpaceMd };
        var catalogue = new StackPanel { Spacing = DesignTokens.SpaceSm };
        catalogue.Children.Add(Caption("Every calculation the product registers, by category. Pick one to generate its form."));
        catalogue.Children.Add(_catalogue);
        left.Children.Add(Section("Catalogue", catalogue));

        var libraries = new StackPanel { Spacing = DesignTokens.SpaceSm };
        libraries.Children.Add(Caption("Each reference input on a form picks from the released records of its own library. Release records under Reference data."));
        libraries.Children.Add(_librariesNote);
        left.Children.Add(Section("Libraries", libraries));

        var right = new StackPanel { Spacing = DesignTokens.SpaceMd };
        right.Children.Add(PageHeading.Label("Engineering"));
        right.Children.Add(PageHeading.Title(Heading));
        right.Children.Add(PageHeading.Lead(
            "Hand calculations from the product catalogue, each run against its own method with its inputs, units and limits "
            + "generated from the calculation itself. Every run is recorded, named, and can be re-run and compared."));

        var about = new StackPanel { Spacing = DesignTokens.SpaceXs };
        about.Children.Add(_title);
        about.Children.Add(_description);
        about.Children.Add(_method);
        about.Children.Add(_specification);
        right.Children.Add(Section("About this calculation", about));

        var inputs = new StackPanel { Spacing = DesignTokens.SpaceSm };
        inputs.Children.Add(_form);
        inputs.Children.Add(LabelledRow("Calculation name", _nameBox));
        inputs.Children.Add(_calculateButton);
        inputs.Children.Add(_problems);
        inputs.Children.Add(_status);
        right.Children.Add(Section("Inputs", inputs));

        right.Children.Add(_resultsSection);
        right.Children.Add(_workingSection);
        right.Children.Add(_comparisonSection);

        var leftColumn = new ScrollViewer { Content = left, Margin = new Thickness(0, 0, DesignTokens.SpaceLg, 0) };
        var rightColumn = new ScrollViewer { Content = right };
        Grid.SetColumn(leftColumn, 0);
        Grid.SetColumn(rightColumn, 1);
        AutomationProperties.SetName(leftColumn, LeftColumnAutomationName);
        AutomationProperties.SetName(rightColumn, RightColumnAutomationName);
        page.Children.Add(leftColumn);
        page.Children.Add(rightColumn);

        return page;
    }

    private Control BuildResultsPanel()
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceSm };
        panel.Children.Add(_outcome);
        panel.Children.Add(_refusal);
        panel.Children.Add(_resultPanel);
        panel.Children.Add(_recordNote);

        var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        commands.Children.Add(_rerunButton);
        commands.Children.Add(_compareButton);
        commands.Children.Add(_newButton);
        panel.Children.Add(commands);
        panel.Children.Add(Caption("Re-run repeats the recorded calculation with its retained input as a new record; Compare with previous shows what changed between the last two records. Both are the calculation's own commands, the same ones the Ribbon offers. Calculate again records another run against the same calculation; Start a new calculation leaves it, so the next Calculate names a new one."));
        return panel;
    }

    private Control BuildWorkingPanel()
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceSm };
        panel.Children.Add(Caption("Every intermediate the calculation recorded, in the order it computed them."));
        panel.Children.Add(_workingPanel);
        panel.Children.Add(Caption("Every constraint it checked."));
        panel.Children.Add(_checksPanel);
        return panel;
    }

    private Control BuildComparisonPanel()
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceSm };
        panel.Children.Add(_comparisonNote);
        panel.Children.Add(_comparisonPanel);
        return panel;
    }

    private static Control ComparisonRow(string section, string field, string before, string after, bool header)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("90,220,*,*") };
        var cells = new[] { section, field, before, after };
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = new TextBlock
            {
                Text = cells[i],
                FontSize = DesignTokens.FontSizeBody,
                FontFamily = i >= 2 && !header ? DesignTokens.MonoFont : FontFamily.Default,
                FontWeight = header ? DesignTokens.WeightHeading : FontWeight.Normal,
                TextWrapping = TextWrapping.Wrap,
                Opacity = i < 2 && !header ? 0.8 : 1.0,
            };
            Grid.SetColumn(cell, i);
            row.Children.Add(cell);
        }

        return row;
    }

    private static Expander Section(string title, Control content) => new()
    {
        Header = title,
        IsExpanded = true,
        Margin = DesignTokens.SectionMargin,
        Content = content,
    };

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontSizeCaption,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 640,
        Opacity = 0.8,
    };

    private static Control LabelledRow(string label, Control control)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("220,*") };
        var text = new TextBlock
        {
            Text = label,
            Opacity = 0.8,
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        Grid.SetColumn(text, 0);
        Grid.SetColumn(control, 1);
        row.Children.Add(text);
        row.Children.Add(control);
        return row;
    }

    private static Control Readout(string label, string value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("220,*") };
        var name = new TextBlock
        {
            Text = label,
            Opacity = 0.8,
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Top,
            TextWrapping = TextWrapping.Wrap,
        };
        var read = new TextBlock
        {
            Text = value,
            FontFamily = DesignTokens.MonoFont,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 640,
        };

        Grid.SetColumn(name, 0);
        Grid.SetColumn(read, 1);
        row.Children.Add(name);
        row.Children.Add(read);
        return row;
    }

    private static TextBox Input(string initial, string? watermark = null) => new()
    {
        Text = initial,
        Watermark = watermark,
        FontSize = DesignTokens.FontSizeBody,
        MinHeight = DesignTokens.MinControlSize,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
}
