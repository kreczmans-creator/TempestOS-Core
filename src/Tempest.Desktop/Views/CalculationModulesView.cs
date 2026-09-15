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
/// The Engineering Calculators (`WP 21.7B`): every product calculation in
/// a catalogue by category, a form generated from the calculation's own
/// descriptor, and the result with its working and its method reference.
/// </summary>
/// <remarks>
/// <para>
/// <b>Generated, not written.</b> Nothing in this view names a
/// calculation or an input. The catalogue is <see cref="CalculationModuleDescriptors"/>
/// grouped by category, the form is one row per
/// <see cref="CalculationInputDescriptor"/> (a number with a unit picker,
/// a plain number, text, a choice, yes-or-no, or rows), and the result is
/// whatever rows <see cref="CalculationModuleWorkbench"/> presents. A
/// module registered tomorrow appears here with no change to this file:
/// the Product Owner's ask, a calculator surface driven by its catalogue.
/// </para>
/// <para>
/// <b>Materials are picked, never typed.</b> An input the descriptor marks
/// as coming from the material record is read-only on the form and filled
/// from the released record picked above it, with the record's own
/// revision pinned onto the calculation. Nothing is offered that has not
/// been released.
/// </para>
/// <para>
/// <b>This view decides nothing.</b> It collects text, raises intent and
/// renders what comes back, exactly as <see cref="EngineeringCalculationView"/>
/// does beside it. A refusal is shown as the outcome the definition
/// reported, not as an error.
/// </para>
/// </remarks>
public sealed class CalculationModulesView : UserControl
{
    /// <summary>The heading, and this surface's own automation name.</summary>
    public const string Heading = "Engineering Calculators";

    /// <summary>The caption on the button that runs the selected calculation.</summary>
    public const string CalculateCaption = "Calculate";

    /// <summary>Automation name of the catalogue tree.</summary>
    public const string CatalogueAutomationName = "Calculator catalogue";

    /// <summary>Automation name of the material picker.</summary>
    public const string MaterialPickerAutomationName = "Material record for the calculation";

    /// <summary>Automation name of the left column (catalogue and material).</summary>
    public const string LeftColumnAutomationName = "Engineering calculators left column";

    /// <summary>Automation name of the right column (about, inputs, results, working).</summary>
    public const string RightColumnAutomationName = "Engineering calculators right column";

    /// <summary>What the surface says before a calculation is chosen.</summary>
    public const string PickModuleGuidance = "Choose a calculation from the catalogue on the left. Its inputs, with their units and limits, appear here.";

    /// <summary>What the material section says when nothing is released.</summary>
    public const string NoReleasedMaterialGuidance =
        "No released material record yet. Release one under Engineering Calculations (Reference Library) or Reference data; "
        + "only a released record can fill a calculation's material inputs.";

    private readonly TreeView _catalogue = new() { MinHeight = 200 };
    private readonly ComboBox _materialPicker = new()
    {
        FontSize = DesignTokens.FontSizeBody,
        MinHeight = DesignTokens.MinControlSize,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        PlaceholderText = "Choose a released material",
    };

