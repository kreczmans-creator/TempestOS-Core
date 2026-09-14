using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.Commands;
using Tempest.Core.Macros;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The User Command Macro foundation's own authoring/browsing surface
/// (`WP 10.6A`) — lists existing macros (Run/Delete), and a minimal, real
/// "New Macro" editor: a name, and an ordered list of steps picked from
/// the commands that can run with nobody present (<see cref="IsMacroEligible"/>
/// — since TD-77 Stage 5 that includes the real discipline lifecycle
/// transitions, which `ADR-0098`'s own previously-disclosed limitation
/// excluded). Deliberately not a drag/drop builder — the brief's own "user
/// command macros (foundation)" framing, taken literally: real, working,
/// minimal. Shares the Dialog Framework's own established panel styling
/// (mirrors <see cref="SettingsDialog"/>'s construction).
/// </summary>
public sealed class MacroManagerDialog : Border
{
    private readonly IMacroManager _macroManager;
    private readonly ICommandRegistry _commandRegistry;
    private readonly CommandParameterPrompt _collectStepValues;
    private readonly Func<Guid, Task<CommandResult>> _runMacro;

    private readonly StackPanel _browsePanel = new();
    private readonly StackPanel _editorPanel = new() { IsVisible = false };

    // `Focusable` set explicitly (`WP 16.5A`, `TD-65`) — `ListBox`'s own
    // class default is `false`; only the Fluent theme's control theme
    // turns it on, once a template is actually applied, which is too
    // late for `ShowAsync`'s own deterministic initial-focus call below.
    private readonly ListBox _macroList = new() { MinHeight = 160, MinWidth = 320, Focusable = true };
    private readonly Button _newButton = new() { Content = "New Macro..." };
    private readonly Button _runButton = new() { Content = "Run" };
    private readonly Button _deleteButton = new() { Content = "Delete" };
    private readonly Button _closeButton = new() { Content = "Close" };

