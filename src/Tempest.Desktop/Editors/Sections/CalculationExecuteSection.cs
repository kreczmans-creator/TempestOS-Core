using Avalonia.Controls;
using Avalonia.Media;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Desktop.Theming;
using Tempest.Workspace;
using Tempest.Workspace.Calculations;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The Calculations Execute/Recalculate section (`WP 10.7A`) — permanently
/// hidden since `WP 17.9.1` (the raw-JSON Execute box was a developer
/// seam retired from this editor once the calc-sheet editor existed), but
/// left in the tree, moved verbatim from <see cref="ObjectEditorView"/>'s
/// own former <c>PopulateCalculationExecutionAsync</c>/
/// <c>OnExecuteCalculationAsync</c> (`WP 21.1B`) so the command wiring
/// behind it stays untouched, exactly as the pre-split remarks describe.
/// </summary>
/// <remarks>
/// <see cref="AppliesTo"/> answers "would this section's own data logic
/// run" (Kind is Calculation/CalculationSet and a template registry is
/// wired) — <see cref="LoadAsync"/> still forces the Expander's own
/// <c>IsVisible</c> to <see langword="false"/> unconditionally afterwards,
/// exactly as the pre-split method did, since this section is permanently
/// hidden regardless of applicability.
/// </remarks>
internal sealed class CalculationExecuteSection : IEditorSection
{
    private readonly ComboBox _templatePicker = new() { MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _inputJsonBox = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80, FontSize = DesignTokens.FontSizeBody, Watermark = "{ ... }" };
    private readonly Button _executeButton = new() { Content = "Execute", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    private IReadOnlyList<CalculationTemplateDescriptor> _availableTemplates = [];
    private bool _hasBeenExecuted;

    public string Title => "Execute";

    public bool AppliesTo(IEngineeringObject? subject) =>
        _ctx?.CalculationTemplates is not null && _ctx.ObjectKind is "Calculation" or "CalculationSet";

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;

        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(EditorSectionHelpers.LabeledRow("Template", _templatePicker));
        panel.Children.Add(new TextBlock { Text = "Input (JSON):", Opacity = 0.8, FontSize = DesignTokens.FontSizeBody });
        panel.Children.Add(_inputJsonBox);
        panel.Children.Add(_executeButton);
        panel.Children.Add(_statusMessage);

        _executeButton.Classes.Add(ChromeStyles.Primary);
        _executeButton.Click += async (_, _) => await OnExecuteAsync().ConfigureAwait(true);

        _expander = EditorSectionHelpers.BuildSection(Title, panel);
        _expander.IsVisible = false;
        return _expander;
    }

    public async Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        // `WP 17.9.1`: the raw-JSON Execute box is retired from this
        // editor. It was a developer seam — a template picker over a JSON
        // textbox — and the first Windows review of `v0.17.0` met it as
        // the first thing offered on a Calculation. Calculations are run,
        // named and traced in the Engineering Calculations workspace
        // (rail); `WP 18.2A` replaces both surfaces with the calc-sheet
        // editor. The section stays in the tree, hidden, so the command
        // wiring behind it is untouched.
        _expander.IsVisible = false;

        if (!AppliesTo(subject))
            return;

        _availableTemplates = _ctx.CalculationTemplates!.Templates;
        _templatePicker.ItemsSource = _availableTemplates.Select(t => $"{t.Metadata.Name} ({t.CalculationId})").ToList();
        if (_availableTemplates.Count > 0)
            _templatePicker.SelectedIndex = 0;

        _hasBeenExecuted = subject is IHasRelationships hasRelationships
            && (await hasRelationships.GetRelationshipsAsync().ConfigureAwait(true))
                .Any(r => r.RelationshipKind == CalculationTemplateRegistry.CalculatedByRelationshipKind);

        _executeButton.Content = _hasBeenExecuted ? "Recalculate" : "Execute";
        _inputJsonBox.Text = string.Empty;
        _statusMessage.Text = string.Empty;
    }

    private async Task OnExecuteAsync()
    {
        if (_templatePicker.SelectedIndex < 0 || _templatePicker.SelectedIndex >= _availableTemplates.Count)
        {
            _statusMessage.Text = "Choose a Calculation Template first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_inputJsonBox.Text))
        {
            _statusMessage.Text = "Input (JSON) is required.";
            return;
        }

        var calculationId = _availableTemplates[_templatePicker.SelectedIndex].CalculationId;
        IWorkspaceCommand command = _hasBeenExecuted
            ? new RecalculateCalculationCommand(_ctx.ObjectId, _ctx.ObjectKind, calculationId, _inputJsonBox.Text)
            : new ExecuteCalculationCommand(_ctx.ObjectId, _ctx.ObjectKind, calculationId, _inputJsonBox.Text);

        var result = await _ctx.CommandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);
        // Refresh() before the final message — see BillOfMaterialsSection's own identical remarks.
        var message = result.Succeeded ? "Executed." : result.Message ?? "Execution failed.";
        if (result.Succeeded)
            await _ctx.RefreshAsync().ConfigureAwait(true);
        _statusMessage.Text = message;
        _ctx.ReportAction(message, ActionOutcome.From(result.Succeeded));
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}

/// <summary>
/// The Calculation pointer (`WP 17.9.1`) — what a Calculation/CalculationSet
/// shows instead of the retired raw-JSON Execute box, pointing at the
/// Engineering Calculations workspace. Moved verbatim from the pre-split
/// shell's own <c>_calculationPointerSection</c> half of
/// <c>PopulateCalculationExecutionAsync</c> (`WP 21.1B`) — kept as its own
/// class, not folded into <see cref="CalculationExecuteSection"/>, since
/// the two Expanders never shared any state: the guidance text below is
/// static, and this section's own visibility never depended on
/// <see cref="EditorSectionContext.CalculationTemplates"/> being wired,
/// unlike Execute's own applicability.
/// </summary>
internal sealed class CalculationPointerSection : IEditorSection
{
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Calculation";

    public bool AppliesTo(IEngineeringObject? subject) => _ctx?.ObjectKind is "Calculation" or "CalculationSet";

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;
        _expander = EditorSectionHelpers.BuildSection(Title, new StackPanel
        {
            Spacing = DesignTokens.SpaceXs,
            Children = { new TextBlock { Text = ObjectEditorView.CalculationPointerGuidance, TextWrapping = TextWrapping.Wrap, Opacity = 0.85, FontSize = DesignTokens.FontSizeBody } },
        });
        _expander.IsVisible = false;
        return _expander;
    }

    public Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        _expander.IsVisible = AppliesTo(subject);
        return Task.CompletedTask;
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