    private readonly TextBlock _materialGuidance = Caption(string.Empty);
    private readonly TextBlock _title = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 4, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _description = Caption(PickModuleGuidance);
    private readonly TextBlock _method = Caption(string.Empty);
    private readonly TextBlock _specification = Caption(string.Empty);
    private readonly StackPanel _form = new() { Spacing = DesignTokens.SpaceSm };
    private readonly Button _calculateButton = new() { Content = CalculateCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly TextBlock _problems = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, MaxWidth = 640, Foreground = Brushes.IndianRed };
    private readonly TextBlock _status = Caption(string.Empty);
    private readonly TextBlock _outcome = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _refusal = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, MaxWidth = 640 };
    private readonly StackPanel _resultPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _workingPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _checksPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly TextBlock _recordNote = Caption(string.Empty);
    private readonly Expander _resultsSection;
    private readonly Expander _workingSection;

    private readonly Dictionary<string, FieldControls> _fields = new(StringComparer.Ordinal);
    private bool _replacingMaterials;

    private sealed record FieldControls(CalculationInputDescriptor Descriptor, TextBox? Text, ComboBox? Unit, ComboBox? Choice, CheckBox? Flag, TextBox? Rows);

    /// <summary>Initialises a new instance of the <see cref="CalculationModulesView"/> class.</summary>
    public CalculationModulesView()
    {
        AutomationProperties.SetName(this, Heading);
        AutomationProperties.SetName(_catalogue, CatalogueAutomationName);
        AutomationProperties.SetName(_materialPicker, MaterialPickerAutomationName);
        AutomationProperties.SetName(_calculateButton, CalculateCaption);
        AutomationProperties.SetName(_outcome, "Calculation outcome");
        AutomationProperties.SetLiveSetting(_outcome, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_problems, "Input problems");
        AutomationProperties.SetLiveSetting(_problems, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_status, "Calculator status");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);

        _catalogue.SelectionChanged += (_, _) => OnCatalogueSelectionChanged();
        _materialPicker.SelectionChanged += (_, _) =>
        {
            if (!_replacingMaterials && SelectedMaterial is { } picked)
                MaterialPicked?.Invoke(picked);
        };
        _calculateButton.Click += (_, _) => CalculateRequested?.Invoke();

        _resultsSection = Section("Results", BuildResultsPanel());
        _workingSection = Section("Working", BuildWorkingPanel());

        Content = BuildLayout();
        ClearResult();
    }

    /// <summary>Raised when a calculation is chosen from the catalogue.</summary>
    public event Action<CalculationModuleDescriptor>? ModuleSelected;

    /// <summary>Raised when the engineer asks for the selected calculation to run.</summary>
    public event Action? CalculateRequested;

    /// <summary>Raised when a released material is picked.</summary>
    public event Action<ReleasedMaterialOption>? MaterialPicked;

    /// <summary>The calculation the form is generated for, or <see langword="null"/> before one is chosen.</summary>
    public CalculationModuleDescriptor? SelectedModule { get; private set; }

    /// <summary>The released materials the picker offers.</summary>
    public IReadOnlyList<ReleasedMaterialOption> Materials { get; private set; } = [];

    /// <summary>The material picked, or <see langword="null"/>.</summary>
    public ReleasedMaterialOption? SelectedMaterial => _materialPicker.SelectedItem as ReleasedMaterialOption;

    /// <summary>The catalogue as shown: every category and the calculations under it.</summary>
    public IReadOnlyList<CalculationModuleGroup> Catalogue { get; private set; } = [];

    /// <summary>The last run shown, or <see langword="null"/>.</summary>
    public CalculationModuleRun? LastRun { get; private set; }

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

    /// <summary>Shows the released materials, keeping <paramref name="keepRecordId"/> (or the current pick) selected where it is still offered.</summary>
    public void ShowMaterials(IReadOnlyList<ReleasedMaterialOption> materials, string? keepRecordId = null)
    {
        ArgumentNullException.ThrowIfNull(materials);

        var keep = keepRecordId ?? SelectedMaterial?.RecordId;
        Materials = materials;

        _replacingMaterials = true;
        try
        {
            _materialPicker.ItemsSource = materials;
            _materialPicker.SelectedItem = keep is null ? null : materials.FirstOrDefault(m => m.RecordId == keep);
        }
        finally
        {
            _replacingMaterials = false;
        }

        _materialPicker.IsEnabled = materials.Count > 0;
        _materialGuidance.Text = materials.Count == 0
            ? NoReleasedMaterialGuidance
            : "The picked record fills every input the calculation takes from the material, and is pinned, at its revision, onto the record of the calculation.";
    }

    /// <summary>Picks the released material with <paramref name="recordId"/>, as the engineer would from the picker.</summary>
    /// <exception cref="ArgumentException">The picker offers no such record.</exception>
    public void PickMaterial(string recordId)
    {
        _materialPicker.SelectedItem = Materials.FirstOrDefault(m => string.Equals(m.RecordId, recordId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"No released material '{recordId}' is offered.", nameof(recordId));
    }

    /// <summary>The form as typed, one field per input the form shows.</summary>
    public IReadOnlyList<CalculationFormField> ReadForm() =>
        _fields.Values.Select(f => new CalculationFormField(
            f.Descriptor.Name,
            f.Text?.Text,
            f.Unit?.SelectedItem as string,
            f.Choice?.SelectedItem as string,
            f.Flag?.IsChecked ?? false,
            f.Rows is { } rows ? (rows.Text ?? string.Empty).Split('\n').Select(r => r.TrimEnd('\r')).ToList() : null)).ToList();

    /// <summary>Sets one field of the form, as a test or a material fill would.</summary>
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
        _fields.TryGetValue(inputName, out var f) ? (Control?)f.Text ?? (Control?)f.Choice ?? (Control?)f.Flag ?? f.Rows : null;

    /// <summary>The unit picker of <paramref name="inputName"/>, or <see langword="null"/> where it has none.</summary>
    public ComboBox? UnitPicker(string inputName) => _fields.TryGetValue(inputName, out var f) ? f.Unit : null;

    /// <summary>Fills the material-sourced inputs from a picked record, and says what the record could not supply.</summary>
    public void ApplyFill(MaterialFill fill)
    {
        ArgumentNullException.ThrowIfNull(fill);

        foreach (var field in fill.Fields)
            SetField(field.Name, field.Text, field.UnitSymbol);

        _status.Text = fill.Problems.Count == 0
            ? $"Filled from {fill.Material.Label} ({fill.Material.Pin})."
            : $"{fill.Material.Label} ({fill.Material.Pin}) cannot supply every input: {string.Join(" ", fill.Problems.Select(p => $"{p.Label}: {p.Problem}."))}";
    }

    /// <summary>Shows what stopped the calculation: the form's problems by input, or the definition's own rejection.</summary>
    public void ShowProblems(IReadOnlyList<CalculationFormProblem> problems, string? rejection)
    {
        ArgumentNullException.ThrowIfNull(problems);

        var lines = problems.Select(p => $"{p.Label}: {p.Problem}.").ToList();
        if (rejection is not null)
            lines.Add($"Rejected by the calculation: {rejection}");

        _problems.Text = string.Join(Environment.NewLine, lines);
        _problems.IsVisible = lines.Count > 0;
    }

    /// <summary>Shows a run: the outcome, the refusal where there is one, every result row, the working and the checks.</summary>
    public void ShowRun(CalculationModuleRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        LastRun = run;
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
        _recordNote.Text = $"Recorded as {run.RecordId} at {run.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC; validation {run.ValidationOutcome}; cites {cited}. Method: {run.Module.MethodReference}";

        _resultsSection.IsVisible = true;
        _workingSection.IsVisible = true;
    }

    /// <summary>A short message about the last action, in the surface's own words.</summary>
    public void ShowStatus(string text) => _status.Text = text;

    /// <summary>Clears any result shown, as when a different calculation is chosen.</summary>
    public void ClearResult()
    {
        LastRun = null;
        _outcome.Text = string.Empty;
        _refusal.Text = string.Empty;
        _refusal.IsVisible = false;
        _resultPanel.Children.Clear();
        _workingPanel.Children.Clear();
        _checksPanel.Children.Clear();
        _recordNote.Text = string.Empty;
        _resultsSection.IsVisible = false;
        _workingSection.IsVisible = false;
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

                if (input.MaterialPropertyName is not null)
                {
                    box.IsReadOnly = true;
                    box.Watermark = "from the picked material record";
                }

                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,120") };
                Grid.SetColumn(box, 0);
                Grid.SetColumn(units, 1);
                row.Children.Add(box);
                row.Children.Add(units);
                _fields[input.Name] = new FieldControls(input, box, units, null, null, null);
                return LabelledRow(input.Label, row);
            }

            case CalculationInputKind.Number:
            case CalculationInputKind.Text:
            {
                var box = Input(string.Empty, input.Limits);
                AutomationProperties.SetName(box, input.Label);
                ToolTip.SetTip(box, input.Description);
                _fields[input.Name] = new FieldControls(input, box, null, null, null, null);
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
                _fields[input.Name] = new FieldControls(input, null, null, picker, null, null);
                return LabelledRow(input.Label, picker);
            }

            case CalculationInputKind.Boolean:
            {
                var check = new CheckBox { Content = input.Description, FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
                AutomationProperties.SetName(check, input.Label);
                _fields[input.Name] = new FieldControls(input, null, null, null, check, null);
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
                _fields[input.Name] = new FieldControls(input, null, null, null, null, rows);
                return LabelledRow(input.Label, rows);
            }

            case CalculationInputKind.Reference:
            {
                var note = Caption(input.IsOptional
                    ? $"{input.Description} Taken from the material picked on the left, if any."
                    : $"{input.Description} Taken from the material picked on the left.");
                _fields[input.Name] = new FieldControls(input, null, null, null, null, null);
                return LabelledRow(input.Label, note);
            }

            default:
                return Caption($"{input.Label}: this form cannot show a {input.Kind} input.");
        }
    }

    // ---- Layout ----

    private Control BuildLayout()
    {
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("360,*"), Margin = DesignTokens.PagePadding };

        var left = new StackPanel { Spacing = DesignTokens.SpaceMd };
        var catalogue = new StackPanel { Spacing = DesignTokens.SpaceSm };
        catalogue.Children.Add(Caption("Every calculation the product registers, by category. Pick one to generate its form."));
        catalogue.Children.Add(_catalogue);
        left.Children.Add(Section("Catalogue", catalogue));

        var material = new StackPanel { Spacing = DesignTokens.SpaceSm };
        material.Children.Add(_materialPicker);
        material.Children.Add(_materialGuidance);
        left.Children.Add(Section("Material", material));

        var right = new StackPanel { Spacing = DesignTokens.SpaceMd };
        right.Children.Add(PageHeading.Label("Engineering"));
        right.Children.Add(PageHeading.Title(Heading));
        right.Children.Add(PageHeading.Lead(
            "Hand calculations from the product catalogue, each run against its own method with its inputs, units and limits "
            + "generated from the calculation itself. Every run is recorded with its working and the material record it stood on."));

        var about = new StackPanel { Spacing = DesignTokens.SpaceXs };
        about.Children.Add(_title);
        about.Children.Add(_description);
        about.Children.Add(_method);
        about.Children.Add(_specification);
        right.Children.Add(Section("About this calculation", about));

        var inputs = new StackPanel { Spacing = DesignTokens.SpaceSm };
        inputs.Children.Add(_form);
        inputs.Children.Add(_calculateButton);
        inputs.Children.Add(_problems);
        inputs.Children.Add(_status);
        right.Children.Add(Section("Inputs", inputs));

        right.Children.Add(_resultsSection);
        right.Children.Add(_workingSection);

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