    private readonly TextBox _nameBox = new() { Watermark = "Macro name" };
    private readonly ListBox _availableCommands = new() { MinHeight = 140, MinWidth = 260 };
    private readonly ListBox _steps = new() { MinHeight = 140, MinWidth = 260 };
    private readonly Button _addStepButton = new() { Content = "Add Step →" };
    private readonly Button _removeStepButton = new() { Content = "← Remove Step" };
    private readonly Button _saveMacroButton = new() { Content = "Save Macro" };
    private readonly Button _cancelEditorButton = new() { Content = "Cancel" };
    private readonly TextBlock _statusText = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85 };

    private IReadOnlyList<ICommandMacro> _macros = [];
    private List<CommandDescriptor> _availableDescriptors = [];
    private readonly List<MacroStep> _draftSteps = [];

    /// <summary>Initialises a new instance of the <see cref="MacroManagerDialog"/> class, initially hidden.</summary>
    /// <param name="macroManager">The Macro foundation's own Platform Service.</param>
    /// <param name="commandRegistry">Used to list step-eligible commands (never to invoke — see <paramref name="runMacro"/>).</param>
    /// <param name="collectStepValues">
    /// Collects a step's own declared values at record time (`WP 20.2C`)
    /// — the identical <see cref="CommandParameterPrompt"/> seam a live
    /// invocation already uses (<see cref="DesktopCommandPrompt"/>), so a
    /// person answers each parameterised step's own question once, here,
    /// rather than every time the macro runs.
    /// </param>
    /// <param name="runMacro">Runs the macro with the given Id — set by <c>MainWindow</c> to route through <see cref="Tasks.IBackgroundTaskRunner"/>.</param>
    public MacroManagerDialog(
        IMacroManager macroManager,
        ICommandRegistry commandRegistry,
        CommandParameterPrompt collectStepValues,
        Func<Guid, Task<CommandResult>> runMacro)
    {
        ArgumentNullException.ThrowIfNull(macroManager);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(collectStepValues);
        ArgumentNullException.ThrowIfNull(runMacro);
        _macroManager = macroManager;
        _commandRegistry = commandRegistry;
        _collectStepValues = collectStepValues;
        _runMacro = runMacro;

        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 420;
        MaxWidth = 560;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        // A watermark alone names nothing to a screen reader (`WP 16.5A`,
        // `TD-65`).
        AutomationProperties.SetName(_nameBox, "Macro name");
        ToolTip.SetTip(_nameBox, "Macro name");

        AutomationProperties.SetName(_macroList, "Macros");
        AutomationProperties.SetName(_newButton, "New Macro...");
        AutomationProperties.SetName(_runButton, "Run");
        AutomationProperties.SetName(_deleteButton, "Delete");
        AutomationProperties.SetName(_closeButton, "Close");
        AutomationProperties.SetName(_availableCommands, "Available Commands");
        AutomationProperties.SetName(_steps, "Steps (run in order)");
        AutomationProperties.SetName(_addStepButton, "Add Step →");
        AutomationProperties.SetName(_removeStepButton, "← Remove Step");
        AutomationProperties.SetName(_saveMacroButton, "Save Macro");
        AutomationProperties.SetName(_cancelEditorButton, "Cancel");
        ToolTip.SetTip(_runButton, "Run");
        ToolTip.SetTip(_deleteButton, "Delete");
        ToolTip.SetTip(_addStepButton, "Add Step");
        ToolTip.SetTip(_removeStepButton, "Remove Step");

        BuildBrowsePanel();
        BuildEditorPanel();

        // Chrome treatments (`WP-Z4` Productisation Phase 1) — this dialog
        // predates ChromeStyles (`WP 10.0A`) and had never been brought
        // forward; every button now carries the same three treatments the
        // rest of the shell uses instead of the unstyled default button.
        _newButton.Classes.Add(ChromeStyles.Primary);
        _runButton.Classes.Add(ChromeStyles.Subtle);
        _deleteButton.Classes.Add(ChromeStyles.Danger);
        _closeButton.Classes.Add(ChromeStyles.Flat);
        _addStepButton.Classes.Add(ChromeStyles.Subtle);
        _removeStepButton.Classes.Add(ChromeStyles.Subtle);
        _saveMacroButton.Classes.Add(ChromeStyles.Primary);
        _cancelEditorButton.Classes.Add(ChromeStyles.Flat);

        var root = new StackPanel();
        var title = new TextBlock
        {
            Text = "Macros",
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeTitle,
            FontWeight = DesignTokens.WeightHeading,
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceMd),
        };
        ThemeReactiveBrush.Bind(title, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        root.Children.Add(title);
        root.Children.Add(_browsePanel);
        root.Children.Add(_editorPanel);
        root.Children.Add(_statusText);
        Child = root;

        KeyDown += OnKeyDown;

        // Real modal behaviour (`WP 16.5A`, `TD-65`) — see
        // `DialogModality`'s own remarks.
        DialogModality.Install(this);
    }

    /// <summary>
    /// <c>Escape</c> cancels one step at a time: mid-authoring (the
    /// editor panel showing), it returns to the browse panel — the exact
    /// same outcome as its own <see cref="_cancelEditorButton"/> — rather
    /// than abruptly discarding the whole dialog and losing the browse
    /// context; browsing, it closes the dialog outright, mirroring
    /// <see cref="_closeButton"/>. <c>Enter</c> needs no explicit
    /// handling — native <see cref="Button"/> behaviour, unchanged.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        if (_editorPanel.IsVisible)
            CloseEditor();
        else
            IsVisible = false;

        e.Handled = true;
    }

    private void BuildBrowsePanel()
    {
        _browsePanel.Children.Add(_macroList);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
        buttons.Children.Add(_newButton);
        buttons.Children.Add(_runButton);
        buttons.Children.Add(_deleteButton);
        buttons.Children.Add(_closeButton);
        _browsePanel.Children.Add(buttons);

        _newButton.Click += (_, _) => OpenEditor();
        _closeButton.Click += (_, _) => IsVisible = false;
        _runButton.Click += async (_, _) => await RunSelectedAsync().ConfigureAwait(true);
        _deleteButton.Click += async (_, _) => await DeleteSelectedAsync().ConfigureAwait(true);
    }

    private void BuildEditorPanel()
    {
        _editorPanel.Children.Add(_nameBox);

        var columns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };

        var availableColumn = new StackPanel();
        availableColumn.Children.Add(new TextBlock { Text = "Available Commands", FontSize = DesignTokens.FontSizeCaption });
        availableColumn.Children.Add(_availableCommands);

        var stepsColumn = new StackPanel();
        stepsColumn.Children.Add(new TextBlock { Text = "Steps (run in order)", FontSize = DesignTokens.FontSizeCaption });
        stepsColumn.Children.Add(_steps);

        columns.Children.Add(availableColumn);
        columns.Children.Add(stepsColumn);
        _editorPanel.Children.Add(columns);

        var stepButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
        stepButtons.Children.Add(_addStepButton);
        stepButtons.Children.Add(_removeStepButton);
        _editorPanel.Children.Add(stepButtons);

        var editorButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceMd, 0, 0) };
        editorButtons.Children.Add(_cancelEditorButton);
        editorButtons.Children.Add(_saveMacroButton);
        _editorPanel.Children.Add(editorButtons);

        _addStepButton.Click += async (_, _) => await AddStepAsync().ConfigureAwait(true);
        _removeStepButton.Click += (_, _) => RemoveStep();
        _cancelEditorButton.Click += (_, _) => CloseEditor();
        _saveMacroButton.Click += async (_, _) => await SaveMacroAsync().ConfigureAwait(true);
    }

    /// <summary>Shows this dialog, re-reading every current macro and every currently step-eligible command.</summary>
    public async Task ShowAsync()
    {
        await RefreshMacroListAsync().ConfigureAwait(true);
        CloseEditor();
        IsVisible = true;
        // The browse list, never a button — a ListBox does not invoke
        // anything on Enter, so an unintentional Enter right after
        // opening can never trigger Run/Delete.
        _macroList.Focus();
    }

    private async Task RefreshMacroListAsync()
    {
        _macros = await _macroManager.ListAsync().ConfigureAwait(true);
        _macroList.ItemsSource = _macros.Select(m => $"{m.Name} ({m.Steps.Count} step(s))").ToList();
    }

    private void OpenEditor()
    {
        _nameBox.Text = string.Empty;
        _draftSteps.Clear();
        _steps.ItemsSource = null;
        _statusText.Text = string.Empty;

        _availableDescriptors = _commandRegistry.Items.Where(IsMacroEligible).ToList();
        _availableCommands.ItemsSource = _availableDescriptors.Select(d => d.Category is null ? d.DisplayName : $"{d.Category}: {d.DisplayName}").ToList();

        _browsePanel.IsVisible = false;
        _editorPanel.IsVisible = true;
        _nameBox.Focus();
    }

    /// <summary>
    /// Whether <paramref name="descriptor"/> can be a macro step —
    /// TD-77 Stage 5, widened by `WP 20.2C` (`ADR-0099`'s own addendum).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A macro still never runs unattended past a confirmation
    /// (<c>ADR-0098</c>): a step declaring one is excluded, because no
    /// recording can stand in for a person's "yes". A step declaring
    /// <i>values</i>, though, is no longer excluded on that basis alone —
    /// recording what the person supplies when the step is added
    /// (<see cref="AddStepAsync"/>) is exactly what lets it replay
    /// unattended later (<c>RunMacroCommandHandler</c>). Only a binding
    /// this platform genuinely cannot invoke at all (declared
    /// <see cref="CommandBinding.Unavailable"/> — today, the object-picker
    /// set) stays excluded outright.
    /// </para>
    /// <para>
    /// This used to read <see cref="CommandDescriptor.CreateDefault"/>
    /// alone, which no production discipline command has ever set — so no
    /// real engineering command could be a macro step at all; then
    /// (TD-77 Stage 5) <see cref="CommandBinding.RequiresPrompt"/>, which
    /// admitted a bound command only if it declared neither a value nor a
    /// confirmation. The <c>CreateDefault</c> clause remains for the
    /// commands that still work the original way.
    /// </para>
    /// </remarks>
    internal static bool IsMacroEligible(CommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.Binding is { } binding
            ? binding.IsInvocable && binding.ConfirmationMessage is null
            : descriptor.CreateDefault is not null;
    }

    private void CloseEditor()
    {
        _editorPanel.IsVisible = false;
        _browsePanel.IsVisible = true;
    }

    /// <summary>
    /// Adds the selected available command as the macro's own next step —
    /// collecting its own declared values right now, through
    /// <see cref="_collectStepValues"/>, if it declares any (`WP 20.2C`).
    /// Declining that collection adds no step at all, exactly as
    /// declining any other command's own prompt runs nothing
    /// (<see cref="DesktopCommandPrompt"/>'s own remarks).
    /// </summary>
    private async Task AddStepAsync()
    {
        if (_availableCommands.SelectedIndex < 0 || _availableCommands.SelectedIndex >= _availableDescriptors.Count)
            return;

        var descriptor = _availableDescriptors[_availableCommands.SelectedIndex];
        var binding = descriptor.Binding;

        var recordedValues = EmptyValues;

        if (binding is { RequiresPrompt: true })
        {
            // IsMacroEligible already excludes a declared confirmation, so
            // only values are ever collected here — passed through
            // unchanged regardless, since this seam is the identical one
            // a live invocation already uses.
            var collected = await _collectStepValues(descriptor, binding.Parameters, binding.ConfirmationMessage, CancellationToken.None)
                .ConfigureAwait(true);

            if (collected is null)
                return;

            recordedValues = collected;
        }

        _draftSteps.Add(new MacroStep(descriptor.Id, recordedValues));
        _steps.ItemsSource = _draftSteps.Select((step, index) => $"{index + 1}. {DescribeStep(step)}").ToList();
    }

    /// <summary>One line describing a draft step — its Id, and any recorded values, so what will replay is visible before Save.</summary>
    private static string DescribeStep(MacroStep step) =>
        step.RecordedValues.Count == 0
            ? step.CommandId
            : $"{step.CommandId} ({string.Join(", ", step.RecordedValues.Select(kv => $"{kv.Key}={kv.Value}"))})";

    private static readonly IReadOnlyDictionary<string, string> EmptyValues = new Dictionary<string, string>(StringComparer.Ordinal);

    private void RemoveStep()
    {
        if (_steps.SelectedIndex < 0 || _steps.SelectedIndex >= _draftSteps.Count)
            return;

        _draftSteps.RemoveAt(_steps.SelectedIndex);
        _steps.ItemsSource = _draftSteps.Select((step, index) => $"{index + 1}. {DescribeStep(step)}").ToList();
    }

    private async Task SaveMacroAsync()
    {
        var name = _nameBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            _statusText.Text = "Name is required.";
            return;
        }

        if (_draftSteps.Count == 0)
        {
            _statusText.Text = "Add at least one step.";
            return;
        }

        await _macroManager.CreateAsync(name, _draftSteps.ToList()).ConfigureAwait(true);
        await RefreshMacroListAsync().ConfigureAwait(true);
        CloseEditor();
    }

    private async Task RunSelectedAsync()
    {
        if (_macroList.SelectedIndex < 0 || _macroList.SelectedIndex >= _macros.Count)
            return;

        var macro = _macros[_macroList.SelectedIndex];
        var result = await _runMacro(macro.Id).ConfigureAwait(true);
        _statusText.Text = result.Succeeded ? $"'{macro.Name}' completed." : result.Message ?? "Macro failed.";
    }

    private async Task DeleteSelectedAsync()
    {
        if (_macroList.SelectedIndex < 0 || _macroList.SelectedIndex >= _macros.Count)
            return;

        var macro = _macros[_macroList.SelectedIndex];
        await _macroManager.DeleteAsync(macro.Id).ConfigureAwait(true);
        await RefreshMacroListAsync().ConfigureAwait(true);
    }
}
