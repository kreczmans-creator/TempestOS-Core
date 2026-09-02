using Avalonia;
using Avalonia.Controls;
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
    private readonly Func<Guid, Task<CommandResult>> _runMacro;

    private readonly StackPanel _browsePanel = new();
    private readonly StackPanel _editorPanel = new() { IsVisible = false };

    private readonly ListBox _macroList = new() { MinHeight = 160, MinWidth = 320 };
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
    private readonly List<string> _draftSteps = [];

    /// <summary>Initialises a new instance of the <see cref="MacroManagerDialog"/> class, initially hidden.</summary>
    /// <param name="macroManager">The Macro foundation's own Platform Service.</param>
    /// <param name="commandRegistry">Used to list step-eligible commands (never to invoke — see <paramref name="runMacro"/>).</param>
    /// <param name="runMacro">Runs the macro with the given Id — set by <c>MainWindow</c> to route through <see cref="Tasks.IBackgroundTaskRunner"/>.</param>
    public MacroManagerDialog(IMacroManager macroManager, ICommandRegistry commandRegistry, Func<Guid, Task<CommandResult>> runMacro)
    {
        ArgumentNullException.ThrowIfNull(macroManager);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(runMacro);
        _macroManager = macroManager;
        _commandRegistry = commandRegistry;
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

        BuildBrowsePanel();
        BuildEditorPanel();

        var root = new StackPanel();
        root.Children.Add(new TextBlock { Text = "Macros", FontSize = DesignTokens.FontSizeTitle, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, DesignTokens.SpaceMd) });
        root.Children.Add(_browsePanel);
        root.Children.Add(_editorPanel);
        root.Children.Add(_statusText);
        Child = root;
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

        _addStepButton.Click += (_, _) => AddStep();
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
    }

    private async Task RefreshMacroListAsync()
    {
        _macros = await _macroManager.ListAsync().ConfigureAwait(true);
        _macroList.ItemsSource = _macros.Select(m => $"{m.Name} ({m.StepCommandIds.Count} step(s))").ToList();
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
    }

    /// <summary>
    /// Whether <paramref name="descriptor"/> can be a macro step —
    /// TD-77 Stage 5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A macro is unattended by definition (<c>ADR-0098</c>: an ordered
    /// list of Ids, no branching, no looping, no scripting, no
    /// parameters), so a step must be a command that needs nobody present.
    /// The binding already answers that exactly:
    /// <see cref="CommandBinding.RequiresPrompt"/> is true for a command
    /// declaring values to collect or a confirmation to obtain, and those
    /// are precisely the ones that must never run unattended. Nothing new
    /// decides eligibility, and no list of Ids is maintained here.
    /// </para>
    /// <para>
    /// This used to read <see cref="CommandDescriptor.CreateDefault"/>
    /// alone, which no production discipline command has ever set — so no
    /// real engineering command could be a macro step at all. The
    /// <c>CreateDefault</c> clause remains for the commands that still
    /// work that way.
    /// </para>
    /// <para>
    /// The result is the audited macro-safe set: the thirteen lifecycle
    /// transitions and <c>mechanical.validate-configuration</c>. Every
    /// delete and every duplicate declares a confirmation and is excluded
    /// by that fact, not by being named here.
    /// </para>
    /// </remarks>
    internal static bool IsMacroEligible(CommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.Binding is { } binding
            ? binding is { IsInvocable: true, RequiresPrompt: false }
            : descriptor.CreateDefault is not null;
    }

    private void CloseEditor()
    {
        _editorPanel.IsVisible = false;
        _browsePanel.IsVisible = true;
    }

    private void AddStep()
    {
        if (_availableCommands.SelectedIndex < 0 || _availableCommands.SelectedIndex >= _availableDescriptors.Count)
            return;

        var descriptor = _availableDescriptors[_availableCommands.SelectedIndex];
        _draftSteps.Add(descriptor.Id);
        _steps.ItemsSource = _draftSteps.Select((id, index) => $"{index + 1}. {id}").ToList();
    }

    private void RemoveStep()
    {
        if (_steps.SelectedIndex < 0 || _steps.SelectedIndex >= _draftSteps.Count)
            return;

        _draftSteps.RemoveAt(_steps.SelectedIndex);
        _steps.ItemsSource = _draftSteps.Select((id, index) => $"{index + 1}. {id}").ToList();
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
